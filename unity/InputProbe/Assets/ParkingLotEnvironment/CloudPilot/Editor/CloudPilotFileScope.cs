using System;
using System.IO;
using System.Collections.Generic;

namespace ParkingLotEnvironment.CloudPilot {
// Pure filesystem helpers: no Unity runtime needed for focused path/scope tests.
internal static class CloudPilotFileScope {
    const string Project="unity/InputProbe/";
    const string Kit=Project+"Assets/ParkingLotEnvironment/CloudPilot";
    internal static bool IsNewPilot(string relative) {
        return relative==Kit || relative==Kit+".meta" || relative.StartsWith(Kit+"/",StringComparison.Ordinal);
    }
    static bool IsCache(string name) {
        foreach(string cache in new[]{".git","Library","Temp","Logs","obj"})
            if(string.Equals(name,cache,StringComparison.OrdinalIgnoreCase))return true;
        return false;
    }
    internal static string Resolve(string repo,string relative) {
        if(string.IsNullOrEmpty(relative)||Path.IsPathRooted(relative)||relative.IndexOf(':')>=0)
            throw new InvalidDataException("Expected a repository-relative path.");
        relative=relative.Replace('\\','/');string[] parts=relative.Split('/');
        foreach(string part in parts)if(part.Length==0||part=="."||part=="..")
            throw new InvalidDataException("Non-canonical repository path: "+relative);
        string root=Path.GetFullPath(repo).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        string full=Path.GetFullPath(Path.Combine(root,relative));
        StringComparison comparison=Path.DirectorySeparatorChar=='\\'?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(!full.StartsWith(root+Path.DirectorySeparatorChar,comparison))throw new InvalidDataException("Path outside repository.");
        string current=root;
        foreach(string part in parts) {
            current=Path.Combine(current,part);
            try {
                if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                    throw new InvalidDataException("Symlink/reparse path is not a protected repository file: "+relative);
            } catch(FileNotFoundException) {break;} catch(DirectoryNotFoundException) {break;}
        }
        return full;
    }
    internal static IEnumerable<string> UnityFiles(string repo) {
        // Validate the only excluded source subtree even though it will not be walked.
        Resolve(repo,Kit);
        foreach(string area in new[]{"Assets","Packages","ProjectSettings"}) {
            var pending=new Stack<string>();pending.Push(Project+area);
            while(pending.Count>0) {
                string relative=pending.Pop();string directory=Resolve(repo,relative);
                if(!Directory.Exists(directory))continue;
                foreach(string file in Directory.GetFiles(directory,"*",SearchOption.TopDirectoryOnly)) {
                    string path=relative+"/"+Path.GetFileName(file);
                    if(IsNewPilot(path))continue;
                    Resolve(repo,path);yield return path;
                }
                foreach(string child in Directory.GetDirectories(directory,"*",SearchOption.TopDirectoryOnly)) {
                    string name=Path.GetFileName(child),path=relative+"/"+name;
                    // Exclude BEFORE resolving, pushing, or enumerating the directory.
                    if(IsCache(name)||IsNewPilot(path))continue;
                    pending.Push(path);
                }
            }
        }
    }
}
}
