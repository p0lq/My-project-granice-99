using UnityEngine;

// Что происходит, когда игрок «использует» предмет в инвентаре (кнопка или ПКМ по ячейке).
// Логика из actions.py старой игры: еда и питьё, лекарства, одежда, книги, батарейки, фильтры,
// радио, записки. Бинты, тряпки и шина применяются к конкретной ране в окне здоровья [H].
public static class ItemActions
{
    public static string UseLabel(ItemStack stack)
    {
        var def = stack.Def;
        if (def == null) return null;
        switch (def.type)
        {
            case "food": return def.B("smoke") ? "Использовать с огнивом" : "Съесть";
            case "drink": return "Выпить";
            case "medical": return def.S("med") is "painkiller" or "charcoal" or "antidote" ? "Принять" : "Лечить [H]";
            case "weapon":
            case "gun":
            case "clothing": return IsEquipped(stack) ? "Снять" : "Надеть";
            case "book": return "Читать";
            case "note": return "Прочесть";
        }
        if (stack.id == "batteries") return "Вставить в фонарик";
        if (def.Has("refill")) return "Сменить фильтр";
        if (def.B("radio")) return "Включить радио";
        return null;
    }

    public static bool IsEquipped(ItemStack stack)
    {
        foreach (var slot in GameSession.Slots)
            if (GameSession.Equipped(slot) == stack)
                return true;
        return false;
    }

    public static void Use(ItemStack stack, PlayerNeeds needs)
    {
        var def = stack.Def;
        if (def == null || needs == null)
            return;

        switch (def.type)
        {
            case "food":
            case "drink":
                needs.Consume(stack);
                return;
            case "medical":
                if (!needs.Consume(stack))
                    GameWindows.Open("health");
                return;
            case "weapon":
            case "gun":
            case "clothing":
                if (IsEquipped(stack))
                    GameSession.Unequip(def.Slot);
                else
                    GameSession.Equip(stack);
                return;
            case "book":
                Read(stack);
                return;
            case "note":
                NoteWindow.Show(stack.id);
                return;
        }

        if (stack.id == "batteries")
        {
            if (GameSession.Battery >= 99f)
            {
                GameMessages.Show("Фонарик и так заряжен");
                return;
            }
            GameSession.RemoveStack(stack);
            GameSession.Battery = Mathf.Min(100f, GameSession.Battery + Items.BatteryCharge);
            GameMessages.Show("Поменял батарейки");
        }
        else if (def.Has("refill"))
        {
            var face = GameSession.Equipped("face");
            if (face == null || face.id != "gasmask")
            {
                GameMessages.Show("Фильтр подходит к надетому противогазу");
                return;
            }
            face.charge = Mathf.Max(face.charge, 0f) + def.F("refill");
            GameSession.RemoveStack(stack);
            GameMessages.Show("Поменял фильтр противогаза");
        }
        else if (def.B("radio"))
            GameWindows.Open("radio");
    }

    // Книга учит рецептам, даёт опыт навыка и слова для словарика. Читается один раз.
    static void Read(ItemStack stack)
    {
        var data = GameSession.Data;
        if (data.readBooks.Contains(stack.id))
        {
            GameMessages.Show("Уже прочитано");
            return;
        }
        data.readBooks.Add(stack.id);
        var def = stack.Def;
        foreach (var recipe in def.List("unlock"))
            GameSession.LearnRecipe(recipe);
        if (def.raw["xp"] is Newtonsoft.Json.Linq.JArray xp && xp.Count == 2)
            GameSession.AddXp(xp[0].ToString(), xp[1].ToObject<float>());
        foreach (var word in def.List("glossary"))
            GameSession.LearnWord(word);
        data.mood = Mathf.Min(100f, data.mood + 8f);
        GameMessages.Show($"Прочитал: {def.name}");
    }

    // Выбросить на землю: предмет можно будет подобрать обратно.
    public static void Drop(ItemStack stack, Vector3 position)
    {
        if (stack.Def != null && stack.Def.B("quest"))
        {
            GameMessages.Show("Это нужно для дела, лучше не выбрасывать");
            return;
        }
        foreach (var slot in GameSession.Slots)
            if (GameSession.Equipped(slot) == stack)
                GameSession.Unequip(slot);
        GameSession.Data.items.Remove(stack);
        GameSession.NotifyChanged();
        Collectible.Spawn(stack, position);
    }
}
