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
using Sandbox.Obstacles;
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

        // Obstacle course placement -- off to another side from both spawn
        // and the lake, away from the tree/rock scatter (see ScatterProps).
        // Close enough to spawn to be visible just by looking around,
        // rather than requiring blind exploration with no compass/map to
        // guide direction -- the previous spot (43 units away) got players
        // lost in real testing.
        private const float CourseStartX = 25f;
        private const float CourseStartZ = 0f;
        private const float CourseExclusionRadius = 12f; // the spiral tower's footprint is much smaller than the old straight-line course

        // Storytale place placement -- a fireside clearing on the opposite
        // side of spawn from the lake and obstacle course, close enough
        // (like the course) to actually be stumbled across rather than
        // requiring blind exploration.
        private const float StoryPlaceX = -22f;
        private const float StoryPlaceZ = 18f;
        private const float StoryPlaceExclusionRadius = 7f;

        // Hill landmark placement -- opposite corner from the lake, course,
        // and storytale place so every point of interest sits in its own
        // direction from spawn. Tall enough to see over the trees and work
        // as a vantage point/waypoint.
        private const float HillCenterX = -35f;
        private const float HillCenterZ = -32f;
        private const float HillRadius = 22f;
        // Smaller than HillRadius on purpose: trees still grow on the
        // slopes (that's what makes it look like a hill instead of a bald
        // mound), just not right at the summit, so there's a clear payoff
        // view once you've climbed it.
        private const float HillPeakClearRadius = 8f;

        // Elder tent placement -- its own direction from spawn, clear of
        // every other point of interest.
        private const float ElderTentX = 10f;
        private const float ElderTentZ = 35f;
        private const float ElderTentExclusionRadius = 8f;

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

            ApplyImportedTexture(groundMaterial, "Grass001.jpg", new Vector2(20f, 20f));
            ApplyNoiseTexture(playerMaterial, "BodyNoise", new Color(0.2f, 0.5f, 0.9f), new Color(0.3f, 0.6f, 0.95f), 0.4f, 0.5f);
            ApplyNoiseTexture(playerHeadMaterial, "SkinNoise", new Color(0.9f, 0.75f, 0.6f), new Color(0.98f, 0.85f, 0.7f), 0.4f, 0.5f);
            // Solid white base color so the stripe colors show through unmodified
            // (albedo = mainTex * color); the texture itself carries the actual hues.
            ApplyStripeTexture(shirtMaterial, "ShirtStripes", new Color(0.2f, 0.45f, 0.85f), Color.white, 6);
            ApplyBlockTexture(blockMaterial, "BlockGrain", new Color(0.85f, 0.85f, 0.85f), Color.white, 0.35f, 1f, 0.07f, 0.7f);
            ApplyImportedTexture(rockMaterial, "Rock020.jpg", new Vector2(1f, 1f));
            ApplyImportedTexture(trunkMaterial, "Bark001.jpg", new Vector2(1f, 1f));
            trunkMaterial.SetFloat("_Glossiness", 0.15f); // matte bark, not glossy plastic
            ApplyImportedTexture(leafMaterial, "LeafSprig.png", Vector2.one, hasAlpha: true);
            SetMaterialCutout(leafMaterial);
            leafMaterial.SetFloat("_Glossiness", 0.1f); // matte, not shiny plastic

            Material waterMaterial = CreateMaterial("Water", new Color(0.15f, 0.45f, 0.7f, 0.75f));
            ApplyWaterTexture(waterMaterial, "WaterRipple", new Color(0.1f, 0.35f, 0.6f, 0.75f), new Color(0.32f, 0.65f, 0.85f, 0.75f));
            waterMaterial.mainTextureScale = new Vector2(6f, 6f);
            waterMaterial.SetFloat("_Glossiness", 0.85f);
            SetMaterialTransparent(waterMaterial);

            Terrain terrain = CreateTerrain(groundMaterial.mainTexture as Texture2D, rockMaterial.mainTexture as Texture2D);
            CreateLake(waterMaterial);

            GameObject rockPrefab = CreateRockPrefab(rockMaterial);
            GameObject treePrefab = CreateTreePrefab(trunkMaterial, leafMaterial);
            ScatterEnvironmentProps(terrain, treePrefab, rockPrefab);

            BuildSkybox("AlpsSkybox.jpg");

            Material tentMaterial = CreateMaterial("TentCanvas", new Color(0.75f, 0.62f, 0.4f));
            ApplyNoiseTexture(tentMaterial, "TentCanvasNoise", new Color(0.7f, 0.56f, 0.36f), new Color(0.82f, 0.68f, 0.46f), 0.3f, 0.6f);
            Material robeMaterial = CreateMaterial("ElderRobe", new Color(0.35f, 0.28f, 0.42f));
            ApplyNoiseTexture(robeMaterial, "ElderRobeNoise", new Color(0.3f, 0.24f, 0.38f), new Color(0.4f, 0.32f, 0.46f), 0.3f, 0.6f);
            BuildElderTent(terrain, tentMaterial, robeMaterial, playerHeadMaterial);

            GameObject[] blockPrefabs = CreateShapePrefabs(blockMaterial);
            GameObject placedBlocks = new GameObject("PlacedBlocks");

            BuildObstacleCourse(blockPrefabs, terrain);

            Material fireGlowMaterial = CreateMaterial("FireGlow", new Color(1f, 0.45f, 0.1f));
            fireGlowMaterial.EnableKeyword("_EMISSION");
            fireGlowMaterial.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 2.5f);
            BuildStorytalePlace(terrain, trunkMaterial, rockMaterial, fireGlowMaterial);

            GameObject remoteAvatarPrefab = CreateRemoteAvatarPrefab(playerMaterial, playerHeadMaterial, shirtMaterial);

            GameObject player = BuildPlayer(actions, blockPrefabs, placedBlocks.transform, playerMaterial, playerHeadMaterial, shirtMaterial, remoteAvatarPrefab);
            OrbitCameraDragController cameraController = BuildCamera(player.transform);
            BuildPaletteUI(player.GetComponent<BuildPlacer>());
            BuildEventSystem();
            BuildMobileControls(player.GetComponent<ThirdPersonController>(), cameraController, player.GetComponent<BuildPlacer>());
            BuildMirror(player.GetComponent<AvatarCustomization>());

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
            InputAction place = map.AddAction("Place", InputActionType.Button, binding: "<Mouse>/leftButton");
            // Tap-to-place on touch, same as a click does on desktop --
            // BuildPlacer.IsPointerOverUI keeps this from also firing when
            // the tap is actually on the joysticks/corner buttons.
            place.AddBinding("<Touchscreen>/primaryTouch/press");
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

        // Loads a real imported CC0 photo texture (see Assets/Textures/Imported/README.txt)
        // and points the material at it -- white base color so the photo's
        // own colors show through unmodified rather than getting multiplied
        // by a tint meant for the old procedural noise texture.
        private static void ApplyImportedTexture(Material material, string importedFileName, Vector2 tiling, bool hasAlpha = false)
        {
            string path = $"{TexturesFolder}/Imported/{importedFileName}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 9;
                importer.mipmapEnabled = true;
                if (hasAlpha)
                    importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError($"SceneBootstrapper: missing imported texture at {path}");
                return;
            }

            material.color = Color.white;
            material.mainTexture = texture;
            material.mainTextureScale = tiling;
        }

        // Cutout (alpha-tested) so the transparent parts of a leaf-card
        // texture are actually punched out rather than just faded, and
        // double-sided since a flat foliage card needs to be visible from
        // its back as well as its front.
        private static void SetMaterialCutout(Material material)
        {
            material.SetFloat("_Mode", 1f);
            material.SetFloat("_Cutoff", 0.5f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 2450;
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

        private const int TextureSize = 256;

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
                filterMode = FilterMode.Trilinear,
                anisoLevel = 9,
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

        // Fbm ripple pattern for subtle light/dark mottling; WaterAnimator
        // scrolls this material's UV offset at runtime for a simple flowing
        // shimmer without needing a custom shader.
        private static void ApplyWaterTexture(Material material, string name, Color baseColor, Color highlightColor)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 9,
            };

            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float ripple = Fbm(x * 0.15f, y * 0.15f, 4);
                    float n = Mathf.Clamp01(0.5f + (ripple - 0.5f) * 1.3f);
                    Color pixel = Color.Lerp(baseColor, highlightColor, n);

                    // Small bright sun-glint specks: a finer, higher-frequency
                    // noise layer thresholded to only its brightest peaks reads
                    // as scattered light catching tiny wave facets.
                    float glint = Fbm(x * 0.5f + 500f, y * 0.5f + 500f, 2);
                    if (glint > 0.78f)
                        pixel = Color.Lerp(pixel, Color.white, Mathf.InverseLerp(0.78f, 0.95f, glint) * 0.8f);

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
                filterMode = FilterMode.Trilinear,
                anisoLevel = 9,
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

        private static Terrain CreateTerrain(Texture2D grassTexture, Texture2D rockTexture)
        {
            const int resolution = 129; // must be 2^n + 1
            const float worldSize = 120f;
            const float maxHeight = 14f; // raised from 6 -- taller ambient hills, plus headroom for the landmark hill above them
            const float noiseScale = 0.06f; // up from 0.045 -- more frequent, more varied undulation
            const float flatRadius = resolution * 0.14f;   // fully flat around spawn
            const float falloffRadius = resolution * 0.35f; // blends into full hills
            // Ambient hills top out around 9 units (was ~6) -- noticeably
            // hillier throughout, while still leaving clear separation from
            // the landmark hill's ~13-unit peak so it still reads as the
            // one distinct summit rather than blending into the background.
            const float ambientHeightScale = 9f / maxHeight;

            var terrainData = new TerrainData
            {
                heightmapResolution = resolution,
                size = new Vector3(worldSize, maxHeight, worldSize),
            };

            Vector2 center = new Vector2(resolution / 2f, resolution / 2f);
            float noiseOffsetX = 137.2f;
            float noiseOffsetZ = 291.7f;

            // Lake/hill constants are in world units; convert to the same
            // grid-index space the heightmap loop below works in.
            Vector2 lakeCenterGrid = new Vector2(
                (LakeCenterX + worldSize / 2f) / worldSize * (resolution - 1),
                (LakeCenterZ + worldSize / 2f) / worldSize * (resolution - 1));
            float lakeRadiusGrid = LakeRadius / worldSize * (resolution - 1);
            float lakeBlendGrid = LakeShoreBlend / worldSize * (resolution - 1);

            Vector2 hillCenterGrid = new Vector2(
                (HillCenterX + worldSize / 2f) / worldSize * (resolution - 1),
                (HillCenterZ + worldSize / 2f) / worldSize * (resolution - 1));
            float hillRadiusGrid = HillRadius / worldSize * (resolution - 1);

            float[,] heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    // Fbm (layered octaves) instead of single-frequency Perlin --
                    // reads as varied ridges/bumps rather than one smooth undulation.
                    float noise = Fbm((x + noiseOffsetX) * noiseScale, (z + noiseOffsetZ) * noiseScale, 4);
                    float distFromCenter = Vector2.Distance(new Vector2(x, z), center);
                    float falloff = Mathf.Clamp01(Mathf.InverseLerp(flatRadius, falloffRadius, distFromCenter));
                    float height = noise * falloff * ambientHeightScale;

                    // Carve a basin down to ground level for the lake bed,
                    // blending smoothly back to the ambient terrain height
                    // over the shore band instead of a hard-edged pit.
                    float distFromLake = Vector2.Distance(new Vector2(x, z), lakeCenterGrid);
                    if (distFromLake < lakeRadiusGrid + lakeBlendGrid)
                    {
                        float t = Mathf.Clamp01(Mathf.InverseLerp(lakeRadiusGrid, lakeRadiusGrid + lakeBlendGrid, distFromLake));
                        height = Mathf.Lerp(0f, height, t);
                    }

                    // Hill landmark: a smooth (smoothstep) bump that reaches
                    // near the full height range at its center -- much
                    // taller than the ambient rolling hills, so it reads as
                    // a distinct peak rather than just another bump.
                    float distFromHill = Vector2.Distance(new Vector2(x, z), hillCenterGrid);
                    if (distFromHill < hillRadiusGrid)
                    {
                        float t = 1f - distFromHill / hillRadiusGrid;
                        float bump = t * t * (3f - 2f * t);
                        height = Mathf.Max(height, bump * 0.92f);
                    }

                    heights[z, x] = height;
                }
            }
            terrainData.SetHeights(0, 0, heights);

            // Two real terrain layers instead of one flat material: grass
            // everywhere, with the rock photo texture blended in wherever
            // the slope gets steep (hillsides, the lake basin's edge) --
            // bare rock on a steep slope instead of grass reads as actual
            // terrain rather than a painted green sheet.
            var grassLayer = new TerrainLayer { diffuseTexture = grassTexture, tileSize = new Vector2(4f, 4f) };
            var rockLayer = new TerrainLayer { diffuseTexture = rockTexture, tileSize = new Vector2(4f, 4f) };
            Directory.CreateDirectory(TerrainFolder);
            AssetDatabase.CreateAsset(grassLayer, $"{TerrainFolder}/GrassLayer.terrainlayer");
            AssetDatabase.CreateAsset(rockLayer, $"{TerrainFolder}/RockLayer.terrainlayer");
            terrainData.terrainLayers = new[] { grassLayer, rockLayer };

            const int alphaRes = 128;
            terrainData.alphamapResolution = alphaRes;
            float[,,] alphamap = new float[alphaRes, alphaRes, 2];
            for (int ay = 0; ay < alphaRes; ay++)
            {
                for (int ax = 0; ax < alphaRes; ax++)
                {
                    float u = ax / (float)(alphaRes - 1);
                    float v = ay / (float)(alphaRes - 1);
                    float steepness = terrainData.GetSteepness(u, v); // degrees
                    float rockWeight = Mathf.Clamp01(Mathf.InverseLerp(25f, 45f, steepness));
                    alphamap[ay, ax, 0] = 1f - rockWeight;
                    alphamap[ay, ax, 1] = rockWeight;
                }
            }
            terrainData.SetAlphamaps(0, 0, alphamap);

            AssetDatabase.CreateAsset(terrainData, $"{TerrainFolder}/GroundTerrainData.asset");

            GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
            terrainGo.name = "Ground";
            terrainGo.transform.position = new Vector3(-worldSize / 2f, 0f, -worldSize / 2f);

            return terrainGo.GetComponent<Terrain>();
        }

        // A flattened cylinder gives a circular water surface for free,
        // matching the lake basin's radius exactly with no custom mesh work.
        // No collider -- it's purely visual, so raycasts (block placement,
        // props) pass through to the terrain underneath instead of hitting
        // an invisible flat plane.
        private static void CreateLake(Material waterMaterial)
        {
            // A flat Cylinder cap (used previously) has no interior
            // vertices to displace -- just a fan from rim to one center
            // point -- so even with a rippled texture it reads as a static,
            // perfectly flat disc. A proper radial grid mesh gives
            // WaterWave real per-vertex geometry to animate.
            GameObject water = new GameObject("Lake");
            water.transform.position = new Vector3(LakeCenterX, LakeSurfaceY, LakeCenterZ);

            Mesh waterMesh = CreateWaterMesh(LakeRadius, 10, 32);
            Directory.CreateDirectory(TerrainFolder);
            AssetDatabase.CreateAsset(waterMesh, $"{TerrainFolder}/LakeMesh.asset");

            MeshFilter meshFilter = water.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = waterMesh;
            MeshRenderer meshRenderer = water.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = waterMaterial;

            water.AddComponent<WaterAnimator>();
            water.AddComponent<WaterWave>();

            ParticleSystem splashSystem = CreateLakeSplashes();
            CreateLakeSplashTrigger(splashSystem);
        }

        // Radial grid disc (concentric rings of vertices around a center
        // point) instead of a simple fan -- gives WaterWave enough interior
        // vertices to displace for a real wave pattern.
        private static Mesh CreateWaterMesh(float radius, int rings, int segments)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var uvs = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            void AddTriangle(int a, int b, int c)
            {
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
            }

            vertices.Add(Vector3.zero);
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int ring = 1; ring <= rings; ring++)
            {
                float ringRadius = radius * ring / rings;
                for (int seg = 0; seg < segments; seg++)
                {
                    float angle = seg * Mathf.PI * 2f / segments;
                    float x = Mathf.Cos(angle) * ringRadius;
                    float z = Mathf.Sin(angle) * ringRadius;
                    vertices.Add(new Vector3(x, 0f, z));
                    uvs.Add(new Vector2(x / radius * 0.5f + 0.5f, z / radius * 0.5f + 0.5f));
                }
            }

            // Single winding, verified by hand (Cross(p1-p0, p2-p0) with
            // these three points comes out +Y) rather than the previous
            // both-windings approach: duplicating every triangle in reverse
            // made RecalculateNormals average an up-facing and a
            // down-facing normal at every shared vertex, which cancelled
            // toward zero/garbage and rendered as a dark, badly-lit
            // surface. The lake is only ever seen from above in this game,
            // so single-sided, correctly-wound geometry is both simpler and
            // actually correct here (ordinary lighting instead), unlike the
            // foliage cards where the player really does need both sides.
            for (int seg = 0; seg < segments; seg++)
                AddTriangle(0, 1 + (seg + 1) % segments, 1 + seg);

            for (int ring = 0; ring < rings - 1; ring++)
            {
                int ringStart = 1 + ring * segments;
                int nextRingStart = 1 + (ring + 1) * segments;
                for (int seg = 0; seg < segments; seg++)
                {
                    int a = ringStart + seg;
                    int b = ringStart + (seg + 1) % segments;
                    int c = nextRingStart + seg;
                    int d = nextRingStart + (seg + 1) % segments;
                    AddTriangle(a, d, c);
                    AddTriangle(a, b, d);
                }
            }

            var mesh = new Mesh { name = "LakeMesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Small ring-shaped ripples that pop up at random points across the
        // lake and fade out -- gives the surface a bit of ambient life
        // instead of sitting perfectly still. HorizontalBillboard (not
        // plain Billboard) keeps each ripple lying flat on the water no
        // matter the camera angle, like a decal rather than a sprite
        // facing the viewer.
        private static ParticleSystem CreateLakeSplashes()
        {
            Texture2D glowTexture = CreateGlowTexture("SplashGlow");
            Material splashMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended")) { name = "SplashParticle" };
            splashMaterial.mainTexture = glowTexture;
            Directory.CreateDirectory(MaterialsFolder);
            AssetDatabase.CreateAsset(splashMaterial, $"{MaterialsFolder}/SplashParticle.mat");

            // Not parented under the Lake cylinder: that object's transform
            // is scaled by LakeRadius*2 to size the water disc, which would
            // also scale up the particle shape's radius below if this were
            // a child of it.
            GameObject splashGo = new GameObject("LakeSplashes");
            splashGo.transform.position = new Vector3(LakeCenterX, LakeSurfaceY + 0.02f, LakeCenterZ);
            splashGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem splashes = splashGo.AddComponent<ParticleSystem>();
            var main = splashes.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startColor = new Color(1f, 1f, 1f, 0.6f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 20;

            var emission = splashes.emission;
            emission.rateOverTime = 1.5f; // sparse -- a handful visible on the lake at once

            var shape = splashes.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = LakeRadius * 0.85f; // stay shy of the shore
            shape.radiusThickness = 1f; // fill the whole disc, not just the rim

            var sizeOverLifetime = splashes.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.3f, 1f, 1f)); // ripple expanding outward

            var colorOverLifetime = splashes.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0.7f, 0f), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer renderer = splashGo.GetComponent<ParticleSystemRenderer>();
            renderer.material = splashMaterial;
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;

            return splashes;
        }

        // Invisible trigger volume covering the water -- CapsuleCollider
        // with a Y-axis direction gives circular coverage matching the
        // lake's footprint, same trick ObstacleCourseNoBuildZone uses for
        // its own circular boundary.
        private static void CreateLakeSplashTrigger(ParticleSystem splashSystem)
        {
            GameObject triggerGo = new GameObject("LakeSplashTrigger");
            triggerGo.transform.position = new Vector3(LakeCenterX, LakeSurfaceY, LakeCenterZ);

            CapsuleCollider collider = triggerGo.AddComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.direction = 1; // Y-axis
            collider.radius = LakeRadius * 0.95f;
            collider.height = 3f; // generous enough to catch entry from a jump, not just a slow walk-in

            LakeSplashTrigger splashTrigger = triggerGo.AddComponent<LakeSplashTrigger>();
            SetPrivateField(splashTrigger, "splashSystem", splashSystem);
            SetPrivateField(splashTrigger, "surfaceY", LakeSurfaceY);
        }

        // A simple jump-platform course ending in a distinct gold finish
        // platform, wrapped in a no-build trigger zone (see NoBuildZone) so
        // players can't bridge past a hard jump with their own blocks.
        // Course pieces intentionally don't get a PlacedBlock component --
        // they're permanent world geometry, same as trees/rocks, not
        // player-removable building blocks.
        private static void BuildObstacleCourse(GameObject[] blockPrefabs, Terrain terrain)
        {
            GameObject courseRoot = new GameObject("ObstacleCourse");

            float baseY = terrain.SampleHeight(new Vector3(CourseStartX, 0f, CourseStartZ));
            Color climbColor = new Color(0.9f, 0.6f, 0.2f);
            Color startColor = new Color(0.3f, 0.55f, 0.95f);
            Color finishColor = new Color(1f, 0.84f, 0.2f);

            // A spiral staircase around a fixed center point reads as an
            // actual climb (and looks more dramatic) rather than a flat
            // line of jumps. heightStep is comfortably within jump reach
            // (jumpHeight=1.5 in ThirdPersonController); the chord distance
            // between consecutive steps combines with that for a moderate
            // running jump.
            const float radius = 4.2f;
            const float angleStepDegrees = 32f;
            const float heightStep = 1.15f;
            const int climbSteps = 12;
            var shapes = new[] { 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2 }; // cube-heavy, wedge/cylinder/ball for variety

            Vector3 startPos = new Vector3(CourseStartX + radius, baseY + 0.5f, CourseStartZ);
            CreateCoursePlatform(courseRoot.transform, blockPrefabs[0], startPos, new Vector3(3f, 1f, 3f), startColor);

            // Every step's position gets kept around so a couple of the gaps
            // can be bridged with a walk-across obstacle afterward instead
            // of just another jump.
            var stepPositions = new Vector3[climbSteps + 1];
            stepPositions[0] = startPos;

            for (int i = 1; i <= climbSteps; i++)
            {
                float angleRad = i * angleStepDegrees * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(
                    CourseStartX + radius * Mathf.Cos(angleRad),
                    baseY + 0.5f + i * heightStep,
                    CourseStartZ + radius * Mathf.Sin(angleRad));
                CreateCoursePlatform(courseRoot.transform, blockPrefabs[shapes[(i - 1) % shapes.Length]], pos, Vector3.one, climbColor);
                stepPositions[i] = pos;
            }

            // Finish platform: continues the spiral one more step, big and
            // gold at the top -- unmistakably the summit.
            float finishAngle = (climbSteps + 1) * angleStepDegrees * Mathf.Deg2Rad;
            float finishHeight = baseY + 0.5f + (climbSteps + 1) * heightStep;
            Vector3 finishPos = new Vector3(
                CourseStartX + radius * Mathf.Cos(finishAngle),
                finishHeight,
                CourseStartZ + radius * Mathf.Sin(finishAngle));
            CreateCoursePlatform(courseRoot.transform, blockPrefabs[0], finishPos, new Vector3(3.5f, 1f, 3.5f), finishColor);

            // A narrow plank and a tilting seesaw bridge two of the jump
            // gaps partway up the climb -- walkable instead of jumpable,
            // for obstacle variety rather than just more jumps.
            Color skinnyColor = new Color(0.55f, 0.35f, 0.15f);
            Color teeterColor = new Color(0.6f, 0.4f, 0.2f);
            CreateSkinnyBeam(courseRoot.transform, blockPrefabs[0], stepPositions[3], stepPositions[4], skinnyColor);
            CreateTeeterTotter(courseRoot.transform, blockPrefabs[0], stepPositions[7], stepPositions[8], teeterColor);

            // Timer: starts when the player steps onto the start platform,
            // stops when they press the red button waiting on the finish
            // platform.
            ObstacleCourseTimer timer = BuildObstacleCourseTimerUI();

            GameObject startTriggerGo = new GameObject("CourseStartTrigger");
            startTriggerGo.transform.SetParent(courseRoot.transform, false);
            startTriggerGo.transform.position = startPos;
            BoxCollider startTriggerCollider = startTriggerGo.AddComponent<BoxCollider>();
            startTriggerCollider.isTrigger = true;
            startTriggerCollider.size = new Vector3(3f, 2f, 3f);
            CourseStartTrigger startTrigger = startTriggerGo.AddComponent<CourseStartTrigger>();
            SetPrivateField(startTrigger, "timer", timer);

            GameObject buttonGo = Object.Instantiate(blockPrefabs[2], finishPos + new Vector3(0f, 0.65f, 0f), Quaternion.identity, courseRoot.transform);
            buttonGo.name = "TimerButton";
            buttonGo.transform.localScale = new Vector3(0.6f, 0.15f, 0.6f);
            Renderer buttonRenderer = buttonGo.GetComponent<Renderer>();
            if (buttonRenderer != null)
                buttonRenderer.material.color = new Color(0.85f, 0.1f, 0.1f);
            Collider buttonCollider = buttonGo.GetComponent<Collider>();
            if (buttonCollider != null)
                buttonCollider.isTrigger = true;
            CourseFinishButton finishButton = buttonGo.AddComponent<CourseFinishButton>();
            SetPrivateField(finishButton, "timer", timer);

            // Fence ring at a radius clear of the platforms but inside the
            // no-build zone, so the visible boundary and the actual
            // build-lock boundary line up exactly -- a box zone (the
            // original design) wouldn't match a circular fence in every
            // direction (its corners reach further out than its
            // straight edges).
            const float fenceRadius = radius + 2.2f;
            const float zoneRadius = radius + 3f;
            float totalHeight = finishHeight - baseY;
            BuildAttractionFence(courseRoot.transform, blockPrefabs[0], new Vector3(CourseStartX, baseY, CourseStartZ), fenceRadius);

            Vector3 zoneCenter = new Vector3(CourseStartX, baseY + totalHeight / 2f + 1f, CourseStartZ);
            GameObject zoneGo = new GameObject("ObstacleCourseNoBuildZone");
            zoneGo.transform.SetParent(courseRoot.transform, false);
            zoneGo.transform.position = zoneCenter;
            CapsuleCollider zoneCollider = zoneGo.AddComponent<CapsuleCollider>();
            zoneCollider.isTrigger = true;
            zoneCollider.direction = 1; // Y-axis
            zoneCollider.radius = zoneRadius;
            zoneCollider.height = totalHeight + 8f;
            zoneGo.AddComponent<NoBuildZone>();
        }

        // A narrow plank bridging two course steps -- no jump needed, but
        // the width leaves little margin for error walking across.
        private static void CreateSkinnyBeam(Transform parent, GameObject cubePrefab, Vector3 pointA, Vector3 pointB, Color color)
        {
            Vector3 mid = (pointA + pointB) / 2f;
            Vector3 direction = pointB - pointA;
            float length = direction.magnitude;

            GameObject beam = Object.Instantiate(cubePrefab, mid, Quaternion.FromToRotation(Vector3.right, direction.normalized), parent);
            beam.name = "SkinnyBeam";
            beam.transform.localScale = new Vector3(length, 0.3f, 0.5f);
            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;
        }

        // A seesaw plank pivoting at its midpoint between two course steps
        // -- TeeterTotter.cs tilts it at runtime toward whichever end the
        // player is standing closer to.
        private static void CreateTeeterTotter(Transform parent, GameObject cubePrefab, Vector3 pointA, Vector3 pointB, Color color)
        {
            Vector3 mid = (pointA + pointB) / 2f;
            Vector3 direction = pointB - pointA;
            float length = direction.magnitude;

            GameObject pivot = new GameObject("TeeterTotter");
            pivot.transform.SetParent(parent, false);
            pivot.transform.position = mid;
            pivot.transform.rotation = Quaternion.FromToRotation(Vector3.right, direction.normalized);

            GameObject beam = Object.Instantiate(cubePrefab, pivot.transform);
            beam.name = "Plank";
            beam.transform.localPosition = Vector3.zero;
            beam.transform.localRotation = Quaternion.identity;
            beam.transform.localScale = new Vector3(length, 0.3f, 0.5f);
            Renderer renderer = beam.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;

            TeeterTotter teeter = pivot.AddComponent<TeeterTotter>();
            SetPrivateField(teeter, "halfLength", length / 2f);
        }

        // Screen-space timer readout for the obstacle course -- its own
        // small canvas since it's purely a display, no interaction, so it
        // doesn't need a GraphicRaycaster/EventSystem the way the palette
        // and mobile control canvases do.
        private static ObstacleCourseTimer BuildObstacleCourseTimerUI()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasGo = new GameObject("CourseTimerUI");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject textGo = new GameObject("TimerText");
            textGo.transform.SetParent(canvasGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 1f);
            textRect.anchorMax = new Vector2(0.5f, 1f);
            textRect.pivot = new Vector2(0.5f, 1f);
            textRect.sizeDelta = new Vector2(400f, 50f);
            textRect.anchoredPosition = new Vector2(0f, -20f);

            Text text = textGo.AddComponent<Text>();
            text.font = font;
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = string.Empty;

            ObstacleCourseTimer timer = canvasGo.AddComponent<ObstacleCourseTimer>();
            SetPrivateField(timer, "displayText", text);
            return timer;
        }

        // A ring of alternating red/white posts with connecting rails around
        // a fixed center point -- reads as an amusement-park attraction
        // railing. Purely decorative (no colliders): it marks the boundary,
        // it doesn't block movement, since the actual build-lock is handled
        // by ObstacleCourseNoBuildZone's trigger volume.
        private static void BuildAttractionFence(Transform parent, GameObject cubePrefab, Vector3 groundCenter, float fenceRadius)
        {
            const int postCount = 16;
            const float postHeight = 1.1f;
            const float postThickness = 0.18f;
            const float railHeight = 0.7f;
            const float railThickness = 0.1f;
            Color postColorA = Color.white;
            Color postColorB = new Color(0.85f, 0.15f, 0.15f);

            var postPositions = new Vector3[postCount];
            for (int i = 0; i < postCount; i++)
            {
                float angle = i * (360f / postCount) * Mathf.Deg2Rad;
                postPositions[i] = groundCenter + new Vector3(Mathf.Cos(angle) * fenceRadius, 0f, Mathf.Sin(angle) * fenceRadius);

                GameObject post = CreateCoursePlatform(parent, cubePrefab, postPositions[i] + new Vector3(0f, postHeight / 2f, 0f),
                    new Vector3(postThickness, postHeight, postThickness), i % 2 == 0 ? postColorA : postColorB);
                Object.DestroyImmediate(post.GetComponent<Collider>());
            }

            for (int i = 0; i < postCount; i++)
            {
                Vector3 a = postPositions[i];
                Vector3 b = postPositions[(i + 1) % postCount];
                Vector3 direction = b - a;
                float length = direction.magnitude;
                Vector3 midpoint = (a + b) / 2f + new Vector3(0f, railHeight, 0f);

                GameObject rail = Object.Instantiate(cubePrefab, midpoint, Quaternion.FromToRotation(Vector3.right, direction.normalized), parent);
                rail.transform.localScale = new Vector3(length * 1.05f, railThickness, railThickness);
                Renderer railRenderer = rail.GetComponent<Renderer>();
                if (railRenderer != null)
                    railRenderer.material.color = Color.white;
                Object.DestroyImmediate(rail.GetComponent<Collider>());
            }
        }

        private static GameObject CreateCoursePlatform(Transform parent, GameObject prefab, Vector3 position, Vector3 scale, Color color)
        {
            GameObject platform = Object.Instantiate(prefab, position, Quaternion.identity, parent);
            platform.transform.localScale = scale;

            Renderer renderer = platform.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = color;

            return platform;
        }

        // A fireside clearing: a stone-ringed pit with crossed logs and a
        // flickering flame at its center, plus a few sitting-logs around it
        // -- the kind of spot a group would gather to tell stories.
        private static void BuildStorytalePlace(Terrain terrain, Material logMaterial, Material stoneMaterial, Material fireGlowMaterial)
        {
            GameObject root = new GameObject("StorytalePlace");
            float baseY = terrain.SampleHeight(new Vector3(StoryPlaceX, 0f, StoryPlaceZ));
            root.transform.position = new Vector3(StoryPlaceX, baseY, StoryPlaceZ);

            BuildFirePit(root.transform, stoneMaterial, logMaterial, fireGlowMaterial);
            BuildStoryBenches(root.transform, logMaterial);
        }

        private static void BuildFirePit(Transform parent, Material stoneMaterial, Material logMaterial, Material fireGlowMaterial)
        {
            const int stoneCount = 10;
            const float ringRadius = 1.15f;
            for (int i = 0; i < stoneCount; i++)
            {
                float angle = i * (360f / stoneCount) * Mathf.Deg2Rad;
                GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
                stone.name = $"FireStone{i}";
                stone.transform.SetParent(parent, false);
                stone.transform.localPosition = new Vector3(Mathf.Cos(angle) * ringRadius, 0.12f, Mathf.Sin(angle) * ringRadius);
                stone.transform.localScale = new Vector3(0.32f, 0.24f, 0.28f);
                stone.transform.localRotation = Quaternion.Euler(
                    UnityEngine.Random.Range(-10f, 10f),
                    UnityEngine.Random.Range(0f, 360f),
                    UnityEngine.Random.Range(-10f, 10f));
                stone.GetComponent<Renderer>().sharedMaterial = stoneMaterial;
            }

            // Four leaning logs meeting above the center, teepee-style --
            // slightly uneven angles/lengths so it reads as a stacked pile
            // rather than a perfectly symmetric prop.
            CreateLeaningLog(parent, logMaterial, "Log0", 5f, 0.75f, 1.15f, 0.14f);
            CreateLeaningLog(parent, logMaterial, "Log1", 95f, 0.8f, 1.05f, 0.13f);
            CreateLeaningLog(parent, logMaterial, "Log2", 185f, 0.7f, 1.1f, 0.14f);
            CreateLeaningLog(parent, logMaterial, "Log3", 268f, 0.78f, 1.0f, 0.12f);

            GameObject flame = new GameObject("Flame");
            flame.transform.SetParent(parent, false);
            flame.transform.localPosition = new Vector3(0f, 0.1f, 0f);

            // Small squashed glow at the base, mostly tucked among the logs
            // -- keeps a warm coal-like glow visible even at moments the
            // sparser particles above thin out.
            CreateFlameBlob(flame.transform, fireGlowMaterial, "Embers", new Vector3(0f, 0.03f, 0f), 0.2f);

            CreateFireParticles(flame.transform);

            Light fireLight = flame.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.55f, 0.2f);
            fireLight.intensity = 2.5f;
            fireLight.range = 9f;
            flame.AddComponent<FireFlicker>();
        }

        // A real particle flame reads far better than static stretched
        // spheres: licking, randomized shapes with a proper yellow-white ->
        // orange -> red-and-fading color ramp, plus sparse rising smoke on
        // top for extra realism.
        private static void CreateFireParticles(Transform parent)
        {
            // A real flame-shaped alpha mask (tapered, wavy-edged silhouette)
            // instead of a plain soft circle -- a stretched circle reads as
            // a spark/streak, not a flame, no matter how it's sized.
            Texture2D flameTexture = CreateFlameTexture("FireFlameShape");

            Material flameParticleMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Additive")) { name = "FireParticle" };
            flameParticleMaterial.mainTexture = flameTexture;
            Directory.CreateDirectory(MaterialsFolder);
            AssetDatabase.CreateAsset(flameParticleMaterial, $"{MaterialsFolder}/FireParticle.mat");

            GameObject flameGo = new GameObject("FireParticles");
            flameGo.transform.SetParent(parent, false);

            ParticleSystem flame = flameGo.AddComponent<ParticleSystem>();
            var main = flame.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            // Low speed on purpose -- each particle already looks like a
            // flame lick by itself now, so the flicker should come from
            // overlapping/rotating/rescaling shapes near the logs, not from
            // particles traveling far and spreading thin.
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.55f, 0.95f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-25f * Mathf.Deg2Rad, 25f * Mathf.Deg2Rad);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.55f, 0.15f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 60;

            var emission = flame.emission;
            emission.rateOverTime = 22f;

            var shape = flame.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.2f;

            var colorOverLifetime = flame.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var flameGradient = new Gradient();
            flameGradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                    new GradientColorKey(new Color(1f, 0.5f, 0.1f), 0.5f),
                    new GradientColorKey(new Color(0.6f, 0.1f, 0.05f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = flameGradient;

            var sizeOverLifetime = flame.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.85f, 1f, 0.2f));

            // Continuous rotation plus turbulence -- together these make
            // each flame shape twist and waver instead of just rising as a
            // rigid billboard.
            var rotationOverLifetime = flame.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-45f * Mathf.Deg2Rad, 45f * Mathf.Deg2Rad);

            var noise = flame.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.6f;

            ParticleSystemRenderer flameRenderer = flameGo.GetComponent<ParticleSystemRenderer>();
            flameRenderer.material = flameParticleMaterial;
            flameRenderer.renderMode = ParticleSystemRenderMode.Billboard;

            BuildSmoke(parent);
        }

        // Flame silhouette: a single sine hump gives the classic "wide
        // through the middle, pinched at the base, pointed at the tip"
        // candle-flame profile; per-pixel fbm noise perturbs the edge so it
        // reads as organic rather than a stamped-out teardrop.
        private static Texture2D CreateFlameTexture(string name)
        {
            const int width = 48;
            const int height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            for (int y = 0; y < height; y++)
            {
                float h = y / (float)(height - 1); // 0 at the base, 1 at the tip
                float widthFactor = Mathf.Max(0f, Mathf.Sin(Mathf.Pow(h, 0.6f) * Mathf.PI) * (1f - h * 0.15f));

                for (int x = 0; x < width; x++)
                {
                    float dx = (x - width / 2f + 0.5f) / (width / 2f); // -1..1
                    float edgeNoise = Fbm(x * 0.15f, y * 0.15f + 100f, 3);
                    float jitteredWidth = widthFactor * (0.75f + edgeNoise * 0.5f);

                    float edge = jitteredWidth - Mathf.Abs(dx);
                    float alpha = Mathf.Clamp01(edge / 0.12f);
                    alpha *= Mathf.Clamp01(1.15f - h); // extra soft fade right at the tip

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");
            return texture;
        }

        // Sparse, slow, and mostly transparent -- meant to be noticed
        // rather than stared at, like real campfire smoke.
        private static void BuildSmoke(Transform parent)
        {
            Texture2D glowTexture = CreateGlowTexture("SmokeGlowParticle");
            Material smokeMaterial = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended")) { name = "SmokeParticle" };
            smokeMaterial.mainTexture = glowTexture;
            Directory.CreateDirectory(MaterialsFolder);
            AssetDatabase.CreateAsset(smokeMaterial, $"{MaterialsFolder}/SmokeParticle.mat");

            GameObject smokeGo = new GameObject("Smoke");
            smokeGo.transform.SetParent(parent, false);
            smokeGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            ParticleSystem smoke = smokeGo.AddComponent<ParticleSystem>();
            var main = smoke.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startColor = new Color(0.3f, 0.3f, 0.3f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 30;

            var emission = smoke.emission;
            emission.rateOverTime = 4f;

            var shape = smoke.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.18f;

            var colorOverLifetime = smoke.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var smokeGradient = new Gradient();
            smokeGradient.SetKeys(
                new[] { new GradientColorKey(Color.gray, 0f), new GradientColorKey(Color.gray, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.25f, 0.3f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = smokeGradient;

            var sizeOverLifetime = smoke.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.5f));

            ParticleSystemRenderer smokeRenderer = smokeGo.GetComponent<ParticleSystemRenderer>();
            smokeRenderer.material = smokeMaterial;
            smokeRenderer.alignment = ParticleSystemRenderSpace.View;
        }

        // White with a soft radial falloff baked into alpha -- works both as
        // an additive glow sprite (alpha modulates brightness) and as a
        // plain soft circle for alpha-blended smoke.
        private static Texture2D CreateGlowTexture(string name)
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            Vector2 center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / (size / 2f);
                    float falloff = Mathf.Clamp01(1f - dist);
                    falloff *= falloff;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, falloff));
                }
            }
            texture.Apply();

            Directory.CreateDirectory(TexturesFolder);
            AssetDatabase.CreateAsset(texture, $"{TexturesFolder}/{name}.asset");
            return texture;
        }

        // FromToRotation on the cylinder's default up axis both aims and lays
        // it down in one step: base sits at the ring, tip leans in toward a
        // point above center, and the log's length runs along that line.
        private static void CreateLeaningLog(Transform parent, Material material, string name, float angleDegrees, float baseRadius, float length, float diameter)
        {
            GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            log.name = name;
            log.transform.SetParent(parent, false);

            float rad = angleDegrees * Mathf.Deg2Rad;
            Vector3 basePoint = new Vector3(Mathf.Cos(rad), 0.05f, Mathf.Sin(rad)) * baseRadius;
            Vector3 tipPoint = new Vector3(0f, length * 0.85f, 0f);
            Vector3 direction = (tipPoint - basePoint).normalized;

            log.transform.localPosition = (basePoint + tipPoint) / 2f;
            log.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            log.transform.localScale = new Vector3(diameter, length / 2f, diameter);
            log.GetComponent<Renderer>().sharedMaterial = material;
        }

        // Same "base on a ring, tip at a shared point overhead" convergence
        // as CreateLeaningLog, but a flat board instead of a round pole, with
        // its orientation fully controlled (not just FromToRotation's
        // arbitrary roll) so its width actually runs tangentially around the
        // ring -- that's what lets neighboring panels overlap into a solid
        // wall instead of leaving round-pole-shaped gaps between them.
        // Keeps its default BoxCollider, unlike most decorative props here:
        // this is a wall, it should actually block the player outside the
        // doorway gap.
        private static void CreateTentPanel(Transform parent, Material material, string name, float angleDegrees, float baseRadius, float length, float width, float thickness)
        {
            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = name;
            panel.transform.SetParent(parent, false);

            float rad = angleDegrees * Mathf.Deg2Rad;
            Vector3 radialDir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));
            Vector3 tangentDir = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));
            Vector3 basePoint = radialDir * baseRadius + new Vector3(0f, 0.05f, 0f);
            Vector3 tipPoint = new Vector3(0f, length * 0.85f, 0f);
            Vector3 leanDir = (tipPoint - basePoint).normalized;

            panel.transform.localPosition = (basePoint + tipPoint) / 2f;
            // leanDir lies entirely in the {radialDir, up} plane (basePoint
            // and tipPoint are both on that plane), so it has zero
            // component along tangentDir -- the two are already exactly
            // perpendicular, meaning LookRotation needs no orthogonalizing
            // adjustment: local Z lands exactly on tangentDir (width axis)
            // and local Y exactly on leanDir (length axis, the actual lean).
            panel.transform.localRotation = Quaternion.LookRotation(tangentDir, leanDir);
            panel.transform.localScale = new Vector3(thickness, Vector3.Distance(basePoint, tipPoint), width);

            Renderer renderer = panel.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
        }

        // Squashed flat rather than stretched -- a bed of embers glowing
        // among the logs, not a flame shape (the particles above handle
        // that). No collider since it's purely visual.
        private static void CreateFlameBlob(Transform parent, Material material, string name, Vector3 localPosition, float scale)
        {
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = name;
            blob.transform.SetParent(parent, false);
            blob.transform.localPosition = localPosition;
            blob.transform.localScale = new Vector3(scale, scale * 0.4f, scale);
            Object.DestroyImmediate(blob.GetComponent<Collider>());
            blob.GetComponent<Renderer>().sharedMaterial = material;
        }

        // Sitting logs ringed around the fire, gapped on one side so
        // there's an obvious way in rather than a solid ring you'd have to
        // walk over to reach the fire.
        private static void BuildStoryBenches(Transform parent, Material logMaterial)
        {
            float[] benchAngles = { -70f, -25f, 25f, 70f, 145f, -145f };
            const float benchRadius = 3.2f;
            const float benchLength = 1.6f;
            const float benchDiameter = 0.4f;

            foreach (float angle in benchAngles)
            {
                float rad = angle * Mathf.Deg2Rad;
                Vector3 tangent = new Vector3(-Mathf.Sin(rad), 0f, Mathf.Cos(rad));

                GameObject bench = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bench.name = $"Bench{angle}";
                bench.transform.SetParent(parent, false);
                bench.transform.localPosition = new Vector3(Mathf.Cos(rad) * benchRadius, benchDiameter / 2f, Mathf.Sin(rad) * benchRadius);
                // Lays the cylinder on its side with its length running
                // along the tangent, so it sits facing the fire like a bench.
                bench.transform.localRotation = Quaternion.FromToRotation(Vector3.up, tangent);
                bench.transform.localScale = new Vector3(benchDiameter, benchLength / 2f, benchDiameter);
                bench.GetComponent<Renderer>().sharedMaterial = logMaterial;
            }
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

        // A real photographic panorama (see Assets/Textures/Imported/README.txt)
        // on a Skybox/Panoramic material -- infinitely distant and
        // genuinely photorealistic, unlike any amount of foreground
        // geometry could be. Replaced an earlier cube-chunk mountain range
        // that read as clearly blocky up close.
        private static void BuildSkybox(string importedFileName)
        {
            string path = $"{TexturesFolder}/Imported/{importedFileName}";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 4096;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                Debug.LogError($"SceneBootstrapper: missing imported skybox texture at {path}");
                return;
            }

            Material skyboxMaterial = new Material(Shader.Find("Skybox/Panoramic")) { name = "Skybox" };
            skyboxMaterial.SetTexture("_MainTex", texture);
            skyboxMaterial.SetFloat("_Exposure", 1f);
            Directory.CreateDirectory(MaterialsFolder);
            AssetDatabase.CreateAsset(skyboxMaterial, $"{MaterialsFolder}/Skybox.mat");

            RenderSettings.skybox = skyboxMaterial;
            DynamicGI.UpdateEnvironment();
        }

        // A generic wise-traveler's tent: not tied to any specific real
        // culture or spiritual tradition, deliberately -- a plain canvas
        // cone and a robed figure who shares a short, kid-friendly line of
        // wisdom when you step inside.
        private static void BuildElderTent(Terrain terrain, Material tentMaterial, Material robeMaterial, Material skinMaterial)
        {
            float baseY = terrain.SampleHeight(new Vector3(ElderTentX, 0f, ElderTentZ));
            GameObject tentRoot = new GameObject("ElderTent");
            tentRoot.transform.position = new Vector3(ElderTentX, baseY, ElderTentZ);

            // Wide flat panels leaning in to a shared apex (like the
            // campfire's log pile, but boards instead of round poles so
            // adjacent panels actually overlap into a solid-looking wall
            // instead of leaving gaps you can see the sky through). Three
            // consecutive slots are skipped for an actual doorway -- facing
            // roughly southwest, back toward spawn, so it's the side a
            // player walking up to the tent sees first.
            const int panelCount = 18;
            const float baseRadius = 3.4f;
            const float tentLength = 7f;
            const float panelWidth = 1.5f;
            const float panelThickness = 0.18f;
            for (int i = 0; i < panelCount; i++)
            {
                if (i >= 11 && i <= 13)
                    continue; // doorway

                float angle = i * (360f / panelCount);
                CreateTentPanel(tentRoot.transform, tentMaterial, $"Panel{i}", angle, baseRadius, tentLength, panelWidth, panelThickness);
            }

            // Floor mat.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Floor";
            floor.transform.SetParent(tentRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            floor.transform.localScale = new Vector3(baseRadius * 1.6f, 0.03f, baseRadius * 1.6f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            Renderer floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
                floorRenderer.sharedMaterial = tentMaterial;

            CreateElderNpc(tentRoot.transform, robeMaterial, skinMaterial);

            Text speechText = BuildSpeechUI();

            GameObject triggerGo = new GameObject("ElderTentTrigger");
            triggerGo.transform.SetParent(tentRoot.transform, false);
            triggerGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            ElderTentSpeech speech = triggerGo.AddComponent<ElderTentSpeech>();
            SetPrivateField(speech, "displayText", speechText);
            SetPrivateField(speech, "triggerRadius", baseRadius * 0.9f);
        }

        // A simple standing robed figure, reusing the same body-part
        // primitives as the player avatar (CreateBodyPart/CreateLimb).
        private static void CreateElderNpc(Transform parent, Material robeMaterial, Material skinMaterial)
        {
            GameObject elder = new GameObject("Elder");
            elder.transform.SetParent(parent, false);
            elder.transform.localPosition = new Vector3(0f, 0f, -0.6f);

            CreateBodyPart(elder.transform, "Torso", new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 1.1f, 0.5f), robeMaterial);
            CreateBodyPart(elder.transform, "Head", new Vector3(0f, 1.65f, 0f), new Vector3(0.4f, 0.4f, 0.4f), skinMaterial);
            CreateLimb(elder.transform, "LeftArm", new Vector3(-0.45f, 1.3f, 0f), new Vector3(0.25f, 0.8f, 0.25f), robeMaterial);
            CreateLimb(elder.transform, "RightArm", new Vector3(0.45f, 1.3f, 0f), new Vector3(0.25f, 0.8f, 0.25f), robeMaterial);
            CreateLimb(elder.transform, "LeftLeg", new Vector3(-0.2f, 0.4f, 0f), new Vector3(0.3f, 0.8f, 0.3f), robeMaterial);
            CreateLimb(elder.transform, "RightLeg", new Vector3(0.2f, 0.4f, 0f), new Vector3(0.3f, 0.8f, 0.3f), robeMaterial);

            // A simple walking staff propped beside them.
            GameObject staff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            staff.name = "Staff";
            staff.transform.SetParent(elder.transform, false);
            staff.transform.localPosition = new Vector3(0.55f, 0.9f, 0f);
            staff.transform.localScale = new Vector3(0.06f, 0.9f, 0.06f);
            Object.DestroyImmediate(staff.GetComponent<Collider>());
            Renderer staffRenderer = staff.GetComponent<Renderer>();
            if (staffRenderer != null)
                staffRenderer.sharedMaterial = robeMaterial;
        }

        // Bottom-center text readout for ElderTentSpeech -- empty until the
        // player steps into the tent's trigger. Its own small canvas, no
        // GraphicRaycaster/EventSystem needed since it's display-only.
        private static Text BuildSpeechUI()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasGo = new GameObject("ElderSpeechUI");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject textGo = new GameObject("SpeechText");
            textGo.transform.SetParent(canvasGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0f);
            textRect.anchorMax = new Vector2(0.5f, 0f);
            textRect.pivot = new Vector2(0.5f, 0f);
            textRect.sizeDelta = new Vector2(1100f, 140f);
            textRect.anchoredPosition = new Vector2(0f, 140f);

            Text text = textGo.AddComponent<Text>();
            text.font = font;
            text.fontSize = 40;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = string.Empty;

            // Dark outline so bold white text stays readable against sky,
            // grass, or the tent's own tan canvas behind it.
            Outline outline = textGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.1f, 0.05f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            return text;
        }

        // A simple standing mirror near spawn -- a framed glossy panel on
        // a stand. Not a real reflection (that needs a second camera and a
        // RenderTexture, real cost/complexity for something purely
        // cosmetic); a shiny flat panel reads as "mirror" well enough for
        // a customization prop.
        private static void BuildMirror(AvatarCustomization customization)
        {
            Vector3 mirrorPos = new Vector3(-6f, 0f, -3f);
            GameObject mirrorGo = new GameObject("Mirror");
            mirrorGo.transform.position = mirrorPos;
            mirrorGo.transform.rotation = Quaternion.Euler(0f, 30f, 0f); // angled back toward spawn

            Material frameMaterial = CreateMaterial("MirrorFrame", new Color(0.3f, 0.22f, 0.15f));
            Material glassMaterial = CreateMaterial("MirrorGlass", new Color(0.75f, 0.8f, 0.85f));
            glassMaterial.SetFloat("_Glossiness", 0.95f);
            glassMaterial.SetFloat("_Metallic", 0.6f);

            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Frame";
            frame.transform.SetParent(mirrorGo.transform, false);
            frame.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            frame.transform.localScale = new Vector3(1.1f, 1.8f, 0.12f);
            frame.GetComponent<Renderer>().sharedMaterial = frameMaterial;

            GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            glass.name = "Glass";
            glass.transform.SetParent(mirrorGo.transform, false);
            glass.transform.localPosition = new Vector3(0f, 1.1f, 0.07f);
            glass.transform.localScale = new Vector3(0.9f, 1.55f, 0.03f);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            glass.GetComponent<Renderer>().sharedMaterial = glassMaterial;

            GameObject stand = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stand.name = "Stand";
            stand.transform.SetParent(mirrorGo.transform, false);
            stand.transform.localPosition = new Vector3(0f, 0.1f, -0.15f);
            stand.transform.localScale = new Vector3(0.5f, 0.2f, 0.4f);
            stand.GetComponent<Renderer>().sharedMaterial = frameMaterial;

            GameObject panel = BuildMirrorPanel(customization);

            MirrorProximityUI proximity = mirrorGo.AddComponent<MirrorProximityUI>();
            SetPrivateField(proximity, "panel", panel);
            SetPrivateField(proximity, "radius", 2.5f);
        }

        // Hidden by default (MirrorProximityUI shows/hides it based on
        // distance to the mirror) -- three rows of color swatches for
        // shirt, skin, and pants.
        private static GameObject BuildMirrorPanel(AvatarCustomization customization)
        {
            GameObject canvasGo = new GameObject("MirrorUI");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(420f, 220f);
            panelRect.anchoredPosition = new Vector2(0f, -40f);
            Image panelBackground = panel.AddComponent<Image>();
            panelBackground.color = new Color(0f, 0f, 0f, 0.55f);

            Color[] shirtColors =
            {
                new Color(0.2f, 0.45f, 0.85f), new Color(0.85f, 0.2f, 0.2f), new Color(0.2f, 0.7f, 0.3f),
                new Color(0.9f, 0.8f, 0.2f), new Color(0.6f, 0.2f, 0.75f),
            };
            Color[] skinColors =
            {
                new Color(0.96f, 0.8f, 0.65f), new Color(0.85f, 0.65f, 0.45f),
                new Color(0.6f, 0.42f, 0.28f), new Color(0.4f, 0.28f, 0.18f),
            };
            Color[] legColors =
            {
                new Color(0.25f, 0.35f, 0.55f), new Color(0.15f, 0.15f, 0.15f), new Color(0.5f, 0.35f, 0.2f),
                new Color(0.3f, 0.3f, 0.3f), new Color(0.35f, 0.5f, 0.3f),
            };

            BuildSwatchRow(panel.transform, "Shirt", 0, shirtColors, ColorSwatchButton.Target.Shirt, customization);
            BuildSwatchRow(panel.transform, "Skin", 1, skinColors, ColorSwatchButton.Target.Skin, customization);
            BuildSwatchRow(panel.transform, "Pants", 2, legColors, ColorSwatchButton.Target.Legs, customization);

            return panel;
        }

        private static void BuildSwatchRow(Transform parent, string label, int rowIndex, Color[] colors, ColorSwatchButton.Target target, AvatarCustomization customization)
        {
            float rowY = -10f - rowIndex * 65f;

            GameObject labelGo = new GameObject($"{label}Label");
            labelGo.transform.SetParent(parent, false);
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 1f);
            labelRect.sizeDelta = new Vector2(100f, 30f);
            labelRect.anchoredPosition = new Vector2(15f, rowY);
            Text labelText = labelGo.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.text = label;
            labelText.fontSize = 16;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;

            for (int i = 0; i < colors.Length; i++)
            {
                GameObject swatchGo = new GameObject($"{label}Swatch{i}");
                swatchGo.transform.SetParent(parent, false);
                RectTransform swatchRect = swatchGo.AddComponent<RectTransform>();
                swatchRect.anchorMin = new Vector2(0f, 1f);
                swatchRect.anchorMax = new Vector2(0f, 1f);
                swatchRect.pivot = new Vector2(0f, 1f);
                swatchRect.sizeDelta = new Vector2(40f, 40f);
                swatchRect.anchoredPosition = new Vector2(120f + i * 50f, rowY - 5f);

                Image swatchImage = swatchGo.AddComponent<Image>();
                swatchImage.color = colors[i];

                Button button = swatchGo.AddComponent<Button>();
                button.targetGraphic = swatchImage;

                ColorSwatchButton swatch = swatchGo.AddComponent<ColorSwatchButton>();
                SetPrivateField(swatch, "customization", customization);
                SetPrivateField(swatch, "target", target);
                SetPrivateField(swatch, "color", colors[i]);

                // AddPersistentListener, not button.onClick.AddListener --
                // see the identical note on the mobile corner buttons
                // (CreateCornerButton) for why: a plain runtime listener
                // added from this editor script doesn't survive being
                // saved into the scene.
                UnityEventTools.AddPersistentListener(button.onClick, swatch.Apply);
            }
        }

        private static GameObject CreateTreePrefab(Material trunkMaterial, Material leafMaterial)
        {
            GameObject root = new GameObject("Tree");

            // Two tapered segments instead of one uniform cylinder -- a trunk
            // that's the same width top to bottom is a big part of what made
            // this read as a toy rather than a tree.
            CreateTrunkSegment(root.transform, trunkMaterial, "TrunkLower", localY: 0.55f, height: 0.55f, diameter: 0.34f);
            CreateTrunkSegment(root.transform, trunkMaterial, "TrunkUpper", localY: 1.6f, height: 0.5f, diameter: 0.22f);

            // Solid spheres read as balloons no matter what texture sits on
            // them -- a photo silhouette needs to be cut out of a flat card
            // instead. Each cluster below is 3 alpha-cutout cards crossed
            // 60 degrees apart so it reads as foliage (not a flat cutout)
            // from any viewing angle -- the standard "billboard tree"
            // technique.
            CreateFoliageCluster(root.transform, leafMaterial, "FoliageCenter", new Vector3(0f, 2.7f, 0f), 1.7f);
            CreateFoliageCluster(root.transform, leafMaterial, "FoliageLeft", new Vector3(-0.55f, 2.3f, 0.3f), 1.2f);
            CreateFoliageCluster(root.transform, leafMaterial, "FoliageRight", new Vector3(0.5f, 2.35f, -0.4f), 1.15f);
            CreateFoliageCluster(root.transform, leafMaterial, "FoliageTop", new Vector3(0.1f, 3.1f, -0.1f), 0.95f);

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

        // Alpha-cutout quads crossed 60 degrees apart around Y, all showing
        // the same leaf-sprig cutout -- from any angle at least one card is
        // close to face-on, so it reads as a solid clump of foliage rather
        // than a flat cardboard cutout. No collider: walking through
        // foliage is expected, and a thin quad collider would be an odd
        // shape to snag on anyway.
        private static void CreateFoliageCluster(Transform parent, Material material, string name, Vector3 localPosition, float scale)
        {
            GameObject cluster = new GameObject(name);
            cluster.transform.SetParent(parent, false);
            cluster.transform.localPosition = localPosition;

            // 6, not 3: Standard's shader hardcodes backface culling with no
            // material property to override it, so each of the 3 planes
            // needs a second copy rotated 180 degrees to actually have a
            // front face pointing the other way -- geometry, not a shader
            // flag, is what makes these visible from behind.
            for (int i = 0; i < 6; i++)
            {
                GameObject card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = $"Card{i}";
                card.transform.SetParent(cluster.transform, false);
                card.transform.localRotation = Quaternion.Euler(0f, i * 60f, 0f);
                card.transform.localScale = new Vector3(scale, scale, scale);
                card.GetComponent<Renderer>().sharedMaterial = material;
                Object.DestroyImmediate(card.GetComponent<Collider>());
            }
        }

        private static void ScatterEnvironmentProps(Terrain terrain, GameObject treePrefab, GameObject rockPrefab)
        {
            const float worldSize = 120f;
            const float clearRadius = 20f; // keep the flat spawn/build area free of scenery

            GameObject environment = new GameObject("Environment");

            ScatterProps(environment.transform, treePrefab, 40, worldSize, clearRadius, terrain, 0.8f, 1.3f, tintFoliage: true);
            ScatterProps(environment.transform, rockPrefab, 50, worldSize, clearRadius, terrain, 0.5f, 1.2f);
        }

        private static void ScatterProps(Transform parent, GameObject prefab, int count, float worldSize, float clearRadius, Terrain terrain, float minScale, float maxScale, bool tintFoliage = false)
        {
            Vector2 lakeCenter = new Vector2(LakeCenterX, LakeCenterZ);
            float lakeExclusionRadius = LakeRadius + LakeShoreBlend + 2f; // keep props off the shore, not just out of the water
            Vector2 courseCenter = new Vector2(CourseStartX, CourseStartZ);
            Vector2 storyCenter = new Vector2(StoryPlaceX, StoryPlaceZ);
            Vector2 hillCenter = new Vector2(HillCenterX, HillCenterZ);
            Vector2 elderTentCenter = new Vector2(ElderTentX, ElderTentZ);

            for (int i = 0; i < count; i++)
            {
                float x, z;
                do
                {
                    x = UnityEngine.Random.Range(-worldSize / 2f, worldSize / 2f);
                    z = UnityEngine.Random.Range(-worldSize / 2f, worldSize / 2f);
                } while (new Vector2(x, z).magnitude < clearRadius
                    || Vector2.Distance(new Vector2(x, z), lakeCenter) < lakeExclusionRadius
                    || Vector2.Distance(new Vector2(x, z), courseCenter) < CourseExclusionRadius
                    || Vector2.Distance(new Vector2(x, z), storyCenter) < StoryPlaceExclusionRadius
                    || Vector2.Distance(new Vector2(x, z), hillCenter) < HillPeakClearRadius
                    || Vector2.Distance(new Vector2(x, z), elderTentCenter) < ElderTentExclusionRadius);

                float y = terrain.SampleHeight(new Vector3(x, 0f, z));
                Quaternion rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
                GameObject instance = Object.Instantiate(prefab, new Vector3(x, y, z), rotation, parent);
                instance.transform.localScale *= UnityEngine.Random.Range(minScale, maxScale);

                if (tintFoliage)
                    TintFoliageChildren(instance.transform);
            }
        }

        // Subtle per-tree hue/brightness jitter on the canopy so a scattered
        // forest of identical prefab instances doesn't read as a single tree
        // stamped out forty times -- each renderer gets its own material
        // instance (via .material, not .sharedMaterial) so this only ever
        // affects this one tree.
        private static void TintFoliageChildren(Transform root)
        {
            float hueShift = UnityEngine.Random.Range(-0.04f, 0.04f);
            float brightness = UnityEngine.Random.Range(0.85f, 1.15f);

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith("Card"))
                    continue;

                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer == null)
                    continue;

                Color.RGBToHSV(renderer.material.color, out float h, out float s, out float v);
                h = Mathf.Repeat(h + hueShift, 1f);
                renderer.material.color = Color.HSVToRGB(h, s, v) * brightness;
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

            GameObject avatar = BuildAvatarVisual(player.transform, bodyMaterial, headMaterial, shirtMaterial);

            AvatarCustomization customization = player.AddComponent<AvatarCustomization>();
            SetPrivateField(customization, "shirtRenderers", new[]
            {
                avatar.transform.Find("Torso").GetComponent<Renderer>(),
            });
            SetPrivateField(customization, "skinRenderers", new[]
            {
                avatar.transform.Find("Head").GetComponent<Renderer>(),
                avatar.transform.Find("LeftArmPivot/LeftArm").GetComponent<Renderer>(),
                avatar.transform.Find("RightArmPivot/RightArm").GetComponent<Renderer>(),
            });
            SetPrivateField(customization, "legsRenderers", new[]
            {
                avatar.transform.Find("LeftLegPivot/LeftLeg").GetComponent<Renderer>(),
                avatar.transform.Find("RightLegPivot/RightLeg").GetComponent<Renderer>(),
            });

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
            // ~18% bigger than the previous size (still not precise enough
            // per real testing -- paired with a wider VirtualJoystick
            // deadzone this time instead of relying on size alone).
            VirtualJoystick moveJoystick = CreateJoystick(canvasGo.transform, "MoveJoystick", new Vector2(0f, 0f), new Vector2(260f, 260f), ringSprite, knobSprite);
            Vector2 lookJoystickPos = new Vector2(-260f, 260f);
            VirtualJoystick lookJoystick = CreateJoystick(canvasGo.transform, "LookJoystick", new Vector2(1f, 0f), lookJoystickPos, ringSprite, knobSprite);

            SetPrivateField(playerController, "moveJoystick", moveJoystick);
            SetPrivateField(cameraController, "lookJoystick", lookJoystick);

            // Action buttons as 4 rounded-triangle corner pieces that
            // together with the joystick's own circle read as one enclosing
            // rounded square -- each piece is a square quadrant with a
            // concave bite taken out following the circle, and a rounded
            // (not sharp) outer tip. One sprite, mirrored per corner via
            // scale flips instead of 4 separate textures.
            const float cornerExtent = 209f; // reaches well past the joystick's 130 radius
            const float cornerJoystickRadius = 130f; // matches the joystick ring exactly, so the cutout lines up
            const float cornerRound = 41f;
            const float cornerGap = 11f; // small gap from the joystick ring and between adjacent corner pieces
            Sprite cornerSprite = CreateCornerWedgeSprite("ActionCorner", new Color(1f, 1f, 1f, 0.4f), cornerJoystickRadius, cornerRound, cornerGap);

            // Small icons instead of text -- at this button size text was
            // unreadable. Same procedural-generation approach as every other
            // texture in this project, no external art.
            // Place has no button of its own -- tapping directly on the
            // world places a block there (see BuildPlacer.OnPlace/
            // GetPointerScreenPosition), same as a mouse click does on
            // desktop. Rotate takes the freed-up slot since it previously
            // had no touch equivalent at all (keyboard R only).
            Sprite iconJump = CreateIconSprite("IconJump", Color.white, IconShape.ArrowUp);
            Sprite iconRotate = CreateIconSprite("IconRotate", Color.white, IconShape.Rotate);
            Sprite iconUndo = CreateIconSprite("IconUndo", Color.white, IconShape.ArrowLeft);
            Sprite iconRemove = CreateIconSprite("IconRemove", Color.white, IconShape.Cross);

            CreateCornerButton(canvasGo.transform, "JumpButton", iconJump, lookJoystickPos, flipX: false, flipY: false, cornerExtent, cornerSprite, playerController.TriggerJump);
            CreateCornerButton(canvasGo.transform, "RotateButton", iconRotate, lookJoystickPos, flipX: true, flipY: false, cornerExtent, cornerSprite, placer.PerformRotate);
            CreateCornerButton(canvasGo.transform, "UndoButton", iconUndo, lookJoystickPos, flipX: false, flipY: true, cornerExtent, cornerSprite, placer.PerformUndo);
            CreateCornerButton(canvasGo.transform, "RemoveButton", iconRemove, lookJoystickPos, flipX: true, flipY: true, cornerExtent, cornerSprite, placer.PerformRemove);

            // Only touch devices need any of this -- MobileControlsVisibility
            // disables the whole canvas at runtime for mouse/keyboard players.
            // (Must stay active here at edit time: a GameObject that starts
            // disabled never runs Awake, so a component depending on Awake to
            // decide whether to show itself would never get the chance to.)
            canvasGo.AddComponent<MobileControlsVisibility>();
        }

        private static VirtualJoystick CreateJoystick(Transform parent, string name, Vector2 cornerAnchor, Vector2 anchoredPosition, Sprite ringSprite, Sprite knobSprite)
        {
            const float backgroundSize = 260f;
            const float knobSize = 112f;

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
            iconRect.sizeDelta = new Vector2(64f, 64f);
            // A smaller inset pulls back less from the outer corner, i.e.
            // sits the icon further from the joystick/cutout edge -- 54 put
            // icons too close to the concave inner border in real testing.
            // Scaled up along with cornerExtent (177->209).
            const float inset = 53f;
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

        private enum IconShape { Plus, Cross, ArrowUp, ArrowLeft, Rotate }

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
                        case IconShape.Rotate:
                        {
                            float dx = px - center, dy = py - center;
                            float r = Mathf.Sqrt(dx * dx + dy * dy);
                            const float ringMid = 19f;
                            const float ringHalf = 4.5f;
                            float ringAlpha = SoftEdge(Mathf.Abs(r - ringMid), ringHalf);

                            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                            if (angle < 0f) angle += 360f;
                            // Arc sweeps most of the circle, leaving a gap
                            // near the top for the arrowhead to sit in.
                            float arcAlpha = (angle >= 15f && angle <= 320f) ? 1f : 0f;

                            // Small triangular arrowhead at the arc's
                            // leading (320deg) end, tangent to the ring.
                            float headAlpha = 0f;
                            float angleDiff = Mathf.DeltaAngle(320f, angle);
                            if (angleDiff > -22f && angleDiff < 12f)
                            {
                                float t = Mathf.InverseLerp(-22f, 12f, angleDiff);
                                float allowedRadial = Mathf.Lerp(ringHalf * 2.4f, 0f, t);
                                headAlpha = SoftEdge(Mathf.Abs(r - ringMid), allowedRadial);
                            }

                            alpha = Mathf.Clamp01(ringAlpha * arcAlpha + headAlpha);
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
