Shader "ParkingLot/ClearViewWindshield" {
 Properties {
  _Color("Original side-window tint",Color)=(.15,.21,.23,.55)
  _WindshieldAlpha("Front windshield absorption",Range(0,1))=.12
  _MainTex("Glass texture",2D)="white"{}
  _Glossiness("Smoothness",Range(0,1))=.94
  _Metallic("Metallic",Range(0,1))=0
 }
 SubShader {
  Tags {"Queue"="Transparent" "RenderType"="Transparent"}
  Cull Off ZWrite Off
  CGPROGRAM
  #pragma surface surf Standard alpha:premul fullforwardshadows vertex:ClassifyGlass finalcolor:InteriorTransmission
  #pragma target 3.0
  sampler2D _MainTex;fixed4 _Color;float _Glossiness,_WindshieldAlpha;
  struct Input {float2 uv_MainTex;float facing:VFACE;float3 viewDir;float windshield;};
  void ClassifyGlass(inout appdata_full v,out Input o){
   UNITY_INITIALIZE_OUTPUT(Input,o);
   // Checked against the original normalized OpenX Ioniq: all 86 front-glass vertices,
   // zero vertices from any other window, and no mixed triangles.
   o.windshield=(v.normal.y>.6&&v.normal.z<-.25&&v.vertex.z<-.3)?1:0;
  }
  void surf(Input IN,inout SurfaceOutputStandard o){
   fixed4 c=tex2D(_MainTex,IN.uv_MainTex)*_Color;
   o.Albedo=c.rgb;o.Alpha=tex2D(_MainTex,IN.uv_MainTex).a*lerp(_Color.a,_WindshieldAlpha,IN.windshield);o.Metallic=0;o.Smoothness=_Glossiness;
   o.Normal=float3(0,0,IN.facing>=0?1:-1);
  }
  void InteriorTransmission(Input IN,SurfaceOutputStandard o,inout fixed4 color){
   if(IN.facing<0&&IN.windshield>.5){
    #if defined(UNITY_PASS_FORWARDADD)
     color=0;
    #else
    // The map's outdoor probe contains no cabin; do not reflect that outdoor panorama
    // on the inner face. Retain absorption and the small dark-cabin Fresnel reflection.
    float nv=saturate(abs(normalize(IN.viewDir).z));
    float fresnel=.04+.96*pow(1-nv,5);
    float alpha=1-(1-o.Alpha)*(1-fresnel);
    color=float4(o.Albedo*o.Alpha,alpha);
    #endif
   }
  }
  ENDCG
 }
 Fallback "Transparent/Diffuse"
}
