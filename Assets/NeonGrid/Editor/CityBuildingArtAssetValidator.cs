using System.Collections.Generic;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class CityBuildingArtAssetValidator
    {
        public static IReadOnlyList<string> Validate(CityBuildingArtDefinition definition,
            string expectedChapterId)
        {
            var issues = new List<string>();
            if (definition == null)
            {
                issues.Add("Definition is missing.");
                return issues;
            }
            if (definition.ChapterId != expectedChapterId)
                issues.Add($"ChapterId must be '{expectedChapterId}', not '{definition.ChapterId}'.");
            if (!definition.HasBaseLayer)
                issues.Add("Base Architecture sprite is required.");

            Vector2Int? sourceSize = null;
            Vector2? pivot = null;
            for (int layer = 0; layer < 4; layer++)
            {
                Sprite sprite = definition.GetSprite(layer);
                if (sprite == null) continue;
                string path = AssetDatabase.GetAssetPath(sprite);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    issues.Add($"Layer {layer} is not backed by a texture importer.");
                    continue;
                }
                if (importer.textureType != TextureImporterType.Sprite)
                    issues.Add($"{path}: Texture Type must be Sprite (2D and UI).");
                if (importer.spriteImportMode != SpriteImportMode.Single)
                    issues.Add($"{path}: Sprite Mode must be Single.");
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType != SpriteMeshType.FullRect)
                    issues.Add($"{path}: Sprite Mesh Type must be Full Rect.");
                if (settings.spriteGenerateFallbackPhysicsShape)
                    issues.Add($"{path}: fallback physics shape generation must be disabled.");
                if (!importer.alphaIsTransparency)
                    issues.Add($"{path}: alpha transparency must be enabled.");
                if (importer.alphaSource != TextureImporterAlphaSource.FromInput)
                    issues.Add($"{path}: alpha source must be Input Texture Alpha.");
                if (!importer.sRGBTexture)
                    issues.Add($"{path}: sRGB must be enabled.");
                if (importer.mipmapEnabled)
                    issues.Add($"{path}: mipmaps must be disabled.");
                if (importer.filterMode != FilterMode.Bilinear)
                    issues.Add($"{path}: filter mode must be Bilinear.");
                if (importer.wrapMode != TextureWrapMode.Clamp)
                    issues.Add($"{path}: wrap mode must be Clamp.");
                if (importer.isReadable)
                    issues.Add($"{path}: Read/Write must be disabled.");
                if (importer.maxTextureSize != 2048)
                    issues.Add($"{path}: max size must be 2048.");
                if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    issues.Add($"{path}: compression must be None.");

                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                var currentSize = new Vector2Int(width, height);
                Vector2 currentPivot = sprite.pivot;
                if (sourceSize == null)
                {
                    sourceSize = currentSize;
                    pivot = currentPivot;
                }
                else
                {
                    if (sourceSize.Value != currentSize)
                        issues.Add($"{path}: source canvas {width}x{height} does not match " +
                                   $"{sourceSize.Value.x}x{sourceSize.Value.y}.");
                    if (Vector2.Distance(pivot.Value, currentPivot) > 0.01f)
                        issues.Add($"{path}: pivot does not match the registered base layer.");
                }
                Vector2 centered = sprite.rect.size * 0.5f;
                if (Vector2.Distance(currentPivot, centered) > 0.01f)
                    issues.Add($"{path}: pivot must be centered for the E1B contract.");
            }
            if (definition.HasBaseLayer && !definition.IsConfigured)
                issues.Add("Definition data is invalid (state profile, tint, footprint or scale).");
            return issues;
        }
    }
}
