using System;
using System.Collections.Generic;
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

    // ---------------------------------------------------------------- текстуры

    // Mixamo кладёт текстуры внутрь FBX. Unity их оттуда сам не достаёт, и материалы
    // получаются белыми. Здесь после импорта модели текстуры извлекаются в Assets/Art/Textures,
    // а модель переимпортируется: поиск текстур (Material Search: Recursive-Up) находит
    // папку Textures рядом с папкой Models и подключает их к материалам.
    const string TextureFolder = "Assets/Art/Textures";
    static readonly List<string> pendingExtract = new();

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (var path in imported)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (path.StartsWith(Folder, StringComparison.Ordinal) && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) &&
                name.StartsWith("SK_", StringComparison.Ordinal) && ClipNameFromFile(name) == null &&
                !SessionState.GetBool("Extracted:" + path, false))
                pendingExtract.Add(path);
        }
        if (pendingExtract.Count > 0)
            EditorApplication.delayCall += ExtractPending;
    }

    // Модели, импортированные до появления этого кода, тоже проверяем: после каждой
    // перекомпиляции скриптов ищем персонажей с пустыми материалами.
    [InitializeOnLoadMethod]
    static void CheckExistingModels()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(Folder.TrimEnd('/')))
                return;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Folder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("SK_", StringComparison.Ordinal) && ClipNameFromFile(name) == null &&
                    !SessionState.GetBool("Extracted:" + path, false))
                    pendingExtract.Add(path);
            }
            ExtractPending();
        };
    }

    // Текстуры нужны, если хотя бы у одного материала модели нет основной текстуры.
    static bool NeedsTextures(string path)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is UnityEngine.Material m && m.mainTexture == null)
                return true;
        return false;
    }

    // Извлекать текстуры во время импорта нельзя, поэтому делаем это сразу после него.
    static void ExtractPending()
    {
        if (pendingExtract.Count == 0)
            return;
        var paths = new List<string>(pendingExtract);
        pendingExtract.Clear();

        if (!AssetDatabase.IsValidFolder(TextureFolder))
            AssetDatabase.CreateFolder("Assets/Art", "Textures");

        foreach (var path in paths)
        {
            // Одна попытка на модель за сеанс редактора, чтобы не уйти в бесконечный переимпорт.
            SessionState.SetBool("Extracted:" + path, true);
            if (!NeedsTextures(path))
                continue;
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && importer.ExtractTextures(TextureFolder))
            {
                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(TextureFolder + "/", StringComparison.Ordinal))
            return;
        var importer = (TextureImporter)assetImporter;
        string name = Path.GetFileNameWithoutExtension(assetPath);
        if (name.EndsWith("_Normal", StringComparison.Ordinal))
            importer.textureType = TextureImporterType.NormalMap;
        // Карты блеска и отражения хранят числа, а не цвет: гамма-коррекция их бы исказила.
        else if (name.EndsWith("_Glossiness", StringComparison.Ordinal) || name.EndsWith("_Specular", StringComparison.Ordinal))
            importer.sRGBTexture = false;
    }

    // ---------------------------------------------------------------- имена клипов

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
