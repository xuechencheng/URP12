using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine.Assertions;

namespace UnityEngine.Rendering
{
    using UnityObject = UnityEngine.Object;

    /// <summary>
    /// A global manager that tracks all the Volumes in the currently loaded Scenes and does all the
    /// interpolation work.
    /// </summary>
    public sealed class VolumeManager
    {
        static readonly Lazy<VolumeManager> s_Instance = new Lazy<VolumeManager>(() => new VolumeManager());

        /// <summary>
        /// The current singleton instance of <see cref="VolumeManager"/>.
        /// </summary>
        public static VolumeManager instance => s_Instance.Value;

        /// <summary>
        /// A reference to the main <see cref="VolumeStack"/>.
        /// </summary>
        /// <seealso cref="VolumeStack"/>
        public VolumeStack stack { get; set; }

        /// <summary>
        /// The current list of all available types that derive from <see cref="VolumeComponent"/>.
        /// </summary>
        [Obsolete("Please use baseComponentTypeArray instead.")]
        public IEnumerable<Type> baseComponentTypes
        {
            get => baseComponentTypeArray;
            private set => baseComponentTypeArray = value.ToArray();
        }

        /// <summary>
        /// The current list of all available types that derive from <see cref="VolumeComponent"/>.
        /// </summary>
        public Type[] baseComponentTypeArray { get; private set; }

        // Max amount of layers available in Unity
        const int k_MaxLayerCount = 32;

        // Cached lists of all volumes (sorted by priority) by layer mask
        readonly Dictionary<int, List<Volume>> m_SortedVolumes;

        // Holds all the registered volumes
        readonly List<Volume> m_Volumes;

        // Keep track of sorting states for layer masks
        readonly Dictionary<int, bool> m_SortNeeded;

        // Internal list of default state for each component type - this is used to reset component
        // states on update instead of having to implement a Reset method on all components (which
        // would be error-prone)
        readonly List<VolumeComponent> m_ComponentsDefaultState;

        // Recycled list used for volume traversal
        readonly List<Collider> m_TempColliders;

        // The default stack the volume manager uses.
        // We cache this as users able to change the stack through code and
        // we want to be able to switch to the default one through the ResetMainStack() function.
        VolumeStack m_DefaultStack = null;
        /// <summary>
        /// Done 1
        /// </summary>
        VolumeManager()
        {
            m_SortedVolumes = new Dictionary<int, List<Volume>>();
            m_Volumes = new List<Volume>();
            m_SortNeeded = new Dictionary<int, bool>();
            m_TempColliders = new List<Collider>(8);
            m_ComponentsDefaultState = new List<VolumeComponent>();
            ReloadBaseTypes();
            m_DefaultStack = CreateStack();
            stack = m_DefaultStack;
        }

        /// <summary>
        /// Done 1
        /// </summary>
        public VolumeStack CreateStack()
        {
            var stack = new VolumeStack();
            stack.Reload(baseComponentTypeArray);
            return stack;
        }

        /// <summary>
        /// Done 1
        /// </summary>
        public void ResetMainStack()
        {
            stack = m_DefaultStack;
        }

        /// <summary>
        /// Destroy a Volume Stack
        /// </summary>
        /// <param name="stack">Volume Stack that needs to be destroyed.</param>
        public void DestroyStack(VolumeStack stack)
        {
            stack.Dispose();
        }

        /// <summary>
        /// Done 1
        /// </summary>
        void ReloadBaseTypes()
        {
            m_ComponentsDefaultState.Clear();
            baseComponentTypeArray = CoreUtils.GetAllTypesDerivedFrom<VolumeComponent>().Where(t => !t.IsAbstract).ToArray();
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            foreach (var type in baseComponentTypeArray)
            {
                type.GetMethod("Init", flags)?.Invoke(null, null);
                var inst = (VolumeComponent)ScriptableObject.CreateInstance(type);
                m_ComponentsDefaultState.Add(inst);
            }
        }

        /// <summary>
        /// Registers a new Volume in the manager. Unity does this automatically when a new Volume is
        /// enabled, or its layer changes, but you can use this function to force-register a Volume
        /// that is currently disabled.
        /// </summary>
        /// <param name="volume">The volume to register.</param>
        /// <param name="layer">The LayerMask that this volume is in.</param>
        /// <seealso cref="Unregister"/>
        public void Register(Volume volume, int layer)
        {
            m_Volumes.Add(volume);

            // Look for existing cached layer masks and add it there if needed
            foreach (var kvp in m_SortedVolumes)
            {
                // We add the volume to sorted lists only if the layer match and if it doesn't contain the volume already.
                if ((kvp.Key & (1 << layer)) != 0 && !kvp.Value.Contains(volume))
                    kvp.Value.Add(volume);
            }

            SetLayerDirty(layer);
        }

