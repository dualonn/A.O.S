using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for Resources/Audio: music and ambience stream from disk (long, one at a time);
/// short effects load into memory, compressed, so they start instantly.
/// WebGL can't stream: there long clips stay compressed in memory, and everything is a little
/// more compressed to keep the download small.
/// </summary>
public class AudioImportRules : AssetPostprocessor
{
    // bump when the rules change, so Unity re-imports the clips under them
    public override uint GetVersion() => 2;

    void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/Audio/")) return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        bool stream = path.Contains("/Music/") || path.Contains("/Ambience/");
        bool longClip = path.Contains("/UI/age_up") || path.Contains("/UI/raid_warning");
        if (stream)
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.quality = 0.6f;
            importer.loadInBackground = true;
        }
        else
        {
            settings.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.quality = 0.7f;
            importer.forceToMono = !path.Contains("/UI/");
        }
        importer.defaultSampleSettings = settings;

        var web = settings;
        web.compressionFormat = AudioCompressionFormat.AAC;
        web.quality = 0.5f;
        web.loadType = stream || longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
        importer.SetOverrideSampleSettings("WebGL", web);
    }
}
