// 名前に Unlit を含め、KomayamaWorldLayerDrawOrder の強制差し替え対象外にする
Shader "KomayamaCraft/Sprite-Unlit-Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width (px)", Range(0, 16)) = 4
        _BodyAlpha ("Body Alpha Cutoff", Range(0.01, 1)) = 0.9
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
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
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            Name "SpriteOutline"
            HLSLPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SpriteFrag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _BodyAlpha;
                float4 _RendererColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings SpriteVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color * _Color * _RendererColor;
                return output;
            }

            float SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            }

            // 8方向×距離ステップのみ（重い二重 unroll を避ける）
            bool HasNearbyBody(float2 uv, float widthPx, float bodyAlpha)
            {
                float2 texel = _MainTex_TexelSize.xy;
                int maxR = (int)ceil(widthPx);
                if (maxR < 1)
                {
                    return false;
                }

                if (maxR > 16)
                {
                    maxR = 16;
                }

                [loop]
                for (int i = 1; i <= 16; i++)
                {
                    if (i > maxR)
                    {
                        break;
                    }

                    float2 o = texel * (float)i;
                    if (SampleAlpha(uv + float2(o.x, 0)) >= bodyAlpha) return true;
                    if (SampleAlpha(uv - float2(o.x, 0)) >= bodyAlpha) return true;
                    if (SampleAlpha(uv + float2(0, o.y)) >= bodyAlpha) return true;
                    if (SampleAlpha(uv - float2(0, o.y)) >= bodyAlpha) return true;
                    if (SampleAlpha(uv + o) >= bodyAlpha) return true;
                    if (SampleAlpha(uv - o) >= bodyAlpha) return true;
                    if (SampleAlpha(uv + float2(o.x, -o.y)) >= bodyAlpha) return true;
                    if (SampleAlpha(uv + float2(-o.x, o.y)) >= bodyAlpha) return true;
                }

                return false;
            }

            float4 SpriteFrag(Varyings input) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float bodyCut = max(0.01, _BodyAlpha);
                float a = tex.a * input.color.a;

                // 本体（十分不透明）だけ絵色を出す
                if (a >= bodyCut)
                {
                    float4 color = tex * input.color;
                    color.rgb *= color.a;
                    return color;
                }

                // 半透明縁＋外側は、近くに本体があればアウトライン色
                if (_OutlineWidth > 0.01 && HasNearbyBody(input.uv, _OutlineWidth, bodyCut))
                {
                    float4 outlineCol = _OutlineColor;
                    outlineCol.a *= input.color.a;
                    outlineCol.rgb *= outlineCol.a;
                    return outlineCol;
                }

                return float4(0, 0, 0, 0);
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
