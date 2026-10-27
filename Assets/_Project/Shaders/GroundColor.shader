// * Shader nền cho Built-in: giảm độ rực và tương phản mà không thay đổi texture gốc.
// * Dùng chung material cho mọi ground chunk; không cần script cập nhật màu mỗi frame.
Shader "ShooterGame/Ground Color"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Saturation ("Saturation", Range(0,1)) = 0.55
        _Contrast ("Contrast", Range(0,1.5)) = 0.85
        _Brightness ("Brightness", Range(0,1.5)) = 0.9
        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment GroundFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            half _Saturation;
            half _Contrast;
            half _Brightness;

            fixed4 GroundFrag(v2f input) : SV_Target
            {
                // * Tận dụng vertex/texture sampling của Unity để giữ tint, flip, atlas và external alpha.
                fixed4 color = SampleSpriteTexture(input.texcoord) * input.color;
                half3 rgb = color.rgb;
                // * Chỉnh trong không gian màu hiển thị để thông số dễ tinh chỉnh ở cả Gamma và Linear.
                #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = LinearToGammaSpace(rgb);
                #endif

                // * Saturation 0 là xám, 1 giữ màu gốc; giữ sắc nền để vẫn phân biệt được các map.
                half luminance = dot(rgb, half3(0.299h, 0.587h, 0.114h));
                rgb = lerp(luminance.xxx, rgb, _Saturation);
                // * Co độ tương phản quanh mức xám giữa, sau đó hạ sáng nhẹ để nhường điểm nhấn cho nhân vật.
                rgb = saturate(((rgb - 0.5h) * _Contrast + 0.5h) * _Brightness);
                #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = GammaToLinearSpace(rgb);
                #endif

                // * Premultiply alpha khớp Blend One OneMinusSrcAlpha của sprite mặc định, giữ nguyên độ trong suốt.
                color.rgb = rgb * color.a;
                return color;
            }
            ENDCG
        }
    }
}
