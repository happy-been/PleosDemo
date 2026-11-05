Shader "Hidden/ParkingLot/AntigravityPhotoFinish" {
 Properties {
  _MainTex("Source", 2D) = "white" {}
 }
 SubShader {
  Cull Off ZWrite Off ZTest Always
  Pass {
   CGPROGRAM
   #pragma vertex vert_img
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"

   sampler2D _MainTex, _CameraDepthNormalsTexture;
   UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
   float4 _ProjectionScale, _MainTex_TexelSize;
   float _ContactStrength, _Radius, _GammaInput, _Exposure;

   float eyeDepth(float2 uv) {
    return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
   }

   float3 viewPosition(float2 uv, float z) {
    return float3((uv * 2.0 - 1.0) * z / _ProjectionScale.xy, -z);
   }

   float sampleOcclusion(float2 uv, float2 dir, float2 uvRadius, float3 center, float3 normal) {
    float2 q = uv + dir * uvRadius;
    if (any(q < 0) || any(q > 1)) return 0;
    float d = eyeDepth(q);
    float3 delta = viewPosition(q, d) - center;
    float dist = length(delta);
    float range = saturate(1.0 - dist / _Radius);
    float angle = saturate(dot(normal, delta / max(dist, 0.0001)) - 0.14);
    return angle * range;
   }

   float3 ACESFilm(float3 x) {
    return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
   }

   float4 frag(v2f_img i) : SV_Target {
    float2 uv = i.uv;
    float2 px = abs(_MainTex_TexelSize.xy);

    // 1. FXAA Edge Antialiasing
    float3 color = tex2D(_MainTex, uv).rgb;
    float3 nw = tex2D(_MainTex, uv + px * float2(-1, -1)).rgb;
    float3 ne = tex2D(_MainTex, uv + px * float2( 1, -1)).rgb;
    float3 sw = tex2D(_MainTex, uv + px * float2(-1,  1)).rgb;
    float3 se = tex2D(_MainTex, uv + px * float2( 1,  1)).rgb;
    const float3 L = float3(0.299, 0.587, 0.114);
    float lnw = dot(nw, L), lne = dot(ne, L), lsw = dot(sw, L), lse = dot(se, L), lm = dot(color, L);
    float2 dir = float2(-((lnw + lne) - (lsw + lse)), (lnw + lsw) - (lne + lse));
    float reduce = max((lnw + lne + lsw + lse) * 0.03125, 0.0078125);
    dir = clamp(dir / (min(abs(dir.x), abs(dir.y)) + reduce), -8.0, 8.0) * px;
    float3 a = 0.5 * (tex2D(_MainTex, uv + dir * (-1.0 / 6.0)).rgb + tex2D(_MainTex, uv + dir * (1.0 / 6.0)).rgb);
    float3 b = a * 0.5 + 0.25 * (tex2D(_MainTex, uv - dir * 0.5).rgb + tex2D(_MainTex, uv + dir * 0.5).rgb);
    float lb = dot(b, L);
    float lo = min(lm, min(min(lnw, lne), min(lsw, lse)));
    float hi = max(lm, max(max(lnw, lne), max(lsw, lse)));
    color = (lb < lo || lb > hi) ? a : b;

    if (_GammaInput > 0.5) color = GammaToLinearSpace(color);

    // 2. 12-tap Depth+Normals SSAO
    float z = eyeDepth(uv);
    if (z < _ProjectionParams.z * 0.995 && _ContactStrength > 0.001) {
     float4 packed = tex2D(_CameraDepthNormalsTexture, uv);
     float decodedDepth;
     float3 normal;
     DecodeDepthNormal(packed, decodedDepth, normal);
     normal = normalize(normal);
     float2 r = min(float2(0.04, 0.04), _Radius * abs(_ProjectionScale.xy) / (2.0 * max(z, 0.1)));
     float3 p = viewPosition(uv, z);
     float sum = 0;
     sum += sampleOcclusion(uv, float2( 1, 0), r, p, normal);
     sum += sampleOcclusion(uv, float2(-1, 0), r, p, normal);
     sum += sampleOcclusion(uv, float2(0,  1), r, p, normal);
     sum += sampleOcclusion(uv, float2(0, -1), r, p, normal);
     sum += sampleOcclusion(uv, float2( 0.707,  0.707), r, p, normal);
     sum += sampleOcclusion(uv, float2(-0.707,  0.707), r, p, normal);
     sum += sampleOcclusion(uv, float2( 0.707, -0.707), r, p, normal);
     sum += sampleOcclusion(uv, float2(-0.707, -0.707), r, p, normal);
     sum += sampleOcclusion(uv, float2( 0.45,  0.18), r, p, normal);
     sum += sampleOcclusion(uv, float2(-0.45, -0.18), r, p, normal);
     sum += sampleOcclusion(uv, float2( 0.18, -0.45), r, p, normal);
     sum += sampleOcclusion(uv, float2(-0.18,  0.45), r, p, normal);
     float distFade = saturate(1.0 - (z - 15.0) / 45.0);
     float occ = min(0.32, _ContactStrength * (sum / 12.0)) * distFade;
     float3 aoFactor = lerp(float3(1, 1, 1), float3(0.55, 0.59, 0.67), occ);
     color *= aoFactor;
    }

    // 3. Controlled Daylight Exposure (0.80 default + soft highlight rolloff prevents asphalt road from blowing out after bake!)
    float exposure = _Exposure > 0.05 ? _Exposure : 0.80;
    color = max(0.0, color) * exposure;
    // Reinhard-style highlight shoulder compression before ACES so baked bright asphalt retains crisp grain
    color = color / (1.0 + color * 0.28);
    color = ACESFilm(color);

    float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
    color = lerp(luma.xxx, color, 0.96);

    if (_GammaInput > 0.5) color = LinearToGammaSpace(color);
    return float4(color, 1.0);
   }
   ENDCG
  }
 }
 Fallback Off
}
