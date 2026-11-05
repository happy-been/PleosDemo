using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Fictional streetscape geometry, not an electrical installation specification.
[InitializeOnLoad]
public static class ParkingLotUtilityNetwork {
    const string Root="Assets/ParkingLotEnvironment";
    const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Name="16_Neighborhood_utility_extension";
    static readonly string Output=ParkingLotEditorPaths.Output;
    struct Pole {public Vector3 p;public float h;public bool cross;public Pole(float x,float z,float height,bool crossing=false){p=new Vector3(x,.065f,z);h=height;cross=crossing;}}
    static readonly Pole[] Poles={new Pole(-36.6f,0,10.25f),new Pole(-36.6f,39.8f,10.45f,true),new Pole(36.6f,0,10.35f),new Pole(36.6f,39.8f,10.15f,true),new Pole(-4,39.8f,10.3f,true)};
    static readonly List<Vector3[]> cablePaths=new List<Vector3[]>();
    static readonly List<float> cableRadii=new List<float>();
    static Material concrete,metal,ceramic,box,cable;
    static ParkingLotUtilityNetwork(){EditorApplication.update+=Tick;}
    static bool Take(string name){string p=Output+"/"+name;if(!File.Exists(p))return false;try{File.Delete(p);return true;}catch(IOException){return false;}}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        if(Take("utility-network-validate.request")){try{File.WriteAllText(Output+"/utility-network-validation.txt",Validate(SceneManager.GetActiveScene()));}catch(Exception e){File.WriteAllText(Output+"/utility-network-validation-error.txt",e.ToString());Debug.LogException(e);}return;}
        if(Take("utility-wire-aa-review.request")){try{ReviewWires();}catch(Exception e){File.WriteAllText(Output+"/utility-wire-aa-error.txt",e.ToString());Debug.LogException(e);}return;}
        if(Take("utility-wire-aa-apply.request")){try{var saved=SceneManager.GetActiveScene();if(saved.path!=Target||saved.isDirty)throw new InvalidOperationException("Open the saved map first.");var root=saved.GetRootGameObjects().Single(g=>g.name==Name);CreatePaths();Audit(saved);var filter=root.GetComponentsInChildren<MeshFilter>().Single(f=>f.name=="Utility_extension_wires");SetWireMesh(filter);EditorSceneManager.MarkSceneDirty(saved);if(!EditorSceneManager.SaveScene(saved,Target))throw new IOException("Could not save wire refinement.");File.WriteAllText(Output+"/utility-wire-aa-apply-success.txt",DateTime.Now.ToString("s")+"\nWireTriangles="+filter.sharedMesh.triangles.Length/3+"\nSaved="+!saved.isDirty);}catch(Exception e){File.WriteAllText(Output+"/utility-wire-aa-apply-error.txt",e.ToString());Debug.LogException(e);}return;}
        bool review=Take("utility-network-review.request"),apply=!review&&Take("utility-network-apply.request");if(!review&&!apply)return;
        GameObject created=null;var scene=SceneManager.GetActiveScene();
        try{
            if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open the saved map before utility review.");
            if(review){
                if(scene.GetRootGameObjects().Any(g=>g.name==Name))throw new InvalidOperationException("Review must start before adopting the network.");
                ParkingLotCinematicCapture.UtilityViews("baseline");
                created=Build(scene);created.hideFlags=HideFlags.HideAndDontSave;
                ParkingLotCinematicCapture.UtilityViews("candidate");
            }else{Build(scene);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save utility map.");}
            File.WriteAllText(Output+"/utility-network-"+(review?"review":"apply")+"-success.txt",DateTime.Now.ToString("s"));
        }catch(Exception e){File.WriteAllText(Output+"/utility-network-error.txt",e.ToString());Debug.LogException(e);}
        finally{if(review&&created!=null)UnityEngine.Object.DestroyImmediate(created);if(review)File.WriteAllText(Output+"/utility-network-review-state.txt","Saved="+!scene.isDirty);}
    }
    public static GameObject Build(Scene scene){
        if(scene.path!=Target||!scene.isLoaded)throw new InvalidOperationException("Utility extension belongs only to ParklingLot.");
        cablePaths.Clear();cableRadii.Clear();var poles=new ParkingLotKoreanContext.Geo();
        concrete=ParkingLotKoreanContext.Mat("Utility_extension_concrete",new Color(.56f,.55f,.51f),.04f,0,null,true);ParkingLotArchitecturalSurfaces.Concrete(concrete);
        metal=ParkingLotKoreanContext.Mat("Utility_extension_zinc",new Color(.38f,.40f,.40f),.32f,.45f);
        ceramic=ParkingLotKoreanContext.Mat("Utility_extension_ceramic",new Color(.31f,.27f,.23f),.45f);
        box=ParkingLotKoreanContext.Mat("Utility_extension_equipment",new Color(.44f,.49f,.46f),.25f,.15f);
        cable=ParkingLotKoreanContext.Mat("Utility_extension_cable",new Color(.105f,.11f,.115f),.12f);
        for(int i=0;i<Poles.Length;i++){
            var n=Poles[i];poles.Cone(concrete,n.p,.175f,.105f,n.h,14);
            if(i!=4)Arm(poles,n,Vector3.right,n.h-.6f);
            if(n.cross)Arm(poles,n,Vector3.forward,n.h-.8f);
            foreach(float y in new[]{6.4f,6.95f})poles.Cone(metal,n.p+Vector3.up*y,.137f,.137f,.045f,12);
            poles.Box(box,n.p+new Vector3(.16f,6.7f,0),new Vector3(.23f,.37f,.18f));
            // A small coiled service cable and a conduit add recognizable scale without new textures.
            var loop=new List<Vector3>();for(int k=0;k<=20;k++){float a=k*Mathf.PI*2/20;loop.Add(n.p+new Vector3(.20f+Mathf.Cos(a)*.20f,6.25f+Mathf.Sin(a)*.20f,.16f));}poles.Tube(cable,loop,.012f,5);
            poles.Tube(cable,new[]{n.p+new Vector3(.15f,1.8f,.08f),n.p+new Vector3(.15f,6.7f,.08f)},.018f,6);
            if(i==0||i==3){
                float side=i==0?1:-1;poles.Box(metal,n.p+new Vector3(side*.26f,7.3f,0),new Vector3(.65f,.08f,.40f));
                poles.Cone(box,n.p+new Vector3(side*.43f,7.35f,0),.23f,.23f,.8f,14);poles.Cone(metal,n.p+new Vector3(side*.43f,8.15f,0),.25f,.22f,.055f,14);
                foreach(int s in new[]{-1,1}){
                    var terminal=n.p+new Vector3(side*.43f,8.2f,s*.12f);Insulator(poles,terminal,.14f);
                    var start=n.p+new Vector3(s*.55f,n.h-.4f,0);var end=terminal+Vector3.up*.14f;var bend=n.p+new Vector3(side*.63f,8.9f,s*.24f);var lead=new List<Vector3>();
                    for(int k=0;k<=10;k++){float t=k/10f;lead.Add((1-t)*(1-t)*start+2*(1-t)*t*bend+t*t*end);}poles.Tube(cable,lead,.013f,5);
                }
                poles.Tube(cable,new[]{n.p+new Vector3(side*.43f,7.36f,.12f),n.p+new Vector3(side*.3f,6.95f,.12f),n.p+new Vector3(.16f,6.7f,.10f)},.016f,5);
            }
        }
        // Two front nodes are the map's existing poles. Their crossarms run along Z.
        CreatePaths();
        Audit(scene); // No scene object is replaced until all positions and wire paths pass.
        foreach(var old in scene.GetRootGameObjects().Where(g=>g.name==Name).ToArray())UnityEngine.Object.DestroyImmediate(old);
        var parent=new GameObject(Name);SceneManager.MoveGameObjectToScene(parent,scene);
        try{
            ParkingLotKoreanContext.Emit(poles,"Utility_extension_poles",parent.transform,Vector3.zero,Quaternion.identity,true);
            var wires=new GameObject("Utility_extension_wires");wires.transform.SetParent(parent.transform,false);var filter=wires.AddComponent<MeshFilter>();var renderer=wires.AddComponent<MeshRenderer>();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;SetWireMesh(filter);
            for(int i=0;i<Poles.Length;i++){var go=new GameObject("Utility_pole_collision_"+i);go.transform.SetParent(parent.transform,false);go.transform.position=Poles[i].p;var c=go.AddComponent<CapsuleCollider>();c.radius=.18f;c.height=Poles[i].h;c.center=Vector3.up*(Poles[i].h/2);}
            AssetDatabase.SaveAssets();File.AppendAllText(Output+"/utility-network-audit.txt","\nMeshTriangles="+parent.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3)+"\nRenderers="+parent.GetComponentsInChildren<Renderer>().Length+"\nPoleColliders="+parent.GetComponentsInChildren<Collider>().Length);return parent;
        }catch{UnityEngine.Object.DestroyImmediate(parent);throw;}
    }
    static void Arm(ParkingLotKoreanContext.Geo geo,Pole n,Vector3 axis,float y){
        geo.Box(metal,n.p+Vector3.up*y,axis==Vector3.right?new Vector3(1.3f,.08f,.08f):new Vector3(.08f,.08f,1.3f));
        foreach(int s in new[]{-1,1})Insulator(geo,n.p+Vector3.up*(y+.04f)+axis*(s*.55f),.16f);
    }
    static void Insulator(ParkingLotKoreanContext.Geo geo,Vector3 p,float h){
        geo.Cone(ceramic,p,.035f,.03f,h,8);for(int k=0;k<3;k++)geo.Cone(ceramic,p+Vector3.up*(h*(k+.4f)/3),.06f,.045f,.025f,8);
    }
    static void Front(ParkingLotKoreanContext.Geo geo,Vector3 source,Pole end){
        foreach(int s in new[]{-1,1})Wire(geo,new Vector3(source.x,9.35f,source.z+s*.75f),end.p+Vector3.up*(end.h-.4f)+Vector3.right*(s*.55f),.72f,.013f);
        Wire(geo,new Vector3(source.x,7.1f,source.z),end.p+Vector3.up*6.95f,.52f,.021f);
    }
    static void Span(ParkingLotKoreanContext.Geo geo,Pole a,Pole b,Vector3 offset,bool rear){
        foreach(int s in new[]{-1,1})Wire(geo,a.p+Vector3.up*(a.h-(rear?.6f:.4f))+offset*(s*.55f),b.p+Vector3.up*(b.h-(rear?.6f:.4f))+offset*(s*.55f),.78f,.013f);
        Wire(geo,a.p+Vector3.up*6.95f,b.p+Vector3.up*6.95f,.55f,.021f);
    }
    static void Wire(ParkingLotKoreanContext.Geo geo,Vector3 a,Vector3 b,float sag,float radius){
        var points=new Vector3[33];for(int i=0;i<points.Length;i++){float t=i/(float)(points.Length-1);points[i]=Vector3.Lerp(a,b,t)-Vector3.up*(4*sag*t*(1-t));}
        cablePaths.Add(points);cableRadii.Add(radius);if(geo!=null)geo.Tube(cable,points,radius,5);
    }
    static void ReviewWires(){
        var scene=SceneManager.GetActiveScene();if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Save the map before wire comparison.");
        var root=scene.GetRootGameObjects().Single(g=>g.name==Name);var f=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Utility_extension_wires");var r=f.GetComponent<MeshRenderer>();
        var previousMesh=f.sharedMesh;var previousMaterials=r.sharedMaterials;Mesh mesh=null;Material material=null;
        try{
            f.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/KoreanContext/Utility_extension_wires.asset");r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/KoreanContext/Utility_extension_cable.mat");
            if(f.sharedMesh==null||r.sharedMaterial==null)throw new InvalidOperationException("Missing tube comparison assets.");
            ParkingLotCinematicCapture.UtilityNativeViews("tube");
            CreatePaths();
            mesh=RibbonMesh();var shader=Shader.Find("ParkingLot/UtilityWireAA");if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Wire AA shader unavailable.");
            material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};f.sharedMesh=mesh;r.sharedMaterial=material;
            ParkingLotCinematicCapture.UtilityNativeViews("aa");
        }finally{f.sharedMesh=previousMesh;r.sharedMaterials=previousMaterials;if(mesh!=null)UnityEngine.Object.DestroyImmediate(mesh);if(material!=null)UnityEngine.Object.DestroyImmediate(material);}
        File.WriteAllText(Output+"/utility-wire-aa-success.txt",DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty);
    }
    static void CreatePaths(){cablePaths.Clear();cableRadii.Clear();Front(null,new Vector3(-40,.12f,-37.1f),Poles[0]);Front(null,new Vector3(32,.12f,-37.1f),Poles[2]);Span(null,Poles[0],Poles[1],Vector3.right,false);Span(null,Poles[2],Poles[3],Vector3.right,false);Span(null,Poles[1],Poles[4],Vector3.forward,true);Span(null,Poles[4],Poles[3],Vector3.forward,true);}
    static void SetWireMesh(MeshFilter filter){
        if(filter.sharedMesh!=null){string path=AssetDatabase.GetAssetPath(filter.sharedMesh);if(path!=Root+"/Meshes/KoreanContext/Utility_extension_wires.asset"&&path!=Root+"/Meshes/KoreanContext/Utility_extension_antialiased_wires.asset")throw new InvalidOperationException("Refusing to replace an unrecognized wire mesh.");}
        var shader=Shader.Find("ParkingLot/UtilityWireAA");if(shader==null||!shader.isSupported||ShaderUtil.ShaderHasError(shader))throw new InvalidOperationException("Wire AA shader unavailable.");
        string matPath=Root+"/Materials/KoreanContext/Utility_extension_antialiased_wire.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}else if(material.shader!=shader)throw new InvalidOperationException("Wire material shader was edited.");
        filter.sharedMesh=ParkingLotMeshAsset.Save(RibbonMesh(),Root+"/Meshes/KoreanContext/Utility_extension_antialiased_wires.asset");var renderer=filter.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;AssetDatabase.SaveAssets();
    }
    public static string Validate(Scene scene){
        if(scene.path!=Target||!scene.isLoaded)throw new InvalidOperationException("Wrong utility audit scene.");CreatePaths();Audit(scene);
        var root=scene.GetRootGameObjects().Single(g=>g.name==Name);var filters=root.GetComponentsInChildren<MeshFilter>();var colliders=root.GetComponentsInChildren<CapsuleCollider>();
        if(filters.Length!=2||colliders.Length!=5)throw new InvalidOperationException("Unexpected utility group contents.");
        var wire=filters.Single(f=>f.name=="Utility_extension_wires");var mesh=wire.sharedMesh;var renderer=wire.GetComponent<MeshRenderer>();
        if(mesh==null||mesh.vertexCount!=1188||mesh.triangles.Length!=3456||renderer.sharedMaterial==null||renderer.sharedMaterial.shader.name!="ParkingLot/UtilityWireAA"||ShaderUtil.ShaderHasError(renderer.sharedMaterial.shader)||renderer.shadowCastingMode!=UnityEngine.Rendering.ShadowCastingMode.Off)throw new InvalidOperationException("Wire mesh/material check failed.");
        var expected=RibbonMesh();try{if(!mesh.vertices.SequenceEqual(expected.vertices)||!mesh.normals.SequenceEqual(expected.normals)||!mesh.uv.SequenceEqual(expected.uv)||!mesh.triangles.SequenceEqual(expected.triangles))throw new InvalidOperationException("Saved wire path data differs from the reviewed layout.");}finally{UnityEngine.Object.DestroyImmediate(expected);}
        foreach(var p in Poles)if(!colliders.Any(c=>(c.transform.position-p.p).sqrMagnitude<.000001f&&Mathf.Abs(c.height-p.h)<.00001f))throw new InvalidOperationException("Pole collider differs from its mast.");
        return DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nPoles=5\nWirePaths=18\nWireTriangles=1152\nTotalAddedTriangles="+filters.Sum(f=>f.sharedMesh.triangles.Length/3)+"\nRenderers=2\nBuildingAndTreeOverlap=0\nSavedWireDataMatches=True";
    }
    static Mesh RibbonMesh(){
        var p=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var ids=new List<int>();
        for(int j=0;j<cablePaths.Count;j++){
            var path=cablePaths[j];int start=p.Count;
            for(int i=0;i<path.Length;i++){var tangent=(path[Mathf.Min(i+1,path.Length-1)]-path[Mathf.Max(0,i-1)]).normalized;foreach(int s in new[]{-1,1}){p.Add(path[i]);n.Add(tangent);uv.Add(new Vector2(s,cableRadii[j]));}}
            for(int i=0;i<path.Length-1;i++){int a=start+i*2;ids.AddRange(new[]{a,a+2,a+1,a+1,a+2,a+3});}
        }
        var mesh=new Mesh{name="Utility_extension_antialiased_wires"};mesh.SetVertices(p);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.tangents=Enumerable.Repeat(new Vector4(1,0,0,1),p.Count).ToArray();mesh.SetTriangles(ids,0);mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.5f);mesh.bounds=bounds;mesh.UploadMeshData(false);return mesh;
    }
    static bool Hit(Vector3 a,Vector3 b,Bounds box){var d=b-a;if(box.Contains(a)||box.Contains(b))return true;return box.IntersectRay(new Ray(a,d.normalized),out float distance)&&distance>=0&&distance<=d.magnitude;}
    static void Audit(Scene scene){
        var buildings=scene.GetRootGameObjects().Where(g=>g.name=="09_Korean_neighborhood_context"||g.name=="12_Korean_residential_infill").SelectMany(g=>g.GetComponentsInChildren<BoxCollider>()).ToArray();
        if(buildings.Length!=32)throw new InvalidOperationException("Expected 32 building bounds for utility clearance.");
        foreach(var pole in Poles){if(Mathf.Abs(pole.p.x)<30&&pole.p.z<25)throw new InvalidOperationException("Utility pole entered playable lot.");foreach(var b in buildings){var bounds=b.bounds;bounds.Expand(.44f);if(Hit(pole.p,pole.p+Vector3.up*pole.h,bounds))throw new InvalidOperationException("Pole intersects "+b.name);}}
        foreach(var path in cablePaths)foreach(var b in buildings){var bounds=b.bounds;bounds.Expand(.08f);for(int i=1;i<path.Length;i++)if(Hit(path[i-1],path[i],bounds))throw new InvalidOperationException("Wire intersects "+b.name);}
        var trees=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>t.name=="Photographic_3D_tree"||t.name=="Neighborhood_tree_12k");int treeHits=0;
        foreach(var t in trees){var rr=t.GetComponentsInChildren<Renderer>();if(rr.Length==0)continue;var bounds=rr[0].bounds;foreach(var r in rr)bounds.Encapsulate(r.bounds);foreach(var path in cablePaths)if(Enumerable.Range(1,path.Length-1).Any(i=>Hit(path[i-1],path[i],bounds)))treeHits++;}
        File.WriteAllText(Output+"/utility-network-audit.txt",DateTime.Now.ToString("s")+"\nPoles=5\nSpans=6\nWirePaths="+cablePaths.Count+"\nBuildingBounds=32\nPoleBuildingOverlaps=0\nWireBuildingOverlaps=0\nTreeBoundsWireHits="+treeHits+"\nMinimumCableCentreHeight="+cablePaths.SelectMany(p=>p).Min(p=>p.y));
        if(treeHits>0)throw new InvalidOperationException("Utility wires intersect "+treeHits+" tree bounds; reroute before review.");
    }
}
}
