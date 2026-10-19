using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static SceneBuildKit;

// Детализированные постройки и предметы окружения из примитивов. Размеры реальные
// (дверь 0,9 × 2 м, этаж 3 м, колесо легковушки Ø 0,65 м), чтобы потом каждую можно было
// заменить моделью из Blender один к одному: имя объекта подсказывает, что это.
public static class PropKit
{
    static readonly Dictionary<string, Color> palette = new()
    {
        { "Ground", new Color(0.72f, 0.63f, 0.45f) },
        { "Wall", new Color(0.85f, 0.82f, 0.74f) },
        { "WallBlue", new Color(0.62f, 0.72f, 0.8f) },
        { "Brick", new Color(0.6f, 0.33f, 0.25f) },
        { "Roof", new Color(0.45f, 0.2f, 0.16f) },
        { "Slate", new Color(0.4f, 0.43f, 0.45f) },
        { "Glass", new Color(0.12f, 0.17f, 0.22f) },
        { "Frame", new Color(0.92f, 0.92f, 0.88f) },
        { "Box", new Color(0.45f, 0.33f, 0.2f) },
        { "DarkWood", new Color(0.3f, 0.21f, 0.13f) },
        { "Metal", new Color(0.3f, 0.32f, 0.33f) },
        { "Rust", new Color(0.5f, 0.3f, 0.18f) },
        { "Concrete", new Color(0.55f, 0.55f, 0.52f) },
        { "Road", new Color(0.36f, 0.34f, 0.31f) },
        { "Grass", new Color(0.55f, 0.56f, 0.33f) },
        { "Leaves", new Color(0.33f, 0.42f, 0.22f) },
        { "Saxaul", new Color(0.48f, 0.47f, 0.3f) },
        { "Felt", new Color(0.86f, 0.8f, 0.68f) },
        { "FeltDark", new Color(0.55f, 0.32f, 0.22f) },
        { "Tire", new Color(0.12f, 0.12f, 0.12f) },
        { "CarGreen", new Color(0.33f, 0.4f, 0.3f) },
        { "CarBlue", new Color(0.3f, 0.38f, 0.52f) },
        { "TractorRed", new Color(0.62f, 0.2f, 0.15f) },
        { "AwningRed", new Color(0.7f, 0.2f, 0.18f) },
        { "Hay", new Color(0.8f, 0.7f, 0.4f) },
        { "Danger", new Color(0.85f, 0.75f, 0.2f) },
        { "Fire", new Color(0.95f, 0.5f, 0.15f) },
        { "Grey", new Color(0.45f, 0.5f, 0.45f) },
        { "Player", new Color(0.25f, 0.4f, 0.6f) },
        { "Item", new Color(0.3f, 0.7f, 0.9f) },
    };

    public static Material M(string name) => GetMaterial(name, palette.TryGetValue(name, out var c) ? c : Color.magenta);

