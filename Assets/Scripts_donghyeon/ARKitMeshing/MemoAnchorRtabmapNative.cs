using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public sealed class MemoAnchorRtabmapNative : IDisposable
{
    public bool IsRunning => false;
    public int PostedFrames => 0;
    public int RejectedFrames => 0;
    public string OutputDirectory => string.Empty;
    public string DatabasePath => string.Empty;

    public static bool IsCompiledWithRtabmap => false;
    public static string LastError => "Native RTAB-Map is disabled in recorder-only Phase A/B builds.";

    public bool Start(ARSession arSession, string scanId)
    {
        Debug.LogWarning("[MemoAnchorRtabmapNative] Native RTAB-Map is disabled. Unity RGB-D recorder is the active capture path.");
        return false;
    }

    public bool PostCurrentFrame(ARSession arSession, float viewportWidth, float viewportHeight)
    {
        return false;
    }

    public string ExportTexturedMesh(string meshName = "memoanchor_rtabmap")
    {
        return string.Empty;
    }

    public void Dispose()
    {
    }
}
