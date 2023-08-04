#ifndef UNIVERSAL_SSAO_INCLUDED
#define UNIVERSAL_SSAO_INCLUDED

// Includes
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

// Textures & Samplers
TEXTURE2D_X(_BaseMap);
TEXTURE2D_X(_ScreenSpaceOcclusionTexture);

SAMPLER(sampler_BaseMap);
SAMPLER(sampler_ScreenSpaceOcclusionTexture);

// Params
half4 _SSAOParams;
half4 _CameraViewTopLeftCorner[2];
half4x4 _CameraViewProjections[2]; // This is different from UNITY_MATRIX_VP (platform-agnostic projection matrix is used). Handle both non-XR and XR modes.

float4 _SourceSize;
float4 _ProjectionParams2;
float4 _CameraViewXExtent[2];
float4 _CameraViewYExtent[2];
float4 _CameraViewZExtent[2];

// Hardcoded random UV values that improves performance.
// The values were taken from this function:
// r = frac(43758.5453 * sin( dot(float2(12.9898, 78.233), uv)) ));
// Indices  0 to 19 are for u = 0.0
// Indices 20 to 39 are for u = 1.0
static half SSAORandomUV[40] =
{
    0.00000000,  // 00
    0.33984375,  // 01
    0.75390625,  // 02
    0.56640625,  // 03
    0.98437500,  // 04
    0.07421875,  // 05
    0.23828125,  // 06
    0.64062500,  // 07
    0.35937500,  // 08
    0.50781250,  // 09
    0.38281250,  // 10
    0.98437500,  // 11
    0.17578125,  // 12
    0.53906250,  // 13
    0.28515625,  // 14
    0.23137260,  // 15
    0.45882360,  // 16
    0.54117650,  // 17
    0.12941180,  // 18
    0.64313730,  // 19

    0.92968750,  // 20
    0.76171875,  // 21
    0.13333330,  // 22
    0.01562500,  // 23
    0.00000000,  // 24
    0.10546875,  // 25
    0.64062500,  // 26
    0.74609375,  // 27
    0.67968750,  // 28
    0.35156250,  // 29
    0.49218750,  // 30
    0.12500000,  // 31
    0.26562500,  // 32
    0.62500000,  // 33
    0.44531250,  // 34
    0.17647060,  // 35
    0.44705890,  // 36
    0.93333340,  // 37
    0.87058830,  // 38
    0.56862750,  // 39
};

// SSAO Settings
#define INTENSITY _SSAOParams.x
#define RADIUS _SSAOParams.y
#define DOWNSAMPLE _SSAOParams.z //1.0f / downsampleDivider

// GLES2: In many cases, dynamic looping is not supported.
#if defined(SHADER_API_GLES) && !defined(SHADER_API_GLES3)
    #define SAMPLE_COUNT 3
#else
    #define SAMPLE_COUNT int(_SSAOParams.w)
#endif

// Function defines
#define SCREEN_PARAMS        GetScaledScreenParams()//new Vector4(scaledCameraWidth, scaledCameraHeight, 1.0f + 1.0f / scaledCameraWidth, 1.0f + 1.0f / scaledCameraHeight)
#define SAMPLE_BASEMAP(uv)   SAMPLE_TEXTURE2D_X(_BaseMap, sampler_BaseMap, UnityStereoTransformScreenSpaceTex(uv));

// Constants
// kContrast determines the contrast of occlusion. This allows users to control over/under
// occlusion. At the moment, this is not exposed to the editor because it's rarely useful.
// The range is between 0 and 1.
static const half kContrast = half(0.5);

// The constant below controls the geometry-awareness of the bilateral
// filter. The higher value, the more sensitive it is.
static const half kGeometryCoeff = half(0.8);

// The constants below are used in the AO estimator. Beta is mainly used for suppressing
// self-shadowing noise, and Epsilon is used to prevent calculation underflow. See the paper
// (Morgan 2011 https://casual-effects.com/research/McGuire2011AlchemyAO/index.html)
// for further details of these constants.
static const half kBeta = half(0.002);
static const half kEpsilon = half(0.0001);

#if defined(USING_STEREO_MATRICES)
    #define unity_eyeIndex unity_StereoEyeIndex
#else
    #define unity_eyeIndex 0
#endif
// Done 1
half4 PackAONormal(half ao, half3 n)
{
    return half4(ao, n * half(0.5) + half(0.5));
}
// Done 1
half3 GetPackedNormal(half4 p)
{
    return p.gba * half(2.0) - half(1.0);
}
// Done
half GetPackedAO(half4 p)
{
    return p.r;
}

