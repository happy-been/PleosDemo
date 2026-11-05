using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ParkingLotEnvironment.Editor {
/// <summary>Optional details on scene instances. Source vehicle prefabs are never modified.</summary>
public static class ParkingLotVehicleDetails {
    const string Root = "Assets/ParkingLotEnvironment/RealVehicles/Details";
    const string DetailName = "ParkingLot_instance_details";
    static readonly Dictionary<string, Material> Shadows = new Dictionary<string, Material>();
    static Material plateWhite, plateFrame;
    static Mesh groundQuad;

    // The importer bakes normalization into every vertex. All three Hyundai fronts face -Z.
    public static void Add(GameObject car, string key, int index, Font font) {
        if (car == null || car.transform.Find(DetailName) != null) return;
        float length = key == "hyundai-tucson" ? 4.53f : key == "hyundai-i30" ? 4.25f :
            key == "hyundai-azera" ? 4.895f : key == "hyundai-ioniq5-openx" ? 4.657335f : key == "generico-caminhao" ? 6.02f : 4.7f;
        bool truck = key == "generico-caminhao";
        var details = new GameObject(DetailName);
        details.transform.SetParent(car.transform, false);
        details.isStatic = true;

        var shadow = new GameObject("Soft_ground_contact");
        shadow.transform.SetParent(details.transform, false);
        shadow.transform.localPosition = new Vector3(0, .03f, 0);
        shadow.transform.localScale = new Vector3(truck ? 2.95f : 2.2f, 1, length + .4f);
        shadow.AddComponent<MeshFilter>().sharedMesh = GroundQuad();
        var sr = shadow.AddComponent<MeshRenderer>();
        sr.sharedMaterial = ShadowMaterial(key, length, truck);
        QuietRenderer(sr);
        shadow.isStatic = true;

        if (font == null || truck) return;
        string registration = (123 + Math.Abs(index % 70) * 7).ToString() + "가 " +
            (1000 + Math.Abs(index % 60) * 137).ToString("D4");

        // Mounts were measured against the imported meshes at X=-.23, 0, +.23m.
        // Each face sits approximately 5-15mm beyond its existing bumper/recess.
        if (key == "hyundai-tucson") {
            Plate(details.transform, "Rear_registration", registration, new Vector3(0, .735f, 2.238f), new Vector3(-7.3f, 0, 0), font);
            Plate(details.transform, "Front_registration", registration, new Vector3(0, .45f, -2.273f), new Vector3(0, 180, 0), font);
        } else if (key == "hyundai-i30") {
            Plate(details.transform, "Rear_registration", registration, new Vector3(0, .65f, 2.065f), Vector3.zero, font);
            Plate(details.transform, "Front_registration", registration, new Vector3(0, .396f, -2.072f), new Vector3(0, 180, 0), font);
        } else if (key == "hyundai-azera") {
            Plate(details.transform, "Rear_registration", registration, new Vector3(0, .825f, 2.327f), new Vector3(-15, 0, 0), font);
            Plate(details.transform, "Front_registration", registration, new Vector3(0, .48f, -2.454f), new Vector3(-5, 180, 0), font);
        } else if (key == "hyundai-ioniq5-openx") {
            // Replacement on this scene instance only; omit the source's European-style sample registration.
            foreach(var r in car.transform.Find("Model_geometry").GetComponentsInChildren<MeshRenderer>())if(r.sharedMaterial.name.Contains("License_Plate_"))UnityEngine.Object.DestroyImmediate(r.gameObject);
            Plate(details.transform, "Rear_registration", registration, new Vector3(0, .632f, 2.302f), new Vector3(-27.5f, 0, 0), font);
            Plate(details.transform, "Front_registration", registration, new Vector3(0, .531f, -2.322f), new Vector3(0, 180, 0), font);
        }
    }

    static void Plate(Transform parent, string name, string registration, Vector3 position, Vector3 angles, Font font) {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.localPosition = position;
        root.transform.localRotation = Quaternion.Euler(angles);
        root.isStatic = true;
        if (plateFrame == null) plateFrame = Solid("Plate_frame", new Color(.035f, .04f, .044f), .28f);
        if (plateWhite == null) plateWhite = Solid("Plate_enamel", new Color(.86f, .875f, .84f), .35f);
        PlateBox(root.transform, "Thin_mounting_frame", new Vector3(.532f, .122f, .006f), Vector3.zero, plateFrame);
        PlateBox(root.transform, "White_registration_face", new Vector3(.52f, .11f, .003f), new Vector3(0, 0, .004f), plateWhite);

        var textObject = new GameObject("Registration_" + registration);
        textObject.transform.SetParent(root.transform, false);
        textObject.transform.localPosition = new Vector3(0, -.001f, .0061f);
        // TextMesh faces -Z by default; the plate's exterior is +Z.
        textObject.transform.localRotation = Quaternion.Euler(0, 180, 0);
        var text = textObject.AddComponent<TextMesh>();
        text.font = font;
        text.text = registration;
        text.fontSize = 64;
        text.characterSize = 1;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Normal;
        text.color = new Color(.025f, .03f, .032f, 1);
        var renderer = textObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = font.material;
        // Character metrics avoid dependence on the vehicle's world rotation or font-atlas timing.
        if (font.dynamic) font.RequestCharactersInTexture(registration, 64, FontStyle.Normal);
        float width = 0, height = 0;
        foreach (char character in registration) {
            CharacterInfo info;
            if (font.GetCharacterInfo(character, out info, 64, FontStyle.Normal)) {
                width += info.advance;
                height = Mathf.Max(height, info.maxY - info.minY);
            }
        }
        // TextMesh's characterSize=1 maps ten font pixels to one world unit.
        float s = width > 0 && height > 0 ? Mathf.Min(.458f / (width * .1f), .081f / (height * .1f)) : .013f;
        textObject.transform.localScale = Vector3.one * s;
        QuietRenderer(renderer);
        textObject.isStatic = true;
    }

    static void PlateBox(Transform parent, string name, Vector3 size, Vector3 position, Material material) {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = size;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        var renderer = obj.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        QuietRenderer(renderer);
        obj.isStatic = true;
    }

    static void QuietRenderer(Renderer renderer) {
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.allowOcclusionWhenDynamic = false;
    }

    static Material Solid(string name, Color color, float smoothness) {
        Directory.CreateDirectory(Root);
        string path = Root + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) return material;
        material = new Material(Shader.Find("Standard"));
        material.name = name;
        material.color = color;
        material.SetFloat("_Metallic", .05f);
        material.SetFloat("_Glossiness", smoothness);
        material.enableInstancing = true;
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    static Mesh GroundQuad() {
        if (groundQuad != null) return groundQuad;
        Directory.CreateDirectory(Root);
        string path = Root + "/Contact_ground_quad.asset";
        groundQuad = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (groundQuad != null) return groundQuad;
        groundQuad = new Mesh { name = "Contact_ground_quad" };
        groundQuad.vertices = new[] { new Vector3(-.5f,0,-.5f), new Vector3(.5f,0,-.5f), new Vector3(-.5f,0,.5f), new Vector3(.5f,0,.5f) };
        groundQuad.uv = new[] { new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1) };
        groundQuad.triangles = new[] { 0,2,1, 2,3,1 };
        groundQuad.RecalculateNormals();
        groundQuad.RecalculateBounds();
        AssetDatabase.CreateAsset(groundQuad, path);
        return groundQuad;
    }

    static Material ShadowMaterial(string key, float length, bool truck) {
        Material material;
        if (Shadows.TryGetValue(key, out material) && material != null) return material;
        Directory.CreateDirectory(Root);
        string path = Root + "/Contact_" + key + ".mat";
        material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null) { Shadows[key] = material; return material; }
        string texturePath = Root + "/Contact_" + key + ".png";
        if (!File.Exists(texturePath)) {
            const int resolution = 256;
            var texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[resolution * resolution];
            float patchWidth = truck ? 2.95f : 2.2f, patchLength = length + .4f;
            float halfWidth = truck ? 1.07f : .74f, halfLength = length * .5f - .43f;
            float track = truck ? .99f : .80f, axle = truck ? 1.72f : (length < 4.4f ? 1.29f : 1.34f);
            for (int y = 0; y < resolution; y++) for (int x = 0; x < resolution; x++) {
                float px = (x / 255f - .5f) * patchWidth, pz = (y / 255f - .5f) * patchLength;
                // Rounded body footprint with a 30cm soft falloff; white outside is multiplicative identity.
                Vector2 q = new Vector2(Mathf.Abs(px) - (halfWidth - .16f), Mathf.Abs(pz) - (halfLength - .16f));
                float distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - .16f;
                float transmission = 1 - .45f * (1 - Smooth01((distance + .02f) / .32f));
                for (int sx = -1; sx <= 1; sx += 2) for (int sz = -1; sz <= 1; sz += 2) {
                    float r = new Vector2(px - sx * track, pz - sz * axle).magnitude;
                    float wheel = 1 - .65f * (1 - Smooth01((r - .025f) / (truck ? .245f : .205f)));
                    transmission = Mathf.Min(transmission, wheel);
                }
                // The outer texel is strictly white, avoiding a visible rectangular texture boundary.
                if (x == 0 || y == 0 || x == 255 || y == 255) transmission = 1;
                byte value = (byte)Mathf.RoundToInt(transmission * 255);
                pixels[y * resolution + x] = new Color32(value, value, value, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
        AssetDatabase.ImportAsset(texturePath);
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null) {
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        var shader = Shader.Find("ParkingLot/ContactShadowMultiply");
        if (shader == null) throw new InvalidOperationException("ParkingLot contact-shadow shader has not imported yet.");
        material = new Material(shader) { name = "Contact_" + key, enableInstancing = true };
        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        AssetDatabase.CreateAsset(material, path);
        Shadows[key] = material;
        return material;
    }

    static float Smooth01(float value) {
        float t = Mathf.Clamp01(value);
        return t * t * (3 - 2 * t);
    }
}
}
