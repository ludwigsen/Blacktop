Shader "Hidden/Blacktop/Menu Field Gaussian"
{
    Properties { _MainTex ("Field", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            // Keep this offline filter off the visible UI material: overlay canvases draw every pass.
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment gaussian
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Direction;
            fixed4 gaussian(v2f_img i):SV_Target
            {
                float2 d=_Direction.xy;
                fixed4 c=tex2D(_MainTex,i.uv)*.227027;
                c+=(tex2D(_MainTex,i.uv+d)+tex2D(_MainTex,i.uv-d))*.1945946;
                c+=(tex2D(_MainTex,i.uv+2*d)+tex2D(_MainTex,i.uv-2*d))*.1216216;
                c+=(tex2D(_MainTex,i.uv+3*d)+tex2D(_MainTex,i.uv-3*d))*.054054;
                c+=(tex2D(_MainTex,i.uv+4*d)+tex2D(_MainTex,i.uv-4*d))*.016216;
                return c;
            }
            ENDCG
        }
    }
}
