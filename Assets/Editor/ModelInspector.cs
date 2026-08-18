using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sandbox.EditorTools
{
    // One-off diagnostic, run via -executeMethod, to see what's actually in
    // PersonAnimated.fbx (bounds, clips, humanoid validity) before writing
    // integration code around assumptions about its contents.
    public static class ModelInspector
    {
        private const string Path = "Assets/Models/Imported/PersonAnimated.fbx";

        [MenuItem("Sandbox/Debug/Inspect Person Model")]
        public static void Inspect()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            if (prefab == null)
            {
                Debug.LogError("ModelInspector: no root GameObject found");
                return;
            }

            GameObject instance = Object.Instantiate(prefab);

            Camera[] cameras = instance.GetComponentsInChildren<Camera>(true);
            Debug.Log($"ModelInspector: {cameras.Length} Camera component(s) in the model: {string.Join(", ", cameras.Select(c => c.name))}");

            Light[] lights = instance.GetComponentsInChildren<Light>(true);
            Debug.Log($"ModelInspector: {lights.Length} Light component(s) in the model: {string.Join(", ", lights.Select(l => l.name))}");

            AudioListener[] listeners = instance.GetComponentsInChildren<AudioListener>(true);
            Debug.Log($"ModelInspector: {listeners.Length} AudioListener component(s) in the model");

            Transform[] all = instance.GetComponentsInChildren<Transform>(true);
            Debug.Log($"ModelInspector: {all.Length} total transforms, names: {string.Join(", ", all.Select(t => t.name))}");

            Object.DestroyImmediate(instance);
        }
    }
}
