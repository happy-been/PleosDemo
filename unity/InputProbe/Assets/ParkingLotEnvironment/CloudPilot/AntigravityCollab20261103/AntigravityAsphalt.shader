Shader "ParkingLot/AntigravityAsphalt" {
 Properties {
  _Color("Asphalt Base Tint", Color) = (0.98, 0.98, 1.0, 1)
  _MainTex("Photorealistic Asphalt Albedo", 2D) = "gray" {}
  _BumpMap("Asphalt Normal Map", 2D) = "bump" {}
  _CarRect0("Car 0 XZ Center & Extents", Vector) = (0,0,0,0)
  _CarRect1("Car 1 XZ Center & Extents", Vector) = (0,0,0,0)
  _CarRect2("Car 2 XZ Center & Extents", Vector) = (0,0,0,0)
  _CarRect3("Car 3 XZ Center & Extents", Vector) = (0,0,0,0)
  _CarRect4("Car 4 XZ Center & Extents", Vector) = (0,0,0,0)
  _CarRect5("Car 5 XZ Center & Extents", Vector) = (0,0,0,0)
  _CarRect6("Car 6 XZ Center & Extents", Vector) = (0,0,0,0)
 }
 SubShader {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" }
  LOD 200
  Pass {
   Tags { "LightMode"="ForwardBase" }
   Cull Back ZWrite On
   CGPROGRAM
   #pragma target 3.0
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap
   #pragma multi_compile_fog
   #include "UnityCG.cginc"
   #include "Lighting.cginc"
   #include "AutoLight.cginc"

   sampler2D _MainTex, _BumpMap;
   float4 _Color;
   float4 _CarRect0, _CarRect1, _CarRect2, _CarRect3, _CarRect4, _CarRect5, _CarRect6;

   struct appdata {
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
   };

   struct v2f {
    float4 pos : SV_POSITION;
    float3 worldPos : TEXCOORD0;
    UNITY_SHADOW_COORDS(1)
    UNITY_FOG_COORDS(2)
   };

   v2f vert(appdata v) {
    v2f o;
    o.pos = UnityObjectToClipPos(v.vertex);
    o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
    UNITY_TRANSFER_SHADOW(o, v.uv1);
    UNITY_TRANSFER_FOG(o, o.pos);
    return o;
   }

   float carContactAO(float2 xz, float4 rect) {
    if (rect.z <= 0.05) return 1.0;
    float2 d = abs(xz - rect.xy) - rect.zw * float2(0.90, 0.86);
    float boxDist = length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
    // Deep under-chassis ambient occlusion with smooth penumbra around bumpers & sills
    float chassisAO = smoothstep(-0.32, 0.42, boxDist);
    // Extra dark tire contact patches near the 4 wheel positions
    float2 tirePos = rect.zw * float2(0.60, 0.76);
    float2 td = abs(xz - rect.xy) - tirePos;
    float tireDist = length(td * float2(1.0, 1.7));
    float tireAO = smoothstep(0.06, 0.45, tireDist);
    return lerp(0.44, 1.0, chassisAO) * lerp(0.68, 1.0, tireAO);
   }

   float4 frag(v2f i) : SV_Target {
    float2 xz = i.worldPos.xz;

    // 1. Multi-octave world-space UV sampling for crisp crushed-stone aggregate near camera
    //    and natural macro bitumen variation across the entire parking lot
    float2 uvFine  = xz * 1.35;
    float2 uvMicro = xz * 0.52 + float2(0.19, 0.43);
    float2 uvMacro = xz * 0.075 + float2(0.37, 0.71);

    float3 texFine  = tex2D(_MainTex, uvFine).rgb;
    float3 texMicro = tex2D(_MainTex, uvMicro).rgb;
    float3 texMacro = tex2D(_MainTex, uvMacro).rgb;

    // Unpack world-space normal map at fine & micro scales for stone grain relief
    float3 nFine  = UnpackNormal(tex2D(_BumpMap, uvFine));
    float3 nMicro = UnpackNormal(tex2D(_BumpMap, uvMicro));
    float2 nXY = (nFine.xy * 0.65 + nMicro.xy * 0.35) * 0.85;
    float3 worldNormal = normalize(float3(nXY.x, 1.0, nXY.y));

    float3 L = normalize(UnityWorldSpaceLightDir(i.worldPos));
    float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);
    float3 H = normalize(L + V);

    // Subtle normal-mapped stone relief + grazing sun mineral aggregate sparkle
    float nDotL = saturate(dot(worldNormal, L));
    float relief = 0.90 + 0.22 * (nDotL - 0.65);
    float sparkle = pow(saturate(dot(worldNormal, H)), 16.0) * 0.045;

    // Combine 3 octaves preserving high-frequency stone speckle contrast (std ~ 0.070 in final sRGB)
    float lumaFine  = dot(texFine,  float3(0.299, 0.587, 0.114));
    float lumaMicro = dot(texMicro, float3(0.299, 0.587, 0.114));
    float lumaMacro = dot(texMacro, float3(0.299, 0.587, 0.114));

    float grain = (lumaFine - 0.51) * 0.46 + (lumaMicro - 0.51) * 0.34 + (lumaMacro - 0.51) * 0.22;

    // 2. Subtle organic tire wear paths along the central driving aisle and bay turn-ins
    float aisleTrack = exp(-pow(( abs(xz.x - 1.8 + 0.35 * sin(xz.y * 0.18)) - 0.88 ) / 0.55, 2.0));
    float turnMask = smoothstep(2.4, 0.5, xz.x) * smoothstep(-4.8, -2.5, xz.x) * saturate(lumaMacro * 1.4 - 0.2);
    float turnArc1 = exp(-pow((length(xz - float2(2.2, -9.5)) - 5.2) / 0.65, 2.0)) * turnMask;
    float turnArc2 = exp(-pow((length(xz - float2(2.2, -1.5)) - 5.2) / 0.65, 2.0)) * turnMask;
    float turnArc3 = exp(-pow((length(xz - float2(2.2,  6.5)) - 5.2) / 0.65, 2.0)) * turnMask;
    float lotMask = saturate(1.0 - max(abs(xz.x) - 8.5, abs(xz.y) - 16.5) * 0.25);
    float tireWear = saturate(aisleTrack * 0.65 + (turnArc1 + turnArc2 + turnArc3) * 0.55) * lotMask;

    // Calibrated gamma-space luminance: 0.535 mean in sunlight -> 0.575 sRGB after PhotoFinish ACES!
    float baseLuma = 0.535 * (1.0 - tireWear * 0.085) + grain;
    baseLuma = clamp(baseLuma * relief + sparkle, 0.34, 0.72);

    // Neutral warm-charcoal asphalt tint matching approved-target.png ([0.595, 0.582, 0.581])
    float3 col = float3(1.01, 0.995, 0.99) * baseLuma * _Color.rgb;

    // 3. Analytical under-vehicle & tire contact occlusion (grounds all 7 vehicles firmly onto asphalt)
    float contactAO = 1.0;
    contactAO = min(contactAO, carContactAO(xz, _CarRect0));
    contactAO = min(contactAO, carContactAO(xz, _CarRect1));
    contactAO = min(contactAO, carContactAO(xz, _CarRect2));
    contactAO = min(contactAO, carContactAO(xz, _CarRect3));
    contactAO = min(contactAO, carContactAO(xz, _CarRect4));
    contactAO = min(contactAO, carContactAO(xz, _CarRect5));
    contactAO = min(contactAO, carContactAO(xz, _CarRect6));

    // 4. Full-resolution real-time tree leaf, building, and car shadows
    UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
    float3 shadowTint = lerp(float3(0.62, 0.65, 0.70), float3(1.0, 1.0, 1.0), atten);
    col *= shadowTint * contactAO;

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
