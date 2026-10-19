using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static SceneBuildKit;

// Собирает сцены игры: главное меню, посёлок Сарыкудук и бункер «Подземка».
// Сюжет: в посёлке найти ключ от люка, спуститься в бункер, включить питание,
// забрать из лаборатории антидот «ПФ-2» и выбраться наверх.
public static class GameScenesBuilder
{
    const string MenuPath = "Assets/Scenes/MainMenu.unity";
    const string VillagePath = "Assets/Scenes/Village.unity";
    const string BunkerPath = "Assets/Scenes/Bunker.unity";
    const string TestPath = "Assets/Scenes/Test.unity";

    [MenuItem("Граница-99/Собрать все сцены игры", priority = 0)]
    static void BuildAll()
    {
        if (!EditorUtility.DisplayDialog("Сборка сцен", "Пересобрать MainMenu, Village и Bunker? Ручные правки в них пропадут.", "Пересобрать", "Отмена"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        BuildMenu();
        BuildBunker();
        BuildVillage();
        UpdateBuildSettings();
        Debug.Log("Сцены собраны. Открыт посёлок; для полной игры открой MainMenu и нажми Play.");
    }

    [MenuItem("Граница-99/Собрать отдельно/Главное меню", priority = 40)]
    static void MenuOnly()
    {
        if (ConfirmOverwrite(MenuPath)) { BuildMenu(); UpdateBuildSettings(); }
    }

    [MenuItem("Граница-99/Собрать отдельно/Посёлок", priority = 41)]
    static void VillageOnly()
    {
        if (ConfirmOverwrite(VillagePath)) { BuildVillage(); UpdateBuildSettings(); }
    }

    [MenuItem("Граница-99/Собрать отдельно/Бункер", priority = 42)]
    static void BunkerOnly()
    {
        if (ConfirmOverwrite(BunkerPath)) { BuildBunker(); UpdateBuildSettings(); }
    }

    // Меню первым: Unity запускает в сборке сцену с индексом 0.
    public static void UpdateBuildSettings()
    {
        AddToBuildSettings((MenuPath, true), (VillagePath, true), (BunkerPath, true), (TestPath, false));
    }

    // ---------------------------------------------------------------- главное меню

    static void BuildMenu()
    {
        var scene = NewScene();
        var ground = GetMaterial("Ground", new Color(0.72f, 0.63f, 0.45f));
        var wall = GetMaterial("Wall", new Color(0.85f, 0.82f, 0.74f));

        // Небольшой вид степного посёлка на фоне меню.
        Sun(1.1f, new Color(0.6f, 0.55f, 0.5f));
        Ground(80f, ground);
        Block("Дом", new Vector3(-6f, 1.5f, 10f), new Vector3(6f, 3f, 8f), wall);
        Block("Дом", new Vector3(5f, 1.5f, 16f), new Vector3(8f, 3f, 6f), wall);
        Block("Столб", new Vector3(1f, 3f, 6f), new Vector3(0.2f, 6f, 0.2f), wall);

        var camera = new GameObject("Main Camera");
        camera.tag = "MainCamera";
        var cam = camera.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.82f, 0.7f, 0.52f);
        cam.fieldOfView = 40f;
        camera.AddComponent<AudioListener>();
        camera.transform.position = new Vector3(0f, 4f, -8f);
        camera.transform.rotation = Quaternion.Euler(12f, 0f, 0f);

        new GameObject("Меню").AddComponent<MainMenuUI>();
        EditorSceneManager.SaveScene(scene, MenuPath);
    }

    // ---------------------------------------------------------------- посёлок

    static void BuildVillage()
    {
        var scene = NewScene();
        var ground = GetMaterial("Ground", new Color(0.72f, 0.63f, 0.45f));
        var wall = GetMaterial("Wall", new Color(0.85f, 0.82f, 0.74f));
        var wood = GetMaterial("Box", new Color(0.45f, 0.33f, 0.2f));
        var metal = GetMaterial("Metal", new Color(0.3f, 0.32f, 0.33f));
        var concrete = GetMaterial("Concrete", new Color(0.55f, 0.55f, 0.52f));
        var player = GetMaterial("Player", new Color(0.25f, 0.4f, 0.6f));
        var grey = GetMaterial("Grey", new Color(0.45f, 0.5f, 0.45f));
        var dust = GetMaterial("Danger", new Color(0.85f, 0.75f, 0.2f));
        var item = GetMaterial("Item", new Color(0.3f, 0.7f, 0.9f));

        Sun(1.2f, new Color(0.55f, 0.53f, 0.55f));
        Ground(64f, ground);

        // Забор по краю карты 60×60 м, чтобы нельзя было уйти в пустоту.
        var fence = new GameObject("Забор").transform;
        Wall(fence, -30f, -30f, 30f, -30f, 1.5f, wood);
        Wall(fence, -30f, 30f, 30f, 30f, 1.5f, wood);
        Wall(fence, -30f, -30f, -30f, 30f, 1.5f, wood);
        Wall(fence, 30f, -30f, 30f, 30f, 1.5f, wood);

        // Закрытые дома (пока сплошные блоки, потом их заменят модули стен из плана арта).
        Block("Дом", new Vector3(-14f, 1.5f, 8f), new Vector3(6f, 3f, 8f), wall);
        Block("Дом", new Vector3(14f, 1.5f, 12f), new Vector3(8f, 3f, 6f), wall);
        Block("Дом", new Vector3(14f, 1.5f, -8f), new Vector3(6f, 3f, 8f), wall);
        Block("Дом", new Vector3(-14f, 1.5f, -12f), new Vector3(6f, 3f, 6f), wall);

        // Дом Толика: 8×6 м, без крыши, чтобы с камеры было видно, что внутри.
        // Проём двери 1,2 м в южной стене, внутри ящик с ключом от люка.
        var house = new GameObject("Дом Толика").transform;
        Wall(house, -4f, 17f, 4f, 17f, 3f, wall);
        Wall(house, -4f, 11f, -4f, 17f, 3f, wall);
        Wall(house, 4f, 11f, 4f, 17f, 3f, wall);
        Wall(house, -4f, 11f, -0.75f, 11f, 3f, wall);
        Wall(house, 0.75f, 11f, 4f, 11f, 3f, wall);
        // Полотно идёт от петли вдоль +X, поворот на −100° открывает дверь внутрь дома.
        Door(house, new Vector3(-0.6f, 0f, 11f), Vector3.right, 1.2f, 2f, -100f, wood);
        Block("Стол", new Vector3(-2.5f, 0.4f, 15.5f), new Vector3(1.2f, 0.8f, 0.7f), wood, house);
        Crate("Сундук Толика", new Vector3(2.5f, 0f, 15.8f), wood, (ItemId.BunkerKey, 1), (ItemId.Batteries, 1));

        BuildWell(new Vector3(0f, 0f, 4f), concrete, wood);

        Crate("Ящик у дороги", new Vector3(-7f, 0f, -2f), wood, (ItemId.Water, 1));
        Crate("Ящик за домом", new Vector3(8f, 0f, 3f), wood, (ItemId.Batteries, 1));

        Pickup(ItemId.Water, new Vector3(-20f, 0f, 8f), item);
        Pickup(ItemId.Batteries, new Vector3(6f, 0f, -14f), item);
        Pickup(ItemId.Water, new Vector3(20f, 0f, 4f), item);

        DustZone(new Vector3(-20f, 0f, 22f), 8f, dust);

        // Люк «Подземки» на юге посёлка.
        var hatch = Block("Люк «Подземки»", new Vector3(0f, 0.1f, -24f), new Vector3(1.2f, 0.2f, 1.2f), metal);
        Block("Бетонное кольцо", new Vector3(0f, 0.05f, -24f), new Vector3(2f, 0.1f, 2f), concrete);
        var exit = hatch.AddComponent<LevelExit>();
        SetString(exit, "prompt", "Спуститься в «Подземку»");
        SetBool(exit, "needsItem", true);
        Set(exit, "requiredItem", p => p.enumValueIndex = (int)ItemId.BunkerKey);
        SetString(exit, "missingMessage", "Люк заперт на замок. Ключ был у Толика");
        SetString(exit, "nextScene", SceneNames.Bunker);

        Navigation(Vector3.zero, new Vector3(64f, 20f, 64f));

        var playerObject = Player(new Vector3(0f, 0f, -6f), player, true);

        Grey(new Vector3(-8f, 0f, 22f), grey, 1);
        Grey(new Vector3(20f, 0f, 22f), grey, 2);
        Grey(new Vector3(22f, 0f, -2f), grey, 3);
        Grey(new Vector3(-22f, 0f, 0f), grey, 4);
        Grey(new Vector3(6f, 0f, -20f), grey, 5);
        Grey(new Vector3(-7f, 0f, -21f), grey, 6);

        Camera(playerObject, new Color(0.78f, 0.72f, 0.6f));
        GameSystems(ItemId.BunkerKey,
            "Найди ключ от люка «Подземки». Толик жил в доме на севере",
            "Спустись в люк «Подземки» на юге посёлка");

        EditorSceneManager.SaveScene(scene, VillagePath);
    }

    static void BuildWell(Vector3 position, Material stone, Material wood)
    {
        // Кольцо Ø 1,2 м, высота 0,8 м, ворот на высоте 1,8 м, как в плане арта.
        var root = new GameObject("Колодец");
        root.transform.position = position;

        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "Кольцо";
        ring.transform.SetParent(root.transform, false);
        // Цилиндр Unity высотой 2 м, поэтому по Y масштаб 0,4.
        ring.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        ring.transform.localScale = new Vector3(1.2f, 0.4f, 1.2f);
        ring.GetComponent<Renderer>().sharedMaterial = stone;

        Block("Стойка", position + new Vector3(-0.7f, 1f, 0f), new Vector3(0.1f, 2f, 0.1f), wood, root.transform);
        Block("Стойка", position + new Vector3(0.7f, 1f, 0f), new Vector3(0.1f, 2f, 0.1f), wood, root.transform);

        // Ворот: пустой объект без поворота, внутри него лежачий цилиндр.
        // Крутится пустой объект вокруг своей оси X, то есть вдоль цилиндра.
        var crank = new GameObject("Ворот").transform;
        crank.SetParent(root.transform, false);
        crank.localPosition = new Vector3(0f, 1.8f, 0f);
        var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(log.GetComponent<Collider>());
        log.transform.SetParent(crank, false);
        log.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        log.transform.localScale = new Vector3(0.12f, 0.7f, 0.12f);
        log.GetComponent<Renderer>().sharedMaterial = wood;

        var well = root.AddComponent<Well>();
        SetObject(well, "crank", crank);
    }

    // ---------------------------------------------------------------- бункер

    static void BuildBunker()
    {
        var scene = NewScene();
        var floorMat = GetMaterial("Concrete", new Color(0.55f, 0.55f, 0.52f));
        var wall = GetMaterial("BunkerWall", new Color(0.42f, 0.46f, 0.42f));
        var metal = GetMaterial("Metal", new Color(0.3f, 0.32f, 0.33f));
        var wood = GetMaterial("Box", new Color(0.45f, 0.33f, 0.2f));
        var player = GetMaterial("Player", new Color(0.25f, 0.4f, 0.6f));
        var grey = GetMaterial("Grey", new Color(0.45f, 0.5f, 0.45f));
        var item = GetMaterial("Item", new Color(0.3f, 0.7f, 0.9f));
        var antidote = GetMaterial("Antidote", new Color(0.3f, 0.9f, 0.4f));

        // Солнца нет, рассеянный свет почти чёрный: без фонарика видно только аварийные лампы.
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.03f, 0.03f, 0.04f);

        var floor = Ground(40f, floorMat);
        floor.transform.position = new Vector3(0f, 0f, 11f);

        // План (x, z), стены высотой 2,6 м:
        //   стартовая комната −3..3 × −3..3, лестница наверх у южной стены;
        //   коридор −1,5..1,5 × 3..18;
        //   зал −4..4 × 18..26;
        //   генераторная −14..−4 × 18..26 (рубильник), лаборатория 4..14 × 18..26 (антидот).
        const float h = 2.6f;
        var walls = new GameObject("Стены").transform;
        Wall(walls, -3f, -3f, 3f, -3f, h, wall);
        Wall(walls, -3f, -3f, -3f, 3f, h, wall);
        Wall(walls, 3f, -3f, 3f, 3f, h, wall);
        Wall(walls, -3f, 3f, -1.5f, 3f, h, wall);
        Wall(walls, 1.5f, 3f, 3f, 3f, h, wall);

        Wall(walls, -1.5f, 3f, -1.5f, 18f, h, wall);
        Wall(walls, 1.5f, 3f, 1.5f, 18f, h, wall);

        Wall(walls, -4f, 18f, -1.5f, 18f, h, wall);
        Wall(walls, 1.5f, 18f, 4f, 18f, h, wall);
        Wall(walls, -4f, 26f, 4f, 26f, h, wall);
        Wall(walls, -4f, 18f, -4f, 21f, h, wall);
        Wall(walls, -4f, 23f, -4f, 26f, h, wall);
        Wall(walls, 4f, 18f, 4f, 21.25f, h, wall);
        Wall(walls, 4f, 22.75f, 4f, 26f, h, wall);

        Wall(walls, -14f, 18f, -4f, 18f, h, wall);
        Wall(walls, -14f, 26f, -4f, 26f, h, wall);
        Wall(walls, -14f, 18f, -14f, 26f, h, wall);

        Wall(walls, 4f, 18f, 14f, 18f, h, wall);
        Wall(walls, 4f, 26f, 14f, 26f, h, wall);
        Wall(walls, 14f, 18f, 14f, 26f, h, wall);

        // Гермодверь 1,2 × 2 м между залом и лабораторией. Заперта, пока нет питания.
        // Полотно идёт от петли вдоль +Z, поворот на +95° открывает её в лабораторию.
        var door = Door(walls, new Vector3(4f, 0f, 21.4f), Vector3.forward, 1.2f, 2f, 95f, metal);
        door.name = "Гермодверь";
        SetString(door, "doorName", "гермодверь");
        SetBool(door, "locked", true);
        SetString(door, "lockedMessage", "Гермодверь не открывается. Нет питания");

        // Аварийный свет горит всегда, основной включается рубильником.
        var red = new Color(1f, 0.25f, 0.2f);
        PointLight("Аварийная лампа", new Vector3(0f, 2.2f, 0f), new Color(1f, 0.9f, 0.7f), 1.2f, 6f);
        PointLight("Аварийная лампа", new Vector3(0f, 2.2f, 9f), red, 1.5f, 6f);
        PointLight("Аварийная лампа", new Vector3(0f, 2.2f, 16f), red, 1.5f, 6f);
        PointLight("Аварийная лампа", new Vector3(-9f, 2.2f, 22f), red, 1.5f, 7f);
        var hallLamp = PointLight("Лампа зала", new Vector3(0f, 2.3f, 22f), Color.white, 3f, 9f);
        var labLamp1 = PointLight("Лампа лаборатории", new Vector3(7f, 2.3f, 22f), Color.white, 3f, 8f);
        var labLamp2 = PointLight("Лампа лаборатории", new Vector3(11f, 2.3f, 22f), Color.white, 3f, 8f);

        BuildLever(new Vector3(-13.6f, 0f, 22f), metal, door, hallLamp, labLamp1, labLamp2);

        // Лестница наверх: выход с уровня, но только с антидотом.
        var ladder = Block("Лестница наверх", new Vector3(0f, 1.5f, -2.7f), new Vector3(0.8f, 3f, 0.1f), metal);
        var exit = ladder.AddComponent<LevelExit>();
        SetString(exit, "prompt", "Подняться по лестнице наверх");
        SetBool(exit, "needsItem", true);
        Set(exit, "requiredItem", p => p.enumValueIndex = (int)ItemId.Antidote);
        SetString(exit, "missingMessage", "Без антидота наверх возвращаться незачем");
        SetString(exit, "nextScene", "");

        Block("Лабораторный стол", new Vector3(12f, 0.45f, 22f), new Vector3(1.2f, 0.9f, 2.5f), metal);
        // Ближе к краю стола, чтобы игрок дотягивался до предмета, не залезая на стол.
        Pickup(ItemId.Antidote, new Vector3(11.5f, 0.4f, 22f), antidote);
        Pickup(ItemId.Batteries, new Vector3(-12f, 0f, 20f), item);
        Pickup(ItemId.Water, new Vector3(-0.8f, 0f, 8f), item);
        Crate("Ящик с припасами", new Vector3(7f, 0f, 25.2f), wood, (ItemId.Water, 1));

        Navigation(new Vector3(0f, 0f, 11f), new Vector3(40f, 10f, 40f));

        var playerObject = Player(new Vector3(0f, 0f, -1f), player, true);
        playerObject.transform.rotation = Quaternion.identity;

        Grey(new Vector3(0f, 0f, 12f), grey, 1);
        Grey(new Vector3(2f, 0f, 24f), grey, 2);
        Grey(new Vector3(-10f, 0f, 23.5f), grey, 3);
        Grey(new Vector3(10f, 0f, 24.5f), grey, 4);

        Camera(playerObject, Color.black);
        GameSystems(ItemId.Antidote,
            "Найди антидот «ПФ-2» в лаборатории. Гермодвери нужно питание",
            "Вернись к лестнице и выберись наверх");

        EditorSceneManager.SaveScene(scene, BunkerPath);
    }

    static void BuildLever(Vector3 position, Material metal, Door door, params Light[] lamps)
    {
        var root = new GameObject("Рубильник");
        root.transform.position = position;
        Block("Щиток", position + new Vector3(0f, 1.2f, 0f), new Vector3(0.2f, 0.6f, 0.4f), metal, root.transform);

        var handle = new GameObject("Ручка").transform;
        handle.SetParent(root.transform, false);
        handle.localPosition = new Vector3(0.15f, 1.3f, 0f);
        var stick = Block("Рычаг", Vector3.zero, new Vector3(0.06f, 0.4f, 0.06f), metal, handle);
        Object.DestroyImmediate(stick.GetComponent<Collider>());
        stick.transform.localPosition = new Vector3(0f, 0.2f, 0f);

        var lever = root.AddComponent<Lever>();
        SetObject(lever, "door", door);
        SetObject(lever, "handle", handle);
        Set(lever, "lamps", p =>
        {
            p.arraySize = lamps.Length;
            for (int i = 0; i < lamps.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = lamps[i];
        });
    }
}
