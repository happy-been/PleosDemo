using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
[InitializeOnLoad]
public static class ParkingLotWindshieldFinish {
    const string Root="Assets/ParkingLotEnvironment",Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string MeshPath=Root+"/RealVehicles/hyundai-ioniq5-openx/Meshes/part_2.asset";
    const string SourcePath=Root+"/RealVehicles/hyundai-ioniq5-openx/Materials/002_Body_Glass.mat";
    const string ResultPath=Root+"/Materials/Ioniq/Windshield_clear_view.mat";
    static readonly string Output=ParkingLotEditorPaths.Output;
    static ParkingLotWindshieldFinish(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/windshield-finish.request";if(!File.Exists(path))return;
        string mode;try{mode=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{
            var scene=SceneManager.GetActiveScene();if(scene.path!=Target||scene.isDirty)throw new InvalidOperationException("Open the saved map before windshield review.");
            if(mode==""||mode=="review")Review(scene);
            else if(mode=="apply"){
                Build(scene);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save windshield finish.");
                File.WriteAllText(Output+"/windshield-apply-success.txt",Validate(scene));
            }else if(mode=="validate")File.WriteAllText(Output+"/windshield-validate-success.txt",Validate(scene));
            else throw new InvalidOperationException("Unknown windshield operation.");
        }catch(Exception e){File.WriteAllText(Output+"/windshield-finish-error.txt",e.ToString());Debug.LogException(e);}
    }
    static MeshRenderer[] Renderers(Scene scene){
        if(scene.path!=Target||!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Windshield finish belongs only to ParklingLot.");
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
        if(mesh==null||mesh.vertexCount!=329||mesh.triangles.Length!=1440)throw new InvalidOperationException("Original combined Ioniq glazing mesh differs.");
        var positions=mesh.vertices;var normals=mesh.normals;var triangles=mesh.triangles;
        var front=Enumerable.Range(0,positions.Length).Select(i=>normals[i].y>.6f&&normals[i].z<-.25f&&positions[i].z<-.3f).ToArray();int frontTriangles=0;
        for(int i=0;i<triangles.Length;i+=3){int count=(front[triangles[i]]?1:0)+(front[triangles[i+1]]?1:0)+(front[triangles[i+2]]?1:0);if(count==3)frontTriangles++;else if(count!=0)throw new InvalidOperationException("A glass triangle crosses the front/side mask.");}
        if(front.Count(v=>v)!=86||frontTriangles!=124)throw new InvalidOperationException("Front windshield selection differs from source analysis.");
        var cars=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("Electric_crossover_bay_")&&t.name.EndsWith("_hyundai-ioniq5-openx")).ToArray();
        if(cars.Length!=3)throw new InvalidOperationException("Expected three original Ioniqs.");
        return cars.Select(car=>{
            var matches=car.GetComponentsInChildren<MeshFilter>(true).Where(f=>f.sharedMesh==mesh).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("Expected exactly one unmodified windshield per Ioniq.");
            var r=matches[0].GetComponent<MeshRenderer>();if(r==null||r.sharedMaterials.Length!=1)throw new InvalidOperationException("Unexpected windshield material slots.");return r;
        }).ToArray();
    }
    static Shader CheckedShader(){var s=Shader.Find("ParkingLot/ClearViewWindshield");if(s==null||!s.isSupported||ShaderUtil.ShaderHasError(s))throw new InvalidOperationException("Windshield shader not ready.");return s;}
    static Material Copy(){
        var source=AssetDatabase.LoadAssetAtPath<Material>(SourcePath);if(source==null)throw new InvalidOperationException("Missing original Ioniq glass.");
        var copy=new Material(source){name="Windshield_clear_view",shader=CheckedShader()};copy.SetFloat("_WindshieldAlpha",.12f);return copy;
    }
    static void Review(Scene scene){
        var renderers=Renderers(scene);var previous=renderers.ToDictionary(r=>r,r=>r.sharedMaterial);var copy=Copy();copy.hideFlags=HideFlags.HideAndDontSave;
        try{
            foreach(var r in renderers)r.sharedMaterial=copy;
            ParkingLotCinematicCapture.DriverSeatViews("windshield-candidate");
            ParkingLotCinematicCapture.WholeMapViews("windshield-candidate");
        }finally{foreach(var pair in previous)pair.Key.sharedMaterial=pair.Value;UnityEngine.Object.DestroyImmediate(copy);}
        if(scene.isDirty)throw new InvalidOperationException("Windshield review dirtied scene.");
        File.WriteAllText(Output+"/windshield-review-success.txt",DateTime.Now.ToString("s")+"\nSaved=True\nTemporaryOnly=True\nWindshields=3\nGeometryChanges=0\nOriginalMaterialReferencesRestored=True");
    }
    public static void Build(Scene scene){
        var renderers=Renderers(scene);var copy=AssetDatabase.LoadAssetAtPath<Material>(ResultPath);
        if(copy==null){copy=Copy();AssetDatabase.CreateAsset(copy,ResultPath);AssetDatabase.SaveAssetIfDirty(copy);}
        foreach(var r in renderers)r.sharedMaterial=copy;
        File.WriteAllText(Output+"/windshield-finish-audit.txt",Validate(scene)+"\nSourceMaterialsModified=False\nSourceMeshesModified=False\nGeometryChanges=0\nNewTextures=0");
    }
    public static string Validate(Scene scene){
        var renderers=Renderers(scene);var source=AssetDatabase.LoadAssetAtPath<Material>(SourcePath);var copy=AssetDatabase.LoadAssetAtPath<Material>(ResultPath);
        if(source==null||copy==null||copy.shader!=CheckedShader()||renderers.Any(r=>r.sharedMaterial!=copy)||Mathf.Abs(copy.GetFloat("_WindshieldAlpha")-.12f)>.0001f||copy.color!=source.color||copy.mainTexture!=source.mainTexture)throw new InvalidOperationException("Saved windshield finish differs.");
        return DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nWindshields="+renderers.Length+"\nFrontVerticesPerCar=86\nFrontTrianglesPerCar=124\nOtherWindowMaskVertices=0\nSharedMaterialCopies=1\nOriginalMeshReferences=True\nShaderErrors=0";
    }
}
}
