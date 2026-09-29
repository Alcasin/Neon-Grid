using System.Collections.Generic;
using System.IO;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class M16HapticsPrototypeBuilder
    {
        public const string DefinitionPath =
            "Assets/NeonGrid/Resources/VisualPrototypes/M16_HapticsPrototype.asset";
        public const string PrototypePath =
            "Assets/NeonGrid/Resources/VisualPrototypes/M15_GameplayVisualPrototype.asset";

        [MenuItem("Neon Grid/M16/Prepare Haptics Prototype")]
        public static void Build()
        {
            EnsureDirectory(Path.GetDirectoryName(DefinitionPath)?.Replace('\\', '/'));
            NeonGridHapticsDefinition definition =
                AssetDatabase.LoadAssetAtPath<NeonGridHapticsDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<NeonGridHapticsDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            definition.SetData(CreateCues());
            if (!definition.IsConfigured)
                throw new InvalidDataException("M16 haptics prototype definition is invalid.");

            GameplayVisualPrototypeDefinition prototype =
                AssetDatabase.LoadAssetAtPath<GameplayVisualPrototypeDefinition>(PrototypePath);
            if (prototype == null || !prototype.IsConfigured)
                throw new FileNotFoundException(
                    "Accepted M15/M16 gameplay visual prototype is missing.");

            prototype.SetHapticsDefinition(definition);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(prototype);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Prepared isolated M16 haptics prototype: {DefinitionPath}");
        }

        private static IEnumerable<NeonGridHapticCue> CreateCues()
        {
            return new[]
            {
                new NeonGridHapticCue(NeonGridHapticEvent.TileRotate, 12, .18f, .045f),
                new NeonGridHapticCue(NeonGridHapticEvent.SwitchToggle, 18, .28f, .08f),
                new NeonGridHapticCue(NeonGridHapticEvent.ObjectiveActivate, 28, .42f, .15f),
                new NeonGridHapticCue(NeonGridHapticEvent.Hint, 32, .38f, .25f),
                new NeonGridHapticCue(NeonGridHapticEvent.LockedReject, 36, .62f, .12f),
                new NeonGridHapticCue(NeonGridHapticEvent.Completion, 55, .68f, 1f)
            };
        }

        private static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
