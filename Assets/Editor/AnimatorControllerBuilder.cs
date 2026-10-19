using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Создаёт Animator Controller для персонажа из клипов его FBX.
// Клипы берутся из самого FBX и из файлов «Модель@Клип.fbx» рядом с ним (так отдаёт Mixamo).
// Схема: Locomotion (смесь Idle/Walk/Run по Speed) ⇄ Crouch (если есть клипы приседа),
// из любого состояния по триггерам в Attack, Hit и Scream с возвратом, по Dead в Death навсегда.
// Параметры совпадают с теми, что выставляет CharacterAnimator.
public static class AnimatorControllerBuilder
{
    const string OutputFolder = "Assets/Art/Animators";

    // На какой скорости (м/с) клип играет «в ноги». Совпадает со скоростями в
    // PlayerMovement и EnemyAI: если там поменять, лучше поменять и здесь.
    static readonly Dictionary<string, (float walk, float run)> Speeds = new()
    {
        { "SK_Player", (3f, 5.5f) },
        // Walk под скорость блуждания, Run под погоню.
        { "SK_Grey", (1.2f, 3.4f) },
        { "SK_Bandit", (1.4f, 3f) },
    };
    const float CrouchSpeed = 1.5f;

    // У кого занимать недостающие клипы.
    // У кого занимать недостающие клипы, по порядку. Бандит (X Bot) своих анимаций не имеет:
    // ходит и бегает как герой, умирает как «Серый».
    static readonly Dictionary<string, string[]> BorrowFrom = new()
    {
        { "SK_Player", new[] { "SK_Grey" } },
        { "SK_Bandit", new[] { "SK_Player", "SK_Grey" } },
    };

