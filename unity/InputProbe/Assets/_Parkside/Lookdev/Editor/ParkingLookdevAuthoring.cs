using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Parkside.Lookdev.Editor
{
    /// <summary>Authors one isolated visual prototype. Does not replace the practice scene or build entry.</summary>
    public static class ParkingLookdevAuthoring
    {
        public const string ScenePath = "Assets/_Parkside/Scenes/ParkingLookdev.unity";
        private const string Root = "Assets/_Parkside/Lookdev/";
        private static Material asphalt, concrete, render, trim, glass, metal, rubber, yellow, soil, wood, car, bark, branch, leaves, shrub;
        private static Transform environment;
        private static int meshIndex;
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../../../build/parking-lookdev"));

        [MenuItem("PARKSIDE/Lookdev/Create courtyard scene")]
        public static void CreateScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "Materials"); Directory.CreateDirectory(Root + "Generated/Meshes");
            ImportAssets();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            meshIndex = 0;
            environment = new GameObject("Courtyard · 36m × 32m").transform;
            CreateMaterials();
            CreateRoadAndCurbs();
            CreateLandscaping();
            CreateBuildings();
            CreateDetails();
            CreateLighting();
            CreateViewer();
            EditorSceneManager.SaveScene(scene,ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("PARKSIDE_LOOKDEV_AUTHORED " + ScenePath);
        }

        private static void ImportAssets()
        {
            foreach (var path in Directory.GetFiles(Root + "Textures","*",SearchOption.AllDirectories).Concat(Directory.GetFiles(Root + "Generated","*.png")))
            {
                var normalized = path.Replace('\\','/');
                if (AssetImporter.GetAtPath(normalized) is not TextureImporter importer) continue;
                if (normalized.EndsWith(".hdr"))
                {
                    importer.textureShape = TextureImporterShape.TextureCube;
                    importer.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                    importer.sRGBTexture = false;
                    importer.maxTextureSize = 1024;
                }
                else
                {
                    var normal = normalized.Contains("_nor_gl_");
                    importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = !normal && !normalized.Contains("metalSmooth") && !normalized.Contains("WearMask") && !normalized.Contains("_alpha_") && !normalized.Contains("_arm_");
                    importer.maxTextureSize = normalized.Contains("asphalt") || normalized.Contains("concrete") || normalized.Contains("WearMask") ? 2048 : 1024;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = normalized.Contains("_diff_") && normalized.EndsWith(".png");
                    importer.mipMapsPreserveCoverage = importer.alphaIsTransparency;
                    importer.alphaTestReferenceValue = .4f;
                    importer.anisoLevel = 8;
                    importer.wrapMode = normalized.Contains("WearMask") ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                }
                importer.mipmapEnabled = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
            foreach (var path in Directory.GetFiles(Root + "Models","*.fbx"))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.generateSecondaryUV = false;
                importer.importAnimation = false;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
        }
        private static Texture2D Texture(string folder, string name)
        {
            var files = Directory.GetFiles(Root + "Textures/" + folder,name + ".*");
            var file = files.OrderByDescending(value=>value.EndsWith(".png")).FirstOrDefault();
            if (file == null) throw new InvalidOperationException("Missing texture " + folder + "/" + name);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(file.Replace('\\','/'));
        }
        private static Material SaveMaterial(string name, Shader shader)
        {
            if (shader == null) throw new InvalidOperationException("Missing shader for " + name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + name + ".mat");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material,Root + "Materials/" + name + ".mat"); }
            else material.shader = shader;
            material.name = name; material.enableInstancing = true; EditorUtility.SetDirty(material);
            return material;
        }
        private static Material Plain(string name, Color colour, float smoothness = .15f, float metallic = 0)
        {
            var material = SaveMaterial(name,Shader.Find("Standard"));
            material.color = colour; material.SetFloat("_Glossiness",smoothness); material.SetFloat("_Metallic",metallic);
            return material;
        }
        private static Material Scan(string name, string folder, string prefix, string resolution, Color colour, bool foliage = false)
        {
            var material = SaveMaterial(name,Shader.Find(foliage ? "Parkside/Lookdev/Two Sided Foliage" : "Standard"));
            material.color = colour;
            material.mainTexture = Texture(folder,prefix + "_diff_" + resolution);
            var normalName = prefix + "_nor_gl_" + resolution;
            if (Directory.GetFiles(Root + "Textures/" + folder,normalName + ".*").Length > 0)
            {
                material.SetTexture("_BumpMap",Texture(folder,normalName)); material.SetFloat("_BumpScale",.55f); material.EnableKeyword("_NORMALMAP");
            }
            var packedName = prefix + "_metalSmooth_" + resolution;
            if (!foliage && Directory.GetFiles(Root + "Textures/" + folder,packedName + ".*").Length > 0)
            {
                var packed = Texture(folder,packedName); material.SetTexture("_MetallicGlossMap",packed); material.SetTexture("_OcclusionMap",packed);
                material.SetFloat("_GlossMapScale",.7f); material.SetFloat("_OcclusionStrength",.7f); material.EnableKeyword("_METALLICGLOSSMAP");
            }
            material.SetFloat("_Glossiness",.15f);
            return material;
        }
        private static void CreateMaterials()
        {
            asphalt = SaveMaterial("Scanned asphalt and worn paint",Shader.Find("Parkside/Lookdev/Courtyard Asphalt"));
            asphalt.SetTexture("_MainTex",Texture("asphalt_03","asphalt_03_diff_2k"));
            asphalt.SetTexture("_BumpMap",Texture("asphalt_03","asphalt_03_nor_gl_2k"));
            asphalt.SetTexture("_Packed",Texture("asphalt_03","asphalt_03_metalSmooth_2k"));
            asphalt.SetTexture("_WearMask",AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Generated/ParkingWearMask.png"));
            asphalt.SetColor("_Colour",new Color(.6f,.68f,.73f));
            concrete = Scan("Weathered concrete","concrete_floor","concrete_floor","2k",new Color(.8f,.81f,.78f));
            concrete.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Generated/CurbstoneColour.png");
            concrete.color=Color.white;
            concrete.mainTextureScale = Vector2.one * .5f;
            render = Scan("Warm apartment render","concrete_floor","concrete_floor","2k",new Color(.88f,.87f,.82f));
            render.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Generated/FacadePlaster.png");
            render.color=Color.white; render.SetFloat("_BumpScale",.06f);
            render.mainTextureScale = Vector2.one * .23f;
            trim = Plain("Basalt and window frames",new Color(.23f,.25f,.26f),.25f);
            glass = Plain("Sky reflecting window glass",new Color(.27f,.37f,.42f),.86f,.72f);
            metal = Plain("Weathered galvanized steel",new Color(.35f,.37f,.36f),.35f,.7f);
            rubber = Plain("Rubber wheel stops",new Color(.055f,.06f,.06f),.1f);
            yellow = Plain("Muted safety yellow",new Color(.68f,.49f,.12f),.2f);
            soil = Scan("Planting soil","asphalt_03","asphalt_03","2k",new Color(.34f,.27f,.18f));
            soil.mainTextureScale = Vector2.one * 2;
            wood = Scan("Weathered timber","brown_planks_03","brown_planks_03","1k",Color.white);
            car = Scan("Covered parked car","covered_car","covered_car","1k",Color.white);
            bark = Scan("Tree bark","tree_small_02","tree_small_02","1k",Color.white);
            branch = Scan("Tree branches","tree_small_02","tree_small_02_branch","1k",Color.white);
            leaves = Scan("Tree leaves","tree_small_02","tree_small_02_leaves","1k",new Color(.82f,.88f,.78f),true);
            shrub = Scan("Shrub leaves","shrub_01","shrub_01","1k",new Color(.85f,.9f,.82f),true);
        }
        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material, Transform parent = null, bool lightmap = true)
        {
            lightmap=lightmap&&Mathf.Min(size.x,size.y,size.z)>=.2f;
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name;
            obj.transform.SetParent(parent ?? environment,false); obj.transform.localPosition = position; obj.transform.localScale = size;
            var renderer = obj.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.receiveGI = lightmap ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            GameObjectUtility.SetStaticEditorFlags(obj,StaticEditorFlags.BatchingStatic | (lightmap ? StaticEditorFlags.ContributeGI : 0));
            return obj;
        }
        private static GameObject Cylinder(string name, Vector3 position, float radius, float height, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder); obj.name = name; obj.transform.SetParent(environment,false);
            obj.transform.position = position; obj.transform.localScale = new Vector3(radius*2,height/2,radius*2);
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(obj,StaticEditorFlags.BatchingStatic);
            return obj;
        }
        private static GameObject Curb(string name, Vector3 position, float width, float height, float length, Material material)
        {
            var obj = new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));
            obj.transform.SetParent(environment,false); obj.transform.position = position;
            var b = Mathf.Min(.035f,width*.15f,height*.2f);
            var section = new[] {new Vector2(-width/2+b,0),new Vector2(width/2-b,0),new Vector2(width/2,b),new Vector2(width/2,height-b),
                new Vector2(width/2-b,height),new Vector2(-width/2+b,height),new Vector2(-width/2,height-b),new Vector2(-width/2,b)};
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            void Quad(Vector3 a,Vector3 c,Vector3 d,Vector3 e,float span,float high)
            {
                var start = vertices.Count; vertices.AddRange(new[] {a,c,d,e}); uv.AddRange(new[] {Vector2.zero,new Vector2(span,0),new Vector2(span,high),new Vector2(0,high)});
                triangles.AddRange(new[] {start,start+2,start+1,start,start+3,start+2});
            }
            for (var i=0;i<8;i++)
            {
                var a=section[i]; var c=section[(i+1)%8];
                Quad(new Vector3(a.x,a.y,-length/2),new Vector3(a.x,a.y,length/2),new Vector3(c.x,c.y,length/2),new Vector3(c.x,c.y,-length/2),length,Vector2.Distance(a,c));
            }
            for(var i=1;i<7;i++)
            {
                var start=vertices.Count;
                vertices.AddRange(new[] {new Vector3(section[0].x,section[0].y,-length/2),new Vector3(section[i+1].x,section[i+1].y,-length/2),new Vector3(section[i].x,section[i].y,-length/2),
                    new Vector3(section[0].x,section[0].y,length/2),new Vector3(section[i].x,section[i].y,length/2),new Vector3(section[i+1].x,section[i+1].y,length/2)});
                uv.AddRange(vertices.Skip(start).Take(6).Select(vertex=>new Vector2(vertex.x,vertex.y))); triangles.AddRange(new[] {start,start+1,start+2,start+3,start+4,start+5});
            }
            var mesh=new Mesh {name=name}; mesh.SetVertices(vertices); mesh.SetUVs(0,uv); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            Unwrapping.GenerateSecondaryUVSet(mesh);
            AssetDatabase.CreateAsset(mesh,Root+"Generated/Meshes/Curb"+(meshIndex++)+".asset");
            obj.GetComponent<MeshFilter>().sharedMesh=mesh; obj.GetComponent<MeshRenderer>().sharedMaterial=material;
            var collider=obj.GetComponent<BoxCollider>(); collider.size=new Vector3(width,height,length); collider.center=Vector3.up*height/2;
            obj.GetComponent<MeshRenderer>().receiveGI=ReceiveGI.LightProbes;
            GameObjectUtility.SetStaticEditorFlags(obj,StaticEditorFlags.BatchingStatic);
            return obj;
        }
        private static void CreateRoadAndCurbs()
        {
            var surroundings=Scan("Surrounding pavement","asphalt_03","asphalt_03","2k",new Color(.7f,.76f,.8f)); surroundings.mainTextureScale=new Vector2(80,80);
            Box("Neighbourhood ground",new Vector3(0,-.29f,0),new Vector3(160,.22f,160),surroundings,null,false);
            Box("Road · scanned 2m asphalt with metre-scaled wear mask",new Vector3(0,-.12f,0),new Vector3(36,.24f,32),asphalt);
            Box("North raised planting bed",new Vector3(0,.06f,10.7f),new Vector3(29,.12f,3.4f),soil);
            Box("Left walkway",new Vector3(-15,.05f,0),new Vector3(2,.1f,30),concrete);
            Box("Right walkway",new Vector3(15,.05f,0),new Vector3(2,.1f,30),concrete);
            Box("Walkway behind planting",new Vector3(0,.05f,13.6f),new Vector3(32,.1f,2.2f),concrete);
            for(var i=0;i<30;i++)
            {
                var stone=Curb("North curb · joint "+i,new Vector3(-14.5f+i,.005f,8.9f),.3f,.18f,.985f,concrete);
                stone.transform.Rotate(0,90,0);
            }
            for(var i=0;i<28;i++)
            {
                Curb("Left curb "+i,new Vector3(-13.8f,0,-13.5f+i),.28f,.16f,.985f,concrete);
                Curb("Right curb "+i,new Vector3(13.8f,0,-13.5f+i),.28f,.16f,.985f,concrete);
            }
            for(var i=0;i<8;i++)
            {
                var x=-9.1f+i*2.6f;
                var stop=Curb("Wheel stop "+(101+i),new Vector3(x,.008f,7.45f),.19f,.105f,1.5f,rubber); stop.transform.Rotate(0,90,0);
                Box("Wheel stop reflector",new Vector3(x-.48f,.105f,7.45f),new Vector3(.12f,.012f,.17f),yellow);
                Box("Wheel stop reflector",new Vector3(x+.48f,.105f,7.45f),new Vector3(.12f,.012f,.17f),yellow);
            }
        }
        private static GameObject Model(string asset, string name, Vector3 position, float targetHeight, float rotation, int variant = -1)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Models/"+asset+".fbx");
            if(source==null) throw new InvalidOperationException("Missing scan model "+asset);
            var container=new GameObject(name); container.transform.SetParent(environment,false);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(source); obj.transform.SetParent(container.transform,false);
            var renderers=obj.GetComponentsInChildren<MeshRenderer>();
            if(variant>=0) for(var i=0;i<renderers.Length;i++) renderers[i].gameObject.SetActive(i==variant%renderers.Length);
            foreach(var renderer in renderers)
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(mat=>asset=="covered_car"?car:asset=="shrub_01"?shrub:
                    mat.name.ToLowerInvariant().Contains("leav")?leaves:mat.name.ToLowerInvariant().Contains("branch")?branch:bark).ToArray();
                renderer.receiveGI=ReceiveGI.LightProbes;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic|(asset=="covered_car"?StaticEditorFlags.ContributeGI:0));
            }
            container.transform.rotation=Quaternion.Euler(0,rotation,0);
            var active=renderers.Where(value=>value.gameObject.activeInHierarchy).ToArray();
            var bounds=active[0].bounds; foreach(var renderer in active.Skip(1)) bounds.Encapsulate(renderer.bounds);
            container.transform.localScale*=targetHeight/bounds.size.y;
            bounds=active[0].bounds; foreach(var renderer in active.Skip(1)) bounds.Encapsulate(renderer.bounds);
            container.transform.position+=position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            return container;
        }
        private static void CreateLandscaping()
        {
            Model("tree_small_02","Courtyard tree west",new Vector3(-11.6f,.12f,10.4f),5.9f,22);
            Model("tree_small_02","Courtyard tree east",new Vector3(9.6f,.12f,10.4f),6.4f,132);
            Model("tree_small_02","Courtyard tree entrance",new Vector3(16,.1f,-6),7.1f,255);
            for(var i=0;i<38;i++) Model("shrub_01","Planting bed shrub "+i,new Vector3(-13.3f+(i%19)*1.46f,.13f,9.65f+(i/19)*1.3f),.65f+(i%4)*.1f,i*41,i);
            Model("covered_car","Covered parked car · scanned asset",new Vector3(-6.5f,.025f,5.6f),1.4f,180);
            Model("covered_car","Covered parked car east",new Vector3(6.5f,.025f,5.6f),1.45f,0);
        }
        private static void CreateBuildings()
        {
            Apartment("Apartment 101",new Vector3(-3,0,23),31,18,8);
            Apartment("Apartment 102",new Vector3(-27,0,11),12,27,16);
            Apartment("Apartment 103",new Vector3(29,0,24),15,31,12);
        }
        private static void Apartment(string name,Vector3 center,float width,float height,float depth)
        {
            var group=new GameObject(name).transform; group.SetParent(environment,false); group.position=center;
            Box("Rendered facade",new Vector3(0,height/2,0),new Vector3(width,height,depth),render,group);
            Box("Dark podium",new Vector3(0,1.1f,-depth/2-.04f),new Vector3(width,2.2f,.12f),trim,group);
            var count=Mathf.FloorToInt(width/3.7f); var stories=Mathf.FloorToInt((height-2)/3);
            for(var floor=0;floor<stories;floor++) for(var col=0;col<count;col++)
            {
                var x=(col-(count-1)/2f)*3.7f; var y=3.3f+floor*3f; var z=-depth/2;
                Box("Window recess",new Vector3(x,y,z-.07f),new Vector3(2.15f,1.75f,.15f),trim,group,false);
                Box("Glass",new Vector3(x,y,z-.16f),new Vector3(1.99f,1.59f,.035f),glass,group,false);
                Box("Centre mullion",new Vector3(x,y,z-.195f),new Vector3(.045f,1.6f,.04f),trim,group,false);
                Box("Window sill",new Vector3(x,y-.93f,z-.2f),new Vector3(2.3f,.12f,.45f),concrete,group);
            }
            for(var floor=0;floor<=stories;floor++) Box("Facade horizontal joint",new Vector3(0,2.2f+floor*3,-depth/2-.04f),new Vector3(width,.025f,.04f),trim,group);
        }
        private static void CreateDetails()
        {
            foreach(var x in new[] {-11f,1.7f,11f})
            {
                Box("Drain dark opening",new Vector3(x,.002f,8.52f),new Vector3(.95f,.01f,.38f),rubber);
                Box("Drain frame",new Vector3(x,.019f,8.73f),new Vector3(1.02f,.028f,.045f),metal);
                Box("Drain frame",new Vector3(x,.019f,8.31f),new Vector3(1.02f,.028f,.045f),metal);
                for(var i=0;i<17;i++) Box("Drain slat",new Vector3(x-.46f+i*.057f,.022f,8.52f),new Vector3(.023f,.035f,.38f),metal);
            }
            foreach(var x in new[] {-12.7f,12.7f})
            {
                Cylinder("Steel bollard",new Vector3(x,.45f,1.2f),.055f,.9f,metal);
                Cylinder("Bollard reflective collar",new Vector3(x,.73f,1.2f),.057f,.08f,yellow);
            }
            for(var i=0;i<5;i++) Box("Bench timber slat",new Vector3(1.7f,.46f,12.3f+i*.1f),new Vector3(1.7f,.06f,.075f),wood);
            Box("Bench leg",new Vector3(1.1f,.23f,12.5f),new Vector3(.07f,.46f,.48f),metal);
            Box("Bench leg",new Vector3(2.3f,.23f,12.5f),new Vector3(.07f,.46f,.48f),metal);
        }
        private static void CreateLighting()
        {
            var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(Root+"Textures/Sky/CourtyardSky.hdr");
            if(cube==null) throw new InvalidOperationException("Sky cubemap import failed");
            var sky=SaveMaterial("Courtyard scanned sky",Shader.Find("Skybox/Cubemap")); sky.SetTexture("_Tex",cube); sky.SetFloat("_Exposure",.95f); sky.SetFloat("_Rotation",115);
            RenderSettings.skybox=sky; RenderSettings.ambientMode=AmbientMode.Skybox; RenderSettings.ambientIntensity=1;
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom; RenderSettings.customReflection=cube; RenderSettings.reflectionIntensity=.8f;
            RenderSettings.fog=true; RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogColor=new Color(.71f,.76f,.8f); RenderSettings.fogStartDistance=40; RenderSettings.fogEndDistance=130;
            var sun=new GameObject("Late morning sun",typeof(Light)).GetComponent<Light>(); sun.type=LightType.Directional; sun.transform.rotation=Quaternion.Euler(43,-65,0);
            sun.color=new Color(1,.955f,.88f); sun.intensity=1.15f; sun.shadows=LightShadows.Soft; sun.shadowStrength=.78f; sun.shadowBias=.035f; sun.shadowNormalBias=.25f;
            sun.lightmapBakeType=LightmapBakeType.Mixed; RenderSettings.sun=sun;
            var probes=new GameObject("Plant and parked car light probes",typeof(LightProbeGroup)).GetComponent<LightProbeGroup>();
            var positions=new List<Vector3>(); foreach(var x in new[] {-13f,-6f,0f,6f,13f}) foreach(var z in new[] {-8f,2f,10f}) foreach(var y in new[] {1f,3f,7f}) positions.Add(new Vector3(x,y,z));
            probes.probePositions=positions.ToArray();
            var settings=AssetDatabase.LoadAssetAtPath<LightingSettings>(Root+"CourtyardLighting.lighting");
            if(settings==null) { settings=new LightingSettings(); AssetDatabase.CreateAsset(settings,Root+"CourtyardLighting.lighting"); }
            settings.bakedGI=true; settings.realtimeGI=false; settings.lightmapper=LightingSettings.Lightmapper.ProgressiveCPU;
            settings.mixedBakeMode=MixedLightingMode.Shadowmask; settings.lightmapResolution=8; settings.lightmapMaxSize=1024;
            settings.directSampleCount=24; settings.indirectSampleCount=48; settings.environmentSampleCount=48;
            settings.maxBounces=2; settings.ao=true; settings.aoMaxDistance=.8f; settings.aoExponentIndirect=1;
            Lightmapping.lightingSettings=settings; Lightmapping.giWorkflowMode=Lightmapping.GIWorkflowMode.OnDemand;
            DynamicGI.UpdateEnvironment();
        }
        private static void CreateViewer()
        {
            var anchorRoot=new GameObject("Saved viewpoints").transform;
            var positions=new[] {new Vector3(-3.2f,1.22f,-7),new Vector3(-12.5f,1.8f,-4),new Vector3(-6.5f,.5f,.6f),new Vector3(-18,17,-18)};
            var targets=new[] {new Vector3(.2f,1.15f,7),new Vector3(0,1.6f,7.4f),new Vector3(-1,.07f,4.3f),new Vector3(0,.7f,3)};
            var anchors=new Transform[4];
            for(var i=0;i<4;i++) { anchors[i]=new GameObject(new[] {"01 Driver eye height","02 Parking bay","03 Asphalt close-up","04 Aerial"}[i]).transform; anchors[i].SetParent(anchorRoot,false); anchors[i].position=positions[i]; anchors[i].LookAt(targets[i]); }
            var rig=new GameObject("Lookdev viewer"); var viewer=rig.AddComponent<ParkingLookdevViewer>();
            var camera=new GameObject("Lookdev camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>(); camera.transform.SetParent(rig.transform,false);
            camera.tag="MainCamera"; camera.nearClipPlane=.08f; camera.farClipPlane=180; camera.fieldOfView=56; camera.allowHDR=true; camera.allowMSAA=true;
            var canvas=new GameObject("Lookdev help",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler)).GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920,1080);
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/_Parkside/UI/Fonts/AstaSans-Medium.ttf");
            Text Label(string name,string text,int size,Vector2 position,Vector2 dimensions)
            {
                var label=new GameObject(name,typeof(RectTransform),typeof(Text)).GetComponent<Text>(); label.transform.SetParent(canvas.transform,false);
                var rect=label.rectTransform; rect.anchorMin=rect.anchorMax=new Vector2(0,0); rect.pivot=new Vector2(0,0); rect.anchoredPosition=position; rect.sizeDelta=dimensions;
                label.font=font; label.fontSize=size; label.text=text; label.color=new Color(.94f,.95f,.91f); label.raycastTarget=false;
                var shadow=label.gameObject.AddComponent<Shadow>(); shadow.effectDistance=new Vector2(1,-1); shadow.effectColor=new Color(0,0,0,.8f);
                return label;
            }
            Label("Title","PARKSIDE / COURTYARD 01",20,new Vector2(32,1040),new Vector2(600,32));
            var view=Label("View label","01  운전자 높이 · 이동 가능",20,new Vector2(32,62),new Vector2(1000,32));
            Label("Controls","1–4 시점 선택   ·   W/S 전진·후진   ←/→ 조향   Space 정지   R 위치 초기화   Tab 안내 숨김",18,new Vector2(32,26),new Vector2(1500,30));
            var so=new SerializedObject(viewer); var array=so.FindProperty("viewpoints"); array.arraySize=4;
            for(var i=0;i<4;i++) array.GetArrayElementAtIndex(i).objectReferenceValue=anchors[i];
            so.FindProperty("viewCamera").objectReferenceValue=camera; so.FindProperty("helpCanvas").objectReferenceValue=canvas; so.FindProperty("viewLabel").objectReferenceValue=view; so.ApplyModifiedPropertiesWithoutUndo();
            viewer.SetView(0);
        }

        public static void CreateBakeAndVerify()
        {
            Directory.CreateDirectory(Output); CreateScene();
            Debug.Log("PARKSIDE_LOOKDEV_BAKE_START");
            if(!Lightmapping.Bake()) throw new InvalidOperationException("Courtyard lighting bake failed");
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            ValidateScene();
            SessionState.SetBool("ParksideLookdevSmoke",true); EditorApplication.isPlaying=true;
        }
        [MenuItem("PARKSIDE/Lookdev/Open courtyard scene")]
        public static void OpenForReview() => EditorSceneManager.OpenScene(ScenePath);
        public static void VerifyExisting()
        {
            ImportAssets(); OpenForReview(); ValidateScene();
            SessionState.SetBool("ParksideLookdevSmoke",true); EditorApplication.isPlaying=true;
        }
        public static void ValidateScene()
        {
            var scene=SceneManager.GetActiveScene(); var viewer=Object.FindObjectOfType<ParkingLookdevViewer>();
            if(viewer==null||!viewer.IsConfigured) throw new InvalidOperationException("Lookdev viewpoints or Canvas missing");
            var renderers=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MeshRenderer>()).ToArray();
            var triangles=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<MeshFilter>()).Sum(filter=>Enumerable.Range(0,filter.sharedMesh.subMeshCount).Sum(index=>(int)filter.sharedMesh.GetIndexCount(index)/3));
            foreach(var material in renderers.SelectMany(renderer=>renderer.sharedMaterials).Distinct())
                if(material==null||material.shader==null||ShaderUtil.ShaderHasError(material.shader)) throw new InvalidOperationException("Invalid courtyard material: "+material);
            if(LightmapSettings.lightmaps.Length==0) throw new InvalidOperationException("Courtyard has no baked lightmaps");
            File.WriteAllText(Path.Combine(Output,"scene-report.txt"),"Scene: "+scene.path+"\nRenderers: "+renderers.Length+"\nVisible triangles: "+triangles+"\nLightmaps: "+LightmapSettings.lightmaps.Length+"\nGraphics target: PC preview; not measured on Pleos AVD.\n");
            Debug.Log("PARKSIDE_LOOKDEV_VALIDATION_PASS renderers="+renderers.Length+" triangles="+triangles+" lightmaps="+LightmapSettings.lightmaps.Length);
        }
        public static void Capture(string name)
        {
            var camera=Object.FindObjectOfType<Camera>(); var canvas=Object.FindObjectOfType<Canvas>(); var enabled=canvas.enabled; canvas.enabled=false;
            var previous=RenderTexture.active; var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf);
            camera.targetTexture=target; camera.Render(); RenderTexture.active=target;
            var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,1920,1080),0,0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(Output,name+".png"),pixels.EncodeToPNG());
            camera.targetTexture=null; RenderTexture.active=previous; canvas.enabled=enabled;
            Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
        }
    }
    [InitializeOnLoad]
    public static class LookdevPlayVerification
    {
        private static double next,deadline; private static int view;
        static LookdevPlayVerification()
        {
            EditorApplication.playModeStateChanged+=state=>{
                if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("ParksideLookdevSmoke",false)) return;
                view=0; next=EditorApplication.timeSinceStartup+2; deadline=next+35; EditorApplication.update+=Tick;
            };
        }
        private static void Tick()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline) throw new InvalidOperationException("Lookdev Play validation timed out");
                if(EditorApplication.timeSinceStartup<next) return;
                var viewer=Object.FindObjectOfType<ParkingLookdevViewer>();
                if(viewer==null||!viewer.IsConfigured) throw new InvalidOperationException("Lookdev missing in Play");
                if(view<4)
                {
                    viewer.SetView(view); ParkingLookdevAuthoring.Capture(new[] {"driver","parking-bay","asphalt-closeup","aerial"}[view]);
                    view++; next=EditorApplication.timeSinceStartup+.5;
                }
                else
                {
                    SessionState.SetBool("ParksideLookdevSmoke",false); EditorApplication.update-=Tick;
                    Debug.Log("PARKSIDE_LOOKDEV_PLAY_PASS views=4"); EditorApplication.Exit(0);
                }
            }
            catch(Exception error)
            {
                SessionState.SetBool("ParksideLookdevSmoke",false); EditorApplication.update-=Tick;
                Debug.LogError("PARKSIDE_LOOKDEV_PLAY_FAIL "+error); EditorApplication.Exit(1);
            }
        }
    }
}
