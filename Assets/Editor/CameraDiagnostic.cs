using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

namespace Sandbox.EditorTools
{
    public static class CameraDiagnostic
    {
        private const string ScenePath = "Assets/Scenes/Sandbox.unity";
        private const int FramesToRun = 60;

        private static int framesElapsed;

        [MenuItem("Sandbox/Run Camera Diagnostic")]
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
            if (framesElapsed < FramesToRun)
                return;

            EditorApplication.update -= OnUpdate;

            var mainCamGo = GameObject.Find("Main Camera");
            var vcamGo = GameObject.Find("PlayerFollowCamera");
            var playerGo = GameObject.Find("Player");

            Debug.Log("DIAG_START");
            Debug.Log($"Main Camera found: {mainCamGo != null}");
            if (mainCamGo != null)
                Debug.Log($"Main Camera pos: {mainCamGo.transform.position}, rot: {mainCamGo.transform.eulerAngles}");

            var brain = mainCamGo != null ? mainCamGo.GetComponent<CinemachineBrain>() : null;
            Debug.Log($"CinemachineBrain found: {brain != null}");
            if (brain != null)
            {
                var active = brain.ActiveVirtualCamera;
                Debug.Log($"Brain.ActiveVirtualCamera null? {active == null}");
                if (active != null)
                    Debug.Log($"Brain.ActiveVirtualCamera name: {active.Name}");
            }

            var vcam = vcamGo != null ? vcamGo.GetComponent<CinemachineCamera>() : null;
            Debug.Log($"CinemachineCamera found: {vcam != null}");
            if (vcam != null)
            {
                Debug.Log($"vcam enabled: {vcam.enabled}, gameObject active: {vcam.gameObject.activeInHierarchy}");
                Debug.Log($"vcam.Follow null? {vcam.Follow == null}, vcam.LookAt null? {vcam.LookAt == null}");
                Debug.Log($"vcam State.RawPosition: {vcam.State.RawPosition}");
                Debug.Log($"vcam Priority: {vcam.Priority}");
            }

            var thirdPerson = vcamGo != null ? vcamGo.GetComponent<CinemachineThirdPersonFollow>() : null;
            Debug.Log($"ThirdPersonFollow found: {thirdPerson != null}, enabled: {thirdPerson != null && thirdPerson.enabled}");
            if (thirdPerson != null)
                Debug.Log($"ThirdPersonFollow.IsValid (needs FollowTarget != null): {thirdPerson.FollowTarget != null}");

            if (playerGo != null)
                Debug.Log($"Player pos: {playerGo.transform.position}");

            Debug.Log("DIAG_END");

            EditorApplication.isPlaying = false;
            EditorApplication.Exit(0);
        }
    }
}
