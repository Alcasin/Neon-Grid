using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public sealed class NeonGridAudioImporter : AssetPostprocessor
    {
        private const string SfxRoot = "Assets/NeonGrid/Audio/SFX/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(SfxRoot, System.StringComparison.Ordinal) ||
                !assetPath.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.DecompressOnLoad,
                compressionFormat = AudioCompressionFormat.PCM,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                preloadAudioData = true
            };
        }
    }
}
