using System.Collections.Generic;

// Все предметы игры. Их немного, поэтому хватает списка в коде без ScriptableObject.
public enum ItemId
{
    Water,
    Batteries,
    BunkerKey,
    Antidote,
}

public static class Items
{
    static readonly Dictionary<ItemId, string> names = new()
    {
        { ItemId.Water, "Бутылка воды" },
        { ItemId.Batteries, "Батарейки «Электрон»" },
        { ItemId.BunkerKey, "Ключ от люка «Подземки»" },
        { ItemId.Antidote, "Антидот «ПФ-2»" },
    };

    public static string Name(ItemId id) => names[id];

    // Сколько здоровья возвращает одна бутылка воды.
    public const float WaterHeal = 30f;
    // Сколько заряда фонарика (из 100) даёт одна пачка батареек.
    public const float BatteryCharge = 60f;
}
