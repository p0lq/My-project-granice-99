using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

// Собирает сцену для проверки управления, камеры и здоровья из примитивов.
// Нужна, пока нет моделей: игрок здесь капсула, дома кубы.
public static class TestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Test.unity";
    const string MaterialFolder = "Assets/Materials/Test";

    [MenuItem("Граница-99/Собрать тестовую сцену")]
    static void Build()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Тестовая сцена", "Сцена Test уже есть. Пересобрать её заново?", "Пересобрать", "Отмена"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var ground = GetMaterial("Ground", new Color(0.72f, 0.63f, 0.45f));
        var wall = GetMaterial("Wall", new Color(0.85f, 0.82f, 0.74f));
        var box = GetMaterial("Box", new Color(0.45f, 0.33f, 0.2f));
        var player = GetMaterial("Player", new Color(0.25f, 0.4f, 0.6f));
        var grey = GetMaterial("Grey", new Color(0.45f, 0.5f, 0.45f));
        var danger = GetMaterial("Danger", new Color(0.85f, 0.75f, 0.2f));

        var sun = new GameObject("Солнце").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

        // Plane 10×10 м, масштаб 6 даёт поле 60×60 м.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Земля";
        floor.transform.localScale = new Vector3(6f, 1f, 6f);
        floor.GetComponent<Renderer>().sharedMaterial = ground;

        // Дома размером как в плане арта: 6×8 м, стены 3 м.
        Block("Дом 1", new Vector3(-8f, 1.5f, 6f), new Vector3(6f, 3f, 8f), wall);
        Block("Дом 2", new Vector3(9f, 1.5f, 8f), new Vector3(8f, 3f, 6f), wall);
        Block("Дом 3", new Vector3(10f, 1.5f, -9f), new Vector3(6f, 3f, 8f), wall);
        Block("Ящик 1", new Vector3(-3f, 0.25f, -4f), new Vector3(0.8f, 0.5f, 0.5f), box);
        Block("Ящик 2", new Vector3(-2f, 0.25f, -4.5f), new Vector3(0.8f, 0.5f, 0.5f), box);
        // Пандус, чтобы проверить ходьбу по наклону.
        var ramp = Block("Пандус", new Vector3(0f, 0.6f, 12f), new Vector3(3f, 0.2f, 6f), box);
        ramp.transform.rotation = Quaternion.Euler(-12f, 0f, 0f);

        var zone = Block("Пыль (урон)", new Vector3(-9f, 0.05f, -8f), new Vector3(5f, 0.1f, 5f), danger);
        var zoneCollider = zone.GetComponent<BoxCollider>();
        zoneCollider.isTrigger = true;
        // Высокий триггер, чтобы капсула гарантированно в нём оказалась.
        zoneCollider.size = new Vector3(1f, 20f, 1f);
        zone.AddComponent<DamageZone>();

        new GameObject("Навигация").AddComponent<RuntimeNavMesh>();

        var playerObject = BuildPlayer(player);
        BuildGrey(grey, new Vector3(-4f, 0f, 14f), 1);
        BuildGrey(grey, new Vector3(16f, 0f, 0f), 2);
        BuildGrey(grey, new Vector3(2f, 0f, -14f), 3);

        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 35f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.78f, 0.72f, 0.6f);
        cameraObject.AddComponent<AudioListener>();
        var iso = cameraObject.AddComponent<IsoCamera>();
        SetField(iso, "target", playerObject.transform);
        iso.Snap();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Selection.activeGameObject = playerObject;
        Debug.Log("Тестовая сцена собрана: " + ScenePath + ". Нажми Play.");
    }

    static GameObject BuildPlayer(Material material)
    {
        // Рост 1,75 м, как у будущей модели.
        var root = new GameObject("Игрок");
        root.tag = "Player";
        root.transform.position = new Vector3(0f, 0.1f, 0f);

        var controller = root.AddComponent<CharacterController>();
        controller.height = 1.75f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, 0.875f, 0f);

        var health = root.AddComponent<Health>();
        root.AddComponent<PlayerMovement>();
        root.AddComponent<PlayerCombat>();
        var animator = root.AddComponent<CharacterAnimator>();
        SetField(animator, "fallbackBody", Body(root.transform, "SK_Player", material, 1.75f));
        var label = root.AddComponent<DebugHealthLabel>();
        SetField(label, "health", health);
        return root;
    }

    static void BuildGrey(Material material, Vector3 position, int number)
    {
        var root = new GameObject($"Серый {number}");
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // Коллайдер нужен, чтобы по «Серому» попадал удар и игрок в него упирался.
        var collider = root.AddComponent<CapsuleCollider>();
        collider.height = 1.8f;
        collider.radius = 0.35f;
        collider.center = new Vector3(0f, 0.9f, 0f);

        var agent = root.AddComponent<NavMeshAgent>();
        agent.height = 1.8f;
        agent.radius = 0.35f;
        agent.angularSpeed = 360f;
        agent.stoppingDistance = 1.1f;
        // EnemyAI включит агента в Start, когда навигационная сетка уже построена.
        agent.enabled = false;

        var health = root.AddComponent<Health>();
        SetFloat(health, "maxHealth", 60f);
        root.AddComponent<EnemyAI>();
        var animator = root.AddComponent<CharacterAnimator>();
        SetField(animator, "fallbackBody", Body(root.transform, "SK_Grey", material, 1.8f));
    }

    // Видимая часть персонажа. Если модель уже лежит в Assets/Art/Models, ставим её
    // с Animator Controller, иначе капсулу с «носом», чтобы было видно, куда смотрит.
    static Transform Body(Transform root, string modelName, Material material, float height)
    {
        string modelPath = $"Assets/Art/Models/{modelName}.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            // У объектов Unity оператор ?? не видит «уничтоженный» null, поэтому проверки явные.
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"Assets/Art/Animators/{modelName}.controller");
            if (controller == null)
                controller = AnimatorControllerBuilder.Build(modelPath);
            animator.runtimeAnimatorController = controller;
            return instance.transform;
        }

        var body = new GameObject("Тело (временное)").transform;
        body.SetParent(root, false);

        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        Object.DestroyImmediate(capsule.GetComponent<Collider>());
        capsule.transform.SetParent(body, false);
        capsule.transform.localPosition = new Vector3(0f, height / 2f, 0f);
        capsule.transform.localScale = new Vector3(0.6f, height / 2f, 0.6f);
        capsule.GetComponent<Renderer>().sharedMaterial = material;

        var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Направление";
        Object.DestroyImmediate(nose.GetComponent<Collider>());
        nose.transform.SetParent(body, false);
        nose.transform.localPosition = new Vector3(0f, height - 0.3f, 0.3f);
        nose.transform.localScale = new Vector3(0.15f, 0.15f, 0.25f);
        nose.GetComponent<Renderer>().sharedMaterial = material;

        return body;
    }

    static GameObject Block(string name, Vector3 position, Vector3 size, Material material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.position = position;
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    static Material GetMaterial(string name, Color color)
    {
        string path = $"{MaterialFolder}/{name}.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
            return existing;

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Materials", "Test");

        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = color };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Поля скриптов закрытые ([SerializeField]), поэтому задаём их так же, как инспектор.
    static void SetField(Object component, string field, Object value)
    {
        var serialized = new SerializedObject(component);
        serialized.FindProperty(field).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object component, string field, float value)
    {
        var serialized = new SerializedObject(component);
        serialized.FindProperty(field).floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddToBuildSettings(string path)
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            // Шаблон оставил ссылку на несуществующую SampleScene.
            if (s.path != path && File.Exists(s.path))
                scenes.Add(s);
        }
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
