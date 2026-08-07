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
using Sandbox.Audio;
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
        private const string TexturesFolder = "Assets/Textures";

        [MenuItem("Sandbox/Build Scaffolded Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            InputActionAsset actions = BuildInputActions();
            EnsureLayerExists(PlayerLayerName);

            Material groundMaterial = CreateMaterial("Ground", new Color(0.35f, 0.6f, 0.3f));
            Material playerMaterial = CreateMaterial("Player", new Color(0.25f, 0.55f, 0.95f));
            Material playerHeadMaterial = CreateMaterial("PlayerHead", new Color(0.95f, 0.8f, 0.65f));
            Material shirtMaterial = CreateMaterial("Shirt", Color.white);
            Material blockMaterial = CreateMaterial("Block", new Color(0.85f, 0.55f, 0.25f));
            Material rockMaterial = CreateMaterial("Rock", new Color(0.5f, 0.5f, 0.52f));
            Material trunkMaterial = CreateMaterial("Trunk", new Color(0.4f, 0.25f, 0.1f));
            Material leafMaterial = CreateMaterial("Leaves", new Color(0.15f, 0.5f, 0.2f));

            ApplyNoiseTexture(groundMaterial, "GrassNoise", new Color(0.3f, 0.55f, 0.25f), new Color(0.45f, 0.68f, 0.35f), 0.15f, 1.3f);
            groundMaterial.mainTextureScale = new Vector2(24f, 24f);
            ApplyNoiseTexture(playerMaterial, "BodyNoise", new Color(0.2f, 0.5f, 0.9f), new Color(0.3f, 0.6f, 0.95f), 0.4f, 0.5f);
            ApplyNoiseTexture(playerHeadMaterial, "SkinNoise", new Color(0.9f, 0.75f, 0.6f), new Color(0.98f, 0.85f, 0.7f), 0.4f, 0.5f);
            // Solid white base color so the stripe colors show through unmodified
            // (albedo = mainTex * color); the texture itself carries the actual hues.
            ApplyStripeTexture(shirtMaterial, "ShirtStripes", new Color(0.2f, 0.45f, 0.85f), Color.white, 6);
            ApplyNoiseTexture(blockMaterial, "BlockGrain", new Color(0.85f, 0.85f, 0.85f), Color.white, 0.5f, 0.4f);
            ApplyNoiseTexture(rockMaterial, "RockNoise", new Color(0.42f, 0.42f, 0.44f), new Color(0.58f, 0.58f, 0.6f), 0.25f, 1.2f);
            ApplyWoodTexture(trunkMaterial, "WoodGrain", new Color(0.35f, 0.22f, 0.09f), new Color(0.5f, 0.32f, 0.15f));
            ApplyNoiseTexture(leafMaterial, "LeafNoise", new Color(0.12f, 0.45f, 0.18f), new Color(0.22f, 0.58f, 0.25f), 0.3f, 1.4f);

            Terrain terrain = CreateTerrain(groundMaterial);

            GameObject rockPrefab = CreateRockPrefab(rockMaterial);
            GameObject treePrefab = CreateTreePrefab(trunkMaterial, leafMaterial);
            ScatterEnvironmentProps(terrain, treePrefab, rockPrefab);

            GameObject[] blockPrefabs = CreateShapePrefabs(blockMaterial);
            GameObject placedBlocks = new GameObject("PlacedBlocks");

            GameObject player = BuildPlayer(actions, blockPrefabs, placedBlocks.transform, playerMaterial, playerHeadMaterial, shirtMaterial);
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

        private const int TextureSize = 64;

        // Multiplies into whatever tint the material/instance already has
        // (Standard shader albedo = mainTex * color), so this works both for
        // static materials (Ground, Rock, ...) and Block, whose per-instance
        // random color is applied later at placement time.
        private static void ApplyNoiseTexture(Material material, string name, Color baseColor, Color varyColor, float noiseScale, float contrast)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float n = Mathf.PerlinNoise(x * noiseScale, y * noiseScale);
                    n = Mathf.Clamp01(0.5f + (n - 0.5f) * contrast);
                    texture.SetPixel(x, y, Color.Lerp(baseColor, varyColor, n));
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");
            material.mainTexture = texture;
        }

        private static void ApplyWoodTexture(Material material, string name, Color baseColor, Color grainColor)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
            };

            // Vertical bark-like streaks: primarily varies across x (wraps around
            // the trunk's circumference), with a little Perlin jitter so the
            // streaks aren't perfectly straight.
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float jitter = Mathf.PerlinNoise(x * 0.05f, y * 0.2f) * 3f;
                    float stripe = Mathf.Sin((x + jitter) * 0.8f) * 0.5f + 0.5f;
                    texture.SetPixel(x, y, Color.Lerp(baseColor, grainColor, stripe * 0.6f));
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");
            material.mainTexture = texture;
        }

        private static void ApplyStripeTexture(Material material, string name, Color colorA, Color colorB, int stripeCount)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                // Point filtering keeps stripe edges crisp instead of blurring them
                // into a gradient the way Bilinear would.
                filterMode = FilterMode.Point,
            };

            for (int y = 0; y < TextureSize; y++)
            {
                int band = y * stripeCount / TextureSize;
                Color rowColor = band % 2 == 0 ? colorA : colorB;
                for (int x = 0; x < TextureSize; x++)
                    texture.SetPixel(x, y, rowColor);
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");
            material.mainTexture = texture;
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

        private static GameObject BuildPlayer(InputActionAsset actions, GameObject[] blockPrefabs, Transform blockParent, Material bodyMaterial, Material headMaterial, Material shirtMaterial)
        {
            // Root holds collision only (CharacterController); the visible blocky
            // humanoid lives under a child "Avatar" transform so the two can vary
            // independently (e.g. later swapping/animating the avatar without
            // touching collision).
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.layer = LayerMask.NameToLayer(PlayerLayerName);

            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.center = Vector3.zero;
            characterController.radius = 0.5f;
            characterController.height = 2f;

            BuildAvatarVisual(player.transform, bodyMaterial, headMaterial, shirtMaterial);

            player.AddComponent<SoundEffects>();

            ThirdPersonController controller = player.AddComponent<ThirdPersonController>();
            player.AddComponent<AvatarAnimator>();
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

        private static void BuildAvatarVisual(Transform parent, Material bodyMaterial, Material headMaterial, Material shirtMaterial)
        {
            // Positions/sizes are in the root's local space, which is centered on
            // the CharacterController (center=(0,0,0), height=2) -- so this spans
            // local y -1 (feet) to +1 (head top), matching the capsule it replaces.
            GameObject avatar = new GameObject("Avatar");
            avatar.transform.SetParent(parent, false);

            CreateBodyPart(avatar.transform, "Torso", new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.7f, 0.45f), shirtMaterial);
            CreateBodyPart(avatar.transform, "Head", new Vector3(0f, 0.8f, 0f), new Vector3(0.4f, 0.4f, 0.4f), headMaterial);

            // Arms/legs hang from a pivot at the joint (shoulder/hip) rather than
            // being centered on themselves, so AvatarAnimator can rotate the pivot
            // for a real hinge swing instead of the limb just rocking in place.
            CreateLimb(avatar.transform, "LeftArm", new Vector3(-0.6f, 0.6f, 0f), new Vector3(0.3f, 0.7f, 0.3f), headMaterial);
            CreateLimb(avatar.transform, "RightArm", new Vector3(0.6f, 0.6f, 0f), new Vector3(0.3f, 0.7f, 0.3f), headMaterial);
            CreateLimb(avatar.transform, "LeftLeg", new Vector3(-0.2f, -0.1f, 0f), new Vector3(0.35f, 0.9f, 0.35f), bodyMaterial);
            CreateLimb(avatar.transform, "RightLeg", new Vector3(0.2f, -0.1f, 0f), new Vector3(0.35f, 0.9f, 0.35f), bodyMaterial);
        }

        private static void CreateBodyPart(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;

            // Visual only -- the root's CharacterController is the sole collider,
            // so a collider here would just be redundant (and could interfere with
            // BuildPlacer's raycasts, which only exclude the root's own layer).
            Object.DestroyImmediate(part.GetComponent<BoxCollider>());
        }

        private static void CreateLimb(Transform parent, string name, Vector3 pivotLocalPosition, Vector3 limbSize, Material material)
        {
            GameObject pivot = new GameObject($"{name}Pivot");
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pivotLocalPosition;

            // The limb hangs below its pivot rather than being centered on it, so
            // rotating the pivot swings it like a real hinge.
            CreateBodyPart(pivot.transform, name, new Vector3(0f, -limbSize.y / 2f, 0f), limbSize, material);
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
