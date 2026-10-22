Shader "Hiking/StationEdgeBlend"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _LeftBlend ("Left Blend", Range(0, 0.5)) = 0
        _EdgeUv ("Edge Extension", Range(0, 0.25)) = 0
        _TextureInset ("Texture Inset", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        Lighting Off
        ZWrite Off
        // Keep framebuffer coverage opaque over the sky fill when clouds or
        // station edges have partial alpha. DWM uses that coverage for the window.
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _Color;
            float _LeftBlend;
            float _EdgeUv;
            float _TextureInset;
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float2 sampleUv = input.uv;
                // Extend the edge texels without changing the scale of the artwork
                // between station boundaries. Inset avoids opposite-edge filtering
                // on the repeat-wrapped imported background textures.
                sampleUv.x = (sampleUv.x - _EdgeUv) / max(1.0 - 2.0 * _EdgeUv, 0.00001);
                float inset = _TextureInset;
                sampleUv.x = clamp(sampleUv.x, inset, 1.0 - inset);
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUv) * input.color;
                color.a *= saturate(input.uv.x / max(_LeftBlend, 0.00001));
                return color;
            }
            ENDHLSL
        }
    }
}
