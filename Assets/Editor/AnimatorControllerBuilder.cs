using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Создаёт Animator Controller для персонажа из клипов его FBX.
// Схема: Locomotion (смесь Idle/Walk/Run по Speed) ⇄ Crouch (если есть клипы приседа),
// из любого состояния по триггерам в Attack и Hit с возвратом, по Dead в Death навсегда.
// Параметры совпадают с теми, что выставляет CharacterAnimator.
public static class AnimatorControllerBuilder
{
    const string OutputFolder = "Assets/Art/Animators";

    // На какой скорости (м/с) клип играет «в ноги». Совпадает со скоростями в
    // PlayerMovement и EnemyAI: если там поменять, лучше поменять и здесь.
    static readonly Dictionary<string, (float walk, float run)> Speeds = new()
    {
        { "SK_Player", (3f, 5.5f) },
        { "SK_Grey", (3.4f, 5f) },
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
        var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath)
            .OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToDictionary(c => c.name);

        if (!clips.ContainsKey("Idle"))
        {
            Debug.LogError($"В {modelPath} нет клипа Idle. Найдено: {string.Join(", ", clips.Keys)}");
            return null;
        }

        string modelName = Path.GetFileNameWithoutExtension(modelPath);
        var (walkSpeed, runSpeed) = Speeds.TryGetValue(modelName, out var s) ? s : (3f, 5.5f);

        if (!AssetDatabase.IsValidFolder("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Art", "Animators");

        string controllerPath = $"{OutputFolder}/{modelName}.controller";
        AssetDatabase.DeleteAsset(controllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        var sm = controller.layers[0].stateMachine;

        var locomotion = controller.CreateBlendTreeInController("Locomotion", out var tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(clips["Idle"], 0f);
        if (clips.TryGetValue("Walk", out var walk))
            tree.AddChild(walk, walkSpeed);
        if (clips.TryGetValue("Run", out var run))
            tree.AddChild(run, runSpeed);
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

    static void Connect(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter)
    {
        var t = from.AddTransition(to);
        t.AddCondition(mode, 0f, parameter);
        t.hasExitTime = false;
        t.duration = 0.15f;
    }
}
