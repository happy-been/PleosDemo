using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
// Preparation only: produces a separate prefab, never rebuilds or edits the saved scene.
public static class CloudPilotPreparation {
    [Serializable] public class Item { public string name,asset,material; public Vector3 position; public float yaw; public bool collider; }
    [Serializable] public class Layout { public Item[] objects; public Vector3[] trees,vehicleSlots; public Vector3 cameraPosition; public float cameraYaw,verticalFov; }
    const string Root = "Assets/ParkingLotEnvironment/CloudPilot";
    const string Env = "Assets/ParkingLotEnvironment";
    const string TargetScene = "Assets/_Parkside/Scenes/ParklingLot.unity";
    static readonly List<string> Created = new List<string>();

    [MenuItem("ParkingLot/Cloud pilot/Prepare isolated prefab (no scene changes)")]
    public static void PreparePrefab() {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Wait for compilation and stop Play mode.");
        if (QualitySettings.activeColorSpace != ColorSpace.Gamma)
            throw new InvalidOperationException("Expected existing Gamma project. Settings will not be changed.");
        CloudPilotInspection.VerifyOriginalSnapshot();
        string before = Hash(TargetScene);
        string path = Root + "/PilotCorner.prepared.prefab";
        if (File.Exists(path)) throw new IOException("Prepared prefab already exists. Review it before replacing it.");
        Created.Clear();
        Directory.CreateDirectory(Root + "/Materials");
        AssetDatabase.Refresh();
        Scene preview = EditorSceneManager.NewPreviewScene();
        GameObject root = new GameObject("ParkingLot_CornerPilot_Root");
        SceneManager.MoveGameObjectToScene(root, preview);
        try {
            Material asphalt = Material("Asphalt", new Color(.8f,.8f,.8f), .13f,
                Env + "/KoreanAssets/aerial_asphalt_01_diff_2k.jpg", 1f);
            Material concrete = Material("Concrete", new Color(.72f,.7f,.65f), .12f,
                Env + "/KoreanAssets/concrete_wall_009_diff_2k.jpg", 1f);
            Material paving = Material("Paving", Color.white, .15f,
                Env + "/KoreanAssets/concrete_pavement_diff_2k.jpg", 1f/1.8f);
            Material brick = Material("Brick", new Color(.8f,.76f,.7f), .12f,
                Env + "/KoreanAssets/red_brick_diff_2k.jpg", 1f/1.4f);
            Material steel = Material("DrainSteel", new Color(.24f,.26f,.27f), .42f);
            steel.SetFloat("_Metallic", .75f);
            SaveCreatedMaterial(steel);
            Material dark = Material("DrainWell", new Color(.06f,.055f,.045f), .05f);
            Material paint = Material("ParkingPaint", new Color(.79f,.78f,.7f), .12f);
            Material frames = Material("WindowFrames", new Color(.45f,.48f,.47f), .45f);
            frames.SetFloat("_Metallic", .7f);
            SaveCreatedMaterial(frames);
            Material glass = Material("WindowGlass", new Color(.12f,.17f,.18f), .91f);
            Material sign = Material("SignBlue", new Color(.03f,.10f,.3f), .25f);
            Material("PlateBacking", new Color(.88f,.88f,.82f), .2f);
            // Opaque backed glazing is a provisional comparison, not validated glass realism.
            var materials=new Dictionary<string,Material> {
                {"Asphalt",asphalt},{"Concrete",concrete},{"Paving",paving},{"Brick",brick},
                {"DrainSteel",steel},{"DrainWell",dark},{"ParkingPaint",paint},
                {"WindowFrames",frames},{"WindowGlass",glass},{"SignBlue",sign}};
            Layout layout=JsonUtility.FromJson<Layout>(File.ReadAllText(Root+"/candidate-layout.json"));
            if(layout==null || layout.objects==null)throw new InvalidDataException("Missing layout objects.");
            foreach(Item item in layout.objects) {
                GameObject instance=Add(root,item.asset,materials[item.material],item.collider);
                instance.name=item.name;instance.transform.localPosition=item.position;
                instance.transform.localRotation=Quaternion.Euler(0,item.yaw,0);
            }
            Transform slots = new GameObject("Vehicle_slots_pending_quality_review").transform;
            slots.SetParent(root.transform, false);
            for (int i=0;i<layout.vehicleSlots.Length;i++) {
                Transform slot = new GameObject("Bay_"+(i+1)).transform;
                slot.SetParent(slots,false); slot.localPosition=layout.vehicleSlots[i];
            }
            // Original full tree prefab is referenced; no source assets or shared materials edited.
            foreach(Vector3 position in layout.trees)Tree(root,position);
            Camera camera=new GameObject("Pilot_PedestrianCamera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform,false);camera.transform.localPosition=layout.cameraPosition;
            camera.transform.localRotation=Quaternion.Euler(0,layout.cameraYaw,0);
            camera.fieldOfView=layout.verticalFov;camera.nearClipPlane=.08f;camera.farClipPlane=250;
            Light sun=new GameObject("Pilot_Sun_CANDIDATE_UNVALIDATED").AddComponent<Light>();
            sun.transform.SetParent(root.transform,false);sun.type=LightType.Directional;
            sun.transform.localRotation=Quaternion.Euler(43,-32,0);sun.intensity=1;
            sun.color=new Color(1,.97f,.92f);sun.shadows=LightShadows.Soft;
            ReflectionProbe probe=new GameObject("Pilot_LocalReflectionProbe").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(root.transform,false);probe.transform.localPosition=new Vector3(0,2,0);
            probe.size=new Vector3(24,16,34);probe.center=new Vector3(0,3,0);probe.boxProjection=true;
            probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode=UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.resolution=128;
            AddSignText(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Created.Add(path);
            if (Hash(TargetScene) != before) throw new InvalidOperationException("Protected scene hash changed.");
            Debug.Log("Prepared isolated pilot prefab. No vehicle quality gate, Unity render, or saved-scene application is claimed.");
        } catch {
            foreach (string created in Created) AssetDatabase.DeleteAsset(created);
            throw;
        } finally {
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    static Material Material(string name, Color color, float smoothness, string texture=null, float scale=1) {
        string path=Root+"/Materials/"+name+".mat";
        if (File.Exists(path)) throw new IOException("Material already exists; review before replacing: "+path);
        Shader shader=Shader.Find("Standard");
        if (shader==null) throw new InvalidOperationException("Built-in Standard shader missing.");
        Material mat=new Material(shader){name=name,color=color};
        mat.SetFloat("_Glossiness",smoothness);
        if (texture!=null) {
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texture);
            if (mat.mainTexture==null) throw new FileNotFoundException(texture);
            mat.mainTextureScale=new Vector2(scale,scale);
        }
        AssetDatabase.CreateAsset(mat,path);Created.Add(path);return mat;
    }
    static void SaveCreatedMaterial(Material material) {
        string path=AssetDatabase.GetAssetPath(material);
        if(!Created.Contains(path)||!path.StartsWith(Root+"/Materials/",StringComparison.Ordinal))
            throw new InvalidOperationException("Only a material created in this invocation may be saved.");
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
    }

    static GameObject Add(GameObject root,string asset,Material material,bool collider) {
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/"+asset);
        if (model==null) throw new FileNotFoundException(asset+" not imported.");
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,root.scene);
        instance.transform.SetParent(root.transform,false);
        foreach(var r in instance.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterial=material;
        if(collider) foreach(var mf in instance.GetComponentsInChildren<MeshFilter>()) {
            var mc=mf.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=mf.sharedMesh;
        }
        return instance;
    }
    static void AddSignText(GameObject root) {
        Font font=AssetDatabase.LoadAssetAtPath<Font>(Env+"/Fonts/ParkingSigns.ttf");
        if(font==null)throw new FileNotFoundException("Existing Korean font missing.");
        TextMesh text=new GameObject("Pilot_SignText").AddComponent<TextMesh>();
        text.transform.SetParent(root.transform,false);text.transform.localPosition=new Vector3(8.18f,2.88f,9.88f);
        text.font=font;text.fontSize=64;text.characterSize=.07f;text.color=Color.white;text.text="P  공영주차장";
        text.anchor=TextAnchor.MiddleLeft;text.GetComponent<MeshRenderer>().sharedMaterial=font.material;
    }
    static void Tree(GameObject root,Vector3 position) {
        string path=Env+"/RealVehicles/tree-small-real/tree-small-real.prefab";
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(source==null)throw new FileNotFoundException(path);
        var tree=(GameObject)PrefabUtility.InstantiatePrefab(source,root.scene);
        tree.transform.SetParent(root.transform,false);tree.transform.localPosition=position;
    }
    static string Hash(string path) {
        using(var s=System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(path))).Replace("-","");
    }
}
}
