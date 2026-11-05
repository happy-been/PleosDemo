using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Applies the visually reviewed footpath/curb surfaces to map scene instances only; meshes and colliders stay untouched.
public static class ParkingLotFootpathFinish {
    const string Root="Assets/ParkingLotEnvironment";
    const string MaterialDir=Root+"/Materials/RealismDetails";
    public static void Build(Scene scene){
        if(!scene.IsValid()||!scene.isLoaded||scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Footpath finish belongs only to ParklingLot.");
        var shader=Shader.Find("ParkingLot/ArchitecturalSurface");
        var pavement=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/KoreanAssets/concrete_pavement_diff_2k.jpg");
        var concrete=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/KoreanAssets/concrete_wall_009_diff_2k.jpg");
        if(shader==null||pavement==null||concrete==null)throw new InvalidOperationException("Missing existing surface sources.");
        var paths=new List<MeshRenderer>();var curbs=new List<MeshRenderer>();
        foreach(var obj in scene.GetRootGameObjects())if(obj.name=="01_Ground_and_drainage")foreach(var r in obj.GetComponentsInChildren<MeshRenderer>(true)){
            if(r.name=="Perimeter_footpath")paths.Add(r);
            else if(r.name=="Individual_curb"||r.name=="End_curb")curbs.Add(r);
        }
        if(paths.Count!=2||curbs.Count!=187)throw new InvalidOperationException("Unexpected footpath/curb layout: paths="+paths.Count+" curbs="+curbs.Count);
        var targets=new List<MeshRenderer>(paths);targets.AddRange(curbs);
        var meshes=new Dictionary<MeshRenderer,Mesh>();var colliders=new Dictionary<MeshRenderer,int>();var positions=new Dictionary<MeshRenderer,Matrix4x4>();
        foreach(var r in targets){
            var filter=r.GetComponent<MeshFilter>();
            if(filter==null||filter.sharedMesh==null)throw new InvalidOperationException("Footpath target without mesh: "+r.name);
            meshes[r]=filter.sharedMesh;colliders[r]=r.GetComponents<Collider>().Length;positions[r]=r.transform.localToWorldMatrix;
        }

        Directory.CreateDirectory(Path.Combine(Application.dataPath,"ParkingLotEnvironment/Materials/RealismDetails"));
        AssetDatabase.Refresh();
        var pathMat=LoadOrCreate("Perimeter_path_pavers",shader);
        var curbMat=LoadOrCreate("Perimeter_curb_concrete",shader);
        pathMat.shader=shader;pathMat.mainTexture=pavement;pathMat.color=new Color(.76f,.77f,.75f);
        curbMat.shader=shader;curbMat.mainTexture=concrete;curbMat.color=new Color(.50f,.50f,.48f);
        pathMat.SetFloat("_WorldScale",1/1.8f);pathMat.SetFloat("_TextureAsColor",1);
        curbMat.SetFloat("_WorldScale",1/18.05f);curbMat.SetFloat("_TextureAsColor",0);curbMat.SetFloat("_DetailStrength",.7f);
        foreach(var mat in new[]{pathMat,curbMat}){mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");mat.EnableKeyword("_GLOSSYREFLECTIONS_OFF");mat.enableInstancing=true;EditorUtility.SetDirty(mat);}
        AssetDatabase.SaveAssetIfDirty(pathMat);AssetDatabase.SaveAssetIfDirty(curbMat);

        foreach(var r in paths)r.sharedMaterial=pathMat;
        foreach(var r in curbs)r.sharedMaterial=curbMat;

        foreach(var r in targets){
            if(r.GetComponent<MeshFilter>().sharedMesh!=meshes[r]||r.GetComponents<Collider>().Length!=colliders[r]||r.transform.localToWorldMatrix!=positions[r])
                throw new InvalidOperationException("Footpath finish changed geometry unexpectedly on "+r.name);
        }
        File.WriteAllText(ParkingLotEditorPaths.Output+"/footpath-finish-audit.txt",DateTime.Now.ToString("s")+"\nPaths="+paths.Count+"\nCurbs="+curbs.Count+"\nMaterials=2\nAddedTriangles=0\nGeometryAndCollidersUnchanged=True");
    }
    static Material LoadOrCreate(string name,Shader shader){
        string path=MaterialDir+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m!=null)return m;
        m=new Material(shader);
        AssetDatabase.CreateAsset(m,path);
        return m;
    }
}
}
