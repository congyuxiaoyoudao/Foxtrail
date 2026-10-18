Shader "Hiking/UI/TravelIris"
{
    Properties
    {
        _MainTex ("Leaf texture", 2D) = "white" {}
        _Progress ("Opening", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _Progress;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            v2f vert(appdata v) { v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o; }
            float2 rotatePoint(float2 p,float a) { float s=sin(a),c=cos(a);return float2(c*p.x-s*p.y,s*p.x+c*p.y); }
            fixed4 frag(v2f input):SV_Target
            {
                // Same fixed leaf and cyclic local draw order as the approved six-blade prototype.
                float2 display=float2(input.uv.x-.5,.5-input.uv.y)*480;
                clip(180-length(display));
                float2 p=display/1.45;
                const float stepAngle=1.0471975512,turn=.837758041;
                float sector=floor((atan2(p.y,p.x)+stepAngle)/stepAngle);
                p=rotatePoint(p,-sector*stepAngle);
                float2 last=float2(156-156*cos(turn),156*sin(turn));
                [loop] for(int i=5;i>=0;i--)
                {
                    float2 q=rotatePoint(p,-i*stepAngle);
                    q=rotatePoint(q-float2(156,0),-turn*_Progress)+float2(156,0);
                    float edge=length(q-last)-123;
                    if(edge<0) continue;
                    bool inside=true;
                    [loop] for(int j=0;j<=48;j++)
                    {
                        float a=turn*j/48;
                        if(length(q-float2(156-156*cos(a),156*sin(a)))>221) {inside=false;break;}
                    }
                    if(!inside) continue;
                    float2 uv=float2((300+(q.x+240)/480*450)/1254,1-(200+(q.y+240)/480*300)/1254);
                    fixed4 paint=tex2Dlod(_MainTex,float4(uv,0,0));
                    fixed3 colour=lerp(fixed3(.157,.365,.263),paint.rgb,paint.a);
                    colour=lerp(fixed3(.667,.765,.545),colour,smoothstep(1,2,edge));
                    return fixed4(colour,1)*input.color;
                }
                return 0;
            }
            ENDCG
        }
    }
}
