using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Connects buildings to believable land and streets, using a few shared materials and small meshes.
public static class ParkingLotNeighborhoodGround {
    const string Root="Assets/ParkingLotEnvironment";
    const string MeshDir=Root+"/Meshes/KoreanContext";
    const string MatDir=Root+"/Materials/KoreanContext";
    static Transform group;
    static Material grass,paving,asphalt,curb,hill;
    public static void Build(Scene scene){
        if(!scene.IsValid()||!scene.isLoaded||scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Neighborhood ground belongs only to ParklingLot.");
        foreach(var existing in scene.GetRootGameObjects())if(existing.name=="10_Connected_Korean_neighborhood")UnityEngine.Object.DestroyImmediate(existing);
        var go=new GameObject("10_Connected_Korean_neighborhood");SceneManager.MoveGameObjectToScene(go,scene);group=go.transform;
        grass=GroundMaterial("Korean_grass_courtyards",new Color(.48f,.56f,.42f),.18f);
        foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())if(renderer.name=="Context_ground_240m")renderer.sharedMaterial=grass;
        hill=GroundMaterial("Distant_vegetated_Korean_hills",new Color(.27f,.36f,.25f),.0125f);
        hill.mainTexture=Texture("forest_canopy.png",false);hill.SetTexture("_BumpMap",null);hill.SetFloat("_BumpScale",0);
        hill.SetTexture("_FootTex",Texture("grass_ground_diff_2k.jpg",false));hill.SetFloat("_FootBlend",1);hill.SetColor("_FootTint",grass.color);Save(hill);
        paving=Material("Korean_courtyard_paving",new Color(.52f,.52f,.48f));
        var originalConcrete=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Concrete.mat");paving.mainTexture=originalConcrete!=null?originalConcrete.mainTexture:null;Save(paving);
        curb=Material("Korean_neighborhood_granite",new Color(.62f,.61f,.57f));
        asphalt=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Weathered_asphalt.mat");
        if(asphalt==null)throw new InvalidOperationException("Parking lot asphalt must be built first.");
        // Flat land starts at the same grade as the buildings; courtyards replace the empty gray void.
        Plane("Rear_residential_land",-96,96,28,125,-.005f,grass);
        Plane("West_residential_land",-96,-30.8f,-23,28,-.005f,grass);
        Plane("East_residential_land",30.8f,96,-23,28,-.005f,grass);
        Plane("South_neighborhood_land",-96,96,-110,-38,-.005f,grass);
        // The old continuous lane crossed tower 103. End the main lane at its entrance courtyard.
        Plane("Rear_access_lane",-56,92,34,39,.005f,asphalt);
        Plane("Rear_west_access_lane",-92,-74,34,39,.005f,asphalt);
        Plane("West_access_lane",-36,-31,-34,62,.006f,asphalt);
        Plane("West_lane_jog",-36,-20,58,63,.007f,asphalt);
        Plane("West_lane_north_connector",-25,-20,63,106,.006f,asphalt);
        // Jog around tower 102, whose footprint reaches x36, z86..98.
        Plane("East_access_lane",31,36,-34,82,.006f,asphalt);
        Plane("East_lane_jog",31,43,79,84,.007f,asphalt);
        Plane("East_lane_north_connector",38,43,84,106,.006f,asphalt);
        Plane("Rear_lane_sidewalk_west",-56,-1,39,41,.065f,paving);
        Plane("Rear_lane_sidewalk_east",11,92,39,41,.065f,paving);
        Plane("Rear_shop_landing_walk",-1,11,39,40.5f,.065f,paving);
        Plane("Outer_west_lane",-84,-78,-34,126,.006f,asphalt);
        Plane("Outer_east_lane",80,86,-34,126,.006f,asphalt);
        Plane("Northern_connecting_lane",-84,86,100,106,.008f,asphalt);
        Plane("Main_street_west_extension",-90,-70,-34,-24,.006f,asphalt);
        Plane("Main_street_east_extension",70,90,-34,-24,.006f,asphalt);
        Plane("West_lane_frontage_walk",-38,-36,-24,64,.065f,paving);
        Plane("East_lane_frontage_walk",36,38,-24,79,.065f,paving);
        Plane("Northern_frontage_walk",-76,79,106,109,.065f,paving);
        Plane("Outer_west_frontage_walk",-78,-76,-24,100,.065f,paving);
        // Individual shop entrances and apartment courtyards, with narrow paths that reach the streets.
        Pad("West_shop_forecourt",-50,-36,-16,2);
        Pad("East_shop_forecourt",35,49,9,27);
        Pad("Rear_shop_forecourt",-4,14,39.2f,53);
        Pad("Tower_101_forecourt",-52,-24,64,80);
        Pad("Tower_102_forecourt",10,38,84,100);
        Pad("Tower_103_forecourt",-73,-57,24,52);
        Pad("Tower_104_forecourt",57,73,-12,16);
        Plane("Path_to_101",-28,-26,63,66,.025f,paving);
        Plane("Path_to_102",28,30.5f,55,84,.025f,paving);
        Plane("Path_to_103",-59,-56,34,39,.025f,paving);
        Plane("Path_to_104",36,57,-1,3,.025f,paving);
        // Low curbs along the service street give its edge thickness without adding thousands of objects.
        foreach(var span in new[]{new Vector2(-56,-36),new Vector2(-31,31),new Vector2(36,80),new Vector2(86,92)})
            for(int side=0;side<2;side++)for(float x=span.x;x<span.y;x+=4){float length=Mathf.Min(4,span.y-x);Box("Service_street_kerb",new Vector3(x+length/2,.075f,side==0?33.85f:39.15f),new Vector3(length-.04f,.15f,.2f),curb);}
        ResidentialPlots();
        Hills();CourtyardTrees();
    }
    static void ResidentialPlots(){
        int i=0;var walls=new ParkingLotKoreanContext.Geo();
        foreach(var b in ParkingLotNeighborhoodInfill.Buildings){
            var r=b.Footprint(.85f);Pad("Residential_plot_"+(++i).ToString("D2"),r.xMin,r.xMax,r.yMin,r.yMax);
            var rotation=Quaternion.Euler(0,b.yaw,0);float hw=13*b.scale.x/2+.8f,hd=11*b.scale.z/2+.8f;
            // Side/back garden walls leave the full entrance frontage open.
            foreach(int side in new[]{-1,1})walls.Box(curb,b.position+rotation*new Vector3(side*hw,.26f,.3f),new Vector3(.16f,.52f,2*hd-.6f),rotation);
            walls.Box(curb,b.position+rotation*new Vector3(0,.26f,hd),new Vector3(2*hw,.52f,.16f),rotation);
            if(b.position.z>110)Plane("North_entry_walk_"+i,r.xMin,r.xMax,109,r.yMin,.026f,paving);
            if(b.position.z<-40)Plane("South_entry_walk_"+i,r.xMin,r.xMax,r.yMax,-37.6f,.026f,paving);
        }
        ParkingLotKoreanContext.Emit(walls,"Residential_plot_boundary_walls",group,Vector3.zero,Quaternion.identity,true);
    }
    static void Pad(string name,float x0,float x1,float z0,float z1){Plane(name,x0,x1,z0,z1,.025f,paving);}
    static void Plane(string name,float x0,float x1,float z0,float z1,float y,Material material){
        var v=new[]{new Vector3(x0,y,z0),new Vector3(x0,y,z1),new Vector3(x1,y,z1),new Vector3(x1,y,z0)};
        var uv=new[]{new Vector2(x0,z0),new Vector2(x0,z1),new Vector2(x1,z1),new Vector2(x1,z0)};
        var mesh=new Mesh{name=name};mesh.vertices=v;mesh.uv=uv;mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateNormals();mesh.RecalculateBounds();Emit(name,mesh,material,false);
    }
    static void Hills(){
        const int segments=384,rings=36;var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
        for(int r=0;r<=rings;r++)for(int i=0;i<=segments;i++){
            float a=i*Mathf.PI*2/segments;float radius=HillStart(a)+r/(float)rings*HillWidth(a);
            float y=HillHeight(a,radius);
            var p=new Vector3(Mathf.Sin(a)*radius,y,Mathf.Cos(a)*radius);v.Add(p);uv.Add(new Vector2(p.x,p.z));
            if(r<rings&&i<segments){int b=r*(segments+1)+i,c=b+segments+1;t.AddRange(new[]{b,c,b+1,b+1,c,c+1});}
        }
        var mesh=new Mesh{name="Distant_Korean_hills"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();var n=mesh.normals;for(int r=0;r<=rings;r++){int a=r*(segments+1),b=a+segments;var mean=(n[a]+n[b]).normalized;n[a]=mean;n[b]=mean;}mesh.normals=n;mesh.RecalculateBounds();Emit(mesh.name,mesh,hill,false);
    }
    static float HillStart(float a){return 255+30*Mathf.Sin(a*3+.7f)+15*Mathf.Sin(a*7);}
    static float HillWidth(float a){return 380+60*Mathf.Sin(a*2-.4f);}
    static float CrestT(float a){return .6f+.12f*Mathf.Sin(a*5+1.3f);}
    static float HillHeight(float a,float radius){
        float t=Mathf.Clamp01((radius-HillStart(a))/HillWidth(a));
        float foot=12*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.45f,t));
        float peak=Mathf.Max(10,42+18*Mathf.Sin(a*3+.4f)+12*Mathf.Sin(a*7)+9*(1-Mathf.Abs(Mathf.Sin(a*11+.5f))));
        float d=(t-CrestT(a))/.16f;
        float detail=(Mathf.PerlinNoise(Mathf.Sin(a)*radius*.035f+40,Mathf.Cos(a)*radius*.035f+60)-.5f)*12*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.45f,t));
        return (foot+peak*Mathf.Exp(-d*d)+detail)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.88f,1,t)))-1.5f;
    }
    static float TreeGround(float a,float radius){
        // Sink the cutout base below the local slope in both radial and lateral directions.
        float y=HillHeight(a,radius);
        foreach(float da in new[]{-.022f,0,.022f})foreach(float dr in new[]{-8f,0,8f})y=Mathf.Min(y,HillHeight(a+da,radius+dr));
        return Mathf.Max(0,y)-.6f;
    }
    static void CourtyardTrees(){
        // Low-cost crossed photographic cards are used only in the neighborhood beyond the playable lot.
        var mat=Material("Distant_courtyard_tree_cards",new Color(.75f,.78f,.69f));mat.shader=Shader.Find("ParkingLot/DistantFoliage");
        string canopyPath=Root+"/KoreanAssets/forest_crown_cutout.png";var importer=AssetImporter.GetAtPath(canopyPath) as TextureImporter;
        if(importer==null)throw new InvalidOperationException("Missing forest canopy cutout.");
        if(!importer.alphaIsTransparency||importer.maxTextureSize!=2048){importer.alphaIsTransparency=true;importer.mipmapEnabled=true;importer.maxTextureSize=2048;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();}
        mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(canopyPath);mat.SetFloat("_Cutoff",.28f);Save(mat);
        var random=new System.Random(9284);var vertices=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        var positions=new List<Vector3>();
        for(int i=0;i<60;i++)positions.Add(new Vector3(-210+i*7,0,165+(float)random.NextDouble()*24));
        // Break the smooth terrain silhouette with small trees beyond any close-up camera view.
        for(int i=0;i<420;i++){
            float a=i*Mathf.PI*2/420;float radius=HillStart(a)+HillWidth(a)*(CrestT(a)+((float)random.NextDouble()-.5f)*.22f);
            positions.Add(new Vector3(Mathf.Sin(a)*radius,TreeGround(a,radius),Mathf.Cos(a)*radius));
        }
        for(int i=0;i<100;i++){
            float x=-150+(float)random.NextDouble()*300,z=110+(float)random.NextDouble()*55;
            positions.Add(new Vector3(x,0,z));
        }
        var tree=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/RealVehicles/tree-small-far/tree-small-far.prefab");
        if(tree!=null){
            for(int i=0;i<22;i++){
                float x=-84+i*8,z=46+(float)random.NextDouble()*9;if(x>-7&&x<17)continue;
                AddTree(tree,new Vector3(x,0,z),random);
            }
            for(int i=0;i<10;i++){AddTree(tree,new Vector3(-52,0,8+i*8),random);AddTree(tree,new Vector3(50,0,-10+i*8),random);}
        }
        foreach(var p in positions){
            if(ParkingLotNeighborhoodInfill.Occupied(p,3)||RoadOccupied(p,2))continue;
            if(p.z<54&&p.x>-5&&p.x<15)continue;
            float height=6.5f+(float)random.NextDouble()*4,width=height*1.5f,turn=(float)random.NextDouble()*Mathf.PI;
            for(int face=0;face<3;face++){
                float a=turn+face*Mathf.PI/3;var right=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*width*.5f;int b=vertices.Count;
                vertices.AddRange(new[]{p-right,p+right,p+right+Vector3.up*height,p-right+Vector3.up*height});
                uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)});tris.AddRange(new[]{b,b+2,b+1,b,b+3,b+2});
            }
        }
        var mesh=new Mesh{name="Distant_courtyard_tree_cards"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();Emit(mesh.name,mesh,mat,false);
    }
    static void AddTree(GameObject prefab,Vector3 p,System.Random random){
        if(ParkingLotNeighborhoodInfill.Occupied(p,3.2f)||RoadOccupied(p,2))return;
        var tree=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group.gameObject.scene);tree.name="Neighborhood_tree_12k";tree.transform.SetParent(group,false);tree.transform.position=p;tree.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*360,0);tree.transform.localScale=Vector3.one*(1.5f+(float)random.NextDouble()*.6f);
        var collider=tree.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
    }
    static bool RoadOccupied(Vector3 p,float margin){
        var point=new Vector2(p.x,p.z);
        foreach(var r in new[]{new Rect(-56,34,148,5),new Rect(-92,34,18,5),new Rect(-36,-34,5,96),new Rect(-36,58,16,5),new Rect(-25,63,5,43),new Rect(31,-34,5,116),new Rect(31,79,12,5),new Rect(38,84,5,22),new Rect(-84,-34,6,160),new Rect(80,-34,6,160),new Rect(-84,100,170,6),new Rect(-90,-34,180,10)})if(new Rect(r.x-margin,r.y-margin,r.width+margin*2,r.height+margin*2).Contains(point))return true;
        return false;
    }
    static void Emit(string name,Mesh mesh,Material mat,bool shadows){
        string path=MeshDir+"/"+name+".asset";var saved=ParkingLotMeshAsset.Save(mesh,path);
        var go=new GameObject(name);go.transform.SetParent(group,false);go.AddComponent<MeshFilter>().sharedMesh=saved;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=shadows?ShadowCastingMode.On:ShadowCastingMode.Off;r.receiveShadows=true;go.isStatic=true;
    }
    static void Box(string name,Vector3 position,Vector3 scale,Material mat){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(group,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;}
    static Material Material(string name,Color color){string path=MatDir+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}m.color=color;m.SetFloat("_Glossiness",.04f);m.enableInstancing=true;Save(m);return m;}
    static Material GroundMaterial(string name,Color color,float scale){
        string path=MatDir+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("ParkingLot/KoreanGround"));AssetDatabase.CreateAsset(m,path);}m.shader=Shader.Find("ParkingLot/KoreanGround");m.color=color;m.SetFloat("_TileScale",scale);m.SetFloat("_BumpScale",.28f);
        // A smooth terrain sheet's grazing-angle reflection made the forest look milky despite dark albedo.
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
        m.mainTexture=Texture("grass_ground_diff_2k.jpg",false);m.SetTexture("_BumpMap",Texture("grass_ground_nor_gl_2k.jpg",true));m.SetFloat("_FootBlend",0);m.enableInstancing=true;Save(m);return m;
    }
    static Texture2D Texture(string name,bool normal){string path=Root+"/KoreanAssets/"+name;var imp=AssetImporter.GetAtPath(path) as TextureImporter;if(imp==null)throw new InvalidOperationException("Missing ground texture: "+path);var kind=normal?TextureImporterType.NormalMap:TextureImporterType.Default;if(imp.textureType!=kind||imp.anisoLevel!=8||imp.sRGBTexture==normal){imp.textureType=kind;imp.sRGBTexture=!normal;imp.anisoLevel=8;imp.mipmapEnabled=true;imp.maxTextureSize=2048;imp.SaveAndReimport();}return AssetDatabase.LoadAssetAtPath<Texture2D>(path);}
    static void Save(UnityEngine.Object o){EditorUtility.SetDirty(o);AssetDatabase.SaveAssetIfDirty(o);}
}
}
