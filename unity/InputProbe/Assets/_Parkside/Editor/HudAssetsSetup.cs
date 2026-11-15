using System.IO;
using Parkside.UI.Hud;
using UnityEditor;
using UnityEngine;

namespace Parkside.Editor
{
    /// <summary>
    /// Imports the presentation HUD art (exported from Figma "HUD export (Unity)") as UI sprites and fills
    /// Resources/ParksideHud/HudAssets with them, the Asta Sans fonts and the shared player car (Tucson).
    /// </summary>
    public static class HudAssetsSetup
    {
        public const string Folder = "Assets/_Parkside/Resources/ParksideHud/";
        private const string Fonts = "Assets/_Parkside/UI/Fonts/AstaSans-";
        private const string Car = "Assets/ParkingLotEnvironment/RealVehicles/hyundai-tucson/hyundai-tucson.prefab";

        [MenuItem("PARKSIDE/Update HUD assets")]
        public static string Build()
        {
            foreach (var path in Directory.GetFiles(Folder, "*.png"))
            {
                var assetPath = path.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                var name = Path.GetFileNameWithoutExtension(assetPath);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 4096;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                var border = name == "round20" ? 20f : name == "round14" ? 14f : 0f;
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.SaveAndReimport();
            }
            var assetFile = Folder + "HudAssets.asset";
            var assets = AssetDatabase.LoadAssetAtPath<HudAssets>(assetFile);
            if (assets == null) { assets = ScriptableObject.CreateInstance<HudAssets>(); AssetDatabase.CreateAsset(assets, assetFile); }
            assets.regular = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Regular.ttf");
            assets.medium = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "Medium.ttf");
            assets.semiBold = AssetDatabase.LoadAssetAtPath<Font>(Fonts + "SemiBold.ttf");
            assets.playerCar = AssetDatabase.LoadAssetAtPath<GameObject>(Car);
            Sprite S(string n) => AssetDatabase.LoadAssetAtPath<Sprite>(Folder + n + ".png");
            assets.round20 = S("round20"); assets.round14 = S("round14"); assets.circle = S("circle"); assets.arrow = S("arrow"); assets.carIcon = S("car-icon");
            assets.cockpit = S("cockpit"); assets.wheel = S("wheel"); assets.handLeft = S("hand-left"); assets.handRight = S("hand-right");
            assets.mirrorRearHousing = S("mirror-rear-housing"); assets.mirrorRearMask = S("mirror-rear-mask");
            assets.mirrorLeftHousing = S("mirror-left-housing"); assets.mirrorLeftMask = S("mirror-left-mask");
            assets.mirrorRightHousing = S("mirror-right-housing"); assets.mirrorRightMask = S("mirror-right-mask");
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            var missing = new SerializedObject(assets);
            var report = "";
            var it = missing.GetIterator();
            for (var enter = true; it.NextVisible(enter); enter = false)
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == null && it.name != "m_Script") report += it.name + " ";
            return report.Length == 0 ? "HUD assets ok" : "HUD assets missing: " + report;
        }
    }
}
