Shader "ParkingLot/DistantFoliage" {
Properties {_MainTex("Baked tree",2D)="white"{} _Color("Tint",Color)=(.6,.68,.55,1) _Cutoff("Cutout",Range(0,1))=.28}
SubShader {Tags{"Queue"="AlphaTest" "RenderType"="TransparentCutout"} Cull Off ZWrite On
Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fog
#include "UnityCG.cginc"
sampler2D _MainTex;float4 _Color;float _Cutoff;
struct appdata{float4 vertex:POSITION;float2 uv:TEXCOORD0;};
struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1)};
v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.uv;UNITY_TRANSFER_FOG(o,o.pos);return o;}
fixed4 frag(v2f i):SV_Target{fixed4 c=tex2D(_MainTex,i.uv);clip(c.a-_Cutoff);c.rgb*=_Color.rgb;c.a=1;UNITY_APPLY_FOG(i.fogCoord,c);return c;}
ENDCG
}
}Fallback Off
}
