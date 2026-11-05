Shader "ParkingLot/WholeLookRoadReview" {
Properties {
 _Color("Asphalt tint",Color)=(.65,.65,.64,1)
 _MainTex("Photographed aggregate",2D)="white"{}
 _BumpMap("Photographed normal",2D)="bump"{}
 _WeatherTex("Broad weathering",2D)="gray"{}
 _RoughnessTex("Roughness",2D)="white"{}
 _BumpScale("Normal strength",Range(0,2))=.65
}
SubShader {Tags{"RenderType"="Opaque"}
CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
#include "UnityStandardUtils.cginc"
sampler2D _MainTex,_BumpMap,_WeatherTex,_RoughnessTex;fixed4 _Color;float _BumpScale;
struct Input {float3 worldPos;};
float bayRandom(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float roadNoise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(bayRandom(i),bayRandom(i+float2(1,0)),f.x),lerp(bayRandom(i+float2(0,1)),bayRandom(i+1),f.x),f.y);}
void surf(Input IN,inout SurfaceOutputStandard o){
 float2 uv=IN.worldPos.xz*.5;
 float3 fine=tex2D(_MainTex,uv).rgb;
 float weather=dot(tex2D(_WeatherTex,IN.worldPos.xz*.075).rgb,float3(.333,.333,.334));
 float macro=sin(IN.worldPos.x*.17+sin(IN.worldPos.z*.27))*sin(IN.worldPos.z*.13);
 float2 p=IN.worldPos.xz;
 // Continuous, low-contrast wear: no raised polygons or collision surfaces.
 float row=(abs(abs(p.y)-17)<abs(abs(p.y)-3))?17:3;row*=p.y<0?-1:1;
 float col=floor((p.x+14)/2.8);float center=-12.6+col*2.8;
 float random=bayRandom(float2(col,row));
 float inside=step(abs(p.x),13.95)*step(abs(p.y-row),2.6);
 float2 oilDelta=float2((p.x-center-(random-.5)*.38)*1.7,(p.y-row-.45)*2.3);
 float oil=exp(-dot(oilDelta,oilDelta))*.30*step(.28,random)*inside;
 float trackX=abs(abs(p.x-center)-.72);
 float tire=(1-smoothstep(.07,.20,trackX))*(1-smoothstep(.8,2.35,abs(p.y-row)))*inside*.10;
 float lanes=exp(-pow((abs(p.y)-10)*.45,2))*.08;
 float crackDistance=abs(p.x-(5.8+sin(p.y*.62)*.12+sin(p.y*2.1)*.035));
 float crackAA=max(fwidth(p.x)*1.25,.005);
 float crack=(1-smoothstep(.006,.006+crackAA,crackDistance))*step(-16,p.y)*step(p.y,13)*.25;
 // Broader uneven ageing stays visible in a moving aerial shot without becoming high-frequency noise.
 float age=roadNoise(p*.045)*.16+roadNoise(p*.11)*.08;
 float lot=step(abs(p.x),26)*step(abs(p.y),23);
 float edge=1-smoothstep(0,1.5,min(26-abs(p.x),23-abs(p.y)));
 float dust=edge*lot*.08;
 o.Albedo=fine*_Color.rgb*(.83+weather*.34+macro*.055-age-oil-tire-lanes-crack+dust);
 o.Normal=UnpackScaleNormal(tex2D(_BumpMap,uv),_BumpScale);
 o.Metallic=0;o.Smoothness=(1-tex2D(_RoughnessTex,uv).r)*.24;o.Occlusion=1;
}
ENDCG
}Fallback "Diffuse"
}
