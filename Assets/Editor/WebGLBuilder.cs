using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sandbox.EditorTools
{
    // Batch-mode entry point for producing the WebGL build that gets
    // deployed to emma-tony.com (a self-hosted Next.js app -- the built
    // files just need to land in that repo's public/unity/emmas-world/Build/
    // and get committed there, not published through any platform).
    public static class WebGLBuilder
    {
        private const string OutputPath = "WebGLBuild";

        [MenuItem("Sandbox/Build WebGL")]
        public static void Build()
        {
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray(),
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log($"WebGLBuilder: result={report.summary.result} totalSize={report.summary.totalSize} totalErrors={report.summary.totalErrors} output={OutputPath}");
        }
    }
}
