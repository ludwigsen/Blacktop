Shader "Blacktop/Menu Field Blur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Field", 2D) = "white" {}
        _BlurTex ("Soft field", 2D) = "white" {}
        _Blur ("Blend", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex, _BlurTex;
            float _Blur;
            v2f vert(appdata v)
            {
                v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                // One sharp and one cached soft sample keep submenu transitions inexpensive.
                fixed4 c=lerp(tex2D(_MainTex,i.uv),tex2D(_BlurTex,i.uv),_Blur);
                c.rgb *= 1-saturate(length((i.uv-.5)*float2(1,.7)))*.4;
                return c*i.color;
            }
            ENDCG
        }
    }
}
