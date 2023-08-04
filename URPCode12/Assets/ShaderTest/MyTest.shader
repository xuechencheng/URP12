Shader "Test/MyTest"
{
    Properties{
        _MainTex("Main Tex", 2D) = "white" {}
        _Color("Color Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags {  "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline"  }
        LOD 100
        Pass
        {
            Cull Back
            ZWrite On
            Tags{"LightMode" = "UniversalForward"}
            // Blend DstColor Zero
            // Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
            CBUFFER_END

            struct Attributes {
                float4 vertex : POSITION;
                float4 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 pos : SV_POSITION;
                float4 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vertex(Attributes input) {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                float3 worldPos = TransformObjectToWorld(input.vertex.xyz);
                o.pos = TransformWorldToHClip(worldPos);
                o.uv.xy = TRANSFORM_TEX(input.texcoord, _MainTex);
                return o;
            }

            half4 Fragment(Varyings input) : SV_TARGET{
                UNITY_SETUP_INSTANCE_ID(input);
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy);
                c.rgb = _Color.rgb;

                half3 v1 = half3( 1, 1, 1);
                half3 v2 = half3( 0.5, 0.5, 0.5);
                half3 v3 = half3( 0, 0, 0);
                half3x3 matrix1 = half3x3( v1, v2, v3);
                half3 test = half3(1,1,1);
                return half4(mul(test, matrix1),1);
                // return half4(1,1,0,1);
                // return matrix1[0][1];
            }

            ENDHLSL
        }
    }
}
