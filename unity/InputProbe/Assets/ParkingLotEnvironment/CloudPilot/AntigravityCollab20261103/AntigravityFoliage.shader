Shader "ParkingLot/AntigravityFoliage" {
 Properties {
  _Color("Leaf tint", Color) = (1, 1, 1, 1)
  _MainTex("Photographed leaf color", 2D) = "white" {}
  _AlphaTex("Separate silhouette mask", 2D) = "white" {}
  _BumpMap("Normal", 2D) = "bump" {}
  _OcclusionMap("Occlusion", 2D) = "white" {}
  _RoughnessMap("Roughness", 2D) = "white" {}
  _Cutoff("Cutout", Range(0, 1)) = 0.44
  _SunTransmission("Sun Translucency", Range(0, 1)) = 0.25
 }
 SubShader {
  Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
  Cull Off
  CGPROGRAM
  #pragma surface surf Standard alphatest:_Cutoff addshadow fullforwardshadows vertex:LeafWind
  #pragma target 3.0
  sampler2D _MainTex, _AlphaTex, _BumpMap, _OcclusionMap, _RoughnessMap;
  fixed4 _Color;
  float _SunTransmission;
  float _ParkingLotWindStrength, _ParkingLotWindTime, _ParkingLotWindTimeOverride;

  struct Input {
   float2 uv_MainTex;
   float3 worldPos;
   float facing : VFACE;
  };

  void LeafWind(inout appdata_full v) {
   float t = _ParkingLotWindTimeOverride > 0.5 ? _ParkingLotWindTime : _Time.y;
   if (_ParkingLotWindStrength <= 0 || t == 0) return;
   float3 p = mul(unity_ObjectToWorld, v.vertex).xyz;
   float phase = dot(p.xz, float2(0.43, 0.37)) + p.y * 1.7;
   float sway = sin(t * 0.85 + phase) - sin(phase);
   float flutter = sin(t * 2.1 + phase * 1.93) - sin(phase * 1.93);
   float amplitude = min(_ParkingLotWindStrength, 0.02) * saturate(p.y * 0.6);
   p += float3(sway, flutter * 0.12, sway * 0.37 + flutter * 0.18) * amplitude;
   v.vertex = mul(unity_WorldToObject, float4(p, 1));
  }

  void surf(Input IN, inout SurfaceOutputStandard o) {
   float3 texCol = tex2D(_MainTex, IN.uv_MainTex).rgb * _Color.rgb;
   float luma = dot(texCol, float3(0.299, 0.587, 0.114));

   // Natural Korean summer green canopy — strictly clamped so it NEVER washes out to white in Gamma space
   float3 targetGreen = float3(0.24, 0.38, 0.11) * (0.55 + 0.85 * luma);
   float3 blended = lerp(texCol * 0.95, targetGreen, 0.55);
   o.Albedo = clamp(blended, float3(0.08, 0.14, 0.04), float3(0.36, 0.50, 0.18));

   o.Alpha = tex2D(_AlphaTex, IN.uv_MainTex).r;

   float3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
   n.xy *= 0.45;
   n.z *= (IN.facing >= 0 ? 1.0 : -1.0);
   o.Normal = normalize(n);

   float occ = lerp(0.55, 1.0, tex2D(_OcclusionMap, IN.uv_MainTex).r);
   o.Occlusion = occ;
   // Very subtle ambient fill so shadow side isn't pitch black, but zero white blowout
   o.Emission = o.Albedo * 0.05 * _SunTransmission * occ;
   o.Metallic = 0.0;
   o.Smoothness = 0.18;
  }
  ENDCG
 }
 Fallback "Transparent/Cutout/Diffuse"
}
