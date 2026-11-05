Shader "ParkingLot/AntigravityGlass" {
 Properties {
  _Color("Tint and transmission", Color) = (0.02, 0.03, 0.028, 1.0)
  _MainTex("Lens texture", 2D) = "white" {}
  _Glossiness("Smoothness", Range(0, 1)) = 0.96
  _Metallic("Metallic", Range(0, 1)) = 0.0
 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Cull Off
  ZWrite On
  Blend SrcAlpha OneMinusSrcAlpha

  Pass {
   Tags { "LightMode"="ForwardBase" }
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fwdbase
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   #include "AutoLight.cginc"
   #include "UnityStandardUtils.cginc"

   sampler2D _MainTex;
   float4 _Color;
   float _Glossiness;

   struct appdata {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
   };

   struct v2f {
    float4 pos : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    float3 worldNormal : TEXCOORD1;
    float2 uv : TEXCOORD2;
    UNITY_SHADOW_COORDS(3)
    UNITY_FOG_COORDS(4)
   };

   v2f vert(appdata v) {
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
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
     return max(0, c);
    #endif
   }

   float3 SampleBoxReflectionLinear(float3 reflDir, float3 worldPos, float roughness) {
    float mip = roughness * 4.0;
    float3 dir = reflDir;
    #if defined(UNITY_SPECCUBE_BOX_PROJECTION)
     dir = BoxProjectedCubemapDirection(reflDir, worldPos, unity_SpecCube0_ProbePosition, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax);
    #endif
    float4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, dir, mip);
    return ActiveToLinear(DecodeHDR(rgbm, unity_SpecCube0_HDR));
   }

   float4 frag(v2f i) : SV_Target {
    float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
    float3 l = normalize(UnityWorldSpaceLightDir(i.worldPos));
    float3 h = normalize(l + v);

    float3 rawN = i.worldNormal;
    float lenN = length(rawN);
    rawN = (lenN > 0.001) ? (rawN / lenN) : float3(0, 1, 0);
    if (dot(rawN, v) < 0) rawN = -rawN;

    // Separate cabin windows (worldPos.y >= 1.00m) from lower bumper headlights/lamps (worldPos.y < 0.98m)
    float isWindow = smoothstep(0.97, 1.01, i.worldPos.y);

    // Windows use smooth dome normal to eliminate low-poly triangulation creases; headlights use true lens normal
    float3 smoothDomeN = normalize(float3(rawN.x * 0.24, 0.76, rawN.z * 0.24) + v * 0.22);
    float3 n = normalize(lerp(rawN, lerp(smoothDomeN, rawN, 0.14), isWindow));

    float nv = saturate(dot(n, v));
    float nh = saturate(dot(n, h));

    UNITY_LIGHT_ATTENUATION(attenuation, i, i.worldPos);
    float shadowFactor = lerp(0.55, 1.0, attenuation);

    float roughness = max(0.03, 1.0 - _Glossiness);
    float3 reflDir = reflect(-v, n);
    reflDir.y = max(reflDir.y, 0.18 * isWindow);
    reflDir = normalize(reflDir);

    float3 probeCol = SampleBoxReflectionLinear(reflDir, i.worldPos, roughness);

    float skyGrad = saturate(reflDir.y * 1.5);
    float3 neutralSky = lerp(float3(0.16, 0.18, 0.20), float3(0.36, 0.40, 0.45), skyGrad);
    float3 envCol = lerp(probeCol, neutralSky, 0.50) * shadowFactor;

    float fresnel = 0.06 + 0.94 * pow(1.0 - nv, 3.2);
    float specPow = exp2(10.0 * _Glossiness + 1.0);
    float3 lightLinear = ActiveToLinear(_LightColor0.rgb);
    float3 sunSpec = lightLinear * pow(nh, specPow) * fresnel * attenuation * 0.50;

    // 1. Cabin Window Appearance: Neutral dark charcoal-green smoked automotive glass (opaque 0.99 alpha)
    float3 windowBase = float3(0.015, 0.018, 0.017);
    float3 windowLinear = lerp(windowBase, envCol, saturate(fresnel * 0.65 + 0.14)) + sunSpec;

    // 2. Headlamp Appearance: Clear transparent polycarbonate outer lens showing inner chrome reflector
    float3 lensTex = ActiveToLinear(tex2D(_MainTex, i.uv).rgb);
    float3 headlightLinear = lerp(lensTex * 0.95 + float3(0.30, 0.32, 0.35), envCol, fresnel * 0.60) + sunSpec * 1.5;

    float3 finalLinear = lerp(headlightLinear, windowLinear, isWindow);
    // Headlamps are transparent (0.35 alpha) so inner bright reflector is visible; windows are opaque (0.99 alpha)
    float finalAlpha = lerp(0.35, 0.99, isWindow);

    float4 col = float4(LinearToActive(max(0, finalLinear)), finalAlpha);
    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
   }
   ENDCG
  }
 }
 Fallback "Transparent/VertexLit"
}
