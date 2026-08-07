using UnityEngine;
using UnityEngine.InputSystem;
using Sandbox.Save;

namespace Sandbox.Building
{
    public class BuildPlacer : MonoBehaviour
    {
        [SerializeField] private Camera placementCamera;
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private float maxPlaceDistance = 8f;
        [SerializeField] private LayerMask placementMask = ~0;
        [SerializeField] private Transform blockParent;

        private GameObject previewGhost;

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
        }

        private void Update()
        {
            UpdateGhost();
        }

        private void UpdateGhost()
        {
            if (previewGhost == null)
                return;

            if (TryGetPlacementPoint(out Vector3 point, out _))
            {
                previewGhost.SetActive(true);
                previewGhost.transform.position = SnapToGrid(point);
            }
            else
            {
                previewGhost.SetActive(false);
            }
        }

        public void OnPlace(InputAction.CallbackContext context)
        {
            if (!context.performed)
                return;

            if (!TryGetPlacementPoint(out Vector3 point, out _))
                return;

            Vector3 spawnPos = SnapToGrid(point);
            GameObject block = Instantiate(blockPrefab, spawnPos, Quaternion.identity, blockParent);
            block.AddComponent<PlacedBlock>();
        }

        public void OnRemove(InputAction.CallbackContext context)
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

        private bool TryGetPlacementPoint(out Vector3 point, out Vector3 normal)
        {
            point = Vector3.zero;
            normal = Vector3.up;

            if (placementCamera == null)
                return false;

            Ray ray = placementCamera.ScreenPointToRay(GetPointerScreenPosition());
            if (Physics.Raycast(ray, out RaycastHit hit, maxPlaceDistance, placementMask))
            {
                point = hit.point + hit.normal * 0.5f;
                normal = hit.normal;
                return true;
            }

            return false;
        }

        private static Vector3 SnapToGrid(Vector3 position, float gridSize = 1f)
        {
            return new Vector3(
                Mathf.Round(position.x / gridSize) * gridSize,
                Mathf.Round(position.y / gridSize) * gridSize,
                Mathf.Round(position.z / gridSize) * gridSize);
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
