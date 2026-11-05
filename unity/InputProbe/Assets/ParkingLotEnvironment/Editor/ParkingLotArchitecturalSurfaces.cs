using System;
using UnityEditor;
using UnityEngine;

namespace ParkingLotEnvironment.Editor {
// Materials are map-owned; photographic concrete and generated roof coating stay separately credited.
public static class ParkingLotArchitecturalSurfaces {
    const string Root="Assets/ParkingLotEnvironment";
    public static void Roof(Material material){
        Configure(material,"roof_waterproofing_aged.png",.1f,1);
        material.color=new Color(.70f,.80f,.72f);
        Save(material);
    }
    public static void Concrete(Material material){Configure(material,"concrete_wall_009_diff_2k.jpg",1f/18.05f,0);Save(material);}
    public static void Brick(Material material){Configure(material,"red_brick_diff_2k.jpg",1f/1.4f,1);material.color=new Color(.78f,.78f,.76f);Save(material);}
    static void Configure(Material material,string filename,float scale,float textureColor){
        string path=AssetDatabase.GetAssetPath(material);
        if(!path.StartsWith(Root+"/Materials/KoreanContext/",StringComparison.Ordinal))throw new InvalidOperationException("Architectural finish requires a map-owned material.");
        var shader=Shader.Find("ParkingLot/ArchitecturalSurface");if(shader==null)throw new InvalidOperationException("Compile architectural surface shader first.");
        material.shader=shader;material.mainTexture=Load(filename);material.SetFloat("_WorldScale",scale);material.SetFloat("_TextureAsColor",textureColor);material.SetFloat("_DetailStrength",.7f);
        material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");material.enableInstancing=true;
    }
    static Texture2D Load(string filename){
        string path=Root+"/KoreanAssets/"+filename;
        var imp=AssetImporter.GetAtPath(path) as TextureImporter;
        if(imp==null){AssetDatabase.ImportAsset(path);imp=AssetImporter.GetAtPath(path) as TextureImporter;}
        if(imp==null)throw new InvalidOperationException("Missing architectural texture: "+path);
        if(imp.textureType!=TextureImporterType.Default||!imp.sRGBTexture||imp.maxTextureSize!=2048||imp.anisoLevel!=8||!imp.mipmapEnabled||imp.wrapMode!=TextureWrapMode.Repeat||imp.filterMode!=FilterMode.Trilinear||imp.textureCompression!=TextureImporterCompression.CompressedHQ){
            imp.textureType=TextureImporterType.Default;imp.sRGBTexture=true;imp.maxTextureSize=2048;imp.anisoLevel=8;imp.mipmapEnabled=true;imp.wrapMode=TextureWrapMode.Repeat;imp.filterMode=FilterMode.Trilinear;imp.textureCompression=TextureImporterCompression.CompressedHQ;imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static void Save(Material material){EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);}
}
}
