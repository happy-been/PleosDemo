Shader "Parkside/Lookdev/Courtyard Asphalt"
{
    Properties
    {
        _MainTex ("Scanned asphalt colour", 2D) = "white" {}
        _BumpMap ("Scanned asphalt normal", 2D) = "bump" {}
        _Packed ("Metal / AO / unused / smoothness", 2D) = "white" {}
        _WearMask ("Variation / cracks / paint / oil", 2D) = "black" {}
        _WorldSize ("Road width and depth", Vector) = (36,32,0,0)
        _TileMetres ("Scan width in metres", Float) = 2
        _Colour ("Asphalt tint", Color) = (0.6,0.68,0.73,1)
        _PaintColour ("Weathered paint colour", Color) = (0.74,0.74,0.68,1)
        _NormalStrength ("Surface relief", Range(0,2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "UnityStandardUtils.cginc"
        sampler2D _MainTex, _BumpMap, _Packed, _WearMask;
        float4 _WorldSize;
        fixed4 _Colour, _PaintColour;
        float _TileMetres, _NormalStrength;
        struct Input { float3 worldPos; };
        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.worldPos.xz / _TileMetres;
            fixed4 surface = tex2D(_MainTex, uv);
            fixed4 packed = tex2D(_Packed, uv);
            fixed4 wear = tex2D(_WearMask, IN.worldPos.xz / _WorldSize.xy + 0.5);
            float3 asphalt = surface.rgb * _Colour.rgb * lerp(0.76,1.18,wear.r);
            asphalt *= 1 - wear.g * 0.55;
            asphalt *= 1 - wear.a * 0.45;
            o.Albedo = lerp(asphalt, _PaintColour.rgb * lerp(0.86,1.04,surface.r), wear.b);
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _NormalStrength * (1 - wear.b * 0.65));
            o.Metallic = 0;
            o.Smoothness = 0.035 + wear.b * 0.08 + wear.a * 0.03;
            o.Occlusion = lerp(0.8,1,packed.g);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
