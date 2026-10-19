using System;
using System.IO;
using UnityEditor;

// Настройки импорта моделей, чтобы не выставлять их руками для каждого FBX.
//   Assets/Art/Models/SK_Имя.fbx      — персонаж со скелетом (и, если из Blender, со всеми анимациями):
//                                       риг Humanoid, клипы «Armature|Walk» переименовываются в «Walk».
//   Assets/Art/Models/SK_Имя@Клип.fbx — отдельный файл с одной анимацией, как отдаёт Mixamo:
//                                       клип получает имя из части после @.
//   Assets/Art/Models/SM_Имя.fbx      — неподвижный объект без скелета и анимаций.
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

        string name = Path.GetFileNameWithoutExtension(assetPath);
        if (name.StartsWith("SK_", StringComparison.Ordinal))
        {
            importer.animationType = ModelImporterAnimationType.Human;
            // Свой аватар у каждого файла: Humanoid сам переносит анимацию между скелетами,
            // и не важно, какой из файлов персонажа Unity импортирует первым.
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            if (ClipNameFromFile(name) != null)
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
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
        string name = Path.GetFileNameWithoutExtension(assetPath);
        if (!assetPath.StartsWith(Folder, StringComparison.Ordinal) || !name.StartsWith("SK_", StringComparison.Ordinal))
            return;

        var importer = (ModelImporter)assetImporter;
        string fileClip = ClipNameFromFile(name);
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            // В файле Mixamo один клип с именем «mixamo.com», настоящее имя берём из имени файла.
            clip.name = fileClip ?? CleanName(clip.name);
            clip.loopTime = Array.IndexOf(LoopingClips, clip.name) >= 0;

            // Двигает персонажа код, а не анимация (applyRootMotion выключен). Перемещение
            // корня по XZ не запекаем, и Unity его отбрасывает: даже анимация Mixamo без
            // галочки In Place будет идти на месте. Высоту и поворот запекаем в позу,
            // чтобы падение в Death и развороты остались видны.
            clip.lockRootPositionXZ = false;
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
        }
        importer.clipAnimations = clips;
    }

    // «SK_Grey@Walk» → «Walk», «SK_Grey» → null.
    public static string ClipNameFromFile(string fileName)
    {
        int at = fileName.IndexOf('@');
        return at >= 0 ? fileName.Substring(at + 1) : null;
    }

    // Blender называет клипы по схеме «ИмяАрматуры|ИмяAction».
    public static string CleanName(string clipName)
    {
        int bar = clipName.LastIndexOf('|');
        return bar >= 0 ? clipName.Substring(bar + 1) : clipName;
    }
}
