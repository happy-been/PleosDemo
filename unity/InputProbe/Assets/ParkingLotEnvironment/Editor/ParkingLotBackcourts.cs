using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Background paving only. Disjoint rectangles avoid layered surfaces and never cover the driving lot.
public static class ParkingLotBackcourts {
    const string Root="Assets/ParkingLotEnvironment";
    const string Name="15_Residential_backcourts";
    public static void Build(Scene scene){
        if(!scene.IsValid()||!scene.isLoaded||scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Backcourts belong only to ParklingLot.");
        foreach(var root in scene.GetRootGameObjects())if(root.name==Name)UnityEngine.Object.DestroyImmediate(root);
        var excluded=new List<Rect>{Rect.MinMaxRect(-31,-24,31,29),Rect.MinMaxRect(-92,-35,92,-23)};
        var plots=new List<Rect>();foreach(var building in ParkingLotNeighborhoodInfill.Buildings)plots.Add(building.Footprint(5.2f));
        foreach(var root in scene.GetRootGameObjects()){
            // Existing flat street, sidewalk and plot-pad meshes are the actual clipping source.
            if(root.name=="10_Connected_Korean_neighborhood")foreach(var r in root.GetComponentsInChildren<MeshRenderer>()){
                var b=r.bounds;if(b.size.y<.005f&&b.center.y>0&&b.max.y<.1f)excluded.Add(Expanded(b,.015f));
            }
            if(root.name=="09_Korean_neighborhood_context"||root.name=="12_Korean_residential_infill")
                foreach(var collider in root.GetComponentsInChildren<BoxCollider>()){
                    excluded.Add(Expanded(collider.bounds,.02f));
                    if(collider.name.StartsWith("Shop_block_"))plots.Add(Expanded(collider.bounds,5.2f));
                }
            foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name=="Photographic_3D_tree"||t.name=="Neighborhood_tree_12k")
                excluded.Add(new Rect(t.position.x-1.1f,t.position.z-1.1f,2.2f,2.2f));
        }
        var diffuse=Texture("concrete_pavement_diff_2k.jpg",false);
        var normal=Texture("concrete_pavement_nor_gl_2k.jpg",true);
        var asphalt=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/RealismAssets/clean_asphalt_diff_2k.jpg");
        var materials=new[]{Material("Backcourt_pavers_neutral",new Color(.70f,.71f,.69f),diffuse,normal),
            Material("Backcourt_pavers_warm",new Color(.73f,.68f,.59f),diffuse,normal),
            Material("Backcourt_worn_asphalt",new Color(.58f,.59f,.56f),asphalt,null)};
        var geo=new ParkingLotKoreanContext.Geo();var placed=new List<Rect>();float area=0;int index=0;
        foreach(var plot in plots){
            var pieces=new List<Rect>{plot};
            foreach(var cut in excluded)pieces=Subtract(pieces,cut);
            foreach(var cut in placed)pieces=Subtract(pieces,cut);
            int tone=(index++)%3;float tile= tone==2?.5f:1/1.8f;
            foreach(var r in pieces){
                if(r.width<.08f||r.height<.08f)continue;
                foreach(var cut in excluded)if(OverlapArea(r,cut)>.0001f)throw new InvalidOperationException("Backcourt covers protected ground.");
                foreach(var cut in placed)if(OverlapArea(r,cut)>.0001f)throw new InvalidOperationException("Backcourt surfaces overlap.");
                placed.Add(r);area+=r.width*r.height;
                const float y=.012f;
                geo.Quad(materials[tone],new Vector3(r.xMin,y,r.yMin),new Vector3(r.xMax,y,r.yMin),new Vector3(r.xMax,y,r.yMax),new Vector3(r.xMin,y,r.yMax),Vector3.up,
                    new Vector2(r.xMin,r.yMin)*tile,new Vector2(r.xMax,r.yMin)*tile,new Vector2(r.xMax,r.yMax)*tile,new Vector2(r.xMin,r.yMax)*tile);
            }
        }
        var parent=new GameObject(Name);SceneManager.MoveGameObjectToScene(parent,scene);
        var result=ParkingLotKoreanContext.Emit(geo,"Residential_backcourt_surfaces",parent.transform,Vector3.zero,Quaternion.identity,false);
        File.WriteAllText(ParkingLotEditorPaths.Output+"/backcourt-layout-audit.txt",DateTime.Now.ToString("s")+"\nRectangles="+placed.Count+"\nAreaM2="+area+"\nTriangles="+(placed.Count*2)+"\nMaterialSlots="+result.GetComponent<MeshRenderer>().sharedMaterials.Length+"\nProtectedGroundOverlaps=0\nSelfOverlaps=0\nCollidersAdded=0");
    }
    static Rect Expanded(Bounds b,float margin){return Rect.MinMaxRect(b.min.x-margin,b.min.z-margin,b.max.x+margin,b.max.z+margin);}
    static float OverlapArea(Rect a,Rect b){return Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin))*Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin));}
    static List<Rect> Subtract(List<Rect> input,Rect cut){
        var output=new List<Rect>();
        foreach(var a in input){
            float x0=Mathf.Max(a.xMin,cut.xMin),x1=Mathf.Min(a.xMax,cut.xMax),z0=Mathf.Max(a.yMin,cut.yMin),z1=Mathf.Min(a.yMax,cut.yMax);
            if(x1<=x0||z1<=z0){output.Add(a);continue;}
            Add(output,a.xMin,a.yMin,x0,a.yMax);Add(output,x1,a.yMin,a.xMax,a.yMax);
            Add(output,x0,a.yMin,x1,z0);Add(output,x0,z1,x1,a.yMax);
        }
        return output;
    }
    static void Add(List<Rect> output,float x0,float z0,float x1,float z1){if(x1-x0>.00001f&&z1-z0>.00001f)output.Add(Rect.MinMaxRect(x0,z0,x1,z1));}
    static Material Material(string name,Color color,Texture2D diffuse,Texture2D normal){
        if(diffuse==null)throw new InvalidOperationException("Missing backcourt texture.");
        string path=Root+"/Materials/KoreanContext/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.mainTexture=diffuse;m.mainTextureScale=Vector2.one;m.SetFloat("_Metallic",0);m.SetFloat("_Glossiness",.1f);
        m.SetTexture("_BumpMap",normal);m.SetFloat("_BumpScale",.35f);if(normal!=null)m.EnableKeyword("_NORMALMAP");else m.DisableKeyword("_NORMALMAP");
        m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");m.EnableKeyword("_GLOSSYREFLECTIONS_OFF");m.enableInstancing=true;EditorUtility.SetDirty(m);AssetDatabase.SaveAssetIfDirty(m);return m;
    }
    static Texture2D Texture(string name,bool normal){
        string path=Root+"/KoreanAssets/"+name;var importer=AssetImporter.GetAtPath(path) as TextureImporter;
        if(importer==null){AssetDatabase.ImportAsset(path);importer=AssetImporter.GetAtPath(path) as TextureImporter;}
        if(importer==null)throw new InvalidOperationException("Missing pavement source: "+path);
        importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal;
        importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.anisoLevel=8;importer.filterMode=FilterMode.Trilinear;importer.wrapMode=TextureWrapMode.Repeat;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
}
