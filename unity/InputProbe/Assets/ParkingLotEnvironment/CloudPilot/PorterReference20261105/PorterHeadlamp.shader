Shader "ParkingLot/PorterReference20261105/Headlamp" {
 Properties { _Color("Reflector",Color)=(.5,.52,.54,1) }
 SubShader { Tags {"RenderType"="Opaque"} Cull Back ZWrite On
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows vertex:vert
 #pragma target 3.0
 struct Input { float3 localP; };
 void vert(inout appdata_full v,out Input o){UNITY_INITIALIZE_OUTPUT(Input,o);o.localP=v.vertex.xyz;}
 void surf(Input i,inout SurfaceOutputStandard o){
  float2 uv=float2((abs(i.localP.x)-.495)/.325,(i.localP.y-.706)/.391);
  float border=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
  float interior=smoothstep(.02,.075,border);
  float row=frac(saturate(uv.y)*3.0);
  float2 q=float2((uv.x-.50)*2.0,(row-.50)*2.0);
  float radius=length(q*float2(1.1,1.05));
  float housing=1-smoothstep(.84,1.10,radius);
  float bowl=.18+.60*saturate(radius);
  float bulb=1-smoothstep(.14,.27,length(q));
  float ribs=pow(.5+.5*cos(atan2(q.y,q.x)*18),10)*.09;
  float3 reflector=(bowl+ribs+bulb*.26)*float3(.78,.82,.85);
  float3 dark=float3(.018,.022,.026);
  float3 c=lerp(dark,reflector,housing);
  float chrome=smoothstep(.005,.025,border)*(1-interior);
  o.Albedo=lerp(dark,lerp(c,float3(.48,.51,.54),chrome),step(0,border));
  float amber=step(.80,uv.y)*step(.65,uv.x)*interior;
  o.Albedo=lerp(o.Albedo,float3(.44,.19,.045),amber*.65);
  o.Metallic=lerp(.05,.78,interior*housing+chrome);
  o.Smoothness=.82;o.Occlusion=lerp(.58,1,interior);o.Alpha=1;
 }
 ENDCG
 } FallBack "Standard"
}
