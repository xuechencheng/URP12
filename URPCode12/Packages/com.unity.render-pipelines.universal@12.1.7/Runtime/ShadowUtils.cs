using System;

namespace UnityEngine.Rendering.Universal
{
    public struct ShadowSliceData
    {
        public Matrix4x4 viewMatrix;
        public Matrix4x4 projectionMatrix;
        public Matrix4x4 shadowTransform;
        public int offsetX;
        public int offsetY;
        public int resolution;
        public ShadowSplitData splitData; // splitData contains culling information
        /// <summary>
        /// Done
        /// </summary>
        public void Clear()
        {
            viewMatrix = Matrix4x4.identity;
            projectionMatrix = Matrix4x4.identity;
            shadowTransform = Matrix4x4.identity;
            offsetX = offsetY = 0;
            resolution = 1024;
        }
    }

    public static class ShadowUtils
    {
        private static readonly bool m_ForceShadowPointSampling;

        static ShadowUtils()
        {
            m_ForceShadowPointSampling = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Metal &&
                GraphicsSettings.HasShaderDefine(Graphics.activeTier, BuiltinShaderDefine.UNITY_METAL_SHADOWS_USE_POINT_FILTERING);
        }

        public static bool ExtractDirectionalLightMatrix(ref CullingResults cullResults, ref ShadowData shadowData, int shadowLightIndex, int cascadeIndex, int shadowmapWidth, int shadowmapHeight, int shadowResolution, float shadowNearPlane, out Vector4 cascadeSplitDistance, out ShadowSliceData shadowSliceData, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix)
        {
            bool result = ExtractDirectionalLightMatrix(ref cullResults, ref shadowData, shadowLightIndex, cascadeIndex, shadowmapWidth, shadowmapHeight, shadowResolution, shadowNearPlane, out cascadeSplitDistance, out shadowSliceData);
            viewMatrix = shadowSliceData.viewMatrix;
            projMatrix = shadowSliceData.projectionMatrix;
            return result;
        }
        /// <summary>
        /// Done1 空间变换
        /// </summary>
        public static bool ExtractDirectionalLightMatrix(ref CullingResults cullResults, ref ShadowData shadowData, int shadowLightIndex, int cascadeIndex, int shadowmapWidth, int shadowmapHeight, int shadowResolution, float shadowNearPlane, out Vector4 cascadeSplitDistance, out ShadowSliceData shadowSliceData)
        {
            bool success = cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(shadowLightIndex,
                cascadeIndex, shadowData.mainLightShadowCascadesCount, shadowData.mainLightShadowCascadesSplit, shadowResolution, shadowNearPlane, out shadowSliceData.viewMatrix, out shadowSliceData.projectionMatrix, out shadowSliceData.splitData);
            cascadeSplitDistance = shadowSliceData.splitData.cullingSphere;//cullSphere (球心, 半径)
            shadowSliceData.offsetX = (cascadeIndex % 2) * shadowResolution;
            shadowSliceData.offsetY = (cascadeIndex / 2) * shadowResolution;
            shadowSliceData.resolution = shadowResolution;
            shadowSliceData.shadowTransform = GetShadowTransform(shadowSliceData.projectionMatrix, shadowSliceData.viewMatrix);
            shadowSliceData.splitData.shadowCascadeBlendCullingFactor = 1.0f;
            if (shadowData.mainLightShadowCascadesCount > 1)
                ApplySliceTransform(ref shadowSliceData, shadowmapWidth, shadowmapHeight);
            return success;
        }

        public static bool ExtractSpotLightMatrix(ref CullingResults cullResults, ref ShadowData shadowData, int shadowLightIndex, out Matrix4x4 shadowMatrix, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData)
        {
            bool success = cullResults.ComputeSpotShadowMatricesAndCullingPrimitives(shadowLightIndex, out viewMatrix, out projMatrix, out splitData); // returns false if input parameters are incorrect (rare)
            shadowMatrix = GetShadowTransform(projMatrix, viewMatrix);
            return success;
        }

