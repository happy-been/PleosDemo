Shader "Hidden/ParkingLot/TucsonGlassDepthReview" {
Properties {_Color("Tint and transmission",Color)=(.15,.21,.23,.55) _MainTex("Lens texture",2D)="white"{} _Glossiness("Smoothness",Range(0,1))=.94 _Metallic("Metallic",Range(0,1))=0}
SubShader {Tags{"Queue"="Transparent" "RenderType"="Transparent"} Cull Off ZWrite Off
Pass {ZWrite On ColorMask 0}
CGPROGRAM
#pragma surface surf Standard alpha:premul fullforwardshadows
#pragma target 3.0
sampler2D _MainTex;fixed4 _Color;float _Glossiness;
struct Input{float2 uv_MainTex;float facing:VFACE;};
void surf(Input IN,inout SurfaceOutputStandard o){fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;o.Albedo=c.rgb;o.Alpha=c.a;o.Metallic=0;o.Smoothness=_Glossiness;o.Normal=float3(0,0,IN.facing>=0?1:-1);}
ENDCG
}Fallback Off
}
