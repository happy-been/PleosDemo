using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;

namespace ParkingLotEnvironment.Editor {
// Measures the actual focused Game view. Editor-only and never included in an APK.
[InitializeOnLoad]
public static class ParkingLotPerformanceCheck {
    static readonly string Output=ParkingLotEditorPaths.Output;
    const string ScenePath="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Key="ParkingLotPerformanceCheck.Active";
    static readonly List<float> milliseconds=new List<float>();
    static double started;
    static int warmup,lastFrame=-1,focusedFrames;
    static long drawCalls,batches,triangles;
    static bool finishing;
    static ParkingLotPerformanceCheck(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayState;Camera.onPostRender+=Rendered;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
        string request=Output+"/profile.request";
        if(File.Exists(request)&&!EditorApplication.isPlayingOrWillChangePlaymode){
            string mode;
            try{mode=File.ReadAllText(request).Trim();File.Delete(request);}catch(IOException){return;}
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath||scene.isDirty){File.WriteAllText(Output+"/profile-error.txt","Only a saved ParklingLot scene can be measured.");return;}
            SessionState.SetString(Key+".Mode",mode=="aerial"?"aerial":"eye");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        if(SessionState.GetBool(Key,false)&&EditorApplication.isPlaying&&started>0&&!finishing&&EditorApplication.timeSinceStartup-started>45)Finish("45-second measurement limit");
    }
    static void PlayState(PlayModeStateChange state){
        if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false)){
            milliseconds.Clear();drawCalls=batches=triangles=0;warmup=0;focusedFrames=0;lastFrame=-1;finishing=false;started=EditorApplication.timeSinceStartup;
            var camera=Camera.main;if(camera==null||camera.gameObject.scene.path!=ScenePath){Finish("Missing map camera");return;}
            camera.orthographic=false;var preview=camera.GetComponent<ParkingLotCamera>();if(preview!=null)preview.cinematicPreview=false;
            if(SessionState.GetString(Key+".Mode","eye")=="aerial"){camera.transform.position=new Vector3(31,25,-34);camera.transform.LookAt(new Vector3(0,1,2));camera.fieldOfView=52;}
        }
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);started=0;finishing=false;}
    }
    static void Rendered(Camera camera){
        if(!Application.isPlaying||!SessionState.GetBool(Key,false)||finishing||camera.name!="ParkingLot_PreviewCamera"||lastFrame==Time.frameCount)return;
        lastFrame=Time.frameCount;if(++warmup<90)return;
        milliseconds.Add(Time.unscaledDeltaTime*1000);if(Application.isFocused)focusedFrames++;drawCalls+=UnityStats.drawCalls;batches+=UnityStats.batches;triangles+=UnityStats.triangles;
        if(milliseconds.Count>=300)Finish("300 rendered Game-view frames");
    }
    static void Finish(string reason){
        if(finishing)return;finishing=true;var sorted=milliseconds.ToArray();Array.Sort(sorted);float total=0;foreach(float value in sorted)total+=value;int count=sorted.Length;float average=count>0?total/count:0;
        var text="Editor Game-view measurement only; not Android or offline video FPS.\nDate="+DateTime.Now.ToString("s")+"\nMode="+SessionState.GetString(Key+".Mode","eye")+"\nReason="+reason+"\nResolution="+UnityStats.screenRes+"\nFrames="+count+"\nMeanFrameMs="+average+"\nMeanFps="+(average>0?1000/average:0)+"\nP95FrameMs="+(count>0?sorted[Mathf.Min(count-1,Mathf.FloorToInt(count*.95f))]:0)+"\nAverageDrawCalls="+(count>0?drawCalls/count:0)+"\nAverageBatches="+(count>0?batches/count:0)+"\nAverageRenderedTriangles="+(count>0?triangles/count:0)+"\nUnityAllocatedMB="+(Profiler.GetTotalAllocatedMemoryLong()/1048576)+"\nGPU="+SystemInfo.graphicsDeviceName;
        text+="\nFocusedFrames="+focusedFrames+"\nTargetFrameRate="+Application.targetFrameRate+"\nVSyncCount="+QualitySettings.vSyncCount;
        Directory.CreateDirectory(Output);File.WriteAllText(Output+"/profile-"+SessionState.GetString(Key+".Mode","eye")+".txt",text);SessionState.SetBool(Key,false);EditorApplication.delayCall+=()=>EditorApplication.isPlaying=false;
    }
}
}
