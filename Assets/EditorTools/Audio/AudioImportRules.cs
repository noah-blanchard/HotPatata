using UnityEditor;
using UnityEngine;

namespace HotPatata.Editor
{
    /// <summary>
    /// Import settings for a sound dropped in Assets/Audio, by folder (ARCHITECTURE §26), set once when it first arrives, so a
    /// setting changed by hand afterwards is kept:
    ///   Music: streamed from disk (long tracks never sit in memory), loaded in the background.
    ///   Ambience beds and emitters: compressed in memory (long loops).
    ///   Everything else (effects, menus, scattered calls): decompressed on load (short, played often, no decode cost).
    /// Sounds placed in the world are forced to mono (3D sources play one channel); music, beds and menus keep their stereo.
    /// The generated placeholders in Assets/Audio/Ambient keep their own rule (NatureAssetImporter).
    /// </summary>
    public class AudioImportRules : AssetPostprocessor
    {
        const string Root = "Assets/Audio/";
        const string LegacyAmbient = "Assets/Audio/Ambient/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root) || assetPath.StartsWith(LegacyAmbient) || !assetImporter.importSettingsMissing) return;
            var importer = (AudioImporter)assetImporter;
            string key = assetPath.Substring(Root.Length);
            var s = importer.defaultSampleSettings;

            bool music = key.StartsWith("Music/");
            bool loops = key.StartsWith("Ambience/Emitters/") || key.StartsWith("Ambience/") && key.Contains("/Beds/");
            bool flat = music || key.StartsWith("SFX/UI/") || key.StartsWith("SFX/Course/") || key.Contains("/Beds/");

            if (music)
            {
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.7f;
                s.preloadAudioData = false;
                importer.loadInBackground = true;
            }
            else if (loops)
            {
                s.loadType = AudioClipLoadType.CompressedInMemory;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.6f;
                importer.loadInBackground = true;
            }
            else
            {
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.ADPCM;
            }
            importer.forceToMono = !flat;
            importer.defaultSampleSettings = s;
        }
    }
}
