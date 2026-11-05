using UnityEditor;

namespace Sandbox.EditorTools
{
    // Configures the imported humanoid FBX deterministically on import,
    // since there's no interactive Editor GUI available in this workflow
    // to click through "Rig > Humanoid > Configure..." by hand.
    public class ChibiGirlImporter : AssetPostprocessor
    {
        private void OnPreprocessModel()
        {
            if (!assetPath.EndsWith("ChibiGirl.fbx"))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
        }
    }
}
