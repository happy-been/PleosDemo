Shader "ParkingLot/Realism20261104/Road" {
 Properties {_Color("Tint",Color)=(.8,.79,.77,1) _MainTex("Albedo",2D)="gray"{} _BumpMap("Normal",2D)="bump"{} }
 SubShader { Tags {"RenderType"="Opaque"} Cull Back
 CGPROGRAM
 #pragma surface surf Standard fullforwardshadows addshadow
 #pragma target 3.0
 #include "UnityStandardUtils.cginc"
 sampler2D _MainTex,_BumpMap;float4 _Color;
 struct Input {float3 worldPos;};
 float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
 float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
 void surf(Input i,inout SurfaceOutputStandard o){float2 uv=i.worldPos.xz;float3 c=tex2D(_MainTex,uv*.7).rgb;float macro=noise(uv*.12);o.Albedo=c*_Color.rgb*lerp(.88,1.08,macro);float3 a=UnpackScaleNormal(tex2D(_BumpMap,uv*.7),.32),b=UnpackScaleNormal(tex2D(_BumpMap,uv*7),.17);o.Normal=BlendNormals(a,b);o.Metallic=0;o.Smoothness=lerp(.10,.23,macro);o.Occlusion=1;}
 ENDCG
 } Fallback "Standard"
}
