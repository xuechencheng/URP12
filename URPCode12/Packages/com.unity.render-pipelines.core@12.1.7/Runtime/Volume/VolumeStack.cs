using System;
using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    /// <summary>
    /// Holds the state of a Volume blending update. A global stack is
    /// available by default in <see cref="VolumeManager"/> but you can also create your own using
    /// <see cref="VolumeManager.CreateStack"/> if you need to update the manager with specific
    /// settings and store the results for later use.
    /// </summary>
    public sealed class VolumeStack : IDisposable
    {
        // Holds the state of _all_ component types you can possibly add on volumes
        internal Dictionary<Type, VolumeComponent> components;

        internal VolumeStack()
        {
        }
        /// <summary>
        /// Done 1
        /// </summary>
        internal void Reload(Type[] baseTypes)
        {
            if (components == null)
                components = new Dictionary<Type, VolumeComponent>();
            else
                components.Clear();

            foreach (var type in baseTypes)
            {
                var inst = (VolumeComponent)ScriptableObject.CreateInstance(type);
                components.Add(type, inst);
            }
        }

        /// <summary>
        /// Done 1
        /// </summary>
        public T GetComponent<T>() where T : VolumeComponent
        {
            var comp = GetComponent(typeof(T));
            return (T)comp;
        }

        /// <summary>
        /// Done 1
        /// </summary>
        public VolumeComponent GetComponent(Type type)
        {
            components.TryGetValue(type, out var comp);
            return comp;
        }

        /// <summary>
        /// Done
        /// </summary>
        public void Dispose()
        {
            foreach (var component in components)
                CoreUtils.Destroy(component.Value);
            components.Clear();
        }
    }
}
