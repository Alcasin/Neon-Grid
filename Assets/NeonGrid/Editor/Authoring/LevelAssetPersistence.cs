using System;
using System.Collections.Generic;
using NeonGrid.Data;
using NeonGrid.Simulation;
using NeonGrid.Validation;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor.Authoring
{
    public static class LevelAssetPersistence
    {
        public static LevelDefinition CreateAsset(string assetPath, int width, int height)
        {
            if (string.IsNullOrWhiteSpace(assetPath)) throw new ArgumentException("An asset path is required.", nameof(assetPath));

            var model = new LevelAuthoringModel(width, height);
            var asset = ScriptableObject.CreateInstance<LevelDefinition>();
            asset.SetData(width, height, model.CreateSnapshot());
            AssetDatabase.CreateAsset(asset, assetPath);
            AssetDatabase.SaveAssets();
            model.DiscardDirtyFlag();
            return asset;
        }

        public static LevelValidationResult Save(LevelAuthoringModel model, LevelDefinition asset)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (asset == null) throw new ArgumentNullException(nameof(asset));

            LevelValidationResult validation = model.Validate();
            if (!validation.IsValid) return validation;

            IReadOnlyList<TileDefinition> snapshot = model.CreateSnapshot();
            Undo.RecordObject(asset, "Save Neon Grid Level");
            asset.SetData(model.Width, model.Height, snapshot);
            EditorUtility.SetDirty(asset);
            if (EditorUtility.IsPersistent(asset)) AssetDatabase.SaveAssetIfDirty(asset);
            model.MarkSaved(asset);
            return validation;
        }
    }
}
