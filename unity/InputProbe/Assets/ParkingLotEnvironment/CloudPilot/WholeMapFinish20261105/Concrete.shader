Shader "ParkingLot/WholeMap/Concrete" {
 Properties {_Color("Concrete tint",Color)=(.53,.52,.485,1)}
 SubShader {Tags {"RenderType"="Opaque"} LOD 250
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows addshadow
 #pragma target 3.0
 struct Input {float3 worldPos;float3 worldNormal;};fixed4 _Color;
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
 void surf(Input IN,inout SurfaceOutputStandard o){float2 p=abs(IN.worldNormal.x)>.5?IN.worldPos.zy:IN.worldPos.xy;float y=IN.worldPos.y;float macro=noise(p*1.1);float fine=noise(p*48);float streak=noise(float2(p.x*12,y*.5));float baseDirt=(1-smoothstep(.05,.7,y))*.18;float topDirt=smoothstep(1.4,2,y)*streak*.15;o.Albedo=_Color.rgb*(.89+.17*macro+.045*(fine-.5)-baseDirt-topDirt);o.Metallic=0;o.Smoothness=.12;o.Occlusion=1;o.Alpha=1;}
 ENDCG
 }Fallback "Standard"
}
