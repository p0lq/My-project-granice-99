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
    };
    const float CrouchSpeed = 1.5f;

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
        var files = new List<string> { modelPath };
        files.AddRange(Directory.GetFiles(folder, modelName + "@*.fbx").Select(f => f.Replace(Path.DirectorySeparatorChar, '/')));

        var clips = new Dictionary<string, AnimationClip>();
        foreach (var file in files)
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(file).OfType<AnimationClip>())
                if (!clip.name.StartsWith("__preview__"))
                    clips[clip.name] = clip;

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

        AssetDatabase.SaveAssets();
        return controller;
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
