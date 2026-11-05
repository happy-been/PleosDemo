Shader "ParkingLot/UtilityWireAA" {
    Properties { _Color("Wire color",Color)=(.10,.105,.11,1) _MinHalfPixels("Minimum filter half width",Range(.5,2))=.9 }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off Cull Off
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            fixed4 _Color;float _MinHalfPixels;
            // Position is the wire centre, normal its tangent, UV=(side sign, physical radius).
            struct appdata {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
            struct v2f {float4 pos:SV_POSITION;float side:TEXCOORD0;float coverage:TEXCOORD1;UNITY_FOG_COORDS(2)};
            v2f vert(appdata v){
                v2f o;float3 centre=mul(unity_ObjectToWorld,v.vertex).xyz;
                float3 tangent=normalize(UnityObjectToWorldDir(v.normal));
                float3 view=normalize(lerp(_WorldSpaceCameraPos-centre,UNITY_MATRIX_V[2].xyz,unity_OrthoParams.w));
                float3 across=cross(tangent,view);float length2=dot(across,across);
                across=length2>1e-8?across*rsqrt(length2):normalize(cross(tangent,float3(0,1,0)));
                float depth=max(.01,-mul(UNITY_MATRIX_V,float4(centre,1)).z);
                float pixel=2*lerp(depth,1,unity_OrthoParams.w)/max(1,_ScreenParams.y*abs(UNITY_MATRIX_P[1][1]));
                float radius=v.uv.y,halfWidth=max(radius,pixel*_MinHalfPixels);
                o.pos=mul(UNITY_MATRIX_VP,float4(centre+across*(v.uv.x*halfWidth),1));
                o.side=v.uv.x;o.coverage=saturate(radius/halfWidth);UNITY_TRANSFER_FOG(o,o.pos);return o;
            }
            fixed4 frag(v2f i):SV_Target {
                float footprint=max(fwidth(i.side),.001);
                float edge=saturate((1-abs(i.side))/footprint);
                fixed4 c=_Color;c.a*=edge*i.coverage;
                UNITY_APPLY_FOG(i.fogCoord,c);return c;
            }
            ENDCG
        }
    }
}
