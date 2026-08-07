using UnityEngine;
using UnityEngine.InputSystem;
using Sandbox.Save;

namespace Sandbox.Building
{
    public class BuildPlacer : MonoBehaviour
    {
        [SerializeField] private Camera placementCamera;
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private float maxPlaceDistance = 25f;
        [SerializeField] private LayerMask placementMask = ~0;
        [SerializeField] private Transform blockParent;
        [SerializeField] private InputActionAsset actions;
        [SerializeField] private string actionMapName = "Player";

        private GameObject previewGhost;
        private InputAction placeAction;
        private InputAction removeAction;

        private void Awake()
        {
            if (placementCamera == null)
                placementCamera = Camera.main;

            if (blockPrefab != null)
            {
                previewGhost = Instantiate(blockPrefab);
                SetGhostAppearance(previewGhost);
                previewGhost.SetActive(false);
            }

            InputActionMap map = actions.FindActionMap(actionMapName, throwIfNotFound: true);
            placeAction = map.FindAction("Place", throwIfNotFound: true);
            removeAction = map.FindAction("Remove", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            placeAction.performed += OnPlace;
            removeAction.performed += OnRemove;
            placeAction.Enable();
            removeAction.Enable();
        }

        private void OnDisable()
        {
            placeAction.performed -= OnPlace;
            removeAction.performed -= OnRemove;
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
            }
            else
            {
                previewGhost.SetActive(false);
            }
        }

        private void OnPlace(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;

            if (!TryGetPlacementPoint(out Vector3 spawnPosition))
                return;

            GameObject block = Instantiate(blockPrefab, spawnPosition, Quaternion.identity, blockParent);
            block.AddComponent<PlacedBlock>();

            Renderer blockRenderer = block.GetComponent<Renderer>();
            if (blockRenderer != null)
                blockRenderer.material.color = Random.ColorHSV(0f, 1f, 0.55f, 0.85f, 0.75f, 1f);
        }

        private void OnRemove(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;

            if (Physics.Raycast(placementCamera.ScreenPointToRay(GetPointerScreenPosition()), out RaycastHit hit, maxPlaceDistance, placementMask))
            {
                PlacedBlock block = hit.collider.GetComponent<PlacedBlock>();
                if (block != null)
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
            else
            {
                // Ground (or any other surface): snap to the 1x1 cell under the
                // hit point and rest the block on top of it.
                spawnPosition = new Vector3(
                    Mathf.Floor(hit.point.x) + 0.5f,
                    0.5f,
                    Mathf.Floor(hit.point.z) + 0.5f);
            }

            return true;
        }

        private static Vector2 GetPointerScreenPosition()
        {
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
