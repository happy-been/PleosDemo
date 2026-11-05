Shader "ParkingLot/RoadRepair" {
Properties {
 _MainTex("Repair diffuse and alpha",2D)="white"{}
 _Opacity("Repair darkening",Range(0,1))=.35
}
SubShader {Tags{"Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True"}
Blend DstColor Zero
ZWrite Off ZTest LEqual Cull Off Offset -1,-1
Pass {
CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_fog
#pragma multi_compile_instancing
#pragma target 3.0
#include "UnityCG.cginc"
sampler2D _MainTex;float _Opacity;
struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO};
v2f vert(appdata v){
 v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
 o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;UNITY_TRANSFER_FOG(o,o.vertex);return o;
}
fixed4 frag(v2f i):SV_Target{
 fixed4 patch=tex2D(_MainTex,i.uv);
 // Multiply the already-lit road, retaining its shadows and fine aggregate.
 // Neutral transmission outside the feathered alpha cannot create a pale rectangle.
 float luma=dot(patch.rgb,float3(.299,.587,.114));
 float shade=1-patch.a*_Opacity*lerp(.5,1,saturate(1-luma));
 fixed4 color=fixed4(shade,shade,shade,1);
 UNITY_APPLY_FOG_COLOR(i.fogCoord,color,fixed4(1,1,1,1));return color;
}
ENDCG
}}
Fallback Off
}
