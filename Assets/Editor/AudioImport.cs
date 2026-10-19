using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Настройки импорта звуков из Assets/Resources/Audio/<имя звука>/.
//   Длинные петли (дыхание, ломка двери, фон) хранятся сжатыми: распакованная
//   двухминутная запись заняла бы в памяти около 20 МБ.
//   Остальные распаковываются при загрузке: так они играют без задержки,
//   а SoundSlicer может прочитать их данные и нарезать.
//   Звуки в мире переводятся в моно: у стерео в 3D хуже слышно, откуда идёт звук.
public class AudioImport : AssetPostprocessor
{
    const string Folder = "Assets/Resources/Audio/";

    static readonly string[] LongLoops = { "DoorBash", "ZombieBreath", "AmbientWind", "AmbientHum" };
    static readonly string[] Flat = { "UIClick", "Win", "Lose", "AmbientWind", "AmbientHum" };

    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith(Folder, StringComparison.Ordinal))
            return;

        var importer = (AudioImporter)assetImporter;
        string sound = Path.GetFileName(Path.GetDirectoryName(assetPath));

        var settings = importer.defaultSampleSettings;
        settings.loadType = Array.IndexOf(LongLoops, sound) >= 0
            ? AudioClipLoadType.CompressedInMemory
            : AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
        importer.forceToMono = Array.IndexOf(Flat, sound) < 0;
    }
}
