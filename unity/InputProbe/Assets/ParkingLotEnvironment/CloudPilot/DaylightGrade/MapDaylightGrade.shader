Shader "Hidden/Parkside/CloudPilot/MapDaylightGrade"
{
    Properties { _MainTex("Source", 2D) = "white" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "ACESFitted.cginc"
            sampler2D _MainTex;
            float _Exposure, _Contrast, _Saturation;

            float4 frag(v2f_img i) : SV_Target
            {
                float4 source = tex2D(_MainTex, i.uv);
                float3 color = max(source.rgb, 0);
                #if defined(UNITY_COLORSPACE_GAMMA)
                color = GammaToLinearSpace(color);
                #endif
                color *= _Exposure;
                color = .18 * exp2(log2(max(color, 1e-6) / .18) * _Contrast);
                float luminance = dot(color, float3(.2126, .7152, .0722));
                color = max(lerp(luminance.xxx, color, _Saturation), 0);
                color = ACESFitted(color);
                #if defined(UNITY_COLORSPACE_GAMMA)
                color = LinearToGammaSpace(color);
                #endif
                return float4(color, source.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
