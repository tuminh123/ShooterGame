Shader "ShooterGame/UI/Shield Border"
{
    Properties
    {
        [PerRendererData] _MainTex ("UI Texture (unused)", 2D) = "white" {}
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 border : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 border : TEXCOORD1;
                half4 mask : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            float4 _ClipRect;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                output.border = input.border;
                float2 pixelSize = output.vertex.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                output.mask = half4(input.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return output;
            }

            float RoundBox(float2 p, float2 halfSize, float radius)
            {
                float2 q = abs(p) - halfSize + radius;
                return length(max(q, 0)) + min(max(q.x, q.y), 0) - radius;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 size = input.border.xy;
                float thickness = input.border.w;
                float2 p = (input.uv - 0.5) * size;
                float2 outerSize = size * 0.5;
                float2 innerSize = outerSize - thickness;
                float radius = min(thickness, min(innerSize.x, innerSize.y));
                float outerDistance = RoundBox(p, outerSize, radius + thickness);
                float innerDistance = RoundBox(p, innerSize, radius);
                float aa = max(fwidth(outerDistance), 0.05);
                // Chỉ vẽ vùng nằm giữa hai hình bo góc. Vùng máu bên trong giữ trong suốt.
                // Một quad/one pass, không texture, không blur hoặc material riêng mỗi bar.
                half ring = saturate(0.5 - outerDistance / aa) * saturate(0.5 + innerDistance / aa);
                half coverage = saturate((input.border.z - input.uv.x) / max(fwidth(input.uv.x), 0.0001));
                // Ánh sáng chạy trên GPU nhưng viền nền luôn rõ, không nhấp nháy mất giáp.
                half pulse = frac(input.uv.x * 3 - _Time.y * 0.75);
                pulse *= pulse;
                pulse *= pulse;
                fixed4 result;
                result.rgb = lerp(input.color.rgb, fixed3(1, 0.98, 0.65), pulse * 0.65h);
                result.a = ring * coverage * input.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                half2 mask = saturate((_ClipRect.zw - _ClipRect.xy - abs(input.mask.xy)) * input.mask.zw);
                result.a *= mask.x * mask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001h);
                #endif
                return result;
            }
            ENDCG
        }
    }
}
