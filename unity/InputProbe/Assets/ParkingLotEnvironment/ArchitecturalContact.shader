Shader "ParkingLot/ArchitecturalContact" {
Properties { _Feather("Contact feather in metres",Range(.05,1))=.45 }
SubShader {
 Tags { "Queue"="Transparent-20" "RenderType"="Transparent" }
 Blend DstColor Zero
 ZWrite Off
 ZTest LEqual
 Cull Off
 Offset -1,-1
 Pass {
  CGPROGRAM
  #pragma vertex vert
  #pragma fragment frag
  #pragma multi_compile_fog
  #include "UnityCG.cginc"
  float _Feather;
  struct appdata { float4 vertex:POSITION; float2 coord:TEXCOORD0; float2 extent:TEXCOORD1; fixed4 color:COLOR; };
  struct v2f { float4 pos:SV_POSITION; float2 coord:TEXCOORD0; float2 extent:TEXCOORD1; fixed opacity:TEXCOORD2; UNITY_FOG_COORDS(3) };
  v2f vert(appdata v) { v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.coord=v.coord;o.extent=v.extent;o.opacity=v.color.a;UNITY_TRANSFER_FOG(o,o.pos);return o; }
  fixed4 frag(v2f i):SV_Target {
   float2 q=max(abs(i.coord)-i.extent,0);
   float attenuation=1-i.opacity*(1-smoothstep(0,_Feather,length(q)));
   fixed4 c=fixed4(attenuation,attenuation,attenuation,1);
   UNITY_APPLY_FOG_COLOR(i.fogCoord,c,fixed4(1,1,1,1));return c;
  }
  ENDCG
 }
}
Fallback Off
}
