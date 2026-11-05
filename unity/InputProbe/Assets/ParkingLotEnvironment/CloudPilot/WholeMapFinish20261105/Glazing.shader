Shader "ParkingLot/WholeMap/Glazing" {
 Properties {_Color("Interior tint",Color)=(.035,.046,.052,1) _Glossiness("Smoothness",Range(0,1))=.94}
 SubShader {Tags {"RenderType"="Opaque"} LOD 300 Cull Off
 CGPROGRAM
 #pragma surface surf StandardSpecular fullforwardshadows addshadow
 #pragma target 3.0
 struct Input {float3 viewDir;float3 worldPos;};fixed4 _Color;half _Glossiness;
 void surf(Input IN,inout SurfaceOutputStandardSpecular o){float window=smoothstep(.92,1.02,IN.worldPos.y);o.Normal=float3(0,0,IN.viewDir.z<0?-1:1);o.Albedo=lerp(float3(.38,.4,.41),float3(.065,.083,.096),window);o.Specular=lerp(float3(.6,.62,.65),float3(.12,.12,.12),window);o.Emission=lerp(float3(.012,.013,.014),float3(.008,.012,.017),window);o.Smoothness=lerp(.84,_Glossiness,window);o.Alpha=1;}
 ENDCG
 }Fallback "Standard"
}
