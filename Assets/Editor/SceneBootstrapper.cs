using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Sandbox.Player;
using Sandbox.Building;
using Sandbox.Save;

namespace Sandbox.EditorTools
{
    public static class SceneBootstrapper
    {
        private const string InputActionsPath = "Assets/Settings/PlayerControls.inputactions";
        private const string ScenePath = "Assets/Scenes/Sandbox.unity";
        private const string PrefabPath = "Assets/Prefabs/Block.prefab";
        private const string PlayerLayerName = "Player";

        [MenuItem("Sandbox/Build Scaffolded Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            InputActionAsset actions = BuildInputActions();
            EnsureLayerExists(PlayerLayerName);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(5f, 1f, 5f);

            GameObject blockPrefab = CreateBlockPrefab();
            GameObject placedBlocks = new GameObject("PlacedBlocks");

            GameObject player = BuildPlayer(actions, blockPrefab, placedBlocks.transform);
            BuildCamera(actions, player.transform);

            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath)!);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("SceneBootstrapper: build complete");
        }

        private static InputActionAsset BuildInputActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = asset.AddActionMap("Player");

            InputAction move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            map.AddAction("Look", InputActionType.Value, binding: "<Mouse>/delta", expectedControlLayout: "Vector2");
            map.AddAction("Sprint", InputActionType.Button, binding: "<Keyboard>/leftShift");
            map.AddAction("Jump", InputActionType.Button, binding: "<Keyboard>/space");
            map.AddAction("Place", InputActionType.Button, binding: "<Mouse>/leftButton");
            map.AddAction("Remove", InputActionType.Button, binding: "<Mouse>/rightButton");
            map.AddAction("Save", InputActionType.Button, binding: "<Keyboard>/f5");
            map.AddAction("Load", InputActionType.Button, binding: "<Keyboard>/f9");

            string json = asset.ToJson();
            Object.DestroyImmediate(asset);

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), InputActionsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, json);

            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceSynchronousImport);

            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        }

        private static InputActionReference FindActionReference(InputActionAsset asset, string mapName, string actionName)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<InputActionReference>()
                .FirstOrDefault(r => r.action != null && r.action.actionMap.name == mapName && r.action.name == actionName);
        }

        private static GameObject CreateBlockPrefab()
        {
            GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
            source.name = "Block";
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath)!);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath, out bool success);
            Object.DestroyImmediate(source);
            if (!success)
                Debug.LogError("SceneBootstrapper: failed to save Block prefab");
            return prefab;
        }

        private static GameObject BuildPlayer(InputActionAsset actions, GameObject blockPrefab, Transform blockParent)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.layer = LayerMask.NameToLayer(PlayerLayerName);
            player.AddComponent<CharacterController>();

            ThirdPersonController controller = player.AddComponent<ThirdPersonController>();
            BuildPlacer placer = player.AddComponent<BuildPlacer>();
            WorldSaveSystem save = player.AddComponent<WorldSaveSystem>();

            SetPrivateField(controller, "actions", actions);
            SetPrivateField(placer, "actions", actions);
            SetPrivateField(save, "actions", actions);

            SetPrivateField(placer, "blockPrefab", blockPrefab);
            SetPrivateField(placer, "blockParent", blockParent);
            int playerLayerIndex = LayerMask.NameToLayer(PlayerLayerName);
            LayerMask placementMask = ~(1 << playerLayerIndex);
            SetPrivateField(placer, "placementMask", placementMask);

            SetPrivateField(save, "blockPrefab", blockPrefab);
            SetPrivateField(save, "blockParent", blockParent);

            return player;
        }

        private static void BuildCamera(InputActionAsset actions, Transform playerTransform)
        {
            GameObject camGo = new GameObject("PlayerFollowCamera");
            CinemachineCamera cmCam = camGo.AddComponent<CinemachineCamera>();
            cmCam.Follow = playerTransform;
            cmCam.LookAt = playerTransform;

            CinemachineThirdPersonFollow thirdPerson = camGo.AddComponent<CinemachineThirdPersonFollow>();
            thirdPerson.Damping = new Vector3(0.1f, 0.5f, 0.3f);
            thirdPerson.ShoulderOffset = new Vector3(0.4f, 0.6f, 0.2f);
            thirdPerson.VerticalArmLength = 0.3f;
            thirdPerson.CameraSide = 1f;
            thirdPerson.CameraDistance = 7f;

            camGo.AddComponent<CinemachinePanTilt>();

            CinemachineInputAxisController axisController = camGo.AddComponent<CinemachineInputAxisController>();
            axisController.SynchronizeControllers();
            InputActionReference lookRef = FindActionReference(actions, "Player", "Look");
            if (lookRef == null)
            {
                Debug.LogError("SceneBootstrapper: could not find Look InputActionReference");
            }
            else
            {
                foreach (var controller in axisController.Controllers)
                    controller.Input.InputAction = lookRef;
            }

            GameObject mainCamGo = new GameObject("Main Camera");
            mainCamGo.tag = "MainCamera";
            mainCamGo.AddComponent<Camera>();
            mainCamGo.AddComponent<AudioListener>();
            mainCamGo.AddComponent<CinemachineBrain>();
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"SceneBootstrapper: field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            field.SetValue(target, value);
        }

        private static void EnsureLayerExists(string layerName)
        {
            Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets.Length == 0)
            {
                Debug.LogError("SceneBootstrapper: could not load TagManager.asset");
                return;
            }

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (layerProp.stringValue == layerName)
                    return;
            }

            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = layerName;
                    tagManager.ApplyModifiedProperties();
                    return;
                }
            }

            Debug.LogError("SceneBootstrapper: no free user layer slot for 'Player'");
        }
    }
}
