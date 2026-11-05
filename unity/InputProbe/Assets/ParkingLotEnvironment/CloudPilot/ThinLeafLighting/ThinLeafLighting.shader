Shader "Parkside/CloudPilot/ThinLeafLighting"
{
    Properties
    {
        _MainTex("Leaf RGB and matching mask",2D)="white"{}
        _Color("Leaf tint",Color)=(0.72,0.80,0.64,1)
        _Cutoff("Silhouette cutoff",Range(0,1))=0.28
    }
    SubShader
    {
        Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"}
        LOD 200
        Cull Off
        CGPROGRAM
        #pragma surface surf ThinLeaf fullforwardshadows addshadow alphatest:_Cutoff exclude_path:deferred
        #pragma target 3.0
        #include "UnityPBSLighting.cginc"
        sampler2D _MainTex;
        fixed4 _Color;
        struct Input {float2 uv_MainTex;float facing:VFACE;};
        inline half4 LightingThinLeaf(SurfaceOutputStandardSpecular s,half3 viewDir,UnityGI gi)
        {
            half4 color=LightingStandardSpecular(s,viewDir,gi);
            half nDotL=dot(s.Normal,gi.light.dir);
            half transmitted=saturate(-nDotL)*0.55;
            half wrapped=max(0.0,saturate((nDotL+0.35)/1.35)-saturate(nDotL))*0.35;
            // Approximate thin-leaf scattering from the actual attenuated light.
            // No emission or fixed ambient lift; preserve baked GI and shadow attenuation.
            color.rgb+=s.Albedo*gi.light.color*(transmitted+wrapped);
            return color;
        }
        inline void LightingThinLeaf_GI(SurfaceOutputStandardSpecular s,UnityGIInput data,inout UnityGI gi)
        {
            LightingStandardSpecular_GI(s,data,gi);
        }
        void surf(Input IN,inout SurfaceOutputStandardSpecular o)
        {
            fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
            o.Albedo=c.rgb;o.Alpha=c.a;o.Specular=0.025;o.Smoothness=0.08;
            o.Normal=float3(0,0,IN.facing>=0?1:-1);o.Occlusion=1;
        }
        ENDCG
    }
    Fallback "Transparent/Cutout/VertexLit"
}
