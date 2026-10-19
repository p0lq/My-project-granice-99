using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Инвентарь-сетка, как в старой игре: 8 × 5 ячеек, слоты надетого, вес.
//   ЛКМ — выбрать предмет (описание и кнопки справа), ПКМ — быстрое действие (съесть, надеть…).
//   Перетаскивание: ячейка → ячейка меняет порядок, ячейка → слот надевает,
//   слот → сетка снимает, за пределы окна — выбросить на землю.
public class InventoryWindow : GameWindows.Window
{
    const int Columns = 8;
    const int Rows = 5;

    static readonly (string slot, string label)[] EquipSlots =
    {
        ("weapon", "Оружие"), ("head", "Голова"), ("body", "Тело"), ("face", "Лицо"), ("back", "Рюкзак"), ("light", "Свет"),
    };

    readonly List<InventorySlot> grid = new();
    readonly Dictionary<string, InventorySlot> equipViews = new();
    Text weightText;
    RectTransform weightFill;
    Text infoTitle;
    Text infoBody;
    RectTransform infoButtons;
    ItemStack selected;
    RectTransform box;

    public override void Build(Transform canvas)
    {
        box = UIFactory.Window(canvas, "Инвентарь", new Vector2(1500f, 820f), out root, GameWindows.Close);

        // Сетка 8 × 5 ячеек по 96 px.
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                var view = InventorySlot.Create(box, new Vector2(30f + c * 100f, -90f - r * 100f), this, grid.Count, null);
                grid.Add(view);
            }

        var equipTitle = UIFactory.Text("Надето", box, "Надето", 26, TextAnchor.UpperLeft);
        UIFactory.Place(equipTitle.rectTransform, new Vector2(0f, 1f), new Vector2(850f, -86f), new Vector2(300f, 34f));
        for (int i = 0; i < EquipSlots.Length; i++)
        {
            var (slot, label) = EquipSlots[i];
            float x = 850f + (i % 2) * 150f;
            float y = -126f - (i / 2) * 124f;
            var view = InventorySlot.Create(box, new Vector2(x, y), this, -1, slot);
            var caption = UIFactory.Text("Подпись", box, label, 18, TextAnchor.UpperCenter);
            UIFactory.Place(caption.rectTransform, new Vector2(0f, 1f), new Vector2(x - 2f, y - 96f), new Vector2(100f, 24f));
            equipViews[slot] = view;
        }

        var bar = UIFactory.Bar("Вес", box, new Color(0.6f, 0.55f, 0.35f), out weightFill);
        UIFactory.Place(bar, new Vector2(0f, 0f), new Vector2(30f, 40f), new Vector2(790f, 22f));
        weightText = UIFactory.Text("Вес, кг", box, "", 22, TextAnchor.LowerLeft);
        UIFactory.Place(weightText.rectTransform, new Vector2(0f, 0f), new Vector2(30f, 66f), new Vector2(790f, 30f));

