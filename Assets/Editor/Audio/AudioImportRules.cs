using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for Resources/Audio: music and ambience stream from disk (long, one at a time);
/// short effects load into memory, compressed, so they start instantly.
/// </summary>
public class AudioImportRules : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/Audio/")) return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        if (path.Contains("/Music/") || path.Contains("/Ambience/"))
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.quality = 0.6f;
            importer.loadInBackground = true;
        }
        else
        {
            bool longClip = path.Contains("/UI/victory") || path.Contains("/UI/defeat") || path.Contains("/UI/age_up") || path.Contains("/UI/raid_warning");
            settings.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.quality = 0.7f;
            importer.forceToMono = !path.Contains("/UI/");
        }
        importer.defaultSampleSettings = settings;
    }
}
