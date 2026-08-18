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
            ModelImporter importer = AssetImporter.GetAtPath(Path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"ModelInspector: no ModelImporter at {Path}");
                return;
            }

            Debug.Log($"ModelInspector: animationType={importer.animationType}");

            Avatar avatar = importer.sourceAvatar;
            if (avatar == null)
            {
                // Humanoid avatars built by the importer live as a sub-asset,
                // not directly on sourceAvatar in every Unity version -- fall
                // back to scanning sub-assets for it.
                avatar = AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Avatar>().FirstOrDefault();
            }
            Debug.Log(avatar != null
                ? $"ModelInspector: avatar found, isValid={avatar.isValid}, isHuman={avatar.isHuman}"
                : "ModelInspector: no Avatar sub-asset found");

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(Path);
            var clips = allAssets.OfType<AnimationClip>().ToArray();
            Debug.Log($"ModelInspector: {clips.Length} AnimationClip(s): {string.Join(", ", clips.Select(c => c.name))}");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            if (prefab == null)
            {
                Debug.LogError("ModelInspector: no root GameObject found");
                return;
            }

            GameObject instance = Object.Instantiate(prefab);
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds();
            foreach (Renderer r in renderers)
                bounds.Encapsulate(r.bounds);
            Debug.Log($"ModelInspector: {renderers.Length} renderer(s), combined bounds center={bounds.center}, size={bounds.size}");

            foreach (Renderer r in renderers)
                Debug.Log($"ModelInspector: renderer '{r.name}' type={r.GetType().Name} materials=[{string.Join(", ", r.sharedMaterials.Select(m => m == null ? "null" : m.name))}]");

            SkinnedMeshRenderer smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null)
                Debug.Log($"ModelInspector: SkinnedMeshRenderer bone count={smr.bones.Length}, rootBone={(smr.rootBone != null ? smr.rootBone.name : "null")}");

            Animator animator = instance.GetComponentInChildren<Animator>();
            Debug.Log(animator != null ? $"ModelInspector: has Animator, isHuman={animator.isHuman}" : "ModelInspector: no Animator component on prefab");

            Object.DestroyImmediate(instance);
        }
    }
}
