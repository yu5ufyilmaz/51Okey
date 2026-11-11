using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Telefona kablosuz (adb install) kurulacak Android test build'i.
// Test build'i Unity'nin debug anahtarıyla imzalanır, projedeki keystore şifresi istenmez.
// Not: Telefonda Play Store'dan kurulu sürüm varsa imza farklı olduğu için önce onu silmek gerekir.
public static class AndroidDevBuild
{
    public const string ApkPath = "Builds/Android/51Okey-dev.apk";

    [MenuItem("51Okey/Android Test Build")]
    public static void Build()
    {
        bool customKeystore = PlayerSettings.Android.useCustomKeystore;
        bool appBundle = EditorUserBuildSettings.buildAppBundle;
        AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
        var android = NamedBuildTarget.Android;
        Il2CppCompilerConfiguration compilerConfig = PlayerSettings.GetIl2CppCompilerConfiguration(android);
        Il2CppCodeGeneration codeGeneration = PlayerSettings.GetIl2CppCodeGeneration(android);
        try
        {
            PlayerSettings.Android.useCustomKeystore = false;
            EditorUserBuildSettings.buildAppBundle = false; // .aab değil, doğrudan kurulabilen .apk
            // Hız için: test telefonu ARM64, ARMv7 için ikinci C++ derlemesine gerek yok; Debug C++ ve OptimizeSize daha hızlı derlenir
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Debug);
            PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSize);

            BuildReport report = BuildPipeline.BuildPlayer(
                new BuildPlayerOptions
                {
                    scenes = EditorBuildSettings
                        .scenes.Where(s => s.enabled)
                        .Select(s => s.path)
                        .ToArray(),
                    locationPathName = ApkPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.Development,
                }
            );

            Debug.Log(
                $"[AndroidDevBuild] {report.summary.result} -> {ApkPath} ({report.summary.totalTime.TotalSeconds:F0} sn, {report.summary.totalErrors} hata)"
            );
        }
        finally
        {
            PlayerSettings.Android.useCustomKeystore = customKeystore;
            EditorUserBuildSettings.buildAppBundle = appBundle;
            PlayerSettings.Android.targetArchitectures = architectures;
            PlayerSettings.SetIl2CppCompilerConfiguration(android, compilerConfig);
            PlayerSettings.SetIl2CppCodeGeneration(android, codeGeneration);
            AssetDatabase.SaveAssets(); // Unity build sırasında ProjectSettings'i geçici değerle diske yazıyor, gerçek değeri geri kaydet
        }
    }
}
