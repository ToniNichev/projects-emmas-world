using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using Sandbox.Building;

namespace Sandbox.Save
{
    public class WorldSaveSystem : MonoBehaviour
    {
        [SerializeField] private Transform blockParent;
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private string saveFileName = "world.json";
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string actionMapName = "Player";

        private InputAction saveAction;
        private InputAction loadAction;

        private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

        private void Awake()
        {
            InputActionMap map = actions.FindActionMap(actionMapName, throwIfNotFound: true);
            saveAction = map.FindAction("Save", throwIfNotFound: true);
            loadAction = map.FindAction("Load", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            saveAction.performed += OnSave;
            loadAction.performed += OnLoad;
            saveAction.Enable();
            loadAction.Enable();
        }

        private void OnDisable()
        {
            saveAction.performed -= OnSave;
            loadAction.performed -= OnLoad;
        }

        private void OnSave(InputAction.CallbackContext context)
        {
            SaveWorld();
        }

        private void OnLoad(InputAction.CallbackContext context)
        {
            LoadWorld();
        }

        public void SaveWorld()
        {
            var data = new WorldData();
            foreach (PlacedBlock block in blockParent.GetComponentsInChildren<PlacedBlock>())
            {
                data.blocks.Add(new BlockData
                {
                    position = block.transform.position,
                    rotation = block.transform.rotation.eulerAngles
                });
            }

            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            Debug.Log($"Saved {data.blocks.Count} blocks to {SavePath}");
        }

        public void LoadWorld()
        {
            if (!File.Exists(SavePath))
            {
                Debug.LogWarning($"No save file found at {SavePath}");
                return;
            }

            foreach (PlacedBlock existing in blockParent.GetComponentsInChildren<PlacedBlock>())
                Destroy(existing.gameObject);

            WorldData data = JsonUtility.FromJson<WorldData>(File.ReadAllText(SavePath));
            foreach (BlockData block in data.blocks)
            {
                GameObject instance = Instantiate(blockPrefab, block.position, Quaternion.Euler(block.rotation), blockParent);
                instance.AddComponent<PlacedBlock>();
            }

            Debug.Log($"Loaded {data.blocks.Count} blocks from {SavePath}");
        }

        [Serializable]
        private class WorldData
        {
            public List<BlockData> blocks = new List<BlockData>();
        }

        [Serializable]
        private class BlockData
        {
            public Vector3 position;
            public Vector3 rotation;
        }
    }
}
