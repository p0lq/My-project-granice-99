using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Настройки импорта моделей, чтобы не выставлять их руками для каждого FBX.
//   Assets/Art/Models/SK_Имя.fbx      — персонаж со скелетом (и, если из Blender, со всеми анимациями):
//                                       риг Humanoid, клипы «Armature|Walk» переименовываются в «Walk».
//   Assets/Art/Models/SK_Имя@Клип.fbx — отдельный файл с одной анимацией, как отдаёт Mixamo:
//                                       клип получает имя из части после @.
//   Assets/Art/Models/SM_Имя.fbx      — неподвижный объект без скелета и анимаций.
//   Assets/Art/Import/SK_Имя@Клип.fbx — анимация Mixamo вместе с моделью (With Skin, ~110 МБ).
//                                       После импорта её клипы сохраняются в Assets/Art/Animations
//                                       как SK_Имя@Клип.anim, а сам FBX в Git не попадает (.gitignore).
public class CharacterModelImport : AssetPostprocessor
{
    const string Folder = "Assets/Art/Models/";
    const string ImportFolder = "Assets/Art/Import/";
    public const string AnimationFolder = "Assets/Art/Animations";

    static bool IsCharacterFile(string path) =>
        path.StartsWith(Folder, StringComparison.Ordinal) || path.StartsWith(ImportFolder, StringComparison.Ordinal);

    static readonly string[] LoopingClips = { "Idle", "Walk", "Run", "CrouchIdle", "CrouchWalk" };

    void OnPreprocessModel()
    {
        if (!IsCharacterFile(assetPath))
            return;

        var importer = (ModelImporter)assetImporter;
        // Только при первом импорте, чтобы ручные правки в инспекторе не затирались.
        // Папка Import служебная: там настройки выставляются всегда.
        if (!importer.importSettingsMissing && !assetPath.StartsWith(ImportFolder, StringComparison.Ordinal))
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
        if (!IsCharacterFile(assetPath) || !name.StartsWith("SK_", StringComparison.Ordinal))
            return;

        var importer = (ModelImporter)assetImporter;
        string fileClip = ClipNameFromFile(name);
        var result = new List<ModelImporterClipAnimation>();
        foreach (var clip in importer.defaultClipAnimations)
        {
            // В файле Mixamo один клип с именем «mixamo.com», настоящее имя берём из имени файла.
            string clipName = fileClip ?? CleanName(clip.name);
            if (Splits.TryGetValue(name, out var parts))
            {
                foreach (var (part, from, to, fromEnd, mirror) in parts)
                {
                    var piece = Piece(clip, part, from, to, fromEnd);
                    piece.mirror = mirror;
                    result.Add(piece);
                }
            }
            else
                result.Add(Setup(clip, clipName));
        }
        importer.clipAnimations = result.ToArray();
    }

    // Одна запись Mixamo — несколько клипов игры (ключ — имя файла). Кадры from..to считаются
    // от начала записи, а при fromEnd — от её конца.
    //   «Start Walking» начинается со стойки, потом персонаж трогается и идёт: первый кадр — Idle,
    //   последние 32 кадра (шаг левой и правой) — цикл Walk.
    //   Стойка для приседа — первый кадр CrouchWalk.
    //   Из ударов берётся окно вокруг момента удара, чтобы оно укладывалось в перезарядку 0,5 с
    //   почти без ускорения (иначе удар дёргается). Момент удара — кадр, где кисть движется
    //   быстрее всего (замерено в Blender):
    //     «удар ножом», 39 кадров: бьёт левая, пик на 18-м → кадры 10–26, Mirror, потому что
    //     нож у героя в правой руке;
    //     «удар монтировкой», 93 кадра: двуручный, пик правой кисти на 35-м → кадры 22–42.
    static readonly Dictionary<string, (string clip, int from, int to, bool fromEnd, bool mirror)[]> Splits = new()
    {
        { "SK_Player@Walk", new[] { ("Idle", 0, 1, false, false), ("Walk", 32, 0, true, false) } },
        { "SK_Player@CrouchWalk", new[] { ("CrouchIdle", 0, 1, false, false), ("CrouchWalk", 0, 0, false, false) } },
        { "SK_Player@Attack", new[] { ("Attack", 9, 25, false, true) } },
        { "SK_Player@Attack2H", new[] { ("Attack2H", 21, 41, false, false) } },
    };

