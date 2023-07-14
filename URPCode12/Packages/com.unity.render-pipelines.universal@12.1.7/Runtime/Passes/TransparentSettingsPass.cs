namespace UnityEngine.Rendering.Universal
{
    /// <summary>
    /// Applies relevant settings before rendering transparent objects
    /// </summary>

    internal class TransparentSettingsPass : ScriptableRenderPass
    {
        bool m_shouldReceiveShadows;

        const string m_ProfilerTag = "Transparent Settings Pass";
        private static readonly ProfilingSampler m_ProfilingSampler = new ProfilingSampler(m_ProfilerTag);
        /// <summary>
        /// Done
        /// </summary>
        public TransparentSettingsPass(RenderPassEvent evt, bool shadowReceiveSupported)
        {
            base.profilingSampler = new ProfilingSampler(nameof(TransparentSettingsPass));
            renderPassEvent = evt;
            m_shouldReceiveShadows = shadowReceiveSupported;
        }
        /// <summary>
        /// Done
        /// </summary>
        public bool Setup(ref RenderingData renderingData)
        {
            return !m_shouldReceiveShadows;
        }
        /// <summary>
        /// Done
        /// </summary>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Get a command buffer...
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                // Toggle light shadows enabled based on the renderer setting set in the constructor
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, m_shouldReceiveShadows);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, m_shouldReceiveShadows);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.AdditionalLightShadows, m_shouldReceiveShadows);
            }
            // Execute and release the command buffer...
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
