using System;
using System.IO;
using UnityEditor;

// Настройки импорта моделей из Blender, чтобы не выставлять их руками для каждого FBX.
//   Assets/Art/Models/SK_*.fbx — персонаж со скелетом и анимациями: риг Humanoid,
//     клипы переименовываются из «Armature|Walk» в «Walk», циклы включаются по имени.
//   Assets/Art/Models/SM_*.fbx — неподвижный объект: без анимаций и скелета.
public class CharacterModelImport : AssetPostprocessor
{
    const string Folder = "Assets/Art/Models/";

    static readonly string[] LoopingClips = { "Idle", "Walk", "Run", "CrouchIdle", "CrouchWalk" };

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder, StringComparison.Ordinal))
            return;

        var importer = (ModelImporter)assetImporter;
        // Только при первом импорте, чтобы ручные правки в инспекторе не затирались.
        if (!importer.importSettingsMissing)
            return;

        string name = Path.GetFileName(assetPath);
        if (name.StartsWith("SK_", StringComparison.Ordinal))
        {
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
        }
        else if (name.StartsWith("SM_", StringComparison.Ordinal))
        {
            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
        }
    }

    // Список клипов пересобирается при каждом импорте: добавил в Blender новый Action,
    // переэкспортировал FBX — клип появился сам.
    void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(Folder, StringComparison.Ordinal) ||
            !Path.GetFileName(assetPath).StartsWith("SK_", StringComparison.Ordinal))
            return;

        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name = CleanName(clip.name);
            clip.loopTime = Array.IndexOf(LoopingClips, clip.name) >= 0;

            // Анимации сделаны на месте, двигает персонажа код. Запекаем корень в позу,
            // чтобы модель не уплывала и не поворачивалась от накопленной ошибки.
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = clips;
    }

    // Blender называет клипы по схеме «ИмяАрматуры|ИмяAction».
    public static string CleanName(string clipName)
    {
        int bar = clipName.LastIndexOf('|');
        return bar >= 0 ? clipName.Substring(bar + 1) : clipName;
    }
}
