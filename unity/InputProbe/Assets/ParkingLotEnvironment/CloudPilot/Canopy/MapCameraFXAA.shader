Shader "Hidden/Parkside/CloudPilot/MapCameraFXAA" { Properties {_MainTex("Source",2D)="white"{}} SubShader {Cull Off ZWrite Off ZTest Always Pass {
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #include "UnityCG.cginc"
 sampler2D _MainTex; float4 _MainTex_TexelSize;
 fixed4 frag(v2f_img i):SV_Target {
 float2 uv=i.uv,px=_MainTex_TexelSize.xy;float3 nw=tex2D(_MainTex,uv+float2(-1,-1)*px).rgb,ne=tex2D(_MainTex,uv+float2(1,-1)*px).rgb,sw=tex2D(_MainTex,uv+float2(-1,1)*px).rgb,se=tex2D(_MainTex,uv+px).rgb,m=tex2D(_MainTex,uv).rgb;float3 luma=float3(.299,.587,.114);float lnw=dot(nw,luma),lne=dot(ne,luma),lsw=dot(sw,luma),lse=dot(se,luma),lm=dot(m,luma);float low=min(lm,min(min(lnw,lne),min(lsw,lse))),high=max(lm,max(max(lnw,lne),max(lsw,lse)));if(high-low<max(.0312,high*.125))return float4(m,1);
 float2 dir=float2(-((lnw+lne)-(lsw+lse)),(lnw+lsw)-(lne+lse));float reduce=max((lnw+lne+lsw+lse)*(.25*.125),1.0/128.0);float rcpMin=1.0/(min(abs(dir.x),abs(dir.y))+reduce);dir=clamp(dir*rcpMin,-8.0,8.0)*px;
 float3 a=.5*(tex2D(_MainTex,uv+dir*(1.0/3.0-.5)).rgb+tex2D(_MainTex,uv+dir*(2.0/3.0-.5)).rgb);float3 b=a*.5+.25*(tex2D(_MainTex,uv+dir*-.5).rgb+tex2D(_MainTex,uv+dir*.5).rgb);float lb=dot(b,luma);return float4(lb<low||lb>high?a:b,1);
 }
 ENDCG
 }} Fallback Off }
