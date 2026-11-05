using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.Editor {
// One merged, four-quad visual layer. No colliders, route changes or original-asset edits.
public static class ParkingLotGroundWeathering {
    const string Root="Assets/ParkingLotEnvironment";
    public static void Build(Scene scene){
        if(!scene.IsValid()||!scene.isLoaded||scene.path!="Assets/_Parkside/Scenes/ParklingLot.unity")throw new InvalidOperationException("Ground repairs belong only to this map.");
        // Only map-owned photographed asphalt imports. Preserve mipmaps but sharpen grazing views.
        foreach(string texture in new[]{"clean_asphalt_diff_2k.jpg","clean_asphalt_nor_gl_2k.jpg","clean_asphalt_rough_2k.jpg"}){
            var ground=AssetImporter.GetAtPath(Root+"/RealismAssets/"+texture) as TextureImporter;
            if(ground!=null&&(ground.anisoLevel!=8||ground.filterMode!=FilterMode.Trilinear)){ground.anisoLevel=8;ground.filterMode=FilterMode.Trilinear;ground.SaveAndReimport();}
        }
        // The colored pedestrian strip is a weathered aggregate surface, not a flat pastel slab.
        var walkway=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Pedestrian_terracotta.mat");
        if(walkway==null)throw new InvalidOperationException("Build the map pedestrian path first.");
        walkway.shader=Shader.Find("ParkingLot/ArchitecturalSurface");
        if(walkway.shader==null)throw new InvalidOperationException("Compile the map surface shader first.");
        walkway.color=new Color(.50f,.27f,.21f);walkway.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/RealismAssets/clean_asphalt_diff_2k.jpg");
        walkway.SetFloat("_WorldScale",.5f);walkway.SetFloat("_TextureAsColor",0);walkway.SetFloat("_DetailStrength",.9f);
        walkway.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");walkway.EnableKeyword("_GLOSSYREFLECTIONS_OFF");walkway.enableInstancing=true;EditorUtility.SetDirty(walkway);AssetDatabase.SaveAssetIfDirty(walkway);
        const string image=Root+"/KoreanAssets/asphalt_repair_decal.png";
        var imp=AssetImporter.GetAtPath(image) as TextureImporter;if(imp==null)throw new InvalidOperationException("Import the repair decal first.");
        if(!imp.alphaIsTransparency||!imp.mipmapEnabled||imp.anisoLevel!=8||imp.maxTextureSize!=2048||imp.wrapMode!=TextureWrapMode.Clamp){
            imp.alphaIsTransparency=true;imp.mipmapEnabled=true;imp.anisoLevel=8;imp.maxTextureSize=2048;imp.wrapMode=TextureWrapMode.Clamp;imp.filterMode=FilterMode.Trilinear;imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.SaveAndReimport();
        }
        const string path=Root+"/Materials/KoreanContext/Weathered_asphalt_repairs.mat";
        var shader=Shader.Find("ParkingLot/RoadRepair");if(shader==null)throw new InvalidOperationException("Repair shader must compile first.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}material.shader=shader;
        material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(image);material.SetFloat("_Opacity",.18f);material.enableInstancing=true;EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        foreach(var root in scene.GetRootGameObjects())if(root.name=="13_Weathered_ground_repairs")UnityEngine.Object.DestroyImmediate(root);
        var parent=new GameObject("13_Weathered_ground_repairs");SceneManager.MoveGameObjectToScene(parent,scene);var g=new ParkingLotKoreanContext.Geo();
        // Arrow extents are x=-10.4..-7.6 and 6.6..9.4, z=+/-10.
        // Keep the visible patches within the driving aisles and clear of those markings.
        Patch(g,material,new Vector3(3,.003f,-9),1.7f,3.2f,55);
        Patch(g,material,new Vector3(-3,.003f,11),1.35f,2.55f,-73);
        Patch(g,material,new Vector3(20,.003f,1),1.6f,3,7);
        Patch(g,material,new Vector3(-18,.003f,-5),1.25f,2.4f,92);
        var go=ParkingLotKoreanContext.Emit(g,"Four_asphalt_repairs",parent.transform,Vector3.zero,Quaternion.identity,false);
        var renderer=go.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;renderer.lightProbeUsage=LightProbeUsage.Off;
    }
    static void Patch(ParkingLotKoreanContext.Geo g,Material m,Vector3 center,float width,float length,float yaw){
        var q=Quaternion.Euler(0,yaw,0);
        g.Quad(m,center+q*new Vector3(-width/2,0,-length/2),center+q*new Vector3(width/2,0,-length/2),center+q*new Vector3(width/2,0,length/2),center+q*new Vector3(-width/2,0,length/2),Vector3.up);
    }
}
}
