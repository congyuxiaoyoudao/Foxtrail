Shader "Hiking/UI/NotebookFrameFringe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _AlphaCutoff ("Low Alpha Cutoff", Range(0, 1)) = 0.12
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct VertexOutput
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _AlphaCutoff;

            VertexOutput Vert(VertexInput input)
            {
                VertexOutput output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 Frag(VertexOutput input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.uv) * input.color;
                color.a = saturate((color.a - _AlphaCutoff) / max(0.001, 1.0 - _AlphaCutoff));
                return color;
            }
            ENDCG
        }
    }
}
