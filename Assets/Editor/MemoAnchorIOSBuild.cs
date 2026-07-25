#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MemoAnchorIOSBuild
{
    private const string OutputPath = "ios_Build";
    private static readonly string[] Scenes =
    {
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

        if (Directory.Exists(OutputPath))
            Directory.Delete(OutputPath, true);

        Debug.Log("[MemoAnchorIOSBuild] Building iOS ARKit Mesh Scan recorder-only build without Native RTAB-Map.");
        var report = BuildPipeline.BuildPlayer(Scenes, OutputPath, BuildTarget.iOS, BuildOptions.None);
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
            if (string.IsNullOrEmpty(trimmed) || trimmed == MemoAnchorRtabmapIOSPostProcess.EnableNativeRtabmapDefine)
                continue;

            nextSymbols.Add(trimmed);
        }

        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.iOS, string.Join(";", nextSymbols));
    }
}
#endif