    static Dictionary<string, AnimationClip> LoadClips(string folder, string modelName)
    {
        var files = new List<string> { $"{folder}/{modelName}.fbx" };
        files.AddRange(Directory.GetFiles(folder, modelName + "@*.fbx").Select(f => f.Replace(Path.DirectorySeparatorChar, '/')));
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var file in files)
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>())
                // «mixamo.com» — пустой клип с T-позой внутри самой модели Mixamo.
                if (!clip.name.StartsWith("__preview__") && clip.name != "mixamo.com")
                    clips[clip.name] = clip;

        // Клипы, сохранённые из больших FBX Mixamo (Assets/Art/Import), важнее одноимённых из FBX.
        if (AssetDatabase.IsValidFolder(CharacterModelImport.AnimationFolder))
            foreach (var path in Directory.GetFiles(CharacterModelImport.AnimationFolder, modelName + "@*.anim"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path.Replace(Path.DirectorySeparatorChar, '/'));
                if (clip != null)
                    clips[clip.name] = clip;
            }
        return clips;
    }

    [MenuItem("Граница-99/Создать Animator Controller для модели")]
    static void BuildForSelection()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        var controller = Build(path);
        if (controller != null)
        {
            Selection.activeObject = controller;
            Debug.Log("Animator Controller создан: " + AssetDatabase.GetAssetPath(controller));
        }
    }

    [MenuItem("Граница-99/Создать Animator Controller для модели", true)]
    static bool CanBuildForSelection()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase) &&
               Path.GetFileName(path).StartsWith("SK_");
    }

    public static AnimatorController Build(string modelPath)
    {
        string modelName = Path.GetFileNameWithoutExtension(modelPath);
        // Unity хранит пути ассетов с прямыми слешами, а Windows отдаёт обратные.
        string folder = Path.GetDirectoryName(modelPath).Replace(Path.DirectorySeparatorChar, '/');
        var clips = LoadClips(folder, modelName);

        // Недостающие клипы берём у другой модели: Humanoid переносит анимацию между
        // скелетами. Так у героя есть модель, пока не скачаны его собственные анимации.
        if (BorrowFrom.TryGetValue(modelName, out var donors))
            foreach (var donor in donors)
            {
                var donorClips = LoadClips(folder, donor);
                // Бандит стреляет: его удар — запись Firing Rifle героя (у героя это клип CrouchWalk).
                if (modelName == "SK_Bandit" && !clips.ContainsKey("Attack") && donorClips.TryGetValue("CrouchWalk", out var fire))
                    clips["Attack"] = fire;
                var borrowed = new List<string>();
                foreach (var pair in donorClips)
                    if (!clips.ContainsKey(pair.Key) && pair.Key != "Scream" && pair.Key != "Attack2H")
                    {
                        clips[pair.Key] = pair.Value;
                        borrowed.Add(pair.Key);
                    }
                if (borrowed.Count > 0)
                    Debug.Log($"{modelName}: клипы {string.Join(", ", borrowed)} взяты у {donor}");
            }

        if (!clips.ContainsKey("Idle"))
        {
            Debug.LogWarning($"У {modelName} нет клипа Idle (нужен {modelName}@Idle.fbx или Action Idle в самом FBX), " +
                             $"в сцене пока будет капсула. Найдено: {(clips.Count > 0 ? string.Join(", ", clips.Keys) : "ничего")}");
            return null;
        }

        var (walkSpeed, runSpeed) = Speeds.TryGetValue(modelName, out var s) ? s : (3f, 5.5f);

        if (!AssetDatabase.IsValidFolder("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Art", "Animators");

        // Существующий контроллер очищаем, а не удаляем: на него уже ссылаются собранные
        // сцены, и новый файл с другим GUID оставил бы их персонажей без анимаций.
        string controllerPath = $"{OutputFolder}/{modelName}.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        else
            Clear(controller);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Scream", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;

        var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(clips["Idle"], 0f);
        clips.TryGetValue("Walk", out var walk);
        clips.TryGetValue("Run", out var run);
        // Порог клипа ставим на ту скорость, с которой он движется сам, тогда ноги не скользят.
        // Клипы In Place (скорость около нуля) получают скорость из таблицы Speeds.
        float walkAt = walk != null ? ClipSpeed(walk, walkSpeed) : walkSpeed;
        float runAt = run != null ? Mathf.Max(ClipSpeed(run, runSpeed), walkAt + 0.5f) : runSpeed;
        if (walk != null)
            tree.AddChild(walk, walkAt);
        if (run != null)
            tree.AddChild(run, runAt);
        sm.defaultState = locomotion;

        if (clips.TryGetValue("CrouchWalk", out var crouchWalk))
        {
            var crouch = controller.CreateBlendTreeInController("Crouch", out var crouchTree, 0);
            crouchTree.blendParameter = "Speed";
            crouchTree.useAutomaticThresholds = false;
            crouchTree.AddChild(clips.TryGetValue("CrouchIdle", out var crouchIdle) ? crouchIdle : crouchWalk, 0f);
            crouchTree.AddChild(crouchWalk, CrouchSpeed);

            Connect(locomotion, crouch, AnimatorConditionMode.If, "Crouch");
            Connect(crouch, locomotion, AnimatorConditionMode.IfNot, "Crouch");
        }

        // У героя удар играет слой верха тела (ниже), а ноги продолжают идти или бежать.
        // Удар в базовом слое съел бы триггер Attack раньше этого слоя.
        bool weapons = modelName == "SK_Player";
        if (!weapons)
            AddOneShot(sm, clips, "Attack", locomotion);
        AddOneShot(sm, clips, "Hit", locomotion);
        AddOneShot(sm, clips, "Scream", locomotion);

        if (clips.TryGetValue("Death", out var death))
        {
            var dead = sm.AddState("Death");
            dead.motion = death;
            var t = sm.AddAnyStateTransition(dead);
            t.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            t.canTransitionToSelf = false;
            t.duration = 0.1f;
        }

        if (weapons)
            AddPlayerLayers(controller, folder, clips);

        AssetDatabase.SaveAssets();
        return controller;
    }

    // Слои героя поверх ходьбы и бега:
    //   «Руки крадучись» — в приседе руки берутся из стойки Idle (опущены, оружие в правой),
    //     а не из записи с винтовкой; вес слоя ставит CharacterAnimator по параметру Crouch;
    //   «Удар (верх тела)» — свой удар у каждого оружия, ноги в это время продолжают двигаться.
    static void AddPlayerLayers(AnimatorController controller, string folder, Dictionary<string, AnimationClip> clips)
    {
        controller.AddParameter("Weapon", AnimatorControllerParameterType.Int);

        var arms = NewLayer(controller, CrouchArmsLayer, ArmsMask(), 0f);
        arms.defaultState = arms.AddState("Руки опущены");
        arms.defaultState.motion = clips["Idle"];

        var attack = NewLayer(controller, "Удар (верх тела)", UpperBodyMask(), 1f);
        attack.defaultState = attack.AddState("Нет удара");
        clips.TryGetValue("Attack", out var knife);
        // Пока нет своего удара монтировкой (SK_Player@Attack2H), берём удар «Серого»:
        // замах двумя руками сверху похож на удар тяжёлым двуручным предметом.
        if (!clips.TryGetValue("Attack2H", out var crowbar))
            LoadClips(folder, "SK_Grey").TryGetValue("Attack", out crowbar);
        AddStrike(attack, "Удар ножом", knife, PlayerCombat.OneHanded);
        AddStrike(attack, "Удар монтировкой", crowbar, PlayerCombat.TwoHanded);
        // Выстрел из ПМ или ружья — вскидка из записи Firing Rifle (у героя это клип CrouchWalk).
        clips.TryGetValue("CrouchWalk", out var fire);
        AddStrike(attack, "Выстрел", fire, PlayerCombat.Gun);
    }

    public const string CrouchArmsLayer = "Руки крадучись";

    static AnimatorStateMachine NewLayer(AnimatorController controller, string name, AvatarMask mask, float weight)
    {
        var sm = new AnimatorStateMachine { name = name, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(sm, controller);
        controller.AddLayer(new AnimatorControllerLayer { name = name, avatarMask = mask, defaultWeight = weight, stateMachine = sm });
        return sm;
    }

    static void AddStrike(AnimatorStateMachine sm, string name, AnimationClip clip, int weapon)
    {
        if (clip == null)
            return;
        var strike = sm.AddState(name);
        strike.motion = clip;
        // Любой клип удара ускоряется или замедляется до длины перезарядки.
        strike.speed = clip.length / PlayerCombat.AttackDuration;

        var enter = sm.AddAnyStateTransition(strike);
        enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        enter.AddCondition(AnimatorConditionMode.Equals, weapon, "Weapon");
        enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        enter.canTransitionToSelf = true;
        enter.hasExitTime = false;
        enter.duration = 0.08f;
        // Плавный возврат рук после удара, без рывка в стойку.
        var back = strike.AddTransition(sm.defaultState);
        back.hasExitTime = true;
        back.exitTime = 0.85f;
        back.duration = 0.15f;
    }

    static AvatarMask ArmsMask() => Mask("Assets/Art/Animators/ArmsMask.mask",
        AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers);

    static AvatarMask UpperBodyMask() => Mask("Assets/Art/Animators/UpperBodyMask.mask",
        AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head, AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm,
        AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers);

    static AvatarMask Mask(string path, params AvatarMaskBodyPart[] active)
    {
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
        if (mask == null)
        {
            mask = new AvatarMask();
            AssetDatabase.CreateAsset(mask, path);
        }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, System.Array.IndexOf(active, (AvatarMaskBodyPart)i) >= 0);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    // Короткое действие поверх всего: запускается триггером и само возвращается в Locomotion.
    static void AddOneShot(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips, string name, AnimatorState back)
    {
        if (!clips.TryGetValue(name, out var clip))
            return;

        var state = sm.AddState(name);
        state.motion = clip;

        var enter = sm.AddAnyStateTransition(state);
        enter.AddCondition(AnimatorConditionMode.If, 0f, name);
        enter.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        enter.canTransitionToSelf = false;
        enter.duration = 0.05f;

        var exit = state.AddTransition(back);
        exit.hasExitTime = true;
        exit.exitTime = 0.9f;
        exit.duration = 0.1f;
    }

    static float ClipSpeed(AnimationClip clip, float fallback)
    {
        var v = clip.averageSpeed;
        float speed = new Vector2(v.x, v.z).magnitude;
        return speed > 0.3f ? speed : fallback;
    }

    static void Clear(AnimatorController controller)
    {
        controller.parameters = new AnimatorControllerParameter[0];
        while (controller.layers.Length > 1)
            controller.RemoveLayer(1);
        var sm = controller.layers[0].stateMachine;
        foreach (var t in sm.anyStateTransitions)
            sm.RemoveAnyStateTransition(t);
        foreach (var child in sm.states)
            sm.RemoveState(child.state);
        // Деревья смешивания живут в файле контроллера отдельно от состояний.
        foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller)).OfType<BlendTree>())
            AssetDatabase.RemoveObjectFromAsset(tree);
    }

    static void Connect(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter)
    {
        var t = from.AddTransition(to);
        t.AddCondition(mode, 0f, parameter);
        t.hasExitTime = false;
        t.duration = 0.15f;
    }
}
