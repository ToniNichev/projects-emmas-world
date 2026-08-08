using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Sandbox.Audio;
using Sandbox.Multiplayer;
using Sandbox.Save;

namespace Sandbox.Building
{
    public class BuildPlacer : MonoBehaviour
    {
        [SerializeField] private Camera placementCamera;
        [SerializeField] private GameObject[] blockPrefabs;
        [SerializeField] private float maxPlaceDistance = 25f;
        [SerializeField] private LayerMask placementMask = ~0;
        [SerializeField] private Transform blockParent;
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string actionMapName = "Player";

        public event Action<int> ShapeSelected;

        private GameObject previewGhost;
        private int selectedShapeIndex;
        private int rotationSteps;
        private InputAction placeAction;
        private InputAction removeAction;
        private InputAction selectShapeAction;
        private InputAction rotateAction;
        private InputAction undoAction;
        private SoundEffects soundEffects;
        private MultiplayerManager multiplayerManager;

        // Local (non-multiplayer) undo target. In multiplayer mode, undo is
        // tracked server-side-confirmed instead (see MultiplayerManager),
        // since a block isn't "real" here until it round-trips.
        private GameObject lastLocallyPlacedBlock;

        public int SelectedShapeIndex => selectedShapeIndex;

        private GameObject SelectedPrefab => blockPrefabs[selectedShapeIndex];
        private Quaternion CurrentRotation => Quaternion.Euler(0f, rotationSteps * 90f, 0f);

