using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

// Предмет в инвентаре. У оружия прочность (dur), у респиратора и противогаза запас фильтра (charge).
[Serializable]
public class ItemStack
{
    public string id;
    public int count = 1;
    public float dur = -1f;
    public float charge = -1f;

    [JsonIgnore] public GameData.ItemDef Def => GameData.Item(id);

    public ItemStack Clone() => (ItemStack)MemberwiseClone();
}

[Serializable]
public class Wound
{
    public string type;      // Порез, Укус, Ушиб, Перелом
    public string part;      // голова, торс, левая рука…
    public bool bleeding;
    public bool bandaged;
    public bool dirty;       // перевязано грязной тряпкой
    public bool disinfected;
    public bool splinted;
    public bool inflamed;
    public float left;       // игровых минут до заживления
}

[Serializable]
public class SkillState
{
    public int lvl;
    public float xp;
}

// Всё, что знает игра о герое и его прогрессе. Один объект: его копирует контрольная точка
// уровня и целиком пишет в файл сохранение (SaveSystem).
[Serializable]
public class SessionData
{
    public string heroName = "Выживший";
    public List<string> traits = new();
    public Dictionary<string, SkillState> skills = new();

    public List<ItemStack> items = new();
    public Dictionary<string, ItemStack> equip = new();
    public List<string> knownRecipes = new();
    public List<string> readBooks = new();

    public float health = GameSession.StartHealth;
    public float battery = GameSession.StartBattery;
    public float hunger = 10f, thirst = 15f, fatigue = 10f, mood = 60f, temp = 36.6f;
    public float disease, poison, pain, painkiller;
    public float sinceSmoke;
    public List<Wound> wounds = new();

    public Dictionary<string, int> quests = new();
    public HashSet<string> flags = new();
    public string objective = "";

    // Игровое время: минут с полуночи первого дня. Игра начинается в 8 утра.
    public float minutes = 8 * 60f;
    public HashSet<string> glossary = new();
    public HashSet<string> radioHeard = new();
    // Что осталось у торговцев (Галя, Ким). Заполняется из npcs.json при первом обмене.
    public Dictionary<string, Dictionary<string, int>> traderStock = new();
}

// Состояние героя, которое переживает смену сцен. Обычные объекты сцены уничтожаются
// при загрузке следующей, а статический класс нет.
//
// При входе на уровень GameManager делает снимок (Checkpoint). «Заново» в меню
// проигрыша возвращает этот снимок, чтобы игрок начинал уровень с тем, с чем пришёл.
public static class GameSession
{
    public const float StartHealth = 100f;
    public const float StartBattery = 100f;
    public const float BaseCarry = 10f;

    public static readonly string[] Slots = { "weapon", "head", "body", "face", "back", "light" };

    public static SessionData Data { get; private set; } = new();
    static string checkpoint;

    public static float Health { get => Data.health; set => Data.health = value; }
    public static float Battery { get => Data.battery; set => Data.battery = value; }
    public static string Objective { get => Data.objective; set => Data.objective = value; }

    public static event Action InventoryChanged;
    public static event Action<string> WordLearned;

