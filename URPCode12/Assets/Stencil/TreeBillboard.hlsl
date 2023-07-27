#ifndef TREE_BILLBOARD_PASS_INCLUDED
#define TREE_BILLBOARD_PASS_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
CBUFFER_START(UnityPerMaterial)
	float4 _MainTex_ST;
	half4 _Color;
	half _Cutoff;
	half _VerticalBillboarding;//调整时固定法线还是固定指向上的方向，即约束垂直方向的程度。
	half4   _HueVariation;
    half    _HueVariationSensitive;
CBUFFER_END
struct Attributes {
    float4 vertex : POSITION;
    float4 texcoord : TEXCOORD0;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings {
    float4 pos : SV_POSITION;
    float4 uv : TEXCOORD0;
	// float3 normalDir : TEXCOORD1;
	// float3 worldPos : TEXCOORD2;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

//#include "../FoliageCommon.hlsl"
Varyings TreeBillboardPassVertex(Attributes input) {
	Varyings o = (Varyings)0;
	UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, o);
	float3 worldPos = TransformObjectToWorld(input.vertex.xyz);
	o.pos = TransformWorldToHClip(worldPos);
	o.uv.xy = TRANSFORM_TEX(input.texcoord, _MainTex);
	#ifdef CNC_HUE_VARIATION
		float3 viewer = GetCameraPositionWS();
		float3 center = TransformObjectToWorld(float3( 0, 0, 0));
		float3 normalDir = viewer - center;
		normalDir = normalize(normalDir);
        o.uv.w = HueVariationVS(input.vertex.xyz, normalDir.xyz);
    #endif
	return o;
}

float4 TreeBillboardPassFragment(Varyings input) : SV_TARGET{
	UNITY_SETUP_INSTANCE_ID(input);
	half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
	clip(c.a - _Cutoff);
	c.rgb *= _Color.rgb;
	#ifdef CNC_HUE_VARIATION
		float hueVariation = input.uv.w;
		AppleHueVariation(c.rgb, hueVariation);
	#endif
	return c;
}


float4 TreeBillboardShadowPassFragment(Varyings input) : SV_TARGET{
	UNITY_SETUP_INSTANCE_ID(input);
	half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
	clip(c.a - 0.01);
	//alpha小于0.01的地方置为白色
	// half isTransparent = step(c.a, 0.01);
	// c.rgb = isTransparent * 1 + (1 - isTransparent) * c.rgb;
	c.rgb = _Color.rgb;
	return c;
}

#endif