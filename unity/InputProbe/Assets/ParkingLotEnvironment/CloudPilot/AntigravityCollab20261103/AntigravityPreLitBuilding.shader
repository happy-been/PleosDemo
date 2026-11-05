Shader "ParkingLot/AntigravityPreLitBuilding" {
 Properties {
  _Color("Tint", Color) = (1, 1, 1, 1)
  _MainTex("Photorealistic Facade Albedo", 2D) = "white" {}
  _BumpMap("Normal Map", 2D) = "bump" {}
  _BumpScale("Normal Scale", Float) = 0.75
  _MetallicGlossMap("Metallic (R) Occlusion (G) Smoothness (A)", 2D) = "white" {}
 }
 SubShader {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" }
  LOD 250
  Pass {
   Tags { "LightMode"="ForwardBase" }
   Cull Off ZWrite On
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   #include "AutoLight.cginc"
   #include "UnityStandardUtils.cginc"

   sampler2D _MainTex, _BumpMap, _MetallicGlossMap;
   float4 _MainTex_ST, _Color;
   float _BumpScale;

   struct appdata {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
   };

   struct v2f {
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 worldPos : TEXCOORD1;
    float3 worldNormal : TEXCOORD2;
    UNITY_SHADOW_COORDS(3)
    UNITY_FOG_COORDS(4)
   };

   v2f vert(appdata v) {
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = TRANSFORM_TEX(v.uv0, _MainTex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    o.worldNormal = UnityObjectToWorldNormal(v.normal);
    UNITY_TRANSFER_SHADOW(o, v.uv1);
    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
   }

   float3 SampleEnv(float3 reflDir, float3 worldPos, float roughness) {
    float mip = roughness * 5.0;
    float3 dir = reflDir;
    #if defined(UNITY_SPECCUBE_BOX_PROJECTION)
     dir = BoxProjectedCubemapDirection(reflDir, worldPos, unity_SpecCube0_ProbePosition, unity_SpecCube0_BoxMin, unity_SpecCube0_BoxMax);
    #endif
    float4 rgbm = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, dir, mip);
    return DecodeHDR(rgbm, unity_SpecCube0_HDR);
   }

   float4 frag(v2f i) : SV_Target {
    float3 photo = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
    float4 mos = tex2D(_MetallicGlossMap, i.uv);
    float smoothness = mos.a;

    float3 n = normalize(i.worldNormal);
    float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
    if (dot(n, v) < 0) n = -n;
    float3 l = normalize(UnityWorldSpaceLightDir(i.worldPos));

    // Clean concrete slab appearance on horizontal building roofs
    if (abs(n.y) > 0.85 && i.worldPos.y > 4.0) {
     photo = float3(0.54, 0.53, 0.51);
     smoothness = 0.15;
    }

    float nl = saturate(dot(n, l));
    UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);

    // Preserve 85% of the photographic daylight radiance so Unity's diffuse
    // never washes out tile grout lines or turns shaded walls into murky 2003 CG grey!
    float directTerm = nl * lerp(0.55, 1.0, atten);
    float3 sunTint = lerp(float3(0.86, 0.88, 0.93), float3(1.02, 1.00, 0.98), directTerm);

    // Subtle ground contact AO at the base of the building (0..0.7m)
    float groundAO = lerp(0.78, 1.0, saturate(i.worldPos.y / 0.75));
    float3 col = photo * sunTint * groundAO;

    // Add real 3D environment reflection onto dark window glass panes (where smoothness > 0.65)
    float isWindowGlass = smoothstep(0.60, 0.82, smoothness);
    if (isWindowGlass > 0.01) {
     float nv = saturate(dot(n, v));
     float fresnel = 0.12 + 0.65 * pow(1.0 - nv, 3.0);
     float3 reflDir = reflect(-v, n);
     reflDir.y = max(reflDir.y, 0.12);
     float3 envCol = SampleEnv(normalize(reflDir), i.worldPos, 0.12);
     col = lerp(col, col * 0.65 + envCol * 0.45, isWindowGlass * fresnel);
    }

    float4 result = float4(saturate(col), 1.0);
    UNITY_APPLY_FOG(i.fogCoord, result);
    return result;
   }
   ENDCG
  }
  UsePass "Standard/SHADOWCASTER"
 }
 Fallback "Diffuse"
}