    // В проекте выключена перезагрузка домена при входе в Play (Project Settings → Editor →
    // Enter Play Mode Settings), поэтому статические поля сами между запусками не сбрасываются.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        InventoryChanged = null;
        WordLearned = null;
        NewGame("Выживший", new List<string>());
    }

    public static void NewGame(string name, List<string> traits)
    {
        Data = new SessionData { heroName = name, traits = new List<string>(traits) };
        foreach (var skill in GameData.Skills.Keys)
            Data.skills[skill] = new SkillState();
        foreach (var t in traits)
            if (GameData.Traits.TryGetValue(t, out var def))
                foreach (var s in def.skills)
                    if (Data.skills.ContainsKey(s.Key))
                        Data.skills[s.Key].lvl += s.Value;
        foreach (var r in GameData.Recipes.Values)
            if (r.known)
                Data.knownRecipes.Add(r.id);

        // Стартовый набор: нож и монтировка в руках, немного воды и еды, фонарик.
        Add("knife");
        Add("crowbar");
        Equip(FindStack("knife"));
        Add("water_clean");
        Add("lepeshka", 2);
        Add("bandage");
        Add("matches");
        Add("flashlight");
        Equip(FindStack("flashlight"));
        Story.StartNewGame();
        Checkpoint();
    }

    public static void Checkpoint() => checkpoint = Serialize();

    public static void RestoreCheckpoint()
    {
        if (checkpoint != null)
            Deserialize(checkpoint);
    }

    public static string Serialize() => JsonConvert.SerializeObject(Data);

    public static void Deserialize(string json)
    {
        Data = JsonConvert.DeserializeObject<SessionData>(json) ?? new SessionData();
        InventoryChanged?.Invoke();
    }

    // ---------------------------------------------------------------- предметы

    public static int Count(string id)
    {
        int n = 0;
        foreach (var st in Data.items)
            if (st.id == id)
                n += st.count;
        foreach (var st in Data.equip.Values)
            if (st != null && st.id == id)
                n += st.count;
        return n;
    }

    public static int Count(ItemId id) => Count(Items.Key(id));
    public static bool Has(string id) => Count(id) > 0;
    public static bool Has(ItemId id) => Count(id) > 0;

    public static ItemStack FindStack(string id) => Data.items.Find(s => s.id == id);

    public static void Add(string id, int amount = 1)
    {
        var def = GameData.Item(id);
        if (def == null || amount <= 0)
            return;
        if (def.Stackable)
        {
            var st = FindStack(id);
            if (st != null)
                st.count += amount;
            else
                Data.items.Add(new ItemStack { id = id, count = amount });
        }
        else
            for (int i = 0; i < amount; i++)
                Data.items.Add(NewStack(id));
        InventoryChanged?.Invoke();
    }

    public static void Add(ItemId id, int amount = 1) => Add(Items.Key(id), amount);

    public static void AddStack(ItemStack stack)
    {
        if (stack.Def != null && stack.Def.Stackable)
            Add(stack.id, stack.count);
        else
        {
            Data.items.Add(stack);
            InventoryChanged?.Invoke();
        }
    }

    static ItemStack NewStack(string id)
    {
        var def = GameData.Item(id);
        var st = new ItemStack { id = id };
        if (def.Has("dur"))
            st.dur = def.F("dur");
        if (def.Has("filter"))
            st.charge = def.F("filter");
        return st;
    }

    public static bool Remove(string id, int amount = 1)
    {
        if (Count(id) < amount)
            return false;
        for (int i = Data.items.Count - 1; i >= 0 && amount > 0; i--)
        {
            var st = Data.items[i];
            if (st.id != id)
                continue;
            int take = Mathf.Min(st.count, amount);
            st.count -= take;
            amount -= take;
            if (st.count <= 0)
                Data.items.RemoveAt(i);
        }
        // Остаток снимаем с надетого (например, последний бинт не бывает надет, а вот нож — да).
        foreach (var slot in Slots)
            if (amount > 0 && Data.equip.TryGetValue(slot, out var st) && st != null && st.id == id)
            {
                Data.equip[slot] = null;
                amount--;
            }
        InventoryChanged?.Invoke();
        return true;
    }

    public static bool Remove(ItemId id, int amount = 1) => Remove(Items.Key(id), amount);

    public static void RemoveStack(ItemStack stack, int amount = 1)
    {
        stack.count -= amount;
        if (stack.count <= 0)
        {
            Data.items.Remove(stack);
            foreach (var slot in Slots)
                if (Equipped(slot) == stack)
                    Data.equip[slot] = null;
        }
        InventoryChanged?.Invoke();
    }

    // ---------------------------------------------------------------- экипировка

    public static ItemStack Equipped(string slot) => Data.equip.TryGetValue(slot, out var st) ? st : null;

    public static void Equip(ItemStack stack)
    {
        if (stack == null || stack.Def == null)
            return;
        string slot = stack.Def.Slot;
        if (string.IsNullOrEmpty(slot))
            return;
        Data.items.Remove(stack);
        var old = Equipped(slot);
        if (old != null)
            Data.items.Add(old);
        Data.equip[slot] = stack;
        InventoryChanged?.Invoke();
    }

    public static void Unequip(string slot)
    {
        var st = Equipped(slot);
        if (st == null)
            return;
        Data.equip[slot] = null;
        Data.items.Add(st);
        InventoryChanged?.Invoke();
    }

    public static void NotifyChanged() => InventoryChanged?.Invoke();

    // Надетое весит вдвое меньше, как в старой игре.
    public static float Weight()
    {
        float w = 0f;
        foreach (var st in Data.items)
            w += (st.Def?.weight ?? 0f) * st.count;
        foreach (var st in Data.equip.Values)
            if (st != null)
                w += (st.Def?.weight ?? 0f) * 0.5f;
        return w;
    }

    public static float CarryLimit()
    {
        float limit = BaseCarry;
        foreach (var t in Data.traits)
            if (GameData.Traits.TryGetValue(t, out var def))
                limit += def.carry;
        var back = Equipped("back");
        if (back != null)
            limit += back.Def.F("capacity");
        return limit;
    }

    public static float EquipSum(string key)
    {
        float sum = 0f;
        foreach (var st in Data.equip.Values)
            if (st != null && st.Def != null)
                sum += st.Def.F(key);
        return sum;
    }

    // ---------------------------------------------------------------- навыки и черты

    public static bool HasTrait(string trait) => Data.traits.Contains(trait);

    public static int Skill(string skill) => Data.skills.TryGetValue(skill, out var s) ? s.lvl : 0;

    // Навыки растут от использования. Как в старой игре: следующий уровень за 60 × (уровень + 1) опыта.
    public static void AddXp(string skill, float amount)
    {
        if (string.IsNullOrEmpty(skill))
            return;
        if (!Data.skills.TryGetValue(skill, out var s))
            Data.skills[skill] = s = new SkillState();
        if (s.lvl >= 10)
            return;
        s.xp += amount;
        float need = 60f * (s.lvl + 1);
        if (s.xp >= need)
        {
            s.xp -= need;
            s.lvl++;
            string name = GameData.Skills.TryGetValue(skill, out var n) ? n : skill;
            GameMessages.Show($"Навык «{name}» вырос до {s.lvl}");
        }
    }

    // ---------------------------------------------------------------- сюжет

    public static int QuestStage(string quest) => Data.quests.TryGetValue(quest, out int stage) ? stage : 0;

    // Задания только продвигаются вперёд: если записку нашли раньше, чем дошли до этого
    // места разговора, поздний диалог не откатит стадию назад.
    public static void AdvanceQuest(string quest, int stage)
    {
        if (stage > QuestStage(quest))
            Data.quests[quest] = stage;
    }

    public static IEnumerable<KeyValuePair<string, int>> Quests => Data.quests;

    public static bool HasFlag(string flag) => Data.flags.Contains(flag);

    public static void SetFlag(string flag) => Data.flags.Add(flag);

    public static void LearnWord(string word)
    {
        if (GameData.Glossary.ContainsKey(word) && Data.glossary.Add(word))
            WordLearned?.Invoke(word);
    }

    public static bool KnowsRecipe(string id) => Data.knownRecipes.Contains(id);

    public static void LearnRecipe(string id)
    {
        if (GameData.Recipes.TryGetValue(id, out var r) && !Data.knownRecipes.Contains(id))
        {
            Data.knownRecipes.Add(id);
            GameMessages.Show($"Новый рецепт: {r.name}");
        }
    }
}
