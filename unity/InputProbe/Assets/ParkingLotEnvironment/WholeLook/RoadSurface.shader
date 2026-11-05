Shader "ParkingLot/WholeLookRoad" {
Properties {
 _Color("Asphalt tint",Color)=(.68,.64,.575,1)
 _MainTex("Fine photographed aggregate",2D)="white"{}
 _BumpMap("Fine photographed normal",2D)="bump"{}
 _WeatherTex("30 metre photographed road",2D)="gray"{}
 _RoughnessTex("Roughness",2D)="white"{}
 _BumpScale("Normal strength",Range(0,2))=.4
}
SubShader {Tags{"RenderType"="Opaque"}
CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
#include "UnityStandardUtils.cginc"
sampler2D _MainTex,_BumpMap,_WeatherTex,_RoughnessTex;fixed4 _Color;float _BumpScale;
struct Input {float3 worldPos;};
float bayRandom(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
void surf(Input IN,inout SurfaceOutputStandard o){
 float2 p=IN.worldPos.xz,uv=p*.5;
 float3 fine=tex2D(_MainTex,uv).rgb;
 float2 macroUV=p/30+float2(.37,.11);
 // Aerial imagery supplies broad wear, not enlarged pebbles at the driver's feet.
 // Smooth coarse scan grain nearby; distant/aerial views keep the photograph's normal filtering.
 float2 dx=ddx(macroUV),dy=ddy(macroUV);
 float minimum=.00390625*(1-smoothstep(8,28,distance(_WorldSpaceCameraPos,IN.worldPos)));
 dx*=max(1,minimum/max(length(dx),1e-6));
 dy*=max(1,minimum/max(length(dy),1e-6));
 float3 photographed=tex2Dgrad(_WeatherTex,macroUV,dx,dy).rgb;
 // Preserve photographed wear at the source's documented 30m scale. Fine grain stays at 2m.
 // Measured mean RGB normalizes the two unmodified photos without baking light into the map.
 float photoVariation=dot(photographed,float3(.2126,.7152,.0722))/.389071;
 float3 fineDetail=fine/float3(.269547,.274069,.275485);
 float row=(abs(abs(p.y)-17)<abs(abs(p.y)-3))?17:3;row*=p.y<0?-1:1;
 float col=floor((p.x+14)/2.8),center=-12.6+col*2.8;
 float random=bayRandom(float2(col,row));
 float inside=step(abs(p.x),13.95)*step(abs(p.y-row),2.6);
 float2 oilDelta=float2((p.x-center-(random-.5)*.38)*1.7,(p.y-row-.45)*2.3);
 float oil=exp(-dot(oilDelta,oilDelta))*.08*step(.28,random)*inside;
 float tire=(1-smoothstep(.07,.20,abs(abs(p.x-center)-.72)))*(1-smoothstep(.8,2.35,abs(p.y-row)))*inside*.025;
 float lanes=exp(-pow((abs(p.y)-10)*.45,2))*.025;
 float lot=step(abs(p.x),26)*step(abs(p.y),23);
 float edge=1-smoothstep(0,1.5,min(26-abs(p.x),23-abs(p.y)));
 o.Albedo=.274*_Color.rgb*.78*photoVariation*fineDetail*(1-oil-tire-lanes+edge*lot*.035);
 o.Normal=UnpackScaleNormal(tex2D(_BumpMap,uv),_BumpScale);
 o.Metallic=0;o.Smoothness=(1-tex2D(_RoughnessTex,uv).r)*.24;o.Occlusion=1;
}
ENDCG
}Fallback "Diffuse"
}
