using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Bounded, disposable lighting comparison. Does not write the scene or source sky.
[InitializeOnLoad]
public static class ParkingLotSkyLightReview {
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotSkyLightReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/sky-light-review.request";if(!File.Exists(path))return;
        try{File.Delete(path);}catch(IOException){return;}
        try{Review();}catch(Exception e){File.WriteAllText(Output+"/sky-light-review-error.txt",DateTime.Now.ToString("s")+"\n"+e);Debug.LogException(e);}
    }
    static float Luma(Color c){return .2126f*c.r+.7152f*c.g+.0722f*c.b;}
    static Vector3 Direction(float u,float v){
        float longitude=(.5f-u)*2*Mathf.PI,latitude=(1-v)*Mathf.PI;
        return new Vector3(Mathf.Cos(longitude)*Mathf.Sin(latitude),Mathf.Cos(latitude),Mathf.Sin(longitude)*Mathf.Sin(latitude));
    }
    static Color[] Evaluate(SphericalHarmonicsL2 sh,Vector3[] dirs){var colors=new Color[dirs.Length];sh.Evaluate(dirs,colors);return colors;}
    static SphericalHarmonicsL2 Candidate(SphericalHarmonicsL2 baseline,StringBuilder log){
        var sky=RenderSettings.skybox;
        if(sky==null||sky.shader.name!="Skybox/Panoramic"||Mathf.Abs(sky.GetFloat("_Rotation"))>.001f)throw new InvalidOperationException("Review expects the current unrotated panoramic sky.");
        var source=sky.GetTexture("_MainTex");if(source==null)throw new InvalidOperationException("No source sky.");
        const int width=128,height=64;
        var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        Texture2D cpu=null;var previous=RenderTexture.active;Color[] pixels;
        try{
            Graphics.Blit(source,rt);RenderTexture.active=rt;
            cpu=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true);
            cpu.ReadPixels(new Rect(0,0,width,height),0,0);cpu.Apply(false,false);pixels=cpu.GetPixels();
        }finally{RenderTexture.active=previous;if(cpu!=null)UnityEngine.Object.DestroyImmediate(cpu);RenderTexture.ReleaseTemporary(rt);}
        var upper=pixels.Skip(width*height/2).Select(Luma).OrderBy(x=>x).ToArray();
        float median=upper[upper.Length/2],cap=median*3;
        if(!(median>0)||float.IsInfinity(cap))throw new InvalidDataException("Invalid HDR sample range.");
        Color mean=Color.black;float weight=0;int limited=0;
        for(int y=height/2;y<height;y++)for(int x=0;x<width;x++){
            Color c=pixels[y*width+x];float l=Luma(c);if(l>cap){c*=cap/l;limited++;}
            float w=Mathf.Sin((1-(y+.5f)/height)*Mathf.PI);mean+=c*w;weight+=w;
        }
        mean/=weight;
        // Sky-only source has no local terrain bounce: use a low neutral-warm ground term.
        Color ground=new Color(1,.95f,.87f)*Luma(mean)*.10f;
        var sh=new SphericalHarmonicsL2();
        for(int y=0;y<height;y++)for(int x=0;x<width;x++){
            var dir=Direction((x+.5f)/width,(y+.5f)/height);Color c=dir.y>=0?pixels[y*width+x]:ground;
            float l=Luma(c);if(l>cap)c*=cap/l;
            float solidAngle=2*Mathf.PI/width*Mathf.PI/height*Mathf.Sqrt(Mathf.Max(0,1-dir.y*dir.y));
            sh.AddDirectionalLight(dir,c,solidAngle/Mathf.PI);
        }
        var dirs=new Vector3[512];
        for(int i=0;i<dirs.Length;i++){float y=1-2*(i+.5f)/dirs.Length,a=i*2.39996323f,r=Mathf.Sqrt(1-y*y);dirs[i]=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);}
        var values=Evaluate(sh,dirs);var offset=Color.black;
        for(int c=0;c<3;c++)offset[c]=Mathf.Max(0,-values.Min(v=>v[c]));
        sh.AddAmbientLight(offset); // Remove any negative low-order SH ringing before exposure matching.
        values=Evaluate(sh,dirs);var old=Evaluate(baseline,dirs);
        for(int c=0;c<3;c++){
            float scale=old.Average(v=>v[c])/Mathf.Max(.00001f,values.Average(v=>v[c]));
            for(int k=0;k<9;k++)sh[c,k]*=scale;
        }
        values=Evaluate(sh,dirs);
        for(int i=0;i<values.Length;i++)for(int c=0;c<3;c++)if(float.IsNaN(values[i][c])||float.IsInfinity(values[i][c])||values[i][c]<-.0001f)throw new InvalidDataException("Non-finite/negative ambient sample.");
        log.AppendLine("Sky="+AssetDatabase.GetAssetPath(source)+"\nSamples="+pixels.Length+"\nUpperMedian="+median+"\nSunClamp="+cap+"\nClampedUpperSamples="+limited+"\nNegativeSHOffset="+offset+"\nMeanRGBMatchedToBaseline=True\nProbeValidationDirections="+dirs.Length);
        var axes=new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
        var a0=Evaluate(baseline,axes);var a1=Evaluate(sh,axes);
        for(int i=0;i<axes.Length;i++)log.AppendLine(axes[i]+": "+a0[i]+" -> "+a1[i]);
        return sh;
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved map before the sky comparison.");
        var mode=RenderSettings.ambientMode;var original=RenderSettings.ambientProbe;float intensity=RenderSettings.ambientIntensity;
        var log=new StringBuilder(DateTime.Now.ToString("s")+"\n");
        try{
            var candidate=Candidate(original,log);
            ParkingLotCinematicCapture.WholeMapViews("sky-ambient-baseline-v35");
            RenderSettings.ambientMode=AmbientMode.Custom;RenderSettings.ambientProbe=candidate;
            ParkingLotCinematicCapture.WholeMapViews("sky-ambient-candidate");
            var actual=RenderSettings.ambientProbe;float delta=0;
            for(int c=0;c<3;c++)for(int k=0;k<9;k++)delta=Mathf.Max(delta,Mathf.Abs(actual[c,k]-candidate[c,k]));
            log.AppendLine("AppliedProbeMaxDelta="+delta);
            if(delta>.0001f)throw new InvalidOperationException("SkyManager replaced the comparison probe.");
        }finally{RenderSettings.ambientMode=mode;RenderSettings.ambientIntensity=intensity;RenderSettings.ambientProbe=original;}
        if(scene.isDirty)throw new InvalidOperationException("Scene became dirty; do not save the comparison.");
        log.AppendLine("Saved=True\nOriginalAmbientRestored=True\nGeometryChanges=0\nNewRuntimeComponents=0");
        File.WriteAllText(Output+"/sky-light-review-success.txt",log.ToString());
    }
}
}
