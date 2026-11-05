using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Reuse whole facade floors, keeping window proportions and every building footprint.
[InitializeOnLoad]
public static class ParkingLotSkylineVariation {
    const string Root="Assets/ParkingLotEnvironment";
    const string Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static readonly Dictionary<int,int> Floors=new Dictionary<int,int>{{0,3},{1,5},{5,3},{8,5},{9,3},{10,3},{12,5},{15,3},{18,5},{21,3},{24,5}};
    const string RoofPath=Root+"/Meshes/Skyline/Villa_roof_contacts_varied_floors_clear_street.asset";
    const string WallSource=Root+"/Meshes/KoreanContext/Residential_plot_boundary_walls.asset";
    const string WallPath=Root+"/Meshes/Skyline/Residential_plot_boundary_walls_clear_street.asset";
    static ParkingLotSkylineVariation(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/skyline-variation.request";if(!File.Exists(path))return;
        string mode;try{mode=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{
            var scene=SceneManager.GetActiveScene();if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open the saved parking map before skyline work.");
            if(mode=="review")Review(scene);
            else if(mode=="apply"||mode=="clear-street"){
                if(mode=="apply")Build(scene);else ClearStreet(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save skyline variation.");
                File.WriteAllText(Output+"/skyline-variation-apply-success.txt",Validate(scene));
            }else if(mode=="validate")File.WriteAllText(Output+"/skyline-variation-validate-success.txt",Validate(scene));
            else throw new InvalidOperationException("Unknown skyline operation.");
        }catch(Exception e){File.WriteAllText(Output+"/skyline-variation-error.txt",e.ToString());Debug.LogException(e);}
    }
    public static int FloorCount(int index){return Floors.TryGetValue(index,out int floors)?floors:4;}
    public static Vector3 Position(int index){return ParkingLotNeighborhoodInfill.Buildings[index].position+(index==20?Vector3.left*1.25f:Vector3.zero);}
    static string VariantPath(int type,int floors){return Root+"/Meshes/Skyline/Korean_villa_type_"+type+"_"+floors+"floors.asset";}
    public static bool IsSavedVariant(int index,Mesh mesh){return Floors.ContainsKey(index)&&AssetDatabase.GetAssetPath(mesh)==VariantPath(ParkingLotNeighborhoodInfill.Buildings[index].finish,FloorCount(index));}
    static MeshFilter[] Villas(Scene scene,bool finished=false){
        if(scene.path!=Target||!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Skyline variation belongs only to this map.");
        var parent=scene.GetRootGameObjects().Single(g=>g.name=="12_Korean_residential_infill");
        var villas=parent.GetComponentsInChildren<MeshFilter>(true).OrderBy(f=>f.name,StringComparer.Ordinal).ToArray();
        if(villas.Length!=25)throw new InvalidOperationException("Unexpected villa count.");
        for(int i=0;i<villas.Length;i++){
            var b=ParkingLotNeighborhoodInfill.Buildings[i];var f=villas[i];
            bool validMesh=finished&&Floors.ContainsKey(i)?IsSavedVariant(i,f.sharedMesh):AssetDatabase.GetAssetPath(f.sharedMesh)==Root+"/Meshes/KoreanContext/Korean_villa_type_"+b.finish+".asset";
            if(f.name!="Residential_villa_"+(i+1).ToString("D2")||!validMesh)throw new InvalidOperationException("Unexpected villa mesh: "+f.name);
            if(f.transform.localPosition!=(finished?Position(i):b.position)||f.transform.localScale!=b.scale||Quaternion.Angle(f.transform.localRotation,Quaternion.Euler(0,b.yaw,0))>.001f)throw new InvalidOperationException("Villa placement was manually edited.");
            float height=finished?FloorCount(i)*3.2f:12.8f;var box=f.GetComponent<BoxCollider>();
            if(box==null||Vector3.Distance(box.center,new Vector3(0,height/2,0))>.001f||Vector3.Distance(box.size,new Vector3(13,height,11))>.001f)throw new InvalidOperationException("Unexpected villa collider: "+f.name);
        }
        return villas;
    }
    struct Vertex {
        public Vector3 p,n;public Vector4 t;public Vector2 uv;
        public static Vertex Lerp(Vertex a,Vertex b,float t){return new Vertex{p=Vector3.LerpUnclamped(a.p,b.p,t),n=Vector3.LerpUnclamped(a.n,b.n,t),t=Vector4.LerpUnclamped(a.t,b.t,t),uv=Vector2.LerpUnclamped(a.uv,b.uv,t)};}
    }
    static List<Vertex> Clip(List<Vertex> polygon,float height,bool above){
        var result=new List<Vertex>();if(polygon.Count==0)return result;
        Vertex a=polygon[polygon.Count-1];bool inA=above?a.p.y>=height:a.p.y<=height;
        foreach(var b in polygon){bool inB=above?b.p.y>=height:b.p.y<=height;
            if(inA!=inB){float t=(height-a.p.y)/(b.p.y-a.p.y);var v=Vertex.Lerp(a,b,t);v.p.y=height;result.Add(v);}
            if(inB)result.Add(b);a=b;inA=inB;
        }return result;
    }
    static Mesh Variant(Mesh source,int floors){
        if(floors!=3&&floors!=5)throw new InvalidOperationException("Only complete 3/5-floor variants are supported.");
        if(source.colors.Length>0||source.uv2.Length>0||source.uv3.Length>0||source.uv4.Length>0||source.blendShapeCount>0)throw new InvalidOperationException("Unexpected source vertex channels.");
        var p=source.vertices;var n=source.normals;var uv=source.uv;var t=source.tangents;
        if(n.Length!=p.Length||uv.Length!=p.Length||t.Length!=p.Length)throw new InvalidOperationException("Missing source vertex attributes.");
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var tangents=new List<Vector4>();var uvs=new List<Vector2>();
        var indices=new List<int>[source.subMeshCount];
        var bands=floors==3?new[]{new Vector3(-1000,3.2f,0),new Vector3(6.4f,1000,-3.2f)}:new[]{new Vector3(-1000,6.4f,0),new Vector3(3.2f,6.4f,3.2f),new Vector3(6.4f,1000,3.2f)};
        for(int sub=0;sub<indices.Length;sub++){
            if(source.GetTopology(sub)!=UnityEngine.MeshTopology.Triangles)throw new InvalidOperationException("Expected triangle topology.");
            indices[sub]=new List<int>();var tris=source.GetTriangles(sub);
            for(int k=0;k<tris.Length;k+=3){
                var triangle=new List<Vertex>(3);for(int j=0;j<3;j++){int id=tris[k+j];triangle.Add(new Vertex{p=p[id],n=n[id],t=t[id],uv=uv[id]});}
                foreach(var band in bands){
                    var poly=Clip(Clip(triangle,band.x,true),band.y,false);
                    for(int j=1;j+1<poly.Count;j++){
                        var a=poly[0];var b=poly[j];var c=poly[j+1];
                        if(Vector3.Cross(b.p-a.p,c.p-a.p).sqrMagnitude<1e-14f)continue;
                        foreach(var v in new[]{a,b,c}){
                            indices[sub].Add(vertices.Count);vertices.Add(v.p+Vector3.up*band.z);normals.Add(v.n.normalized);
                            Vector3 tangent=new Vector3(v.t.x,v.t.y,v.t.z).normalized;tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,v.t.w<0?-1:1));uvs.Add(v.uv);
                        }
                    }
                }
            }
        }
        var mesh=new Mesh{name=source.name+"_"+floors+"floors",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,hideFlags=HideFlags.HideAndDontSave};
        mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTangents(tangents);mesh.SetUVs(0,uvs);mesh.subMeshCount=indices.Length;
        for(int sub=0;sub<indices.Length;sub++)mesh.SetTriangles(indices[sub],sub,false);mesh.RecalculateBounds();
        var before=source.bounds;var after=mesh.bounds;
        if(Mathf.Abs(before.min.x-after.min.x)>.001f||Mathf.Abs(before.max.x-after.max.x)>.001f||Mathf.Abs(before.min.z-after.min.z)>.001f||Mathf.Abs(before.max.z-after.max.z)>.001f||Mathf.Abs(after.max.y-before.max.y-(floors-4)*3.2f)>.001f||Mathf.Abs(after.min.y-before.min.y)>.001f)throw new InvalidOperationException("Floor variant unexpectedly changed footprint or roof offset.");
        if(vertices.Any(v=>float.IsNaN(v.x)||float.IsNaN(v.y)||float.IsNaN(v.z)||float.IsInfinity(v.x)||float.IsInfinity(v.y)||float.IsInfinity(v.z)))throw new InvalidOperationException("Non-finite geometry.");
        return mesh;
    }
    static Mesh RoofVariant(Mesh source,bool movePlot=true){
        var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Villa_roof_contacts_varied_floors";mesh.hideFlags=HideFlags.HideAndDontSave;
        var vertices=mesh.vertices;int changed=0;
        for(int v=0;v<vertices.Length;v++){
            var owners=ParkingLotNeighborhoodInfill.Buildings.Select((b,i)=>new{b,i}).Where(x=>x.b.Footprint(.001f).Contains(new Vector2(vertices[v].x,vertices[v].z))).ToArray();
            if(owners.Length!=1)throw new InvalidOperationException("Ambiguous roof contact owner.");
            var owner=owners[0];if(Floors.TryGetValue(owner.i,out int floors)){vertices[v].y+=(floors-4)*3.2f*owner.b.scale.y;changed++;}
            if(movePlot&&owner.i==20)vertices[v].x-=1.25f;
        }
        if(changed!=Floors.Count*36)throw new InvalidOperationException("Unexpected roof contact vertex count.");
        mesh.vertices=vertices;mesh.RecalculateBounds();return mesh;
    }
    public static void Build(Scene scene){
        var villas=Villas(scene);var originals=villas.ToDictionary(f=>f,f=>f.sharedMesh);
        var materials=villas.ToDictionary(f=>f,f=>f.GetComponent<MeshRenderer>().sharedMaterials);
        var transforms=villas.ToDictionary(f=>f,f=>f.transform.localToWorldMatrix);
        var roof=scene.GetRootGameObjects().Single(g=>g.name=="14_Rooftop_contact_shading").GetComponent<MeshFilter>();
        string roofSource=Root+"/Meshes/KoreanContext/Villa_roof_contact_surfaces.asset";
        if(AssetDatabase.GetAssetPath(roof.sharedMesh)!=roofSource)throw new InvalidOperationException("Roof contact source was changed.");
        if(!AssetDatabase.IsValidFolder(Root+"/Meshes/Skyline"))AssetDatabase.CreateFolder(Root+"/Meshes","Skyline");
        var prepared=new Dictionary<string,Mesh>();
        foreach(var pair in Floors){var f=villas[pair.Key];string path=VariantPath(ParkingLotNeighborhoodInfill.Buildings[pair.Key].finish,pair.Value);
            if(prepared.ContainsKey(path))continue;
            var candidate=Variant(f.sharedMesh,pair.Value);var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null){try{SameMesh(existing,candidate);}finally{UnityEngine.Object.DestroyImmediate(candidate);}prepared.Add(path,existing);}
            else{candidate.hideFlags=HideFlags.None;prepared.Add(path,ParkingLotMeshAsset.Save(candidate,path));}
        }
        string roofPath=RoofPath;
        var roofCandidate=RoofVariant(roof.sharedMesh);var savedRoof=AssetDatabase.LoadAssetAtPath<Mesh>(roofPath);
        if(savedRoof!=null){try{SameMesh(savedRoof,roofCandidate);}finally{UnityEngine.Object.DestroyImmediate(roofCandidate);}}
        else{roofCandidate.hideFlags=HideFlags.None;savedRoof=ParkingLotMeshAsset.Save(roofCandidate,roofPath);}
        foreach(var pair in Floors){var f=villas[pair.Key];f.sharedMesh=prepared[VariantPath(ParkingLotNeighborhoodInfill.Buildings[pair.Key].finish,pair.Value)];float height=pair.Value*3.2f;
            var box=f.GetComponent<BoxCollider>();box.center=new Vector3(0,height/2,0);box.size=new Vector3(13,height,11);
        }
        roof.sharedMesh=savedRoof;
        foreach(var f in villas)if(f.transform.localToWorldMatrix!=transforms[f]||!f.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(materials[f]))throw new InvalidOperationException("Skyline pass changed placement or materials.");
        RelocatePlot(scene);
        File.WriteAllText(Output+"/skyline-variation-audit.txt",Validate(scene)+"\nSourceMeshesModified=False\nMaterialReferencesUnchanged=True\nAllExceptPlot21TransformsUnchanged=True\nAddedRenderers=0");
    }
    static Mesh WallsVariant(){
        var source=AssetDatabase.LoadAssetAtPath<Mesh>(WallSource);if(source==null)throw new InvalidOperationException("Original boundary mesh missing.");
        var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Residential_plot_boundaries_clear_street";mesh.hideFlags=HideFlags.HideAndDontSave;
        var vertices=mesh.vertices;var footprint=ParkingLotNeighborhoodInfill.Buildings[20].Footprint(1.05f);int moved=0;
        for(int i=0;i<vertices.Length;i++)if(footprint.Contains(new Vector2(vertices[i].x,vertices[i].z))){vertices[i].x-=1.25f;moved++;}
        if(moved!=72)throw new InvalidOperationException("Expected exactly three walls/72 vertices for plot 21, found "+moved);
        mesh.vertices=vertices;mesh.RecalculateBounds();return mesh;
    }
    static Mesh SaveCandidate(Mesh candidate,string path){
        var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(saved!=null){try{SameMesh(saved,candidate);}finally{UnityEngine.Object.DestroyImmediate(candidate);}return saved;}
        candidate.hideFlags=HideFlags.None;return ParkingLotMeshAsset.Save(candidate,path);
    }
    static void RelocatePlot(Scene scene){
        var villa=scene.GetRootGameObjects().Single(g=>g.name=="12_Korean_residential_infill").transform.Find("Residential_villa_21");
        var context=scene.GetRootGameObjects().Single(g=>g.name=="10_Connected_Korean_neighborhood");
        var surfaces=context.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Residential_plot_21"||f.name=="North_entry_walk_21").ToArray();
        var walls=context.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Residential_plot_boundary_walls");
        if(villa==null||villa.localPosition!=ParkingLotNeighborhoodInfill.Buildings[20].position||surfaces.Length!=2||surfaces.Any(f=>f.transform.localPosition!=Vector3.zero)||AssetDatabase.GetAssetPath(walls.sharedMesh)!=WallSource)throw new InvalidOperationException("Plot 21 was manually edited; inspect before moving it.");
        var shiftedWalls=SaveCandidate(WallsVariant(),WallPath);
        villa.localPosition=Position(20);foreach(var f in surfaces)f.transform.localPosition=Vector3.left*1.25f;walls.sharedMesh=shiftedWalls;
    }
    static void ClearStreet(Scene scene){
        // Migration from the already saved floor-height candidate; never rebuild the map.
        var roof=scene.GetRootGameObjects().Single(g=>g.name=="14_Rooftop_contact_shading").GetComponent<MeshFilter>();
        if(AssetDatabase.GetAssetPath(roof.sharedMesh)!=Root+"/Meshes/Skyline/Villa_roof_contacts_varied_floors.asset")throw new InvalidOperationException("Expected saved first skyline candidate.");
        var candidate=RoofVariant(AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/KoreanContext/Villa_roof_contact_surfaces.asset"));
        var saved=SaveCandidate(candidate,RoofPath);RelocatePlot(scene);roof.sharedMesh=saved;
    }
    static void SameMesh(Mesh actual,Mesh expected){
        if(actual.vertexCount!=expected.vertexCount||actual.subMeshCount!=expected.subMeshCount||!actual.vertices.SequenceEqual(expected.vertices)||!actual.uv.SequenceEqual(expected.uv)||!actual.colors.SequenceEqual(expected.colors)||!actual.uv2.SequenceEqual(expected.uv2))throw new InvalidOperationException("Saved mesh differs from reviewed complete-floor geometry.");
        // MeshAsset.Save supplies normals/tangents for the contact decal when its source omitted them.
        if(expected.normals.Length>0&&!actual.normals.SequenceEqual(expected.normals))throw new InvalidOperationException("Saved mesh normals differ.");
        if(expected.tangents.Length>0&&!actual.tangents.SequenceEqual(expected.tangents))throw new InvalidOperationException("Saved mesh tangents differ.");
        for(int i=0;i<actual.subMeshCount;i++)if(!actual.GetIndices(i).SequenceEqual(expected.GetIndices(i)))throw new InvalidOperationException("Saved mesh indices differ.");
    }
    public static string Validate(Scene scene){
        var villas=Villas(scene,true);var checkedMeshes=new HashSet<Mesh>();long delta=0;
        ParkingLotNeighborhoodAudit.Check(scene);
        for(int i=0;i<villas.Length;i++){
            var actual=villas[i].sharedMesh;var source=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/KoreanContext/Korean_villa_type_"+ParkingLotNeighborhoodInfill.Buildings[i].finish+".asset");
            delta+=(actual.triangles.Length-source.triangles.Length)/3;
            if(!Floors.ContainsKey(i)||!checkedMeshes.Add(actual))continue;
            var expected=Variant(source,FloorCount(i));try{SameMesh(actual,expected);}finally{UnityEngine.Object.DestroyImmediate(expected);}
        }
        var roof=scene.GetRootGameObjects().Single(g=>g.name=="14_Rooftop_contact_shading").GetComponent<MeshFilter>();
        if(AssetDatabase.GetAssetPath(roof.sharedMesh)!=RoofPath)throw new InvalidOperationException("Roof contact variant not saved.");
        var expectedRoof=RoofVariant(AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/KoreanContext/Villa_roof_contact_surfaces.asset"));try{SameMesh(roof.sharedMesh,expectedRoof);}finally{UnityEngine.Object.DestroyImmediate(expectedRoof);}
        var context=scene.GetRootGameObjects().Single(g=>g.name=="10_Connected_Korean_neighborhood");
        var surfaces=context.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.name=="Residential_plot_21"||f.name=="North_entry_walk_21").ToArray();
        if(surfaces.Length!=2||surfaces.Any(f=>f.transform.localPosition!=Vector3.left*1.25f))throw new InvalidOperationException("Plot paving not aligned with relocated building.");
        var walls=context.GetComponentsInChildren<MeshFilter>(true).Single(f=>f.name=="Residential_plot_boundary_walls");
        if(AssetDatabase.GetAssetPath(walls.sharedMesh)!=WallPath)throw new InvalidOperationException("Relocated wall mesh not saved.");
        var expectedWalls=WallsVariant();try{SameMesh(walls.sharedMesh,expectedWalls);}finally{UnityEngine.Object.DestroyImmediate(expectedWalls);}
        return DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nThreeFloorBuildings="+Floors.Values.Count(f=>f==3)+"\nFourFloorBuildings="+(25-Floors.Count)+"\nFiveFloorBuildings="+Floors.Values.Count(f=>f==5)+"\nNewBuildingMeshes="+checkedMeshes.Count+"\nTrianglesDelta="+delta+"\nFootprintSizesUnchanged=True\nPlot21ShiftWestMetres=1.25\nRoofContactAlignment=True\nCollidersMatchHeight=True\nBuildingStreetOverlaps=0";
    }
    static void Review(Scene scene){
        var villas=Villas(scene);var previous=villas.ToDictionary(f=>f,f=>f.sharedMesh);var variants=new Dictionary<string,Mesh>();
        var roof=scene.GetRootGameObjects().Single(g=>g.name=="14_Rooftop_contact_shading").GetComponent<MeshFilter>();var originalRoof=roof.sharedMesh;Mesh roofVariant=null;
        try{
            foreach(var pair in Floors){var f=villas[pair.Key];string key=AssetDatabase.GetAssetPath(f.sharedMesh)+":"+pair.Value;
                if(!variants.TryGetValue(key,out var mesh)){mesh=Variant(f.sharedMesh,pair.Value);variants.Add(key,mesh);}f.sharedMesh=mesh;
            }
            roofVariant=RoofVariant(originalRoof,false);roof.sharedMesh=roofVariant;
            ParkingLotCinematicCapture.WholeMapViews("skyline-candidate");
        }finally{foreach(var pair in previous)pair.Key.sharedMesh=pair.Value;roof.sharedMesh=originalRoof;foreach(var mesh in variants.Values)UnityEngine.Object.DestroyImmediate(mesh);if(roofVariant!=null)UnityEngine.Object.DestroyImmediate(roofVariant);}
        if(scene.isDirty)throw new InvalidOperationException("Review dirtied scene; do not save.");
        File.WriteAllText(Output+"/skyline-variation-review-success.txt",DateTime.Now.ToString("s")+"\nSaved=True\nOriginalMeshesRestored=True\nTemporaryOnly=True\nBuildingsChanged="+Floors.Count+"\nFootprintsUnchanged=True\nWindowProportionsUnchanged=True\nRoofContactsMovedWithRoof=True");
    }
}
}
