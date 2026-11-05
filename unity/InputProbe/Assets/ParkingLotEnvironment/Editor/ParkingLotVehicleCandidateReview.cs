using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Temporary scene-only comparison. Imports new assets; never replaces parked cars or saves the scene.
[InitializeOnLoad]
public static class ParkingLotVehicleCandidateReview {
    const string Root="Assets/ParkingLotEnvironment";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotVehicleCandidateReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string request=Output+"/candidate-review.request";if(!File.Exists(request))return;
        try{File.Delete(request);Review();}catch(Exception e){File.WriteAllText(Output+"/candidate-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved ParklingLot scene for candidate review.");
        if(!File.Exists(Root+"/RealVehicles/hyundai-ioniq5-openx/hyundai-ioniq5-openx.prefab"))ParkingLotVehicleImporter.Import(Root+"/SourceModels~/hyundai-ioniq5-openx/model.json","hyundai-ioniq5-openx",4.657335f);
        if(!File.Exists(Root+"/RealVehicles/hyundai-tucson-openx/hyundai-tucson-openx.prefab"))ParkingLotVehicleImporter.Import(Root+"/SourceModels~/hyundai-tucson-openx/model.json","hyundai-tucson-openx",4.534803f);
        ParkingLotPaintNormals.Reset();
        foreach(string key in new[]{"hyundai-ioniq5-openx","hyundai-tucson-openx","hyundai-tucson"}){
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RealVehicles/"+key+"/"+key+".prefab");if(prefab==null)throw new InvalidOperationException("Missing candidate "+key);
            var paint=new Material(AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Real_carpaint_0.mat")){hideFlags=HideFlags.HideAndDontSave};paint.color=new Color(.54f,.57f,.59f,1);
            var candidate=UnityEngine.Object.Instantiate(prefab);candidate.name="Temporary_vehicle_review";candidate.hideFlags=HideFlags.HideInHierarchy;candidate.transform.position=new Vector3(18,0,4);candidate.transform.rotation=Quaternion.Euler(0,180,0);
            try{
                foreach(var r in candidate.GetComponentsInChildren<MeshRenderer>()){
                    string name=r.sharedMaterial.name;
                    if(name.EndsWith("_Body")||name.EndsWith("_varnish")){
                        r.sharedMaterial=paint;
                        if(key=="hyundai-tucson"){var normalMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/SurfaceNormals/Tucson_paint_weighted_normals.asset");if(normalMesh!=null)r.GetComponent<MeshFilter>().sharedMesh=normalMesh;}
                    }
                }
                ParkingLotCinematicCapture.CandidateViews(key+"-aligned",candidate.transform.position);
                if(key.EndsWith("openx")){
                    var filters=new System.Collections.Generic.Dictionary<MeshFilter,Mesh>();foreach(var r in candidate.GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial==paint){var f=r.GetComponent<MeshFilter>();filters.Add(f,f.sharedMesh);}
                    for(int variant=0;variant<3;variant++){
                        string suffix=variant==0?"weighted":variant==1?"weighted35":"angle35";
                        foreach(var item in filters){item.Key.sharedMesh=item.Value;ParkingLotPaintNormals.Refine(item.Key,key+"-"+suffix,variant==0?60:35,variant==2);}
                        ParkingLotCinematicCapture.CandidateViews(key+"-"+suffix,candidate.transform.position);
                    }
                    if(key=="hyundai-ioniq5-openx"){
                        foreach(var item in filters)item.Key.sharedMesh=item.Value;
                        ParkingLotIoniqFinish.Apply(candidate);
                        var font=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/ParkingSigns.ttf");
                        if(font==null)throw new InvalidOperationException("ParkingSigns font not found.");
                        ParkingLotVehicleDetails.Add(candidate,key,30,font);
                        var depth=UnityEngine.Object.FindObjectOfType<ParkingLotTextDepth>();if(depth==null||depth.textMaterial==null)throw new InvalidOperationException("Map depth-tested text material missing.");
                        foreach(var t in candidate.GetComponentsInChildren<TextMesh>())t.GetComponent<Renderer>().sharedMaterial=depth.textMaterial;
                        ParkingLotCinematicCapture.CandidateViews(key+"-finished",candidate.transform.position);
                    }
                }
            }finally{UnityEngine.Object.DestroyImmediate(candidate);UnityEngine.Object.DestroyImmediate(paint);}
        }
        File.WriteAllText(Output+"/candidate-review-success.txt",DateTime.Now.ToString("s")+"\nOriginal saved scene unchanged; no parked vehicles replaced.\nSceneSaved="+!scene.isDirty);
    }
}
}
