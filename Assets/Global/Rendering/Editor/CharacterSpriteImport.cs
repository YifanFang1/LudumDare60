using UnityEditor;
using UnityEngine;

// New images dropped anywhere under Assets/Characters are imported with Mesh Type: Full Rect
// instead of Unity's default, Tight. The outline (SpriteOutline) needs a rectangle around the art
// to draw on; a Tight mesh hugs the art, so the outline would be cut off.
//
// Only the FIRST import of a new image is changed. If an artist later picks Tight on purpose in
// the Inspector, that choice is kept.
class CharacterSpriteImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Characters/")) return;
        if (!assetImporter.importSettingsMissing) return;   // not new: respect its saved settings

        TextureImporter importer = (TextureImporter)assetImporter;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
    }
}
