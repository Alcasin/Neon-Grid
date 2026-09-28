using System.Collections.Generic;
using System.IO;
using NeonGrid.Data;
using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public static class M16AudioPrototypeBuilder
    {
        public const string DefinitionPath =
            "Assets/NeonGrid/Resources/AudioPrototypes/M16_AudioPrototype.asset";
        public const string PrototypePath =
            "Assets/NeonGrid/Resources/VisualPrototypes/M15_GameplayVisualPrototype.asset";

        [MenuItem("Neon Grid/M16/Prepare Audio Prototype")]
        public static void Build()
        {
            EnsureDirectory(Path.GetDirectoryName(DefinitionPath)?.Replace('\\', '/'));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            NeonGridAudioDefinition definition =
                AssetDatabase.LoadAssetAtPath<NeonGridAudioDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<NeonGridAudioDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            definition.SetData(CreateCues(), 6, 1f, 1f);
            if (!definition.IsConfigured)
                throw new InvalidDataException("M16 audio prototype definition is invalid.");

            GameplayVisualPrototypeDefinition prototype =
                AssetDatabase.LoadAssetAtPath<GameplayVisualPrototypeDefinition>(PrototypePath);
            if (prototype == null || !prototype.IsConfigured)
                throw new FileNotFoundException(
                    "Accepted M15/M16 gameplay visual prototype is missing.");

            prototype.SetAudioDefinition(definition);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(prototype);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Prepared isolated M16 audio prototype: {DefinitionPath}");
        }

        private static IEnumerable<NeonGridAudioCue> CreateCues()
        {
            AudioClip rotate = Load("Interaction/SFX_TileRotate_01.wav");
            AudioClip reject = Load("Interaction/SFX_LockedReject_01.wav");
            AudioClip powerOn = Load("Circuit/SFX_PowerActivate_01.wav");
            AudioClip powerOff = Load("Circuit/SFX_PowerDeactivate_01.wav");
            AudioClip source = Load("Circuit/SFX_SourcePulse_01.wav");
            AudioClip switchToggle = Load("Logic/SFX_SwitchToggle_01.wav");
            AudioClip gate = Load("Logic/SFX_GateActivate_01.wav");
            AudioClip objective = Load("Objective/SFX_ObjectiveActivate_01.wav");
            AudioClip hint = Load("UI/SFX_Hint_01.wav");
            AudioClip button = Load("UI/SFX_UIButton_01.wav");
            AudioClip completion = Load("Completion/SFX_Completion_01.wav");

            return new[]
            {
                Cue(NeonGridAudioEvent.TileRotateAccepted, rotate, .42f, .97f, 1.03f,
                    .035f, 2601),
                Cue(NeonGridAudioEvent.InteractionRejected, reject, .52f, .99f, 1.01f,
                    .08f, 2602),
                Cue(NeonGridAudioEvent.PowerActivated, powerOn, .55f, .99f, 1.01f,
                    .08f, 2603),
                Cue(NeonGridAudioEvent.PowerDeactivated, powerOff, .40f, .99f, 1.01f,
                    .08f, 2604),
                Cue(NeonGridAudioEvent.SourcePulse, source, .28f, .99f, 1.01f,
                    .12f, 2611),
                Cue(NeonGridAudioEvent.SwitchChanged, switchToggle, .52f, .98f, 1.02f,
                    .06f, 2605),
                Cue(NeonGridAudioEvent.GateActivated, gate, .43f, .99f, 1.01f,
                    .10f, 2606),
                Cue(NeonGridAudioEvent.GateDeactivated, powerOff, .32f, .99f, 1.01f,
                    .10f, 2612),
                Cue(NeonGridAudioEvent.ObjectiveActivated, objective, .68f, .99f, 1.01f,
                    .12f, 2607),
                Cue(NeonGridAudioEvent.HintActivated, hint, .55f, .995f, 1.005f,
                    .15f, 2608),
                Cue(NeonGridAudioEvent.UIButtonPressed, button, .38f, .985f, 1.015f,
                    .035f, 2609),
                Cue(NeonGridAudioEvent.CompletionTriggered, completion, .78f, 1f, 1f,
                    .50f, 2610)
            };
        }

        private static NeonGridAudioCue Cue(NeonGridAudioEvent eventType, AudioClip clip,
            float gain, float minimumPitch, float maximumPitch, float cooldown, int seed)
        {
            return new NeonGridAudioCue(eventType, clip, gain, minimumPitch, maximumPitch,
                cooldown, seed);
        }

        private static AudioClip Load(string relativePath)
        {
            string path = $"Assets/NeonGrid/Audio/SFX/{relativePath}";
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new FileNotFoundException($"Generated SFX is missing: {path}");
            return clip;
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
