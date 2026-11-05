Shader "ParkingLot/PorterReference20261105/Glass" {
 Properties { _Color("Tint",Color)=(.025,.034,.04,.36) _Glossiness("Smoothness",Range(0,1))=.94 }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Cull Back
 Pass { ZWrite On ColorMask 0 }
 ZWrite Off
 CGPROGRAM
 #pragma surface surf Standard alpha:premul fullforwardshadows
 #pragma target 3.0
 float4 _Color;float _Glossiness;
 struct Input {float3 viewDir;};
 void surf(Input i,inout SurfaceOutputStandard o){float f=pow(1-saturate(abs(normalize(i.viewDir).z)),5);o.Albedo=_Color.rgb;o.Metallic=0;o.Smoothness=_Glossiness;o.Alpha=lerp(_Color.a,.76,f);}
 ENDCG
 } FallBack "Transparent/Diffuse"
}
