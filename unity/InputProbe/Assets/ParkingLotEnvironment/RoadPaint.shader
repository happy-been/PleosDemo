Shader "ParkingLot/RoadPaint" {
Properties {_Color("Weathered paint",Color)=(.68,.68,.64,1)}
SubShader {Tags{"RenderType"="Opaque"}
CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
fixed4 _Color;
struct Input {float3 worldPos;};
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
void surf(Input IN,inout SurfaceOutputStandard o){
 float2 detail=IN.worldPos.xz*100;
 float footprint=max(fwidth(detail.x),fwidth(detail.y));
 float grain=lerp(hash(floor(detail)),.5,saturate(footprint));
 float2 broad=IN.worldPos.xz*7;float2 cell=floor(broad),f=frac(broad);f=f*f*(3-2*f);
 float worn=lerp(lerp(hash(cell),hash(cell+float2(1,0)),f.x),lerp(hash(cell+float2(0,1)),hash(cell+1),f.x),f.y);
 float chips=smoothstep(.72,.85,worn)*(1-saturate(footprint*.22));
 o.Albedo=_Color.rgb*(.71+grain*.12+worn*.11-chips*.38);o.Smoothness=.03;o.Metallic=0;
}
ENDCG
}Fallback "Diffuse"
}
