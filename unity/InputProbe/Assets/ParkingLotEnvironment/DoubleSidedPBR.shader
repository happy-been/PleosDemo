Shader "ParkingLot/DoubleSidedPBR" {
Properties {
 _Color("Color",Color)=(1,1,1,1) _MainTex("Color texture",2D)="white"{}
 _Metallic("Metallic",Range(0,1))=0 _Glossiness("Smoothness",Range(0,1))=.5
 _MetallicGlossMap("Metallic R / Smoothness A",2D)="white"{} _GlossMapScale("Smoothness multiplier",Float)=1
 _BumpMap("Normal",2D)="bump"{} _BumpScale("Normal strength",Float)=1
 _OcclusionMap("Occlusion",2D)="white"{} _OcclusionStrength("Occlusion strength",Range(0,1))=1
 _EmissionMap("Emission",2D)="white"{} _EmissionColor("Emission color",Color)=(0,0,0,1)
}
SubShader {Tags{"RenderType"="Opaque"} Cull Off
CGPROGRAM
#pragma surface surf Standard fullforwardshadows addshadow
#pragma target 3.0
#pragma shader_feature_local _METALLICGLOSSMAP
#pragma shader_feature_local _NORMALMAP
#pragma shader_feature_local _EMISSION
#include "UnityStandardUtils.cginc"
sampler2D _MainTex,_MetallicGlossMap,_BumpMap,_OcclusionMap,_EmissionMap;
fixed4 _Color,_EmissionColor;float _Metallic,_Glossiness,_GlossMapScale,_BumpScale,_OcclusionStrength;
struct Input{float2 uv_MainTex;float facing:VFACE;};
void surf(Input IN,inout SurfaceOutputStandard o){
 fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;o.Albedo=c.rgb;o.Alpha=1;
 o.Metallic=_Metallic;o.Smoothness=_Glossiness;
 #ifdef _METALLICGLOSSMAP
 float4 m=tex2D(_MetallicGlossMap,IN.uv_MainTex);o.Metallic=m.r;o.Smoothness=m.a*_GlossMapScale;
 #endif
 o.Normal=float3(0,0,1);
 #ifdef _NORMALMAP
 o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_MainTex),_BumpScale);
 #endif
 o.Normal.z*=IN.facing>=0?1:-1;
 // Source glTF occlusion is R. G in the packed source is roughness, not shadow.
 o.Occlusion=lerp(1,tex2D(_OcclusionMap,IN.uv_MainTex).r,_OcclusionStrength);
 #ifdef _EMISSION
 o.Emission=tex2D(_EmissionMap,IN.uv_MainTex).rgb*_EmissionColor.rgb;
 #endif
}
ENDCG
}Fallback "Diffuse"
}
