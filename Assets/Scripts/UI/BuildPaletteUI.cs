using UnityEngine;
using UnityEngine.UI;
using Sandbox.Building;

namespace Sandbox.UI
{
    public class BuildPaletteUI : MonoBehaviour
    {
        private static readonly Color SelectedColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        private static readonly Color UnselectedColor = new Color(0f, 0f, 0f, 0.5f);

        [SerializeField] private BuildPlacer buildPlacer;
        [SerializeField] private Image[] slotBackgrounds;

        private void OnEnable()
        {
            if (buildPlacer != null)
                buildPlacer.ShapeSelected += OnShapeSelected;
        }

        private void OnDisable()
        {
            if (buildPlacer != null)
                buildPlacer.ShapeSelected -= OnShapeSelected;
        }

        private void Start()
        {
            if (buildPlacer != null)
                OnShapeSelected(buildPlacer.SelectedShapeIndex);
        }

        private void OnShapeSelected(int index)
        {
            for (int i = 0; i < slotBackgrounds.Length; i++)
            {
                if (slotBackgrounds[i] != null)
                    slotBackgrounds[i].color = i == index ? SelectedColor : UnselectedColor;
            }
        }
    }
}