    // to = 0 — до конца записи.
    static ModelImporterClipAnimation Piece(ModelImporterClipAnimation source, string name, int from, int to, bool fromEnd)
    {
        var clip = Setup(Copy(source), name);
        if (fromEnd)
            clip.firstFrame = Mathf.Max(source.firstFrame, source.lastFrame - from);
        else
        {
            clip.firstFrame = Mathf.Min(source.lastFrame - 1f, source.firstFrame + from);
            if (to > 0)
                clip.lastFrame = Mathf.Min(source.lastFrame, source.firstFrame + to);
        }
        // Концы цикла, вырезанного из середины записи, могут не совпасть: Loop Pose сглаживает стык.
        clip.loopPose = clip.loopTime && clip.lastFrame - clip.firstFrame > 1f;
        return clip;
    }

    static ModelImporterClipAnimation Setup(ModelImporterClipAnimation clip, string name)
    {
        clip.name = name;
        clip.loopTime = Array.IndexOf(LoopingClips, name) >= 0;

        // Двигает персонажа код, а не анимация (applyRootMotion выключен). Перемещение
        // корня по XZ не запекаем, и Unity его отбрасывает: даже анимация Mixamo без
        // галочки In Place будет идти на месте. Высоту и поворот запекаем в позу,
        // чтобы падение в Death и развороты остались видны.
        clip.lockRootPositionXZ = false;
        clip.lockRootRotation = true;
        clip.keepOriginalOrientation = true;
        clip.lockRootHeightY = true;
        clip.keepOriginalPositionY = true;
        return clip;
    }

    static ModelImporterClipAnimation Copy(ModelImporterClipAnimation c) => new ModelImporterClipAnimation
    {
        takeName = c.takeName,
        firstFrame = c.firstFrame,
        lastFrame = c.lastFrame,
        wrapMode = c.wrapMode,
    };

    // ---------------------------------------------------------------- клипы из больших FBX

    static readonly List<string> pendingAnims = new();

    static void QueueAnimationExtract(string[] imported)
    {
        foreach (var path in imported)
            if (path.StartsWith(ImportFolder, StringComparison.Ordinal) && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                pendingAnims.Add(path);
        if (pendingAnims.Count > 0)
            EditorApplication.delayCall += ExtractAnimations;
    }

    // Если FBX в Import импортировался раньше, чем скомпилировался этот код, клипов .anim
    // ещё нет: переимпортируем такие файлы, и ExtractAnimations их сохранит.
    [InitializeOnLoadMethod]
    static void CheckImportFolder()
    {
        EditorApplication.delayCall += () =>
        {
            if (!AssetDatabase.IsValidFolder(ImportFolder.TrimEnd('/')))
                return;
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ImportFolder.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                int at = file.IndexOf('@');
                if (at <= 0)
                    continue;
                bool missing = !File.Exists($"{AnimationFolder}/{file}.anim") && !SessionState.GetBool("AnimImport:" + path, false);
                if (missing)
                {
                    SessionState.SetBool("AnimImport:" + path, true);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
        };
    }

    // Копия клипа в отдельном файле .anim не зависит от FBX: Humanoid-клип хранит «мышцы»,
    // посчитанные Unity по скелету самого Mixamo, поэтому стопы и колени остаются как в оригинале.
    static void ExtractAnimations()
    {
        if (pendingAnims.Count == 0)
            return;
        var paths = new List<string>(pendingAnims);
        pendingAnims.Clear();
        if (!AssetDatabase.IsValidFolder(AnimationFolder))
            AssetDatabase.CreateFolder("Assets/Art", "Animations");

        foreach (var path in paths)
        {
            string file = Path.GetFileNameWithoutExtension(path);
            int at = file.IndexOf('@');
            string model = at > 0 ? file.Substring(0, at) : file;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is not AnimationClip clip || clip.name.StartsWith("__preview__") || clip.name == "mixamo.com")
                    continue;
                string target = $"{AnimationFolder}/{model}@{clip.name}.anim";
                var copy = UnityEngine.Object.Instantiate(clip);
                copy.name = clip.name;
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
                if (existing != null)
                {
                    // Перезаписываем на месте: на клип уже ссылается Animator Controller.
                    EditorUtility.CopySerialized(copy, existing);
                    existing.name = clip.name;
                    EditorUtility.SetDirty(existing);
                }
                else
                    AssetDatabase.CreateAsset(copy, target);
                Debug.Log($"Анимация сохранена: {target}");
            }
        }
        AssetDatabase.SaveAssets();
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
        QueueAnimationExtract(imported);
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
