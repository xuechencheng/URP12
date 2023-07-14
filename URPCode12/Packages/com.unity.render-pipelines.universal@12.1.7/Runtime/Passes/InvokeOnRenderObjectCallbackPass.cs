namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Invokes OnRenderObject callback
    /// </summary>

    internal class InvokeOnRenderObjectCallbackPass : ScriptableRenderPass
    {
        /// <summary>
        /// Done
        /// </summary>
        public InvokeOnRenderObjectCallbackPass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler(nameof(InvokeOnRenderObjectCallbackPass));
            renderPassEvent = evt;
        }
        /// <summary>
        /// Done
        /// </summary>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            context.InvokeOnRenderObjectCallback();
        }
    }
}
