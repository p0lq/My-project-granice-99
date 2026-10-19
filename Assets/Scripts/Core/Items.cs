using System.Collections.Generic;

// Сюжетные предметы, на которые ссылаются код и Story.json. Все остальные предметы
// (их около 80) живут только в данных Resources/Data/Legacy/items.json по строковому id.
public enum ItemId
{
    Water,
    Batteries,
    BunkerKey,
    Antidote,
    UzbekNote,
    Scrap,
}

public static class Items
{
    static readonly Dictionary<ItemId, string> keys = new()
    {
        { ItemId.Water, "water_clean" },
        { ItemId.Batteries, "batteries" },
        { ItemId.BunkerKey, "bunker_key" },
        { ItemId.Antidote, "antidote" },
        { ItemId.UzbekNote, "note_uz" },
        { ItemId.Scrap, "scrap" },
    };

    public static string Key(ItemId id) => keys[id];

    public static string Name(ItemId id) => GameData.ItemName(Key(id));

    public static string Name(string id) => GameData.ItemName(id);

    // Сколько заряда фонарика (из 100) даёт одна пачка батареек.
    public const float BatteryCharge = 60f;
}
