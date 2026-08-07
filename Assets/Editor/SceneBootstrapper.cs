using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.ProBuilder;
using UnityEngine.UI;
using Unity.Cinemachine;
using Sandbox.Player;
using Sandbox.Building;
using Sandbox.Save;
using Sandbox.CameraControl;
using Sandbox.UI;

namespace Sandbox.EditorTools
{
    public static class SceneBootstrapper
    {
        private const string InputActionsPath = "Assets/Settings/PlayerControls.inputactions";
        private const string ScenePath = "Assets/Scenes/Sandbox.unity";
        private const string PrefabsFolder = "Assets/Prefabs";
        private const string TerrainFolder = "Assets/Terrain";
        private const string PlayerLayerName = "Player";
        private const string MaterialsFolder = "Assets/Materials";

        [MenuItem("Sandbox/Build Scaffolded Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            InputActionAsset actions = BuildInputActions();
            EnsureLayerExists(PlayerLayerName);

            Material groundMaterial = CreateMaterial("Ground", new Color(0.35f, 0.6f, 0.3f));
            Material playerMaterial = CreateMaterial("Player", new Color(0.25f, 0.55f, 0.95f));
            Material blockMaterial = CreateMaterial("Block", new Color(0.85f, 0.55f, 0.25f));
            Material rockMaterial = CreateMaterial("Rock", new Color(0.5f, 0.5f, 0.52f));
            Material trunkMaterial = CreateMaterial("Trunk", new Color(0.4f, 0.25f, 0.1f));
            Material leafMaterial = CreateMaterial("Leaves", new Color(0.15f, 0.5f, 0.2f));

            Terrain terrain = CreateTerrain(groundMaterial);

            GameObject rockPrefab = CreateRockPrefab(rockMaterial);
            GameObject treePrefab = CreateTreePrefab(trunkMaterial, leafMaterial);
            ScatterEnvironmentProps(terrain, treePrefab, rockPrefab);

            GameObject[] blockPrefabs = CreateShapePrefabs(blockMaterial);
            GameObject placedBlocks = new GameObject("PlacedBlocks");

            GameObject player = BuildPlayer(actions, blockPrefabs, placedBlocks.transform, playerMaterial);
            BuildCamera(player.transform);
            BuildPaletteUI(player.GetComponent<BuildPlacer>());

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

            map.AddAction("Sprint", InputActionType.Button, binding: "<Keyboard>/leftShift");
            map.AddAction("Jump", InputActionType.Button, binding: "<Keyboard>/space");
            map.AddAction("Place", InputActionType.Button, binding: "<Mouse>/leftButton");
            map.AddAction("Remove", InputActionType.Button, binding: "<Keyboard>/q");
            map.AddAction("Rotate", InputActionType.Button, binding: "<Keyboard>/r");
            map.AddAction("Save", InputActionType.Button, binding: "<Keyboard>/f5");
            map.AddAction("Load", InputActionType.Button, binding: "<Keyboard>/f9");

            InputAction selectShape = map.AddAction("SelectShape", InputActionType.Button, binding: "<Keyboard>/1");
            selectShape.AddBinding("<Keyboard>/2");
            selectShape.AddBinding("<Keyboard>/3");
            selectShape.AddBinding("<Keyboard>/4");

            string json = asset.ToJson();
            Object.DestroyImmediate(asset);

            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), InputActionsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, json);

            AssetDatabase.ImportAsset(InputActionsPath, ImportAssetOptions.ForceSynchronousImport);

            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            Directory.CreateDirectory(MaterialsFolder);
            string path = $"{MaterialsFolder}/{name}.mat";
            var material = new Material(Shader.Find("Standard")) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Terrain CreateTerrain(Material material)
        {
            const int resolution = 129; // must be 2^n + 1
            const float worldSize = 120f;
            const float maxHeight = 6f;
            const float noiseScale = 0.045f;
            const float flatRadius = resolution * 0.14f;   // fully flat around spawn
            const float falloffRadius = resolution * 0.35f; // blends into full hills

            var terrainData = new TerrainData
            {
                heightmapResolution = resolution,
                size = new Vector3(worldSize, maxHeight, worldSize),
            };

            Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
            float noiseOffsetX = 137.2f;
            float noiseOffsetZ = 291.7f;

            float[,] heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float noise = Mathf.PerlinNoise((x + noiseOffsetX) * noiseScale, (z + noiseOffsetZ) * noiseScale);
                    float distFromCenter = Vector2.Distance(new Vector2(x, z), center);
                    float falloff = Mathf.Clamp01(Mathf.InverseLerp(flatRadius, falloffRadius, distFromCenter));
                    heights[z, x] = noise * falloff;
                }
            }
            terrainData.SetHeights(0, 0, heights);

