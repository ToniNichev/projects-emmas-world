using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sandbox.EditorTools
{
    public static class WebGLBuildScript
    {
        private const string OutputPath = "Builds/WebGL";

        [MenuItem("Sandbox/Build WebGL Test")]
        public static void Build()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            // Uncompressed output rather than Unity's default gzip: the app's
            // custom server.js serves public/ through Next's own request
            // handler, which already gzips on the fly (next.config.mjs has
            // compress: true) -- letting that handle it avoids needing to
            // hand-configure Content-Encoding headers for pre-gzipped .gz
            // files, which plain static file serving won't do by default.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;

            Debug.Log($"WEBGL_BUILD scenes={string.Join(",", scenes)}");
            Debug.Log($"WEBGL_BUILD activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}");
            Debug.Log($"WEBGL_BUILD compressionFormat={PlayerSettings.WebGL.compressionFormat}");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            Debug.Log($"WEBGL_BUILD_RESULT result={summary.result} totalErrors={summary.totalErrors} totalWarnings={summary.totalWarnings} totalSize={summary.totalSize} totalTime={summary.totalTime}");
        }
    }
}
