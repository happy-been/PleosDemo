using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Scene-only material assignment. Original paving, mesh data and colliders are retained.
[InitializeOnLoad]
public static class ParkingLotNeighborhoodPavingFinish {
    const string Root="Assets/ParkingLotEnvironment";
    const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Original=Root+"/Materials/KoreanContext/Korean_courtyard_paving.mat";
    const string Finished=Root+"/Materials/RealismDetails/Neighborhood_pavers.mat";
    const string OriginalEdges=Root+"/Materials/KoreanContext/Korean_neighborhood_granite.mat";
    const string FinishedEdges=Root+"/Materials/RealismDetails/Neighborhood_curb_concrete.mat";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotNeighborhoodPavingFinish(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string request=Output+"/neighborhood-paving.request";
        if(!File.Exists(request))return;
        string mode;try{mode=File.ReadAllText(request).Trim();File.Delete(request);}catch(IOException){return;}
        try{
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open the saved map before paving work.");
            if(mode=="review")Review(scene);
            else if(mode=="apply"){
                Build(scene);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save map paving.");
            }else if(mode=="validate"){
                if(Targets(scene).Any(r=>AssetDatabase.GetAssetPath(r.sharedMaterial)!=Finished)||Edges(scene).Any(r=>AssetDatabase.GetAssetPath(r.sharedMaterial)!=FinishedEdges))throw new InvalidOperationException("Not all neighborhood surfaces use their saved finish.");
            }else throw new InvalidOperationException("Unknown paving request.");
            File.WriteAllText(Output+"/neighborhood-paving-"+mode+"-success.txt",Describe(scene));
        }catch(Exception e){File.WriteAllText(Output+"/neighborhood-paving-error.txt",e.ToString());Debug.LogException(e);}
    }
    static List<MeshRenderer> Targets(Scene scene){
        if(scene.path!=Target||!scene.isLoaded)throw new InvalidOperationException("Paving finish belongs only to ParklingLot.");
        var parent=scene.GetRootGameObjects().Single(g=>g.name=="10_Connected_Korean_neighborhood");
        var found=parent.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterials.Length==1&&(AssetDatabase.GetAssetPath(r.sharedMaterial)==Original||AssetDatabase.GetAssetPath(r.sharedMaterial)==Finished)).ToList();
        if(found.Count!=53)throw new InvalidOperationException("Unexpected neighborhood paving layout: "+found.Count);
        foreach(var r in found){
            var mesh=r.GetComponent<MeshFilter>();
            if(mesh==null||mesh.sharedMesh==null||mesh.sharedMesh.vertexCount!=4||mesh.sharedMesh.triangles.Length!=6||r.GetComponents<Collider>().Length!=0)
                throw new InvalidOperationException("Unexpected paving geometry on "+r.name);
        }
        return found;
    }
    static List<MeshRenderer> Edges(Scene scene){
        var parent=scene.GetRootGameObjects().Single(g=>g.name=="10_Connected_Korean_neighborhood");
        var found=parent.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterials.Length==1&&(AssetDatabase.GetAssetPath(r.sharedMaterial)==OriginalEdges||AssetDatabase.GetAssetPath(r.sharedMaterial)==FinishedEdges)).ToList();
        if(found.Count!=69||found.Any(r=>r.name!="Service_street_kerb"&&!r.name.StartsWith("Residential_plot_boundary_walls",StringComparison.Ordinal)))throw new InvalidOperationException("Unexpected neighborhood curb/wall layout.");
        return found;
    }
    static Material CreateEdges(){
        var source=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/RealismDetails/Perimeter_curb_concrete.mat");
        if(source==null||source.shader.name!="ParkingLot/ArchitecturalSurface")throw new InvalidOperationException("Missing reviewed curb surface.");
        return new Material(source){name="Neighborhood_curb_concrete",hideFlags=HideFlags.HideAndDontSave};
    }
    static Material Create(){
        var shader=Shader.Find("ParkingLot/ArchitecturalSurface");
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/KoreanAssets/concrete_pavement_diff_2k.jpg");
        if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader)||texture==null)throw new InvalidOperationException("Missing reviewed paving surface.");
        var m=new Material(shader){name="Neighborhood_pavers",hideFlags=HideFlags.HideAndDontSave};
        m.mainTexture=texture;m.color=new Color(.76f,.77f,.75f);
        m.SetFloat("_WorldScale",1/1.8f);m.SetFloat("_TextureAsColor",1);
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");m.enableInstancing=true;
        return m;
    }
    static void Review(Scene scene){
        var targets=Targets(scene);var edges=Edges(scene);var previous=targets.Concat(edges).ToDictionary(r=>r,r=>r.sharedMaterial);Material candidate=null,edgeCandidate=null;
        var source=AssetDatabase.LoadAssetAtPath<Material>(Original);if(source==null)throw new InvalidOperationException("Missing original paving.");
        var edgeSource=AssetDatabase.LoadAssetAtPath<Material>(OriginalEdges);if(edgeSource==null)throw new InvalidOperationException("Missing original curb.");
        try{
            foreach(var r in targets)r.sharedMaterial=source;
            foreach(var r in edges)r.sharedMaterial=edgeSource;
            ParkingLotCinematicCapture.NeighborhoodPavingViews("original");
            candidate=Create();foreach(var r in targets)r.sharedMaterial=candidate;
            edgeCandidate=CreateEdges();foreach(var r in edges)r.sharedMaterial=edgeCandidate;
            ParkingLotCinematicCapture.NeighborhoodPavingViews("candidate");
        }finally{foreach(var pair in previous)pair.Key.sharedMaterial=pair.Value;if(candidate!=null)UnityEngine.Object.DestroyImmediate(candidate);if(edgeCandidate!=null)UnityEngine.Object.DestroyImmediate(edgeCandidate);}
    }
    public static void Build(Scene scene){
        var targets=Targets(scene);var edges=Edges(scene);var all=targets.Concat(edges).ToList();
        var originalMeshes=all.ToDictionary(r=>r,r=>r.GetComponent<MeshFilter>().sharedMesh);
        var originalTransforms=all.ToDictionary(r=>r,r=>r.transform.localToWorldMatrix);
        var originalColliders=all.ToDictionary(r=>r,r=>r.GetComponents<Collider>());
        var material=AssetDatabase.LoadAssetAtPath<Material>(Finished);
        if(material==null){material=Create();material.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(material,Finished);AssetDatabase.SaveAssetIfDirty(material);}
        else if(material.shader.name!="ParkingLot/ArchitecturalSurface")throw new InvalidOperationException("Existing neighborhood finish was edited.");
        var edgeMaterial=AssetDatabase.LoadAssetAtPath<Material>(FinishedEdges);
        if(edgeMaterial==null){edgeMaterial=CreateEdges();edgeMaterial.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(edgeMaterial,FinishedEdges);AssetDatabase.SaveAssetIfDirty(edgeMaterial);}
        else if(edgeMaterial.shader.name!="ParkingLot/ArchitecturalSurface")throw new InvalidOperationException("Existing neighborhood curb finish was edited.");
        foreach(var r in targets)r.sharedMaterial=material;
        foreach(var r in edges)r.sharedMaterial=edgeMaterial;
        foreach(var r in all)if(r.GetComponent<MeshFilter>().sharedMesh!=originalMeshes[r]||r.transform.localToWorldMatrix!=originalTransforms[r]||!r.GetComponents<Collider>().SequenceEqual(originalColliders[r]))throw new InvalidOperationException("Paving geometry changed unexpectedly.");
        File.WriteAllText(Output+"/neighborhood-paving-audit.txt",Describe(scene)+"\nGeometryAndCollidersUnchanged=True\nAddedTriangles=0\nAddedRenderers=0");
    }
    static string Describe(Scene scene){
        var targets=Targets(scene);
        var edges=Edges(scene);
        return DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nTargets="+targets.Count+"\nFinished="+targets.Count(r=>AssetDatabase.GetAssetPath(r.sharedMaterial)==Finished)+"\nOriginal="+targets.Count(r=>AssetDatabase.GetAssetPath(r.sharedMaterial)==Original)+"\nCurbsAndWalls="+edges.Count+"\nFinishedCurbsAndWalls="+edges.Count(r=>AssetDatabase.GetAssetPath(r.sharedMaterial)==FinishedEdges)+"\nNames="+string.Join(",",targets.Select(r=>r.name));
    }
}
}
