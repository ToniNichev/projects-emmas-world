using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.ProBuilder;
using UnityEngine.UI;
using Unity.Cinemachine;
using Sandbox.Audio;
using Sandbox.Player;
using Sandbox.Building;
using Sandbox.Multiplayer;
using Sandbox.Save;
using Sandbox.CameraControl;
using Sandbox.Environment;
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

        // Lake placement -- off to one side, away from the flat spawn/build
        // area, with a shore band that blends smoothly into the surrounding
        // terrain rather than a hard-edged pit.
        private const float LakeCenterX = 32f;
        private const float LakeCenterZ = -28f;
        private const float LakeRadius = 14f;
        private const float LakeShoreBlend = 6f;
        private const float LakeSurfaceY = 0.45f;

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
            ApplyBlockTexture(blockMaterial, "BlockGrain", new Color(0.85f, 0.85f, 0.85f), Color.white, 0.35f, 1f, 0.07f, 0.7f);
            ApplyRockTexture(rockMaterial, "RockNoise", new Color(0.4f, 0.4f, 0.42f), new Color(0.62f, 0.62f, 0.65f), new Color(0.2f, 0.2f, 0.22f));
            rockMaterial.mainTextureScale = new Vector2(2f, 2f);
            ApplyWoodTexture(trunkMaterial, "WoodGrain", new Color(0.35f, 0.22f, 0.09f), new Color(0.5f, 0.32f, 0.15f));
            // Default (1,1) tiling wraps the whole 64px texture around the
            // trunk/canopy exactly once, so at normal camera distance the
            // pattern reads as 2-3 soft blobs instead of visible detail --
            // tile it several times like the ground texture does.
            trunkMaterial.mainTextureScale = new Vector2(3f, 2f);
            ApplyFoliageTexture(leafMaterial, "LeafNoise", new Color(0.12f, 0.46f, 0.17f), new Color(0.36f, 0.68f, 0.3f), new Color(0.03f, 0.16f, 0.06f));
            leafMaterial.mainTextureScale = new Vector2(3f, 3f);

            Material waterMaterial = CreateMaterial("Water", new Color(0.15f, 0.45f, 0.7f, 0.75f));
            ApplyWaterTexture(waterMaterial, "WaterRipple", new Color(0.1f, 0.35f, 0.6f, 0.75f), new Color(0.32f, 0.65f, 0.85f, 0.75f));
            waterMaterial.mainTextureScale = new Vector2(6f, 6f);
            waterMaterial.SetFloat("_Glossiness", 0.85f);
            SetMaterialTransparent(waterMaterial);

            Terrain terrain = CreateTerrain(groundMaterial);
            CreateLake(waterMaterial);

            GameObject rockPrefab = CreateRockPrefab(rockMaterial);
            GameObject treePrefab = CreateTreePrefab(trunkMaterial, leafMaterial);
            ScatterEnvironmentProps(terrain, treePrefab, rockPrefab);

            GameObject[] blockPrefabs = CreateShapePrefabs(blockMaterial);
            GameObject placedBlocks = new GameObject("PlacedBlocks");

            GameObject remoteAvatarPrefab = CreateRemoteAvatarPrefab(playerMaterial, playerHeadMaterial, shirtMaterial);

            GameObject player = BuildPlayer(actions, blockPrefabs, placedBlocks.transform, playerMaterial, playerHeadMaterial, shirtMaterial, remoteAvatarPrefab);
            OrbitCameraDragController cameraController = BuildCamera(player.transform);
            BuildPaletteUI(player.GetComponent<BuildPlacer>());
            BuildEventSystem();
            BuildMobileControls(player.GetComponent<ThirdPersonController>(), cameraController, player.GetComponent<BuildPlacer>());

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
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            map.AddAction("Sprint", InputActionType.Button, binding: "<Keyboard>/leftShift");
            map.AddAction("Jump", InputActionType.Button, binding: "<Keyboard>/space");
            map.AddAction("Place", InputActionType.Button, binding: "<Mouse>/leftButton");
            map.AddAction("Remove", InputActionType.Button, binding: "<Keyboard>/q");
            map.AddAction("Rotate", InputActionType.Button, binding: "<Keyboard>/r");
            map.AddAction("Undo", InputActionType.Button, binding: "<Keyboard>/backspace");
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

        // Standard shader defaults to opaque; this is the standard scripted
        // equivalent of picking "Transparent" in the Rendering Mode dropdown.
        private static void SetMaterialTransparent(Material material)
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
        }

        private const int TextureSize = 64;

        // Layered Perlin octaves ("fractional Brownian motion") instead of a
        // single frequency -- reads as organic cloudy/mottled detail rather
        // than the smooth single-blob-size look one octave produces.
        private static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amplitude = 0.5f, frequency = 1f, maxSum = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amplitude * Mathf.PerlinNoise(x * frequency, y * frequency);
                maxSum += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }
            return sum / maxSum;
        }

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

            Color crackColor = baseColor * 0.45f;

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    // Vertical bark-like streaks: primarily varies across x (wraps
                    // around the trunk's circumference), with a little Perlin
                    // jitter so the streaks aren't perfectly straight.
                    float jitter = Mathf.PerlinNoise(x * 0.05f, y * 0.2f) * 3f;
                    float stripe = Mathf.Sin((x + jitter) * 0.8f) * 0.5f + 0.5f;
                    Color pixel = Color.Lerp(baseColor, grainColor, stripe * 0.6f);

                    // Fine roughness between streaks so bark doesn't read as
                    // smooth stripes -- real bark is rough all over. Fbm gives
                    // it an organic, uneven texture instead of a uniform grid
                    // of bumps a single Perlin octave would produce.
                    float roughness = Fbm(x * 0.3f, y * 0.3f, 3);
                    pixel = Color.Lerp(pixel, grainColor, (roughness - 0.5f) * 0.35f);

                    // Sparse thin dark cracks running with the grain.
                    float crackNoise = Mathf.PerlinNoise(x * 0.06f + 50f, y * 0.9f);
                    if (crackNoise > 0.75f)
                        pixel = Color.Lerp(pixel, crackColor, Mathf.InverseLerp(0.75f, 0.92f, crackNoise));

                    texture.SetPixel(x, y, pixel);
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");
            material.mainTexture = texture;
        }

        // Mottled stone surface via fbm, plus a few thin angled mineral-vein
        // cracks -- reads as weathered rock rather than a flat grey blob.
        private static void ApplyRockTexture(Material material, string name, Color baseColor, Color varyColor, Color veinColor)
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
                    float n = Fbm(x * 0.12f, y * 0.12f, 4);
                    n = Mathf.Clamp01(0.5f + (n - 0.5f) * 1.5f);
                    Color pixel = Color.Lerp(baseColor, varyColor, n);

                    // Thin diagonal mineral veins: low-frequency noise near
                    // its midpoint traces a wandering thin line instead of a
                    // wide band.
                    float veinNoise = Fbm(x * 0.05f + 30f, y * 0.05f - 40f, 2);
                    float veinDist = Mathf.Abs(veinNoise - 0.5f);
                    if (veinDist < 0.02f)
                        pixel = Color.Lerp(pixel, veinColor, 1f - veinDist / 0.02f);

                    texture.SetPixel(x, y, pixel);
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");
            material.mainTexture = texture;
        }

        // Fbm ripple pattern for subtle light/dark mottling; WaterAnimator
        // scrolls this material's UV offset at runtime for a simple flowing
        // shimmer without needing a custom shader.
        private static void ApplyWaterTexture(Material material, string name, Color baseColor, Color highlightColor)
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
                    float ripple = Fbm(x * 0.15f, y * 0.15f, 4);
                    float n = Mathf.Clamp01(0.5f + (ripple - 0.5f) * 1.3f);
                    texture.SetPixel(x, y, Color.Lerp(baseColor, highlightColor, n));
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");
            material.mainTexture = texture;
        }

        // Leaves need to read as a canopy of many small clumps rather than a
        // flat tinted gradient: large-scale noise carves out clump/shadow-gap
        // shapes, fine noise adds a dappled speckle on top of each clump.
        private static void ApplyFoliageTexture(Material material, string name, Color baseColor, Color varyColor, Color shadowColor)
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
                    float clumps = Fbm(x * 0.16f, y * 0.16f, 3);
                    float speckle = Mathf.PerlinNoise(x * 0.7f + 100f, y * 0.7f + 100f);

                    float n = Mathf.Clamp01(0.5f + (clumps * 0.7f + speckle * 0.3f - 0.5f) * 1.6f);
                    Color pixel = Color.Lerp(baseColor, varyColor, n);

                    // Deep shadow gaps where clump density is lowest.
                    if (clumps < 0.35f)
                        pixel = Color.Lerp(pixel, shadowColor, Mathf.InverseLerp(0.35f, 0.05f, clumps));

                    texture.SetPixel(x, y, pixel);
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

        // Noise speckle plus a darkened border near each UV edge, so every block
        // face reads as a distinct bevelled unit (a "toy block" look) instead of
        // just a flat tinted surface -- more pronounced than ApplyNoiseTexture's
        // subtle grain. Multiplies with whatever per-instance tint BuildPlacer
        // assigns at placement time, same as the texture it replaces.
        private static void ApplyBlockTexture(Material material, string name, Color baseColor, Color varyColor, float noiseScale, float contrast, float borderWidth, float borderDarken)
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
                    Color pixel = Color.Lerp(baseColor, varyColor, n);

                    float u = x / (float)(TextureSize - 1);
                    float v = y / (float)(TextureSize - 1);
                    float edgeDist = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                    if (edgeDist < borderWidth)
                    {
                        float darken = Mathf.Lerp(borderDarken, 1f, edgeDist / borderWidth);
                        pixel = new Color(pixel.r * darken, pixel.g * darken, pixel.b * darken, 1f);
                    }

                    texture.SetPixel(x, y, pixel);
                }
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

            // Lake constants are in world units; convert to the same
            // grid-index space the heightmap loop below works in.
            Vector2 lakeCenterGrid = new Vector2(
                (LakeCenterX + worldSize / 2f) / worldSize * (resolution - 1),
                (LakeCenterZ + worldSize / 2f) / worldSize * (resolution - 1));
            float lakeRadiusGrid = LakeRadius / worldSize * (resolution - 1);
            float lakeBlendGrid = LakeShoreBlend / worldSize * (resolution - 1);

            float[,] heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float noise = Mathf.PerlinNoise((x + noiseOffsetX) * noiseScale, (z + noiseOffsetZ) * noiseScale);
                    float distFromCenter = Vector2.Distance(new Vector2(x, z), center);
                    float falloff = Mathf.Clamp01(Mathf.InverseLerp(flatRadius, falloffRadius, distFromCenter));
                    float height = noise * falloff;

                    // Carve a basin down to ground level for the lake bed,
                    // blending smoothly back to the ambient terrain height
                    // over the shore band instead of a hard-edged pit.
                    float distFromLake = Vector2.Distance(new Vector2(x, z), lakeCenterGrid);
                    if (distFromLake < lakeRadiusGrid + lakeBlendGrid)
                    {
                        float t = Mathf.Clamp01(Mathf.InverseLerp(lakeRadiusGrid, lakeRadiusGrid + lakeBlendGrid, distFromLake));
                        height = Mathf.Lerp(0f, height, t);
                    }

                    heights[z, x] = height;
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

        // A flattened cylinder gives a circular water surface for free,
        // matching the lake basin's radius exactly with no custom mesh work.
        // No collider -- it's purely visual, so raycasts (block placement,
        // props) pass through to the terrain underneath instead of hitting
        // an invisible flat plane.
        private static void CreateLake(Material waterMaterial)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            water.name = "Lake";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.position = new Vector3(LakeCenterX, LakeSurfaceY, LakeCenterZ);
            water.transform.localScale = new Vector3(LakeRadius * 2f, 0.05f, LakeRadius * 2f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            water.AddComponent<WaterAnimator>();
        }

        private static GameObject CreateRockPrefab(Material material)
        {
            GameObject root = new GameObject("Rock");

            // A smooth sphere reads as a ball, not a rock. Cluster a few
            // angular, differently-rotated cube chunks instead -- also fits
            // the game's blocky aesthetic better than a rounded boulder would.
            CreateRockChunk(root.transform, material, "ChunkMain", Vector3.zero, new Vector3(1f, 0.7f, 0.85f), Quaternion.Euler(8f, 20f, -5f));
            CreateRockChunk(root.transform, material, "ChunkA", new Vector3(0.35f, -0.08f, 0.3f), new Vector3(0.55f, 0.45f, 0.5f), Quaternion.Euler(-12f, 55f, 10f));
            CreateRockChunk(root.transform, material, "ChunkB", new Vector3(-0.4f, -0.12f, -0.25f), new Vector3(0.5f, 0.4f, 0.45f), Quaternion.Euler(15f, -35f, -8f));

            Directory.CreateDirectory(PrefabsFolder);
            string path = $"{PrefabsFolder}/Rock.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);
            if (!success)
                Debug.LogError("SceneBootstrapper: failed to save Rock prefab");
            return prefab;
        }

        private static void CreateRockChunk(Transform parent, Material material, string name, Vector3 localPosition, Vector3 scale, Quaternion rotation)
        {
            GameObject chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chunk.name = name;
            chunk.transform.SetParent(parent, false);
            chunk.transform.localPosition = localPosition;
            chunk.transform.localScale = scale;
            chunk.transform.localRotation = rotation;
            chunk.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static GameObject CreateTreePrefab(Material trunkMaterial, Material leafMaterial)
        {
            GameObject root = new GameObject("Tree");

            // Two tapered segments instead of one uniform cylinder -- a trunk
            // that's the same width top to bottom is a big part of what made
            // this read as a toy rather than a tree.
            CreateTrunkSegment(root.transform, trunkMaterial, "TrunkLower", localY: 0.55f, height: 0.55f, diameter: 0.34f);
            CreateTrunkSegment(root.transform, trunkMaterial, "TrunkUpper", localY: 1.6f, height: 0.5f, diameter: 0.22f);

            // A single sphere reads as a lollipop; clustering a few
            // differently-sized, off-center spheres breaks up the silhouette
            // into something closer to an actual leaf canopy.
            CreateCanopyBlob(root.transform, leafMaterial, "LeavesCenter", new Vector3(0f, 2.7f, 0f), 1.3f);
            CreateCanopyBlob(root.transform, leafMaterial, "LeavesLeft", new Vector3(-0.5f, 2.35f, 0.35f), 0.95f);
            CreateCanopyBlob(root.transform, leafMaterial, "LeavesRight", new Vector3(0.45f, 2.4f, -0.4f), 0.9f);
            CreateCanopyBlob(root.transform, leafMaterial, "LeavesTop", new Vector3(0.1f, 3.05f, -0.15f), 0.75f);

            Directory.CreateDirectory(PrefabsFolder);
            string path = $"{PrefabsFolder}/Tree.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            Object.DestroyImmediate(root);
            if (!success)
                Debug.LogError("SceneBootstrapper: failed to save Tree prefab");
            return prefab;
        }

        private static void CreateTrunkSegment(Transform parent, Material material, string name, float localY, float height, float diameter)
        {
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = name;
            segment.transform.SetParent(parent, false);
            segment.transform.localPosition = new Vector3(0f, localY, 0f);
            segment.transform.localScale = new Vector3(diameter, height, diameter);
            segment.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void CreateCanopyBlob(Transform parent, Material material, string name, Vector3 localPosition, float scale)
        {
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = name;
            blob.transform.SetParent(parent, false);
            blob.transform.localPosition = localPosition;
            blob.transform.localScale = new Vector3(scale, scale, scale);
            blob.GetComponent<Renderer>().sharedMaterial = material;
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
            Vector2 lakeCenter = new Vector2(LakeCenterX, LakeCenterZ);
            float lakeExclusionRadius = LakeRadius + LakeShoreBlend + 2f; // keep props off the shore, not just out of the water

            for (int i = 0; i < count; i++)
            {
                float x, z;
                do
                {
                    x = UnityEngine.Random.Range(-worldSize / 2f, worldSize / 2f);
                    z = UnityEngine.Random.Range(-worldSize / 2f, worldSize / 2f);
                } while (new Vector2(x, z).magnitude < clearRadius || Vector2.Distance(new Vector2(x, z), lakeCenter) < lakeExclusionRadius);

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

        private static GameObject BuildPlayer(InputActionAsset actions, GameObject[] blockPrefabs, Transform blockParent, Material bodyMaterial, Material headMaterial, Material shirtMaterial, GameObject remoteAvatarPrefab)
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

            // Its own GameObject, named to match exactly -- the WebGL bridge's
            // SendMessage('MultiplayerManager', ...) calls look up a GameObject
            // by that literal name in the scene, not by component type. Living
            // on "Player" would silently fail every incoming socket event.
            GameObject multiplayerGo = new GameObject("MultiplayerManager");
            multiplayerGo.transform.SetParent(player.transform, false);
            MultiplayerManager multiplayer = multiplayerGo.AddComponent<MultiplayerManager>();

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

            SetPrivateField(multiplayer, "blockPrefabs", blockPrefabs);
            SetPrivateField(multiplayer, "blockParent", blockParent);
            SetPrivateField(multiplayer, "remoteAvatarPrefab", remoteAvatarPrefab);
            SetPrivateField(multiplayer, "localPlayerTransform", player.transform);

            return player;
        }

        private static GameObject BuildAvatarVisual(Transform parent, Material bodyMaterial, Material headMaterial, Material shirtMaterial)
        {
            // Positions/sizes are in the root's local space, which is centered on
            // the CharacterController (center=(0,0,0), height=2) -- so this spans
            // local y -1 (feet) to +1 (head top), matching the capsule it replaces.
            // Zero local offset from parent, which is what lets CreateRemoteAvatarPrefab
            // below reuse the exact same part layout for a standalone (no
            // CharacterController) prefab positioned directly at a remote
            // player's reported position.
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

            return avatar;
        }

        private static GameObject CreateRemoteAvatarPrefab(Material bodyMaterial, Material headMaterial, Material shirtMaterial)
        {
            GameObject temp = new GameObject("RemoteAvatarRoot");
            GameObject avatar = BuildAvatarVisual(temp.transform, bodyMaterial, headMaterial, shirtMaterial);
            avatar.transform.SetParent(null, true);
            Object.DestroyImmediate(temp);
            avatar.name = "RemoteAvatar";

            Directory.CreateDirectory(PrefabsFolder);
            string path = $"{PrefabsFolder}/RemoteAvatar.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(avatar, path, out bool success);
            Object.DestroyImmediate(avatar);
            if (!success)
                Debug.LogError("SceneBootstrapper: failed to save RemoteAvatar prefab");
            return prefab;
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

        private static OrbitCameraDragController BuildCamera(Transform playerTransform)
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

            return dragController;
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
            canvasGo.AddComponent<GraphicRaycaster>();

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
            hintText.text = "1-4 Select Shape   |   R Rotate   |   LMB Place   |   Q Remove   |   Backspace Undo   |   RMB+Drag Look   |   Scroll/+- Zoom   |   F5 Save   |   F9 Load";
            hintText.font = font;
            hintText.fontSize = 14;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.color = Color.white;

            BuildPaletteUI paletteUi = canvasGo.AddComponent<BuildPaletteUI>();
            SetPrivateField(paletteUi, "buildPlacer", placer);
            SetPrivateField(paletteUi, "slotBackgrounds", slots);
        }

        // UI pointer/drag events (joysticks, buttons) need an EventSystem in
        // the scene to be dispatched at all -- there wasn't one before since
        // the existing UI (palette, hints) was purely visual.
        private static void BuildEventSystem()
        {
            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            InputSystemUIInputModule inputModule = go.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }

        // Left joystick drives movement, right joystick drives camera look --
        // mirrors Roblox's own mobile control scheme. Plus a small cluster of
        // tap buttons for actions that have no touch equivalent otherwise
        // (jump, place, remove, undo). Wired onto the existing player/camera
        // components rather than replacing their keyboard/mouse paths, so
        // desktop input keeps working unchanged alongside these.
        private static void BuildMobileControls(ThirdPersonController playerController, OrbitCameraDragController cameraController, BuildPlacer placer)
        {
            GameObject canvasGo = new GameObject("MobileControlsUI");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // Without this, the EventSystem has no way to route pointer/touch
            // events to anything on this canvas -- the joysticks and buttons
            // would be completely inert regardless of device or input method.
            canvasGo.AddComponent<GraphicRaycaster>();

            Sprite ringSprite = CreateCircleSprite("JoystickRing", new Color(1f, 1f, 1f, 0.35f), ringOnly: true);
            Sprite knobSprite = CreateCircleSprite("JoystickKnob", new Color(1f, 1f, 1f, 0.6f), ringOnly: false);

            // anchoredPosition is now the circle's center (see CreateJoystick),
            // so these are +90 further out on each axis than the old
            // corner-pivot values, keeping the same on-screen layout.
            VirtualJoystick moveJoystick = CreateJoystick(canvasGo.transform, "MoveJoystick", new Vector2(0f, 0f), new Vector2(220f, 220f), ringSprite, knobSprite);
            Vector2 lookJoystickPos = new Vector2(-220f, 220f);
            VirtualJoystick lookJoystick = CreateJoystick(canvasGo.transform, "LookJoystick", new Vector2(1f, 0f), lookJoystickPos, ringSprite, knobSprite);

            SetPrivateField(playerController, "moveJoystick", moveJoystick);
            SetPrivateField(cameraController, "lookJoystick", lookJoystick);

            // Action buttons as 4 rounded-triangle corner pieces that
            // together with the joystick's own circle read as one enclosing
            // rounded square -- each piece is a square quadrant with a
            // concave bite taken out following the circle, and a rounded
            // (not sharp) outer tip. One sprite, mirrored per corner via
            // scale flips instead of 4 separate textures.
            const float cornerExtent = 150f; // reaches well past the joystick's 110 radius
            const float cornerJoystickRadius = 110f; // matches the joystick ring exactly, so the cutout lines up
            const float cornerRound = 30f;
            const float cornerGap = 8f; // small gap from the joystick ring and between adjacent corner pieces
            Sprite cornerSprite = CreateCornerWedgeSprite("ActionCorner", new Color(1f, 1f, 1f, 0.4f), cornerJoystickRadius, cornerRound, cornerGap);

            // Small icons instead of text -- at this button size text was
            // unreadable. Same procedural-generation approach as every other
            // texture in this project, no external art.
            Sprite iconJump = CreateIconSprite("IconJump", Color.white, IconShape.ArrowUp);
            Sprite iconPlace = CreateIconSprite("IconPlace", Color.white, IconShape.Plus);
            Sprite iconUndo = CreateIconSprite("IconUndo", Color.white, IconShape.ArrowLeft);
            Sprite iconRemove = CreateIconSprite("IconRemove", Color.white, IconShape.Cross);

            CreateCornerButton(canvasGo.transform, "JumpButton", iconJump, lookJoystickPos, flipX: false, flipY: false, cornerExtent, cornerSprite, playerController.TriggerJump);
            CreateCornerButton(canvasGo.transform, "PlaceButton", iconPlace, lookJoystickPos, flipX: true, flipY: false, cornerExtent, cornerSprite, placer.PerformPlace);
            CreateCornerButton(canvasGo.transform, "UndoButton", iconUndo, lookJoystickPos, flipX: false, flipY: true, cornerExtent, cornerSprite, placer.PerformUndo);
            CreateCornerButton(canvasGo.transform, "RemoveButton", iconRemove, lookJoystickPos, flipX: true, flipY: true, cornerExtent, cornerSprite, placer.PerformRemove);

            // Fixed aim point for place/remove on touch (there's no cursor to
            // aim from -- BuildPlacer already falls back to screen-center
            // when a Touchscreen is present).
            GameObject crosshairGo = new GameObject("Crosshair");
            crosshairGo.transform.SetParent(canvasGo.transform, false);
            RectTransform crosshairRect = crosshairGo.AddComponent<RectTransform>();
            crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
            crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
            crosshairRect.sizeDelta = new Vector2(10f, 10f);
            crosshairRect.anchoredPosition = Vector2.zero;
            Image crosshairImage = crosshairGo.AddComponent<Image>();
            crosshairImage.sprite = knobSprite;
            crosshairImage.color = new Color(1f, 1f, 1f, 0.8f);
            crosshairImage.raycastTarget = false;

            // Only touch devices need any of this -- MobileControlsVisibility
            // disables the whole canvas at runtime for mouse/keyboard players.
            // (Must stay active here at edit time: a GameObject that starts
            // disabled never runs Awake, so a component depending on Awake to
            // decide whether to show itself would never get the chance to.)
            canvasGo.AddComponent<MobileControlsVisibility>();
        }

        private static VirtualJoystick CreateJoystick(Transform parent, string name, Vector2 cornerAnchor, Vector2 anchoredPosition, Sprite ringSprite, Sprite knobSprite)
        {
            const float backgroundSize = 220f;
            const float knobSize = 95f;

            GameObject bgGo = new GameObject(name);
            bgGo.transform.SetParent(parent, false);
            RectTransform bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = cornerAnchor;
            bgRect.anchorMax = cornerAnchor;
            // Always center-pivoted regardless of which screen corner this is
            // anchored to -- VirtualJoystick's drag math treats the rect's
            // local origin as the "centered, zero offset" reference point.
            // Pivoting at the corner instead (matching cornerAnchor) put that
            // origin at the edge of the circle, so any tap inside it computed
            // a huge offset toward the far corner and immediately snapped the
            // handle there instead of starting centered.
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.sizeDelta = new Vector2(backgroundSize, backgroundSize);
            bgRect.anchoredPosition = anchoredPosition;

            Image bgImage = bgGo.AddComponent<Image>();
            bgImage.sprite = ringSprite;
            bgImage.color = Color.white;

            GameObject knobGo = new GameObject("Knob");
            knobGo.transform.SetParent(bgGo.transform, false);
            RectTransform knobRect = knobGo.AddComponent<RectTransform>();
            knobRect.anchorMin = new Vector2(0.5f, 0.5f);
            knobRect.anchorMax = new Vector2(0.5f, 0.5f);
            knobRect.sizeDelta = new Vector2(knobSize, knobSize);
            knobRect.anchoredPosition = Vector2.zero;

            Image knobImage = knobGo.AddComponent<Image>();
            knobImage.sprite = knobSprite;
            knobImage.color = Color.white;

            VirtualJoystick joystick = bgGo.AddComponent<VirtualJoystick>();
            SetPrivateField(joystick, "handle", knobRect);
            return joystick;
        }

        // Places a petal-shaped wedge button so its rounded base sits flush
        // against the joystick's ring at the given angle, tip pointing
        // outward. angleDegrees is measured counter-clockwise from
        // screen-right (90=up, 180=left), matching standard math convention.
        // Places a rounded-triangle corner piece with its "inner" corner
        // (nearest the joystick, where CreateCornerWedgeSprite puts the
        // concave circular cutout) pinned exactly at joystickCenter, so all
        // 4 corners share one pivot point and only differ by which way they
        // extend outward. flipX/flipY pick which of the 4 diagonal
        // quadrants this occupies and mirror the shared sprite to match --
        // the sprite is authored once, for the unflipped (extends up-right)
        // case.
        private static void CreateCornerButton(Transform parent, string name, Sprite icon, Vector2 joystickCenter, bool flipX, bool flipY, float extent, Sprite cornerSprite, UnityEngine.Events.UnityAction onClick)
        {
            GameObject buttonGo = new GameObject(name);
            buttonGo.transform.SetParent(parent, false);
            RectTransform rect = buttonGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            // The pivot corner is whichever corner of this rect ends up
            // nearest the joystick once flipped -- e.g. flipX pushes the
            // piece leftward, so its "inner" edge is now on the right.
            rect.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 1f : 0f);
            rect.sizeDelta = new Vector2(extent, extent);
            rect.anchoredPosition = joystickCenter;

            GameObject shapeGo = new GameObject("Shape");
            shapeGo.transform.SetParent(buttonGo.transform, false);
            RectTransform shapeRect = shapeGo.AddComponent<RectTransform>();
            shapeRect.anchorMin = Vector2.zero;
            shapeRect.anchorMax = Vector2.one;
            shapeRect.offsetMin = Vector2.zero;
            shapeRect.offsetMax = Vector2.zero;
            // Mirroring around the shape's own center (which spans the same
            // bounds as the button rect) keeps the sprite's inner corner
            // pinned at joystickCenter after flipping, matching the pivot
            // chosen above.
            shapeRect.localScale = new Vector3(flipX ? -1f : 1f, flipY ? -1f : 1f, 1f);

            Image image = shapeGo.AddComponent<Image>();
            image.sprite = cornerSprite;
            image.color = Color.white;
            // An Image's clickable area is its full rectangle by default,
            // regardless of the sprite's own transparency. This rect
            // deliberately extends inward to the joystick's center (so the
            // shape's concave cutout can sit flush against the ring), which
            // put its transparent corner directly on top of the joystick's
            // own drag area -- and since this button is later in the
            // hierarchy (drawn/hit-tested on top), it was swallowing all of
            // the joystick's touches. Restricting hit-testing to actually
            // opaque pixels lets touches on the transparent part fall
            // through to the joystick underneath. AlphaHitTestSetter (not a
            // direct property set here) because
            // Image.alphaHitTestMinimumThreshold isn't serialized by Unity
            // -- setting it from this editor script would silently do
            // nothing once the scene is saved and reloaded.
            shapeGo.AddComponent<AlphaHitTestSetter>();

            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            // UnityEvent.AddListener registers a runtime-only listener that
            // Unity's own serializer does NOT persist into the saved scene
            // -- the button would exist and look fine but silently do
            // nothing once loaded from disk (in Play mode or a build).
            // AddPersistentListener is the editor-tooling equivalent of
            // wiring an onClick entry in the Inspector, which does save.
            UnityEventTools.AddPersistentListener(button.onClick, onClick);

            // Icon stays unflipped/upright (unlike Shape) and is centered
            // (pivot 0.5,0.5, not the corner) on a point inset from the
            // outer corner -- away from the joystick -- since that's where
            // this wedge shape's visible area is actually concentrated (the
            // joystick-side corner is cut away). Using a corner pivot here
            // previously left the icon's own half-size unaccounted for, so
            // its center landed inside the cutout instead of the visible
            // triangle.
            GameObject iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(buttonGo.transform, false);
            RectTransform iconRect = iconGo.AddComponent<RectTransform>();
            Vector2 outerCorner = new Vector2(flipX ? 0f : 1f, flipY ? 0f : 1f);
            iconRect.anchorMin = outerCorner;
            iconRect.anchorMax = outerCorner;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(46f, 46f);
            const float inset = 54f;
            iconRect.anchoredPosition = new Vector2(flipX ? inset : -inset, flipY ? inset : -inset);

            Image iconImage = iconGo.AddComponent<Image>();
            iconImage.sprite = icon;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;
        }

        // Antialiased filled circle (knob/buttons) or ring (joystick
        // background), generated the same way the world's other procedural
        // textures are -- no external art anywhere in this project.
        private static Sprite CreateCircleSprite(string name, Color color, bool ringOnly)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float outerRadius = size / 2f - 2f;
            float innerRadius = ringOnly ? outerRadius * 0.72f : 0f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float outerAlpha = 1f - Mathf.Clamp01(Mathf.InverseLerp(outerRadius - 1.5f, outerRadius + 1.5f, dist));
                    float innerAlpha = ringOnly ? Mathf.Clamp01(Mathf.InverseLerp(innerRadius - 1.5f, innerRadius + 1.5f, dist)) : 1f;
                    float alpha = outerAlpha * innerAlpha;
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"{name}_Sprite";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(texture));
            return sprite;
        }

        // Square quadrant piece with a concave circular bite taken out of
        // its "inner" corner (local origin, bottom-left) -- following a
        // circle of `circleRadius` centered there -- and a rounded (not
        // sharp) outer corner at the far end. Reads as a rounded triangle:
        // two roughly-straight edges along the square's sides, curving
        // concave near the joystick and convex-rounded at the outward tip.
        // Authored once for the "extends up-right" orientation; the other 3
        // corners reuse this same sprite mirrored via scale flips (see
        // CreateCornerButton).
        private static Sprite CreateCornerWedgeSprite(string name, Color color, float circleRadius, float cornerRound, float gapMargin)
        {
            const int size = 150;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Vector2 innerCorner = Vector2.zero;
            Vector2 outerCornerCenter = new Vector2(size - cornerRound, size - cornerRound);
            float effectiveRadius = circleRadius + gapMargin;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                    // Concave cutout following the joystick's circular edge,
                    // pushed out by gapMargin so there's a visible gap
                    // between the joystick ring and this piece.
                    float distFromInner = Vector2.Distance(p, innerCorner);
                    float innerAlpha = Mathf.Clamp01(Mathf.InverseLerp(effectiveRadius - 1.5f, effectiveRadius + 1.5f, distFromInner));

                    // Straight-edge margin along the two sides that border
                    // the neighboring (mirrored) corner pieces, so they
                    // don't touch either -- the circular cutout above only
                    // covers the area near the diagonal, not far along a
                    // single axis.
                    float edgeAlpha = Mathf.Clamp01(Mathf.InverseLerp(gapMargin - 1.5f, gapMargin + 1.5f, Mathf.Min(p.x, p.y)));

                    // Rounded outer corner -- only clips pixels in the
                    // corner-rounding box near the far corner; everywhere
                    // else the square's straight edges are left alone.
                    float outerAlpha = 1f;
                    if (p.x > outerCornerCenter.x && p.y > outerCornerCenter.y)
                    {
                        float distFromOuterCenter = Vector2.Distance(p, outerCornerCenter);
                        outerAlpha = 1f - Mathf.Clamp01(Mathf.InverseLerp(cornerRound - 1.5f, cornerRound + 1.5f, distFromOuterCenter));
                    }

                    float alpha = innerAlpha * edgeAlpha * outerAlpha;
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0f, 0f), 100f);
            sprite.name = $"{name}_Sprite";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(texture));
            return sprite;
        }

        private enum IconShape { Plus, Cross, ArrowUp, ArrowLeft }

        // Small bold glyphs for the action buttons -- procedurally drawn
        // like every other texture in this project, no external art/fonts.
        // Text at this button size was unreadable; these read fine small.
        private static Sprite CreateIconSprite(string name, Color color, IconShape shape)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            float center = size / 2f;

            float SoftEdge(float dist, float half) => 1f - Mathf.Clamp01(Mathf.InverseLerp(half - 1f, half + 1f, dist));
            float SoftRange(float v, float lo, float hi) =>
                Mathf.Clamp01(Mathf.InverseLerp(lo - 1f, lo + 1f, v)) * (1f - Mathf.Clamp01(Mathf.InverseLerp(hi - 1f, hi + 1f, v)));

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float alpha;

                    switch (shape)
                    {
                        case IconShape.Plus:
                        {
                            const float half = 5f;
                            const float margin = 8f;
                            float hAlpha = SoftEdge(Mathf.Abs(py - center), half) * SoftRange(px, margin, size - margin);
                            float vAlpha = SoftEdge(Mathf.Abs(px - center), half) * SoftRange(py, margin, size - margin);
                            alpha = Mathf.Clamp01(hAlpha + vAlpha);
                            break;
                        }
                        case IconShape.Cross:
                        {
                            const float half = 5f;
                            float dx = px - center;
                            float dy = py - center;
                            float dist1 = Mathf.Abs(dx - dy) * 0.70710678f;
                            float dist2 = Mathf.Abs(dx + dy) * 0.70710678f;
                            float radius = size * 0.34f;
                            float radial = 1f - Mathf.Clamp01(Mathf.InverseLerp(radius - 1f, radius + 1f, Mathf.Sqrt(dx * dx + dy * dy)));
                            alpha = Mathf.Clamp01(SoftEdge(dist1, half) + SoftEdge(dist2, half)) * radial;
                            break;
                        }
                        case IconShape.ArrowUp:
                        {
                            const float baseY = 14f, apexY = 50f, baseHalfWidth = 17f;
                            float t = Mathf.Clamp01(Mathf.InverseLerp(baseY, apexY, py));
                            float halfWidth = baseHalfWidth * (1f - t);
                            alpha = SoftEdge(Mathf.Abs(px - center), halfWidth) * SoftRange(py, baseY, apexY);
                            break;
                        }
                        case IconShape.ArrowLeft:
                        {
                            const float headTipX = 14f, headBaseX = 33f, headHalfHeight = 15f, stemHalf = 4.5f, stemEndX = 51f;
                            float head = 0f;
                            if (px >= headTipX && px <= headBaseX)
                            {
                                float t = Mathf.InverseLerp(headTipX, headBaseX, px);
                                head = SoftEdge(Mathf.Abs(py - center), headHalfHeight * t);
                            }
                            float stem = SoftEdge(Mathf.Abs(py - center), stemHalf) * SoftRange(px, headBaseX - 4f, stemEndX);
                            alpha = Mathf.Clamp01(head + stem);
                            break;
                        }
                        default:
                            alpha = 0f;
                            break;
                    }

                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = $"{name}_Sprite";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(texture));
            return sprite;
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
