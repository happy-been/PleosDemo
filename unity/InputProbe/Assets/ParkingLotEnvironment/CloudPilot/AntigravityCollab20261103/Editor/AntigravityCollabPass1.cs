using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot {
    /// <summary>
    /// Antigravity All-In-One Photorealistic Overhaul (Pass 5 - Photorealistic OEM Automotive Paint, Clear-Coat Horizon
    /// Anisotropy, Smoked Optical Headlights, Gunmetal Alloy Wheels, Charcoal Tires, Calibrated Korean Asphalt & 3D Buildings):
    /// 1. Upgrades ALL 7 vehicles in EXISTING_VEHICLES_UNAPPROVED to `AntigravityCarPaint.shader` with authentic Korean OEM
    ///    paint colors (Creamy White Pearl, Shimmering Silver Metallic, Titan Grey Gunmetal, Hyper White Pearl, Abyss Charcoal,
    ///    Nocturne Grey, and Silver Metallic), eliminating flat Standard/DoubleSidedPBR plastic shading and fixing the Azera trunk bug.
    /// 2. Upgrades all vehicle headlights, grilles, alloy wheels, tires, and black cladding so there are no milky-white/beige
    ///    headlights, glowing white rims, or crushed 0.0 black tires.
    /// 3. Preserves unified Korean crushed-stone asphalt (`AntigravityAsphalt.shader`), white stall lines, analytical under-car
    ///    contact shadows (`_CarRect0..6`), and 4-sided 3D Korean buildings (`AntigravityPreLitBuilding.shader`).
    /// </summary>
    public static class AntigravityCollabPass1 {
        const string CollabFolder = "Assets/ParkingLotEnvironment/CloudPilot/AntigravityCollab20261103";
        const string AssetsSubfolder = CollabFolder + "/Assets3D";

        public static void ApplyVisualFixesToOpenScene(GameObject root) {
            Directory.CreateDirectory(CollabFolder);
            Directory.CreateDirectory(AssetsSubfolder);

            var glassShader = AssetDatabase.LoadAssetAtPath<Shader>(CollabFolder + "/AntigravityGlass.shader");
            var foliageShader = AssetDatabase.LoadAssetAtPath<Shader>(CollabFolder + "/AntigravityFoliage.shader");
            var postShader = AssetDatabase.LoadAssetAtPath<Shader>(CollabFolder + "/AntigravityPhotoFinish.shader");
            var paintShader = AssetDatabase.LoadAssetAtPath<Shader>(CollabFolder + "/AntigravityCarPaint.shader");
            var prelitBldgShader = AssetDatabase.LoadAssetAtPath<Shader>(CollabFolder + "/AntigravityPreLitBuilding.shader");
            var asphaltShader = AssetDatabase.LoadAssetAtPath<Shader>(CollabFolder + "/AntigravityAsphalt.shader");

            if (!glassShader || !foliageShader || !postShader || !paintShader) {
                throw new Exception("Missing core Antigravity shaders in " + CollabFolder);
            }

            // 0. Build PBR / Pre-Lit materials from our generated photorealistic texture atlases
            var facade1Mat = CreateBuildingMaterial("AG_KoreanFacade1_PreLit", "korean_building_facade", prelitBldgShader);
            var facade2Mat = CreateBuildingMaterial("AG_KoreanFacade2_PreLit", "korean_buildings_pack2", prelitBldgShader);
            var propsMat = CreateBuildingMaterial("AG_KoreanStreetProps_PreLit", "korean_street_props", prelitBldgShader);
            var asphaltMat = CreateAsphaltMaterial("AG_KoreanAsphalt_PBR", "korean_asphalt", asphaltShader, root);

            // 1. Replace visible procedural buildings with 4-sided 3D buildings
            InstallPhotorealistic3DAssets(root, facade1Mat, facade2Mat, propsMat);

            // 2. Comprehensive Photorealistic OEM Vehicle Paint, Optical Headlights, Wheels, Tires & Trim for ALL 7 Vehicles
            var vehicles = root.transform.Find("EXISTING_VEHICLES_UNAPPROVED");
            if (vehicles != null) {
                UpgradeAllVehiclesPhotorealistic(vehicles, paintShader, glassShader);
            }

            // 3. Foliage, Tree Bark, and Inner + Outer Asphalt Road Surfaces (strictly excluding EXISTING_VEHICLES_UNAPPROVED!)
            var leafMap = new Dictionary<Material, Material>();
            var barkMap = new Dictionary<Material, Material>();
            var weatherMap = new Dictionary<Material, Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) {
                if (!r.enabled) continue;
                if (vehicles != null && r.transform.IsChildOf(vehicles)) continue;
                string goName = r.gameObject.name.ToLowerInvariant();
                if (goName.StartsWith("ag_")) continue;

                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++) {
                    var m = mats[i];
                    if (m == null || m.shader == null) continue;
                    string sName = m.shader.name;
                    string sNameLow = sName.ToLowerInvariant();
                    string mName = m.name.ToLowerInvariant();

                    bool isBark = mName.Contains("bark") || mName.Contains("trunk") || mName.Contains("branch") || mName.Contains("wood");
                    bool isLeaf = !isBark && (sName.Contains("RealismFoliage") || sName.Contains("LeafCutout") || sName.Contains("AntigravityFoliage") || mName.Contains("leaf") || mName.Contains("leaves"));

                    bool isPaintOrCurb = mName.Contains("line") || mName.Contains("mark") || mName.Contains("paint")
                        || goName.Contains("paint") || goName.Contains("line") || goName.Contains("bay_")
                        || goName.Contains("curb") || goName.Contains("kerb") || goName.Contains("footway")
                        || goName.Contains("sidewalk") || goName.Contains("wall") || goName.Contains("strip")
                        || goName.Contains("wheelstop") || goName.Contains("contact") || goName.Contains("sign")
                        || goName.Contains("pole") || goName.Contains("cable");

                    bool isAsphaltGround = asphaltMat != null && !isPaintOrCurb && (
                        sNameLow.Contains("koreanground") || sNameLow.Contains("layeredroad")
                        || mName.Contains("layeredroad") || mName.Contains("asphalt")
                        || goName.Contains("asphalt") || goName.Contains("finalground")
                        || goName.Contains("context_ground") || goName == "rearstreet"
                        || goName.Contains("access_road") || goName.Contains("access_lane")
                        || goName.Contains("east_lane")
                    );

                    if (isLeaf) {
                        if (!leafMap.TryGetValue(m, out var clone)) {
                            clone = new Material(m) { name = "AG_Leaf_" + m.name, shader = foliageShader };
                            clone.SetColor("_Color", Color.white);
                            clone.SetFloat("_Cutoff", 0.44f);
                            clone.SetFloat("_SunTransmission", 0.25f);
                            string path = AssetDatabase.GenerateUniqueAssetPath(CollabFolder + "/" + clone.name + ".mat");
                            AssetDatabase.CreateAsset(clone, path);
                            leafMap[m] = clone;
                        }
                        mats[i] = clone;
                        changed = true;
                    } else if (isBark) {
                        if (!barkMap.TryGetValue(m, out var bClone)) {
                            bClone = new Material(m) { name = "AG_Bark_" + m.name };
                            if (bClone.HasProperty("_Color")) bClone.SetColor("_Color", new Color(0.52f, 0.45f, 0.39f, 1.0f));
                            if (bClone.HasProperty("_Glossiness")) bClone.SetFloat("_Glossiness", 0.10f);
                            string path = AssetDatabase.GenerateUniqueAssetPath(CollabFolder + "/" + bClone.name + ".mat");
                            AssetDatabase.CreateAsset(bClone, path);
                            barkMap[m] = bClone;
                        }
                        mats[i] = bClone;
                        changed = true;
                    } else if (isAsphaltGround) {
                        mats[i] = asphaltMat;
                        changed = true;
                    } else if (m.HasProperty("_Weathering")) {
                        if (!weatherMap.TryGetValue(m, out var wClone)) {
                            wClone = new Material(m) { name = "AG_CleanWall_" + m.name };
                            wClone.SetFloat("_Weathering", 0.25f);
                            string path = AssetDatabase.GenerateUniqueAssetPath(CollabFolder + "/" + wClone.name + ".mat");
                            AssetDatabase.CreateAsset(wClone, path);
                            weatherMap[m] = wClone;
                        }
                        mats[i] = wClone;
                        changed = true;
                    }
                }
                if (changed) {
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                }
            }

            // 4. Position left-front sidewalk tree so trunk is behind Tucson (0% obstruction) while upper canopy casts crisp leaf shadows
            var frontCanopy = root.transform.Find("Integrated_FrontCanopy");
            if (frontCanopy != null) {
                var tree2 = frontCanopy.Find("Integrated_FrontTree_2");
                if (tree2 != null) {
                    tree2.position = new Vector3(-8.6f, 0.13f, -10.6f);
                    tree2.rotation = Quaternion.Euler(0f, 35f, 0f);
                    tree2.localScale = Vector3.one * 1.30f;
                    foreach (var col in tree2.GetComponentsInChildren<Collider>(true)) {
                        UnityEngine.Object.DestroyImmediate(col);
                    }
                    EditorUtility.SetDirty(tree2);
                }
            }

            // 5. Balanced Sun & Ambient intensity
            var sun = root.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun != null) {
                sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.38f, -0.68f, 0.62f).normalized);
                sun.intensity = 0.88f;
                sun.color = new Color(1.0f, 0.97f, 0.92f, 1.0f);
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.80f;
                EditorUtility.SetDirty(sun);
                EditorUtility.SetDirty(sun.transform);
            }

            RenderSettings.ambientIntensity = 0.85f;

            // 6. Camera Post-Processing
            var cam = root.GetComponentInChildren<Camera>(true);
            if (cam != null) {
                var integratedFx = cam.GetComponent<ParkingLotEnvironment.CloudPilot.Integrated.ParkingLotIntegratedPhotoFinish>();
                if (integratedFx != null) {
                    integratedFx.shader = postShader;
                    integratedFx.contactStrength = 0.65f;
                    integratedFx.sampleRadius = 0.58f;
                    EditorUtility.SetDirty(integratedFx);
                }
            }
        }

        static Material CreateCarPaintAsset(Shader paintShader, string name, Color col, float metallic, float gloss, float coat, float coatRough) {
            var mat = new Material(paintShader) { name = name };
            mat.SetColor("_Color", col);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", gloss);
            mat.SetFloat("_CoatStrength", coat);
            mat.SetFloat("_CoatRoughness", coatRough);
            string path = AssetDatabase.GenerateUniqueAssetPath(CollabFolder + "/" + name + ".mat");
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void UpgradeAllVehiclesPhotorealistic(Transform vehicles, Shader paintShader, Shader glassShader) {
            // 7 OEM Korean Automotive Paint Materials calibrated to target photo (from front z=-6.25 to back z=+8.75)
            // 0: UNAPPROVED_hyundai-tucson_0 (z=-6.25) -> Clean Creamy White Pearl (SWP)
            var paintTucson0 = CreateCarPaintAsset(paintShader, "AG_OEM_Tucson0_PearlWhite", new Color(0.92f, 0.93f, 0.95f, 1f), 0.16f, 0.90f, 0.85f, 0.06f);
            // 1: UNAPPROVED_hyundai-i30_1 (z=-3.75) -> Bright Liquid Silver Metallic (R2T) matching target photo Avante/i30!
            var paintI30_1 = CreateCarPaintAsset(paintShader, "AG_OEM_I30_1_LiquidSilver", new Color(0.80f, 0.82f, 0.84f, 1f), 0.28f, 0.88f, 0.85f, 0.06f);
            // 2: UNAPPROVED_hyundai-ioniq5-openx_2 (z=-1.25) -> Titan Grey / Dark Gunmetal Metallic (R4G)
            var paintIoniq2 = CreateCarPaintAsset(paintShader, "AG_OEM_Ioniq2_TitanGunmetal", new Color(0.24f, 0.25f, 0.27f, 1f), 0.35f, 0.88f, 0.85f, 0.06f);
            // 3: UNAPPROVED_hyundai-azera_3 (z=+1.25) -> Warm Pearl Silver White Sedan
            var paintAzera3 = CreateCarPaintAsset(paintShader, "AG_OEM_Azera3_PearlSilverWhite", new Color(0.78f, 0.80f, 0.82f, 1f), 0.26f, 0.88f, 0.85f, 0.06f);
            // 4: UNAPPROVED_hyundai-i30_4 (z=+3.75) -> Deep Navy / Charcoal Metallic
            var paintI30_4 = CreateCarPaintAsset(paintShader, "AG_OEM_I30_4_AbyssCharcoal", new Color(0.18f, 0.20f, 0.24f, 1f), 0.35f, 0.88f, 0.85f, 0.06f);
            // 5: UNAPPROVED_hyundai-tucson_5 (z=+6.25) -> Nocturne Grey Metallic
            var paintTucson5 = CreateCarPaintAsset(paintShader, "AG_OEM_Tucson5_NocturneGrey", new Color(0.32f, 0.34f, 0.36f, 1f), 0.35f, 0.88f, 0.85f, 0.06f);
            // 6: UNAPPROVED_hyundai-azera_6 (z=+8.75) -> Classic Silver Metallic
            var paintAzera6 = CreateCarPaintAsset(paintShader, "AG_OEM_Azera6_SilverMetallic", new Color(0.76f, 0.78f, 0.80f, 1f), 0.28f, 0.88f, 0.85f, 0.06f);

            // Shared non-body automotive trim materials
            var matAlloyWheel = CreateCarPaintAsset(paintShader, "AG_OEM_SilverAlloyWheel", new Color(0.72f, 0.74f, 0.76f, 1f), 0.75f, 0.82f, 0.55f, 0.08f);
            var matBrightChrome = CreateCarPaintAsset(paintShader, "AG_OEM_BrightChrome", new Color(0.88f, 0.90f, 0.92f, 1f), 0.92f, 0.92f, 0.85f, 0.04f);
            var matOpticalHeadlampHousing = CreateCarPaintAsset(paintShader, "AG_OEM_OpticalLampChrome", new Color(0.82f, 0.84f, 0.86f, 1f), 0.90f, 0.88f, 0.80f, 0.05f);
            var matCharcoalTireRubber = CreateCarPaintAsset(paintShader, "AG_OEM_CharcoalTireRubber", new Color(0.16f, 0.16f, 0.17f, 1f), 0.02f, 0.22f, 0.05f, 0.30f);
            var matBlackPlasticTrim = CreateCarPaintAsset(paintShader, "AG_OEM_BlackCladdingTrim", new Color(0.14f, 0.145f, 0.155f, 1f), 0.06f, 0.35f, 0.10f, 0.25f);
            var matTailLightRuby = CreateCarPaintAsset(paintShader, "AG_OEM_TailLightRuby", new Color(0.55f, 0.05f, 0.06f, 1f), 0.15f, 0.92f, 0.90f, 0.04f);

            var glassMap = new Dictionary<Material, Material>();

            for (int vIdx = 0; vIdx < vehicles.childCount; vIdx++) {
                var car = vehicles.GetChild(vIdx);
                string carName = car.name.ToLowerInvariant();

                Material bodyPaint = paintTucson0;
                if (carName.Contains("tucson_0")) bodyPaint = paintTucson0;
                else if (carName.Contains("i30_1")) bodyPaint = paintI30_1;
                else if (carName.Contains("ioniq5")) bodyPaint = paintIoniq2;
                else if (carName.Contains("azera_3")) bodyPaint = paintAzera3;
                else if (carName.Contains("i30_4")) bodyPaint = paintI30_4;
                else if (carName.Contains("tucson_5")) bodyPaint = paintTucson5;
                else if (carName.Contains("azera_6")) bodyPaint = paintAzera6;

                foreach (var r in car.GetComponentsInChildren<Renderer>(true)) {
                    string rName = r.gameObject.name.ToLowerInvariant();
                    var mats = r.sharedMaterials;
                    bool changed = false;

                    for (int i = 0; i < mats.Length; i++) {
                        var m = mats[i];
                        if (m == null || m.shader == null) continue;
                        string mName = m.name.ToLowerInvariant();
                        string sName = m.shader.name.ToLowerInvariant();

                        // 1. Glass windows & transparent headlamp lenses
                        bool isGlassOrLens = sName.Contains("glass")
                            || mName.Contains("glass") || mName.Contains("window") || mName.Contains("windshiel")
                            || mName.Contains("rear_wind") || mName.Contains("lens_smoked")
                            || rName == "glass" || rName.StartsWith("window") || rName == "windshiel" || rName == "rear_wind"
                            || rName == "body_glass" || rName == "lens_smoked"
                            || rName == "headlight-0-1" || rName == "headlight-0-2"
                            || rName == "fog_lamp-0-1" || rName == "fog_lamp-0-2"
                            || rName == "head_ligh" || rName == "lamps_fro_1" || rName == "lamps_fro_2";

                        // 2. Car Body Paint (covers Tucson varnish, i30 paint/Street_Vehicle_1_3/4_39, Ioniq5 Body/2_7, Azera panels & trunk!)
                        bool isBodyPaint = rName == "varnish" || rName == "paint" || rName == "body"
                            || rName == "front" || rName == "front_bum" || rName == "front_doo" || rName == "front_doo_1"
                            || rName == "hood" || rName == "rear_bump" || rName == "rear_do38" || rName == "rear_do38_1"
                            || rName == "rear_door" || rName == "rear_door_1" || rName == "right_doo" || rName == "right_doo_1"
                            || rName == "roof" || rName == "tail" || rName == "tail_wi39" || rName == "tail_wi39_1"
                            || rName == "tail_wind" || rName == "tail_wind_1"
                            || rName == "trunk"
                            || rName == "object11" || rName == "object13"
                            || (carName.Contains("i30") && (rName == "black1" || rName == "black0" || rName == "color_c01"))
                            || mName.Contains("vehiclepaint") || mName.Contains("silvergrey_street_vehicle")
                            || mName.Contains("charcoal_street_vehicle") || mName.Contains("deepblue_street_vehicle")
                            || mName.Contains("street_vehicle_1_3");

                        // 3. Alloy Wheels & Brake Discs
                        bool isWheel = rName == "metal" || rName == "metal_dark" || rName == "wheel1" || rName == "disc"
                            || rName.Contains("volk_raing_wheel") || rName.StartsWith("rim_") || rName.StartsWith("brake")
                            || rName.StartsWith("cylinde");

                        // 4. Tires
                        bool isTire = rName.Contains("wheel_rubber") || rName.Contains("tire") || rName.Contains("tyre");

                        // 5. Front Grille & Chrome Accents
                        bool isGrilleOrChrome = rName.Contains("chrome") || rName.Contains("radiator") || rName.Contains("muffler")
                            || rName.Contains("exhust") || rName.Contains("mirror") || rName == "trunk_1" || rName == "trunk_2";

                        // 6. Inner Headlamp / Blinker / Foglamp Housings (prevent milky-white or toy-orange blobs!)
                        bool isInnerLamp = rName == "front_lamps" || rName == "blinker_lamps"
                            || rName.StartsWith("headlight-") || rName.StartsWith("foglamp-")
                            || rName == "head_lig2" || rName == "head_lig2_1" || rName == "lamps_fro" || rName == "lamps_fro_3"
                            || rName == "lamp_white" || rName == "lamp_projector" || rName == "lamp_amber"
                            || rName == "white_matte"
                            || rName == "color_a04" || rName == "color_a08" || rName == "color_b03"
                            || rName == "color_b05" || rName == "color_b06" || rName.StartsWith("material_3") || rName == "-edit";

                        // 7. Tail Lights
                        bool isTailLight = rName.Contains("brakelight") || rName.Contains("rearlight")
                            || rName.StartsWith("r-lamp") || rName == "lamp-red" || rName == "lamp_red"
                            || rName == "tail_ligh" || rName.Contains("reflector_red") || rName == "back_blinkers";

                        // 8. Black Plastic Cladding / Bumpers / Trim
                        bool isBlackTrim = (rName.Contains("black") || rName.Contains("plastic") || rName.Contains("trim")
                            || rName.Contains("garnish") || rName.Contains("pixel") || rName == "belly_pan"
                            || rName == "roof_seal" || rName == "front_bum_1" || rName == "front_bum_2"
                            || rName == "rear_bump_1" || rName == "sub01" || rName == "sub02")
                            && !(carName.Contains("i30") && (rName == "black1" || rName == "black0" || rName == "color_c01"));

                        if (isGlassOrLens) {
                            if (!glassMap.TryGetValue(m, out var gClone)) {
                                gClone = new Material(m) { name = "AG_Glass_" + m.name, shader = glassShader };
                                gClone.SetColor("_Color", new Color(0.02f, 0.03f, 0.028f, 1.0f));
                                gClone.SetFloat("_Glossiness", 0.96f);
                                string path = AssetDatabase.GenerateUniqueAssetPath(CollabFolder + "/" + gClone.name + ".mat");
                                AssetDatabase.CreateAsset(gClone, path);
                                glassMap[m] = gClone;
                            }
                            mats[i] = gClone;
                            changed = true;
                        } else if (isBodyPaint) {
                            mats[i] = bodyPaint;
                            changed = true;
                        } else if (isWheel) {
                            mats[i] = matAlloyWheel;
                            changed = true;
                        } else if (isTire) {
                            mats[i] = matCharcoalTireRubber;
                            changed = true;
                        } else if (isGrilleOrChrome) {
                            mats[i] = matBrightChrome;
                            changed = true;
                        } else if (isInnerLamp) {
                            mats[i] = matOpticalHeadlampHousing;
                            changed = true;
                        } else if (isTailLight) {
                            mats[i] = matTailLightRuby;
                            changed = true;
                        } else if (isBlackTrim) {
                            mats[i] = matBlackPlasticTrim;
                            changed = true;
                        }
                    }

                    if (changed) {
                        r.sharedMaterials = mats;
                        EditorUtility.SetDirty(r);
                        if (PrefabUtility.IsPartOfPrefabInstance(r)) {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
                        }
                    }
                    r.receiveShadows = true;
                }
            }
        }

        static Material CreateBuildingMaterial(string matName, string texPrefix, Shader prelitShader) {
            string albedoPath = AssetsSubfolder + "/" + texPrefix + "_albedo.png";
            string normalPath = AssetsSubfolder + "/" + texPrefix + "_normal.png";
            string mosPath = AssetsSubfolder + "/" + texPrefix + "_metallic_smoothness.png";

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (albedo == null) return null;

            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && normalImporter.textureType != TextureImporterType.NormalMap) {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
            }

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var mos = AssetDatabase.LoadAssetAtPath<Texture2D>(mosPath);

            var shaderToUse = (prelitShader != null && prelitShader.isSupported) ? prelitShader : Shader.Find("Standard");
            var mat = new Material(shaderToUse) { name = matName };
            mat.SetTexture("_MainTex", albedo);
            if (normal != null) {
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 0.75f);
            }
            if (mos != null) {
                mat.EnableKeyword("_METALLICGLOSSMAP");
                mat.SetTexture("_MetallicGlossMap", mos);
            }
            string matPath = AssetDatabase.GenerateUniqueAssetPath(CollabFolder + "/" + matName + ".mat");
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        static Material CreateAsphaltMaterial(string matName, string texPrefix, Shader asphaltShader, GameObject root) {
            string albedoPath = AssetsSubfolder + "/" + texPrefix + "_albedo.png";
            string normalPath = AssetsSubfolder + "/" + texPrefix + "_normal.png";

            var albedoImporter = AssetImporter.GetAtPath(albedoPath) as TextureImporter;
            if (albedoImporter != null && albedoImporter.anisoLevel < 8) {
                albedoImporter.anisoLevel = 9;
                albedoImporter.wrapMode = TextureWrapMode.Repeat;
                albedoImporter.SaveAndReimport();
            }
            var normalImporter = AssetImporter.GetAtPath(normalPath) as TextureImporter;
            if (normalImporter != null && (normalImporter.textureType != TextureImporterType.NormalMap || normalImporter.anisoLevel < 8)) {
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.anisoLevel = 9;
                normalImporter.wrapMode = TextureWrapMode.Repeat;
                normalImporter.SaveAndReimport();
            }

            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (albedo == null || asphaltShader == null) return null;

            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            var mat = new Material(asphaltShader) { name = matName };
            mat.SetTexture("_MainTex", albedo);
            if (normal != null) mat.SetTexture("_BumpMap", normal);

            // Populate exact world-space XZ footprints of all 7 vehicles for analytical under-car contact shadows
            var vehicles = root.transform.Find("EXISTING_VEHICLES_UNAPPROVED");
            if (vehicles != null) {
                int count = Mathf.Min(7, vehicles.childCount);
                for (int i = 0; i < count; i++) {
                    var vb = GetRendererBounds(vehicles.GetChild(i));
                    if (vb.size.x > 0.5f && vb.size.z > 0.5f) {
                        mat.SetVector("_CarRect" + i, new Vector4(vb.center.x, vb.center.z, vb.extents.x, vb.extents.z));
                    }
                }
            }

            string matPath = AssetDatabase.GenerateUniqueAssetPath(CollabFolder + "/" + matName + ".mat");
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        static void InstallPhotorealistic3DAssets(GameObject root, Material facade1Mat, Material facade2Mat, Material propsMat) {
            var holder = root.transform.Find("AG_Photorealistic3D_Assets");
            if (holder != null) UnityEngine.Object.DestroyImmediate(holder.gameObject);
            holder = new GameObject("AG_Photorealistic3D_Assets").transform;
            holder.SetParent(root.transform, false);

            var villaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsSubfolder + "/KoreanBuilding_IvoryVilla.obj");
            var brickPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsSubfolder + "/KoreanBuilding_RedBrickCafe.obj");
            var laundryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsSubfolder + "/KoreanBuilding_LaundryRealEstate.obj");
            var aptPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsSubfolder + "/KoreanBuilding_Apartment102.obj");

            // 1A. Replace visible procedural buildings under DaylightPass_Context with 4-sided photorealistic 3D buildings
            var contextGroup = root.transform.Find("DaylightPass_Context");
            if (contextGroup != null && facade1Mat != null) {
                int midRiseIdx = 0;
                bool placedApt = false;
                foreach (Transform child in contextGroup) {
                    if (!child.name.Contains("Building")) continue;
                    foreach (var r in child.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                    var b = GetRendererBounds(child);
                    if (b.size.x < 4.0f || b.size.z < 4.0f || b.size.y < 5.0f) continue;

                    GameObject chosenPrefab = null;
                    Material chosenMat = null;
                    if (b.size.y > 18.0f) {
                        if (!placedApt && aptPrefab != null && facade2Mat != null) {
                            chosenPrefab = aptPrefab;
                            chosenMat = facade2Mat;
                            placedApt = true;
                        } else {
                            continue;
                        }
                    } else {
                        if (midRiseIdx % 3 == 0 && laundryPrefab != null && facade2Mat != null) {
                            chosenPrefab = laundryPrefab;
                            chosenMat = facade2Mat;
                        } else if (midRiseIdx % 3 == 1 && brickPrefab != null) {
                            chosenPrefab = brickPrefab;
                            chosenMat = facade1Mat;
                        } else {
                            chosenPrefab = villaPrefab;
                            chosenMat = facade1Mat;
                        }
                        midRiseIdx++;
                    }

                    if (chosenPrefab == null || chosenMat == null) continue;

                    foreach (var origRenderer in child.GetComponentsInChildren<Renderer>(true)) {
                        origRenderer.enabled = false;
                        EditorUtility.SetDirty(origRenderer);
                    }

                    var inst = UnityEngine.Object.Instantiate(chosenPrefab, holder);
                    inst.name = "AG_3D_" + child.name;
                    inst.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
                    inst.transform.rotation = Quaternion.identity;
                    var srcB = GetRendererBounds(inst.transform);
                    if (srcB.size.x > 0.1f && srcB.size.y > 0.1f && srcB.size.z > 0.1f) {
                        inst.transform.localScale = new Vector3(
                            b.size.x / srcB.size.x,
                            b.size.y / srcB.size.y,
                            b.size.z / srcB.size.z
                        );
                    }
                    AssignMaterialRecursively(inst, chosenMat, true);
                }
            }

            // 1B. Replace the 3 left-side procedural buildings (PhotoStreet_Building_0..2) behind the parked cars!
            string[] leftNames = { "PhotoStreet_Building_0", "PhotoStreet_Building_1", "PhotoStreet_Building_2" };
            GameObject[] leftPrefabs = { brickPrefab, villaPrefab, laundryPrefab };
            Material[] leftMats = { facade1Mat, facade1Mat, facade2Mat };
            for (int li = 0; li < leftNames.Length; li++) {
                var leftChild = root.transform.Find(leftNames[li]);
                var lPrefab = leftPrefabs[li];
                var lMat = leftMats[li];
                if (leftChild == null || lPrefab == null || lMat == null) continue;
                foreach (var r in leftChild.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                var b = GetRendererBounds(leftChild);
                if (b.size.x < 2.0f || b.size.z < 2.0f || b.size.y < 4.0f) continue;

                foreach (var origRenderer in leftChild.GetComponentsInChildren<Renderer>(true)) {
                    origRenderer.enabled = false;
                    EditorUtility.SetDirty(origRenderer);
                }

                var inst = UnityEngine.Object.Instantiate(lPrefab, holder);
                inst.name = "AG_3D_" + leftNames[li];
                inst.transform.position = Vector3.zero;
                inst.transform.rotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                var srcB = GetRendererBounds(inst.transform);
                if (srcB.size.x > 0.1f && srcB.size.y > 0.1f && srcB.size.z > 0.1f) {
                    // When rotated -90 deg around Y (so -Z front wall faces +X into parking lot):
                    // local X spans world Z, and local Z spans world X!
                    inst.transform.localScale = new Vector3(
                        b.size.z / srcB.size.x,
                        b.size.y / srcB.size.y,
                        b.size.x / srcB.size.z
                    );
                }
                inst.transform.position = new Vector3(b.center.x, b.min.y, b.center.z);
                inst.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                AssignMaterialRecursively(inst, lMat, true);
            }

            // 1C. Attach 3D Korean License Plates (1.2cm in front of bumper!), Parking Sign, and Drain Grate Module
            if (propsMat != null) {
                var plate1Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsSubfolder + "/KoreanLicensePlate_165ga7842.obj");
                var tucson = root.transform.Find("EXISTING_VEHICLES_UNAPPROVED/UNAPPROVED_hyundai-tucson_0");
                if (plate1Prefab != null && tucson != null) {
                    var bounds = GetRendererBounds(tucson);
                    var p1 = UnityEngine.Object.Instantiate(plate1Prefab, holder);
                    p1.name = "AG_Tucson_LicensePlate";
                    p1.transform.position = new Vector3(bounds.max.x + 0.012f, bounds.min.y + 0.45f, bounds.center.z);
                    p1.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                    AssignMaterialRecursively(p1, propsMat, false);
                }

                var plate2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsSubfolder + "/KoreanLicensePlate_28ra9176.obj");
                var vehicles = root.transform.Find("EXISTING_VEHICLES_UNAPPROVED");
                if (plate2Prefab != null && vehicles != null && vehicles.childCount > 1) {
                    var bounds2 = GetRendererBounds(vehicles.GetChild(1));
                    var p2 = UnityEngine.Object.Instantiate(plate2Prefab, holder);
                    p2.name = "AG_SecondCar_LicensePlate";
                    p2.transform.position = new Vector3(bounds2.max.x + 0.012f, bounds2.min.y + 0.40f, bounds2.center.z);
                    p2.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                    AssignMaterialRecursively(p2, propsMat, false);
                }

                var signPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsSubfolder + "/KoreanParkingSign_3D.obj");
                if (signPrefab != null) {
                    var sign = UnityEngine.Object.Instantiate(signPrefab, holder);
                    sign.name = "AG_KoreanParkingSign";
                    sign.transform.position = new Vector3(9.6f, 0.01f, 14.5f);
                    sign.transform.rotation = Quaternion.identity;
                    AssignMaterialRecursively(sign, propsMat, true);
                }

                var curbPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetsSubfolder + "/KoreanCurbAndDrainModule.obj");
                if (curbPrefab != null) {
                    var c = UnityEngine.Object.Instantiate(curbPrefab, holder);
                    c.name = "AG_CurbAndDrain_0";
                    c.transform.position = new Vector3(9.35f, -0.03f, -14.0f);
                    c.transform.rotation = Quaternion.identity;
                    AssignMaterialRecursively(c, propsMat, true);
                }
            }
        }

        static Bounds GetRendererBounds(Transform t) {
            var renderers = t.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static void AssignMaterialRecursively(GameObject go, Material mat, bool staticBake) {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) {
                r.sharedMaterial = mat;
                r.receiveShadows = true;
                r.shadowCastingMode = ShadowCastingMode.On;
                if (staticBake) {
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.ReflectionProbeStatic);
                }
                EditorUtility.SetDirty(r);
            }
        }
    }
}
