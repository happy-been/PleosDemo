using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot
{
    public static class CloudPilotThinLeafPass
    {
        const string S="Assets/_Parkside/Scenes/ParklingLot.unity";
        const string K="Assets/ParkingLotEnvironment/CloudPilot";
        const string W=K+"/ThinLeafLighting";
        const string Expected="50571c23cf4290af1237936f36ecd8b5c67416f445ec046c13db2ac806f774e7";
        static string Hash(string p){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant();}
        public static void Apply()
        {
            string output=null;
            try
            {
                if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");
                if(Hash(S)!=Expected)throw new Exception("Accepted scene changed");
                var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-parkingLeafOutput");if(arg<0||arg+1>=args.Length)throw new Exception("Missing output");
                output=Path.GetFullPath(args[arg+1]);Directory.CreateDirectory(output);
                EditorSceneManager.OpenScene(S);
                var roots=SceneManager.GetActiveScene().GetRootGameObjects();var root=roots.Single(g=>g.name=="ParkingLot_CornerPilot_Root");
                if(roots.Single(g=>g.name=="ParkingLot_V35_Preserved").activeSelf)throw new Exception("Preserved root active");
                var old=AssetDatabase.LoadAssetAtPath<Material>(K+"/Canopy/LeafDoubleSided.mat");
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(W+"/ThinLeafLighting.shader");
                if(!old||!shader||!shader.isSupported)throw new Exception("Missing/supported leaf source");
                var leaves=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.IndexOf("leaves",StringComparison.OrdinalIgnoreCase)>=0 && r.sharedMaterials.Contains(old)).ToArray();
                if(leaves.Length!=11)throw new Exception("Expected11 tree foliage renderers before editing,got"+leaves.Length);
                var replacement=AssetDatabase.LoadAssetAtPath<Material>(W+"/ThinLeaf.mat");
                if(replacement)
                {
                    if(root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.sharedMaterials.Contains(replacement)))throw new Exception("Candidate already referenced");
                    replacement.CopyPropertiesFromMaterial(old);replacement.shader=shader;EditorUtility.SetDirty(replacement);
                }
                else {replacement=new Material(old){name="ThinLeaf_LocalLightScattering",shader=shader};AssetDatabase.CreateAsset(replacement,W+"/ThinLeaf.mat");}
                int count=0;
                foreach(var renderer in leaves)
                {
                    var mats=renderer.sharedMaterials;bool changed=false;
                    for(int i=0;i<mats.Length;i++)if(mats[i]==old){mats[i]=replacement;changed=true;count++;}
                    if(changed){renderer.sharedMaterials=mats;EditorUtility.SetDirty(renderer);if(PrefabUtility.IsPartOfPrefabInstance(renderer))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
                }
                if(count!=11)throw new Exception("Expected11 leaf renderers,got"+count);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);
                var notes=new StringBuilder();notes.AppendLine("Whole-canopy thin-leaf lighting trial:11 existing foliage renderers,one new shader/material. Current opaque StandardSpecular plus attenuated direct backlight and wrap. Not a physically complete subsurface-scattering implementation.");
                notes.AppendLine("Exact prior albedo texture,tint,alpha cutoff,geometry/normals,tree size/count/positions,GI,lights,camera,grade and other materials unchanged. No emission,no fixed ambient lift,no rebake. Original shader and material untouched.");
                foreach(var m in ShaderUtil.GetShaderMessages(shader)){notes.AppendLine(m.severity+":"+m.message);if(m.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)throw new Exception("Shader error");}
                notes.AppendLine("Shader supported="+shader.isSupported+" shade="+QualitySettings.shadows+" cameraPath="+root.GetComponentInChildren<Camera>().renderingPath);
                notes.AppendLine("Compare actual eye/aerial before accepting. No finer leaf micro-iteration.");notes.AppendLine("SHA256="+Hash(S));File.WriteAllText(output+"/thin-leaf-report.txt",notes.ToString());EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        }
    }
}
