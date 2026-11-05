using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace ParkingLotEnvironment.Editor {
// Deliberately isolated: never edits shared prefabs, project settings or another scene.
[InitializeOnLoad]
public static class ParkingLotBuilder {
    const string Root = "Assets/ParkingLotEnvironment";
    const string SourceModels = Root+"/SourceModels~";
    const string Target = "Assets/_Parkside/Scenes/ParklingLot.unity";
    static readonly string Output = ParkingLotEditorPaths.LegacyOutput;
    static System.Random rng;
    static Transform group;
    static Material asphalt, concrete, white, yellow, black, metal, glass, red, lamp, grass, bark, leaf, blue;
    static int meshId;
    static Font font;
    static Scene scene;
    static Material treeCard;
    static bool Consume(string name){string path=Output+"/"+name;try{if(!File.Exists(path))return false;File.Delete(path);return true;}catch(IOException){return false;}}
    static ParkingLotBuilder() { EditorApplication.update += Watch; }
    static void Watch() {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || ParkingLotCinematicCapture.IsCapturing) return;
        if(Consume("refresh-assets.request")){AssetDatabase.Refresh();return;}
        if(Consume("import-vehicle-lods.request")){try{foreach(var spec in new[]{"hyundai-tucson|4.53","hyundai-i30|4.25","hyundai-azera|4.895"}){var p=spec.Split('|');ParkingLotVehicleImporter.Import(SourceModels+"/"+p[0]+"-lod1/model.json",p[0]+"-lod1",float.Parse(p[1],System.Globalization.CultureInfo.InvariantCulture),p[0],SourceModels+"/"+p[0]+"/model.json");}File.WriteAllText(Output+"/vehicle-lods-import-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/vehicle-lods-import-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("reload-check.request")){var opened=SceneManager.GetSceneByPath(Target);if(opened.IsValid()&&opened.isDirty){File.WriteAllText(Output+"/reload-check.txt","Skipped: unsaved scene edits");}else{EditorSceneManager.OpenScene(Target,OpenSceneMode.Single);File.WriteAllText(Output+"/reload-check.txt","Reloaded saved ParkingLot scene successfully");}}
        if(Consume("materials.request")) {try{foreach(string key in new[]{"hyundai-tucson","hyundai-i30","hyundai-azera","generico-caminhao"})ParkingLotVehicleImporter.RefreshMaterials(SourceModels+"/"+key+"/model.json",key);File.WriteAllText(Output+"/materials-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/materials-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("bake-reflections.request")) {try{BakeReflections();File.WriteAllText(Output+"/bake-reflections-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/bake-reflections-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("apply-backcourts.request")){try{var saved=SceneManager.GetSceneByPath(Target);if(!saved.IsValid()||!saved.isLoaded||saved.isDirty)throw new InvalidOperationException("Open the saved map before applying backcourts.");ParkingLotBackcourts.Build(saved);EditorSceneManager.MarkSceneDirty(saved);EditorSceneManager.SaveScene(saved,Target);File.WriteAllText(Output+"/backcourts-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/backcourts-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("apply-shop-finish.request")){try{var saved=SceneManager.GetSceneByPath(Target);if(!saved.IsValid()||!saved.isLoaded||saved.isDirty)throw new InvalidOperationException("Open the saved map before applying shop finish.");ParkingLotShopSideFinish.Build(saved);EditorSceneManager.MarkSceneDirty(saved);EditorSceneManager.SaveScene(saved,Target);File.WriteAllText(Output+"/shop-finish-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/shop-finish-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("apply-leaf-finish.request")){try{var saved=SceneManager.GetSceneByPath(Target);if(!saved.IsValid()||!saved.isLoaded||saved.isDirty)throw new InvalidOperationException("Open the saved map before applying leaf finish.");ParkingLotTreeLeafFinish.Build(saved);EditorSceneManager.MarkSceneDirty(saved);EditorSceneManager.SaveScene(saved,Target);File.WriteAllText(Output+"/leaf-finish-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/leaf-finish-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("apply-footpath-finish.request")){try{var saved=SceneManager.GetSceneByPath(Target);if(!saved.IsValid()||!saved.isLoaded||saved.isDirty)throw new InvalidOperationException("Open the saved map before applying footpath finish.");ParkingLotFootpathFinish.Build(saved);EditorSceneManager.MarkSceneDirty(saved);EditorSceneManager.SaveScene(saved,Target);File.WriteAllText(Output+"/footpath-finish-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/footpath-finish-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("apply-tucson-glazing.request")){try{var saved=SceneManager.GetSceneByPath(Target);if(!saved.IsValid()||!saved.isLoaded||saved.isDirty)throw new InvalidOperationException("Open the saved map before applying Tucson glazing.");ParkingLotTucsonGlazingFinish.Build(saved);if(!EditorSceneManager.SaveScene(saved,Target))throw new IOException("Could not save the map.");File.WriteAllText(Output+"/tucson-glazing-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/tucson-glazing-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("apply-tucson-door.request")){try{var saved=SceneManager.GetSceneByPath(Target);if(!saved.IsValid()||!saved.isLoaded||saved.isDirty)throw new InvalidOperationException("Open the saved map before applying Tucson door refinement.");ParkingLotTucsonDoorFinish.Build(saved);if(!EditorSceneManager.SaveScene(saved,Target))throw new IOException("Could not save the map.");File.WriteAllText(Output+"/tucson-door-success.txt",DateTime.Now.ToString("s")+"\n"+ParkingLotTucsonDoorFinish.Validate(saved));}catch(Exception e){File.WriteAllText(Output+"/tucson-door-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("validate-tucson-door.request")){try{File.WriteAllText(Output+"/tucson-door-validation.txt",ParkingLotTucsonDoorFinish.Validate(SceneManager.GetSceneByPath(Target)));}catch(Exception e){File.WriteAllText(Output+"/tucson-door-validation-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("validate-tucson-glazing.request")){try{File.WriteAllText(Output+"/tucson-glazing-validation.txt",DateTime.Now.ToString("s")+"\n"+ParkingLotTucsonGlazingFinish.Validate(SceneManager.GetSceneByPath(Target)));}catch(Exception e){File.WriteAllText(Output+"/tucson-glazing-validation-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("fix-i30.request")) {try{ParkingLotVehicleImporter.Import(SourceModels+"/hyundai-i30/model.json","hyundai-i30",4.25f);File.WriteAllText(Output+"/fix-i30-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/fix-i30-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("import-tree.request")) {try{ParkingLotVehicleImporter.Import(SourceModels+"/tree-small-real/model.json","tree-small-real",0);File.WriteAllText(Output+"/tree-import-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/tree-import-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("import-far-tree.request")) {try{ParkingLotVehicleImporter.Import(SourceModels+"/tree-small-far/model.json","tree-small-far",0);File.WriteAllText(Output+"/far-tree-import-success.txt",DateTime.Now.ToString("s"));}catch(Exception e){File.WriteAllText(Output+"/far-tree-import-error.txt",e.ToString());Debug.LogException(e);}}
        if(Consume("import-vehicles.request")) {
            try {foreach(var spec in new[]{"hyundai-tucson|4.53","hyundai-i30|4.25","hyundai-azera|4.895","generico-caminhao|6.02"}) {var parts=spec.Split('|');ParkingLotVehicleImporter.Import(SourceModels+"/"+parts[0]+"/model.json",parts[0],float.Parse(parts[1],System.Globalization.CultureInfo.InvariantCulture));}File.WriteAllText(Output+"/import-success.txt",DateTime.Now.ToString("s"));}
            catch(Exception e){File.WriteAllText(Output+"/import-error.txt",e.ToString());Debug.LogException(e);}
        }
        if(Consume("capture.request")) {var camera=GameObject.Find("ParkingLot_PreviewCamera").GetComponent<Camera>();var p=camera.transform.position;var r=camera.transform.rotation;
            Render(camera,Output+"/parking-overview.png",new Vector3(31,25,-34),new Vector3(0,0,1));
            Render(camera,Output+"/parking-eye-level.png",new Vector3(19,1.65f,-10),new Vector3(-8,1.2f,-5));
            Render(camera,Output+"/parking-vehicle-detail.png",new Vector3(13,1.45f,-11),new Vector3(9,1,-3));
            camera.transform.position=p;camera.transform.rotation=r;File.WriteAllText(Output+"/capture-success.txt",DateTime.Now.ToString("s"));
        }
        if(Consume("dependencies.request")) {File.WriteAllLines(Output+"/scene-dependencies.txt",AssetDatabase.GetDependencies(Target,true));}
        if(Consume("validate.request")) {var s=SceneManager.GetSceneByPath(Target);int sedans=0,suvs=0,hatch=0,electrics=0,trucks=0,trees=0,missing=0,shaderErrors=0;long triangles=0;
            foreach(var root in s.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>()){if(t.name.StartsWith("Sedan_bay"))sedans++;if(t.name.StartsWith("SUV_bay"))suvs++;if(t.name.StartsWith("Hatchback_bay"))hatch++;if(t.name.StartsWith("Electric_crossover_bay"))electrics++;if(t.name.StartsWith("Light_truck_loading"))trucks++;if(t.name=="Photographic_3D_tree")trees++;var rr=t.GetComponent<Renderer>();if(rr!=null)foreach(var m in rr.sharedMaterials){if(m==null)missing++;else if(m.shader==null||!m.shader.isSupported||ShaderUtil.ShaderHasError(m.shader))shaderErrors++;}var mf=t.GetComponent<MeshFilter>();if(mf!=null&&mf.sharedMesh!=null)triangles+=mf.sharedMesh.triangles.Length/3;}
            File.WriteAllText(Output+"/validation.txt","Scene="+s.path+"\nSaved="+(!s.isDirty)+"\nSedans="+sedans+"\nSUVs="+suvs+"\nHatchbacks="+hatch+"\nElectricCrossovers="+electrics+"\nTrucks="+trucks+"\nTrees="+trees+"\nTriangles="+triangles+"\nMissingMaterials="+missing+"\nShaderErrors="+shaderErrors);
            foreach(var root in s.GetRootGameObjects())foreach(var preview in root.GetComponentsInChildren<ParkingLotCamera>()){
                var windBounds=preview.GetComponent<ParkingLotFoliageBounds>();
                File.AppendAllText(Output+"/validation.txt","\nFoliageWindStrength="+preview.foliageWindStrength+"\nFoliageWindPreviewOverride="+preview.foliageWindPreviewOverride+"\nFoliageBoundsRenderers="+(windBounds!=null?windBounds.RendererCount:0)+"\nRestoredWindGlobal="+Shader.GetGlobalFloat("_ParkingLotWindStrength"));
            }
            var details=new System.Text.StringBuilder();foreach(var root in s.GetRootGameObjects())foreach(var r in root.GetComponentsInChildren<MeshRenderer>())if(r.name=="Traffic_cone"||r.name=="Cone_reflective_band"){var mf=r.GetComponent<MeshFilter>();details.AppendLine(r.name+" enabled="+r.enabled+" bounds="+r.bounds+" mesh="+mf.sharedMesh.name+" triangles="+mf.sharedMesh.triangles.Length/3+" material="+r.sharedMaterial.name+" shader="+r.sharedMaterial.shader.name);}
            File.WriteAllText(Output+"/detail-geometry.txt",details.ToString());
        }
        if(Consume("inspect.request")) {var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/ReferenceAssets/tree_small_02.fbx");
            var report=new System.Text.StringBuilder();
            if(model!=null)foreach(var f in model.GetComponentsInChildren<MeshFilter>(true)){report.AppendLine(f.name+" vertices="+f.sharedMesh.vertexCount+" size="+f.sharedMesh.bounds.size+" scale="+f.transform.lossyScale);var renderer=f.GetComponent<Renderer>();if(renderer!=null)foreach(var m in renderer.sharedMaterials)report.AppendLine("  material="+(m?m.name:"null"));}
            File.WriteAllText(Output+"/tree-report.txt",report.ToString());
        }
        if(!Consume("build.request")) return;

        try { Build(); } catch(Exception e) { File.WriteAllText(Output+"/build-error.txt",e.ToString()); Debug.LogException(e); }
    }
    static float R(float a,float b) { return a+(float)rng.NextDouble()*(b-a); }
    static Color C(string hex) { ColorUtility.TryParseHtmlString("#"+hex,out var c); return c; }
    static Material Mat(string name,Color color,float smooth=0,float metallic=0) {
        string path=Root+"/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) { m=new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m,path); }
        if(name.StartsWith("Real_carpaint"))m.shader=Shader.Find("ParkingLot/LinearCarPaint");
        m.color=color; m.SetFloat("_Glossiness",smooth); m.SetFloat("_Metallic",metallic);m.enableInstancing=true;
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssetIfDirty(m); return m;
    }
    static void Group(string name) { group=new GameObject(name).transform; SceneManager.MoveGameObjectToScene(group.gameObject,scene); }
    static GameObject Primitive(string name,PrimitiveType shape,Vector3 pos,Vector3 scale,Material mat,Transform parent=null) {
        var o=GameObject.CreatePrimitive(shape); o.name=name; o.transform.SetParent(parent??group,false); o.transform.localPosition=pos; o.transform.localScale=scale;
        o.GetComponent<Renderer>().sharedMaterial=mat;if(name.StartsWith("Asphalt_")||name=="Subsoil"||name=="Context_ground_240m"||name=="External_access_road"||name=="Pedestrian_path")o.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;o.isStatic=true; return o;
    }
    static GameObject Box(string name,Vector3 p,Vector3 s,Material m,Transform parent=null) {bool paint=(m==white||m==yellow)&&p.y<.05f&&p.y>=0;if(paint){p.y=.0018f;s.y=.001f;}var go=Primitive(name,PrimitiveType.Cube,p,s,m,parent);if(paint){go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());}return go;}
    static GameObject Cylinder(string name,Vector3 p,float radius,float height,Material m,Transform parent=null) { return Primitive(name,PrimitiveType.Cylinder,p,new Vector3(radius*2,height/2,radius*2),m,parent); }
    static void Beam(string name,Vector3 a,Vector3 b,float radius,Material mat,Transform parent=null) {
        var o=Cylinder(name,(a+b)*.5f,radius,Vector3.Distance(a,b),mat,parent); o.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
    }
    static Mesh MeshAsset(string name,List<Vector3> v,List<int> t,List<Vector2> uv=null) {
        var m=new Mesh(); m.SetVertices(v); m.SetTriangles(t,0);
        // A complete vertex layout prevents missing procedural geometry on Iris Xe instancing.
        if(uv==null){uv=new List<Vector2>(v.Count);foreach(var p in v)uv.Add(new Vector2(p.x,p.z));}
        m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateTangents();m.RecalculateBounds();
        string path=Root+"/Meshes/"+(meshId++).ToString("D4")+"_"+name+".asset";
        m.name=Path.GetFileNameWithoutExtension(path);
        return ParkingLotMeshAsset.Save(m,path);
    }
    static GameObject MeshObject(string name,List<Vector3> v,List<int> t,Material material,Transform parent=null) {
        var o=new GameObject(name); o.transform.SetParent(parent??group,false); o.AddComponent<MeshFilter>().sharedMesh=MeshAsset(name,v,t); o.AddComponent<MeshRenderer>().sharedMaterial=material; o.isStatic=true; return o;
    }
    static void ConeSection(string name,Vector3 origin,float y0,float y1,float r0,float r1,Material material){var v=new List<Vector3>();var t=new List<int>();const int segments=24;for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;v.Add(origin+new Vector3(Mathf.Cos(a)*r0,y0,Mathf.Sin(a)*r0));v.Add(origin+new Vector3(Mathf.Cos(a)*r1,y1,Mathf.Sin(a)*r1));if(i<segments){int k=i*2;t.AddRange(new[]{k,k+1,k+2,k+1,k+3,k+2});}}MeshObject(name,v,t,material);}
    static void Quad(string name,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Material m,Transform parent=null) {
        MeshObject(name,new List<Vector3>{a,b,c,d,a,b,c,d},new List<int>{0,1,2,0,2,3,6,5,4,7,6,4},m,parent);
    }
    // A rounded, longitudinal vehicle shell. Each profile is z, half-width, lower height, upper height.
    static GameObject Loft(string name,Vector4[] profiles,Material material,Transform parent) {
        var verts=new List<Vector3>(); var tris=new List<int>();
        foreach(var p in profiles) {
            float w=p.y,lo=p.z,hi=p.w;
            verts.AddRange(new[]{new Vector3(-w*.82f,lo,p.x),new Vector3(w*.82f,lo,p.x),new Vector3(w,lo+.12f,p.x),new Vector3(w,hi-.12f,p.x),new Vector3(w*.84f,hi,p.x),new Vector3(-w*.84f,hi,p.x),new Vector3(-w,hi-.12f,p.x),new Vector3(-w,lo+.12f,p.x)});
        }
        for(int i=0;i<profiles.Length-1;i++) for(int j=0;j<8;j++) {int a=i*8+j,b=i*8+(j+1)%8,c=b+8,d=a+8;tris.AddRange(new[]{a,b,c,a,c,d});}
        for(int j=1;j<7;j++) { tris.AddRange(new[]{0,j+1,j}); int k=(profiles.Length-1)*8;tris.AddRange(new[]{k,k+j,k+j+1}); }
        return MeshObject(name,verts,tris,material,parent);
    }
    static void Label(string content,Vector3 p,float size,Color color,Quaternion rot,Transform parent=null) {
        var o=new GameObject("Sign_"+content.Replace('\n','_'));o.transform.SetParent(parent??group,false);o.transform.localPosition=p;o.transform.localRotation=rot;
        var t=o.AddComponent<TextMesh>();t.text=content;t.font=font;t.fontSize=80;t.characterSize=size/8f;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
        o.GetComponent<MeshRenderer>().sharedMaterial=font.material;
    }
    static void GroundText(string text,float x,float z,float size,Color c) {Label(text,new Vector3(x,.025f,z),size,c,Quaternion.Euler(90,0,0));}
    static void Line(Vector3 a,Vector3 b,float width,Material m) {var o=Box("Road_paint",(a+b)*.5f,new Vector3(width,.009f,Vector3.Distance(a,b)),m);o.transform.rotation=Quaternion.LookRotation(b-a);}
    static Texture2D Texture(string name,int size,Func<int,int,Color> pixel,bool normal=false) {
        var t=new Texture2D(size,size,TextureFormat.RGB24,true);var p=new Color[size*size]; for(int y=0;y<size;y++)for(int x=0;x<size;x++)p[y*size+x]=pixel(x,y);t.SetPixels(p);t.Apply();
        string path=Root+"/Textures/"+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.wrapMode=TextureWrapMode.Repeat;imp.anisoLevel=8;imp.maxTextureSize=size;imp.textureCompression=TextureImporterCompression.CompressedHQ;if(normal){imp.textureType=TextureImporterType.NormalMap;imp.convertToNormalmap=true;imp.heightmapScale=.07f;}imp.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    [MenuItem("ParkingLot/Build isolated outdoor parking lot")]
    public static void Build() {
        ParkingLotEditorPaths.EnsureOutputDirectories();rng=new System.Random(1172);meshId=0;ParkingLotVehicleBatching.Reset();ParkingLotPaintNormals.Reset();
        foreach(string dir in new[]{"Materials","Textures","Meshes","Fonts"})Directory.CreateDirectory(Root+"/"+dir);
        AssetDatabase.Refresh();
        for(int i=0;i<SceneManager.sceneCount;i++) {var s=SceneManager.GetSceneAt(i); if(s.isDirty) throw new Exception("An open scene has unsaved edits; save it before building. No scene was modified: "+s.path);}
        scene=SceneManager.GetSceneByPath(Target);if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(Target,OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        for(int i=SceneManager.sceneCount-1;i>=0;i--) {var s=SceneManager.GetSceneAt(i);if(s!=scene)EditorSceneManager.CloseScene(s,true);}
        foreach(var go in scene.GetRootGameObjects())UnityEngine.Object.DestroyImmediate(go);
        font=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/ParkingSigns.ttf");if(font==null){AssetDatabase.ImportAsset(Root+"/Fonts/ParkingSigns.ttf");font=AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/ParkingSigns.ttf");}
        asphalt=Mat("Weathered_asphalt",C("606363"),.12f);concrete=Mat("Concrete",C("B6B2A6"),.12f);white=Mat("Faded_road_white",C("C6C6B8"),.08f);yellow=Mat("Safety_yellow",C("D6AA38"),.1f);black=Mat("Rubber",C("161B20"),.25f);
        metal=Mat("Galvanized_metal",C("80898D"),.65f,.78f);glass=Mat("Automotive_glass",C("273C49"),.95f,.45f);red=Mat("Rear_lenses",C("951D18"),.8f);lamp=Mat("Headlight_lenses",C("D9E3D9"),.85f);grass=Mat("Dry_grass",C("656A3B"));bark=Mat("Tree_bark",C("504236"));leaf=Mat("Foliage",C("394D27"));blue=Mat("Blue_sign",C("174B70"),.2f);
        var tex=Texture("asphalt_grain",512,(x,y)=>{float n=Mathf.PerlinNoise(x/28f,y/28f)*.07f+R(-.09f,.09f);float stone=rng.NextDouble()>.96?.16f:0;return new Color(.36f+n+stone,.365f+n+stone,.36f+n+stone);});asphalt.mainTexture=tex;asphalt.mainTextureScale=new Vector2(22,18);EditorUtility.SetDirty(asphalt);AssetDatabase.SaveAssetIfDirty(asphalt);
        var ct=Texture("concrete_grain",256,(x,y)=>{float n=Mathf.PerlinNoise(x/15f,y/15f)*.09f+R(-.04f,.04f);return new Color(.76f+n,.75f+n,.72f+n);});concrete.mainTexture=ct;EditorUtility.SetDirty(concrete);AssetDatabase.SaveAssetIfDirty(concrete);
        var photoAsphalt=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ReferenceAssets/asphalt_diff.jpg");
        if(photoAsphalt!=null){asphalt.color=new Color(.72f,.72f,.72f);asphalt.mainTexture=photoAsphalt;asphalt.mainTextureScale=new Vector2(13,11.5f);string np=Root+"/ReferenceAssets/asphalt_nor_gl.jpg";var ni=AssetImporter.GetAtPath(np) as TextureImporter;if(ni!=null){ni.textureType=TextureImporterType.NormalMap;ni.sRGBTexture=false;ni.SaveAndReimport();asphalt.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(np));asphalt.EnableKeyword("_NORMALMAP");asphalt.SetFloat("_BumpScale",.55f);}EditorUtility.SetDirty(asphalt);AssetDatabase.SaveAssetIfDirty(asphalt);}
        var cleanAsphalt=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/RealismAssets/clean_asphalt_diff_2k.jpg");
        if(cleanAsphalt!=null){asphalt.mainTexture=cleanAsphalt;asphalt.color=C("7F817D");asphalt.mainTextureScale=new Vector2(26,23);string np=Root+"/RealismAssets/clean_asphalt_nor_gl_2k.jpg";var ni=(TextureImporter)AssetImporter.GetAtPath(np);ni.textureType=TextureImporterType.NormalMap;ni.sRGBTexture=false;ni.SaveAndReimport();asphalt.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(np));asphalt.SetFloat("_BumpScale",.4f);EditorUtility.SetDirty(asphalt);AssetDatabase.SaveAssetIfDirty(asphalt);}
        asphalt.shader=Shader.Find("ParkingLot/RoadSurface");asphalt.SetTexture("_WeatherTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ReferenceAssets/asphalt_diff.jpg"));asphalt.SetTexture("_RoughnessTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/RealismAssets/clean_asphalt_rough_2k.jpg"));asphalt.SetFloat("_BumpScale",.65f);EditorUtility.SetDirty(asphalt);AssetDatabase.SaveAssetIfDirty(asphalt);
        white.shader=Shader.Find("ParkingLot/RoadPaint");white.color=C("B0AFA3");yellow.shader=Shader.Find("ParkingLot/RoadPaint");EditorUtility.SetDirty(white);EditorUtility.SetDirty(yellow);AssetDatabase.SaveAssetIfDirty(white);AssetDatabase.SaveAssetIfDirty(yellow);
        Ground(); Parking(); Vehicles(); Surroundings(); ParkingLotKoreanContext.Build(scene,font); ParkingLotShopSideFinish.Build(scene); ParkingLotNeighborhoodInfill.Build(scene); ParkingLotRoofContact.Build(scene); ParkingLotNeighborhoodGround.Build(scene); ParkingLotBackcourts.Build(scene); ParkingLotSiteDetails.Build(scene,font); ParkingLotGroundWeathering.Build(scene); LightingAndCamera();
        ParkingLotTreeLeafFinish.Build(scene);
        ParkingLotFootpathFinish.Build(scene);
        ParkingLotTucsonGlazingFinish.Build(scene);
        ParkingLotTucsonDoorFinish.Build(scene);
        ParkingLotUtilityNetwork.Build(scene);
        ParkingLotNeighborhoodPavingFinish.Build(scene);
        ParkingLotAwningFinish.Build(scene);
        ParkingLotFacadeVariation.Build(scene);
        ParkingLotWholeLookFinish.Build(scene);
        ParkingLotSkylineVariation.Build(scene);
        ParkingLotPlantingFinish.Build(scene);
        ParkingLotForestDepthFinish.Build(scene);
        ParkingLotWindshieldFinish.Build(scene);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,Target);
        File.WriteAllText(Output+"/build-success.txt","Saved only "+Target+"\nNew assets confined to "+Root+"\n"+DateTime.Now.ToString("s"));
        File.WriteAllText(Output+"/vehicle-batching.txt","Vehicle renderers before="+ParkingLotVehicleBatching.Before+"\nAfter="+ParkingLotVehicleBatching.After+"\nOnly identical material properties merged; geometry, UVs and normals retained.");
        Debug.Log("PARKINGLOT_BUILD_SUCCESS "+Target);
    }
    static void Ground() {
        Group("01_Ground_and_drainage");
        var contextRoad=Mat("Surrounding_access_surface",C("8C8E87"),.08f);contextRoad.mainTexture=asphalt.mainTexture;contextRoad.mainTextureScale=new Vector2(120,120);EditorUtility.SetDirty(contextRoad);AssetDatabase.SaveAssetIfDirty(contextRoad);
        Box("Context_ground_240m",new Vector3(0,-.175f,0),new Vector3(1600,.3f,1600),contextRoad);
        Box("Subsoil",new Vector3(0,-.4f,0),new Vector3(60,.6f,54),grass);
        Box("Asphalt_52m_x_46m",new Vector3(0,-.12f,0),new Vector3(52,.24f,46),asphalt);
        var repair=Mat("Asphalt_repairs",C("666B6C"),.08f);
        repair.mainTexture=asphalt.mainTexture;repair.mainTextureScale=new Vector2(.7f,.7f);EditorUtility.SetDirty(repair);AssetDatabase.SaveAssetIfDirty(repair);
        var crack=Mat("Asphalt_hairline",C("292E2E"));
        // Weathering is shaded in the surface material; raised crack geometry would catch tires and cast false shadows.
        for(int side=-1;side<=1;side+=2) {
            Box("Perimeter_footpath",new Vector3(side*27,.03f,0),new Vector3(2,.24f,48),concrete);
            for(int z=-23;z<24;z++) Box("Individual_curb",new Vector3(side*25.9f,.12f,z),new Vector3(.22f,.24f,.97f),concrete);
            for(int z=-18;z<=18;z+=9){Box("Storm_drain_frame",new Vector3(side*25.3f,.012f,z),new Vector3(.55f,.025f,.85f),black);for(int k=0;k<8;k++)Box("Drain_grating",new Vector3(side*25.3f,.03f,z-.36f+k*.1f),new Vector3(.5f,.02f,.025f),metal);}
        }
        for(int side=-1;side<=1;side+=2) for(int x=-25;x<=25;x++) {if(side==-1 && x>16)continue;Box("End_curb",new Vector3(x,.12f,side*23),new Vector3(.97f,.24f,.22f),concrete);}
        Box("External_access_road",new Vector3(0,-.09f,-29),new Vector3(140,.18f,10),asphalt);
        // The Korean street context adds its continuous double centre line.
    }
    static void Parking() {
        Group("02_Parking_markings_40_spaces");
        float[] rows={-17,-3,3,17};int number=1;
        foreach(float z in rows) {
            for(int i=0;i<=10;i++){float x=-14+i*2.8f;Box("Bay_separator",new Vector3(x,.014f,z),new Vector3(.1f,.015f,5.25f),white);}
            float rear=z+(z==-17||z==3?-2.6f:2.6f);Box("Bay_rear_line",new Vector3(0,.014f,rear),new Vector3(28,.015f,.1f),white);
            for(int i=0;i<10;i++){float x=-12.6f+i*2.8f;float stopZ=rear+(rear>z?-.65f:.65f);Box("Wheel_stop",new Vector3(x,.09f,stopZ),new Vector3(1.65f,.18f,.22f),black);for(int s=-1;s<=1;s+=2)Box("Wheel_stop_reflector",new Vector3(x+s*.48f,.184f,stopZ),new Vector3(.33f,.012f,.22f),yellow);GroundText((number++).ToString("D2"),x,z+(rear>z?-1.95f:1.95f),.32f,white.color);}
        }
        // A pedestrian route with hatched buffers at the western edge.
        var walk=Mat("Pedestrian_terracotta",C("895B4D"));Box("Pedestrian_path",new Vector3(-21,.007f,0),new Vector3(2,.012f,43),walk);
        for(int z=-21;z<=21;z+=2)Box("Walk_edge",new Vector3(-19.85f,.016f,z),new Vector3(.1f,.01f,1),white);
        for(int i=0;i<7;i++)Box("Zebra_crossing",new Vector3(18.5f+i*.9f,.017f,-21),new Vector3(.43f,.012f,2.4f),white);
        foreach(float z in new[]{-10f,10f})foreach(float x in new[]{-9f,8f}) {
            float dir=z<0?1:-1;Line(new Vector3(x-1.2f*dir,.02f,z),new Vector3(x+1.2f*dir,.02f,z),.22f,white);
            Line(new Vector3(x+1.2f*dir,.02f,z),new Vector3(x+.35f*dir,.02f,z+.65f),.22f,white);Line(new Vector3(x+1.2f*dir,.02f,z),new Vector3(x+.35f*dir,.02f,z-.65f),.22f,white);
        }
        GroundText("서행",20,-15,.7f,white.color);GroundText("10",20,9,.8f,white.color);
        Group("03_Entry_and_signs");
        foreach(float x in new[]{17.4f,24.5f}){Cylinder("Bollard",new Vector3(x,.4f,-21),.11f,.8f,black);Cylinder("Reflective_band",new Vector3(x,.61f,-21),.115f,.14f,yellow);}
        Box("Parking_sign",new Vector3(25,2.7f,-19.8f),new Vector3(2.2f,2.25f,.12f),blue);Cylinder("Sign_post",new Vector3(25,1.2f,-19.8f),.07f,2.4f,metal);
        Label("P",new Vector3(25,3.1f,-19.88f),1.1f,Color.white,Quaternion.identity);Label("공영주차장",new Vector3(25,2.15f,-19.88f),.21f,Color.white,Quaternion.identity);
        Box("Payment_kiosk",new Vector3(23.8f,.8f,-18.1f),new Vector3(.65f,1.6f,.52f),metal);Box("Kiosk_screen",new Vector3(23.8f,1.12f,-18.37f),new Vector3(.44f,.3f,.02f),glass);Label("정산",new Vector3(23.8f,1.49f,-18.39f),.12f,Color.white,Quaternion.identity);
        for(int i=0;i<4;i++){float z=-17.5f+i*.8f;Box("Cone_rubber_base",new Vector3(16.1f,.025f,z),new Vector3(.38f,.05f,.38f),black);var orange=Mat("Traffic_orange",C("ED5418"),.16f);var at=new Vector3(16.1f,.05f,z);ConeSection("Traffic_cone",at,0,.48f,.145f,.022f,orange);ConeSection("Cone_reflective_band",at,.2f,.31f,.0948f,.0666f,white);}
    }
    static void Vehicles() {
        Group("04_Real_Hyundai_vehicles_and_delivery_trucks");
        Color[] colors={C("E6E6E0"),C("A4A9AD"),C("4F5459"),C("16191D"),C("233B50")};
        int[] palette={0,2,1,0,3,4,0,1,3,0,2,3,1,0,4,2,0,3,1,0,2,3,1,0,3,2};
        int[] models={0,1,0,2,1,3,2,1,2,0,2,1,0,2,1,3,1,2,0,1,2,0,1,3,2,2};
        int[] slots={0,1,3,4,6,7,9,10,12,13,15,17,18,20,21,23,25,26,28,29,30,32,34,35,37,39};
        float[] rows={-17,-3,3,17};
        for(int v=0;v<slots.Length;v++) {int slot=slots[v],row=slot/10,col=slot%10;int type=models[v];string key=type==0?"hyundai-tucson":type==1?"hyundai-azera":type==3?"hyundai-ioniq5-openx":"hyundai-i30";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RealVehicles/"+key+"/"+key+".prefab");if(prefab==null)throw new Exception("Import licensed real vehicle first: "+key);
            var car=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);car.name=(type==0?"SUV":type==1?"Sedan":type==3?"Electric_crossover":"Hatchback")+"_bay_"+(slot+1)+"_"+key;car.transform.SetParent(group,false);car.transform.position=new Vector3(-12.6f+col*2.8f+R(-.10f,.10f),0,rows[row]+R(-.10f,.10f));car.transform.rotation=Quaternion.Euler(0,(row==0||row==2?0:180)+R(-1.3f,1.3f),0);
            foreach(var renderer in car.GetComponentsInChildren<Renderer>()){var original=renderer.sharedMaterial;string n=original.name.ToLowerInvariant();bool paint=n.Contains("varnish")||n.EndsWith("_paint")||(type==3&&n.EndsWith("_body"))||(type==1&&Mathf.Abs(original.color.r-Mathf.LinearToGammaSpace(.749f))<.003f&&Mathf.Abs(original.color.r-original.color.g)<.003f);
                if(paint){int ci=palette[v];float[] metallic={.05f,.58f,.36f,.10f,.28f};float[] gloss={.88f,.86f,.86f,.89f,.87f};var paintMaterial=Mat("Real_carpaint_"+ci,colors[ci],gloss[ci],metallic[ci]);renderer.sharedMaterial=paintMaterial;}}
            if(key=="hyundai-tucson")foreach(var renderer in car.GetComponentsInChildren<MeshRenderer>())if(renderer.sharedMaterial.name.StartsWith("Real_carpaint"))ParkingLotPaintNormals.Refine(renderer.GetComponent<MeshFilter>());
            if(key=="hyundai-i30")foreach(var renderer in car.GetComponentsInChildren<MeshRenderer>())if(renderer.sharedMaterial.name.StartsWith("Real_carpaint"))ParkingLotPaintNormals.Refine(renderer.GetComponent<MeshFilter>(),"i30");
            if(type==1)ParkingLotAzeraFinish.Apply(car);
            if(key=="hyundai-i30")ParkingLotI30Finish.Apply(car);
            if(type==3){PrefabUtility.UnpackPrefabInstance(car,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);ParkingLotIoniqFinish.Apply(car);ParkingLotVehicleDetails.Add(car,key,v,font);ParkingLotVehicleBatching.Optimize(car,key);}
            else {ParkingLotVehicleBatching.Optimize(car,key);ParkingLotVehicleLod.Add(car,key);ParkingLotVehicleDetails.Add(car,key,v,font);}
        }
        var truckPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RealVehicles/generico-caminhao/generico-caminhao.prefab");
        if(truckPrefab!=null)for(int k=0;k<2;k++){var truck=(GameObject)PrefabUtility.InstantiatePrefab(truckPrefab,scene);truck.name="Light_truck_loading_"+(k+1);truck.transform.SetParent(group,false);truck.transform.position=new Vector3(-17.55f,0,k==0?-15.5f:15.5f);truck.transform.rotation=Quaternion.Euler(0,k==0?0:180,0);ParkingLotVehicleDetails.Add(truck,"generico-caminhao",k,font);}
    }
    static void Car(Transform p,int type,Material paint,int index) {
        bool suv=type==1,truck=type==2;float width=suv?.96f:.9f,front=truck?2.35f:2.28f,back=-2.25f;
        Box("Underbody",new Vector3(0,.32f,0),new Vector3(1.5f,.18f,4.05f),black,p);
        if(!truck){
            Loft("Painted_body",new[]{new Vector4(back,.72f,.43f,.77f),new Vector4(-2.03f,width*.96f,.4f,.94f),new Vector4(-1.5f,width,.42f,suv?1.05f:.97f),new Vector4(.9f,width,.43f,.99f),new Vector4(1.65f,width*.96f,.43f,.91f),new Vector4(front,.73f,.48f,.75f)},paint,p);
            float roof=suv?1.72f:1.43f;float r0=suv?-1.45f:-.88f,r1=suv?.65f:.48f;
            Loft("Cabin_glass",new[]{new Vector4(suv?-1.8f:-1.55f,.78f,.9f,1.03f),new Vector4(r0,.7f,.93f,roof),new Vector4(r1,.7f,.93f,roof),new Vector4(1.2f,.78f,.92f,1.04f)},glass,p);
            Box("Roof_panel",new Vector3(0,roof+.012f,(r0+r1)*.5f),new Vector3(1.28f,.045f,r1-r0+.05f),paint,p);
            foreach(int side in new[]{-1,1}) {
                Beam("A_pillar",new Vector3(side*.76f,1.01f,1.17f),new Vector3(side*.67f,roof,.48f),.035f,paint,p);
                Beam("C_pillar",new Vector3(side*.76f,1.01f,suv?-1.77f:-1.5f),new Vector3(side*.67f,roof,r0),.05f,paint,p);
                Beam("B_pillar",new Vector3(side*.78f,.95f,-.28f),new Vector3(side*.71f,roof,-.28f),.045f,black,p);
                Box("Window_sill",new Vector3(side*.85f,.98f,-.15f),new Vector3(.035f,.025f,2.4f),metal,p);
                foreach(float z in new[]{-.95f,.6f}){Box("Door_handle",new Vector3(side*(width+.008f),.87f,z),new Vector3(.04f,.045f,.17f),metal,p);Box("Door_seam",new Vector3(side*(width+.012f),.7f,z-.35f),new Vector3(.008f,.36f,.012f),black,p);}
                Box("Mirror_stalk",new Vector3(side*.96f,1.04f,.81f),new Vector3(.18f,.06f,.08f),black,p);var mirror=Primitive("Mirror",PrimitiveType.Sphere,new Vector3(side*1.065f,1.07f,.82f),new Vector3(.23f,.15f,.3f),paint,p);UnityEngine.Object.DestroyImmediate(mirror.GetComponent<Collider>());
                if(suv)Beam("Roof_rail",new Vector3(side*.55f,roof+.09f,-1.3f),new Vector3(side*.55f,roof+.09f,.6f),.028f,metal,p);
            }
            Box("Front_grille",new Vector3(0,.6f,front+.012f),new Vector3(.94f,.2f,.035f),black,p);
            for(int k=0;k<4;k++)Box("Grille_slats",new Vector3(0,.53f+k*.045f,front+.035f),new Vector3(.9f,.012f,.018f),metal,p);
            foreach(int side in new[]{-1,1}){Box("Headlamp",new Vector3(side*.56f,.78f,front-.02f),new Vector3(.38f,.12f,.07f),lamp,p);Box("Tail_lamp",new Vector3(side*.58f,.81f,back-.015f),new Vector3(.32f,.13f,.055f),red,p);}
            Box("Rear_bumper",new Vector3(0,.48f,back-.012f),new Vector3(1.45f,.12f,.08f),black,p);
        } else {
            Loft("Truck_cab",new[]{new Vector4(.35f,.84f,.43f,1.5f),new Vector4(.6f,.84f,.43f,1.94f),new Vector4(1.55f,.84f,.43f,1.94f),new Vector4(front,.8f,.43f,1.35f)},paint,p);
            Quad("Truck_windshield",new Vector3(-.71f,1.45f,2.22f),new Vector3(.71f,1.45f,2.22f),new Vector3(.71f,1.82f,1.7f),new Vector3(-.71f,1.82f,1.7f),glass,p);
            foreach(int side in new[]{-1,1}){Quad("Truck_side_window",new Vector3(side*.855f,1.27f,.69f),new Vector3(side*.855f,1.27f,1.72f),new Vector3(side*.855f,1.79f,1.48f),new Vector3(side*.855f,1.79f,.69f),glass,p);Box("Truck_mirror",new Vector3(side*1.02f,1.45f,1.6f),new Vector3(.15f,.27f,.17f),black,p);Box("Truck_headlamp",new Vector3(side*.57f,.78f,front+.02f),new Vector3(.3f,.18f,.04f),lamp,p);}
            var bed=Mat("Truck_bed_blue",C("445D66"),.34f,.3f);Box("Truck_bed_floor",new Vector3(0,.75f,-1),new Vector3(1.82f,.12f,2.6f),bed,p);
            foreach(int side in new[]{-1,1}) {Box("Drop_side",new Vector3(side*.9f,.99f,-1),new Vector3(.06f,.44f,2.65f),bed,p);for(int k=0;k<5;k++)Box("Bed_rib",new Vector3(side*.94f,.99f,-2.1f+k*.55f),new Vector3(.022f,.38f,.04f),metal,p);}
            Box("Tailgate",new Vector3(0,.99f,-2.33f),new Vector3(1.85f,.44f,.06f),bed,p);foreach(int s in new[]{-1,1})Box("Truck_tail_lamp",new Vector3(s*.6f,.55f,-2.34f),new Vector3(.22f,.12f,.04f),red,p);
        }
        foreach(int side in new[]{-1,1})foreach(float z in new[]{-1.4f,1.43f}) {
            float radius=suv?.38f:.34f;var tire=Cylinder("Tire",new Vector3(side*(width-.015f),radius,z),radius,.23f,black,p);tire.transform.localRotation=Quaternion.Euler(0,0,90);
            var rim=Cylinder("Alloy_rim",new Vector3(side*(width+.11f),radius,z),radius*.68f,.018f,metal,p);rim.transform.localRotation=Quaternion.Euler(0,0,90);
            var hub=Cylinder("Hub",new Vector3(side*(width+.125f),radius,z),radius*.23f,.025f,black,p);hub.transform.localRotation=Quaternion.Euler(0,0,90);
            for(int k=0;k<5;k++){float a=k*Mathf.PI*2/5;var sp=Box("Wheel_spoke",new Vector3(side*(width+.124f),radius+Mathf.Cos(a)*radius*.39f,z+Mathf.Sin(a)*radius*.39f),new Vector3(.02f,radius*.46f,.045f),metal,p);sp.transform.localRotation=Quaternion.Euler(-a*Mathf.Rad2Deg,0,0);}
        }
        foreach(int s in new[]{-1,1}){float z=s>0?front+.065f:back-.09f;Box("Registration_plate",new Vector3(0,.58f,z),new Vector3(.43f,.105f,.015f),white,p);Label((21+index)+"가 "+(3128+index*137),new Vector3(0,.58f,z+s*.011f),.029f,C("202626"),Quaternion.Euler(0,s>0?0:180,0),p);}
        // Contact shadows underneath parked cars; confined to this scene's geometry.
        // Actual camera-local realtime shadows are used instead of opaque shadow rectangles.
        var collider=p.gameObject.AddComponent<BoxCollider>();collider.center=new Vector3(0,truck?1:.8f,0);collider.size=new Vector3(truck?1.9f:2.05f,truck?2:1.65f,4.8f);
        foreach(var c in p.GetComponentsInChildren<Collider>())if(c!=collider)UnityEngine.Object.DestroyImmediate(c);
    }
    static void Surroundings() {
        Group("05_Planting_and_lighting_columns");
        foreach(float x in new[]{-24f,24f})foreach(float z in new[]{-12f,12f}) {
            Cylinder("Lamp_foundation",new Vector3(x,.12f,z),.3f,.24f,concrete);Cylinder("Lamp_column",new Vector3(x,3.4f,z),.065f,6.8f,metal);Beam("Lamp_arm",new Vector3(x,6.6f,z),new Vector3(x-Mathf.Sign(x)*1.2f,6.8f,z),.04f,metal);Box("LED_housing",new Vector3(x-Mathf.Sign(x)*1.2f,6.77f,z),new Vector3(.6f,.1f,.26f),black);Box("LED_diffuser",new Vector3(x-Mathf.Sign(x)*1.2f,6.71f,z),new Vector3(.5f,.015f,.21f),lamp);
        }
        Group("06_Realistic_perimeter_planting");ParkingLotRealismDetails.Build(group);
        var realTree=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RealVehicles/tree-small-real/tree-small-real.prefab");
        if(realTree!=null){var treePositions=new List<Vector3>{new Vector3(-29,0,-13),new Vector3(-29,0,7),new Vector3(29,0,-10),new Vector3(29,0,11),new Vector3(-17,0,27),new Vector3(9,0,27)};for(int i=0;i<11;i++)treePositions.Add(new Vector3(-47+i*9,0,R(29,31)));foreach(var pos in treePositions){float yaw=R(0,360),scale=R(1.25f,1.8f);if(ParkingLotNeighborhoodInfill.Occupied(pos,3))continue;var tree=(GameObject)PrefabUtility.InstantiatePrefab(realTree,scene);tree.name="Photographic_3D_tree";tree.transform.SetParent(group,false);tree.transform.position=pos;tree.transform.rotation=Quaternion.Euler(0,yaw,0);tree.transform.localScale=Vector3.one*scale;}}
        /* Distant scenery is the photographed HDRI; repetitive box buildings removed.
        var wall=Mat("Neighbourhood_plaster",C("AAA99F"));var brick=Mat("Muted_brick",C("806B5B"));
        for(int i=0;i<9;i++){float x=-56+i*14;float h=R(8,18);float z=R(43,54);var m=i%3==0?brick:wall;Box("Neighbourhood_building",new Vector3(x,h*.5f,z),new Vector3(R(10,13),h,10),m);Box("Parapet",new Vector3(x,h+.2f,z),new Vector3(13,.4f,10.3f),concrete);
            for(float y=2;y<h-1;y+=2.8f)for(int k=-2;k<=2;k++){Box("Window_frame",new Vector3(x+k*2.1f,y,z-5.02f),new Vector3(1.3f,1.55f,.1f),metal);Box("Window_glass",new Vector3(x+k*2.1f,y,z-5.08f),new Vector3(1.16f,1.4f,.02f),glass);Box("Window_mullion",new Vector3(x+k*2.1f,y,z-5.1f),new Vector3(.045f,1.4f,.02f),concrete);}
            Box("Roof_service_box",new Vector3(x+2,h+.7f,z),new Vector3(2.4f,1.2f,2),metal);
        } */
    }
    static void Tree(Vector3 pos,float height) {
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/ReferenceAssets/tree_small_02.fbx")!=null) {
            if(treeCard==null)treeCard=PrepareTreeCard();
            var root=new GameObject("Real_tree_billboard").transform;root.SetParent(group,false);root.position=pos;root.rotation=Quaternion.Euler(0,R(0,180),0);
            float w=height*.58f;
            var v=new List<Vector3>{new Vector3(-w,0,0),new Vector3(w,0,0),new Vector3(w,height,0),new Vector3(-w,height,0),new Vector3(-w,0,0),new Vector3(w,0,0),new Vector3(w,height,0),new Vector3(-w,height,0)};
            var uv=new List<Vector2>{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1),new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};
            var mesh=MeshAsset("Tree_card",v,new List<int>{0,2,1,0,3,2,4,5,6,4,6,7},uv);
            for(int k=0;k<2;k++){var o=new GameObject("Canopy_card");o.transform.SetParent(root,false);o.transform.localRotation=Quaternion.Euler(0,k*90,0);o.AddComponent<MeshFilter>().sharedMesh=mesh;var rend=o.AddComponent<MeshRenderer>();rend.sharedMaterial=treeCard;rend.shadowCastingMode=ShadowCastingMode.TwoSided;}
            return;
        }
        var p=new GameObject("Street_tree").transform;p.SetParent(group,false);p.position=pos;
        Cylinder("Tree_trunk",new Vector3(0,height*.35f,0),.13f,height*.7f,bark,p);
        for(int j=0;j<6;j++){float a=j*Mathf.PI/3;var top=new Vector3(Mathf.Cos(a)*1.2f,height*.72f,Mathf.Sin(a)*1.2f);Beam("Tree_branch",new Vector3(0,height*.4f,0),top,.055f,bark,p);}
        for(int i=0;i<17;i++){var q=new Vector3(R(-1.5f,1.5f),height*.73f+R(-.65f,1.0f),R(-1.5f,1.5f));float s=R(.95f,1.8f);var o=Primitive("Leaf_mass",PrimitiveType.Sphere,q,new Vector3(s,s*R(.85f,1.2f),s),leaf,p);UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>());}
        Box("Tree_pit",new Vector3(0,.01f,0),new Vector3(2,.02f,2),bark,p);
    }
    static void LightingAndCamera() {
        Group("07_Scene_lighting_and_cameras");
        // Controlled outdoor bounce avoids saturated negative-lobe SH artifacts from the HDR sun.
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.60f,.66f);RenderSettings.ambientEquatorColor=new Color(.30f,.32f,.35f);RenderSettings.ambientGroundColor=new Color(.14f,.14f,.13f);RenderSettings.ambientIntensity=1;
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;RenderSettings.fogDensity=.00045f;RenderSettings.fogColor=C("B4BEC4");
        string skyPath=Root+"/KoreanAssets/korean_daylight_sky_4k.hdr";
        var skyImporter=(TextureImporter)AssetImporter.GetAtPath(skyPath);if(skyImporter.maxTextureSize!=4096||skyImporter.textureCompression!=TextureImporterCompression.Uncompressed){skyImporter.maxTextureSize=4096;skyImporter.textureCompression=TextureImporterCompression.Uncompressed;skyImporter.mipmapEnabled=false;skyImporter.SaveAndReimport();}
        var sky=new Material(Shader.Find("Skybox/Panoramic"));sky.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(skyPath));sky.SetFloat("_Exposure",.82f);sky.SetFloat("_Rotation",0);string sp=Root+"/Materials/Parking_sky.mat";var old=AssetDatabase.LoadAssetAtPath<Material>(sp);if(old==null){AssetDatabase.CreateAsset(sky,sp);}else{EditorUtility.CopySerialized(sky,old);UnityEngine.Object.DestroyImmediate(sky);sky=old;EditorUtility.SetDirty(sky);AssetDatabase.SaveAssetIfDirty(sky);}RenderSettings.skybox=sky;
        var sun=new GameObject("HDR_matched_Korean_scene_daylight");sun.transform.SetParent(group);sun.transform.rotation=Quaternion.LookRotation(new Vector3(-.55474323f,-.74146599f,.37747601f),Vector3.up);var l=sun.AddComponent<Light>();l.type=LightType.Directional;l.color=C("FFF8EF");l.intensity=1.15f;l.shadows=LightShadows.Soft;l.shadowStrength=1;l.shadowBias=.5f;l.shadowNormalBias=.5f;RenderSettings.sun=l;
        var camGO=new GameObject("ParkingLot_PreviewCamera");camGO.transform.SetParent(group);var cam=camGO.AddComponent<Camera>();cam.tag="MainCamera";cam.fieldOfView=50;cam.nearClipPlane=.2f;cam.farClipPlane=1000;cam.allowHDR=true;cam.allowMSAA=true;cam.clearFlags=CameraClearFlags.Skybox;
        var orbit=camGO.AddComponent<ParkingLotEnvironment.ParkingLotCamera>();orbit.foliageWindStrength=.012f;orbit.enabled=true;
        camGO.AddComponent<ParkingLotEnvironment.ParkingLotFoliageBounds>();
        var finish=camGO.AddComponent<ParkingLotEnvironment.ParkingLotPhotoFinish>();finish.shader=Shader.Find("Hidden/ParkingLot/PhotoFinish");
        string tp=Root+"/Materials/Depth_tested_text.mat";var tm=AssetDatabase.LoadAssetAtPath<Material>(tp);if(tm==null){tm=new Material(Shader.Find("ParkingLot/DepthText"));AssetDatabase.CreateAsset(tm,tp);}var textDepth=camGO.AddComponent<ParkingLotEnvironment.ParkingLotTextDepth>();textDepth.font=font;textDepth.textMaterial=tm;textDepth.Refresh();
        cam.transform.position=new Vector3(19,1.65f,-10);cam.transform.LookAt(new Vector3(-8,1.2f,-5));
        var probeGO=new GameObject("ParkingLot_ReflectionProbe");probeGO.transform.SetParent(group);probeGO.transform.position=new Vector3(0,1.2f,0);var probe=probeGO.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.OnAwake;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;probe.resolution=512;probe.size=new Vector3(65,15,60);probe.boxProjection=false;probe.intensity=1;probe.RenderProbe();
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,Target);
        // Capture requested on a later editor update, after reflection rendering has finished.
        if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,0),Quaternion.Euler(32,-35,0),42);
    }
    static void Render(Camera cam,string path,Vector3 p,Vector3 look) {
        cam.transform.position=p;cam.transform.LookAt(look);var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf){antiAliasing=4};cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var t=new Texture2D(1920,1080,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1920,1080),0,0);t.Apply();File.WriteAllBytes(path,t.EncodeToPNG());cam.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(t);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
    static void BakeReflections(){
        var s=SceneManager.GetSceneByPath(Target);if(!s.IsValid()||!s.isLoaded)throw new Exception("Open ParkingLot first");
        Directory.CreateDirectory(Root+"/BakedReflections");AssetDatabase.Refresh();
        var parent=GameObject.Find("07_Scene_lighting_and_cameras").transform;
        var existing=GameObject.Find("ParkingLot_ReflectionProbe");if(existing!=null)UnityEngine.Object.DestroyImmediate(existing);
        for(int i=0;i<4;i++){string name="Baked_car_reflections_"+i;var old=GameObject.Find(name);if(old!=null)UnityEngine.Object.DestroyImmediate(old);var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=new Vector3(i%2==0?-8:8,1.2f,i<2?-10:10);var probe=go.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Baked;probe.resolution=256;probe.hdr=true;probe.size=new Vector3(32,12,32);probe.blendDistance=6;probe.boxProjection=false;probe.intensity=1;probe.nearClipPlane=.1f;probe.farClipPlane=800;string path=Root+"/BakedReflections/parking_"+i+".exr";if(!Lightmapping.BakeReflectionProbe(probe,path))throw new Exception("Reflection bake failed: "+name);AssetDatabase.ImportAsset(path);probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(path);if(probe.customBakedTexture==null)throw new Exception("No baked reflection texture: "+path);}
        EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s,Target);
    }
    static Material PrepareTreeCard() {
        const string texPath=Root+"/Textures/Real_tree_card.png";
        if(!File.Exists(texPath)) {
            var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/ReferenceAssets/tree_small_02.fbx"));model.transform.position=new Vector3(0,-1000,0);
            var trunkMat=Mat("Real_trunk",Color.white);trunkMat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ReferenceAssets/tree_small_02_diff_1k.jpg");
            var branchMat=Mat("Real_branch",Color.white);branchMat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ReferenceAssets/tree_small_02_branch_diff_1k.png");
            var leafMat=new Material(Shader.Find("ParkingLot/LeafCutout"));leafMat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ReferenceAssets/tree_small_02_leaves_diff_1k.png");leafMat.SetTexture("_AlphaTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/ReferenceAssets/tree_small_02_leaves_alpha_1k.png"));
            Bounds bounds=new Bounds();bool first=true;
            foreach(var t in model.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            foreach(var r in model.GetComponentsInChildren<Renderer>()){var ms=r.sharedMaterials;for(int i=0;i<ms.Length;i++){string n=ms[i].name;ms[i]=n.Contains("leaves")?leafMat:n.Contains("branch")?branchMat:trunkMat;}r.sharedMaterials=ms;if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            var go=new GameObject("Temporary_tree_capture");var camera=go.AddComponent<Camera>();camera.cullingMask=1<<31;camera.orthographic=true;float extent=Mathf.Max(bounds.size.y,bounds.size.x)*1.08f;camera.orthographicSize=extent*.5f;camera.transform.position=bounds.center+new Vector3(0,0,-extent*2);camera.transform.LookAt(bounds.center);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.nearClipPlane=.01f;camera.farClipPlane=extent*4;
            var lightGO=new GameObject("Temporary_tree_light");var light=lightGO.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.cullingMask=1<<31;lightGO.transform.rotation=Quaternion.Euler(40,-30,0);
            var rt=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;bool fog=RenderSettings.fog;RenderSettings.fog=false;camera.Render();RenderSettings.fog=fog;
            RenderTexture.active=rt;var tex=new Texture2D(1024,1024,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,1024,1024),0,0);tex.Apply();File.WriteAllBytes(texPath,tex.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(model);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(lightGO);UnityEngine.Object.DestroyImmediate(leafMat);
            AssetDatabase.ImportAsset(texPath);var imp=(TextureImporter)AssetImporter.GetAtPath(texPath);imp.alphaIsTransparency=true;imp.mipmapEnabled=true;imp.SaveAndReimport();
        }
        string path=Root+"/Materials/Real_tree_card.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Unlit/Transparent Cutout"));AssetDatabase.CreateAsset(mat,path);}mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);mat.SetFloat("_Cutoff",.4f);EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);return mat;
    }
}
}
