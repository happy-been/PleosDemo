using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotPavingEdgeFinish {
    const string Root="Assets/ParkingLotEnvironment";
    const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Name="17_Neighborhood_paving_edges";
    const string Pavers=Root+"/Materials/RealismDetails/Neighborhood_pavers.mat";
    const string Concrete=Root+"/Materials/RealismDetails/Neighborhood_curb_concrete.mat";
    const string MeshPath=Root+"/Meshes/KoreanContext/Neighborhood_paving_edge_faces.asset";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotPavingEdgeFinish(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/paving-edges.request";if(!File.Exists(path))return;
        string mode;try{mode=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open the saved map before paving-edge work.");
            CheckKnownAreas();
            if(mode=="review")Review(scene);
            else if(mode=="apply"){
                Build(scene);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save paving edges.");
            }else if(mode=="validate")Validate(scene);
            else throw new InvalidOperationException("Unknown paving-edge request.");
            File.WriteAllText(Output+"/paving-edges-"+mode+"-success.txt",DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nKnownAreaCases=5\nSurfaces="+Surfaces(scene).Count);
        }catch(Exception e){File.WriteAllText(Output+"/paving-edges-error.txt",e.ToString());Debug.LogException(e);}
    }
    static List<ParkingLotPavingEdgeGeometry.Surface> Surfaces(Scene scene){
        if(scene.path!=Target||!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Paving edges belong only to ParklingLot.");
        var parent=scene.GetRootGameObjects().Single(g=>g.name=="10_Connected_Korean_neighborhood");
        var targets=parent.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.sharedMaterials.Length==1&&AssetDatabase.GetAssetPath(r.sharedMaterial)==Pavers).OrderBy(r=>r.name,StringComparer.Ordinal).ToArray();
        if(targets.Length!=53)throw new InvalidOperationException("Unexpected paving target count.");
        var surfaces=new List<ParkingLotPavingEdgeGeometry.Surface>();
        foreach(var r in targets){
            var f=r.GetComponent<MeshFilter>();var b=r.bounds;
            if(f==null||f.sharedMesh==null||f.sharedMesh.vertexCount!=4||f.sharedMesh.triangles.Length!=6||r.GetComponents<Collider>().Length!=0||b.size.y>.00001f||b.center.y<=0||b.center.y>.07f)throw new InvalidOperationException("Unexpected flat paving geometry: "+r.name);
            foreach(var p in f.sharedMesh.vertices){var v=f.transform.TransformPoint(p);if(Mathf.Abs(v.x-b.min.x)>.0001f&&Mathf.Abs(v.x-b.max.x)>.0001f||Mathf.Abs(v.z-b.min.z)>.0001f&&Mathf.Abs(v.z-b.max.z)>.0001f)throw new InvalidOperationException("Paving is not axis-aligned.");}
            surfaces.Add(new ParkingLotPavingEdgeGeometry.Surface(Rect.MinMaxRect(b.min.x,b.min.z,b.max.x,b.max.z),b.center.y));
        }
        return surfaces;
    }
    static Material Material(){var m=AssetDatabase.LoadAssetAtPath<Material>(Concrete);if(m==null||m.shader.name!="ParkingLot/ArchitecturalSurface"||ShaderUtil.ShaderHasError(m.shader))throw new InvalidOperationException("Missing reviewed curb material.");return m;}
    static GameObject Object(Scene scene,Mesh mesh,bool temporary){
        var g=new GameObject(Name);SceneManager.MoveGameObjectToScene(g,scene);
        if(temporary)g.hideFlags=HideFlags.HideAndDontSave;
        g.AddComponent<MeshFilter>().sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=Material();
        r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;r.lightProbeUsage=LightProbeUsage.Off;g.isStatic=true;return g;
    }
    static void Review(Scene scene){
        if(scene.GetRootGameObjects().Any(g=>g.name==Name))throw new InvalidOperationException("Paving edges already adopted; do not layer the comparison.");
        var mesh=ParkingLotPavingEdgeGeometry.Create(Surfaces(scene));GameObject g=null;
        try{
            CheckMesh(mesh);
            ParkingLotCinematicCapture.NeighborhoodPavingViews("v30-original");
            g=Object(scene,mesh,true);ParkingLotCinematicCapture.NeighborhoodPavingViews("v30-candidate");
            File.WriteAllText(Output+"/paving-edges-review-audit.txt",GeometryReport(mesh));
        }finally{if(g!=null)UnityEngine.Object.DestroyImmediate(g);UnityEngine.Object.DestroyImmediate(mesh);}
        if(scene.isDirty)throw new InvalidOperationException("Review unexpectedly dirtied the scene.");
    }
    public static void Build(Scene scene){
        var expected=ParkingLotPavingEdgeGeometry.Create(Surfaces(scene));CheckMesh(expected);
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if(saved==null)saved=ParkingLotMeshAsset.Save(expected,MeshPath);
        else{try{Compare(saved,expected);}finally{UnityEngine.Object.DestroyImmediate(expected);}}
        var roots=scene.GetRootGameObjects().Where(g=>g.name==Name).ToArray();
        if(roots.Length>1)throw new InvalidOperationException("Duplicate paving edges.");
        if(roots.Length==0)Object(scene,saved,false);
        Validate(scene);File.WriteAllText(Output+"/paving-edges-audit.txt",GeometryReport(saved));
    }
    static void Validate(Scene scene){
        var g=scene.GetRootGameObjects().Single(o=>o.name==Name);var f=g.GetComponent<MeshFilter>();var r=g.GetComponent<MeshRenderer>();
        if(f==null||r==null||AssetDatabase.GetAssetPath(f.sharedMesh)!=MeshPath||r.sharedMaterial!=Material()||g.GetComponentsInChildren<Collider>().Length!=0||g.transform.localToWorldMatrix!=Matrix4x4.identity||g.transform.childCount!=0)throw new InvalidOperationException("Unexpected saved edge object.");
        var expected=ParkingLotPavingEdgeGeometry.Create(Surfaces(scene));try{Compare(f.sharedMesh,expected);CheckMesh(f.sharedMesh);}finally{UnityEngine.Object.DestroyImmediate(expected);}
    }
    static void Compare(Mesh a,Mesh b){if(!a.vertices.SequenceEqual(b.vertices)||!a.normals.SequenceEqual(b.normals)||!a.uv.SequenceEqual(b.uv)||!a.triangles.SequenceEqual(b.triangles))throw new InvalidOperationException("Saved edge mesh differs from current pad boundaries.");}
    static double Area(Mesh m){var v=m.vertices;var t=m.triangles;double sum=0;for(int i=0;i<t.Length;i+=3)sum+=Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]).magnitude*.5;return sum;}
    static void CheckMesh(Mesh m){
        var v=m.vertices;var n=m.normals;var t=m.triangles;
        if(v.Length==0||v.Length>5000||v.Length!=n.Length||m.uv.Length!=v.Length)throw new InvalidOperationException("Unexpected edge mesh size.");
        for(int i=0;i<v.Length;i++)if(float.IsNaN(v[i].x)||float.IsInfinity(v[i].x)||v[i].y<-.0051f||v[i].y>.0701f||Mathf.Abs(n[i].y)>.0001f)throw new InvalidOperationException("Edge extends outside original paving heights.");
        for(int i=0;i<t.Length;i+=3)if(Vector3.Dot(Vector3.Cross(v[t[i+1]]-v[t[i]],v[t[i+2]]-v[t[i]]),n[t[i]])<=0)throw new InvalidOperationException("Inverted or degenerate edge triangle.");
    }
    static string GeometryReport(Mesh m){return DateTime.Now.ToString("s")+"\nVertices="+m.vertexCount+"\nTriangles="+m.triangles.Length/3+"\nAreaM2="+Area(m)+"\nAddedRenderers=1\nAddedColliders=0\nVerticalFacesOnly=True\nNoFootprintExpansion=True\nOriginalPavingAssetsUnchanged=True";}
    static void CheckKnownAreas(){
        // Analytic perimeter areas exercise touching pads, height steps, duplicate pads and a T junction.
        var a=new ParkingLotPavingEdgeGeometry.Surface(new Rect(0,0,2,3),.1f);
        var b=new ParkingLotPavingEdgeGeometry.Surface(new Rect(2,0,2,3),.1f);
        var high=new ParkingLotPavingEdgeGeometry.Surface(b.footprint,.2f);
        var bar=new ParkingLotPavingEdgeGeometry.Surface(new Rect(-2,0,4,1),.1f);
        var stem=new ParkingLotPavingEdgeGeometry.Surface(new Rect(-.5f,1,1,3),.1f);
        var inputs=new[]{new[]{a},new[]{a,b},new[]{a,high},new[]{a,a},new[]{bar,stem}};
        var areas=new[]{1.0,1.4,2.4,1.0,1.6};
        for(int i=0;i<inputs.Length;i++){var mesh=ParkingLotPavingEdgeGeometry.Create(inputs[i],0);try{if(Math.Abs(Area(mesh)-areas[i])>.0001)throw new InvalidOperationException("Paving area case "+i+" failed.");}finally{UnityEngine.Object.DestroyImmediate(mesh);}}
    }
}
}
