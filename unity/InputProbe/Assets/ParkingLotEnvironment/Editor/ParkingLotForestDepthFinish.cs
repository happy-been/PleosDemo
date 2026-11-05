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
public static class ParkingLotForestDepthFinish {
    const string Root="Assets/ParkingLotEnvironment",Target="Assets/_Parkside/Scenes/ParklingLot.unity";
    const string Group="20_Layered_distant_forest",MeshFolder=Root+"/Meshes/ForestDepth";
    const string MaterialPath=Root+"/Materials/KoreanContext/Layered_distant_forest.mat";
    static readonly string Output=ParkingLotEditorPaths.Output;
    const int Sectors=12;
    static ParkingLotForestDepthFinish(){EditorApplication.update+=Tick;}
    static void Tick(){
        if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||ParkingLotCinematicCapture.IsCapturing)return;
        string path=Output+"/forest-depth.request";if(!File.Exists(path))return;
        string mode;try{mode=File.ReadAllText(path).Trim();File.Delete(path);}catch(IOException){return;}
        try{
            var scene=SceneManager.GetActiveScene();Check(scene);if(scene.isDirty)throw new InvalidOperationException("Save map before forest review.");
            if(mode=="review"||mode=="")Review(scene);
            else if(mode=="apply"){
                Build(scene);EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,Target))throw new IOException("Could not save forest layer.");
                File.WriteAllText(Output+"/forest-depth-apply-success.txt",Validate(scene));
            }else if(mode=="validate")File.WriteAllText(Output+"/forest-depth-validate-success.txt",Validate(scene));
            else throw new InvalidOperationException("Unknown forest operation.");
        }catch(Exception e){File.WriteAllText(Output+"/forest-depth-error.txt",e.ToString());Debug.LogException(e);}
    }
    static void Check(Scene scene){if(scene.path!=Target||!scene.IsValid()||!scene.isLoaded)throw new InvalidOperationException("Forest layer belongs only to ParklingLot.");}
    static Shader CheckedShader(){var s=Shader.Find("ParkingLot/LayeredDistantForest");if(s==null||!s.isSupported||ShaderUtil.ShaderHasError(s))throw new InvalidOperationException("Forest shader not ready.");return s;}
    static Material MakeMaterial(){
        var source=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/KoreanContext/Distant_courtyard_tree_cards.mat");
        if(source==null||source.mainTexture==null)throw new InvalidOperationException("Missing existing canopy source.");
        return new Material(source){name="Layered_distant_forest",shader=CheckedShader(),color=new Color(.62f,.71f,.62f,1)};
    }
    sealed class Geometry {
        public readonly List<Vector3> Positions=new List<Vector3>();
        public readonly List<Vector2> UV=new List<Vector2>();
        public readonly List<Color> Colors=new List<Color>();
        public readonly List<int> Triangles=new List<int>();
        public void Card(Vector3 p,float width,float height,float angle,Color color,bool flip){
            var right=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*width*.5f;int b=Positions.Count;
            Positions.AddRange(new[]{p-right,p+right,p+right+Vector3.up*height,p-right+Vector3.up*height});
            UV.AddRange(new[]{new Vector2(flip?1:0,0),new Vector2(flip?0:1,0),new Vector2(flip?0:1,1),new Vector2(flip?1:0,1)});
            for(int i=0;i<4;i++)Colors.Add(color);
            Triangles.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});
        }
        public Mesh Mesh(int index){var m=new Mesh{name="Forest_sector_"+index.ToString("00"),indexFormat=IndexFormat.UInt32};m.SetVertices(Positions);m.SetUVs(0,UV);m.SetColors(Colors);m.SetTriangles(Triangles,0);m.RecalculateBounds();return m;}
    }
    static Mesh[] Generate(out int clusters){
        var geo=Enumerable.Range(0,Sectors).Select(i=>new Geometry()).ToArray();var rng=new System.Random(340928);
        // Reuse the same terrain function as the saved hills, without modifying their mesh.
        var heightMethod=typeof(ParkingLotNeighborhoodGround).GetMethod("HillHeight",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
        if(heightMethod==null)throw new InvalidOperationException("Missing terrain sampling function.");
        clusters=0;
        for(float z=-625;z<=625;z+=12)for(float x=-625;x<=625;x+=12){
            float px=x+((float)rng.NextDouble()-.5f)*9,pz=z+((float)rng.NextDouble()-.5f)*9;
            float radius=Mathf.Sqrt(px*px+pz*pz),a=Mathf.Atan2(px,pz);if(a<0)a+=Mathf.PI*2;
            float edge=Mathf.Max(175,190+22*Mathf.Sin(a*3+.5f)+12*Mathf.Sin(a*7));
            float outer=535+45*Mathf.Sin(a*2-.4f);
            if(radius<edge||radius>outer)continue;
            float patch=Mathf.PerlinNoise(px*.019f+32,pz*.019f+67);
            if(patch<.27f||radius<edge+35&&patch<.48f)continue;
            float height=6+(float)rng.NextDouble()*4,width=height*(1.35f+(float)rng.NextDouble()*.3f);
            float ground=(float)heightMethod.Invoke(null,new object[]{a,radius});
            var p=new Vector3(px,Mathf.Max(0,ground)-1.4f,pz);
            float tint=.84f+(float)rng.NextDouble()*.24f;var color=new Color(tint,tint,tint,1);
            float turn=(float)rng.NextDouble()*Mathf.PI;bool flip=rng.Next(2)==0;
            int sector=Mathf.Min(Sectors-1,Mathf.FloorToInt(a/(Mathf.PI*2)*Sectors));
            for(int face=0;face<3;face++)geo[sector].Card(p,width,height,turn+face*Mathf.PI/3,color,flip);
            clusters++;
        }
        return geo.Select((g,i)=>g.Mesh(i)).ToArray();
    }
    static GameObject Create(Scene scene,Mesh[] meshes,Material mat,bool temporary){
        var root=new GameObject(Group);SceneManager.MoveGameObjectToScene(root,scene);
        if(temporary)root.hideFlags=HideFlags.HideAndDontSave;
        for(int i=0;i<meshes.Length;i++){
            var go=new GameObject("Forest_sector_"+i.ToString("00"));go.transform.SetParent(root.transform,false);
            if(temporary)go.hideFlags=HideFlags.HideAndDontSave;
            go.AddComponent<MeshFilter>().sharedMesh=meshes[i];var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
            r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.Off;go.isStatic=true;
        }
        return root;
    }
    static void Review(Scene scene){
        if(scene.GetRootGameObjects().Any(g=>g.name==Group))throw new InvalidOperationException("Forest layer already adopted.");
        Mesh[] meshes=null;Material mat=null;GameObject root=null;int clusters=0;
        try{
            meshes=Generate(out clusters);foreach(var m in meshes)m.hideFlags=HideFlags.HideAndDontSave;
            mat=MakeMaterial();mat.hideFlags=HideFlags.HideAndDontSave;root=Create(scene,meshes,mat,true);
            ParkingLotCinematicCapture.WholeMapViews("forest-depth-candidate");
            Benchmark(scene,root);
        }finally{
            if(root!=null)UnityEngine.Object.DestroyImmediate(root);if(mat!=null)UnityEngine.Object.DestroyImmediate(mat);
            if(meshes!=null)foreach(var m in meshes)UnityEngine.Object.DestroyImmediate(m);
        }
        if(scene.isDirty)throw new InvalidOperationException("Temporary forest review dirtied map.");
        File.WriteAllText(Output+"/forest-depth-review-success.txt",DateTime.Now.ToString("s")+"\nSaved=True\nTemporaryOnly=True\nClusters="+clusters+"\nSectors="+Sectors+"\nNewTextures=0");
    }
    // Relative GPU/CPU cost including a synchronous readback; this is not live Game-view FPS.
    static void Benchmark(Scene scene,GameObject candidate){
        var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).Single(c=>c.name=="ParkingLot_PreviewCamera");
        var oldTarget=camera.targetTexture;var active=RenderTexture.active;var position=camera.transform.position;var rotation=camera.transform.rotation;var fov=camera.fieldOfView;bool ortho=camera.orthographic;
        RenderTexture target=null;Texture2D pixel=null;var report=new System.Text.StringBuilder(DateTime.Now.ToString("s")+"\nMode=OfflineSynchronousRender\nSize=1920x1080\nSamplesPerStatePerView=12\n");
        try{
            target=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);pixel=new Texture2D(1,1,TextureFormat.RGB24,false);
            camera.targetTexture=target;camera.orthographic=false;
            for(int view=0;view<2;view++){
                camera.transform.position=view==0?new Vector3(19,1.65f,-10):new Vector3(31,25,-34);camera.transform.LookAt(view==0?new Vector3(-8,1.2f,-5):new Vector3(0,1,2));camera.fieldOfView=view==0?50:52;
                var baseline=new List<double>();var layered=new List<double>();
                for(int i=0;i<32;i++){
                    bool on=(i%4==1||i%4==2);candidate.SetActive(on);var watch=System.Diagnostics.Stopwatch.StartNew();
                    camera.Render();RenderTexture.active=target;pixel.ReadPixels(new Rect(960,540,1,1),0,0,false);watch.Stop();
                    if(i>=8)(on?layered:baseline).Add(watch.Elapsed.TotalMilliseconds);
                }
                baseline.Sort();layered.Sort();double before=(baseline[5]+baseline[6])*.5,after=(layered[5]+layered[6])*.5;
                report.AppendLine((view==0?"Eye":"Overview")+"BaselineMedianMs="+before.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
                report.AppendLine((view==0?"Eye":"Overview")+"CandidateMedianMs="+after.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
                report.AppendLine((view==0?"Eye":"Overview")+"DifferenceMs="+(after-before).ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
            }
        }finally{candidate.SetActive(true);camera.targetTexture=oldTarget;camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;camera.orthographic=ortho;RenderTexture.active=active;if(target!=null)RenderTexture.ReleaseTemporary(target);if(pixel!=null)UnityEngine.Object.DestroyImmediate(pixel);}
        File.WriteAllText(Output+"/forest-depth-cost.txt",report.ToString());
    }
    public static void Build(Scene scene){
        Check(scene);if(scene.GetRootGameObjects().Any(g=>g.name==Group)){Validate(scene);return;}
        var meshes=Generate(out int clusters);
        if(!AssetDatabase.IsValidFolder(MeshFolder))AssetDatabase.CreateFolder(Root+"/Meshes","ForestDepth");
        for(int i=0;i<meshes.Length;i++)meshes[i]=ParkingLotMeshAsset.Save(meshes[i],MeshFolder+"/Forest_sector_"+i.ToString("00")+".asset");
        var mat=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);if(mat==null){mat=MakeMaterial();AssetDatabase.CreateAsset(mat,MaterialPath);AssetDatabase.SaveAssetIfDirty(mat);}
        Create(scene,meshes,mat,false);
        File.WriteAllText(Output+"/forest-depth-audit.txt",Validate(scene)+"\nClusters="+clusters+"\nOriginalTerrainModified=False\nNewTextures=0");
    }
    public static string Validate(Scene scene){
        Check(scene);var root=scene.GetRootGameObjects().Single(g=>g.name==Group);var filters=root.GetComponentsInChildren<MeshFilter>(true);
        if(filters.Length!=Sectors||root.transform.localToWorldMatrix!=Matrix4x4.identity)throw new InvalidOperationException("Unexpected forest sector layout.");
        long triangles=0;float nearest=float.MaxValue;
        foreach(var f in filters){
            var mesh=f.sharedMesh;var r=f.GetComponent<MeshRenderer>();
            if(mesh==null||!AssetDatabase.GetAssetPath(mesh).StartsWith(MeshFolder+"/")||r.sharedMaterial==null||AssetDatabase.GetAssetPath(r.sharedMaterial)!=MaterialPath||r.sharedMaterial.shader!=CheckedShader())throw new InvalidOperationException("Invalid forest mesh/material.");
            if(f.transform.localToWorldMatrix!=Matrix4x4.identity||r.shadowCastingMode!=ShadowCastingMode.Off||mesh.colors.Length!=mesh.vertexCount||mesh.uv.Length!=mesh.vertexCount)throw new InvalidOperationException("Invalid forest streams/state.");
            foreach(var v in mesh.vertices){if(float.IsNaN(v.x)||float.IsInfinity(v.y))throw new InvalidOperationException("Non-finite forest vertex.");nearest=Mathf.Min(nearest,new Vector2(v.x,v.z).magnitude);}
            triangles+=mesh.triangles.Length/3;
        }
        if(nearest<150||triangles<5000||triangles>30000)throw new InvalidOperationException("Forest exceeds reviewed extent or budget.");
        return DateTime.Now.ToString("s")+"\nSaved="+!scene.isDirty+"\nSectors="+filters.Length+"\nTriangles="+triangles+"\nNearestVertexMetres="+nearest+"\nShaderErrors=0\nColliders="+root.GetComponentsInChildren<Collider>(true).Length;
    }
}
}
