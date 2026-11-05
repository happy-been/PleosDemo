Shader "ParkingLot/IntegratedWeatheredPBR" {
Properties {_Color("Color",Color)=(1,1,1,1) _MainTex("Albedo",2D)="white"{} _Metallic("Metallic",Range(0,1))=0 _Glossiness("Smoothness",Range(0,1))=.25 _BumpMap("Normal",2D)="bump"{} _BumpScale("Normal strength",Float)=1 _Weathering("Weathering",Range(0,1))=1}
SubShader {Tags{"RenderType"="Opaque"} LOD 200
CGPROGRAM
#pragma surface surf Standard fullforwardshadows addshadow
#pragma target 3.0
#pragma shader_feature_local _NORMALMAP
#include "UnityStandardUtils.cginc"
sampler2D _MainTex,_BumpMap;fixed4 _Color;float _Metallic,_Glossiness,_BumpScale,_Weathering;
struct Input{float2 uv_MainTex;float3 worldPos;};
float hash1(float p){return frac(sin(p*127.1)*43758.5453);}
float stain(float u){float c=floor(u*3.7);float n=hash1(c);return smoothstep(.70,.94,n)*(1-smoothstep(.02,.42,abs(frac(u*3.7)-.5)));}
void surf(Input IN,inout SurfaceOutputStandard o){float4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;float y=IN.worldPos.y;float streak=max(stain(IN.worldPos.x),stain(IN.worldPos.z));float baseDirt=1-smoothstep(.15,3.2,y);float sill=max(max(step(y,4.16)*exp2(-max(0,4.16-y)*2.3),step(y,7.07)*exp2(-max(0,7.07-y)*2.3)),step(y,10.02)*exp2(-max(0,10.02-y)*2.3));float roof=exp2(-abs(y-12.8)*1.9);float dirt=saturate(_Weathering*(.24*baseDirt+.24*streak*(.30+.7*sill)+.12*roof*streak));o.Albedo=c.rgb*(1-dirt);o.Alpha=1;o.Metallic=_Metallic;o.Smoothness=_Glossiness;o.Normal=float3(0,0,1);
#ifdef _NORMALMAP
o.Normal=UnpackScaleNormal(tex2D(_BumpMap,IN.uv_MainTex),_BumpScale);
#endif
}
ENDCG
}Fallback "Diffuse"
}
