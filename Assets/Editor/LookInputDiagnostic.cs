using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

namespace Sandbox.EditorTools
{
    public static class LookInputDiagnostic
    {
        private const string ScenePath = "Assets/Scenes/Sandbox.unity";
        private const int SettleFrames = 30;
        private const int InjectFrames = 30;

        private static int framesElapsed;
        private static CinemachineOrbitalFollow orbitalFollow;
        private static Vector2 startHV;

        [MenuItem("Sandbox/Run Look Input Diagnostic")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.update += OnUpdate;
            EditorApplication.isPlaying = true;
        }

        private static void OnUpdate()
        {
            if (!EditorApplication.isPlaying)
                return;

            framesElapsed++;

            if (framesElapsed == 1)
            {
                Debug.Log($"Mouse.current null? {Mouse.current == null}");
                if (Mouse.current == null)
                    InputSystem.AddDevice<Mouse>();
                Debug.Log($"Mouse.current null after add? {Mouse.current == null}");

                var vcamGo = GameObject.Find("PlayerFollowCamera");
                orbitalFollow = vcamGo != null ? vcamGo.GetComponent<CinemachineOrbitalFollow>() : null;
                Debug.Log($"OrbitalFollow found: {orbitalFollow != null}");
            }

            if (framesElapsed == SettleFrames && orbitalFollow != null)
            {
                startHV = new Vector2(orbitalFollow.HorizontalAxis.Value, orbitalFollow.VerticalAxis.Value);
                Debug.Log($"Before injecting mouse delta: Horizontal={orbitalFollow.HorizontalAxis.Value}, Vertical={orbitalFollow.VerticalAxis.Value}");
            }

            if (framesElapsed > SettleFrames && framesElapsed <= SettleFrames + InjectFrames)
            {
                if (Mouse.current != null)
                {
                    InputSystem.QueueDeltaStateEvent(Mouse.current.delta, new Vector2(20f, 0f));
                    InputSystem.Update();
                }
            }

            if (framesElapsed == SettleFrames + InjectFrames + 5)
            {
                EditorApplication.update -= OnUpdate;

                if (orbitalFollow != null)
                {
                    Vector2 endHV = new Vector2(orbitalFollow.HorizontalAxis.Value, orbitalFollow.VerticalAxis.Value);
                    Debug.Log("LOOK_DIAG_START");
                    Debug.Log($"After injecting mouse delta: Horizontal={orbitalFollow.HorizontalAxis.Value}, Vertical={orbitalFollow.VerticalAxis.Value}");
                    Debug.Log($"Delta Horizontal={endHV.x - startHV.x}, Delta Vertical={endHV.y - startHV.y}");
                    Debug.Log("LOOK_DIAG_END");
                }

                EditorApplication.isPlaying = false;
                EditorApplication.Exit(0);
            }
        }
    }
}
