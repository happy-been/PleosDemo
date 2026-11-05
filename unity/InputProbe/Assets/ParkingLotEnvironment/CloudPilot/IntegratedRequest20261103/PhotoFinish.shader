Shader "Hidden/ParkingLot/IntegratedPhotoFinish" {
Properties { _MainTex("Source",2D)="white"{} }
SubShader { Cull Off ZWrite Off ZTest Always Pass {
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#include "UnityCG.cginc"
sampler2D _MainTex,_CameraDepthNormalsTexture;
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
float4 _ProjectionScale,_MainTex_TexelSize;float _ContactStrength,_Radius,_GammaInput;
float eyeDepth(float2 uv){return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv));}
float3 viewPosition(float2 uv,float z){return float3((uv*2-1)*z/_ProjectionScale.xy,-z);}
float sampleOcclusion(float2 uv,float2 dir,float2 uvRadius,float3 center,float3 normal){float2 q=uv+dir*uvRadius;if(any(q<0)||any(q>1))return 0;float d=eyeDepth(q);float3 delta=viewPosition(q,d)-center;float dist=length(delta);float range=saturate(1-dist/_Radius);float angle=saturate(dot(normal,delta/max(dist,.0001))-.12);return angle*range;}
float4 frag(v2f_img i):SV_Target {float2 uv=i.uv;float3 color=tex2D(_MainTex,uv).rgb;float z=eyeDepth(uv);float4 packed=tex2D(_CameraDepthNormalsTexture,uv);float decodedDepth;float3 normal;DecodeDepthNormal(packed,decodedDepth,normal);normal=normalize(normal);if(z<_ProjectionParams.z*.999){float2 r=min(float2(.05,.05),_Radius*abs(_ProjectionScale.xy)/(2*max(z,.1)));float3 p=viewPosition(uv,z);float sum=0;sum+=sampleOcclusion(uv,float2(1,0),r,p,normal);sum+=sampleOcclusion(uv,float2(-1,0),r,p,normal);sum+=sampleOcclusion(uv,float2(0,1),r,p,normal);sum+=sampleOcclusion(uv,float2(0,-1),r,p,normal);sum+=sampleOcclusion(uv,float2(.707,.707),r,p,normal);sum+=sampleOcclusion(uv,float2(-.707,.707),r,p,normal);sum+=sampleOcclusion(uv,float2(.707,-.707),r,p,normal);sum+=sampleOcclusion(uv,float2(-.707,-.707),r,p,normal);sum+=sampleOcclusion(uv,float2(.45,.18),r,p,normal);sum+=sampleOcclusion(uv,float2(-.45,-.18),r,p,normal);sum+=sampleOcclusion(uv,float2(.18,-.45),r,p,normal);sum+=sampleOcclusion(uv,float2(-.18,.45),r,p,normal);float ao=1-min(.42,_ContactStrength*sum/12.0);if(_GammaInput>.5)color=GammaToLinearSpace(color);color*=ao;if(_GammaInput>.5)color=LinearToGammaSpace(color);}return float4(color,1);}
ENDCG } } Fallback Off
}