half EncodeAO(half x)
{
    #if UNITY_COLORSPACE_GAMMA
        return half(1.0 - max(LinearToSRGB(1.0 - saturate(x)), 0.0));
    #else
        return x;
    #endif
}
// Done 1
half CompareNormal(half3 d1, half3 d2)
{
    //SmoothStep(float from, float to, float t)
    return smoothstep(kGeometryCoeff, half(1.0), dot(d1, d2));//kGeometryCoeff = half(0.8)
}

// Trigonometric function utility
half2 CosSin(half theta)
{
    half sn, cs;
    sincos(theta, sn, cs);
    return half2(cs, sn);
}

// Done 2
//[0, 1]的伪随机数
half GetRandomUVForSSAO(float u, int sampleIndex)
{
    return SSAORandomUV[u * 20 + sampleIndex];
}

// Done 2
float2 GetScreenSpacePosition(float2 uv)
{
    return float2(uv * SCREEN_PARAMS.xy * DOWNSAMPLE);
}

// Done 2
// 生成一个方向随机的单位向量
half3 PickSamplePoint(float2 uv, int sampleIndex)
{
    const float2 positionSS = GetScreenSpacePosition(uv);
    const half gn = half(InterleavedGradientNoise(positionSS, sampleIndex));//[0-1]的noise
    const half u = frac(GetRandomUVForSSAO(half(0.0), sampleIndex) + gn) * half(2.0) - half(1.0);//[-1,1]
    const half theta = (GetRandomUVForSSAO(half(1.0), sampleIndex) + gn) * half(TWO_PI);//[0, 4π]
    return half3(CosSin(theta) * sqrt(half(1.0) - u * u), u);//单位向量
}

// Done
float SampleAndGetLinearEyeDepth(float2 uv)
{
    float rawDepth = SampleSceneDepth(uv.xy);
    #if defined(_ORTHOGRAPHIC)
        return LinearDepthToEyeDepth(rawDepth);
    #else
        return LinearEyeDepth(rawDepth, _ZBufferParams);
    #endif
}

// Done
//视野空间坐标 可以想象成世界空间内的相机坐标为起点，片元坐标为终点的向量
half3 ReconstructViewPos(float2 uv, float depth)
{
    uv.y = 1.0 - uv.y;
    #if defined(_ORTHOGRAPHIC)
        float zScale = depth * _ProjectionParams.w; // divide by far plane
        float3 viewPos = _CameraViewTopLeftCorner[unity_eyeIndex].xyz + _CameraViewXExtent[unity_eyeIndex].xyz * uv.x + _CameraViewYExtent[unity_eyeIndex].xyz * uv.y + _CameraViewZExtent[unity_eyeIndex].xyz * zScale;
    #else
        float zScale = depth * _ProjectionParams2.x; // divide by near plane  -- 1.0f / renderingData.cameraData.camera.nearClipPlane
        float3 viewPos = _CameraViewTopLeftCorner[unity_eyeIndex].xyz + _CameraViewXExtent[unity_eyeIndex].xyz * uv.x + _CameraViewYExtent[unity_eyeIndex].xyz * uv.y;
        viewPos *= zScale;
    #endif
    return half3(viewPos);
}

