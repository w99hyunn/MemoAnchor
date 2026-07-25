#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

public static class MemoAnchorRtabmapIOSPostProcess
{
    public const string EnableNativeRtabmapDefine = "MEMOANCHOR_ENABLE_RTABMAP_NATIVE";

    [PostProcessBuild(550)]
    public static void LinkRtabmap(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
            return;

        EnableDocumentSharing(pathToBuiltProject);
        Debug.Log("[MemoAnchorRtabmapIOSPostProcess] Recorder-only Phase A/B build: RTAB-Map demo sources and static libraries are intentionally not linked.");
    }

    public static bool IsNativeRtabmapBuildEnabled()
    {
        return false;
    }

    private static void EnableDocumentSharing(string pathToBuiltProject)
    {
        var plistPath = System.IO.Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        var root = plist.root;
        root.SetBoolean("UIFileSharingEnabled", true);
        root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);

        plist.WriteToFile(plistPath);
        Debug.Log("[MemoAnchorRtabmapIOSPostProcess] Enabled iOS Documents file sharing for RGB-D recorder export.");
    }
}
#endif
