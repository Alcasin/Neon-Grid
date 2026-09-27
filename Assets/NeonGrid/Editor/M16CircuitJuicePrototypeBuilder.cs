using System.IO;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class M16CircuitJuicePrototypeBuilder
    {
        public const string JuicePath =
            "Assets/NeonGrid/Resources/VisualPrototypes/M16_CircuitJuicePrototype.asset";
        public const string PrototypePath =
            "Assets/NeonGrid/Resources/VisualPrototypes/M15_GameplayVisualPrototype.asset";

        [MenuItem("Neon Grid/M16/Prepare Interaction Juice Prototype")]
        public static void Build()
        {
            EnsureDirectory(Path.GetDirectoryName(JuicePath)?.Replace('\\', '/'));
            CircuitJuiceDefinition juice =
                AssetDatabase.LoadAssetAtPath<CircuitJuiceDefinition>(JuicePath);
            if (juice == null)
            {
                juice = ScriptableObject.CreateInstance<CircuitJuiceDefinition>();
                AssetDatabase.CreateAsset(juice, JuicePath);
            }
            if (!juice.IsConfigured)
                throw new InvalidDataException("M16 circuit juice definition is invalid.");

            GameplayVisualPrototypeDefinition prototype =
                AssetDatabase.LoadAssetAtPath<GameplayVisualPrototypeDefinition>(PrototypePath);
            if (prototype == null || !prototype.IsConfigured)
                throw new FileNotFoundException("Accepted M15 gameplay visual prototype is missing.");
            prototype.SetCircuitJuice(juice);
            EditorUtility.SetDirty(prototype);
            EditorUtility.SetDirty(juice);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Prepared isolated M16 circuit juice prototype: {JuicePath}");
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