// Done
// 重新构建法线
half3 ReconstructNormal(float2 uv, float depth, float3 vpos)
{
    #if defined(_RECONSTRUCT_NORMAL_LOW)
        return half3(normalize(cross(ddy(vpos), ddx(vpos))));
    #else
        float2 delta = float2(_SourceSize.zw * 2.0);
        float2 lUV = float2(-delta.x, 0.0);
        float2 rUV = float2( delta.x, 0.0);
        float2 uUV = float2(0.0,  delta.y);
        float2 dUV = float2(0.0, -delta.y);
        float3 l1 = float3(uv + lUV, 0.0); l1.z = SampleAndGetLinearEyeDepth(l1.xy); // Left1
        float3 r1 = float3(uv + rUV, 0.0); r1.z = SampleAndGetLinearEyeDepth(r1.xy); // Right1
        float3 u1 = float3(uv + uUV, 0.0); u1.z = SampleAndGetLinearEyeDepth(u1.xy); // Up1
        float3 d1 = float3(uv + dUV, 0.0); d1.z = SampleAndGetLinearEyeDepth(d1.xy); // Down1
        #if defined(_RECONSTRUCT_NORMAL_MEDIUM)
             uint closest_horizontal = l1.z > r1.z ? 0 : 1;//选深度值大的点
             uint closest_vertical   = d1.z > u1.z ? 0 : 1;//(h,v)--(p1,p2) (0,0)--(l1,d1) (0,1)--(u1,l1) (1,0)--(d1,r1) (1,1)--(r1,u1)
        #else
            float3 l2 = float3(uv + lUV * 2.0, 0.0); l2.z = SampleAndGetLinearEyeDepth(l2.xy); // Left2
            float3 r2 = float3(uv + rUV * 2.0, 0.0); r2.z = SampleAndGetLinearEyeDepth(r2.xy); // Right2
            float3 u2 = float3(uv + uUV * 2.0, 0.0); u2.z = SampleAndGetLinearEyeDepth(u2.xy); // Up2
            float3 d2 = float3(uv + dUV * 2.0, 0.0); d2.z = SampleAndGetLinearEyeDepth(d2.xy); // Down2
            const uint closest_horizontal = abs( (2.0 * l1.z - l2.z) - depth) < abs( (2.0 * r1.z - r2.z) - depth) ? 0 : 1;
            const uint closest_vertical   = abs( (2.0 * d1.z - d2.z) - depth) < abs( (2.0 * u1.z - u2.z) - depth) ? 0 : 1;//选取深度值变化小的点
        #endif
        float3 P1;
        float3 P2;
        if (closest_vertical == 0)
        {
            P1 = closest_horizontal == 0 ? l1 : d1;
            P2 = closest_horizontal == 0 ? d1 : r1;
        }
        else
        {
            P1 = closest_horizontal == 0 ? u1 : r1;
            P2 = closest_horizontal == 0 ? l1 : u1;
        }
        return half3(normalize(cross(ReconstructViewPos(P2.xy, P2.z) - vpos, ReconstructViewPos(P1.xy, P1.z) - vpos)));
    #endif
}

// Done 2
half3 SampleNormal(float2 uv)
{
    #if defined(_SOURCE_DEPTH_NORMALS)
        return half3(SampleSceneNormals(uv));
    #else
        float depth = SampleAndGetLinearEyeDepth(uv);
        half3 vpos = ReconstructViewPos(uv, depth);
        return ReconstructNormal(uv, depth, vpos);
    #endif
}
// Done
void SampleDepthNormalView(float2 uv, out float depth, out half3 normal, out half3 vpos)
{
    depth  = SampleAndGetLinearEyeDepth(uv);
    vpos   = ReconstructViewPos(uv, depth);
    #if defined(_SOURCE_DEPTH_NORMALS)
        normal = half3(SampleSceneNormals(uv));
    #else
        normal = ReconstructNormal(uv, depth, vpos);
    #endif
}

// Done 2
half4 SSAO(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float2 uv = input.uv;
    half3x3 camTransform = (half3x3)_CameraViewProjections[unity_eyeIndex];
    float depth_o; half3 norm_o; half3 vpos_o;
    SampleDepthNormalView(uv, depth_o, norm_o, vpos_o);//depth_o线性深度 norm_o单位长度的世界空间法线 vpos_o视野空间坐标
    const half rcpSampleCount = half(rcp(SAMPLE_COUNT));//SAMPLE_COUNT [4,20]
    half ao = 0.0;
    for (int s = 0; s < SAMPLE_COUNT; s++)
    {
        half3 v_s1 = PickSamplePoint(uv, s);
        v_s1 *= sqrt((half(s) + half(1.0)) * rcpSampleCount) * RADIUS;//SAMPLE_COUNT = 4，sqrt(1/4) sqrt(2/4) sqrt(3/4) sqrt(1)
        //保证v_s1和norm_o同向
        v_s1 = faceforward(v_s1, -norm_o, v_s1);//如果第二个参数和第三个参数的点积小于0，返回值为第一个参数，否则返回第一个参数乘以-1。
        half3 vpos_s1 = vpos_o + v_s1;//取周围的一个点
        half3 spos_s1 = mul(camTransform, vpos_s1);//世界空间到齐次空间
        #if defined(_ORTHOGRAPHIC)
            float2 uv_s1_01 = clamp((spos_s1.xy + float(1.0)) * float(0.5), float(0.0), float(1.0));
        #else
            float zdist = -dot(UNITY_MATRIX_V[2].xyz, vpos_s1);// ???
            float2 uv_s1_01 = clamp((spos_s1.xy * rcp(zdist) + float(1.0)) * float(0.5), float(0.0), float(1.0));
        #endif
        float depth_s1 = SampleAndGetLinearEyeDepth(uv_s1_01);
        half3 vpos_s2 = ReconstructViewPos(uv_s1_01, depth_s1);//vpos_s1的遮挡点
        half3 v_s2 = vpos_s2 - vpos_o;//从片元指向遮挡点的向量，非单位长度
        half dotVal = dot(v_s2, norm_o);//阻挡值
        #if defined(_ORTHOGRAPHIC)
            dotVal -= half(2.0 * kBeta * depth_o);
        #else
            dotVal -= half(kBeta * depth_o);//kBeta = half(0.002)
        #endif
        half a1 = max(dotVal, half(0.0));
        half a2 = dot(v_s2, v_s2) + kEpsilon;//kEpsilon = half(0.0001) 向量长度的平方
        ao += a1 * rcp(a2);//cos / |V|
    }
    ao *= RADIUS;
    ao = PositivePow(ao * INTENSITY * rcpSampleCount, kContrast);//kContrast = half(0.5)
    return PackAONormal(ao, norm_o);
}

