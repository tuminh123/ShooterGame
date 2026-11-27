// Sprite chạy animation flipbook ngay trên GPU, dùng cho đạn.
// Texture là sheet một hàng gồm _Frames ô bằng nhau; sprite gán trên SpriteRenderer là ô đầu (mesh Full Rect).
// Shader dời UV theo trục X sang ô hiện tại, nên không cần Animator hay script Update cho từng viên đạn,
// và mọi viên cùng loại vẫn dùng chung material nên vẫn gộp batch.
// _Time theo thời gian game (scaled) nên animation dừng khi gameplay pause.
// Không đưa texture của shader này vào Sprite Atlas: UV của ô phải nằm trên đúng sheet gốc.
Shader "ShooterGame/Vfx/Sprite_Flipbook"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Sheet", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Frames ("Frame Count", Float) = 1
        _Fps ("Frames Per Second", Float) = 12
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Frames;
            float _Fps;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                // Chọn ô ở vertex shader: cả quad dùng chung một ô, fragment chỉ việc lấy mẫu.
                float frame = floor(fmod(_Time.y * _Fps, max(_Frames, 1)));
                o.texcoord = v.texcoord + float2(frame / max(_Frames, 1), 0);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.texcoord) * i.color;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
