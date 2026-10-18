Shader "Hiking/UI/CloseIconContrast"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Ink ("Ink", Color) = (0.20,0.27,0.21,1)
        _Edge ("Cream outline", Color) = (1,0.95,0.79,1)
        _OutlineUV ("Outline UV radius", Float) = 0.022
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct Input { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            fixed4 _Ink, _Edge;
            float _OutlineUV;
            Output vert(Input v) { Output o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float Alpha(float2 uv) { return saturate((tex2D(_MainTex,uv).a-0.12)/0.88); }
            fixed4 frag(Output i):SV_Target
            {
                fixed4 source=tex2D(_MainTex,i.uv);
                float a=Alpha(i.uv);
                float edge=a;
                for(int n=0;n<8;n++)
                {
                    float angle=n*0.78539816;
                    edge=max(edge,Alpha(i.uv+float2(cos(angle),sin(angle))*_OutlineUV));
                }
                float grain=dot(source.rgb,float3(0.299,0.587,0.114));
                fixed3 ink=_Ink.rgb*(0.85+0.3*grain);
                fixed3 rgb=lerp(_Edge.rgb,ink,a/max(edge,0.001));
                return fixed4(rgb*i.color.rgb,edge*i.color.a);
            }
            ENDCG
        }
    }
}
