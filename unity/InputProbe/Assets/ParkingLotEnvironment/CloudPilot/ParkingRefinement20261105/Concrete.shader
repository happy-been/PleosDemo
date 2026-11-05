Shader "ParkingLot/Refinement20261105/Concrete" {
 Properties { _Color("Concrete tone",Color)=(.58,.58,.55,1) }
 SubShader { Tags {"RenderType"="Opaque"} LOD 200 Cull Back
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows addshadow
 #pragma target 3.0
 fixed4 _Color;
 struct Input {float3 worldPos;float3 worldNormal;};
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 void surf(Input IN,inout SurfaceOutputStandard o){float3 p=IN.worldPos;float horizontal=abs(IN.worldNormal.x)>.5?p.z:p.x;float2 uv=float2(horizontal,p.y);float macro=noise(uv*.62)*.55+noise(uv*2.6)*.25+noise(uv*16)*.2;float grain=noise(uv*110);float damp=(1-smoothstep(.0,.62,p.y))*(.21+.1*noise(float2(horizontal*4,0)));float streak=pow(saturate(noise(float2(horizontal*10,p.y*.36))),3)*.12*smoothstep(.6,2.0,p.y);float form=.018*sin(p.y*35);o.Albedo=_Color.rgb*(.84+.25*macro+.075*(grain-.5)-damp-streak+form);o.Smoothness=.12+grain*.045;o.Metallic=0;o.Occlusion=1-damp*.25;}
 ENDCG }
 Fallback "Standard"
}
