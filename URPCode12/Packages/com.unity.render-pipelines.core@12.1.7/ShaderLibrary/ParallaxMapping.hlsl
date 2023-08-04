#ifndef UNIVERSAL_PARALLAX_MAPPING_INCLUDED
#define UNIVERSAL_PARALLAX_MAPPING_INCLUDED

// Done
half3 GetViewDirectionTangentSpace(half4 tangentWS, half3 normalWS, half3 viewDirWS)
{
    half3 unnormalizedNormalWS = normalWS;
    const half renormFactor = 1.0 / length(unnormalizedNormalWS);
    half crossSign = (tangentWS.w > 0.0 ? 1.0 : -1.0); 
    half3 bitang = crossSign * cross(normalWS.xyz, tangentWS.xyz);
    half3 WorldSpaceNormal = renormFactor * normalWS.xyz;       
    half3 WorldSpaceTangent = renormFactor * tangentWS.xyz;
    half3 WorldSpaceBiTangent = renormFactor * bitang;
    half3x3 tangentSpaceTransform = half3x3(WorldSpaceTangent, WorldSpaceBiTangent, WorldSpaceNormal);//按行填充矩阵 切线空间变换矩阵 因为转置矩阵等于逆矩阵吗???
    half3 viewDirTS = mul(tangentSpaceTransform, viewDirWS);
    return viewDirTS;
}

#ifndef BUILTIN_TARGET_API
    half2 ParallaxOffset1Step(half height, half amplitude, half3 viewDirTS)
    {
        height = height * amplitude - amplitude / 2.0;
        half3 v = normalize(viewDirTS);
        v.z += 0.42;
        return height * (v.xy / v.z);
    }
#endif

float2 ParallaxMapping(TEXTURE2D_PARAM(heightMap, sampler_heightMap), half3 viewDirTS, half scale, float2 uv)
{
    half h = SAMPLE_TEXTURE2D(heightMap, sampler_heightMap, uv).g;
    float2 offset = ParallaxOffset1Step(h, scale, viewDirTS);
    return offset;
}

#endif // UNIVERSAL_PARALLAX_MAPPING_INCLUDED
