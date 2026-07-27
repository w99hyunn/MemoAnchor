#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MemoAnchorIOSBuild
{
    private const string OUTPUT_PATH = "ios_Build";
    private static readonly string[] SCENES =
    {
        "Assets/00_MemoAnchor/01_Scenes/Splash.unity",
        "Assets/00_MemoAnchor/01_Scenes/Main.unity",
        "Assets/00_MemoAnchor/01_Scenes/ServicesManager.unity",
        "Assets/Scenes/ARKitMeshScanScene.unity"
    };

    [MenuItem("MemoAnchor/Build/iOS ARKit Mesh Scan")]
    public static void BuildARKitMeshScan()
    {
        BuildARKitMeshScanRecorderOnly();
    }

    private static void BuildARKitMeshScanRecorderOnly()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        RemoveNativeRtabmapDefine();

        if (Directory.Exists(OUTPUT_PATH))
            Directory.Delete(OUTPUT_PATH, true);

        Debug.Log("[MemoAnchorIOSBuild] Building iOS ARKit Mesh Scan recorder-only build without Native RTAB-Map.");
        var report = BuildPipeline.BuildPlayer(SCENES, OUTPUT_PATH, BuildTarget.iOS, BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception($"iOS build failed: {report.summary.result}");
    }

    private static void RemoveNativeRtabmapDefine()
    {
        var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS);
        var nextSymbols = new List<string>();
        foreach (var symbol in symbols.Split(';'))
        {
            var trimmed = symbol.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed == MemoAnchorRtabmapIOSPostProcess.ENABLE_NATIVE_RTABMAP_DEFINE)
                continue;

            nextSymbols.Add(trimmed);
        }

        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, string.Join(";", nextSymbols));
    }
}
#endif
