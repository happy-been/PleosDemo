using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
/// <summary>New scene-local perimeter assets only. Called explicitly by the parking lot builder.</summary>
public static class ParkingLotRealismDetails {
    const string Root="Assets/ParkingLotEnvironment";
    const string Sources=Root+"/RealismAssets/";
    const string Materials=Root+"/Materials/RealismDetails";
    const string Meshes=Root+"/Meshes/RealismDetails";
    static Transform group;
    static System.Random random;
    static Material soil,curb,steel,wire,foliage;
    static GameObject sourceShrub;
    static int shrubCount;

    public static void Build(Transform parent) {
        if(parent==null)throw new ArgumentNullException(nameof(parent));
        if(parent.gameObject.scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")
            throw new InvalidOperationException("Realism details may only be built in ParklingLot.unity.");
        EnsureFolder(Materials);EnsureFolder(Meshes);
        random=new System.Random(60318);shrubCount=0;
        var rootObject=new GameObject("08_Realism_perimeter_and_planting");
        SceneManager.MoveGameObjectToScene(rootObject,parent.gameObject.scene);
        group=rootObject.transform;group.SetParent(parent,false);
        curb=Material("Weathered_concrete_border",Shader.Find("Standard"),new Color(.48f,.47f,.43f),.05f,0);
        steel=Material("Galvanized_fence_posts",Shader.Find("Standard"),new Color(.37f,.40f,.39f),.37f,.65f);
        wire=Material("Galvanized_fence_wire",Shader.Find("Standard"),new Color(.24f,.28f,.27f),.32f,.6f);
        soil=Material("Photographed_soil_and_leaves",Shader.Find("Standard"),new Color(.73f,.73f,.73f),.04f,0);
        soil.mainTexture=Texture("brown_mud_leaves_01_diff_1k.jpg",false,true);
        soil.SetTexture("_BumpMap",Texture("brown_mud_leaves_01_nor_gl_1k.jpg",true,false));
        soil.EnableKeyword("_NORMALMAP");soil.SetFloat("_BumpScale",.55f);
        soil.mainTextureScale=new Vector2(1.6f,19f);Save(soil);
        var shader=Shader.Find("ParkingLot/RealismFoliage");
        if(shader==null)throw new InvalidOperationException("ParkingLot/RealismFoliage shader is not imported.");
        foliage=Material("Photographed_shrub_03",shader,Color.white,0,0);
        foliage.mainTexture=Texture("shrub_03_diff_1k.jpg",false,true);
        foliage.SetTexture("_AlphaTex",Texture("shrub_03_alpha_1k.png",false,false));
        foliage.SetTexture("_BumpMap",Texture("shrub_03_nor_gl_1k.jpg",true,false));
        foliage.SetTexture("_OcclusionMap",Texture("shrub_03_ao_1k.jpg",false,false));
        foliage.SetTexture("_RoughnessMap",Texture("shrub_03_rough_1k.jpg",false,false));
        foliage.SetFloat("_Cutoff",.43f);Save(foliage);
        sourceShrub=AssetDatabase.LoadAssetAtPath<GameObject>(Sources+"shrub_03_1k.fbx");
        if(sourceShrub==null)throw new InvalidOperationException("The photographed shrub FBX is missing.");

        // Existing footpaths reach x=28m; keep all planting beyond them.
        for(int side=-1;side<=1;side+=2) {
            Bed(new Vector3(side*29.05f,.06f,0),1.65f,39f,false);
            for(int z=-18;z<=18;z+=3)PlantCluster(new Vector3(side*29.05f,.13f,z),3,.57f);
            Fence(new Vector3(side*30.1f,.02f,-22.6f),new Vector3(side*30.1f,.02f,26.0f),"Side_"+side);
        }
        Bed(new Vector3(0,.06f,24.55f),1.65f,54f,true);
        for(int x=-25;x<=25;x+=3)PlantCluster(new Vector3(x,.13f,24.55f),3,.57f);
        Fence(new Vector3(-30.1f,.02f,26),new Vector3(30.1f,.02f,26),"Rear");
        // Front boundary stops at x=14m. The requested x=16..26 entrance stays open.
        Fence(new Vector3(-30.1f,.02f,-23.5f),new Vector3(14,.02f,-23.5f),"Front");
        Debug.Log("PARKINGLOT_REALISM_DETAILS: "+shrubCount+" individual 0.25–0.40m photographed herbs; perimeter fences; raised soil beds. No original assets modified.");
    }

    static void Bed(Vector3 p,float width,float length,bool across) {
        Vector3 size=across?new Vector3(length,.14f,width):new Vector3(width,.14f,length);
        var bed=Box("Leaf_litter_planting_bed",p,size,soil);
        // Cube top UVs follow x/z: rotate the geometry, not the source texture, for rear beds.
        if(across){bed.transform.localScale=new Vector3(width,.14f,length);bed.transform.localRotation=Quaternion.Euler(0,90,0);}
        int units=Mathf.CeilToInt(length);
        for(int side=-1;side<=1;side+=2)for(int n=0;n<units;n++){
            float t=-length*.5f+(n+.5f)*length/units;
            Vector3 pos=p+(across?new Vector3(t,.04f,side*(width*.5f+.045f)):new Vector3(side*(width*.5f+.045f),.04f,t));
            Vector3 s=across?new Vector3(length/units-.018f,.20f,.09f):new Vector3(.09f,.20f,length/units-.018f);
            Box("Individual_planting_edging",pos,s,curb);
        }
    }

    static void PlantCluster(Vector3 pos,int count,float radius) {
        for(int i=0;i<count;i++) {
            var go=UnityEngine.Object.Instantiate(sourceShrub,group,false);
            go.name="Photographed_herb_"+(++shrubCount).ToString("000");
            go.transform.localPosition=Vector3.zero;
            var renderers=go.GetComponentsInChildren<MeshRenderer>(true);
            if(renderers.Length==0){UnityEngine.Object.DestroyImmediate(go);continue;}
            int chosen=random.Next(renderers.Length);
            for(int n=0;n<renderers.Length;n++){
                renderers[n].enabled=n==chosen;
                if(n!=chosen)continue;
                var mats=new Material[renderers[n].sharedMaterials.Length];
                for(int m=0;m<mats.Length;m++)mats[m]=foliage;
                renderers[n].sharedMaterials=mats;
                renderers[n].shadowCastingMode=ShadowCastingMode.TwoSided;
                renderers[n].receiveShadows=true;
            }
            foreach(var collider in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            var selected=renderers[chosen];
            // The original asset contains four separate 25–40cm herbs, laid side by side.
            // Normalize the selected renderer only, retaining FBX axis conversion and actual proportions.
            float desiredHeight=Range(.25f,.40f);
            float currentHeight=selected.bounds.size.y;
            if(currentHeight>.00001f)go.transform.localScale*=desiredHeight/currentHeight;
            go.transform.localRotation=Quaternion.Euler(0,Range(0,360),0)*go.transform.localRotation;
            Bounds b=selected.bounds;
            Vector3 target=group.TransformPoint(pos+new Vector3(Range(-radius,radius),0,Range(-radius,radius)));
            go.transform.position+=target-new Vector3(b.center.x,b.min.y,b.center.z);
        }
    }

    static void Fence(Vector3 start,Vector3 end,string name) {
        const float height=1.45f;
        Vector3 direction=(end-start).normalized;
        float length=Vector3.Distance(start,end);
        int sections=Mathf.CeilToInt(length/2.5f);
        for(int n=0;n<=sections;n++){
            Vector3 p=Vector3.Lerp(start,end,(float)n/sections);
            Box("Fence_post_"+name,p+Vector3.up*(height*.5f),new Vector3(.055f,height,.055f),steel);
            Box("Concrete_post_footing",p+Vector3.up*.035f,new Vector3(.18f,.07f,.18f),curb);
        }
        for(int n=0;n<2;n++)Beam("Fence_tension_rail",start+Vector3.up*(n==0?.10f:height),end+Vector3.up*(n==0?.10f:height),.024f,steel);
        // Single mesh of diamond wires per side; thin wires do not cast noisy subpixel shadows.
        var vertices=new List<Vector3>();var triangles=new List<int>();
        const float spacing=.15f,thickness=.0045f;
        for(float k=-height;k<length+height;k+=spacing)for(int slope=-1;slope<=1;slope+=2){
            float lo=Mathf.Max(0,slope==1?-k:k-length);
            float hi=Mathf.Min(height,slope==1?length-k:k);
            if(hi<=lo)continue;
            Vector3 a=start+direction*(k+slope*lo)+Vector3.up*lo;
            Vector3 b=start+direction*(k+slope*hi)+Vector3.up*hi;
            Vector3 cross=(direction-Vector3.up*slope).normalized*thickness*.5f;
            int v=vertices.Count;
            for(int face=0;face<2;face++){vertices.Add(a-cross);vertices.Add(a+cross);vertices.Add(b+cross);vertices.Add(b-cross);}
            triangles.AddRange(new[]{v,v+1,v+2,v,v+2,v+3,v+6,v+5,v+4,v+7,v+6,v+4});
        }
        var mesh=new Mesh{name="Diamond_fence_"+name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        string path=Meshes+"/"+mesh.name+".asset";
        mesh=ParkingLotMeshAsset.Save(mesh,path);
        var fence=new GameObject("Chain_link_"+name);fence.transform.SetParent(group,false);
        fence.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=fence.AddComponent<MeshRenderer>();renderer.sharedMaterial=wire;renderer.shadowCastingMode=ShadowCastingMode.Off;
    }

    static GameObject Box(string name,Vector3 position,Vector3 scale,Material material){
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(group,false);
        go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic);
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
    }
    static void Beam(string name,Vector3 a,Vector3 b,float width,Material material){
        var go=Box(name,(a+b)*.5f,new Vector3(width,width,Vector3.Distance(a,b)),material);
        go.transform.localRotation=Quaternion.LookRotation(b-a);
    }
    static Material Material(string name,Shader shader,Color color,float smoothness,float metallic){
        string path=Materials+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
        m.color=color;if(m.HasProperty("_Glossiness"))m.SetFloat("_Glossiness",smoothness);
        if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metallic);m.enableInstancing=true;Save(m);return m;
    }
    static Texture2D Texture(string name,bool normal,bool srgb){
        string path=Sources+name;var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Missing realism texture: "+path);
        var kind=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
        if(importer.textureType!=kind||importer.sRGBTexture!=srgb||importer.anisoLevel!=4){
            importer.textureType=kind;importer.sRGBTexture=srgb;importer.anisoLevel=4;importer.mipmapEnabled=true;importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static float Range(float min,float max){return min+(float)random.NextDouble()*(max-min);}
    static void Save(UnityEngine.Object asset){EditorUtility.SetDirty(asset);AssetDatabase.SaveAssetIfDirty(asset);}
    static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;int split=path.LastIndexOf('/');string parent=path.Substring(0,split);EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(split+1));}
}
}
