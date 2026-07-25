#import <Foundation/Foundation.h>

static const char *gMemoAnchorRtabmapLastError =
    "Native RTAB-Map is disabled in recorder-only Phase A/B builds.";

extern "C"
{
    const char *MemoAnchor_Rtabmap_LastError(void)
    {
        return gMemoAnchorRtabmapLastError;
    }

    int MemoAnchor_Rtabmap_IsCompiled(void)
    {
        return 0;
    }

    const void *MemoAnchor_Rtabmap_Create(void)
    {
        return NULL;
    }

    void MemoAnchor_Rtabmap_Destroy(const void *)
    {
    }

    int MemoAnchor_Rtabmap_OpenDatabase(const void *, const char *, bool)
    {
        return -1;
    }

    int MemoAnchor_Rtabmap_SetMappingParameter(const void *, const char *, const char *)
    {
        return -1;
    }

    int MemoAnchor_Rtabmap_StartCamera(const void *)
    {
        return 0;
    }

    void MemoAnchor_Rtabmap_SetPausedMapping(const void *, bool)
    {
    }

    int MemoAnchor_Rtabmap_PostCurrentARFrame(const void *, const void *, int, float, float)
    {
        return -1;
    }

    int MemoAnchor_Rtabmap_ExportTexturedMesh(const void *, float, int, int, int, float, float, int)
    {
        return 0;
    }

    int MemoAnchor_Rtabmap_WriteExportedMesh(const void *, const char *, const char *)
    {
        return 0;
    }
}
