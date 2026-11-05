Shader "ParkingLot/DepthText" {
Properties { _MainTex("Font atlas",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
SubShader {Tags{"Queue"="Transparent" "RenderType"="Transparent"} Lighting Off Cull Off ZWrite Off ZTest LEqual Blend SrcAlpha OneMinusSrcAlpha
Pass {CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
sampler2D _MainTex;fixed4 _Color;
struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
v2f vert(appdata_full v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=v.texcoord;o.color=v.color*_Color;return o;}
fixed4 frag(v2f i):SV_Target {fixed4 c=i.color;c.a*=tex2D(_MainTex,i.uv).a;return c;}
ENDCG}
}Fallback Off
}
