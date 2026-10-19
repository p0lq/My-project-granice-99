using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static SceneBuildKit;

// Площадка для проверки управления, камеры, боя и анимаций без сюжета и меню.
public static class TestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test.unity";

    [MenuItem("Граница-99/Собрать тестовую сцену", priority = 20)]
    static void Build()
    {
        if (!ConfirmOverwrite(ScenePath))
            return;
        var scene = NewScene();

        var ground = GetMaterial("Ground", new Color(0.72f, 0.63f, 0.45f));
        var wall = GetMaterial("Wall", new Color(0.85f, 0.82f, 0.74f));
        var box = GetMaterial("Box", new Color(0.45f, 0.33f, 0.2f));
        var player = GetMaterial("Player", new Color(0.25f, 0.4f, 0.6f));
        var grey = GetMaterial("Grey", new Color(0.45f, 0.5f, 0.45f));
        var danger = GetMaterial("Danger", new Color(0.85f, 0.75f, 0.2f));

        Sun(1.2f, new Color(0.55f, 0.55f, 0.6f));
        Ground(60f, ground);

        // Дома размером как в плане арта: 6×8 м, стены 3 м.
        Block("Дом 1", new Vector3(-8f, 1.5f, 6f), new Vector3(6f, 3f, 8f), wall);
        Block("Дом 2", new Vector3(9f, 1.5f, 8f), new Vector3(8f, 3f, 6f), wall);
        Block("Дом 3", new Vector3(10f, 1.5f, -9f), new Vector3(6f, 3f, 8f), wall);
        Block("Ящик 1", new Vector3(-3f, 0.25f, -4f), new Vector3(0.8f, 0.5f, 0.5f), box);
        Block("Ящик 2", new Vector3(-2f, 0.25f, -4.5f), new Vector3(0.8f, 0.5f, 0.5f), box);
        // Пандус, чтобы проверить ходьбу по наклону.
        var ramp = Block("Пандус", new Vector3(0f, 0.6f, 12f), new Vector3(3f, 0.2f, 6f), box);
        ramp.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);

        DustZone(new Vector3(-9f, 0f, -8f), 5f, danger);

        Navigation(Vector3.zero, new Vector3(80f, 20f, 80f));

        var playerObject = Player(Vector3.zero, player, false);
        var label = playerObject.AddComponent<DebugHealthLabel>();
        SetObject(label, "health", playerObject.GetComponent<Health>());

        Grey(new Vector3(-4f, 0f, 14f), grey, 1);
        Grey(new Vector3(16f, 0f, 0f), grey, 2);
        Grey(new Vector3(2f, 0f, -14f), grey, 3);

        Camera(playerObject, new Color(0.78f, 0.72f, 0.6f));

        EditorSceneManager.SaveScene(scene, ScenePath);
        GameScenesBuilder.UpdateBuildSettings();
        Selection.activeGameObject = playerObject;
        Debug.Log("Тестовая сцена собрана: " + ScenePath + ". Нажми Play.");
    }
}
