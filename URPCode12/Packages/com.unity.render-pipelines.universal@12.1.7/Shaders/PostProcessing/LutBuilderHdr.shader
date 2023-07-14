Shader "Hidden/Universal Render Pipeline/LutBuilderHdr"
{
    HLSLINCLUDE

        #pragma exclude_renderers gles
        #pragma multi_compile_local _ _TONEMAP_ACES _TONEMAP_NEUTRAL

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ACES.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        float4 _Lut_Params;         // x: lut_height, y: 0.5 / lut_width, z: 0.5 / lut_height, w: lut_height / lut_height - 1
        float4 _ColorBalance;       // xyz: LMS coeffs, w: unused
        float4 _ColorFilter;        // xyz: color, w: unused
        float4 _ChannelMixerRed;    // xyz: rgb coeffs, w: unused
        float4 _ChannelMixerGreen;  // xyz: rgb coeffs, w: unused
        float4 _ChannelMixerBlue;   // xyz: rgb coeffs, w: unused
        float4 _HueSatCon;          // x: hue shift, y: saturation, z: contrast, w: unused
        float4 _Lift;               // xyz: color, w: unused
        float4 _Gamma;              // xyz: color, w: unused
        float4 _Gain;               // xyz: color, w: unused
        float4 _Shadows;            // xyz: color, w: unused
        float4 _Midtones;           // xyz: color, w: unused
        float4 _Highlights;         // xyz: color, w: unused
        float4 _ShaHiLimits;        // xy: shadows min/max, zw: highlight min/max
        float4 _SplitShadows;       // xyz: color, w: balance
        float4 _SplitHighlights;    // xyz: color, w: unused

        TEXTURE2D(_CurveMaster);
        TEXTURE2D(_CurveRed);
        TEXTURE2D(_CurveGreen);
        TEXTURE2D(_CurveBlue);

        TEXTURE2D(_CurveHueVsHue);
        TEXTURE2D(_CurveHueVsSat);
        TEXTURE2D(_CurveSatVsSat);
        TEXTURE2D(_CurveLumVsSat);

        float EvaluateCurve(TEXTURE2D(curve), float t)
        {
            float x = SAMPLE_TEXTURE2D(curve, sampler_LinearClamp, float2(t, 0.0)).x;
            return saturate(x);
        }

        float3 ColorGrade(float3 colorLutSpace)
        {
            float3 colorLinear = LogCToLinear(colorLutSpace);
            float3 colorLMS = LinearToLMS(colorLinear);
            colorLMS *= _ColorBalance.xyz; // 1, White Balance
            colorLinear = LMSToLinear(colorLMS);
            #if _TONEMAP_ACES
                float3 colorLog = ACES_to_ACEScc(unity_to_ACES(colorLinear));
            #else
                float3 colorLog = LinearToLogC(colorLinear);
            #endif
            colorLog = (colorLog - ACEScc_MIDGRAY) * _HueSatCon.z + ACEScc_MIDGRAY;// 2,Color Adjustments(Constrast)
            #if _TONEMAP_ACES
                colorLinear = ACES_to_ACEScg(ACEScc_to_ACES(colorLog));
            #else
                colorLinear = LogCToLinear(colorLog);
            #endif
            colorLinear *= _ColorFilter.xyz;// 2,Color Adjustments(_ColorFilter)
            colorLinear = max(0.0, colorLinear);
            float balance = _SplitShadows.w;// 3, Split Toning
            float3 colorGamma = PositivePow(colorLinear, 1.0 / 2.2);
            float luma = saturate(GetLuminance(saturate(colorGamma)) + balance);
            float3 splitShadows = lerp((0.5).xxx, _SplitShadows.xyz, 1.0 - luma);
            float3 splitHighlights = lerp((0.5).xxx, _SplitHighlights.xyz, luma);
            colorGamma = SoftLight(colorGamma, splitShadows);
            colorGamma = SoftLight(colorGamma, splitHighlights);
            colorLinear = PositivePow(colorGamma, 2.2);
            colorLinear = float3( dot(colorLinear, _ChannelMixerRed.xyz), dot(colorLinear, _ChannelMixerGreen.xyz), dot(colorLinear, _ChannelMixerBlue.xyz));// 4, Channel Mixer

            // Shadows, midtones, highlights
            luma = GetLuminance(colorLinear);
            float shadowsFactor = 1.0 - smoothstep(_ShaHiLimits.x, _ShaHiLimits.y, luma);
            float highlightsFactor = smoothstep(_ShaHiLimits.z, _ShaHiLimits.w, luma);
            float midtonesFactor = 1.0 - shadowsFactor - highlightsFactor;
            colorLinear = colorLinear * _Shadows.xyz * shadowsFactor + colorLinear * _Midtones.xyz * midtonesFactor + colorLinear * _Highlights.xyz * highlightsFactor;// 5,Shadows Midtones Highlights

            // Lift, gamma, gain
            colorLinear = colorLinear * _Gain.xyz + _Lift.xyz;
            colorLinear = sign(colorLinear) * pow(abs(colorLinear), _Gamma.xyz);// 6, Lift Gamma Gain
            // HSV operations
            float satMult;
            float3 hsv = RgbToHsv(colorLinear);
            {
                // Hue Vs Sat
                satMult = EvaluateCurve(_CurveHueVsSat, hsv.x) * 2.0;
                // Sat Vs Sat
                satMult *= EvaluateCurve(_CurveSatVsSat, hsv.y) * 2.0;
                // Lum Vs Sat
                satMult *= EvaluateCurve(_CurveLumVsSat, Luminance(colorLinear)) * 2.0;
                // Hue Shift & Hue Vs Hue
                float hue = hsv.x + _HueSatCon.x;
                float offset = EvaluateCurve(_CurveHueVsHue, hue) - 0.5;
                hue += offset;
                hsv.x = RotateHue(hue, 0.0, 1.0);
            }
            colorLinear = HsvToRgb(hsv);//7, Color Curves

            // Global saturation
            luma = GetLuminance(colorLinear);
            colorLinear = luma.xxx + (_HueSatCon.yyy * satMult) * (colorLinear - luma.xxx);
            // YRGB curves
            // Conceptually these need to be in range [0;1] and from an artist-workflow perspective
            // it's easier to deal with
            colorLinear = FastTonemap(colorLinear);
            {
                const float kHalfPixel = (1.0 / 128.0) / 2.0;
                float3 c = colorLinear;
                // Y (master)
                c += kHalfPixel.xxx;
                float mr = EvaluateCurve(_CurveMaster, c.r);
                float mg = EvaluateCurve(_CurveMaster, c.g);
                float mb = EvaluateCurve(_CurveMaster, c.b);
                c = float3(mr, mg, mb);
                // RGB
                c += kHalfPixel.xxx;
                float r = EvaluateCurve(_CurveRed, c.r);
                float g = EvaluateCurve(_CurveGreen, c.g);
                float b = EvaluateCurve(_CurveBlue, c.b);
                colorLinear = float3(r, g, b);
            }
            colorLinear = FastTonemapInvert(colorLinear);
            colorLinear = max(0.0, colorLinear);
            return colorLinear;
        }

        float3 Tonemap(float3 colorLinear)
        {
            #if _TONEMAP_NEUTRAL
            {
                colorLinear = NeutralTonemap(colorLinear);
            }
            #elif _TONEMAP_ACES
            {
                // Note: input is actually ACEScg (AP1 w/ linear encoding)
                float3 aces = ACEScg_to_ACES(colorLinear);
                colorLinear = AcesTonemap(aces);
            }
            #endif
            return colorLinear;
        }

        float4 Frag(Varyings input) : SV_Target
        {
            float3 colorLutSpace = GetLutStripValue(input.uv, _Lut_Params);
            float3 gradedColor = ColorGrade(colorLutSpace);
            gradedColor = Tonemap(gradedColor);
            return float4(gradedColor, 1.0);
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "LutBuilderHdr"

            HLSLPROGRAM
                #pragma vertex FullscreenVert
                #pragma fragment Frag
            ENDHLSL
        }
    }
}
