Shader "ParkingLot/AwningFabric" {
Properties {
 _Color("Cloth tint",Color)=(.7,.7,.65,1)
 _MainTex("Photographic polyester weave",2D)="white"{}
 _BumpMap("Photographic weave normal",2D)="bump"{}
 _FabricRepeat("Physical repeats per metre",Vector)=(3.7635,3.7552,0,0)
 _BumpScale("Weave strength",Range(0,1))=.22
 _WeaveStrength("Photographic colour variation",Range(0,1))=.5
}
SubShader {
 Tags{"RenderType"="Opaque"} Cull Off
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows
 #pragma target 3.0
 #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
 #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
 #include "UnityStandardUtils.cginc"
 sampler2D _MainTex,_BumpMap;
 fixed4 _Color;float4 _FabricRepeat;float _BumpScale,_WeaveStrength;
 struct Input {float2 uv_MainTex;float facing:VFACE;};
 void surf(Input IN,inout SurfaceOutputStandard o){
   float2 uv=IN.uv_MainTex*_FabricRepeat.xy;
   float3 tex=tex2D(_MainTex,uv).rgb;
   float weave=clamp(dot(tex,float3(.299,.587,.114))/.58,.65,1.3);
   o.Albedo=_Color.rgb*lerp(1,weave,_WeaveStrength);
   o.Normal=UnpackScaleNormal(tex2D(_BumpMap,uv),_BumpScale);
   o.Normal.z*=IN.facing>=0?1:-1;
   o.Metallic=0;o.Smoothness=.08;o.Occlusion=1;
 }
 ENDCG
}Fallback "Diffuse"
}