    // Деталь объекта: примитив с локальными координатами внутри root.
    public static GameObject Part(Transform root, string name, PrimitiveType type, Vector3 localPos, Vector3 size, string material,
                                  Vector3 euler = default, bool collider = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(root, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = M(material);
        if (!collider)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        else if (type == PrimitiveType.Cylinder)
        {
            // У цилиндра Unity капсульный коллайдер, и у плоского цилиндра (кольцо, крышка люка)
            // он раздувается в шар. Прямоугольный коллайдер по размеру детали точнее.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.AddComponent<BoxCollider>();
        }
        return go;
    }

    public static Transform Root(string name, Vector3 position, float yaw = 0f, Transform parent = null)
    {
        var t = new GameObject(name).transform;
        if (parent != null)
            t.SetParent(parent, false);
        t.position = position;
        t.rotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    // ---------------------------------------------------------------- дома

    // Закрытый дом: стены одним блоком, окна, дверь, двускатная крыша, труба.
    // Ширина по X, глубина по Z, фасад с дверью смотрит в −Z.
    public static Transform House(string name, Vector3 position, float yaw, float width, float depth, string wall = "Wall", string roof = "Roof")
    {
        var root = Root(name, position, yaw);
        const float h = 3f;
        Part(root, "Стены", PrimitiveType.Cube, new Vector3(0f, h / 2f, 0f), new Vector3(width, h, depth), wall);
        Part(root, "Цоколь", PrimitiveType.Cube, new Vector3(0f, 0.2f, 0f), new Vector3(width + 0.15f, 0.4f, depth + 0.15f), "Concrete", collider: false);
        Windows(root, width, depth, h);
        // Дверь заколочена: в этот дом не зайти, это просто фасад.
        Part(root, "Дверь (заколочена)", PrimitiveType.Cube, new Vector3(-width * 0.25f, 1f, -depth / 2f - 0.03f), new Vector3(0.9f, 2f, 0.06f), "DarkWood", collider: false);
        Part(root, "Доска", PrimitiveType.Cube, new Vector3(-width * 0.25f, 1.2f, -depth / 2f - 0.08f), new Vector3(1.1f, 0.15f, 0.04f), "Box", new Vector3(0f, 0f, 20f), false);
        Part(root, "Крыльцо", PrimitiveType.Cube, new Vector3(-width * 0.25f, 0.1f, -depth / 2f - 0.6f), new Vector3(1.6f, 0.2f, 1.2f), "Concrete");
        GableRoof(root, width, depth, h, roof);
        Part(root, "Труба", PrimitiveType.Cube, new Vector3(width * 0.25f, h + 1.3f, depth * 0.15f), new Vector3(0.45f, 1.4f, 0.45f), "Brick", collider: false);
        return root;
    }

    // Окна на длинных сторонах и по одному на торцах: стекло и белая рама.
    static void Windows(Transform root, float width, float depth, float h)
    {
        int count = Mathf.Max(1, Mathf.FloorToInt(width / 3f));
        for (int i = 0; i < count; i++)
        {
            float x = -width / 2f + width * (i + 0.5f) / count;
            if (Mathf.Abs(x + width * 0.25f) < 1f)
                continue; // место под дверь
            Window(root, new Vector3(x, 1.6f, -depth / 2f), 0f);
            Window(root, new Vector3(x, 1.6f, depth / 2f), 180f);
        }
        Window(root, new Vector3(width / 2f, 1.6f, 0f), 90f);
        Window(root, new Vector3(-width / 2f, 1.6f, 0f), -90f);
    }

    public static void Window(Transform root, Vector3 localPos, float yaw)
    {
        var w = Root("Окно", Vector3.zero, 0f, root);
        w.localPosition = localPos;
        w.localRotation = Quaternion.Euler(0f, yaw, 0f);
        // Окно 1 × 1,2 м, низ на высоте 0,9 м — как в плане арта.
        Part(w, "Стекло", PrimitiveType.Cube, new Vector3(0f, 0f, -0.02f), new Vector3(1f, 1.2f, 0.04f), "Glass", collider: false);
        Part(w, "Рама верх", PrimitiveType.Cube, new Vector3(0f, 0.63f, -0.05f), new Vector3(1.15f, 0.08f, 0.06f), "Frame", collider: false);
        Part(w, "Рама низ", PrimitiveType.Cube, new Vector3(0f, -0.63f, -0.08f), new Vector3(1.25f, 0.08f, 0.14f), "Frame", collider: false);
        Part(w, "Рама лево", PrimitiveType.Cube, new Vector3(-0.54f, 0f, -0.05f), new Vector3(0.08f, 1.3f, 0.06f), "Frame", collider: false);
        Part(w, "Рама право", PrimitiveType.Cube, new Vector3(0.54f, 0f, -0.05f), new Vector3(0.08f, 1.3f, 0.06f), "Frame", collider: false);
        Part(w, "Переплёт", PrimitiveType.Cube, new Vector3(0f, 0.15f, -0.05f), new Vector3(0.05f, 0.9f, 0.05f), "Frame", collider: false);
    }

    // Двускатная крыша из двух наклонных плит; гребень вдоль Z. Возвращает рендеры плит.
    public static Renderer[] GableRoof(Transform root, float width, float depth, float wallHeight, string material, float pitch = 28f)
    {
        float half = width / 2f + 0.35f;
        float slab = half / Mathf.Cos(pitch * Mathf.Deg2Rad);
        float rise = half * Mathf.Tan(pitch * Mathf.Deg2Rad);
        var left = Part(root, "Скат", PrimitiveType.Cube, new Vector3(-half / 2f, wallHeight + rise / 2f, 0f), new Vector3(slab, 0.12f, depth + 0.6f), material, new Vector3(0f, 0f, pitch), false);
        var right = Part(root, "Скат", PrimitiveType.Cube, new Vector3(half / 2f, wallHeight + rise / 2f, 0f), new Vector3(slab, 0.12f, depth + 0.6f), material, new Vector3(0f, 0f, -pitch), false);
        // Фронтоны: треугольник под скатами, собранный из горизонтальных досок,
        // каждая уже предыдущей. Ниже края стены ничего не опускается.
        var renderers = new List<Renderer> { left.GetComponent<Renderer>(), right.GetComponent<Renderer>() };
        const int boards = 5;
        float step = rise / boards;
        for (int i = 0; i < boards; i++)
        {
            float w = width * (1f - (i + 0.5f) / boards);
            foreach (float z in new[] { -depth / 2f, depth / 2f })
            {
                var board = Part(root, "Фронтон", PrimitiveType.Cube, new Vector3(0f, wallHeight + step * (i + 0.5f), z), new Vector3(w, step, 0.12f), "Frame", collider: false);
                renderers.Add(board.GetComponent<Renderer>());
            }
        }
        return renderers.ToArray();
    }

    // Крыша для дома, в который можно зайти: прячется, когда игрок внутри.
    public static void HidingRoof(Transform root, Vector3 roomCenter, float width, float depth, float wallHeight, string material)
    {
        var roofRoot = Root("Крыша", Vector3.zero, 0f, root);
        roofRoot.position = roomCenter;
        var renderers = GableRoof(roofRoot, width, depth, wallHeight, material);
        var hider = roofRoot.gameObject.AddComponent<RoofHider>();
        Set(hider, "roomSize", p => p.vector3Value = new Vector3(width, wallHeight + 2f, depth));
        Set(hider, "roof", p =>
        {
            p.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        });
    }

    // Юрта: решётчатая стена, купол из сужающихся колец, тундук наверху, дверь.
    public static Transform Yurt(string name, Vector3 position, float yaw, float radius = 3f)
    {
        var root = Root(name, position, yaw);
        Part(root, "Стена", PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(radius * 2f, 0.9f, radius * 2f), "Felt");
        Part(root, "Пояс", PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(radius * 2.02f, 0.06f, radius * 2.02f), "FeltDark", collider: false);
        for (int i = 0; i < 4; i++)
        {
            float k = 1f - i * 0.22f;
            Part(root, "Купол", PrimitiveType.Cylinder, new Vector3(0f, 1.9f + i * 0.28f, 0f), new Vector3(radius * 2f * k, 0.14f, radius * 2f * k), "Felt", collider: false);
        }
        Part(root, "Тундук", PrimitiveType.Cylinder, new Vector3(0f, 3.05f, 0f), new Vector3(0.9f, 0.08f, 0.9f), "DarkWood", collider: false);
        Part(root, "Дверь", PrimitiveType.Cube, new Vector3(0f, 0.8f, -radius - 0.02f), new Vector3(0.9f, 1.6f, 0.08f), "FeltDark", collider: false);
        return root;
    }

    // Навес или сарай без передней стены (мастерская Кима).
    public static Transform Shed(string name, Vector3 position, float yaw, float width, float depth)
    {
        var root = Root(name, position, yaw);
        const float h = 2.8f;
        Part(root, "Задняя стена", PrimitiveType.Cube, new Vector3(0f, h / 2f, depth / 2f), new Vector3(width, h, 0.2f), "DarkWood");
        Part(root, "Стена", PrimitiveType.Cube, new Vector3(-width / 2f, h / 2f, 0f), new Vector3(0.2f, h, depth), "DarkWood");
        Part(root, "Стена", PrimitiveType.Cube, new Vector3(width / 2f, h / 2f, 0f), new Vector3(0.2f, h, depth), "DarkWood");
        Part(root, "Крыша", PrimitiveType.Cube, new Vector3(0f, h + 0.1f, 0f), new Vector3(width + 0.6f, 0.1f, depth + 0.6f), "Slate", new Vector3(-6f, 0f, 0f), false);
        Part(root, "Верстак", PrimitiveType.Cube, new Vector3(0f, 0.45f, depth / 2f - 0.5f), new Vector3(width * 0.6f, 0.9f, 0.7f), "Box");
        Part(root, "Тиски", PrimitiveType.Cube, new Vector3(width * 0.2f, 1f, depth / 2f - 0.5f), new Vector3(0.2f, 0.2f, 0.3f), "Metal", collider: false);
        Part(root, "Полка", PrimitiveType.Cube, new Vector3(0f, 1.8f, depth / 2f - 0.15f), new Vector3(width * 0.7f, 0.06f, 0.3f), "Box", collider: false);
        for (int i = 0; i < 4; i++)
            Part(root, "Банка", PrimitiveType.Cylinder, new Vector3(-width * 0.3f + i * 0.5f, 1.95f, depth / 2f - 0.15f), new Vector3(0.18f, 0.12f, 0.18f), i % 2 == 0 ? "Rust" : "Metal", collider: false);
        return root;
    }

    // ---------------------------------------------------------------- техника

    public static Transform CarWreck(string name, Vector3 position, float yaw, string paint)
    {
        var root = Root(name, position, yaw);
        // Легковушка 4,2 × 1,6 м, кузов чуть просел на одну сторону.
        var body = Root("Кузов", Vector3.zero, 0f, root);
        body.localPosition = new Vector3(0f, 0f, 0f);
        body.localRotation = Quaternion.Euler(0f, 0f, 4f);
        Part(body, "Низ", PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(1.65f, 0.7f, 4.2f), paint);
        Part(body, "Кабина", PrimitiveType.Cube, new Vector3(0f, 1.25f, -0.2f), new Vector3(1.5f, 0.55f, 2f), paint, collider: false);
        Part(body, "Стекло", PrimitiveType.Cube, new Vector3(0f, 1.27f, -0.2f), new Vector3(1.52f, 0.4f, 1.6f), "Glass", collider: false);
        Part(body, "Бампер", PrimitiveType.Cube, new Vector3(0f, 0.45f, -2.15f), new Vector3(1.7f, 0.15f, 0.1f), "Metal", collider: false);
        Part(body, "Ржавчина", PrimitiveType.Cube, new Vector3(0.83f, 0.6f, 0.8f), new Vector3(0.02f, 0.5f, 1.4f), "Rust", collider: false);
        foreach (var (x, z) in new[] { (-0.8f, -1.35f), (0.8f, -1.35f), (-0.8f, 1.35f), (0.8f, 1.35f) })
            Part(root, "Колесо", PrimitiveType.Cylinder, new Vector3(x, 0.32f, z), new Vector3(0.65f, 0.1f, 0.65f), "Tire", new Vector3(0f, 0f, 90f), false);
        return root;
    }

    public static Transform Tractor(string name, Vector3 position, float yaw)
    {
        var root = Root(name, position, yaw);
        Part(root, "Капот", PrimitiveType.Cube, new Vector3(0f, 1.1f, -0.9f), new Vector3(0.9f, 0.8f, 2f), "TractorRed");
        Part(root, "Рама", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0.2f), new Vector3(1f, 0.4f, 3.2f), "Metal", collider: false);
        Part(root, "Кабина", PrimitiveType.Cube, new Vector3(0f, 1.9f, 0.9f), new Vector3(1.3f, 1.4f, 1.3f), "TractorRed");
        Part(root, "Стекло", PrimitiveType.Cube, new Vector3(0f, 2.05f, 0.9f), new Vector3(1.32f, 0.8f, 1.1f), "Glass", collider: false);
        Part(root, "Выхлоп", PrimitiveType.Cylinder, new Vector3(0.3f, 2f, -1.4f), new Vector3(0.1f, 0.5f, 0.1f), "Metal", collider: false);
        Part(root, "Колесо заднее", PrimitiveType.Cylinder, new Vector3(-0.85f, 0.75f, 0.9f), new Vector3(1.5f, 0.25f, 1.5f), "Tire", new Vector3(0f, 0f, 90f));
        Part(root, "Колесо заднее", PrimitiveType.Cylinder, new Vector3(0.85f, 0.75f, 0.9f), new Vector3(1.5f, 0.25f, 1.5f), "Tire", new Vector3(0f, 0f, 90f));
        Part(root, "Колесо переднее", PrimitiveType.Cylinder, new Vector3(-0.6f, 0.45f, -1.4f), new Vector3(0.9f, 0.15f, 0.9f), "Tire", new Vector3(0f, 0f, 90f), false);
        Part(root, "Колесо переднее", PrimitiveType.Cylinder, new Vector3(0.6f, 0.45f, -1.4f), new Vector3(0.9f, 0.15f, 0.9f), "Tire", new Vector3(0f, 0f, 90f), false);
        return root;
    }

    public static Transform WaterTower(string name, Vector3 position)
    {
        var root = Root(name, position);
        foreach (var (x, z) in new[] { (-1.2f, -1.2f), (1.2f, -1.2f), (-1.2f, 1.2f), (1.2f, 1.2f) })
            Part(root, "Опора", PrimitiveType.Cube, new Vector3(x, 3.5f, z), new Vector3(0.2f, 7f, 0.2f), "Rust");
        Part(root, "Бак", PrimitiveType.Cylinder, new Vector3(0f, 8f, 0f), new Vector3(3.4f, 1.1f, 3.4f), "Metal", collider: false);
        Part(root, "Крышка", PrimitiveType.Cylinder, new Vector3(0f, 9.2f, 0f), new Vector3(2.6f, 0.15f, 2.6f), "Rust", collider: false);
        Part(root, "Лестница", PrimitiveType.Cube, new Vector3(1.75f, 4f, 0f), new Vector3(0.05f, 8f, 0.45f), "Metal", collider: false);
        return root;
    }

    // ---------------------------------------------------------------- природа и мелочи

    public static Transform Tree(Vector3 position, float scale, System.Random rnd)
    {
        var root = Root("Карагач", position, (float)rnd.NextDouble() * 360f);
        Part(root, "Ствол", PrimitiveType.Cylinder, new Vector3(0f, 1.5f * scale, 0f), new Vector3(0.35f, 1.5f, 0.35f) * scale, "DarkWood");
        for (int i = 0; i < 3; i++)
        {
            float a = i * 2.1f;
            var offset = new Vector3(Mathf.Cos(a) * 0.7f, 3.4f + i * 0.35f, Mathf.Sin(a) * 0.7f) * scale;
            Part(root, "Крона", PrimitiveType.Sphere, offset, new Vector3(2.4f, 1.9f, 2.4f) * scale * (1f - i * 0.12f), "Leaves", collider: false);
        }
        return root;
    }

    public static Transform Saxaul(Vector3 position, System.Random rnd)
    {
        float s = 0.7f + (float)rnd.NextDouble() * 0.6f;
        var root = Root("Саксаул", position, (float)rnd.NextDouble() * 360f);
        Part(root, "Куст", PrimitiveType.Sphere, new Vector3(0f, 0.45f * s, 0f), new Vector3(1.6f, 0.9f, 1.3f) * s, "Saxaul", collider: false);
        Part(root, "Куст", PrimitiveType.Sphere, new Vector3(0.5f * s, 0.35f * s, 0.3f * s), new Vector3(1f, 0.7f, 0.9f) * s, "Saxaul", collider: false);
        return root;
    }

    public static void GrassPatch(Vector3 position, System.Random rnd)
    {
        float s = 2f + (float)rnd.NextDouble() * 4f;
        var patch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        patch.name = "Трава";
        Object.DestroyImmediate(patch.GetComponent<Collider>());
        patch.transform.position = position + Vector3.up * 0.01f;
        patch.transform.localScale = new Vector3(s, 0.005f, s * (0.6f + (float)rnd.NextDouble() * 0.6f));
        patch.transform.rotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 180f, 0f);
        patch.GetComponent<Renderer>().sharedMaterial = M("Grass");
    }

