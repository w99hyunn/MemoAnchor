using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace MemoAnchor.Editor
{
    public static class MemoAnchoriOSPostProcessBuild
    {
        [PostProcessBuild(10)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS)
            {
                return;
            }

            string projectPath = PBXProject.GetPBXProjectPath(buildPath);
            PBXProject project = new PBXProject();
            project.ReadFromFile(projectPath);

            string frameworkTargetGuid = project.GetUnityFrameworkTargetGuid();
            project.AddFrameworkToProject(frameworkTargetGuid, "WebKit.framework", false);

            File.WriteAllText(projectPath, project.WriteToString());

            string plistPath = Path.Combine(buildPath, "Info.plist");
            PlistDocument plist = new();
            plist.ReadFromFile(plistPath);
            PlistElementArray urlTypes = plist.root.CreateArray("CFBundleURLTypes");
            PlistElementDict urlType = urlTypes.AddDict();
            urlType.SetString("CFBundleURLName", "MemoAnchor");
            PlistElementArray urlSchemes = urlType.CreateArray("CFBundleURLSchemes");
            urlSchemes.AddString("memoanchor");
            plist.WriteToFile(plistPath);
        }
    }
}
