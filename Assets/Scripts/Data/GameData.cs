using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Данные из исходной игры на Python: Assets/Resources/Data/Legacy/*.json.
// Файлы скопированы без изменений формата (словари по id), поэтому их можно править
// так же, как в старой игре. Читает их Newtonsoft JSON: встроенный JsonUtility словари не умеет.
public static class GameData
{
    public class ItemDef
    {
        public string id;
        public string name;
        public string type;
        public string desc;
        public float weight;
        public int value;
        public JObject raw;

        public bool Has(string key) => raw[key] != null;
        public float F(string key, float fallback = 0f) => raw[key] != null ? raw[key].Value<float>() : fallback;
        public string S(string key) => raw[key]?.Value<string>();
        public bool B(string key) => raw[key] != null && raw[key].Value<bool>();
        public string[] List(string key) => raw[key] is JArray a ? a.ToObject<string[]>() : new string[0];

        // Можно ли складывать в одну стопку. Оружие и одежда хранят своё состояние (прочность, фильтр).
        public bool Stackable => type != "weapon" && type != "gun" && type != "clothing";
        public string Slot => type == "weapon" || type == "gun" ? "weapon" : S("slot");
    }

    public class RecipeDef
    {
        public string id;
        public string name;
        public Dictionary<string, int> inputs = new();
        public Dictionary<string, int> outputs = new();
        public string[] tools = new string[0];
        public string station;
        public string place;
        public string skill;
        public float time;
        public bool known;
    }

    public class LootEntry
    {
        public string id;
        public float weight;
        public int min;
        public int max;
    }

    public class LootTable
    {
        public int minRolls;
        public int maxRolls;
        public List<LootEntry> items = new();
    }

    public class TraitDef
    {
        public string id;
        public string name;
        public int cost;
        public string desc;
        public float carry;
        public Dictionary<string, int> skills = new();
    }

    public class Word
    {
        public string word;
        public string lang;
        public string ru;
    }

    static bool loaded;
    static readonly Dictionary<string, ItemDef> items = new();
    static readonly Dictionary<string, RecipeDef> recipes = new();
    static readonly Dictionary<string, LootTable> loot = new();
    static readonly Dictionary<string, TraitDef> traits = new();
    static readonly Dictionary<string, string> skills = new();
    static readonly Dictionary<string, Word> glossary = new();
    static readonly Dictionary<string, Dictionary<string, int>> npcStock = new();
    static JArray radioStations = new();
    static int startPoints;

    public static IReadOnlyDictionary<string, ItemDef> Items { get { Load(); return items; } }
    public static IReadOnlyDictionary<string, RecipeDef> Recipes { get { Load(); return recipes; } }
    public static IReadOnlyDictionary<string, TraitDef> Traits { get { Load(); return traits; } }
    public static IReadOnlyDictionary<string, string> Skills { get { Load(); return skills; } }
    public static IReadOnlyDictionary<string, Word> Glossary { get { Load(); return glossary; } }
    public static JArray RadioStations { get { Load(); return radioStations; } }
    public static int StartPoints { get { Load(); return startPoints; } }

    public static ItemDef Item(string id)
    {
        Load();
        return id != null && items.TryGetValue(id, out var def) ? def : null;
    }

    public static string ItemName(string id) => Item(id)?.name ?? id;

    public static Dictionary<string, int> NpcStock(string npc)
    {
        Load();
        return npcStock.TryGetValue(npc, out var s) ? new Dictionary<string, int>(s) : new Dictionary<string, int>();
    }

    // Бросок по таблице добычи, как в старой игре: rolls раз выбираем предмет по весу шанса.
    public static List<(string id, int count)> RollLoot(string table, System.Random rnd)
    {
        Load();
        var result = new List<(string, int)>();
        if (string.IsNullOrEmpty(table) || !loot.TryGetValue(table, out var t) || t.items.Count == 0)
            return result;
        float total = 0f;
        foreach (var e in t.items)
            total += e.weight;
        int rolls = rnd.Next(t.minRolls, t.maxRolls + 1);
        for (int i = 0; i < rolls; i++)
        {
            float pick = (float)rnd.NextDouble() * total;
            foreach (var e in t.items)
            {
                pick -= e.weight;
                if (pick > 0f)
                    continue;
                if (Item(e.id) != null)
                    result.Add((e.id, rnd.Next(e.min, e.max + 1)));
                break;
            }
        }
        return result;
    }

    static void Load()
    {
        if (loaded)
            return;
        loaded = true;

        foreach (var pair in Read("items"))
        {
            if (pair.Key.StartsWith("_") || pair.Value is not JObject o)
                continue;
            items[pair.Key] = new ItemDef
            {
                id = pair.Key,
                name = o.Value<string>("name"),
                type = o.Value<string>("type"),
                desc = o.Value<string>("desc") ?? "",
                weight = o["w"]?.Value<float>() ?? 0f,
                value = o["value"]?.Value<int>() ?? 0,
                raw = o,
            };
        }

        foreach (var pair in Read("recipes"))
        {
            if (pair.Key.StartsWith("_") || pair.Value is not JObject o)
                continue;
            recipes[pair.Key] = new RecipeDef
            {
                id = pair.Key,
                name = o.Value<string>("name"),
                inputs = o["in"]?.ToObject<Dictionary<string, int>>() ?? new(),
                outputs = o["out"]?.ToObject<Dictionary<string, int>>() ?? new(),
                tools = o["tools"]?.ToObject<string[]>() ?? new string[0],
                station = o.Value<string>("station"),
                place = o.Value<string>("place"),
                skill = o.Value<string>("skill"),
                time = o["time"]?.Value<float>() ?? 3f,
                known = o["known"]?.Value<bool>() ?? false,
            };
        }

        foreach (var pair in Read("loot"))
        {
            if (pair.Key.StartsWith("_") || pair.Value is not JObject o)
                continue;
            var rolls = o["rolls"] as JArray;
            var table = new LootTable { minRolls = rolls?[0].Value<int>() ?? 0, maxRolls = rolls?[1].Value<int>() ?? 0 };
            if (o["items"] is JArray list)
                foreach (var row in list)
                    table.items.Add(new LootEntry { id = row[0].Value<string>(), weight = row[1].Value<float>(), min = row[2].Value<int>(), max = row[3].Value<int>() });
            loot[pair.Key] = table;
        }

        var character = Read("character");
        if (character["skills"] is JObject sk)
            foreach (var p in sk)
                skills[p.Key] = p.Value.Value<string>();
        startPoints = character["start_points"]?.Value<int>() ?? 2;
        if (character["traits"] is JObject tr)
            foreach (var p in tr)
            {
                var o = (JObject)p.Value;
                traits[p.Key] = new TraitDef
                {
                    id = p.Key,
                    name = o.Value<string>("name"),
                    cost = o.Value<int>("cost"),
                    desc = o.Value<string>("desc") ?? "",
                    carry = o["carry"]?.Value<float>() ?? 0f,
                    skills = o["skills"]?.ToObject<Dictionary<string, int>>() ?? new(),
                };
            }

        foreach (var pair in Read("glossary"))
            if (!pair.Key.StartsWith("_") && pair.Value is JObject o)
                glossary[pair.Key] = new Word { word = pair.Key, lang = o.Value<string>("lang"), ru = o.Value<string>("ru") };

        foreach (var pair in Read("npcs"))
            if (!pair.Key.StartsWith("_") && pair.Value is JObject o && o["stock"] is JObject stock)
                npcStock[pair.Key] = stock.ToObject<Dictionary<string, int>>();

        radioStations = Read("radio")["stations"] as JArray ?? new JArray();
    }

    static JObject Read(string name)
    {
        var asset = Resources.Load<TextAsset>("Data/Legacy/" + name);
        if (asset == null)
        {
            Debug.LogError($"Нет файла данных Resources/Data/Legacy/{name}.json");
            return new JObject();
        }
        return JObject.Parse(asset.text);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        loaded = false;
        items.Clear();
        recipes.Clear();
        loot.Clear();
        traits.Clear();
        skills.Clear();
        glossary.Clear();
        npcStock.Clear();
    }
}