        /// <summary>
        /// Unregisters a Volume from the manager. Unity does this automatically when a Volume is
        /// disabled or goes out of scope, but you can use this function to force-unregister a Volume
        /// that you added manually while it was disabled.
        /// </summary>
        /// <param name="volume">The Volume to unregister.</param>
        /// <param name="layer">The LayerMask that this Volume is in.</param>
        /// <seealso cref="Register"/>
        public void Unregister(Volume volume, int layer)
        {
            m_Volumes.Remove(volume);

            foreach (var kvp in m_SortedVolumes)
            {
                // Skip layer masks this volume doesn't belong to
                if ((kvp.Key & (1 << layer)) == 0)
                    continue;

                kvp.Value.Remove(volume);
            }
        }

        /// <summary>
        /// Checks if a <see cref="VolumeComponent"/> is active in a given LayerMask.
        /// </summary>
        /// <typeparam name="T">A type derived from <see cref="VolumeComponent"/></typeparam>
        /// <param name="layerMask">The LayerMask to check against</param>
        /// <returns><c>true</c> if the component is active in the LayerMask, <c>false</c>
        /// otherwise.</returns>
        public bool IsComponentActiveInMask<T>(LayerMask layerMask)
            where T : VolumeComponent
        {
            int mask = layerMask.value;

            foreach (var kvp in m_SortedVolumes)
            {
                if (kvp.Key != mask)
                    continue;

                foreach (var volume in kvp.Value)
                {
                    if (!volume.enabled || volume.profileRef == null)
                        continue;

                    if (volume.profileRef.TryGet(out T component) && component.active)
                        return true;
                }
            }

            return false;
        }

        internal void SetLayerDirty(int layer)
        {
            Assert.IsTrue(layer >= 0 && layer <= k_MaxLayerCount, "Invalid layer bit");

            foreach (var kvp in m_SortedVolumes)
            {
                var mask = kvp.Key;

                if ((mask & (1 << layer)) != 0)
                    m_SortNeeded[mask] = true;
            }
        }

        internal void UpdateVolumeLayer(Volume volume, int prevLayer, int newLayer)
        {
            Assert.IsTrue(prevLayer >= 0 && prevLayer <= k_MaxLayerCount, "Invalid layer bit");
            Unregister(volume, prevLayer);
            Register(volume, newLayer);
        }

        /// <summary>
        /// Done 1
        /// </summary>
        void OverrideData(VolumeStack stack, List<VolumeComponent> components, float interpFactor)
        {
            foreach (var component in components)
            {
                if (!component.active)
                    continue;
                var state = stack.GetComponent(component.GetType());
                component.Override(state, interpFactor);
            }
        }


        /// <summary>
        /// Done 1
        /// </summary>
        void ReplaceData(VolumeStack stack, List<VolumeComponent> components)
        {
            foreach (var component in components)
            {
                var target = stack.GetComponent(component.GetType());
                int count = component.parameters.Count;
                for (int i = 0; i < count; i++)
                {
                    if (target.parameters[i] != null)
                    {
                        target.parameters[i].overrideState = false;
                        target.parameters[i].SetValue(component.parameters[i]);
                    }
                }
            }
        }

        /// <summary>
        /// Done 1
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public void CheckBaseTypes()
        {
            if (m_ComponentsDefaultState == null || (m_ComponentsDefaultState.Count > 0 && m_ComponentsDefaultState[0] == null))
                ReloadBaseTypes();
        }

        /// <summary>
        /// Done 1
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        public void CheckStack(VolumeStack stack)
        {
            var components = stack.components;
            if (components == null)
            {
                stack.Reload(baseComponentTypeArray);
                return;
            }
            foreach (var kvp in components)
            {
                if (kvp.Key == null || kvp.Value == null)
                {
                    stack.Reload(baseComponentTypeArray);
                    return;
                }
            }
        }

        /// <summary>
        /// Done 1
        /// </summary>
        public void Update(Transform trigger, LayerMask layerMask)
        {
            Update(stack, trigger, layerMask);
        }