    public static void Road(Vector3 from, Vector3 to, float width)
    {
        Vector3 d = to - from;
        var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "Дорога";
        Object.DestroyImmediate(road.GetComponent<Collider>());
        road.transform.position = (from + to) / 2f + Vector3.up * 0.02f;
        road.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z));
        road.transform.localScale = new Vector3(width, 0.02f, d.magnitude);
        road.GetComponent<Renderer>().sharedMaterial = M("Road");
    }

    // Линия столбов ЛЭП с проводами.
    public static void PowerLine(Vector3 from, Vector3 to, float spacing)
    {
        var root = Root("ЛЭП", Vector3.zero);
        int count = Mathf.Max(2, Mathf.RoundToInt(Vector3.Distance(from, to) / spacing) + 1);
        Vector3 prev = default;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = Vector3.Lerp(from, to, i / (count - 1f));
            var pole = Root("Столб", p, Quaternion.LookRotation(to - from).eulerAngles.y, root);
            Part(pole, "Столб", PrimitiveType.Cylinder, new Vector3(0f, 3.5f, 0f), new Vector3(0.22f, 3.5f, 0.22f), "DarkWood");
            Part(pole, "Траверса", PrimitiveType.Cube, new Vector3(0f, 6.6f, 0f), new Vector3(1.6f, 0.1f, 0.1f), "DarkWood", collider: false);
            if (i > 0)
            {
                foreach (float x in new[] { -0.7f, 0.7f })
                {
                    Vector3 a = prev + pole.right * x + Vector3.up * 6.7f;
                    Vector3 b = p + pole.right * x + Vector3.up * 6.7f;
                    var wire = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wire.name = "Провод";
                    Object.DestroyImmediate(wire.GetComponent<Collider>());
                    wire.transform.SetParent(root, false);
                    wire.transform.position = (a + b) / 2f - Vector3.up * 0.3f;
                    wire.transform.rotation = Quaternion.LookRotation(b - a);
                    wire.transform.localScale = new Vector3(0.03f, 0.03f, Vector3.Distance(a, b));
                    wire.GetComponent<Renderer>().sharedMaterial = M("Tire");
                }
            }
            prev = p;
        }
    }

    // Деревянный забор: столбики через 2 м и две жерди. Проёмы (ворота) пропускаются.
    public static void Fence(Transform parent, Vector3 from, Vector3 to, params (float from, float to)[] gaps)
    {
        float length = Vector3.Distance(from, to);
        Vector3 dir = (to - from).normalized;
        int posts = Mathf.CeilToInt(length / 2f);
        for (int i = 0; i < posts; i++)
        {
            float t0 = i * 2f, t1 = Mathf.Min(length, t0 + 2f);
            bool inGap = false;
            foreach (var g in gaps)
                if (t1 > g.from && t0 < g.to)
                    inGap = true;
            if (inGap)
                continue;
            Vector3 a = from + dir * t0, b = from + dir * t1;
            var seg = Root("Секция забора", (a + b) / 2f, Quaternion.LookRotation(dir).eulerAngles.y, parent);
            Part(seg, "Столбик", PrimitiveType.Cube, new Vector3(0f, 0.75f, -(t1 - t0) / 2f), new Vector3(0.12f, 1.5f, 0.12f), "DarkWood");
            Part(seg, "Жердь", PrimitiveType.Cube, new Vector3(0f, 1.2f, 0f), new Vector3(0.06f, 0.1f, t1 - t0), "Box");
            Part(seg, "Жердь", PrimitiveType.Cube, new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 0.1f, t1 - t0), "Box");
        }
    }

    public static Transform Barrel(Vector3 position, string paint = "Rust")
    {
        var root = Root("Бочка", position);
        Part(root, "Бочка", PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.45f, 0.6f), paint);
        Part(root, "Обруч", PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.62f, 0.02f, 0.62f), "Metal", collider: false);
        Part(root, "Обруч", PrimitiveType.Cylinder, new Vector3(0f, 0.65f, 0f), new Vector3(0.62f, 0.02f, 0.62f), "Metal", collider: false);
        return root;
    }

    public static Transform Bench(Vector3 position, float yaw)
    {
        var root = Root("Скамейка", position, yaw);
        Part(root, "Сиденье", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.06f, 0.4f), "Box");
        Part(root, "Ножка", PrimitiveType.Cube, new Vector3(-0.65f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.35f), "DarkWood", collider: false);
        Part(root, "Ножка", PrimitiveType.Cube, new Vector3(0.65f, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.35f), "DarkWood", collider: false);
        Part(root, "Спинка", PrimitiveType.Cube, new Vector3(0f, 0.75f, 0.18f), new Vector3(1.6f, 0.3f, 0.05f), "Box", collider: false);
        return root;
    }

    public static Transform Haystack(Vector3 position)
    {
        var root = Root("Стог", position);
        Part(root, "Стог", PrimitiveType.Sphere, new Vector3(0f, 0.9f, 0f), new Vector3(2.6f, 2f, 2.6f), "Hay");
        return root;
    }

    // Прилавок тёти Гали: стол, навес в полоску, ящики с товаром.
    public static Transform Stall(string name, Vector3 position, float yaw)
    {
        var root = Root(name, position, yaw);
        Part(root, "Прилавок", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f), new Vector3(2.4f, 1f, 0.8f), "Box");
        foreach (var (x, z) in new[] { (-1.2f, -0.5f), (1.2f, -0.5f), (-1.2f, 0.9f), (1.2f, 0.9f) })
            Part(root, "Столб", PrimitiveType.Cube, new Vector3(x, 1.2f, z), new Vector3(0.08f, 2.4f, 0.08f), "DarkWood", collider: false);
        for (int i = 0; i < 5; i++)
            Part(root, "Навес", PrimitiveType.Cube, new Vector3(-1.04f + i * 0.52f, 2.45f, 0.2f), new Vector3(0.52f, 0.05f, 1.9f), i % 2 == 0 ? "AwningRed" : "Frame", new Vector3(-8f, 0f, 0f), false);
        Part(root, "Ящик", PrimitiveType.Cube, new Vector3(-0.6f, 1.15f, 0f), new Vector3(0.6f, 0.3f, 0.4f), "Box", collider: false);
        Part(root, "Банки", PrimitiveType.Cylinder, new Vector3(0.4f, 1.12f, 0f), new Vector3(0.15f, 0.12f, 0.15f), "Metal", collider: false);
        Part(root, "Банки", PrimitiveType.Cylinder, new Vector3(0.6f, 1.12f, 0.1f), new Vector3(0.15f, 0.12f, 0.15f), "Metal", collider: false);
        Part(root, "Лепёшки", PrimitiveType.Cylinder, new Vector3(0.05f, 1.03f, -0.15f), new Vector3(0.35f, 0.03f, 0.35f), "Hay", collider: false);
        return root;
    }

    public static Transform Campfire(Vector3 position)
    {
        var root = Root("Костёр", position);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.PI / 3f;
            Part(root, "Камень", PrimitiveType.Sphere, new Vector3(Mathf.Cos(a) * 0.6f, 0.1f, Mathf.Sin(a) * 0.6f), new Vector3(0.3f, 0.2f, 0.3f), "Concrete", collider: false);
        }
        Part(root, "Полено", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.4f, 0.12f), "DarkWood", new Vector3(0f, 0f, 80f), false);
        Part(root, "Полено", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.12f, 0.4f, 0.12f), "DarkWood", new Vector3(80f, 0f, 0f), false);
        Part(root, "Угли", PrimitiveType.Sphere, new Vector3(0f, 0.15f, 0f), new Vector3(0.45f, 0.2f, 0.45f), "Fire", collider: false);
        var light = PointLight("Огонь", position + Vector3.up * 0.6f, new Color(1f, 0.6f, 0.3f), 2.5f, 7f);
        light.transform.SetParent(root, true);
        // Горит всегда: у него греются и готовят (станция «огонь» для крафта).
        root.gameObject.AddComponent<global::Campfire>();
        return root;
    }

    // ---------------------------------------------------------------- жители

    public enum Hat { None, Kalpak, Scarf, Cap, Doppi }

    // Житель «Зари». Пока моделей нет, фигура собрана из примитивов в цветах из исходной игры.
    public static Npc Resident(string name, string title, string dialogueId, Vector3 position, float yaw,
                               Color body, Color legs, Color skin, Hat hat)
    {
        var root = Root(name, position, yaw);
        var collider = root.gameObject.AddComponent<CapsuleCollider>();
        collider.height = 1.75f;
        collider.radius = 0.35f;
        collider.center = new Vector3(0f, 0.875f, 0f);

        var bodyMat = GetMaterial("Npc_" + dialogueId + "_body", body);
        var legsMat = GetMaterial("Npc_" + dialogueId + "_legs", legs);
        var skinMat = GetMaterial("Npc_" + dialogueId + "_skin", skin);

        Primitive(root, "Ноги", PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(0.42f, 0.42f, 0.3f), legsMat);
        Primitive(root, "Туловище", PrimitiveType.Capsule, new Vector3(0f, 1.1f, 0f), new Vector3(0.55f, 0.42f, 0.38f), bodyMat);
        Primitive(root, "Рука", PrimitiveType.Capsule, new Vector3(-0.33f, 1.05f, 0f), new Vector3(0.14f, 0.32f, 0.14f), bodyMat);
        Primitive(root, "Рука", PrimitiveType.Capsule, new Vector3(0.33f, 1.05f, 0f), new Vector3(0.14f, 0.32f, 0.14f), bodyMat);
        Primitive(root, "Голова", PrimitiveType.Sphere, new Vector3(0f, 1.6f, 0f), new Vector3(0.26f, 0.28f, 0.26f), skinMat);
        Primitive(root, "Нос", PrimitiveType.Cube, new Vector3(0f, 1.6f, 0.13f), new Vector3(0.05f, 0.07f, 0.05f), skinMat);

        switch (hat)
        {
            case Hat.Kalpak:
                // Белый войлочный калпак с чёрной отделкой.
                Primitive(root, "Калпак", PrimitiveType.Cylinder, new Vector3(0f, 1.82f, 0f), new Vector3(0.24f, 0.13f, 0.24f), M("Frame"));
                Primitive(root, "Калпак верх", PrimitiveType.Cylinder, new Vector3(0f, 1.97f, 0f), new Vector3(0.14f, 0.05f, 0.14f), M("Frame"));
                Primitive(root, "Отворот", PrimitiveType.Cylinder, new Vector3(0f, 1.72f, 0f), new Vector3(0.27f, 0.025f, 0.27f), M("Tire"));
                break;
            case Hat.Scarf:
                Primitive(root, "Платок", PrimitiveType.Sphere, new Vector3(0f, 1.64f, -0.03f), new Vector3(0.29f, 0.3f, 0.29f), bodyMat);
                break;
            case Hat.Cap:
                Primitive(root, "Кепка", PrimitiveType.Cylinder, new Vector3(0f, 1.74f, 0f), new Vector3(0.27f, 0.04f, 0.27f), M("Metal"));
                Primitive(root, "Козырёк", PrimitiveType.Cube, new Vector3(0f, 1.72f, 0.16f), new Vector3(0.2f, 0.02f, 0.12f), M("Metal"));
                break;
            case Hat.Doppi:
                Primitive(root, "Тюбетейка", PrimitiveType.Cylinder, new Vector3(0f, 1.74f, 0f), new Vector3(0.24f, 0.04f, 0.24f), M("Tire"));
                break;
        }

        var label = new GameObject("Имя");
        label.transform.SetParent(root, false);
        label.transform.localPosition = new Vector3(0f, 2.25f, 0f);
        var text = label.AddComponent<TextMesh>();
        text.text = name;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.06f;
        text.fontSize = 48;
        text.color = new Color(1f, 0.92f, 0.75f);
        label.AddComponent<Nameplate>();

        var npc = root.gameObject.AddComponent<Npc>();
        SetString(npc, "npcName", name);
        SetString(npc, "title", title);
        SetString(npc, "dialogueId", dialogueId);
        return npc;
    }

    static void Primitive(Transform root, string name, PrimitiveType type, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }
}
