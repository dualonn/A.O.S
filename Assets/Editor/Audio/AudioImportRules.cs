using UnityEditor;
using UnityEngine;

/// <summary>
/// Import settings for Resources/Audio: music and ambience stream from disk (long, one at a time);
/// short effects load into memory, compressed, so they start instantly.
/// The same on every platform on purpose: WebGL has no audio overrides of its own (Unity ignores
/// SetOverrideSampleSettings("WebGL")), and settings that depend on the active platform would rewrite
/// hundreds of .meta files on every platform switch. An hour of music is most of the Web download,
/// so music and ambience are squeezed harder than effects (WebGL ignores Streaming by itself).
/// </summary>
public class AudioImportRules : AssetPostprocessor
{
    // bump when the rules change, so Unity re-imports the clips under them
    public override uint GetVersion() => 6;

    void OnPreprocessAudio()
    {
        string path = assetPath.Replace('\\', '/');
        if (!path.Contains("/Resources/Audio/")) return;
        var importer = (AudioImporter)assetImporter;
        var settings = importer.defaultSampleSettings;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        bool music = path.Contains("/Music/"), ambience = path.Contains("/Ambience/");
        if (music || ambience)
        {
            settings.loadType = AudioClipLoadType.Streaming;
            settings.quality = music ? 0.35f : 0.25f;   // was 0.6: about half the size
            importer.loadInBackground = true;
        }
        else
        {
            bool longClip = path.Contains("/UI/age_up") || path.Contains("/UI/raid_warning");
            settings.loadType = longClip ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.quality = 0.7f;
            importer.forceToMono = !path.Contains("/UI/");
        }
        importer.defaultSampleSettings = settings;
    }
}
