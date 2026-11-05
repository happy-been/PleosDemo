using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Disposable normal-only comparisons. No saved mesh, source asset or scene is changed.
[InitializeOnLoad]
public static class ParkingLotTucsonNormalsReview {
    const string Root="Assets/ParkingLotEnvironment";
    static readonly string Output=ParkingLotEditorPaths.Output;
    struct Face {public Vector3 normal;public float weight;}
    static ParkingLotTucsonNormalsReview(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        bool macro=File.Exists(Output+"/tucson-macro-review.request");
        string request=Output+(macro?"/tucson-macro-review.request":"/tucson-normals-review.request");if(!File.Exists(request))return;
        try{File.Delete(request);}catch(IOException){return;}
        try{Review(macro);}catch(Exception e){File.WriteAllText(Output+"/tucson-normals-review-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Review(bool macro){
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity"||scene.isDirty)throw new InvalidOperationException("Open the saved parking map first.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RealVehicles/hyundai-tucson/hyundai-tucson.prefab");
        if(prefab==null)throw new InvalidOperationException("Tucson prefab missing.");
        var car=UnityEngine.Object.Instantiate(prefab);car.name="Temporary_Tucson_normal_review";car.hideFlags=HideFlags.HideInHierarchy;car.transform.position=new Vector3(18,0,4);car.transform.rotation=Quaternion.Euler(0,180,0);
        var meshes=new List<Mesh>();var report=new StringBuilder();
        var paint=new Material(AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Real_carpaint_1.mat")){hideFlags=HideFlags.HideAndDontSave};
        try{
            var renderer=car.GetComponentsInChildren<MeshRenderer>().Single(r=>r.sharedMaterial.name.EndsWith("_varnish"));renderer.sharedMaterial=paint;
            var filter=renderer.GetComponent<MeshFilter>();var source=filter.sharedMesh;
            if(!macro)ParkingLotCinematicCapture.CandidateViews("tucson-silver-normal-source",car.transform.position);
            var current=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Meshes/SurfaceNormals/Tucson_paint_weighted_normals.asset");if(current==null)throw new InvalidOperationException("Current normal derivative missing.");
            filter.sharedMesh=current;
            if(macro){
                DumpCurrent(current,filter.transform);ParkingLotCinematicCapture.TucsonMacroViews("current",car.transform.position);
            }
            else ParkingLotCinematicCapture.CandidateViews("tucson-silver-normal-current",car.transform.position);
            foreach(var variant in macro?new string[0]:new[]{"angle35","area35","angle60","area60"}){
                float crease=variant.EndsWith("35")?35:60;bool area=variant.StartsWith("area");
                var mesh=Make(source,crease,area,out int changed);meshes.Add(mesh);filter.sharedMesh=mesh;
                report.AppendLine(variant+" changed="+changed+"; vertexCount="+mesh.vertexCount+"; indices="+mesh.triangles.Length+"; bounds="+mesh.bounds);
                ParkingLotCinematicCapture.CandidateViews("tucson-silver-normal-"+variant,car.transform.position);
            }
        }finally{UnityEngine.Object.DestroyImmediate(car);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(paint);}
        report.AppendLine("Saved="+!scene.isDirty);File.WriteAllText(Output+(macro?"/tucson-macro-review-success.txt":"/tucson-normals-review-success.txt"),DateTime.Now.ToString("s")+"\n"+report);
    }
    static void DumpCurrent(Mesh mesh,Transform meshTransform){
        var scale=meshTransform.lossyScale;float unit=Mathf.Abs(scale.x);
        if(unit<=0||Mathf.Abs(Mathf.Abs(scale.y)-unit)>.000001f||Mathf.Abs(Mathf.Abs(scale.z)-unit)>.000001f)throw new InvalidOperationException("This comparison requires uniform model scale.");
        var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;var ids=mesh.triangles;
        using(var w=new BinaryWriter(File.Create(Output+"/tucson-paint-current.bin"))){
            for(int i=0;i<vertices.Length;i++){w.Write(vertices[i].x);w.Write(vertices[i].y);w.Write(vertices[i].z);w.Write(normals[i].x);w.Write(normals[i].y);w.Write(normals[i].z);w.Write(uv[i].x);w.Write(uv[i].y);}
            foreach(var id in ids)w.Write(id);
        }
        File.WriteAllText(Output+"/tucson-paint-current.json","{\"vertices\":"+vertices.Length+",\"triangles\":"+(ids.Length/3)+",\"worldMetresPerLocalUnit\":"+unit.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+",\"coordinates\":\"Unity mesh local units; prefab transform supplies physical scale; no extra transform or UV flip for reattachment\"}");
    }
    static Mesh Make(Mesh source,float crease,bool areaWeight,out int changed){
        var p=source.vertices;var original=source.normals;var ids=source.triangles;var keys=new Vector3Int[p.Length];var faces=new Dictionary<Vector3Int,List<Face>>();
        for(int i=0;i<p.Length;i++){keys[i]=new Vector3Int(Mathf.RoundToInt(p[i].x*100000),Mathf.RoundToInt(p[i].y*100000),Mathf.RoundToInt(p[i].z*100000));if(!faces.ContainsKey(keys[i]))faces[keys[i]]=new List<Face>();}
        for(int i=0;i<ids.Length;i+=3){
            int a=ids[i],b=ids[i+1],c=ids[i+2];var cross=Vector3.Cross(p[b]-p[a],p[c]-p[a]);float area=cross.magnitude;if(area<1e-12f)continue;var normal=cross/area;
            for(int j=0;j<3;j++){int at=ids[i+j],next=ids[i+(j+1)%3],last=ids[i+(j+2)%3];float angle=Vector3.Angle(p[next]-p[at],p[last]-p[at])*Mathf.Deg2Rad;faces[keys[at]].Add(new Face{normal=normal,weight=(areaWeight?area:1)*angle});}
        }
        var normals=new Vector3[p.Length];changed=0;float threshold=Mathf.Cos(crease*Mathf.Deg2Rad);
        for(int i=0;i<p.Length;i++){
            var sum=Vector3.zero;var basis=original[i].normalized;foreach(var face in faces[keys[i]])if(Vector3.Dot(face.normal,basis)>threshold)sum+=face.normal*face.weight;
            normals[i]=sum.sqrMagnitude>1e-12f?sum.normalized:basis;if(Vector3.Angle(normals[i],basis)>.5f)changed++;
        }
        var mesh=UnityEngine.Object.Instantiate(source);mesh.name="Temporary_Tucson_normal_derivative";mesh.hideFlags=HideFlags.HideAndDontSave;mesh.normals=normals;mesh.RecalculateTangents();mesh.UploadMeshData(false);return mesh;
    }
}
}
