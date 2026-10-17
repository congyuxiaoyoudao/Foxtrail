Shader "Hiking/UI/FrameCleanAlpha"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
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
        // Preserve coverage alpha for DWM while blending straight-alpha sprite colors.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionOS : TEXCOORD1;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionOS = input.vertex;
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            float4 CleanSample(float2 uv)
            {
                float4 sampleColor = tex2D(_MainTex, uv);
                // The source sprite has bright red/green pixels with alpha 1-15/255.
                // Remove those texels before filtering so their RGB cannot bleed into the wood edge.
                sampleColor.a *= step(16.0 / 255.0, sampleColor.a);
                return sampleColor;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float2 pixel = input.uv * _MainTex_TexelSize.zw - 0.5;
                float2 fraction = frac(pixel);
                float2 firstUV = (floor(pixel) + 0.5) * _MainTex_TexelSize.xy;
                float2 texel = _MainTex_TexelSize.xy;
                float4 a = CleanSample(firstUV);
                float4 b = CleanSample(firstUV + float2(texel.x, 0));
                float4 c = CleanSample(firstUV + float2(0, texel.y));
                float4 d = CleanSample(firstUV + texel);
                float4 weights = float4(
                    (1 - fraction.x) * (1 - fraction.y),
                    fraction.x * (1 - fraction.y),
                    (1 - fraction.x) * fraction.y,
                    fraction.x * fraction.y);
                float alpha = dot(weights, float4(a.a, b.a, c.a, d.a));
                float3 premultiplied = weights.x * a.rgb * a.a + weights.y * b.rgb * b.a
                    + weights.z * c.rgb * c.a + weights.w * d.rgb * d.a;
                fixed4 color = fixed4(premultiplied / max(alpha, 0.00001), alpha) * input.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(input.positionOS.xy, _ClipRect);
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
