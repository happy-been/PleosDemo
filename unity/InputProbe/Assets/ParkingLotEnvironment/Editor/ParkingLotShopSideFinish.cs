using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// Adds detail on the three map-owned shop side walls without changing footprints or existing meshes.
public static class ParkingLotShopSideFinish {
    const string Root="Assets/ParkingLotEnvironment";
    const string Name="Shop_side_window_details";
    public static void Build(Scene scene){
        if(!scene.IsValid()||!scene.isLoaded||scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Shop finish belongs only to ParklingLot.");
        var shops=new List<Transform>();
        foreach(var root in scene.GetRootGameObjects())if(root.name=="09_Korean_neighborhood_context")
            foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name.StartsWith("Shop_block_"))shops.Add(t);
        if(shops.Count!=3)throw new InvalidOperationException("Expected exactly three map shops.");
        var wall=Load("Korean_shop_side_wall_brick");var frame=Load("Korean_shop_window_frame");var cap=Load("Korean_shop_parapet_cap");
        ParkingLotArchitecturalSurfaces.Brick(wall);
        // Reuse curtain/blind patches from the existing generated facade; no new bitmap is synthesized.
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/KoreanAssets/shop_facade.png");
        if(texture==null)throw new InvalidOperationException("Missing shop facade.");
        var pane=ParkingLotKoreanContext.Mat("Korean_shop_side_window_interior",new Color(.82f,.87f,.90f),.32f,.03f,texture);
        var crops=new[]{Crop(100,124,222,230),Crop(100,403,222,515),Crop(707,124,829,230)};
        var g=new ParkingLotKoreanContext.Geo();int index=0;
        foreach(int side in new[]{-1,1})foreach(float y in new[]{1.9f,5.5f,9.1f})foreach(float z in new[]{-2f,2f}){
            var c=new Vector3(side*6.095f,y,z);var right=Vector3.forward*side*.55f;var up=Vector3.up*.65f;var uv=crops[(index++)%crops.Length];
            g.Quad(pane,c-right-up,c+right-up,c+right+up,c-right+up,Vector3.right*side,
                new Vector2(uv.xMin,uv.yMin),new Vector2(uv.xMax,uv.yMin),new Vector2(uv.xMax,uv.yMax),new Vector2(uv.xMin,uv.yMax));
            g.Box(frame,new Vector3(side*6.12f,y,z),new Vector3(.035f,1.3f,.026f));
            g.Box(cap,new Vector3(side*6.10f,y-.79f,z),new Vector3(.25f,.08f,1.6f));
        }
        GameObject prototype=null;
        foreach(var shop in shops){
            var old=shop.Find(Name);if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            if(prototype==null)prototype=ParkingLotKoreanContext.Emit(g,Name,shop,Vector3.zero,Quaternion.identity,true);
            else{var instance=UnityEngine.Object.Instantiate(prototype,shop,false);instance.name=Name;}
        }
        File.WriteAllText(ParkingLotEditorPaths.Output+"/shop-side-finish-audit.txt",DateTime.Now.ToString("s")+"\nShops=3\nSideWindowPanes=36\nSills=36\nSharedMeshes=1\nAddedTriangles="+(prototype.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3*3)+"\nFootprintsUnchanged=True");
    }
    static Rect Crop(float x0,float y0,float x1,float y1){return Rect.MinMaxRect(x0/1536,1-y1/1024,x1/1536,1-y0/1024);}
    static Material Load(string name){var m=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/KoreanContext/"+name+".mat");if(m==null)throw new InvalidOperationException("Missing shop material "+name);return m;}
}
}
