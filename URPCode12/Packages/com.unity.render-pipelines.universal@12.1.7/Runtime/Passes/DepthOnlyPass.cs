using System;
using UnityEngine.Experimental.Rendering;

namespace UnityEngine.Rendering.Universal.Internal
{
    public class DepthOnlyPass : ScriptableRenderPass
    {
        private static readonly ShaderTagId k_ShaderTagId = new ShaderTagId("DepthOnly");
        private RenderTargetHandle depthAttachmentHandle { get; set; }
        internal RenderTextureDescriptor descriptor { get; set; }
        internal bool allocateDepth { get; set; } = true;
        internal ShaderTagId shaderTagId { get; set; } = k_ShaderTagId;

        FilteringSettings m_FilteringSettings;
        /// <summary>
        /// Done 1
        /// </summary>
        public DepthOnlyPass(RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask)
        {
            base.profilingSampler = new ProfilingSampler(nameof(DepthOnlyPass));
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            renderPassEvent = evt;
            useNativeRenderPass = false;
        }

        /// <summary>
        /// Done 1
        /// </summary>
        public void Setup( RenderTextureDescriptor baseDescriptor, RenderTargetHandle depthAttachmentHandle)
        {
            this.depthAttachmentHandle = depthAttachmentHandle;
            baseDescriptor.colorFormat = RenderTextureFormat.Depth;
            baseDescriptor.depthBufferBits = UniversalRenderer.k_DepthStencilBufferBits;
            // Depth-Only pass don't use MSAA
            baseDescriptor.msaaSamples = 1;
            descriptor = baseDescriptor;
            this.allocateDepth = true;
            this.shaderTagId = k_ShaderTagId;
        }
        /// <summary>
        /// Done 1
        /// </summary>
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (this.allocateDepth)
                cmd.GetTemporaryRT(depthAttachmentHandle.id, descriptor, FilterMode.Point);
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            if (renderingData.cameraData.renderer.useDepthPriming && (renderingData.cameraData.renderType == CameraRenderType.Base || renderingData.cameraData.clearDepth))
            {
                ConfigureTarget(renderingData.cameraData.renderer.cameraDepthTarget, descriptor.depthStencilFormat, desc.width, desc.height, 1, true);
            }
            else
            {
                ConfigureTarget(new RenderTargetIdentifier(depthAttachmentHandle.Identifier(), 0, CubemapFace.Unknown, -1), descriptor.depthStencilFormat, desc.width, desc.height, 1, true);
            }
            ConfigureClear(ClearFlag.Depth, Color.black);
        }
        /// <summary>
        /// Done 1
        /// </summary>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.DepthPrepass)))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawingSettings(this.shaderTagId, ref renderingData, sortFlags);
                drawSettings.perObjectData = PerObjectData.None;
                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref m_FilteringSettings);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
        /// <summary>
        /// Done 1
        /// </summary>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");
            if (depthAttachmentHandle != RenderTargetHandle.CameraTarget)
            {
                if (this.allocateDepth)
                    cmd.ReleaseTemporaryRT(depthAttachmentHandle.id);
                depthAttachmentHandle = RenderTargetHandle.CameraTarget;
            }
        }
    }
}
