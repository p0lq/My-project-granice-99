using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

// Общие детали для сборщиков сцен: материалы, блоки, стены, персонажи, камера, системы уровня.
// Пока нет моделей, всё строится из примитивов в реальных размерах из плана арта,
// так что потом кубы можно заменить моделями один к одному.
public static class SceneBuildKit
{
    const string MaterialFolder = "Assets/Materials/Test";
    const string ModelFolder = "Assets/Art/Models";

    public static bool ConfirmOverwrite(string scenePath)
    {
        if (File.Exists(scenePath) &&
            !EditorUtility.DisplayDialog("Сборка сцены", $"Сцена {Path.GetFileNameWithoutExtension(scenePath)} уже есть. Пересобрать её заново?", "Пересобрать", "Отмена"))
            return false;
        return EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
    }

    public static UnityEngine.SceneManagement.Scene NewScene() =>
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

    public static Material GetMaterial(string name, Color color)
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

    // ---------------------------------------------------------------- геометрия

    public static GameObject Block(string name, Vector3 position, Vector3 size, Material material, Transform parent = null)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        if (parent != null)
            cube.transform.SetParent(parent, false);
        cube.transform.position = position;
        cube.transform.localScale = size;
        cube.GetComponent<Renderer>().sharedMaterial = material;
        return cube;
    }

    // Стена по прямой от точки до точки на земле: (x1,z1) → (x2,z2). Только вдоль осей X или Z.
    public static GameObject Wall(Transform parent, float x1, float z1, float x2, float z2, float height, Material material, float thickness = 0.3f)
    {
        var center = new Vector3((x1 + x2) / 2f, height / 2f, (z1 + z2) / 2f);
        bool alongX = Mathf.Abs(x2 - x1) > Mathf.Abs(z2 - z1);
        // Удлиняем на толщину, чтобы углы смыкались без щелей.
        float length = Mathf.Abs(alongX ? x2 - x1 : z2 - z1) + thickness;
        var size = alongX ? new Vector3(length, height, thickness) : new Vector3(thickness, height, length);
        return Block("Стена", center, size, material, parent);
    }

    public static GameObject Ground(float size, Material material)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Земля";
        // Plane 10×10 м.
        floor.transform.localScale = new Vector3(size / 10f, 1f, size / 10f);
        floor.GetComponent<Renderer>().sharedMaterial = material;
        return floor;
    }

    public static Light Sun(float intensity, Color ambient)
    {
        var sun = new GameObject("Солнце").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = intensity;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;
        return sun;
    }

    public static Light PointLight(string name, Vector3 position, Color color, float intensity, float range)
    {
        var light = new GameObject(name).AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.transform.position = position;
        return light;
    }

    // Пятно «Пыльного фронта»: плоская жёлтая площадка, которая отнимает здоровье.
    public static GameObject DustZone(Vector3 center, float size, Material material)
    {
        var zone = Block("Пыль (урон)", center + Vector3.up * 0.05f, new Vector3(size, 0.1f, size), material);
        var collider = zone.GetComponent<BoxCollider>();
        collider.isTrigger = true;
        // Высокий триггер, чтобы персонаж гарантированно в нём оказался: 20 × 0,1 = 2 м.
        collider.size = new Vector3(1f, 20f, 1f);
        zone.AddComponent<DamageZone>();
        return zone;
    }

    // ---------------------------------------------------------------- персонажи

    public static GameObject Player(Vector3 position, Material material, bool fullKit)
    {
        var root = new GameObject("Игрок");
        root.tag = "Player";
        root.transform.position = position + Vector3.up * 0.1f;

        // Рост 1,75 м, как у будущей модели.
        var controller = root.AddComponent<CharacterController>();
        controller.height = 1.75f;
        controller.radius = 0.3f;
        controller.center = new Vector3(0f, 0.875f, 0f);

        root.AddComponent<Health>();
        root.AddComponent<PlayerMovement>();
        root.AddComponent<PlayerCombat>();
        var animator = root.AddComponent<CharacterAnimator>();
        SetObject(animator, "fallbackBody", Body(root.transform, "SK_Player", material, 1.75f));
        root.AddComponent<Footsteps>();
        root.AddComponent<CharacterSounds>();

        if (fullKit)
        {
            root.AddComponent<PlayerInteractor>();
            root.AddComponent<PlayerSupplies>();

            // Фонарик на груди, чуть вниз, чтобы освещал пол перед игроком.
            var lamp = new GameObject("Фонарик").AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 1.4f, 0.3f);
            lamp.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            lamp.range = 16f;
            lamp.spotAngle = 65f;
            lamp.innerSpotAngle = 35f;
            lamp.intensity = 6f;
            lamp.color = new Color(1f, 0.95f, 0.8f);
            lamp.shadows = LightShadows.Hard;
            var flashlight = root.AddComponent<Flashlight>();
            SetObject(flashlight, "lamp", lamp);
        }
        return root;
    }

    public static GameObject Grey(Vector3 position, Material material, int number)
    {
        var root = new GameObject($"Серый {number}");
        root.transform.position = position;
        // Повороты не случайные, чтобы пересборка давала одну и ту же сцену.
        root.transform.rotation = Quaternion.Euler(0f, number * 137f % 360f, 0f);

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
        SetObject(animator, "fallbackBody", Body(root.transform, "SK_Grey", material, 1.8f));
        var steps = root.AddComponent<Footsteps>();
        Set(steps, "stepLength", p => p.floatValue = 0.6f);
        Set(steps, "volume", p => p.floatValue = 0.3f);
        root.AddComponent<CharacterSounds>();
        return root;
    }

    // Видимая часть персонажа. Если модель уже лежит в Assets/Art/Models, ставим её
    // с Animator Controller, иначе капсулу с «носом», чтобы было видно, куда смотрит.
    static Transform Body(Transform root, string modelName, Material material, float height)
    {
        string modelPath = $"{ModelFolder}/{modelName}.fbx";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        // Контроллер пересобираем при каждой сборке сцены: могли добавиться новые клипы.
        // У объектов Unity оператор ?? не видит «уничтоженный» null, поэтому проверки явные.
        var controller = model != null ? ControllerFor(modelPath) : null;
        if (controller != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            var animator = instance.GetComponent<Animator>();
            if (animator == null)
                animator = instance.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            return instance.transform;
        }
        // Модель есть, а анимаций ещё нет: капсула лучше, чем персонаж, скользящий в T-позе.

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

    // На сцене шесть «Серых», а пересобрать контроллер достаточно один раз за команду меню.
    static readonly Dictionary<string, RuntimeAnimatorController> controllers = new();

    static RuntimeAnimatorController ControllerFor(string modelPath)
    {
        if (controllers.TryGetValue(modelPath, out var cached))
            return cached;
        if (controllers.Count == 0)
            EditorApplication.delayCall += controllers.Clear;
        var controller = AnimatorControllerBuilder.Build(modelPath);
        controllers[modelPath] = controller;
        return controller;
    }

    // ---------------------------------------------------------------- системы

    public static IsoCamera Camera(GameObject target, Color background)
    {
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 35f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = background;
        cameraObject.AddComponent<AudioListener>();
        var iso = cameraObject.AddComponent<IsoCamera>();
        if (target != null)
        {
            SetObject(iso, "target", target.transform);
            iso.Snap();
        }
        return iso;
    }

    public static void Navigation(Vector3 center, Vector3 size)
    {
        var nav = new GameObject("Навигация").AddComponent<RuntimeNavMesh>();
        nav.transform.position = center;
        Set(nav, "size", p => p.vector3Value = size);
    }

    public static GameManager GameSystems(ItemId objectiveItem, string before, string after)
    {
        var go = new GameObject("Игра");
        var manager = go.AddComponent<GameManager>();
        Set(manager, "objectiveItem", p => p.enumValueIndex = (int)objectiveItem);
        Set(manager, "objectiveBefore", p => p.stringValue = before);
        Set(manager, "objectiveAfter", p => p.stringValue = after);
        go.AddComponent<GameHUD>();
        go.AddComponent<GameMenus>();
        return manager;
    }

    // ---------------------------------------------------------------- объекты уровня

    public static Collectible Pickup(ItemId item, Vector3 position, Material material, int amount = 1)
    {
        var root = new GameObject($"Предмет: {Items.Name(item)}");
        root.transform.position = position;
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;
        trigger.center = new Vector3(0f, 0.5f, 0f);

        // Размеры из плана арта: бутылка 0,30 м, остальное примерно с ладонь.
        Vector3 size = item switch
        {
            ItemId.Water => new Vector3(0.1f, 0.3f, 0.1f),
            ItemId.Antidote => new Vector3(0.25f, 0.12f, 0.15f),
            _ => new Vector3(0.18f, 0.08f, 0.12f),
        };
        var visual = Block("Модель", Vector3.zero, size, material, root.transform);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        visual.transform.localPosition = new Vector3(0f, 0.5f, 0f);

        // Мягкий свет под предметом: так его видно и с высоты камеры, и в темноте.
        var glow = PointLight("Подсветка", position + Vector3.up * 0.4f, new Color(1f, 0.85f, 0.5f), 1.5f, 2f);
        glow.transform.SetParent(root.transform, true);

        var collectible = root.AddComponent<Collectible>();
        Set(collectible, "item", p => p.enumValueIndex = (int)item);
        Set(collectible, "amount", p => p.intValue = amount);
        SetObject(collectible, "visual", visual.transform);
        return collectible;
    }

    // Дверь: корень на петле в точке hinge, полотно уходит от петли в сторону direction.
    public static Door Door(Transform parent, Vector3 hinge, Vector3 direction, float width, float height, float openAngle, Material material)
    {
        var root = new GameObject("Дверь");
        root.transform.SetParent(parent, false);
        root.transform.position = hinge;

        direction.Normalize();
        var panel = Block("Полотно", hinge + direction * (width / 2f) + Vector3.up * (height / 2f), Vector3.one, material, root.transform);
        panel.transform.localScale = Mathf.Abs(direction.x) > 0.5f ? new Vector3(width, height, 0.08f) : new Vector3(0.08f, height, width);

        var door = root.AddComponent<Door>();
        Set(door, "openAngle", p => p.floatValue = openAngle);
        return door;
    }

    public static SearchContainer Crate(string name, Vector3 position, Material material, params (ItemId item, int amount)[] loot)
    {
        // Ящик 0,8×0,5×0,5 м, крышка отдельно, ось на заднем ребре.
        var root = new GameObject(name);
        root.transform.position = position;
        var box = Block("Корпус", position + new Vector3(0f, 0.22f, 0f), new Vector3(0.8f, 0.44f, 0.5f), material, root.transform);
        box.name = "Корпус";

        var hinge = new GameObject("Ось крышки").transform;
        hinge.SetParent(root.transform, false);
        hinge.localPosition = new Vector3(0f, 0.44f, 0.25f);
        var lid = Block("Крышка", Vector3.zero, new Vector3(0.82f, 0.06f, 0.52f), material, hinge);
        lid.transform.localPosition = new Vector3(0f, 0.03f, -0.26f);
        Object.DestroyImmediate(lid.GetComponent<Collider>());

        var container = root.AddComponent<SearchContainer>();
        Set(container, "lid", p => p.objectReferenceValue = hinge);
        Set(container, "contents", p =>
        {
            p.arraySize = loot.Length;
            for (int i = 0; i < loot.Length; i++)
            {
                var element = p.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("item").enumValueIndex = (int)loot[i].item;
                element.FindPropertyRelative("amount").intValue = loot[i].amount;
            }
        });
        return container;
    }

    public static void AddToBuildSettings(params (string path, bool enabled)[] scenes)
    {
        var list = new List<EditorBuildSettingsScene>();
        foreach (var (path, enabled) in scenes)
            if (File.Exists(path))
                list.Add(new EditorBuildSettingsScene(path, enabled));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---------------------------------------------------------------- поля скриптов

    // Поля скриптов закрытые ([SerializeField]), поэтому задаём их так же, как инспектор.
    public static void Set(Object component, string field, Action<SerializedProperty> assign)
    {
        var serialized = new SerializedObject(component);
        var property = serialized.FindProperty(field);
        if (property == null)
            throw new ArgumentException($"У {component.GetType().Name} нет поля {field}");
        assign(property);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetObject(Object component, string field, Object value) => Set(component, field, p => p.objectReferenceValue = value);

    public static void SetFloat(Object component, string field, float value) => Set(component, field, p => p.floatValue = value);

    public static void SetString(Object component, string field, string value) => Set(component, field, p => p.stringValue = value);

    public static void SetBool(Object component, string field, bool value) => Set(component, field, p => p.boolValue = value);
}