        public static bool ExtractPointLightMatrix(ref CullingResults cullResults, ref ShadowData shadowData, int shadowLightIndex, CubemapFace cubemapFace, float fovBias, out Matrix4x4 shadowMatrix, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData)
        {
            bool success = cullResults.ComputePointShadowMatricesAndCullingPrimitives(shadowLightIndex, cubemapFace, fovBias, out viewMatrix, out projMatrix, out splitData); // returns false if input parameters are incorrect (rare)

            // In native API CullingResults.ComputeSpotShadowMatricesAndCullingPrimitives there is code that inverts the 3rd component of shadow-casting spot light's "world-to-local" matrix (it was so since its original addition to the code base):
            // https://github.cds.internal.unity3d.com/unity/unity/commit/34813e063526c4be0ef0448dfaae3a911dd8be58#diff-cf0b417fc6bd8ee2356770797e628cd4R331
            // (the same transformation has also always been used in the Built-In Render Pipeline)
            //
            // However native API CullingResults.ComputePointShadowMatricesAndCullingPrimitives does not contain this transformation.
            // As a result, the view matrices returned for a point light shadow face, and for a spot light with same direction as that face, have opposite 3rd component.
            //
            // This causes normalBias to be incorrectly applied to shadow caster vertices during the point light shadow pass.
            // To counter this effect, we invert the point light shadow view matrix component here:
            {
                viewMatrix.m10 = -viewMatrix.m10;
                viewMatrix.m11 = -viewMatrix.m11;
                viewMatrix.m12 = -viewMatrix.m12;
                viewMatrix.m13 = -viewMatrix.m13;
            }

            shadowMatrix = GetShadowTransform(projMatrix, viewMatrix);
            return success;
        }
        /// <summary>
        /// Done 1
        /// </summary>
        public static void RenderShadowSlice(CommandBuffer cmd, ref ScriptableRenderContext context, ref ShadowSliceData shadowSliceData, ref ShadowDrawingSettings settings, Matrix4x4 proj, Matrix4x4 view)
        {
            cmd.SetGlobalDepthBias(1.0f, 2.5f); 
            cmd.SetViewport(new Rect(shadowSliceData.offsetX, shadowSliceData.offsetY, shadowSliceData.resolution, shadowSliceData.resolution));
            cmd.SetViewProjectionMatrices(view, proj);
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            context.DrawShadows(ref settings);
            cmd.DisableScissorRect();
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            cmd.SetGlobalDepthBias(0.0f, 0.0f); // Restore previous depth bias values
        }

        public static void RenderShadowSlice(CommandBuffer cmd, ref ScriptableRenderContext context,
            ref ShadowSliceData shadowSliceData, ref ShadowDrawingSettings settings)
        {
            RenderShadowSlice(cmd, ref context, ref shadowSliceData, ref settings,
                shadowSliceData.projectionMatrix, shadowSliceData.viewMatrix);
        }
        /// <summary>
        /// Done1 计算每个级联的分辨率
        /// </summary>
        public static int GetMaxTileResolutionInAtlas(int atlasWidth, int atlasHeight, int tileCount)
        {
            int resolution = Mathf.Min(atlasWidth, atlasHeight);
            int currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            while (currentTileCount < tileCount)
            {
                resolution = resolution >> 1;
                currentTileCount = atlasWidth / resolution * atlasHeight / resolution;
            }
            return resolution;//正方形
        }
        /// <summary>
        /// Done1 级联偏移
        /// </summary>
        public static void ApplySliceTransform(ref ShadowSliceData shadowSliceData, int atlasWidth, int atlasHeight)
        {
            Matrix4x4 sliceTransform = Matrix4x4.identity;
            float oneOverAtlasWidth = 1.0f / atlasWidth;
            float oneOverAtlasHeight = 1.0f / atlasHeight;
            sliceTransform.m00 = shadowSliceData.resolution * oneOverAtlasWidth;
            sliceTransform.m11 = shadowSliceData.resolution * oneOverAtlasHeight;
            sliceTransform.m03 = shadowSliceData.offsetX * oneOverAtlasWidth;
            sliceTransform.m13 = shadowSliceData.offsetY * oneOverAtlasHeight;
            shadowSliceData.shadowTransform = sliceTransform * shadowSliceData.shadowTransform;
        }
        /// <summary>
        /// Done 设置深度偏移和法线偏移
        /// </summary>
        public static Vector4 GetShadowBias(ref VisibleLight shadowLight, int shadowLightIndex, ref ShadowData shadowData, Matrix4x4 lightProjectionMatrix, float shadowResolution)
        {
            if (shadowLightIndex < 0 || shadowLightIndex >= shadowData.bias.Count)
            {
                Debug.LogWarning(string.Format("{0} is not a valid light index.", shadowLightIndex));
                return Vector4.zero;
            }
            float frustumSize;
            if (shadowLight.lightType == LightType.Directional)
            {
                frustumSize = 2.0f / lightProjectionMatrix.m00;//cot(FOV/2) / Aspect ???
            }
            else if (shadowLight.lightType == LightType.Spot)
            {
                frustumSize = Mathf.Tan(shadowLight.spotAngle * 0.5f * Mathf.Deg2Rad) * shadowLight.range; // half-width (in world-space units) of shadow frustum's "far plane"
            }
            else if (shadowLight.lightType == LightType.Point)
            {
                float fovBias = Internal.AdditionalLightsShadowCasterPass.GetPointLightShadowFrustumFovBiasInDegrees((int)shadowResolution, (shadowLight.light.shadows == LightShadows.Soft));
                float cubeFaceAngle = 90 + fovBias;
                frustumSize = Mathf.Tan(cubeFaceAngle * 0.5f * Mathf.Deg2Rad) * shadowLight.range; // half-width (in world-space units) of shadow frustum's "far plane"
            }
            else
            {
                Debug.LogWarning("Only point, spot and directional shadow casters are supported in universal pipeline");
                frustumSize = 0.0f;
            }
            float texelSize = frustumSize / shadowResolution;
            float depthBias = -shadowData.bias[shadowLightIndex].x * texelSize;
            float normalBias = -shadowData.bias[shadowLightIndex].y * texelSize;
            if (shadowLight.lightType == LightType.Point)
                normalBias = 0.0f;
            if (shadowData.supportsSoftShadows && shadowLight.light.shadows == LightShadows.Soft)
            {
                const float kernelRadius = 2.5f;
                depthBias *= kernelRadius;
                normalBias *= kernelRadius;
            }
            return new Vector4(depthBias, normalBias, 0.0f, 0.0f);
        }

