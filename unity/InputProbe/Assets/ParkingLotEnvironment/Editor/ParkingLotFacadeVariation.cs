using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// A material-only variant on eight existing villa instances. Shared source assets stay intact.
[InitializeOnLoad]
public static class ParkingLotFacadeVariation {
    const string Root="Assets/ParkingLotEnvironment";
    const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Gray=Root+"/Materials/KoreanContext/Villa_gray_facade.mat";
    const string Cool=Root+"/Materials/KoreanContext/Villa_cool_facade.mat";
    const string Finished=Root+"/Materials/KoreanContext/Villa_ivory_facade_v29.mat";
    const string Texture=Root+"/KoreanAssets/villa_facade_ivory_v29.png";
    static readonly int[] Indices={0,5,9,13,15,17,20,23};
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotFacadeVariation(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/facade-variation.request";
        if(!File.Exists(path))return;
        string mode;try{mode=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open the saved parking map before facade work.");
            if(mode=="review")Review(scene);
            else if(mode=="apply"){
                Build(scene);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save facade variation.");
            }else if(mode=="validate")Validate(scene);
            else throw new InvalidOperationException("Unknown facade request.");
            File.WriteAllText(Output+"/facade-variation-"+mode+"-success.txt",Describe(scene));
        }catch(Exception e){File.WriteAllText(Output+"/facade-variation-error.txt",e.ToString());Debug.LogException(e);}
    }
    static MeshRenderer[] Villas(Scene scene){
        if(scene.path!=Target||!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Facade variation is restricted to ParklingLot.");
        var parent=scene.GetRootGameObjects().Single(g=>g.name=="12_Korean_residential_infill");
        var villas=parent.GetComponentsInChildren<MeshRenderer>(true).OrderBy(r=>r.name,StringComparer.Ordinal).ToArray();
        if(villas.Length!=25)throw new InvalidOperationException("Unexpected villa count.");
        for(int i=0;i<villas.Length;i++){
            var r=villas[i];var b=ParkingLotNeighborhoodInfill.Buildings[i];var f=r.GetComponent<MeshFilter>();
            bool skyline=f!=null&&f.sharedMesh!=null&&ParkingLotSkylineVariation.IsSavedVariant(i,f.sharedMesh);
            if(r.name!="Residential_villa_"+(i+1).ToString("D2")||f==null||f.sharedMesh==null||(!skyline&&AssetDatabase.GetAssetPath(f.sharedMesh)!=Root+"/Meshes/KoreanContext/Korean_villa_type_"+b.finish+".asset"))throw new InvalidOperationException("Unexpected villa mesh: "+r.name);
            if((r.transform.localPosition!=b.position&&r.transform.localPosition!=ParkingLotSkylineVariation.Position(i))||r.transform.localScale!=b.scale||Quaternion.Angle(r.transform.localRotation,Quaternion.Euler(0,b.yaw,0))>.001f)throw new InvalidOperationException("Villa layout was manually edited: "+r.name);
            var box=r.GetComponent<BoxCollider>();
            float height=skyline?ParkingLotSkylineVariation.FloorCount(i)*3.2f:12.8f;
            if(box==null||r.GetComponents<Collider>().Length!=1||Vector3.Distance(box.center,new Vector3(0,height/2,0))>.001f||Vector3.Distance(box.size,new Vector3(13,height,11))>.001f)throw new InvalidOperationException("Unexpected villa collider.");
            if(Indices.Contains(i))Slot(r);
            else if(r.sharedMaterials.Any(m=>AssetDatabase.GetAssetPath(m)==Finished))throw new InvalidOperationException("Ivory finish occurs outside selected villas.");
        }
        return villas;
    }
    static int Slot(MeshRenderer r){
        var slots=r.sharedMaterials.Select((m,i)=>new{path=AssetDatabase.GetAssetPath(m),index=i}).Where(p=>p.path==Gray||p.path==Cool||p.path==Finished).ToArray();
        if(slots.Length!=1)throw new InvalidOperationException("Unexpected facade slots: "+r.name);
        return slots[0].index;
    }
    static Material Create(){
        var source=AssetDatabase.LoadAssetAtPath<Material>(Gray);
        var texture=ParkingLotKoreanContext.LoadFacade(Texture);
        if(source==null||source.shader.name!="Standard"||texture==null)throw new InvalidOperationException("Missing source facade material/texture.");
        var m=new Material(source){name="Villa_ivory_facade_v29",hideFlags=HideFlags.HideAndDontSave};
        m.mainTexture=texture;m.color=new Color(.80f,.80f,.78f);m.enableInstancing=true;
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
        return m;
    }
    static void Assign(MeshRenderer[] villas,Material m){
        foreach(int i in Indices){var r=villas[i];var materials=r.sharedMaterials;materials[Slot(r)]=m;r.sharedMaterials=materials;}
    }
    static void Review(Scene scene){
        var villas=Villas(scene);var previous=villas.ToDictionary(r=>r,r=>r.sharedMaterials);Material candidate=null;
        try{
            foreach(int i in Indices){
                var r=villas[i];var materials=r.sharedMaterials;
                materials[Slot(r)]=AssetDatabase.LoadAssetAtPath<Material>(ParkingLotNeighborhoodInfill.Buildings[i].finish==0?Gray:Cool);
                r.sharedMaterials=materials;
            }
            ParkingLotCinematicCapture.UtilityViews("villa-original");
            candidate=Create();Assign(villas,candidate);
            ParkingLotCinematicCapture.UtilityViews("villa-candidate");
        }finally{
            foreach(var p in previous)p.Key.sharedMaterials=p.Value;
            if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate);
        }
        if(scene.isDirty)throw new InvalidOperationException("Review unexpectedly dirtied the scene; inspect before saving.");
    }
    public static void Build(Scene scene){
        var villas=Villas(scene);var previous=villas.ToDictionary(r=>r,r=>r.sharedMaterials);
        var meshes=villas.ToDictionary(r=>r,r=>r.GetComponent<MeshFilter>().sharedMesh);
        var transforms=villas.ToDictionary(r=>r,r=>r.transform.localToWorldMatrix);
        var colliders=villas.ToDictionary(r=>r,r=>r.GetComponents<Collider>());
        var material=AssetDatabase.LoadAssetAtPath<Material>(Finished);
        if(material==null){material=Create();material.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(material,Finished);AssetDatabase.SaveAssetIfDirty(material);}
        CheckMaterial(material);Assign(villas,material);
        for(int i=0;i<villas.Length;i++){
            var r=villas[i];var after=r.sharedMaterials;int selected=Indices.Contains(i)?Slot(r):-1;
            if(after.Length!=previous[r].Length||after.Where((m,k)=>k!=selected&&m!=previous[r][k]).Any())throw new InvalidOperationException("A non-facade material changed.");
            if(r.GetComponent<MeshFilter>().sharedMesh!=meshes[r]||r.transform.localToWorldMatrix!=transforms[r]||!r.GetComponents<Collider>().SequenceEqual(colliders[r]))throw new InvalidOperationException("Villa geometry changed.");
        }
        Validate(scene);
        File.WriteAllText(Output+"/facade-variation-audit.txt",Describe(scene)+"\nOtherMaterialSlotsUnchanged=True\nGeometryTransformsCollidersUnchanged=True\nAddedTriangles=0\nAddedRenderers=0");
    }
    static void CheckMaterial(Material m){
        if(m==null||m.shader.name!="Standard"||ShaderUtil.ShaderHasError(m.shader)||AssetDatabase.GetAssetPath(m.mainTexture)!=Texture||!m.IsKeywordEnabled("_SPECULARHIGHLIGHTS_OFF")||!m.IsKeywordEnabled("_GLOSSYREFLECTIONS_OFF"))throw new InvalidOperationException("Invalid saved facade material.");
    }
    static void Validate(Scene scene){
        var villas=Villas(scene);var m=AssetDatabase.LoadAssetAtPath<Material>(Finished);CheckMaterial(m);
        if(Indices.Any(i=>villas[i].sharedMaterials[Slot(villas[i])]!=m))throw new InvalidOperationException("Selected facade not saved.");
    }
    static string Describe(Scene scene){
        var villas=Villas(scene);
        return DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nVillas="+villas.Length+"\nSelected="+Indices.Length+"\nFinished="+Indices.Count(i=>AssetDatabase.GetAssetPath(villas[i].sharedMaterials[Slot(villas[i])])==Finished)+"\nNames="+string.Join(",",Indices.Select(i=>villas[i].name));
    }
}
}
