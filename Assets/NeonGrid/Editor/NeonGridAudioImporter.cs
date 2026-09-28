using UnityEditor;
using UnityEngine;

namespace NeonGrid.Editor
{
    public sealed class NeonGridAudioImporter : AssetPostprocessor
    {
        private const string SfxRoot = "Assets/NeonGrid/Audio/SFX/";
        private const string AmbienceRoot = "Assets/NeonGrid/Audio/Ambience/";

        private void OnPreprocessAudio()
        {
            bool isSfx = assetPath.StartsWith(SfxRoot, System.StringComparison.Ordinal);
            bool isAmbience = assetPath.StartsWith(AmbienceRoot,
                System.StringComparison.Ordinal);
            if ((!isSfx && !isAmbience) || !assetPath.EndsWith(".wav",
                    System.StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = isSfx;
            importer.loadInBackground = false;
            importer.defaultSampleSettings = new AudioImporterSampleSettings
            {
                loadType = isSfx ? AudioClipLoadType.DecompressOnLoad :
                    AudioClipLoadType.CompressedInMemory,
                compressionFormat = isSfx ? AudioCompressionFormat.PCM :
                    AudioCompressionFormat.Vorbis,
                quality = isSfx ? 1f : 0.55f,
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
                preloadAudioData = true
            };
        }
    }
}