        var hint = UIFactory.Text("Подсказка", box, "ЛКМ — выбрать   ПКМ — быстрое действие   Перетащи в слот, чтобы надеть, за окно — выбросить", 18, TextAnchor.LowerLeft);
        hint.color = new Color(1f, 1f, 1f, 0.5f);
        UIFactory.Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(30f, 12f), new Vector2(1100f, 26f));

        infoTitle = UIFactory.Text("Название", box, "", 26, TextAnchor.UpperLeft);
        infoTitle.color = UIFactory.AccentColor;
        UIFactory.Place(infoTitle.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -86f), new Vector2(300f, 70f));
        infoBody = UIFactory.Text("Описание", box, "", 20, TextAnchor.UpperLeft);
        UIFactory.Place(infoBody.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -160f), new Vector2(300f, 330f));
        infoButtons = UIFactory.Column("Кнопки", box, 8f);
        UIFactory.Place(infoButtons, new Vector2(1f, 0f), new Vector2(-30f, 100f), Vector2.zero);
    }

    public override void Refresh()
    {
        var items = GameSession.Data.items;
        for (int i = 0; i < grid.Count; i++)
            grid[i].Set(i < items.Count ? items[i] : null);
        foreach (var pair in equipViews)
            pair.Value.Set(GameSession.Equipped(pair.Key));

        float w = GameSession.Weight(), limit = GameSession.CarryLimit();
        UIFactory.SetBar(weightFill, w / Mathf.Max(1f, limit * 1.5f));
        weightText.text = $"Вес: {w:0.0} / {limit:0.0} кг" + (w > limit ? "  <color=#e07050>перегруз — идёшь медленнее</color>" : "");
        weightText.supportRichText = true;

        if (selected != null && !items.Contains(selected) && !ItemActions.IsEquipped(selected))
            selected = null;
        ShowInfo(selected);
    }

    public void Select(ItemStack stack)
    {
        selected = stack;
        ShowInfo(stack);
    }

    void ShowInfo(ItemStack stack)
    {
        UIFactory.Clear(infoButtons);
        if (stack?.Def == null)
        {
            infoTitle.text = "";
            infoBody.text = "Выбери предмет, чтобы посмотреть, что это.";
            return;
        }
        var def = stack.Def;
        infoTitle.text = stack.count > 1 ? $"{def.name} ×{stack.count}" : def.name;
        infoBody.text = Describe(stack);

        var needs = Object.FindAnyObjectByType<PlayerNeeds>();
        string label = ItemActions.UseLabel(stack);
        if (label != null)
            UIFactory.SmallButton(infoButtons, label, () => ItemActions.Use(stack, needs), 300f);
        UIFactory.SmallButton(infoButtons, "Выбросить", () => Drop(stack), 300f);
    }

    public static string Describe(ItemStack stack)
    {
        var def = stack.Def;
        var lines = new List<string> { def.desc, "", $"Вес: {def.weight:0.##} кг" };
        if (def.Has("hunger")) lines.Add($"Голод {Signed(-def.F("hunger"))}");
        if (def.Has("thirst")) lines.Add($"Жажда {Signed(-def.F("thirst"))}");
        if (def.Has("mood")) lines.Add($"Настроение {Signed(def.F("mood"))}");
        if (def.Has("poison")) lines.Add("Может отравить");
        if (def.Has("dmg")) lines.Add($"Урон {def.F("dmg") * 2.2f:0}, дальность {def.F("range"):0.#} м");
        if (stack.dur >= 0f) lines.Add($"Прочность {stack.dur:0} / {def.F("dur"):0}");
        if (def.type == "gun") lines.Add($"Заряжено {Mathf.Max(0f, stack.charge):0} / {def.F("mag"):0}, патроны: {Items.Name(def.S("ammo"))}");
        if (def.Has("warmth")) lines.Add($"Тепло +{def.F("warmth"):0.#}");
        if (def.Has("protect")) lines.Add($"Защита {def.F("protect") * 100f:0}%");
        if (def.Has("capacity")) lines.Add($"Вместимость +{def.F("capacity"):0} кг");
        if (def.Has("filter")) lines.Add($"Фильтр: {Mathf.Max(0f, stack.charge) / 60f:0.#} ч");
        return string.Join("\n", lines);
    }

    static string Signed(float v) => v >= 0f ? $"+{v:0}" : $"{v:0}";

    void Drop(ItemStack stack)
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
            return;
        ItemActions.Drop(stack, player.transform.position + player.transform.forward * 0.8f);
        selected = null;
    }

    // ---------------------------------------------------------------- перетаскивание

    static InventorySlot dragging;
    static GameObject ghost;

    public void BeginDrag(InventorySlot from, PointerEventData e)
    {
        if (from.Stack == null)
            return;
        dragging = from;
        ghost = UIFactory.Image("Перетаскивание", box.root, new Color(0.85f, 0.65f, 0.3f, 0.6f)).gameObject;
        ghost.GetComponent<Image>().raycastTarget = false;
        UIFactory.Place((RectTransform)ghost.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));
        var t = UIFactory.Text("Имя", ghost.transform, ShortName(from.Stack), 16, TextAnchor.MiddleCenter);
        UIFactory.Stretch(t.rectTransform);
        Drag(e);
    }

    public void Drag(PointerEventData e)
    {
        if (ghost != null)
            ghost.transform.position = e.position;
    }

    public void EndDrag(PointerEventData e)
    {
        if (ghost != null)
            Object.Destroy(ghost);
        ghost = null;
        if (dragging == null)
            return;
        var from = dragging;
        dragging = null;

        var target = e.pointerCurrentRaycast.gameObject != null ? e.pointerCurrentRaycast.gameObject.GetComponentInParent<InventorySlot>() : null;
        bool insideWindow = e.pointerCurrentRaycast.gameObject != null && e.pointerCurrentRaycast.gameObject.transform.IsChildOf(box);
        if (target == null)
        {
            if (!insideWindow)
                Drop(from.Stack);
            return;
        }
        Move(from, target);
    }

    void Move(InventorySlot from, InventorySlot to)
    {
        var stack = from.Stack;
        var items = GameSession.Data.items;
        if (to.Slot != null)
        {
            if (stack.Def.Slot == to.Slot)
                GameSession.Equip(stack);
            else
                GameMessages.Show($"Это нельзя надеть сюда");
            return;
        }
        if (from.Slot != null)
        {
            GameSession.Unequip(from.Slot);
            return;
        }
        // Перестановка в сетке: меняем местами, пустая ячейка в конце списка.
        int a = items.IndexOf(stack);
        int b = Mathf.Min(to.Index, items.Count - 1);
        if (a < 0 || a == b)
            return;
        items.RemoveAt(a);
        items.Insert(b, stack);
        GameSession.NotifyChanged();
    }

    public static string ShortName(ItemStack stack)
    {
        string name = stack.Def?.name ?? stack.id;
        int cut = name.IndexOfAny(new[] { '(', '«' });
        if (cut > 3)
            name = name.Substring(0, cut).Trim();
        return name.Length > 22 ? name.Substring(0, 21) + "…" : name;
    }
}
