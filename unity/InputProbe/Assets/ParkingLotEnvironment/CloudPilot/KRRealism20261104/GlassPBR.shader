Shader "ParkingLot/Realism20261104/Glass" {
 Properties {_Color("Tint",Color)=(.025,.035,.04,.86) _Glossiness("Smoothness",Range(0,1))=.93}
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Back ZWrite Off
 CGPROGRAM
 #pragma surface surf Standard alpha:premul fullforwardshadows
 #pragma target 3.0
 float4 _Color;float _Glossiness;
 struct Input {float3 viewDir;};
 void surf(Input i,inout SurfaceOutputStandard o){float f=pow(1-saturate(abs(normalize(i.viewDir).z)),5);o.Albedo=_Color.rgb;o.Metallic=0;o.Smoothness=_Glossiness;o.Alpha=lerp(_Color.a,.98,f);}
 ENDCG
 } Fallback "Transparent/Diffuse"
}
