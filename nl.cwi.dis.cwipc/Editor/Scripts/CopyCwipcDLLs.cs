#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

namespace Cwipc {
    public class CopyCwipcDLLs : IPostprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }
        public void OnPostprocessBuild(BuildReport report)
        {
            Debug.LogWarning("xxxjack CopyCwipcDLLs.OnPostprocessBuild not implemented yet");
        }

        public static string getBuildPlatformLibrariesPath(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.StandaloneWindows64)
            {
                var buildOutputPath = Path.GetDirectoryName(report.summary.outputPath);
                var dataDirs = Directory.GetDirectories(buildOutputPath, "*_Data");
                if (dataDirs.Length != 1)
                {
                    Debug.LogError($"Expected 1 *_Data directory but found {dataDirs.Length}");
                    return null;
                }
                var dllOutputPath = Path.Combine(buildOutputPath, dataDirs[0], "Plugins", "x86_64");
                return dllOutputPath;
            }
            else if (report.summary.platform == BuildTarget.StandaloneLinux64)
            {
                Debug.LogWarning("CopyCwipcDLLs: on Linux you must install cwipc with apt-get before running");
                return null;
            }
            else if (report.summary.platform == BuildTarget.StandaloneOSX)
            {
                Debug.LogWarning("CopyCwipcDLLs: on Macos you must install cwipc with brew before running");
                return null;
            }
            else if (report.summary.platform == BuildTarget.Android)
            {
                Debug.Log("CopyCwipcDLLs: no need to copy DLLs for Android, should already be included.");
                return null;
            }
            return null;
        }
        void CopyFiles(string srcDir, string dstDir)
        {
            Debug.Log($"CopyCwipcDLLs.CopyFiles src {srcDir} dst {dstDir}");
            if (!Directory.Exists(dstDir))
            {
                Directory.CreateDirectory(dstDir);
            }
            foreach (var file in Directory.GetFiles(srcDir))
            {
                if (file.EndsWith(".meta"))
                {
                    continue;
                }
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(dstDir, fileName);
                File.Copy(file, destFile, true);
                Debug.Log($"CopyCwipcDLLs.CopyFiles copied {file} to {destFile}");
            }
        }
    }
}
#endif