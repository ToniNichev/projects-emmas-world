using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Sandbox.Building;

namespace Sandbox.Multiplayer
{
    // Bridges to Assets/Plugins/WebGL/EmmasWorldBridge.jslib, which talks to
    // the hosting page's socket.io-client instance. Only active in actual
    // WebGL builds -- in the Editor/standalone this component simply never
    // connects, and BuildPlacer falls back to its existing local-only
    // behavior. Block placement/removal go over plain UnityWebRequest calls
    // to the REST API (same-origin, so the auth cookie is sent automatically)
    // rather than through the bridge; only the live Socket.io connection
    // (position sync, and receiving the *result* of placements/removals)
    // needs the JS bridge.
    public class MultiplayerManager : MonoBehaviour
    {
        [SerializeField] private GameObject[] blockPrefabs;
        [SerializeField] private Transform blockParent;
        [SerializeField] private GameObject remoteAvatarPrefab;
        [SerializeField] private Transform localPlayerTransform;
        [SerializeField] private float moveSendInterval = 0.1f;
        [SerializeField] private float remoteSmoothSpeed = 10f;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern int EmmasWorld_IsReady();
        [DllImport("__Internal")] private static extern void EmmasWorld_RegisterListeners();
        [DllImport("__Internal")] private static extern void EmmasWorld_JoinWorld();
        [DllImport("__Internal")] private static extern void EmmasWorld_LeaveWorld();
        [DllImport("__Internal")] private static extern void EmmasWorld_SendMove(float x, float y, float z, float rotationY);
#endif

        public bool IsConnected { get; private set; }

        private float moveSendTimer;
        private readonly Dictionary<int, GameObject> remoteAvatars = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Vector3> remoteTargetPositions = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, float> remoteTargetRotations = new Dictionary<int, float>();
        private readonly Dictionary<int, GameObject> networkedBlocks = new Dictionary<int, GameObject>();

        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            InvokeRepeating(nameof(TryConnect), 0.5f, 0.5f);
#endif
        }

        private void Update()
        {
            if (!IsConnected)
                return;

            SendLocalPositionPeriodically();
            SmoothRemoteAvatars();
        }

        private void OnDestroy()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (IsConnected)
                EmmasWorld_LeaveWorld();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private void TryConnect()
        {
            if (IsConnected)
            {
                CancelInvoke(nameof(TryConnect));
                return;
            }
            if (EmmasWorld_IsReady() == 0)
                return;

            EmmasWorld_RegisterListeners();
            EmmasWorld_JoinWorld();
            IsConnected = true;
            CancelInvoke(nameof(TryConnect));

            StartCoroutine(LoadExistingBlocks());
        }
#endif

        private void SendLocalPositionPeriodically()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (localPlayerTransform == null)
                return;

            moveSendTimer -= Time.deltaTime;
            if (moveSendTimer > 0f)
                return;
            moveSendTimer = moveSendInterval;