// Done 2 带法线接近程度的模糊
half4 Blur(float2 uv, float2 delta) : SV_Target
{
    half4 p0 =  (half4) SAMPLE_BASEMAP(uv                 );
    half4 p1a = (half4) SAMPLE_BASEMAP(uv - delta * 1.3846153846);
    half4 p1b = (half4) SAMPLE_BASEMAP(uv + delta * 1.3846153846);
    half4 p2a = (half4) SAMPLE_BASEMAP(uv - delta * 3.2307692308);
    half4 p2b = (half4) SAMPLE_BASEMAP(uv + delta * 3.2307692308);

    #if defined(BLUR_SAMPLE_CENTER_NORMAL)
        #if defined(_SOURCE_DEPTH_NORMALS)
            half3 n0 = half3(SampleSceneNormals(uv));
        #else
            half3 n0 = SampleNormal(uv);//为什么要重新构建呢
        #endif
    #else
        half3 n0 = GetPackedNormal(p0);
    #endif

    half w0  =                                           half(0.2270270270);
    half w1a = CompareNormal(n0, GetPackedNormal(p1a)) * half(0.3162162162);
    half w1b = CompareNormal(n0, GetPackedNormal(p1b)) * half(0.3162162162);
    half w2a = CompareNormal(n0, GetPackedNormal(p2a)) * half(0.0702702703);
    half w2b = CompareNormal(n0, GetPackedNormal(p2b)) * half(0.0702702703);

    half s = half(0.0);
    s += GetPackedAO(p0)  * w0;
    s += GetPackedAO(p1a) * w1a;
    s += GetPackedAO(p1b) * w1b;
    s += GetPackedAO(p2a) * w2a;
    s += GetPackedAO(p2b) * w2b;
    s *= rcp(w0 + w1a + w1b + w2a + w2b);

    return PackAONormal(s, n0);
}
// Done 2
half BlurSmall(float2 uv, float2 delta)
{
    half4 p0 = (half4) SAMPLE_BASEMAP(uv                            );
    half4 p1 = (half4) SAMPLE_BASEMAP(uv + float2(-delta.x, -delta.y));
    half4 p2 = (half4) SAMPLE_BASEMAP(uv + float2( delta.x, -delta.y));
    half4 p3 = (half4) SAMPLE_BASEMAP(uv + float2(-delta.x,  delta.y));
    half4 p4 = (half4) SAMPLE_BASEMAP(uv + float2( delta.x,  delta.y));
    half3 n0 = GetPackedNormal(p0);
    half w0 = half(1.0);
    half w1 = CompareNormal(n0, GetPackedNormal(p1));
    half w2 = CompareNormal(n0, GetPackedNormal(p2));
    half w3 = CompareNormal(n0, GetPackedNormal(p3));
    half w4 = CompareNormal(n0, GetPackedNormal(p4));
    half s = half(0.0);
    s += GetPackedAO(p0) * w0;
    s += GetPackedAO(p1) * w1;
    s += GetPackedAO(p2) * w2;
    s += GetPackedAO(p3) * w3;
    s += GetPackedAO(p4) * w4;
    return s *= rcp(w0 + w1 + w2 + w3 + w4);
}

// Done 2
half4 HorizontalBlur(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    const float2 uv = input.uv;
    const float2 delta = float2(_SourceSize.z, 0.0);
    return Blur(uv, delta);
}

// Done 2
half4 VerticalBlur(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    const float2 uv = input.uv;
    const float2 delta = float2(0.0, _SourceSize.w * rcp(DOWNSAMPLE));
    return Blur(uv, delta);
}
// Done 2
half4 FinalBlur(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    const float2 uv = input.uv;
    const float2 delta = _SourceSize.zw;
    return half(1.0) - BlurSmall(uv, delta );
}

#endif //UNIVERSAL_SSAO_INCLUDED
