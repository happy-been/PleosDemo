Shader "ParkingLot/WholeLookPaintReview" {
 Properties {
  _Color("Paint color",Color)=(.5,.5,.5,1)
  _Metallic("Metallic pigment",Range(0,1))=.42
  _Glossiness("Smoothness",Range(0,1))=.86
  _CoatStrength("Clear coat strength",Range(0,1))=.38
  _CoatRoughness("Clear coat roughness",Range(.08,.4))=.14
 }
 SubShader {
  Tags {"RenderType"="Opaque" "Queue"="Geometry"}
  LOD 300
  Pass {
   Tags {"LightMode"="ForwardBase"}
   Cull Off ZWrite On
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fwdbase
   #pragma multi_compile_fog
   #pragma multi_compile_instancing
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   #include "AutoLight.cginc"
   #include "UnityStandardUtils.cginc"
   float4 _Color;
   float _Metallic,_Glossiness,_CoatStrength,_CoatRoughness;
   struct appdata {
    float4 vertex:POSITION;
    float3 normal:NORMAL;
    float2 uv1:TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };
   struct v2f {
    float4 pos:SV_POSITION;
    float3 worldPosition:TEXCOORD0;
    float3 worldNormal:TEXCOORD1;
    UNITY_SHADOW_COORDS(2)
    UNITY_FOG_COORDS(3)
    UNITY_VERTEX_OUTPUT_STEREO
   };
   v2f vert(appdata v) {
    v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.pos=UnityObjectToClipPos(v.vertex);
    o.worldPosition=mul(unity_ObjectToWorld,v.vertex).xyz;
    o.worldNormal=UnityObjectToWorldNormal(v.normal);
    UNITY_TRANSFER_SHADOW(o,v.uv1);
    UNITY_TRANSFER_FOG(o,o.pos);
    return o;
   }
   float3 ActiveToLinear(float3 c) {
    #if defined(UNITY_COLORSPACE_GAMMA)
     // Exact scalar transfer also handles HDR values above one without cubic overshoot.
     return float3(GammaToLinearSpaceExact(max(0,c.r)),GammaToLinearSpaceExact(max(0,c.g)),GammaToLinearSpaceExact(max(0,c.b)));
    #else
     return max(0,c);
    #endif
   }
   float3 LinearToActive(float3 c) {
    #if defined(UNITY_COLORSPACE_GAMMA)
     return float3(LinearToGammaSpaceExact(c.r),LinearToGammaSpaceExact(c.g),LinearToGammaSpaceExact(c.b));
    #else
     return c;
    #endif
   }
   float PaintPow5(float x){float x2=x*x;return x2*x2*x;}
   float3 Fresnel(float3 f0,float cosine){return f0+(1-f0)*PaintPow5(1-saturate(cosine));}
   float GGXSmith(float nl,float nv,float nh,float perceptualRoughness) {
    float alpha=perceptualRoughness*perceptualRoughness;
    float a2=alpha*alpha;
    float denom=nh*nh*(a2-1)+1;
    float distribution=a2/max(UNITY_PI*denom*denom,1e-7);
    float lambdaV=nl*sqrt(max(nv*nv*(1-a2)+a2,1e-7));
    float lambdaL=nv*sqrt(max(nl*nl*(1-a2)+a2,1e-7));
    float visibility=.5/max(lambdaV+lambdaL,1e-5);
    return distribution*visibility;
   }
   float3 Environment(float3 reflection,float3 position,float roughness) {
    float mip=roughness*(1.7-.7*roughness)*UNITY_SPECCUBE_LOD_STEPS;
    float3 direction=reflection;
    #if defined(UNITY_SPECCUBE_BOX_PROJECTION)
     direction=BoxProjectedCubemapDirection(reflection,position,unity_SpecCube0_ProbePosition,unity_SpecCube0_BoxMin,unity_SpecCube0_BoxMax);
    #endif
    float3 env0=ActiveToLinear(DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0,direction,mip),unity_SpecCube0_HDR));
    #if defined(UNITY_SPECCUBE_BLENDING)
     float blend=unity_SpecCube0_BoxMin.w;
     if(blend<.99999) {
      direction=reflection;
      #if defined(UNITY_SPECCUBE_BOX_PROJECTION)
       direction=BoxProjectedCubemapDirection(reflection,position,unity_SpecCube1_ProbePosition,unity_SpecCube1_BoxMin,unity_SpecCube1_BoxMax);
      #endif
      float4 encoded=UNITY_SAMPLE_TEXCUBE_SAMPLER_LOD(unity_SpecCube1,unity_SpecCube0,direction,mip);
      float3 env1=ActiveToLinear(DecodeHDR(encoded,unity_SpecCube1_HDR));
      env0=lerp(env1,env0,blend);
     }
    #endif
    return max(0,env0);
   }
   float4 frag(v2f i,float facing:VFACE):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 n=normalize(i.worldNormal)*(facing<0?-1:1),v=normalize(_WorldSpaceCameraPos-i.worldPosition);
    float3 l=normalize(UnityWorldSpaceLightDir(i.worldPosition));
    float3 h=normalize(l+v);
    float nl=saturate(dot(n,l)),nv=max(saturate(dot(n,v)),.001);
    float nh=saturate(dot(n,h)),lh=saturate(dot(l,h));
    float3 albedo=saturate(ActiveToLinear(_Color.rgb));
    float metallic=saturate(_Metallic);
    // A mild finite lobe avoids mirror-like, subpixel highlights on the simplified paint layer.
    float roughness=max(.14,1-saturate(_Glossiness));
    float3 f0=lerp(float3(.04,.04,.04),albedo,metallic);
    float3 f=Fresnel(f0,lh);
    float coat=saturate(_CoatStrength),coatRoughness=clamp(_CoatRoughness,.08,.4);
    float coatFV=.04+.96*PaintPow5(1-nv),coatFL=.04+.96*PaintPow5(1-nl);
    float baseTransmission=(1-coat*coatFV)*(1-coat*coatFL);
    float3 diffuse=(1-f)*(1-metallic)*albedo/UNITY_PI;
    float3 specular=GGXSmith(nl,nv,nh,roughness)*f;
    float coatSpecular=GGXSmith(nl,nv,nh,coatRoughness)*(.04+.96*PaintPow5(1-lh))*coat;
    float3 light=_LightColor0.rgb;
    #if defined(UNITY_COLORSPACE_GAMMA)
     // Unity packs color * intensity. Preserve white-light intensity (1.25 in this scene)
     // instead of incorrectly applying an sRGB transfer to that physical multiplier.
     float intensity=max(max(light.r,light.g),max(light.b,.0001));
     light=ActiveToLinear(light/intensity)*intensity;
    #endif
    UNITY_LIGHT_ATTENUATION(attenuation,i,i.worldPosition);
    // Unity's unit directional light follows Lambert's convention; PI converts to radiance.
    float3 color=((diffuse+specular)*baseTransmission+coatSpecular)*light*(UNITY_PI*nl*attenuation);
    // ShadeSH9 would gamma-encode here. Evaluate its linear coefficients directly once.
    float4 shNormal=float4(n,1);
    float3 ambient=max(0,SHEvalLinearL0L1(shNormal)+SHEvalLinearL2(shNormal));
    float3 fv=Fresnel(f0,nv);
    float lowerOcclusion=lerp(.35,1,saturate(i.worldPosition.y/.6));
    color+=lowerOcclusion*ambient*albedo*(1-metallic)*(1-fv)*baseTransmission;
    float3 reflection=reflect(-v,n);
    float3 envF=f0+(max((1-roughness).xxx,f0)-f0)*PaintPow5(1-nv);
    float alpha=roughness*roughness;
    color+=lowerOcclusion*Environment(reflection,i.worldPosition,roughness)*envF/(1+alpha*alpha)*baseTransmission;
    color+=lowerOcclusion*Environment(reflection,i.worldPosition,coatRoughness)*coatFV*coat;
    float4 result=float4(LinearToActive(max(0,color)),1);
    UNITY_APPLY_FOG(i.fogCoord,result);
    return result;
   }
   ENDCG
  }
  UsePass "Standard/SHADOWCASTER"
 }
 Fallback "Standard"
}
