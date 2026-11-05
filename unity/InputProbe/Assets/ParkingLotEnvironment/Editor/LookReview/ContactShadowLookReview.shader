Shader "ParkingLot/WholeLookContactReview" {
    Properties { _MainTex ("Contact transmission", 2D) = "white" {} }
    SubShader {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend DstColor Zero
        ZWrite Off
        ZTest LEqual
        Cull Off
        Offset -1, -1
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v) {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target {
                fixed shade = pow(max(.02,tex2D(_MainTex, i.uv).r),1.45);
                fixed4 color = fixed4(shade, shade, shade, 1);
                // Fog attenuates the shadow toward white (no multiplication), never toward a gray rectangle.
                UNITY_APPLY_FOG_COLOR(i.fogCoord, color, fixed4(1,1,1,1));
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
