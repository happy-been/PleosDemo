Shader "ParkingLot/LeafCutout" {
 Properties { _MainTex("Leaf color",2D)="white"{} _AlphaTex("Leaf mask",2D)="white"{} _Cutoff("Cutoff",Range(0,1))=.45 }
 SubShader { Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" } Cull Off
 CGPROGRAM
 #pragma surface surf Standard alphatest:_Cutoff addshadow
 #pragma target 3.0
 sampler2D _MainTex; sampler2D _AlphaTex;
 struct Input {float2 uv_MainTex;};
 void surf(Input IN,inout SurfaceOutputStandard o) {o.Albedo=tex2D(_MainTex,IN.uv_MainTex).rgb;o.Alpha=tex2D(_AlphaTex,IN.uv_MainTex).r;o.Smoothness=.1;}
 ENDCG
 } Fallback "Transparent/Cutout/Diffuse"
}
