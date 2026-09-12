Shader "UI/Dialogue/EdFinaleTitleReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _Reveal ("Reveal", Range(0, 1)) = 0
        _Mode ("Mode (0=Spiral 1=Mosaic)", Float) = 0
        _SpiralTurns ("Spiral Turns", Float) = 2.5
        _MosaicBlocks ("Mosaic Blocks", Float) = 18
        _EdgeSoftness ("Edge Softness", Range(0, 0.25)) = 0.07
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            float _Reveal;
            float _Mode;
            float _SpiralTurns;
            float _MosaicBlocks;
            float _EdgeSoftness;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            float ComputeRevealMask(float2 texCoord)
            {
                float softness = max(_EdgeSoftness, 0.001);
                float headroom = saturate(1.0 - softness);
                float2 uv = texCoord - 0.5;
                float openAtRaw;

                if (_Mode < 0.5)
                {
                    float dist = length(uv) * 2.0;
                    float angle = atan2(uv.y, uv.x);
                    float angle01 = angle * 0.15915494 + 0.5;
                    float spiral = frac(angle01 + dist * _SpiralTurns);
                    openAtRaw = dist * 0.35 + spiral * 0.65;
                }
                else
                {
                    float blocks = max(_MosaicBlocks, 4.0);
                    float2 blockId = floor(texCoord * blocks);
                    float2 blockCenter = (blockId + 0.5) / blocks - 0.5;
                    float dist = length(blockCenter) * 2.0;
                    float hash = frac(sin(dot(blockId, float2(12.9898, 78.233))) * 43758.5453);
                    openAtRaw = dist * 0.62 + hash * 0.38;
                }

                // openAt を [0, headroom] に収め、_Reveal==1 で全パネルが不透明になるようにする
                float openAt = saturate(openAtRaw) * headroom;
                float openEnd = min(openAt + softness, 1.0);
                return smoothstep(openAt, openEnd, _Reveal);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 color = (tex2D(_MainTex, i.texcoord) + _TextureSampleAdd) * i.color;
                color.a *= ComputeRevealMask(i.texcoord);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
