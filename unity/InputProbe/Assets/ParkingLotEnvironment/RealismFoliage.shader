Shader "ParkingLot/RealismFoliage" {
 Properties {
  _Color("Leaf tint",Color)=(1,1,1,1)
  _MainTex("Photographed leaf color",2D)="white"{}
  _AlphaTex("Separate silhouette mask",2D)="white"{}
  _BumpMap("Normal",2D)="bump"{}
  _OcclusionMap("Occlusion",2D)="white"{}
  _RoughnessMap("Roughness",2D)="white"{}
  _Cutoff("Cutout",Range(0,1))=.45
 }
 SubShader {
  Tags {"RenderType"="TransparentCutout" "Queue"="AlphaTest"}
  Cull Off
  CGPROGRAM
  #pragma surface surf Standard alphatest:_Cutoff addshadow fullforwardshadows vertex:LeafWind
  #pragma target 3.0
  #pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
  #pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
  sampler2D _MainTex, _AlphaTex, _BumpMap, _OcclusionMap, _RoughnessMap;
  fixed4 _Color;
  // Scoped by this map's camera; zero leaves every source material unchanged.
  float _ParkingLotWindStrength,_ParkingLotWindTime,_ParkingLotWindTimeOverride;
  struct Input {float2 uv_MainTex; float facing : VFACE;};
  void LeafWind(inout appdata_full v) {
   float t=_ParkingLotWindTimeOverride>.5?_ParkingLotWindTime:_Time.y;
   if(_ParkingLotWindStrength<=0||t==0)return;
   float3 p=mul(unity_ObjectToWorld,v.vertex).xyz;
   float phase=dot(p.xz,float2(.43,.37))+p.y*1.7;
   float sway=sin(t*.85+phase)-sin(phase);
   float flutter=sin(t*2.1+phase*1.93)-sin(phase*1.93);
   float amplitude=min(_ParkingLotWindStrength,.02)*saturate(p.y*.6);
   p+=float3(sway,flutter*.12,sway*.37+flutter*.18)*amplitude;
   v.vertex=mul(unity_WorldToObject,float4(p,1));
  }
  void surf(Input IN,inout SurfaceOutputStandard o) {
   o.Albedo=tex2D(_MainTex,IN.uv_MainTex).rgb*_Color.rgb;
   o.Alpha=tex2D(_AlphaTex,IN.uv_MainTex).r;
   o.Normal=UnpackNormal(tex2D(_BumpMap,IN.uv_MainTex));
   o.Normal.z*=IN.facing>=0?1:-1;
   o.Occlusion=lerp(1,tex2D(_OcclusionMap,IN.uv_MainTex).r,.65);
   o.Metallic=0;
   o.Smoothness=saturate(1-tex2D(_RoughnessMap,IN.uv_MainTex).r)*.35;
  }
  ENDCG
 }
 Fallback "Transparent/Cutout/Diffuse"
}
