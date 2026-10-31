using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Parkside.Editor
{
    /// <summary>Packages saved scenes. Scene creation and layout changes are never part of a build.</summary>
    public static class ParksideAndroidBuild
    {
        [MenuItem("PARKSIDE/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigureAndroidTools();
            PracticeSceneValidator.ValidateBuildScenes();
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            PlayerSettings.companyName = "Parkside";
            PlayerSettings.productName = "Pleos Unity Input Probe";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.example.pleosdemo.unityprobe");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)34;
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            QualitySettings.vSyncCount = 0;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = true;

            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            foreach (var name in new[] { "useCustomMainGradleTemplate", "useCustomLauncherGradleManifest", "useCustomGradlePropertiesTemplate", "useCustomGradleSettingsTemplate" })
            {
                var property = settings.FindProperty(name);
                if (property == null) throw new BuildFailedException("Missing Unity setting: " + name);
                property.boolValue = true;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();

            var repository = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            var output = Path.Combine(repository, "build/unity-probe/android-export");
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
                if (arguments[i] == "-probeOutputPath") output = Path.GetFullPath(arguments[i + 1]);
            Directory.CreateDirectory(output);
            AssetDatabase.SaveAssets();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = scenes, locationPathName = output, target = BuildTarget.Android,
                options = BuildOptions.Development | BuildOptions.AcceptExternalModificationsToPlayer
            });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Android export failed: " + report.summary.result);
            Debug.Log("PLEOS_PROBE_EXPORT " + output);
        }

        private static void ConfigureAndroidTools()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Android Build Support is required for APK builds. PC editing can use the Windows target without it.");
            var type = Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions", false);
            if (type == null) throw new BuildFailedException("The Android Editor extension could not be loaded");
            var tools = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer");
            var sdk = Environment.GetEnvironmentVariable("PARKSIDE_ANDROID_SDK");
            if (string.IsNullOrEmpty(sdk)) sdk = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/unity-probe/android-sdk"));
            if (!Directory.Exists(Path.Combine(sdk, "platforms/android-34")))
                throw new BuildFailedException("Run tools/build-unity-input-probe.ps1 to prepare the API 34 SDK");
            SetToolPath(type, "sdkRootPath", sdk);
            SetToolPath(type, "ndkRootPath", Path.Combine(tools, "NDK"));
            SetToolPath(type, "jdkRootPath", Path.Combine(tools, "OpenJDK"));
        }

        private static void SetToolPath(Type type, string propertyName, string path)
        {
            var property = type.GetProperty(propertyName);
            if (property == null || !property.CanWrite) throw new BuildFailedException("Missing Android setting: " + propertyName);
            property.SetValue(null, path);
        }
    }
}
