Shader "Game02/FieldHoleOverlaySprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _AlphaTex ("External Alpha", 2D) = "white" {}
        _EnableExternalAlpha ("Enable External Alpha", Float) = 0

        _HoleCount ("Hole Count", Float) = 0

        _Hole0 ("Hole0 (x,y,w,h)", Vector) = (0,0,0,0)
        _Hole1 ("Hole1 (x,y,w,h)", Vector) = (0,0,0,0)
        _Hole2 ("Hole2 (x,y,w,h)", Vector) = (0,0,0,0)
        _Hole3 ("Hole3 (x,y,w,h)", Vector) = (0,0,0,0)
        _Hole4 ("Hole4 (x,y,w,h)", Vector) = (0,0,0,0)
        _Hole5 ("Hole5 (x,y,w,h)", Vector) = (0,0,0,0)
        _Hole6 ("Hole6 (x,y,w,h)", Vector) = (0,0,0,0)
        _Hole7 ("Hole7 (x,y,w,h)", Vector) = (0,0,0,0)
        _Hole8 ("Hole8 (x,y,w,h)", Vector) = (0,0,0,0)
        _Hole9 ("Hole9 (x,y,w,h)", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #pragma multi_compile _ PIXELSNAP_ON

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float4 _MainTex_ST;
            float _EnableExternalAlpha;

            float _HoleCount;

            float4 _Hole0;
            float4 _Hole1;
            float4 _Hole2;
            float4 _Hole3;
            float4 _Hole4;
            float4 _Hole5;
            float4 _Hole6;
            float4 _Hole7;
            float4 _Hole8;
            float4 _Hole9;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
#ifdef PIXELSNAP_ON
                o.pos = UnityPixelSnap(o.pos);
#endif
                // i.uv is expected to be Sprite-local UV in [0..1]
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 c = tex2D(_MainTex, uv);
#if ETC1_EXTERNAL_ALPHA
                fixed4 alpha = tex2D(_AlphaTex, uv);
                c.a = lerp(c.a, alpha.r, _EnableExternalAlpha);
#endif
                return c;
            }

            bool HoleContains(float2 uv, float4 r)
            {
                // r = (x, y, w, h), where y is measured from bottom and all are normalized.
                if (r.z <= 0 || r.w <= 0)
                {
                    return false;
                }

                float x0 = r.x;
                float y0 = r.y;
                float x1 = r.x + r.z;
                float y1 = r.y + r.w;

                return (uv.x >= x0) && (uv.x <= x1) && (uv.y >= y0) && (uv.y <= y1);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uvLocal = i.uv;

                bool inside = false;
                if (_HoleCount > 0.5 && HoleContains(uvLocal, _Hole0)) inside = true;
                if (!inside && _HoleCount > 1.5 && HoleContains(uvLocal, _Hole1)) inside = true;
                if (!inside && _HoleCount > 2.5 && HoleContains(uvLocal, _Hole2)) inside = true;
                if (!inside && _HoleCount > 3.5 && HoleContains(uvLocal, _Hole3)) inside = true;
                if (!inside && _HoleCount > 4.5 && HoleContains(uvLocal, _Hole4)) inside = true;
                if (!inside && _HoleCount > 5.5 && HoleContains(uvLocal, _Hole5)) inside = true;
                if (!inside && _HoleCount > 6.5 && HoleContains(uvLocal, _Hole6)) inside = true;
                if (!inside && _HoleCount > 7.5 && HoleContains(uvLocal, _Hole7)) inside = true;
                if (!inside && _HoleCount > 8.5 && HoleContains(uvLocal, _Hole8)) inside = true;
                if (!inside && _HoleCount > 9.5 && HoleContains(uvLocal, _Hole9)) inside = true;

                if (inside)
                {
                    // Fully transparent hole: do not draw any pixels.
                    discard;
                }

                fixed4 tex = SampleSpriteTexture(TRANSFORM_TEX(i.uv, _MainTex));
                return tex * i.color;
            }
            ENDCG
        }
    }
}

