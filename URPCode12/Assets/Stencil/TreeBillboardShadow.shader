Shader "CNC_Demo/Tree/TreeBillboardShadow"
{
    Properties{
        _MainTex("Main Tex", 2D) = "white" {}
        _Color("Color Tint", Color) = ( 0.6, 0.6, 0.6, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _VerticalBillboarding("Vertical Restraints", Range(0, 1)) = 1
        [Space(10)]
        [Header(Hue Variation)]
        [Toggle(CNC_HUE_VARIATION)] _EnableHueVariation ("Hue Variation Enabled", float) = 0.0
        [HideIfDisabled(CNC_HUE_VARIATION)] _HueVariation ("Hue Variation (RGB: Color, Alpha: Intensity)", Color) = (1.0, 0.5, 0.0, 0.1)
        [HideIfDisabled(CNC_HUE_VARIATION)] _HueVariationSensitive("Hue Variation Sensitive",Range(0.1, 10)) = 1

        _StencilComp("Stencil Comparison", Float) = 6
		_Stencil("Stencil ID", Float) = 15
		_StencilOp("Stencil Operation", Float) = 2
		_StencilWriteMask("Stencil Write Mask", Float) = 255.000000
		_StencilReadMask("Stencil Read Mask", Float) = 255.000000
		// _ColorMask("Color Mask", Float) = 15.000000
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"  }
        LOD 100
        Pass
        {
            Cull Off
            ZWrite Off
            Stencil {
				Ref[_Stencil]
				ReadMask[_StencilReadMask]
				WriteMask[_StencilWriteMask]
				Comp[_StencilComp]
				Pass[_StencilOp]
			}

            
            // ZTest Always
            Tags{"LightMode" = "UniversalForward"}
            // Blend One Zero
            // Blend SrcAlpha OneMinusSrcAlpha
            Blend DstColor Zero
            HLSLPROGRAM
            #pragma multi_compile_instancing
            // #pragma shader_feature_local CNC_HUE_VARIATION
            #pragma vertex TreeBillboardPassVertex
            #pragma fragment TreeBillboardShadowPassFragment
            #include "TreeBillboard.hlsl"
            ENDHLSL
        }
    }
}
