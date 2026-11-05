using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ParkingLotEnvironment.CloudPilot
{
    public static class CloudPilotVehiclePalettePass
    {
        const string S="Assets/_Parkside/Scenes/ParklingLot.unity";
        const string K="Assets/ParkingLotEnvironment/CloudPilot";
        const string W=K+"/VehiclePalette";
        const string Expected="fd9d75bf9928a637cfe5602a67c627b9ee62f003440591b82ea07ec3c3c16a7b";
        static string Hash(string p) { using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant(); }
        public static void Apply()
        {
            string output=null;
            try
            {
                if(File.Exists("/workspaces/parkside-setup/STOP"))throw new Exception("STOP");
                if(Hash(S)!=Expected)throw new Exception("Reviewed scene changed");
                if(Directory.Exists(W))throw new Exception("Single-use palette already exists");
                var args=Environment.GetCommandLineArgs();int arg=Array.IndexOf(args,"-parkingPaletteOutput");if(arg<0||arg+1>=args.Length)throw new Exception("Missing output");
                output=Path.GetFullPath(args[arg+1]);Directory.CreateDirectory(output);
                EditorSceneManager.OpenScene(S);
                var roots=SceneManager.GetActiveScene().GetRootGameObjects();
                var root=roots.Single(x=>x.name=="ParkingLot_CornerPilot_Root");
                if(roots.Single(x=>x.name=="ParkingLot_V35_Preserved").activeSelf)throw new Exception("Preserved root active");
                var group=root.transform.Find("EXISTING_VEHICLES_UNAPPROVED");if(!group||group.childCount!=7)throw new Exception("Unexpected vehicle group");
                var indexes=new[]{2,3,4};
                var before=new[]{new Color(.46f,.48f,.47f),new Color(.68f,.69f,.67f),new Color(.62f,.64f,.65f)};
                var after=new[]{new Color(.075f,.085f,.095f),new Color(.22f,.235f,.25f),new Color(.055f,.085f,.14f)};
                var labels=new[]{"Charcoal","SilverGrey","DeepBlue"};
                Directory.CreateDirectory(W);AssetDatabase.Refresh();var notes=new StringBuilder();
                notes.AppendLine("Whole vehicle-row palette: keep existing white/silver cars; change3 owned instances to charcoal,silver grey and deep blue. Generic paint choices,not official Hyundai color specifications.");
                int total=0;
                for(int n=0;n<indexes.Length;n++)
                {
                    int index=indexes[n];var car=group.GetChild(index);var clones=new Dictionary<Material,Material>();int slots=0;
                    foreach(var renderer in car.GetComponentsInChildren<Renderer>())
                    {
                        var mats=renderer.sharedMaterials;bool changed=false;
                        for(int j=0;j<mats.Length;j++)
                        {
                            var src=mats[j];if(!src)continue;string path=AssetDatabase.GetAssetPath(src);
                            if(!path.StartsWith(K+"/Materials/Street_Vehicle_"+index+"_"))continue;
                            var c=src.color;var b=before[n];if(Mathf.Abs(c.r-b.r)>.001f||Mathf.Abs(c.g-b.g)>.001f||Mathf.Abs(c.b-b.b)>.001f||Mathf.Abs(c.a-1f)>.001f)continue;
                            if(!clones.TryGetValue(src,out var replacement))
                            {
                                replacement=new Material(src){name=labels[n]+"_"+src.name,color=after[n]};
                                AssetDatabase.CreateAsset(replacement,W+"/"+replacement.name+".mat");clones.Add(src,replacement);
                            }
                            mats[j]=replacement;slots++;changed=true;
                        }
                        if(changed){renderer.sharedMaterials=mats;EditorUtility.SetDirty(renderer);if(PrefabUtility.IsPartOfPrefabInstance(renderer))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);}
                    }
                    if(slots==0)throw new Exception("No known painted panels on "+car.name);
                    notes.AppendLine("Car="+car.name+" index="+index+" palette="+labels[n]+" clonedPaintMaterials="+clones.Count+" slots="+slots);total+=clones.Count;
                }
                notes.AppendLine("New materials="+total+". Original materials/shaders,glass,trim,tyres,geometry,camera,lighting,baked data,road and parking paint unchanged.");
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene(),S);
                notes.AppendLine("SHA256="+Hash(S));File.WriteAllText(output+"/vehicle-palette-report.txt",notes.ToString());EditorApplication.Exit(0);
            }catch(Exception e){Debug.LogException(e);if(output!=null)File.WriteAllText(output+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        }
    }
}