        /// <summary>
        /// Done 1
        /// </summary>
        public void Update(VolumeStack stack, Transform trigger, LayerMask layerMask)
        {
            Assert.IsNotNull(stack);
            CheckBaseTypes();
            CheckStack(stack);
            ReplaceData(stack, m_ComponentsDefaultState);
            bool onlyGlobal = trigger == null;
            var triggerPos = onlyGlobal ? Vector3.zero : trigger.position;
            var volumes = GrabVolumes(layerMask);
            Camera camera = null;
            if (!onlyGlobal)
                trigger.TryGetComponent<Camera>(out camera);
            foreach (var volume in volumes)
            {
                if (volume == null)
                    continue;
#if UNITY_EDITOR
                if (!IsVolumeRenderedByCamera(volume, camera))
                    continue;
#endif
                if (!volume.enabled || volume.profileRef == null || volume.weight <= 0f)
                    continue;
                if (volume.isGlobal)
                {
                    OverrideData(stack, volume.profileRef.components, Mathf.Clamp01(volume.weight));
                    continue;
                }
                if (onlyGlobal)
                    continue;
                var colliders = m_TempColliders;
                volume.GetComponents(colliders);
                if (colliders.Count == 0)
                    continue;
                float closestDistanceSqr = float.PositiveInfinity;
                foreach (var collider in colliders)
                {
                    if (!collider.enabled)
                        continue;
                    var closestPoint = collider.ClosestPoint(triggerPos);
                    var d = (closestPoint - triggerPos).sqrMagnitude;
                    if (d < closestDistanceSqr)
                        closestDistanceSqr = d;
                }
                colliders.Clear();
                float blendDistSqr = volume.blendDistance * volume.blendDistance;
                if (closestDistanceSqr > blendDistSqr)
                    continue;
                float interpFactor = 1f;
                if (blendDistSqr > 0f)
                    interpFactor = 1f - (closestDistanceSqr / blendDistSqr);
                OverrideData(stack, volume.profileRef.components, interpFactor * Mathf.Clamp01(volume.weight));
            }
        }

        /// <summary>
        /// Get all volumes on a given layer mask sorted by influence.
        /// </summary>
        /// <param name="layerMask">The LayerMask that Unity uses to filter Volumes that it should consider.</param>
        /// <returns>An array of volume.</returns>
        public Volume[] GetVolumes(LayerMask layerMask)
        {
            var volumes = GrabVolumes(layerMask);
            volumes.RemoveAll(v => v == null);
            return volumes.ToArray();
        }
        /// <summary>
        /// Done 1
        /// </summary>
        List<Volume> GrabVolumes(LayerMask mask)
        {
            List<Volume> list;
            if (!m_SortedVolumes.TryGetValue(mask, out list))
            {
                list = new List<Volume>();
                foreach (var volume in m_Volumes)
                {
                    if ((mask & (1 << volume.gameObject.layer)) == 0)
                        continue;
                    list.Add(volume);
                    m_SortNeeded[mask] = true;
                }
                m_SortedVolumes.Add(mask, list);
            }
            bool sortNeeded;
            if (m_SortNeeded.TryGetValue(mask, out sortNeeded) && sortNeeded)
            {
                m_SortNeeded[mask] = false;
                SortByPriority(list);
            }
            return list;
        }

        /// <summary>
        /// Done 1
        /// </summary>
        static void SortByPriority(List<Volume> volumes)
        {
            Assert.IsNotNull(volumes, "Trying to sort volumes of non-initialized layer");
            for (int i = 1; i < volumes.Count; i++)
            {
                var temp = volumes[i];
                int j = i - 1;
                // Sort order is ascending
                while (j >= 0 && volumes[j].priority > temp.priority)
                {
                    volumes[j + 1] = volumes[j];
                    j--;
                }
                volumes[j + 1] = temp;
            }
        }
        /// <summary>
        /// Done 1
        /// </summary>
        static bool IsVolumeRenderedByCamera(Volume volume, Camera camera)
        {
#if UNITY_2018_3_OR_NEWER && UNITY_EDITOR
            // IsGameObjectRenderedByCamera does not behave correctly when camera is null so we have to catch it here.
            return camera == null ? true : UnityEditor.SceneManagement.StageUtility.IsGameObjectRenderedByCamera(volume.gameObject, camera);
#else
            return true;
#endif
        }
    }

    /// <summary>
    /// A scope in which a Camera filters a Volume.
    /// </summary>
    [Obsolete("VolumeIsolationScope is deprecated, it does not have any effect anymore.")]
    public struct VolumeIsolationScope : IDisposable
    {
        /// <summary>
        /// Constructs a scope in which a Camera filters a Volume.
        /// </summary>
        /// <param name="unused">Unused parameter.</param>
        public VolumeIsolationScope(bool unused) { }

        /// <summary>
        /// Stops the Camera from filtering a Volume.
        /// </summary>
        void IDisposable.Dispose() { }
    }
}
