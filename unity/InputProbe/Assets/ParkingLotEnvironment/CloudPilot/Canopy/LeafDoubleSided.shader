Shader "Parkside/CloudPilot/LeafDoubleSided" {
 Properties { _MainTex("Leaf RGB and matching mask",2D)="white"{} _Color("Leaf tint",Color)=(0.72,0.80,0.64,1) _Cutoff("Silhouette cutoff",Range(0,1))=0.3 }
 SubShader { Tags {"Queue"="AlphaTest" "RenderType"="TransparentCutout"} LOD 200 Cull Off
 CGPROGRAM
 #pragma surface surf StandardSpecular fullforwardshadows addshadow alphatest:_Cutoff
 #pragma target 3.0
 sampler2D _MainTex; fixed4 _Color;
 struct Input {float2 uv_MainTex; float facing:VFACE;};
 void surf(Input IN,inout SurfaceOutputStandardSpecular o) {fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color; o.Albedo=c.rgb; o.Alpha=c.a; o.Specular=0.025; o.Smoothness=0.08; o.Normal=float3(0,0,IN.facing>=0?1:-1); o.Occlusion=1;}
 ENDCG
 } Fallback "Transparent/Cutout/VertexLit"
}