using UnityEditor;

namespace Sandbox.EditorTools
{
    // Configures the imported humanoid FBX deterministically on import,
    // since there's no interactive Editor GUI available in this workflow
    // to click through "Rig > Humanoid > Configure..." by hand.
    public class PersonAnimatedImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.EndsWith("PersonAnimated.fbx"))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
        }

        // Every clip in this pack imports with Loop Time off by default, so
        // Walk/Run played through once and then held on their last frame
        // while CharacterController kept translating the character -- a
        // couple steps of animation, then gliding with frozen legs. Every
        // clip here is locomotion/idle, so looping all of them is correct,
        // not just the ones the Animator Controller currently uses.
        private void OnPreprocessAnimation()
        {
            if (!assetPath.EndsWith("PersonAnimated.fbx"))
                return;

            var importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
                clips[i].loopTime = true;
            importer.clipAnimations = clips;
        }
    }
}
