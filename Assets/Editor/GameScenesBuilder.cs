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

    // Карта 140 × 140 м (x, z от −70 до 70):
    //   Сарыкудук в центре: дома, колодец, дом Толика (можно зайти), игрок появляется здесь;
    //   колхоз «Заря» на северо-востоке за забором: Ерлан, Галя, Ким, Бахтияр;
    //   насосная станция на юго-востоке: записка на узбекском;
    //   люк «Подземки» на юге; трасса с брошенными машинами с запада на восток.
    static void BuildVillage()
    {
        var scene = NewScene();
        var rnd = new System.Random(99);

        var sun = Sun(1.2f, new Color(0.55f, 0.53f, 0.55f));
        Clock(sun);
        Ground(150f, PropKit.M("Ground"));

        // Граница карты: высокий глухой забор, чтобы нельзя было уйти в пустоту.
        var border = new GameObject("Граница").transform;
        Wall(border, -70f, -70f, 70f, -70f, 2f, PropKit.M("DarkWood"));
        Wall(border, -70f, 70f, 70f, 70f, 2f, PropKit.M("DarkWood"));
        Wall(border, -70f, -70f, -70f, 70f, 2f, PropKit.M("DarkWood"));
        Wall(border, 70f, -70f, 70f, 70f, 2f, PropKit.M("DarkWood"));

        BuildRoads();
        BuildSarykuduk(rnd);
        BuildZarya();
        BuildPumpStation();
        BuildHatch();
        BuildBanditCamp();
        BuildNature(rnd);

        PropKit.PowerLine(new Vector3(-66f, 0f, -13f), new Vector3(66f, 0f, -13f), 14f);
        PropKit.PowerLine(new Vector3(8f, 0f, -13f), new Vector3(36f, 0f, 22f), 13f);

        // Брошенные на трассе машины: металлолом и вода.
        PropKit.CarWreck("Москвич", new Vector3(-38f, 0f, -9f), 84f, "CarBlue");
        PropKit.CarWreck("УАЗ", new Vector3(-24f, 0f, -6.5f), 100f, "CarGreen");
        PropKit.CarWreck("Жигули", new Vector3(30f, 0f, -10f), 260f, "CarBlue");
        Crate("Багажник «Москвича»", new Vector3(-38f, 0f, -11.8f), PropKit.M("Box"), "trunk", ("scrap", 1));
        Crate("Ящик у УАЗа", new Vector3(-24f, 0f, -3.8f), PropKit.M("Box"), "trunk", ("scrap", 1), ("water_clean", 1));
        Pickup(ItemId.Scrap, new Vector3(31.5f, 0f, -6.8f), PropKit.M("Metal"));

        DustZone(new Vector3(-30f, 0f, 30f), 10f, PropKit.M("Danger"));
        DustZone(new Vector3(20f, 0f, -45f), 8f, PropKit.M("Danger"));

        Navigation(Vector3.zero, new Vector3(150f, 20f, 150f));

        var grey = PropKit.M("Grey");
        var greys = new[]
        {
            new Vector3(-12f, 0f, 24f), new Vector3(18f, 0f, 18f), new Vector3(22f, 0f, -2f), new Vector3(-26f, 0f, 4f),
            new Vector3(-40f, 0f, -20f), new Vector3(-50f, 0f, 30f), new Vector3(10f, 0f, 45f), new Vector3(40f, 0f, -28f),
            new Vector3(54f, 0f, -46f), new Vector3(-8f, 0f, -46f), new Vector3(8f, 0f, -50f), new Vector3(-30f, 0f, -42f),
        };
        for (int i = 0; i < greys.Length; i++)
            Grey(greys[i], grey, i + 1);

        var playerObject = Player(new Vector3(0f, 0f, -4f), PropKit.M("Player"), true);
        Camera(playerObject, new Color(0.78f, 0.72f, 0.6f));
        var manager = GameSystems(ItemId.BunkerKey,
            "Найди ключ от люка «Подземки»",
            "Спустись в люк «Подземки» на юге посёлка");
        SetBool(manager, "storyObjective", true);

        EditorSceneManager.SaveScene(scene, VillagePath);
    }

    static void BuildRoads()
    {
        PropKit.Road(new Vector3(-70f, 0f, -9f), new Vector3(70f, 0f, -9f), 7f);
        // Грунтовки: от трассы к «Заре», к насосной, к люку и к западным домам.
        PropKit.Road(new Vector3(6f, 0f, -9f), new Vector3(40f, 0f, 26f), 3.5f);
        PropKit.Road(new Vector3(30f, 0f, -9f), new Vector3(45f, 0f, -36f), 3f);
        PropKit.Road(new Vector3(0f, 0f, -9f), new Vector3(0f, 0f, -55f), 3f);
        PropKit.Road(new Vector3(-20f, 0f, -9f), new Vector3(-6f, 0f, 20f), 3f);
    }

    static void BuildSarykuduk(System.Random rnd)
    {
        var wall = PropKit.M("Wall");
        var wood = PropKit.M("Box");

        PropKit.House("Дом", new Vector3(-14f, 0f, 8f), 90f, 8f, 6f);
        PropKit.House("Дом", new Vector3(14f, 0f, 9f), 0f, 8f, 6f, "WallBlue");
        PropKit.House("Дом", new Vector3(14f, 0f, -24f), 180f, 7f, 6f, "WallBlue", "Slate");
        PropKit.House("Дом", new Vector3(-17f, 0f, -24f), 180f, 8f, 6f);
        PropKit.House("Дом", new Vector3(-28f, 0f, 14f), 90f, 7f, 6f, "Wall", "Slate");
        PropKit.House("Магазин «Продукты»", new Vector3(-5f, 0f, -22f), 180f, 10f, 7f, "Brick", "Slate");

        // Дом Толика: 8×6 м, можно зайти. Проём 1,2 м в южной стене, крыша прячется, пока игрок внутри.
        var house = new GameObject("Дом Толика").transform;
        Wall(house, -4f, 17f, 4f, 17f, 3f, wall);
        Wall(house, -4f, 11f, -4f, 17f, 3f, wall);
        Wall(house, 4f, 11f, 4f, 17f, 3f, wall);
        Wall(house, -4f, 11f, -0.75f, 11f, 3f, wall);
        Wall(house, 0.75f, 11f, 4f, 11f, 3f, wall);
        PropKit.Window(house, new Vector3(-2.4f, 1.6f, 11f - 0.16f), 0f);
        PropKit.Window(house, new Vector3(2.4f, 1.6f, 11f - 0.16f), 0f);
        PropKit.Window(house, new Vector3(4.16f, 1.6f, 14f), 90f);
        var floor = Block("Пол", new Vector3(0f, 0.02f, 14f), new Vector3(7.7f, 0.04f, 5.7f), PropKit.M("DarkWood"), house);
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        // Полотно идёт от петли вдоль +X, поворот на −100° открывает дверь внутрь дома.
        Door(house, new Vector3(-0.6f, 0f, 11f), Vector3.right, 1.2f, 2f, -100f, wood);
        PropKit.HidingRoof(house, new Vector3(0f, 0f, 14f), 8f, 6f, 3f, "Roof");
        Block("Стол", new Vector3(-2.5f, 0.4f, 15.5f), new Vector3(1.2f, 0.8f, 0.7f), wood, house);
        PropKit.Bench(new Vector3(-2.5f, 0f, 14.6f), 180f).SetParent(house, true);
        // Кровать Толика: на ней можно выспаться [E].
        Block("Кровать", new Vector3(3f, 0.3f, 12.8f), new Vector3(1.8f, 0.6f, 0.9f), PropKit.M("Frame"), house).AddComponent<Bed>();
        Block("Шкаф", new Vector3(-3.5f, 1f, 12.2f), new Vector3(0.6f, 2f, 1.2f), PropKit.M("DarkWood"), house);
        Crate("Сундук Толика", new Vector3(2.5f, 0f, 15.8f), wood, "home", ("scrap", 1), ("batteries", 1));
        Crate("Кухонный шкаф Толика", new Vector3(-3.4f, 0f, 15.9f), wood, "kitchen");

        BuildWell(new Vector3(0f, 0f, 4f), PropKit.M("Concrete"), wood);
        PropKit.Bench(new Vector3(3f, 0f, 4f), -90f);
        PropKit.Tree(new Vector3(-5f, 0f, 6f), 1f, rnd);
        PropKit.Tree(new Vector3(6f, 0f, 1f), 0.9f, rnd);

        // Огороды, стог и бочки возле домов.
        var fence = new GameObject("Заборы Сарыкудука").transform;
        PropKit.Fence(fence, new Vector3(-19f, 0f, 2f), new Vector3(-9f, 0f, 2f), (4f, 6f));
        PropKit.Fence(fence, new Vector3(9f, 0f, 3f), new Vector3(19f, 0f, 3f), (4f, 6f));
        PropKit.Haystack(new Vector3(-22f, 0f, 2f));
        PropKit.Barrel(new Vector3(18f, 0f, 5f));
        PropKit.Barrel(new Vector3(18.7f, 0f, 5.6f), "Metal");

        Crate("Ящик у дороги", new Vector3(-7f, 0f, -2f), wood, "crate", ("water_clean", 1));
        Crate("Ящик за домом", new Vector3(9f, 0f, 14f), wood, "fridge", ("batteries", 1));
        Crate("Прилавок магазина", new Vector3(-5f, 0f, -17.6f), wood, "shop");
        Crate("Ящик у сарая", new Vector3(-24f, 0f, 10f), wood, "home");
        Crate("Аптечка на стене", new Vector3(18f, 0f, -20f), PropKit.M("Frame"), "medical");
        Pickup(ItemId.Water, new Vector3(-20f, 0f, 12f), PropKit.M("Item"));
        Pickup(ItemId.Batteries, new Vector3(6f, 0f, -16f), PropKit.M("Item"));
        Pickup(ItemId.Water, new Vector3(20f, 0f, 3f), PropKit.M("Item"));
    }

    static void BuildZarya()
    {
        // Колхоз «Заря»: участок x 26..62, z 26..60 за забором, ворота в южной стороне.
        var zarya = new GameObject("Колхоз «Заря»").transform;
        PropKit.Fence(zarya, new Vector3(26f, 0f, 26f), new Vector3(62f, 0f, 26f), (12f, 18f));
        PropKit.Fence(zarya, new Vector3(26f, 0f, 60f), new Vector3(62f, 0f, 60f));
        PropKit.Fence(zarya, new Vector3(26f, 0f, 26f), new Vector3(26f, 0f, 60f));
        PropKit.Fence(zarya, new Vector3(62f, 0f, 26f), new Vector3(62f, 0f, 60f));
        var gate = PropKit.Root("Ворота", new Vector3(41f, 0f, 26f), 0f, zarya);
        PropKit.Part(gate, "Столб ворот", PrimitiveType.Cube, new Vector3(-3f, 1.3f, 0f), new Vector3(0.25f, 2.6f, 0.25f), "DarkWood");
        PropKit.Part(gate, "Столб ворот", PrimitiveType.Cube, new Vector3(3f, 1.3f, 0f), new Vector3(0.25f, 2.6f, 0.25f), "DarkWood");
        PropKit.Part(gate, "Вывеска «Заря»", PrimitiveType.Cube, new Vector3(0f, 2.7f, 0f), new Vector3(6.4f, 0.5f, 0.1f), "AwningRed", collider: false);

        PropKit.House("Контора колхоза", new Vector3(36f, 0f, 51f), 0f, 11f, 7f, "Brick", "Slate");
        PropKit.House("Дом", new Vector3(54f, 0f, 53f), 0f, 7f, 6f);
        // Мастерская Кима: в ней верстак для рецептов буржуйки и сборщика воды.
        PropKit.Shed("Мастерская Кима", new Vector3(56f, 0f, 36f), 90f, 7f, 5f).gameObject.AddComponent<Workbench>();
        Crate("Ящик Кима", new Vector3(57f, 0f, 39f), PropKit.M("Box"), "crate");
        Crate("Аптечка конторы", new Vector3(32f, 0f, 46.8f), PropKit.M("Frame"), "medical");
        Crate("Шкаф с книгами", new Vector3(40f, 0f, 46.8f), PropKit.M("DarkWood"), "books");
        Crate("Сундук в юрте", new Vector3(55f, 0f, 49.5f), PropKit.M("DarkWood"), "yurt");
        PropKit.Tractor("Трактор «Беларусь»", new Vector3(49f, 0f, 30.5f), 30f);
        PropKit.Stall("Прилавок тёти Гали", new Vector3(36f, 0f, 35.5f), 0f);
        // Дверь юрты смотрит на запад, к центру колхоза.
        PropKit.Yurt("Юрта Бахтияра", new Vector3(57.5f, 0f, 46f), 90f);
        PropKit.WaterTower("Водонапорная башня", new Vector3(30f, 0f, 56f));
        PropKit.Campfire(new Vector3(44f, 0f, 41f));
        PropKit.Bench(new Vector3(44f, 0f, 43f), 180f);
        PropKit.Bench(new Vector3(46.5f, 0f, 41f), -90f);

        // Отравленный колодец «Зари»: заколочен, о нём рассказывает Ерлан.
        var bad = PropKit.Root("Отравленный колодец", new Vector3(31f, 0f, 41f), 0f, zarya);
        PropKit.Part(bad, "Кольцо", PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.4f, 1.2f), "Concrete");
        PropKit.Part(bad, "Доска", PrimitiveType.Cube, new Vector3(0f, 0.82f, 0f), new Vector3(1.4f, 0.05f, 0.15f), "Box", new Vector3(0f, 45f, 0f), false);
        PropKit.Part(bad, "Доска", PrimitiveType.Cube, new Vector3(0f, 0.84f, 0f), new Vector3(1.4f, 0.05f, 0.15f), "Box", new Vector3(0f, -45f, 0f), false);

        PropKit.Resident("Ерлан-ага", "председатель", "erlan", new Vector3(36f, 0f, 46f), 180f,
            Rgb(92, 70, 120), Rgb(50, 45, 40), Rgb(190, 140, 100), PropKit.Hat.Kalpak);
        PropKit.Resident("Тётя Галя", "торговка", "galya", new Vector3(36f, 0f, 36.5f), 180f,
            Rgb(150, 60, 60), Rgb(60, 50, 60), Rgb(225, 180, 150), PropKit.Hat.Scarf);
        PropKit.Resident("Виктор Ким", "механик", "kim", new Vector3(53f, 0f, 36f), -90f,
            Rgb(60, 80, 110), Rgb(60, 80, 110), Rgb(215, 175, 130), PropKit.Hat.Cap);
        PropKit.Resident("Бахтияр", "переводчик", "bakhtiyar", new Vector3(53.5f, 0f, 46f), -90f,
            Rgb(210, 205, 190), Rgb(70, 60, 50), Rgb(185, 135, 95), PropKit.Hat.Doppi);

        Pickup(ItemId.Scrap, new Vector3(58f, 0f, 30f), PropKit.M("Metal"));
    }

    static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f);

    static void BuildPumpStation()
    {
        var root = PropKit.Root("Насосная станция", new Vector3(46f, 0f, -40f), 20f);
        PropKit.Part(root, "Здание", PrimitiveType.Cube, new Vector3(0f, 1.75f, 0f), new Vector3(6f, 3.5f, 5f), "Brick");
        PropKit.Part(root, "Крыша", PrimitiveType.Cube, new Vector3(0f, 3.6f, 0f), new Vector3(6.4f, 0.2f, 5.4f), "Concrete", collider: false);
        PropKit.Part(root, "Дверь", PrimitiveType.Cube, new Vector3(-1.5f, 1f, -2.53f), new Vector3(1f, 2f, 0.06f), "Rust", collider: false);
        PropKit.Window(root, new Vector3(1.4f, 2f, -2.51f), 0f);
        PropKit.Part(root, "Бак", PrimitiveType.Cylinder, new Vector3(5f, 1.5f, 1f), new Vector3(2.2f, 1.5f, 2.2f), "Metal");
        PropKit.Part(root, "Труба", PrimitiveType.Cylinder, new Vector3(3.5f, 0.6f, 1f), new Vector3(0.35f, 1f, 0.35f), "Rust", new Vector3(0f, 0f, 90f));
        PropKit.Part(root, "Труба", PrimitiveType.Cylinder, new Vector3(-5f, 0.4f, 0f), new Vector3(0.35f, 2f, 0.35f), "Rust", new Vector3(0f, 0f, 90f));
        PropKit.Part(root, "Вентиль", PrimitiveType.Cylinder, new Vector3(-3.3f, 0.8f, 0f), new Vector3(0.5f, 0.04f, 0.5f), "AwningRed", new Vector3(0f, 0f, 90f), false);
        PropKit.Barrel(root.TransformPoint(new Vector3(2.5f, 0f, -3.3f)));
        PropKit.Barrel(root.TransformPoint(new Vector3(3.2f, 0f, -3.6f)));
        PropKit.Barrel(root.TransformPoint(new Vector3(2.8f, 0f, -4.3f)), "Danger");

        var crate = Crate("Ящик у насосной", root.TransformPoint(new Vector3(-1f, 0f, -3.6f)), PropKit.M("Box"), "pump", ("note_uz", 1), ("scrap", 1));
        Set(crate, "storyEffects", p =>
        {
            p.arraySize = 2;
            p.GetArrayElementAtIndex(0).stringValue = "quest:well=2";
            p.GetArrayElementAtIndex(1).stringValue = "objective:Показать записку Бахтияру в «Заре»";
        });
        // Следы машин к станции.
        PropKit.Road(root.TransformPoint(new Vector3(-1f, 0f, -12f)), root.TransformPoint(new Vector3(-1f, 0f, -4.5f)), 2.2f);
    }

    static void BuildHatch()
    {
        // Люк «Подземки»: бетонное кольцо, крышка со штурвалом, вентиляционная труба, табличка.
        var root = PropKit.Root("Люк «Подземки»", new Vector3(0f, 0f, -58f));
        PropKit.Part(root, "Бетонное кольцо", PrimitiveType.Cylinder, new Vector3(0f, 0.1f, 0f), new Vector3(2.4f, 0.1f, 2.4f), "Concrete");
        var lid = PropKit.Part(root, "Крышка люка", PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(1.3f, 0.05f, 1.3f), "Metal");
        PropKit.Part(root, "Штурвал", PrimitiveType.Cylinder, new Vector3(0f, 0.35f, 0f), new Vector3(0.7f, 0.02f, 0.7f), "AwningRed", collider: false);
        PropKit.Part(root, "Спица", PrimitiveType.Cube, new Vector3(0f, 0.36f, 0f), new Vector3(0.7f, 0.03f, 0.05f), "AwningRed", collider: false);
        PropKit.Part(root, "Спица", PrimitiveType.Cube, new Vector3(0f, 0.36f, 0f), new Vector3(0.05f, 0.03f, 0.7f), "AwningRed", collider: false);
        PropKit.Part(root, "Вентиляция", PrimitiveType.Cylinder, new Vector3(2.4f, 1f, 0.5f), new Vector3(0.4f, 1f, 0.4f), "Metal");
        PropKit.Part(root, "Колпак", PrimitiveType.Cylinder, new Vector3(2.4f, 2.05f, 0.5f), new Vector3(0.6f, 0.05f, 0.6f), "Metal", collider: false);
        PropKit.Part(root, "Табличка", PrimitiveType.Cube, new Vector3(-2f, 1.4f, 1f), new Vector3(0.8f, 0.5f, 0.05f), "Danger", collider: false);
        PropKit.Part(root, "Стойка таблички", PrimitiveType.Cube, new Vector3(-2f, 0.6f, 1f), new Vector3(0.06f, 1.2f, 0.06f), "Metal");
        var fence = new GameObject("Ограда люка").transform;
        PropKit.Fence(fence, new Vector3(-5f, 0f, -63f), new Vector3(5f, 0f, -63f));
        PropKit.Fence(fence, new Vector3(-5f, 0f, -63f), new Vector3(-5f, 0f, -53f), (6f, 10f));
        PropKit.Fence(fence, new Vector3(5f, 0f, -63f), new Vector3(5f, 0f, -53f), (6f, 10f));

        var exit = lid.AddComponent<LevelExit>();
        SetString(exit, "prompt", "Спуститься в «Подземку»");
        SetBool(exit, "needsItem", true);
        Set(exit, "requiredItem", p => p.enumValueIndex = (int)ItemId.BunkerKey);
        SetString(exit, "missingMessage", "Люк заперт на замок. Ключ, говорят, у Ерлана в «Заре»");
        SetString(exit, "nextScene", SceneNames.Bunker);
    }

    // Лагерь «Волков степи» на западе у разбитых машин, как в слухах тёти Гали:
    // костёр, две палатки, ящик с добычей и трое бандитов с ПМ.
    static void BuildBanditCamp()
    {
        var camp = PropKit.Root("Лагерь «Волков степи»", new Vector3(-52f, 0f, -29f));
        PropKit.Campfire(new Vector3(-52f, 0f, -29f));
        foreach (var (x, z, yaw) in new[] { (-57f, -25f, 20f), (-47f, -25f, -15f) })
        {
            var tent = PropKit.Root("Палатка", new Vector3(x, 0f, z), yaw, camp);
            PropKit.Part(tent, "Скат", PrimitiveType.Cube, new Vector3(-0.7f, 0.75f, 0f), new Vector3(1.7f, 0.05f, 2.6f), "CarGreen", new Vector3(0f, 0f, 55f));
            PropKit.Part(tent, "Скат", PrimitiveType.Cube, new Vector3(0.7f, 0.75f, 0f), new Vector3(1.7f, 0.05f, 2.6f), "CarGreen", new Vector3(0f, 0f, -55f));
        }
        PropKit.CarWreck("Разбитый «Газик»", new Vector3(-58f, 0f, -35f), 40f, "CarGreen");
        PropKit.Barrel(new Vector3(-49f, 0f, -33f));
        PropKit.Barrel(new Vector3(-48.3f, 0f, -33.6f), "Metal");
        Crate("Ящик бандитов", new Vector3(-52f, 0f, -24f), PropKit.M("Box"), "bandit", ("ammo_9", 6));

        var bandit = GetMaterial("Bandit", new Color(0.3f, 0.28f, 0.25f));
        Bandit(new Vector3(-50f, 0f, -31f), bandit, 1);
        Bandit(new Vector3(-55f, 0f, -29f), bandit, 2);
        Bandit(new Vector3(-47f, 0f, -28f), bandit, 3);
    }

    static void BuildNature(System.Random rnd)
    {
        // Деревья и саксаул случайно, но с постоянным зерном: пересборка даёт ту же карту.
        // Возле построек и дорог не ставим, чтобы не загораживать проходы.
        bool Free(float x, float z) =>
            Mathf.Abs(z + 9f) > 6f &&                                   // трасса
            !(x > 22f && x < 66f && z > 22f && z < 64f) &&              // «Заря»
            !(Mathf.Abs(x) < 34f && z > -30f && z < 22f) &&             // Сарыкудук
            !(x > 34f && x < 60f && z < -28f && z > -54f) &&            // насосная
            !(Mathf.Abs(x) < 8f && z < -48f) &&                        // люк
            !(x > -64f && x < -40f && z > -42f && z < -18f);            // лагерь бандитов

        for (int i = 0, placed = 0; i < 400 && placed < 70; i++)
        {
            float x = (float)(rnd.NextDouble() * 130 - 65), z = (float)(rnd.NextDouble() * 130 - 65);
            if (!Free(x, z))
                continue;
            placed++;
            if (placed % 4 == 0)
                PropKit.Tree(new Vector3(x, 0f, z), 0.8f + (float)rnd.NextDouble() * 0.5f, rnd);
            else
                PropKit.Saxaul(new Vector3(x, 0f, z), rnd);
        }
        for (int i = 0; i < 60; i++)
            PropKit.GrassPatch(new Vector3((float)(rnd.NextDouble() * 136 - 68), 0f, (float)(rnd.NextDouble() * 136 - 68)), rnd);
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

        PropKit.Part(crank, "Ручка", PrimitiveType.Cube, new Vector3(0.8f, 0f, 0.15f), new Vector3(0.05f, 0.05f, 0.35f), "Metal", collider: false);
        // Двускатный навес над колодцем и ведро на цепи.
        PropKit.Part(root.transform, "Навес", PrimitiveType.Cube, new Vector3(-0.4f, 2.25f, 0f), new Vector3(1f, 0.05f, 1.3f), "Roof", new Vector3(0f, 0f, 35f), false);
        PropKit.Part(root.transform, "Навес", PrimitiveType.Cube, new Vector3(0.4f, 2.25f, 0f), new Vector3(1f, 0.05f, 1.3f), "Roof", new Vector3(0f, 0f, -35f), false);
        PropKit.Part(root.transform, "Цепь", PrimitiveType.Cube, new Vector3(0f, 1.45f, 0f), new Vector3(0.02f, 0.6f, 0.02f), "Metal", collider: false);
        PropKit.Part(root.transform, "Ведро", PrimitiveType.Cylinder, new Vector3(0f, 1.05f, 0f), new Vector3(0.28f, 0.14f, 0.28f), "Metal", collider: false);
        PropKit.Part(root.transform, "Желоб", PrimitiveType.Cube, new Vector3(0f, 0.75f, -0.8f), new Vector3(0.3f, 0.08f, 0.6f), "Box", collider: false);

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
        Clock(null);
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
        SetBool(door, "metal", true);
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
        BuildBunkerProps();

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
        Crate("Ящик с припасами", new Vector3(7f, 0f, 25.2f), wood, "lab", ("water_clean", 1));
        Crate("Шкафчик охраны", new Vector3(2.2f, 0f, -2.1f), metal, "locker");
        Crate("Ящик с инструментом", new Vector3(-12.5f, 0f, 25f), metal, "locker");

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

    // Обстановка бункера: трубы вдоль коридора, генератор, койки, шкафчики, лабораторное оборудование.
    static void BuildBunkerProps()
    {
        var props = new GameObject("Обстановка").transform;
        foreach (float x in new[] { -1.3f, 1.3f })
        {
            PropKit.Part(props, "Труба", PrimitiveType.Cylinder, new Vector3(x, 2.2f, 10.5f), new Vector3(0.18f, 7.5f, 0.18f), "Rust", new Vector3(90f, 0f, 0f), false);
            PropKit.Part(props, "Кабель", PrimitiveType.Cube, new Vector3(x, 1.9f, 10.5f), new Vector3(0.05f, 0.05f, 15f), "Tire", collider: false);
        }
        for (int i = 0; i < 4; i++)
            PropKit.Part(props, "Плафон", PrimitiveType.Cube, new Vector3(0f, 2.45f, 5f + i * 4f), new Vector3(0.5f, 0.08f, 0.25f), "Frame", collider: false);

        // Стартовая комната: койки и шкафчики.
        PropKit.Part(props, "Койка", PrimitiveType.Cube, new Vector3(-2.2f, 0.35f, 1.2f), new Vector3(0.9f, 0.15f, 1.9f), "Metal").AddComponent<Bed>();
        PropKit.Part(props, "Матрас", PrimitiveType.Cube, new Vector3(-2.2f, 0.47f, 1.2f), new Vector3(0.85f, 0.1f, 1.8f), "Felt", collider: false);
        PropKit.Part(props, "Шкафчик", PrimitiveType.Cube, new Vector3(2.4f, 1f, 1.5f), new Vector3(0.6f, 2f, 0.5f), "Slate");
        PropKit.Part(props, "Шкафчик", PrimitiveType.Cube, new Vector3(2.4f, 1f, 0.9f), new Vector3(0.6f, 2f, 0.5f), "Slate");
        PropKit.Barrel(new Vector3(-2.3f, 0f, -2.2f), "Metal").SetParent(props, true);

        // Генераторная: генератор, бочки с топливом, щиток.
        PropKit.Part(props, "Генератор", PrimitiveType.Cube, new Vector3(-9f, 0.7f, 24.6f), new Vector3(3f, 1.4f, 1.4f), "TractorRed");
        PropKit.Part(props, "Маховик", PrimitiveType.Cylinder, new Vector3(-10.7f, 0.8f, 24.6f), new Vector3(1f, 0.1f, 1f), "Metal", new Vector3(0f, 0f, 90f), false);
        PropKit.Part(props, "Выхлоп", PrimitiveType.Cylinder, new Vector3(-8f, 2f, 25.3f), new Vector3(0.2f, 0.7f, 0.2f), "Rust", collider: false);
        PropKit.Barrel(new Vector3(-6f, 0f, 19f), "Danger").SetParent(props, true);
        PropKit.Barrel(new Vector3(-5.3f, 0f, 19.4f), "Danger").SetParent(props, true);

        // Лаборатория: стеллажи с колбами, второй стол, шкаф с документами.
        for (int i = 0; i < 3; i++)
        {
            PropKit.Part(props, "Стеллаж", PrimitiveType.Cube, new Vector3(6f + i * 2.4f, 1.1f, 25.5f), new Vector3(1.8f, 2.2f, 0.5f), "Metal");
            for (int k = 0; k < 3; k++)
                PropKit.Part(props, "Колба", PrimitiveType.Cylinder, new Vector3(5.5f + i * 2.4f + k * 0.5f, 1.45f, 25.15f), new Vector3(0.12f, 0.12f, 0.12f), k == 1 ? "Glass" : "Item", collider: false);
        }
        PropKit.Part(props, "Стол", PrimitiveType.Cube, new Vector3(7f, 0.45f, 20f), new Vector3(2f, 0.9f, 1f), "Frame");
        PropKit.Part(props, "Микроскоп", PrimitiveType.Cylinder, new Vector3(6.6f, 1.05f, 20f), new Vector3(0.15f, 0.15f, 0.15f), "Tire", collider: false);
        PropKit.Part(props, "Бумаги", PrimitiveType.Cube, new Vector3(7.4f, 0.92f, 20f), new Vector3(0.5f, 0.02f, 0.35f), "Frame", collider: false);
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