        private void Awake()
        {
            if (placementCamera == null)
                placementCamera = Camera.main;

            soundEffects = GetComponent<SoundEffects>();
            // MultiplayerManager lives on its own child GameObject (named to
            // match what the WebGL bridge's SendMessage calls expect), not on
            // this same GameObject -- GetComponentInChildren still finds it.
            multiplayerManager = GetComponentInChildren<MultiplayerManager>();

            RebuildGhost();

            InputActionMap map = actions.FindActionMap(actionMapName, throwIfNotFound: true);
            placeAction = map.FindAction("Place", throwIfNotFound: true);
            removeAction = map.FindAction("Remove", throwIfNotFound: true);
            selectShapeAction = map.FindAction("SelectShape", throwIfNotFound: true);
            rotateAction = map.FindAction("Rotate", throwIfNotFound: true);
            undoAction = map.FindAction("Undo", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            placeAction.performed += OnPlace;
            removeAction.performed += OnRemove;
            selectShapeAction.performed += OnSelectShape;
            rotateAction.performed += OnRotate;
            undoAction.performed += OnUndo;
            placeAction.Enable();
            removeAction.Enable();
            selectShapeAction.Enable();
            rotateAction.Enable();
            undoAction.Enable();
        }

        private void OnDisable()
        {
            placeAction.performed -= OnPlace;
            removeAction.performed -= OnRemove;
            selectShapeAction.performed -= OnSelectShape;
            rotateAction.performed -= OnRotate;
            undoAction.performed -= OnUndo;
        }

        private void Update()
        {
            UpdateGhost();
        }

        private void UpdateGhost()
        {
            if (previewGhost == null)
                return;

            if (TryGetPlacementPoint(out Vector3 spawnPosition))
            {
                previewGhost.SetActive(true);
                previewGhost.transform.position = spawnPosition;
                previewGhost.transform.rotation = CurrentRotation;
            }
            else
            {
                previewGhost.SetActive(false);
            }
        }

        private void OnPlace(InputAction.CallbackContext context)
        {
            if (context.performed)
                PerformPlace();
        }

        // Public so the on-screen mobile Place button can call it directly
        // without needing to fake an InputAction.CallbackContext.
        public void PerformPlace()
        {
            if (!TryGetPlacementPoint(out Vector3 spawnPosition))
                return;

            Color color = UnityEngine.Random.ColorHSV(0f, 1f, 0.55f, 0.85f, 0.75f, 1f);
            soundEffects?.PlayPlace();

            if (multiplayerManager != null && multiplayerManager.IsConnected)
            {
                // Networked: don't instantiate locally. The block appears once
                // the emmasworld:block_placed event round-trips back (via
                // MultiplayerManager), the same way it does for every other
                // connected client -- avoids needing to reconcile an optimistic
                // local placement against the server-assigned id.
                multiplayerManager.RequestPlaceBlock(selectedShapeIndex, spawnPosition, rotationSteps * 90, color);
                return;
            }

            GameObject block = Instantiate(SelectedPrefab, spawnPosition, CurrentRotation, blockParent);
            block.AddComponent<PlacedBlock>().ShapeIndex = selectedShapeIndex;

            Renderer blockRenderer = block.GetComponent<Renderer>();
            if (blockRenderer != null)
                blockRenderer.material.color = color;

            lastLocallyPlacedBlock = block;
        }

        private void OnUndo(InputAction.CallbackContext context)
        {
            if (context.performed)
                PerformUndo();
        }

        // Public so the on-screen mobile Undo button can call it directly.
        public void PerformUndo()
        {
            if (multiplayerManager != null && multiplayerManager.IsConnected)
            {
                multiplayerManager.UndoLastPlacement();
                soundEffects?.PlayRemove();
                return;
            }

            if (lastLocallyPlacedBlock == null)
                return;

            Destroy(lastLocallyPlacedBlock);
            lastLocallyPlacedBlock = null;
            soundEffects?.PlayRemove();
        }

        private void OnSelectShape(InputAction.CallbackContext context)
        {
            if (!int.TryParse(context.control.name, out int number))
                return;

            int index = number - 1;
            if (index < 0 || index >= blockPrefabs.Length || blockPrefabs[index] == null)
                return;

            selectedShapeIndex = index;
            RebuildGhost();
            ShapeSelected?.Invoke(selectedShapeIndex);
        }

        private void OnRotate(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;

            rotationSteps = (rotationSteps + 1) % 4;
        }

        private void RebuildGhost()
        {
            if (previewGhost != null)
                Destroy(previewGhost);

            if (blockPrefabs == null || blockPrefabs.Length == 0 || SelectedPrefab == null)
                return;

            previewGhost = Instantiate(SelectedPrefab);
            SetGhostAppearance(previewGhost);
            previewGhost.SetActive(false);
        }

        private void OnRemove(InputAction.CallbackContext context)
        {
            if (context.performed)
                PerformRemove();
        }

        // Public so the on-screen mobile Remove button can call it directly.
        public void PerformRemove()
        {
            if (Physics.Raycast(placementCamera.ScreenPointToRay(GetPointerScreenPosition()), out RaycastHit hit, maxPlaceDistance, placementMask))
            {
                PlacedBlock block = hit.collider.GetComponent<PlacedBlock>();
                if (block == null)
                    return;

                soundEffects?.PlayRemove();

                if (multiplayerManager != null && multiplayerManager.IsConnected)
                {
                    // Networked: wait for the emmasworld:block_removed echo
                    // rather than destroying immediately, same reasoning as
                    // placement above. Every block visible while connected came
                    // from a network event, so NetworkId is always valid here.
                    multiplayerManager.RequestRemoveBlock(block.NetworkId);
                    return;
                }

                Destroy(block.gameObject);
            }
        }

        private bool TryGetPlacementPoint(out Vector3 spawnPosition)
        {
            spawnPosition = Vector3.zero;

            if (placementCamera == null)
                return false;

            Ray ray = placementCamera.ScreenPointToRay(GetPointerScreenPosition());
            if (!Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance, placementMask))
                return false;

            PlacedBlock hitBlock = hit.collider.GetComponent<PlacedBlock>();
            if (hitBlock != null)
            {
                // Existing blocks are always exactly grid-aligned, so a one-unit
                // offset along the hit face's normal lands exactly on the next cell.
                spawnPosition = hitBlock.transform.position + hit.normal;
            }
            else if (hit.collider.GetComponent<Terrain>() != null)
            {
                // Terrain: snap X/Z to the 1x1 cell under the hit point, but follow
                // the surface's actual height for Y rather than assuming flat
                // ground at y=0 -- terrain isn't voxel-grid-aligned.
                spawnPosition = new Vector3(
                    Mathf.Floor(hit.point.x) + 0.5f,
                    hit.point.y + 0.5f,
                    Mathf.Floor(hit.point.z) + 0.5f);
            }
            else
            {
                // Any other solid (rock, tree, etc.): not grid-aligned at all, so
                // just rest the block flush against the hit surface rather than
                // grid-snapping, which could otherwise leave it floating or sunk.
                spawnPosition = hit.point + hit.normal * 0.5f;
            }

            return true;
        }

        private static Vector2 GetPointerScreenPosition()
        {
            // On touch devices there's no meaningful cursor position to aim
            // from -- place/remove aim from a fixed screen-center crosshair
            // instead, driven by the look joystick rotating the camera.
            if (Touchscreen.current != null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            return Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private static void SetGhostAppearance(GameObject ghost)
        {
            foreach (Collider col in ghost.GetComponentsInChildren<Collider>())
                col.enabled = false;

            foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>())
            {
                Color c = renderer.material.color;
                c.a = 0.4f;
                renderer.material.color = c;
            }
        }
    }
}
