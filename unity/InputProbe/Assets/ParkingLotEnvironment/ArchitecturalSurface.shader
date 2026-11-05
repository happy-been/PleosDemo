Shader "ParkingLot/ArchitecturalSurface" {
Properties {
 _Color("Surface tint",Color)=(.6,.6,.58,1)
 _MainTex("Photographic surface",2D)="white"{}
 _WorldScale("Repeats per metre",Float)=.0554
 _TextureAsColor("Use texture color",Range(0,1))=0
 _DetailStrength("Concrete weathering",Range(0,1))=.7
}
SubShader {Tags{"RenderType"="Opaque"}
CGPROGRAM
#pragma surface surf Standard fullforwardshadows
#pragma target 3.0
#pragma shader_feature_local _SPECULARHIGHLIGHTS_OFF
#pragma shader_feature_local _GLOSSYREFLECTIONS_OFF
sampler2D _MainTex;fixed4 _Color;float _WorldScale,_TextureAsColor,_DetailStrength;
struct Input {float3 worldPos;float3 worldNormal;};
void surf(Input IN,inout SurfaceOutputStandard o){
 // Box-built ledges and rooftop rooms need physical-scale UVs rather than one stretched tile per face.
 float3 a=abs(IN.worldNormal);float2 uv;
 if(a.y>=a.x&&a.y>=a.z)uv=IN.worldPos.xz;
 else if(a.x>=a.z)uv=float2(IN.worldPos.z,IN.worldPos.y);
 else uv=IN.worldPos.xy;
 float3 tex=tex2D(_MainTex,uv*_WorldScale).rgb;
 float luminance=dot(tex,float3(.299,.587,.114));
 float concrete=lerp(1,clamp(luminance/.64,.55,1.25),_DetailStrength);
 o.Albedo=_Color.rgb*lerp(concrete.xxx,tex,_TextureAsColor);
 o.Metallic=0;o.Smoothness=.04;o.Occlusion=1;
}
ENDCG
}Fallback "Diffuse"
}
