namespace UnityEngine.Rendering.Universal
{
    internal class CapturePass : ScriptableRenderPass
    {
        RenderTargetHandle m_CameraColorHandle;
        const string m_ProfilerTag = "Capture Pass";
        private static readonly ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
        public CapturePass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler(nameof(CapturePass));
            renderPassEvent = evt;
        }

        /// <summary>
        /// Done 1
        /// </summary>
        public void Setup(RenderTargetHandle colorHandle)
        {
            m_CameraColorHandle = colorHandle;
        }

        /// <summary>
        /// Done 1
        /// </summary>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmdBuf = CommandBufferPool.Get();
            using (new ProfilingScope(cmdBuf, m_ProfilingSampler))
            {
                var colorAttachmentIdentifier = m_CameraColorHandle.Identifier();
                var captureActions = renderingData.cameraData.captureActions;
                for (captureActions.Reset(); captureActions.MoveNext();)
                    captureActions.Current(colorAttachmentIdentifier, cmdBuf);
            }

            context.ExecuteCommandBuffer(cmdBuf);
            CommandBufferPool.Release(cmdBuf);
        }
    }
}