        /// <summary>
        /// Extract scale and bias from a fade distance to achieve a linear fading of the fade distance. ???
        /// </summary>
        /// <param name="fadeDistance">Distance at which object should be totally fade</param>
        /// <param name="border">Normalized distance of fade</param>
        /// <param name="scale">[OUT] Slope of the fading on the fading part</param>
        /// <param name="bias">[OUT] Ordinate of the fading part at abscissa 0</param>
        internal static void GetScaleAndBiasForLinearDistanceFade(float fadeDistance, float border, out float scale, out float bias)
        {
            if (border < 0.0001f)
            {
                float multiplier = 1000f;
                scale = multiplier;
                bias = -fadeDistance * multiplier;
                return;
            }
            border = 1 - border;
            border *= border;
            // Fade with distance calculation is just a linear fade from 90% of fade distance to fade distance. 90% arbitrarily chosen but should work well enough.
            float distanceFadeNear = border * fadeDistance;
            scale = 1.0f / (fadeDistance - distanceFadeNear);
            bias = -distanceFadeNear / (fadeDistance - distanceFadeNear);
        }
        /// <summary>
        /// Done 1
        /// </summary>
        public static void SetupShadowCasterConstantBuffer(CommandBuffer cmd, ref VisibleLight shadowLight, Vector4 shadowBias)
        {
            cmd.SetGlobalVector("_ShadowBias", shadowBias);
            Vector3 lightDirection = -shadowLight.localToWorldMatrix.GetColumn(2);
            cmd.SetGlobalVector("_LightDirection", new Vector4(lightDirection.x, lightDirection.y, lightDirection.z, 0.0f));
            Vector3 lightPosition = shadowLight.localToWorldMatrix.GetColumn(3);
            cmd.SetGlobalVector("_LightPosition", new Vector4(lightPosition.x, lightPosition.y, lightPosition.z, 1.0f));
        }
        /// <summary>
        /// Done 1
        /// </summary>
        public static RenderTexture GetTemporaryShadowTexture(int width, int height, int bits)
        {
            var format = Experimental.Rendering.GraphicsFormatUtility.GetDepthStencilFormat(bits, 0);
            RenderTextureDescriptor rtd = new RenderTextureDescriptor(width, height, Experimental.Rendering.GraphicsFormat.None, format);
            rtd.shadowSamplingMode = (RenderingUtils.SupportsRenderTextureFormat(RenderTextureFormat.Shadowmap)
                && (SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2)) ?
                ShadowSamplingMode.CompareDepths : ShadowSamplingMode.None;
            var shadowTexture = RenderTexture.GetTemporary(rtd);
            shadowTexture.filterMode = m_ForceShadowPointSampling ? FilterMode.Point : FilterMode.Bilinear;
            shadowTexture.wrapMode = TextureWrapMode.Clamp;
            return shadowTexture;
        }
        /// <summary>
        /// Done1 1,裁剪空间 2,规划到[0,1]
        /// </summary>
        static Matrix4x4 GetShadowTransform(Matrix4x4 proj, Matrix4x4 view)
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                proj.m20 = -proj.m20;
                proj.m21 = -proj.m21;
                proj.m22 = -proj.m22;
                proj.m23 = -proj.m23;
            }
            Matrix4x4 worldToShadow = proj * view;
            var textureScaleAndBias = Matrix4x4.identity;
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;//x * 0.5 + 0.5, [-1,1] to [0,1]
            return textureScaleAndBias * worldToShadow;
        }
    }
}
