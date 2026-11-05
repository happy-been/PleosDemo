using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ParkingLotEnvironment.Editor {
// Inspection files stay outside Assets and follow this checkout instead of a developer's PC path.
public static class ParkingLotEditorPaths {
    public static readonly string ProjectRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"..")).Replace('\\','/');
    public static readonly string BackupRoot=ResolveBackupRoot(ProjectRoot);
    // Preserve the two existing inspection batches and their recorded filenames.
    public static readonly string LegacyOutput=BackupRoot+"/2026-11-02-parkinglot";
    public static readonly string Output=BackupRoot+"/2026-11-02-korean-parkinglot";

    static string ResolveBackupRoot(string projectRoot){
        var project=new DirectoryInfo(projectRoot);var checkout=project;
        for(var candidate=project;candidate!=null;candidate=candidate.Parent){
            string git=Path.Combine(candidate.FullName,".git");
            if(Directory.Exists(git)||File.Exists(git)){checkout=candidate;break;}
        }
        if(checkout.Parent==null)throw new InvalidOperationException("The map project needs a parent directory for external inspection files.");
        return Path.GetFullPath(Path.Combine(checkout.Parent.FullName,checkout.Name+"-local-backups")).Replace('\\','/');
    }
    public static void EnsureOutputDirectories(){Directory.CreateDirectory(LegacyOutput);Directory.CreateDirectory(Output);}

    [MenuItem("ParkingLot/Show inspection folder paths")]
    public static void Show(){Debug.Log("Parking map inspection paths\nProject: "+ProjectRoot+"\nBuild checks: "+LegacyOutput+"\nImages and videos: "+Output);}
}
}
