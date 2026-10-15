Shader "Hiking/ArcWindow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _UseVertexColor ("Use Mesh Color", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _UseVertexColor;
            CBUFFER_END
            float4 _JourneyArcCenter;
            float _JourneyArcHalfAngle;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half4 color : COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                o.color = lerp(half4(1,1,1,1), input.color, _UseVertexColor) * _Color;
                return o;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 delta = input.positionWS.xy - _JourneyArcCenter.xy;
                // Zero is the editor preview (full ring); runtime assigns 30 degrees per side.
                if (_JourneyArcHalfAngle > 0 && _JourneyArcHalfAngle < 3.14)
                    clip(_JourneyArcHalfAngle - abs(atan2(delta.x, delta.y)));
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            }
            ENDHLSL
        }
    }
}
