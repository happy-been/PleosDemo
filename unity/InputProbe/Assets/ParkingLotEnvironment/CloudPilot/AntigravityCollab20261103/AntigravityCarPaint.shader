Shader "ParkingLot/AntigravityCarPaint" {
 Properties {
  _Color("Paint color", Color) = (0.75, 0.76, 0.78, 1)
  _MainTex("Albedo texture", 2D) = "white" {}
  _Metallic("Metallic pigment", Range(0, 1)) = 0.28
  _Glossiness("Smoothness", Range(0, 1)) = 0.88
  _CoatStrength("Clear coat strength", Range(0, 1)) = 0.85
  _CoatRoughness("Clear coat roughness", Range(0.02, 0.30)) = 0.06
 }
 SubShader {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" }
  LOD 300
  Pass {
   Tags { "LightMode"="ForwardBase" }
   Cull Back ZWrite On
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

   sampler2D _MainTex;
   float4 _Color;
   float _Metallic, _Glossiness, _CoatStrength, _CoatRoughness;

   struct appdata {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
   };

   struct v2f {
    float4 pos : SV_POSITION;
    float3 worldPosition : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float2 uv : TEXCOORD2;
    UNITY_SHADOW_COORDS(3)
    UNITY_FOG_COORDS(4)
    UNITY_VERTEX_OUTPUT_STEREO
   };

   v2f vert(appdata v) {
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.pos = UnityObjectToClipPos(v.vertex);
    o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    o.uv = v.uv0;
    UNITY_TRANSFER_SHADOW(o, v.uv1);
    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
   }

   float3 ActiveToLinear(float3 c) {
    #if defined(UNITY_COLORSPACE_GAMMA)
     return float3(GammaToLinearSpaceExact(max(0, c.r)), GammaToLinearSpaceExact(max(0, c.g)), GammaToLinearSpaceExact(max(0, c.b)));
    #else
     return max(0, c);
    #endif
   }

   float3 LinearToActive(float3 c) {
    #if defined(UNITY_COLORSPACE_GAMMA)
     return float3(LinearToGammaSpaceExact(max(0, c.r)), LinearToGammaSpaceExact(max(0, c.g)), LinearToGammaSpaceExact(max(0, c.b)));
    #else
     return c;
    #endif
   }

   float PaintPow5(float x) { float x2 = x * x; return x2 * x2 * x; }
   float3 Fresnel(float3 f0, float cosine) { return f0 + (1.0 - f0) * PaintPow5(1.0 - saturate(cosine)); }

   float GGXSmith(float nl, float nv, float nh, float perceptualRoughness) {
    float alpha = perceptualRoughness * perceptualRoughness;
    float a2 = alpha * alpha;
    float denom = nh * nh * (a2 - 1.0) + 1.0;
    float distribution = a2 / max(UNITY_PI * denom * denom, 1e-7);
    float lambdaV = nl * sqrt(max(nv * nv * (1.0 - a2) + a2, 1e-7));
    float lambdaL = nv * sqrt(max(nl * nl * (1.0 - a2) + a2, 1e-7));
    float visibility = 0.5 / max(lambdaV + lambdaL, 1e-5);
    return distribution * visibility;
   }

   float3 SampleAutomotiveEnv(float3 reflection, float3 position, float roughness, float shadowOcc) {
    float mip = roughness * (1.7 - 0.7 * roughness) * UNITY_SPECCUBE_LOD_STEPS;
    float3 direction = reflection;
    #if defined(UNITY_SPECCUBE_BOX_PROJECTION)
     direction = BoxProjectedCubemapDirection(reflection, position, unity_SpecCube0_ProbePosition, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax);
    #endif
    float3 probeEnv = ActiveToLinear(DecodeHDR(UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, direction, mip), unity_SpecCube0_HDR));

    // Smooth automotive horizon gradient (No high-frequency procedural noise/waves!)
    float ry = reflection.y;
    float3 groundCol = float3(0.06, 0.065, 0.07);
    float3 horizonCol = float3(0.40, 0.43, 0.47);
    float3 skyCol = float3(0.56, 0.62, 0.70);

    float horizonBlend = smoothstep(-0.15, 0.08, ry);
    float skyBlend = smoothstep(0.05, 0.45, ry);
    float3 analyticEnv = lerp(groundCol, horizonCol, horizonBlend);
    analyticEnv = lerp(analyticEnv, skyCol, skyBlend);

    float3 env = lerp(probeEnv, analyticEnv, 0.45);
    env *= lerp(0.40, 1.0, smoothstep(-0.25, 0.15, ry));
    return max(0, env) * shadowOcc;
   }

   float4 frag(v2f i, float facing : VFACE) : SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 n = normalize(i.worldNormal) * (facing < 0 ? -1 : 1);
    float3 v = normalize(_WorldSpaceCameraPos - i.worldPosition);
    float3 l = normalize(UnityWorldSpaceLightDir(i.worldPosition));
    float3 h = normalize(l + v);

    float nl = saturate(dot(n, l));
    float nv = max(saturate(dot(n, v)), 0.001);
    float nh = saturate(dot(n, h));
    float lh = saturate(dot(l, h));

    float3 baseColor = saturate(ActiveToLinear(_Color.rgb));
    float metallic = saturate(_Metallic);

    // Subtle natural view-angle flop: smooth without harsh threshold
    float flopFactor = lerp(0.72, 1.04, pow(nv, 0.60));
    float3 albedo = baseColor * flopFactor;

    // Normal-Y hemisphere shaping: upward surfaces (hood/roof) catch full ambient; downward sills shade smoothly
    float normalHemisphere = lerp(0.45, 1.0, smoothstep(-0.35, 0.60, n.y));

    float baseRoughness = clamp(1.0 - saturate(_Glossiness), 0.12, 0.45);
    float coat = saturate(_CoatStrength);
    float coatRoughness = clamp(_CoatRoughness, 0.03, 0.20);

    // Dielectric basecoat + subtle metallic tint F0
    float3 f0 = lerp(float3(0.045, 0.045, 0.048), baseColor, metallic * 0.75);
    float3 f = Fresnel(f0, lh);

    // Clear-coat Fresnel (IOR ~ 1.5 -> F0 = 0.04)
    float coatFV = 0.04 + 0.96 * PaintPow5(1.0 - nv);
    float coatFL = 0.04 + 0.96 * PaintPow5(1.0 - nl);
    float baseTransmission = (1.0 - coat * coatFV) * (1.0 - coat * coatFL);

    // Direct lighting: Preserve full diffuse for authentic painted automotive metal!
    float3 diffuse = (1.0 - f) * (1.0 - metallic * 0.45) * albedo;
    float3 baseSpec = GGXSmith(nl, nv, nh, baseRoughness) * f * 0.45;
    float coatSpec = GGXSmith(nl, nv, nh, coatRoughness) * (0.04 + 0.96 * PaintPow5(1.0 - lh)) * coat * 0.55;

    float3 light = _LightColor0.rgb;
    #if defined(UNITY_COLORSPACE_GAMMA)
     float intensity = max(max(light.r, light.g), max(light.b, 0.0001));
     light = ActiveToLinear(light / intensity) * intensity;
    #endif

    UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPosition);
    float3 directColor = ((diffuse + baseSpec) * baseTransmission + coatSpec) * light * (nl * attenuation * 0.85);

    // Ambient occlusion under dappled shadows
    float shadowOcclusion = lerp(0.48, 1.0, attenuation);

    // Smooth spherical harmonics + hemisphere ambient
    float4 shNormal = float4(n, 1);
    float3 shAmbient = max(0, SHEvalLinearL0L1(shNormal) + SHEvalLinearL2(shNormal));
    float3 skyAmb = float3(0.36, 0.40, 0.46);
    float3 groundAmb = float3(0.08, 0.085, 0.09);
    float3 hemiAmb = lerp(groundAmb, skyAmb, saturate(n.y * 0.5 + 0.5));
    float3 ambient = lerp(shAmbient, hemiAmb, 0.50) * shadowOcclusion * normalHemisphere;

    float3 fv = Fresnel(f0, nv);
    float3 indirectDiffuse = ambient * albedo * (1.0 - metallic * 0.40) * (1.0 - fv) * baseTransmission;

    // Dual-layer environment reflections
    float3 reflection = reflect(-v, n);
    float3 envF = f0 + (max((1.0 - baseRoughness).xxx, f0) - f0) * PaintPow5(1.0 - nv);
    float3 baseEnv = SampleAutomotiveEnv(reflection, i.worldPosition, baseRoughness, shadowOcclusion) * envF * baseTransmission * (0.35 + 0.65 * metallic);
    float3 coatEnv = SampleAutomotiveEnv(reflection, i.worldPosition, coatRoughness, lerp(0.55, 1.0, attenuation)) * coatFV * coat;

    float3 color = directColor + indirectDiffuse + baseEnv + coatEnv;

    float4 result = float4(LinearToActive(max(0, color)), 1.0);
    UNITY_APPLY_FOG(i.fogCoord, result);
    return result;
   }
   ENDCG
  }
  UsePass "Standard/SHADOWCASTER"
 }
 Fallback "Standard"
}
