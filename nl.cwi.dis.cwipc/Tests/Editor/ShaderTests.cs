using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cwipc.Tests
{
    /// <summary>
    /// The package's shaders compile in this project, whichever render pipeline it uses
    /// (cwi-dis/cwipc_unity#55, #72). In a project without URP the URP SubShaders must be
    /// skipped through their PackageRequirements block; if one isn't, its #include of URP
    /// files fails and Unity records a shader error.
    ///
    /// Unity only compiles shaders when it has a graphics device, so under -nographics
    /// (CI's containers) this test is skipped rather than passing without checking anything.
    /// .github/scripts/check_urp_subshaders.py checks the same rule textually, without Unity.
    /// </summary>
    public class ShaderTests
    {
        const string PackageRoot = "Packages/nl.cwi.dis.cwipc";

        [Test]
        public void PackageShadersCompileWithoutErrors()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics): Unity doesn't compile shaders");
            }
            string[] guids = AssetDatabase.FindAssets("t:Shader", new[] { PackageRoot });
            Assert.That(guids, Is.Not.Empty, $"no shaders found in {PackageRoot}");
            var failures = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => (path, shader: AssetDatabase.LoadAssetAtPath<Shader>(path)))
                .Where(s => s.shader != null && ShaderUtil.ShaderHasError(s.shader))
                .Select(s => $"{s.path}: " + string.Join("; ", ShaderUtil.GetShaderMessages(s.shader)
                    .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    .Select(m => $"{m.message} ({m.file}:{m.line})")))
                .ToList();
            Assert.That(failures, Is.Empty, "shaders with errors:\n" + string.Join("\n", failures));
        }
    }
}
