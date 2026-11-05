Shader "Hidden/ParkingLot/WholeLookReview" {
Properties { _MainTex("Source",2D)="white"{} }
SubShader { Cull Off ZWrite Off ZTest Always
Pass {
CGPROGRAM
#pragma vertex vert_img
#pragma fragment frag
#pragma target 3.0
#include "UnityCG.cginc"
sampler2D _MainTex,_CameraDepthNormalsTexture;
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
float4 _MainTex_TexelSize,_ProjectionScale;
float _Exposure,_ContactStrength,_GammaInput;
float eyeDepth(float2 uv){return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture,uv));}
float3 viewPosition(float2 uv,float z){return float3((uv*2-1)/_ProjectionScale.xy,1)*z;}
float3 grade(float3 c){return saturate((c*(2.51*c+.03))/(c*(2.43*c+.59)+.14));}
float4 frag(v2f_img i):SV_Target {
    float2 uv=i.uv,px=abs(_MainTex_TexelSize.xy);float3 color=tex2D(_MainTex,uv).rgb;
    // Camera-local edge antialiasing is also used in the actual Game view.
    float3 nw=tex2D(_MainTex,uv+px*float2(-1,-1)).rgb,ne=tex2D(_MainTex,uv+px*float2(1,-1)).rgb;
    float3 sw=tex2D(_MainTex,uv+px*float2(-1,1)).rgb,se=tex2D(_MainTex,uv+px*float2(1,1)).rgb;
    const float3 L=float3(.299,.587,.114);float lnw=dot(nw,L),lne=dot(ne,L),lsw=dot(sw,L),lse=dot(se,L),lm=dot(color,L);
    float2 dir=float2(-((lnw+lne)-(lsw+lse)),(lnw+lsw)-(lne+lse));
    float reduce=max((lnw+lne+lsw+lse)*.03125,.0078125);dir=clamp(dir/(min(abs(dir.x),abs(dir.y))+reduce),-8,8)*px;
    float3 a=.5*(tex2D(_MainTex,uv+dir*(-1.0/6.0)).rgb+tex2D(_MainTex,uv+dir*(1.0/6.0)).rgb);
    float3 b=a*.5+.25*(tex2D(_MainTex,uv-dir*.5).rgb+tex2D(_MainTex,uv+dir*.5).rgb);
    float lb=dot(b,L),lo=min(lm,min(min(lnw,lne),min(lsw,lse))),hi=max(lm,max(max(lnw,lne),max(lsw,lse)));color=(lb<lo||lb>hi)?a:b;
    if(_GammaInput>.5)color=GammaToLinearSpace(color);
    color=grade(max(0,color)*_Exposure)/grade(float3(1.6,1.6,1.6)*_Exposure);
    float luma=dot(color,float3(.2126,.7152,.0722));color=lerp(luma.xxx,color,1.0);
    if(_GammaInput>.5)color=LinearToGammaSpace(color);
    return float4(color,1);
}
ENDCG
}
} Fallback Off
}