            Directory.CreateDirectory(TerrainFolder);
            AssetDatabase.CreateAsset(terrainData, $"{TerrainFolder}/GroundTerrainData.asset");

            GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
            terrainGo.name = "Ground";
            terrainGo.transform.position = new Vector3(-worldSize / 2f, 0f, -worldSize / 2f);

            Terrain terrain = terrainGo.GetComponent<Terrain>();
            terrain.materialType = Terrain.MaterialType.Custom;
            terrain.materialTemplate = material;
            return terrain;
        }

        private static GameObject CreateRockPrefab(Material material)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock";
            rock.GetComponent<Renderer>().sharedMaterial = material;
            // Irregular base scale for a boulder-ish look; ScatterEnvironmentProps
            // layers a further random uniform scale on top per instance.
            rock.transform.localScale = new Vector3(1f, 0.75f, 0.9f);

            Directory.CreateDirectory(PrefabsFolder);
            string path = $"{PrefabsFolder}/Rock.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(rock, path, out bool success);
            Object.DestroyImmediate(rock);
            if (!success)
                Debug.LogError("SceneBootstrapper: failed to save Rock prefab");
            return prefab;
        }

        private static GameObject CreateTreePrefab(Material trunkMaterial, Material leafMaterial)
        {
            GameObject root = new GameObject("Tree");

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1f, 0f);
            trunk.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            trunk.GetComponent<Renderer>().sharedMaterial = trunkMaterial;

            GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            leaves.name = "Leaves";
            leaves.transform.SetParent(root.transform, false);
            leaves.transform.localPosition = new Vector3(0f, 2.6f, 0f);
            leaves.transform.localScale = new Vector3(1.6f, 1.6f, 1.6f);
            leaves.GetComponent<Renderer>().sharedMaterial = leafMaterial;

            Directory.CreateDirectory(PrefabsFolder);
            string path = $"{PrefabsFolder}/Tree.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);
            if (!success)
                Debug.LogError("SceneBootstrapper: failed to save Tree prefab");
            return prefab;
        }

        private static void ScatterEnvironmentProps(Terrain terrain, GameObject treePrefab, GameObject rockPrefab)
        {
            const float worldSize = 120f;
            const float clearRadius = 20f; // keep the flat spawn/build area free of scenery

            GameObject environment = new GameObject("Environment");

            ScatterProps(environment.transform, treePrefab, 40, worldSize, clearRadius, terrain, 0.8f, 1.3f);
            ScatterProps(environment.transform, rockPrefab, 50, worldSize, clearRadius, terrain, 0.5f, 1.2f);
        }

        private static void ScatterProps(Transform parent, GameObject prefab, int count, float worldSize, float clearRadius, Terrain terrain, float minScale, float maxScale)
        {
            for (int i = 0; i < count; i++)
            {
                float x, z;
                do
                {
                    x = UnityEngine.Random.Range(-worldSize / 2f, worldSize / 2f);
                    z = UnityEngine.Random.Range(-worldSize / 2f, worldSize / 2f);
                } while (new Vector2(x, z).magnitude < clearRadius);

                float y = terrain.SampleHeight(new Vector3(x, 0f, z));
                Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                GameObject instance = Object.Instantiate(prefab, new Vector3(x, y, z), rotation, parent);
                instance.transform.localScale *= UnityEngine.Random.Range(minScale, maxScale);
            }
        }

        // Order matters: BuildPlacer/WorldSaveSystem index into this array by
        // shape index (0=Cube, 1=Wedge, 2=Cylinder, 3=Ball), selected via the 1-4 keys.
        private static GameObject[] CreateShapePrefabs(Material material)
        {
            Directory.CreateDirectory(PrefabsFolder);

            return new[]
            {
                SaveShapePrefab("Block", GameObject.CreatePrimitive(PrimitiveType.Cube), material),
                SaveShapePrefab("Wedge", CreateWedgeSource(), material),
                SaveShapePrefab("Cylinder", CreateCylinderSource(), material),
                SaveShapePrefab("Ball", GameObject.CreatePrimitive(PrimitiveType.Sphere), material),
            };
        }

        private static GameObject SaveShapePrefab(string name, GameObject source, Material material)
        {
            source.name = name;
            source.GetComponent<Renderer>().sharedMaterial = material;

            string path = $"{PrefabsFolder}/{name}.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, path, out bool success);
            Object.DestroyImmediate(source);
            if (!success)
                Debug.LogError($"SceneBootstrapper: failed to save {name} prefab");
            return prefab;
        }

        private static GameObject CreateCylinderSource()
        {
            GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            // Default cylinder is height 2 (spans y -1..1); halve it to fit the 1x1x1 grid cell.
            source.transform.localScale = new Vector3(1f, 0.5f, 1f);
            Object.DestroyImmediate(source.GetComponent<CapsuleCollider>());
            MeshCollider collider = source.AddComponent<MeshCollider>();
            // Script-added MeshCollider doesn't auto-populate sharedMesh from the
            // sibling MeshFilter the way the Editor's "Add Component" button does.
            collider.sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
            collider.convex = true;
            return source;
        }

        private static GameObject CreateWedgeSource()
        {
            // Adapted from ProBuilder's ShapeGenerator.GeneratePrism, which builds a
            // symmetric tent (ridge centered at x=0). Moving the ridge to x=+0.5
            // collapses one slope into the wedge's flat vertical back face and turns
            // the other into a single full ramp -- while keeping the exact same
            // vertex winding order as the proven, shipped template.
            //
            // Y values are -0.5 (bottom) / +0.5 (ridge) so the wedge is centered and
            // spans a full 1x1x1 bounding box, matching Cube/Cylinder/Ball -- the
            // ground/adjacency placement math in BuildPlacer assumes every prefab
            // shares that same centered-pivot convention.
            Vector3[] template =
            {
                new Vector3(-0.5f, -0.5f, -0.5f), // 0 back-left-bottom
                new Vector3(0.5f,  -0.5f, -0.5f), // 1 back-right-bottom
                new Vector3(0.5f,   0.5f, -0.5f), // 2 back-ridge
                new Vector3(-0.5f, -0.5f,  0.5f), // 3 front-left-bottom
                new Vector3(0.5f,  -0.5f,  0.5f), // 4 front-right-bottom
                new Vector3(0.5f,   0.5f,  0.5f), // 5 front-ridge
            };

            Vector3[] v =
            {
                template[0], template[1], template[2],                         // 0-2  right-side triangular cap
                template[1], template[4], template[2], template[5],            // 3-6  back vertical face
                template[4], template[3], template[5],                         // 7-9  left-side triangular cap
                template[3], template[0], template[5], template[2],            // 10-13 ramp surface
                template[0], template[1], template[3], template[4],            // 14-17 bottom
            };

            Face[] faces =
            {
                new Face(new[] { 2, 1, 0 }),
                new Face(new[] { 5, 4, 3, 5, 6, 4 }),
                new Face(new[] { 9, 8, 7 }),
                new Face(new[] { 12, 11, 10, 12, 13, 11 }),
                new Face(new[] { 14, 15, 16, 15, 17, 16 }),
            };

            ProBuilderMesh pb = ProBuilderMesh.Create(v, faces);
            pb.ToMesh();
            pb.Refresh();

            Mesh mesh = pb.GetComponent<MeshFilter>().sharedMesh;
            Directory.CreateDirectory(PrefabsFolder);
            AssetDatabase.CreateAsset(mesh, $"{PrefabsFolder}/WedgeMesh.asset");

            GameObject source = new GameObject("Wedge");
            source.AddComponent<MeshFilter>().sharedMesh = mesh;
            source.AddComponent<MeshRenderer>();
            MeshCollider collider = source.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = true;

            Object.DestroyImmediate(pb.gameObject);
            return source;
        }

        private static GameObject BuildPlayer(InputActionAsset actions, GameObject[] blockPrefabs, Transform blockParent, Material playerMaterial)
        {
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.GetComponent<Renderer>().sharedMaterial = playerMaterial;
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.layer = LayerMask.NameToLayer(PlayerLayerName);
            player.AddComponent<CharacterController>();

            ThirdPersonController controller = player.AddComponent<ThirdPersonController>();
            BuildPlacer placer = player.AddComponent<BuildPlacer>();
            WorldSaveSystem save = player.AddComponent<WorldSaveSystem>();

            SetPrivateField(controller, "actions", actions);
            SetPrivateField(placer, "actions", actions);
            SetPrivateField(save, "actions", actions);

            SetPrivateField(placer, "blockPrefabs", blockPrefabs);
            SetPrivateField(placer, "blockParent", blockParent);
            int playerLayerIndex = LayerMask.NameToLayer(PlayerLayerName);
            LayerMask placementMask = ~(1 << playerLayerIndex);
            SetPrivateField(placer, "placementMask", placementMask);

            SetPrivateField(save, "blockPrefabs", blockPrefabs);
            SetPrivateField(save, "blockParent", blockParent);

            return player;
        }

        private static void BuildCamera(Transform playerTransform)
        {
            GameObject camGo = new GameObject("PlayerFollowCamera");
            CinemachineCamera cmCam = camGo.AddComponent<CinemachineCamera>();
            cmCam.Follow = playerTransform;
            cmCam.LookAt = playerTransform;

            CinemachineOrbitalFollow orbitalFollow = camGo.AddComponent<CinemachineOrbitalFollow>();
            orbitalFollow.TargetOffset = new Vector3(0f, 0.3f, 0f);
            orbitalFollow.Radius = 7f;

            camGo.AddComponent<CinemachineRotationComposer>();

            OrbitCameraDragController dragController = camGo.AddComponent<OrbitCameraDragController>();
            SetPrivateField(dragController, "orbitalFollow", orbitalFollow);

            GameObject mainCamGo = new GameObject("Main Camera");
            mainCamGo.tag = "MainCamera";
            mainCamGo.AddComponent<Camera>();
            mainCamGo.AddComponent<AudioListener>();
            mainCamGo.AddComponent<CinemachineBrain>();
        }

        private static void BuildPaletteUI(BuildPlacer placer)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasGo = new GameObject("BuildUI");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            string[] labels = { "1\nCube", "2\nWedge", "3\nCylinder", "4\nBall" };
            const float slotSize = 90f;
            const float spacing = 10f;
            float totalWidth = labels.Length * slotSize + (labels.Length - 1) * spacing;
            float startX = -totalWidth / 2f + slotSize / 2f;

            Image[] slots = new Image[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                GameObject slotGo = new GameObject($"Slot_{i}");
                slotGo.transform.SetParent(canvasGo.transform, false);
                RectTransform slotRect = slotGo.AddComponent<RectTransform>();
                slotRect.anchorMin = new Vector2(0.5f, 0f);
                slotRect.anchorMax = new Vector2(0.5f, 0f);
                slotRect.pivot = new Vector2(0.5f, 0f);
                slotRect.sizeDelta = new Vector2(slotSize, slotSize);
                slotRect.anchoredPosition = new Vector2(startX + i * (slotSize + spacing), 45f);

                Image background = slotGo.AddComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.5f);
                slots[i] = background;

                GameObject textGo = new GameObject("Label");
                textGo.transform.SetParent(slotGo.transform, false);
                RectTransform textRect = textGo.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                Text text = textGo.AddComponent<Text>();
                text.text = labels[i];
                text.font = font;
                text.fontSize = 16;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
            }

            GameObject hintGo = new GameObject("Hints");
            hintGo.transform.SetParent(canvasGo.transform, false);
            RectTransform hintRect = hintGo.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.sizeDelta = new Vector2(900f, 30f);
            hintRect.anchoredPosition = new Vector2(0f, 10f);

            Text hintText = hintGo.AddComponent<Text>();
            hintText.text = "1-4 Select Shape   |   R Rotate   |   LMB Place   |   Q Remove   |   RMB+Drag Look   |   F5 Save   |   F9 Load";
            hintText.font = font;
            hintText.fontSize = 14;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = Color.white;

            BuildPaletteUI paletteUi = canvasGo.AddComponent<BuildPaletteUI>();
            SetPrivateField(paletteUi, "buildPlacer", placer);
            SetPrivateField(paletteUi, "slotBackgrounds", slots);
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
