using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotLightingReview {
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotLightingReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string request=Output+"/lighting-review.request";if(!File.Exists(request))return;
        try{File.Delete(request);}catch(IOException){return;}
        try{Review();}catch(Exception e){File.WriteAllText(Output+"/lighting-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved parking map first.");
        var sky=RenderSettings.ambientSkyColor;var equator=RenderSettings.ambientEquatorColor;var ground=RenderSettings.ambientGroundColor;
        try{
            ParkingLotCinematicCapture.LightingViews("baseline-v18");
            RenderSettings.ambientSkyColor=sky*.85f;RenderSettings.ambientEquatorColor=equator*.85f;RenderSettings.ambientGroundColor=ground*.85f;
            ParkingLotCinematicCapture.LightingViews("ambient85");
            RenderSettings.ambientSkyColor=sky*.7f;RenderSettings.ambientEquatorColor=equator*.7f;RenderSettings.ambientGroundColor=ground*.7f;
            ParkingLotCinematicCapture.LightingViews("ambient70");
        }finally{RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;}
        File.WriteAllText(Output+"/lighting-review-success.txt",DateTime.Now.ToString("s")+"\nThree ambient levels; same sun, exposure, shadows and baked reflection probes. Original ambient restored. Saved="+!scene.isDirty);
    }
}
}
