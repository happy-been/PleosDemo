Shader "ParkingLot/KoreanGround" {
Properties {
 _Color("Terrain tint",Color)=(.7,.75,.65,1)
 _MainTex("Photographed ground",2D)="white"{}
 _BumpMap("Surface normal",2D)="bump"{}
 _TileScale("World UV scale",Float)=.066667
 _BumpScale("Normal strength",Range(0,1))=.35
 _FootTex("Foothill grass",2D)="white"{}
 _FootTint("Foothill grass tint",Color)=(.48,.56,.42,1)
 _FootBlend("Blend forest into flat land",Range(0,1))=0
}
SubShader {Tags{"RenderType"="Opaque"}
CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
#pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
#pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
#include "UnityStandardUtils.cginc"
sampler2D _MainTex,_BumpMap,_FootTex;fixed4 _Color,_FootTint;float _TileScale,_BumpScale,_FootBlend;
struct Input{float3 worldPos;};
void surf(Input IN,inout SurfaceOutputStandard o){
 float2 uv=IN.worldPos.xz*_TileScale;
 float3 a=tex2D(_MainTex,uv).rgb;
 float3 b=tex2D(_MainTex,uv*.237+float2(.19,.53)).rgb;
 float macro=.88+.08*sin(IN.worldPos.x*.037+sin(IN.worldPos.z*.029));
 o.Albedo=lerp(a,b,.28)*_Color.rgb*macro;
 if(_FootBlend>.5){
  float2 footUV=IN.worldPos.xz*.18;
  float3 footA=tex2D(_FootTex,footUV).rgb,footB=tex2D(_FootTex,footUV*.237+float2(.19,.53)).rgb;
  float footWeight=1-smoothstep(0,12,IN.worldPos.y);
  o.Albedo=lerp(o.Albedo,lerp(footA,footB,.28)*_FootTint.rgb*macro,footWeight);
 }
 o.Normal=UnpackScaleNormal(tex2D(_BumpMap,uv),_BumpScale);
 o.Metallic=0;o.Smoothness=.025;o.Occlusion=1;
}
ENDCG
}Fallback "Diffuse"
}
