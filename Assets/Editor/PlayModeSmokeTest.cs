using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Sandbox.EditorTools
{
    public static class PlayModeSmokeTest
    {
        private const string ScenePath = "Assets/Scenes/Sandbox.unity";
        private const int FramesToRun = 120;

        private static int framesElapsed;
        private static readonly StringBuilder Log = new StringBuilder();
        private static bool hadError;

        [MenuItem("Sandbox/Run Play Mode Smoke Test")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath);

            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            Application.logMessageReceived += OnLogMessage;
            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                hadError = true;
                Log.AppendLine($"[{type}] {condition}");
                Log.AppendLine(stackTrace);
            }
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            framesElapsed++;
            if (framesElapsed == 1)
            {
                var player = GameObject.Find("Player");
                Log.AppendLine($"Frame 1: Player found = {player != null}");
                if (player != null)
                    Log.AppendLine($"Frame 1: Player position = {player.transform.position}");
            }

            if (framesElapsed >= FramesToRun)
            {
                var player = GameObject.Find("Player");
                if (player != null)
                {
                    var controller = player.GetComponent<CharacterController>();
                    Log.AppendLine($"Frame {framesElapsed}: Player position = {player.transform.position}, isGrounded = {controller.isGrounded}");
                }

                EditorApplication.update -= OnUpdate;
                Application.logMessageReceived -= OnLogMessage;
                EditorApplication.isPlaying = false;

                Debug.Log("SMOKE_TEST_RESULT_START");
                Debug.Log(Log.ToString());
                Debug.Log(hadError ? "SMOKE_TEST_RESULT: FAIL" : "SMOKE_TEST_RESULT: PASS");
                Debug.Log("SMOKE_TEST_RESULT_END");

                EditorApplication.Exit(hadError ? 1 : 0);
            }
        }
    }
}