            Vector3 pos = localPlayerTransform.position;
            float rotationY = localPlayerTransform.eulerAngles.y;
            EmmasWorld_SendMove(pos.x, pos.y, pos.z, rotationY);
#endif
        }

        private void SmoothRemoteAvatars()
        {
            foreach (KeyValuePair<int, GameObject> kvp in remoteAvatars)
            {
                int userId = kvp.Key;
                GameObject avatar = kvp.Value;
                if (avatar == null)
                    continue;

                if (remoteTargetPositions.TryGetValue(userId, out Vector3 targetPos))
                    avatar.transform.position = Vector3.Lerp(avatar.transform.position, targetPos, remoteSmoothSpeed * Time.deltaTime);

                if (remoteTargetRotations.TryGetValue(userId, out float targetRot))
                {
                    Quaternion targetQuat = Quaternion.Euler(0f, targetRot, 0f);
                    avatar.transform.rotation = Quaternion.Slerp(avatar.transform.rotation, targetQuat, remoteSmoothSpeed * Time.deltaTime);
                }
            }
        }

        // ----- Outgoing block requests (called by BuildPlacer) -----

        // Single-level undo of this client's own most recent placement --
        // not a global undo, since removing someone else's block via your
        // own undo key would be a bad surprise in a shared world.
        private int lastPlacedNetworkId = -1;

        public void RequestPlaceBlock(int shapeIndex, Vector3 position, int rotationY, Color color)
        {
            StartCoroutine(PostPlaceBlock(shapeIndex, position, rotationY, color));
        }

        public void RequestRemoveBlock(int networkId)
        {
            if (networkId < 0)
                return;
            StartCoroutine(DeleteBlock(networkId));
        }

        public void UndoLastPlacement()
        {
            if (lastPlacedNetworkId < 0)
                return;

            RequestRemoveBlock(lastPlacedNetworkId);
            lastPlacedNetworkId = -1;
        }

        private IEnumerator PostPlaceBlock(int shapeIndex, Vector3 position, int rotationY, Color color)
        {
            string x = position.x.ToString(CultureInfo.InvariantCulture);
            string y = position.y.ToString(CultureInfo.InvariantCulture);
            string z = position.z.ToString(CultureInfo.InvariantCulture);
            string json = "{\"shape_index\":" + shapeIndex
                + ",\"pos_x\":" + x + ",\"pos_y\":" + y + ",\"pos_z\":" + z
                + ",\"rotation_y\":" + rotationY
                + ",\"color_r\":" + Mathf.RoundToInt(color.r * 255f)
                + ",\"color_g\":" + Mathf.RoundToInt(color.g * 255f)
                + ",\"color_b\":" + Mathf.RoundToInt(color.b * 255f) + "}";

            using UnityWebRequest request = new UnityWebRequest("/api/emmas-world/blocks", "POST");
            byte[] body = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"MultiplayerManager: place block failed: {request.error}");
                yield break;
            }

            PlaceResponse response = JsonUtility.FromJson<PlaceResponse>(request.downloadHandler.text);
            if (response != null)
                lastPlacedNetworkId = response.id;
        }

        private IEnumerator DeleteBlock(int networkId)
        {
            using UnityWebRequest request = UnityWebRequest.Delete($"/api/emmas-world/blocks/{networkId}");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogError($"MultiplayerManager: remove block failed: {request.error}");
        }

        private IEnumerator LoadExistingBlocks()
        {
            using UnityWebRequest request = UnityWebRequest.Get("/api/emmas-world/blocks");
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"MultiplayerManager: load blocks failed: {request.error}");
                yield break;
            }

            // JsonUtility can't parse a bare JSON array, so wrap it the same
            // way the jslib bridge wraps the socket snapshot payload.
            string wrapped = "{\"items\":" + request.downloadHandler.text + "}";
            BlockListWrapper wrapper = JsonUtility.FromJson<BlockListWrapper>(wrapped);
            if (wrapper?.items == null)
                yield break;

            foreach (BlockEvent block in wrapper.items)
                SpawnNetworkedBlock(block);
        }

        // ----- Incoming events (called from JS via SendMessage) -----

        public void OnRemoteMove(string json)
        {
            MoveEvent data = JsonUtility.FromJson<MoveEvent>(json);
            if (data == null)
                return;

            Vector3 pos = new Vector3(data.x, data.y, data.z);
            EnsureRemoteAvatar(data.user_id, pos, data.rotation_y);
            remoteTargetPositions[data.user_id] = pos;
            remoteTargetRotations[data.user_id] = data.rotation_y;
        }

        public void OnSnapshot(string json)
        {
            SnapshotWrapper wrapper = JsonUtility.FromJson<SnapshotWrapper>(json);
            if (wrapper?.items == null)
                return;

            foreach (MoveEvent data in wrapper.items)
            {
                Vector3 pos = new Vector3(data.x, data.y, data.z);
                EnsureRemoteAvatar(data.user_id, pos, data.rotation_y);
                remoteTargetPositions[data.user_id] = pos;
                remoteTargetRotations[data.user_id] = data.rotation_y;
            }
        }

        public void OnUserLeft(string json)
        {
            UserLeftEvent data = JsonUtility.FromJson<UserLeftEvent>(json);
            if (data == null)
                return;

            if (remoteAvatars.TryGetValue(data.user_id, out GameObject avatar) && avatar != null)
                Destroy(avatar);
            remoteAvatars.Remove(data.user_id);
            remoteTargetPositions.Remove(data.user_id);
            remoteTargetRotations.Remove(data.user_id);
        }

        public void OnBlockPlaced(string json)
        {
            BlockEvent data = JsonUtility.FromJson<BlockEvent>(json);
            if (data == null || networkedBlocks.ContainsKey(data.id))
                return;

            SpawnNetworkedBlock(data);
        }

        public void OnBlockRemoved(string json)
        {
            BlockRemovedEvent data = JsonUtility.FromJson<BlockRemovedEvent>(json);
            if (data == null)
                return;

            if (networkedBlocks.TryGetValue(data.id, out GameObject block) && block != null)
                Destroy(block);
            networkedBlocks.Remove(data.id);
        }

        // ----- Helpers -----

        // Spawns at the correct spot immediately rather than at the default
        // (0,0,0) and drifting there via the per-frame Lerp in
        // SmoothRemoteAvatars -- otherwise every newly-seen remote player's
        // avatar visibly pops in at the world origin (likely underground,
        // given the terrain) before sliding to where they actually are.
        private GameObject EnsureRemoteAvatar(int userId, Vector3 position, float rotationY)
        {
            if (remoteAvatars.TryGetValue(userId, out GameObject existing) && existing != null)
                return existing;

            if (remoteAvatarPrefab == null)
                return null;

            GameObject avatar = Instantiate(remoteAvatarPrefab, position, Quaternion.Euler(0f, rotationY, 0f));
            avatar.name = $"RemotePlayer_{userId}";
            remoteAvatars[userId] = avatar;
            return avatar;
        }

        private void SpawnNetworkedBlock(BlockEvent data)
        {
            if (blockPrefabs == null || data.shape_index < 0 || data.shape_index >= blockPrefabs.Length)
                return;

            Vector3 position = new Vector3(data.pos_x, data.pos_y, data.pos_z);
            Quaternion rotation = Quaternion.Euler(0f, data.rotation_y, 0f);
            GameObject block = Instantiate(blockPrefabs[data.shape_index], position, rotation, blockParent);

            PlacedBlock placedBlock = block.AddComponent<PlacedBlock>();
            placedBlock.ShapeIndex = data.shape_index;
            placedBlock.NetworkId = data.id;

            Renderer blockRenderer = block.GetComponent<Renderer>();
            if (blockRenderer != null)
                blockRenderer.material.color = new Color(data.color_r / 255f, data.color_g / 255f, data.color_b / 255f);

            networkedBlocks[data.id] = block;
        }

        [Serializable]
        private class MoveEvent
        {
            public int user_id;
            public float x, y, z, rotation_y;
            public long t;
            public string first_name;
            public string profile_picture;
        }

        [Serializable] private class SnapshotWrapper { public MoveEvent[] items; }
        [Serializable] private class UserLeftEvent { public int user_id; }

        [Serializable]
        private class BlockEvent
        {
            public int id;
            public int shape_index;
            public float pos_x, pos_y, pos_z;
            public int rotation_y;
            public int color_r, color_g, color_b;
            public int placed_by;
        }

        [Serializable] private class BlockRemovedEvent { public int id; }
        [Serializable] private class BlockListWrapper { public BlockEvent[] items; }
        [Serializable] private class PlaceResponse { public int id; }
    }
}
