using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class M15CentralGridOverlayImportConfigurator
    {
        [MenuItem("Neon Grid/Configure M15 Central Grid Final Overlays")]
        public static void Configure()
        {
            string basePath = M15CityBuildingFinalArtPreparation.CentralLayerPaths[0];
            var baseImporter = AssetImporter.GetAtPath(basePath) as TextureImporter;
            if (baseImporter == null)
                throw new InvalidOperationException("The locked Central Grid Base importer is missing.");
            baseImporter.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != height)
                throw new InvalidOperationException($"Central Grid Base must be square, not {width}x{height}.");

            foreach (string path in M15CityBuildingFinalArtPreparation.CentralLayerPaths.Skip(1))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("Generated overlay importer is missing: " + path);
                importer.GetSourceTextureWidthAndHeight(out int currentWidth, out int currentHeight);
                if (currentWidth != width || currentHeight != height)
                    throw new InvalidOperationException($"Overlay canvas mismatch: {path} is " +
                        $"{currentWidth}x{currentHeight}, expected {width}x{height}.");
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePivot = new Vector2(0.5f, 0.5f);
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.isReadable = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteGenerateFallbackPhysicsShape = false;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Debug.Log("Configured registered Central Grid overlays; locked Base was not modified.");
        }
    }
}
