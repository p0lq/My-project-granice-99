using System;
using System.Collections.Generic;
using UnityEngine;

// Диалоги и задания. Тексты лежат в Assets/Resources/Data/Story.json и переносятся
// из исходной игры на Python почти без изменений, поэтому их удобно править без кода.
//
// Условия (conditions) — когда показывать вариант ответа:
//   quest:water=0   quest:well<3   quest:bunker>=1   item:Water>=3   flag:имя   !flag:имя
// Последствия (effects) — что случается при выборе:
//   quest:water=1  give:Water:1  take:Water:3  flag:имя  heal:30  objective:текст  message:текст
//   learn:рецепт  glossary:слово  xp:навык:количество  trade:торговец
// Предмет — имя из ItemId (Water, Scrap…) или любой id из items.json (tushenka, bandage…).
public static class Story
{
    [Serializable]
    public class File
    {
        public DialogueDef[] dialogues;
        public QuestDef[] quests;
        public string startObjective;
        public string afterDustObjective;
    }

    [Serializable]
    public class DialogueDef
    {
        public string id;
        public string start = "root";
        public NodeDef[] nodes;
    }

    [Serializable]
    public class NodeDef
    {
        public string id;
        public string text;
        public string[] effects;
        public OptionDef[] options;
    }

    [Serializable]
    public class OptionDef
    {
        public string text;
        public string[] conditions;
        public string[] effects;
        public string next;
    }

    // stages[0] — пустая строка (задание не начато), последняя стадия — выполнено.
    [Serializable]
    public class QuestDef
    {
        public string id;
        public string name;
        public string[] stages;
    }

    static File data;

    static File Data
    {
        get
        {
            if (data == null)
            {
                var asset = Resources.Load<TextAsset>("Data/Story");
                data = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
                data.dialogues ??= new DialogueDef[0];
                data.quests ??= new QuestDef[0];
            }
            return data;
        }
    }

    public static IReadOnlyList<QuestDef> Quests => Data.quests;

    public static void StartNewGame()
    {
        GameSession.AdvanceQuest("dust", 1);
        GameSession.Objective = Data.startObjective ?? "";
    }

    // Текущая цель для HUD. Выживших нашли, а задание ещё не взято — подсказываем, к кому идти.
    public static string CurrentObjective()
    {
        string objective = GameSession.Objective;
        if (objective == Data.startObjective && GameSession.QuestStage("dust") >= 2 && !string.IsNullOrEmpty(Data.afterDustObjective))
            return Data.afterDustObjective;
        return objective;
    }

    public static DialogueDef Dialogue(string id) => Array.Find(Data.dialogues, d => d.id == id);

    public static NodeDef Node(DialogueDef dialogue, string id) =>
        dialogue == null ? null : Array.Find(dialogue.nodes, n => n.id == id);

    public static bool Allowed(OptionDef option)
    {
        if (option.conditions == null)
            return true;
        foreach (var condition in option.conditions)
            if (!Check(condition))
                return false;
        return true;
    }

    public static void Apply(string[] effects)
    {
        if (effects == null)
            return;
        foreach (var effect in effects)
            Apply(effect);
    }

    public static bool Check(string condition)
    {
        bool negate = condition.StartsWith("!");
        if (negate)
            condition = condition.Substring(1);

        bool result;
        if (condition.StartsWith("flag:"))
            result = GameSession.HasFlag(condition.Substring(5));
        else if (condition.StartsWith("quest:"))
            result = Compare(condition.Substring(6), GameSession.QuestStage);
        else if (condition.StartsWith("item:"))
            result = Compare(condition.Substring(5), name => GameSession.Count(ItemKey(name)));
        else
        {
            Debug.LogWarning("Непонятное условие в Story.json: " + condition);
            result = false;
        }
        return negate ? !result : result;
    }

    // «water>=3» → значение(water) >= 3. Двухсимвольные операторы проверяем первыми.
    static bool Compare(string expression, Func<string, int> value)
    {
        foreach (var op in new[] { ">=", "<=", "=", "<", ">" })
        {
            int at = expression.IndexOf(op, StringComparison.Ordinal);
            if (at < 0)
                continue;
            int left = value(expression.Substring(0, at));
            int right = int.Parse(expression.Substring(at + op.Length));
            return op switch
            {
                ">=" => left >= right,
                "<=" => left <= right,
                "=" => left == right,
                "<" => left < right,
                _ => left > right,
            };
        }
        return false;
    }

    static void Apply(string effect)
    {
        int colon = effect.IndexOf(':');
        string kind = colon < 0 ? effect : effect.Substring(0, colon);
        string arg = colon < 0 ? "" : effect.Substring(colon + 1);

        switch (kind)
        {
            case "quest":
                var parts = arg.Split('=');
                GameSession.AdvanceQuest(parts[0], int.Parse(parts[1]));
                break;
            case "give":
            case "take":
                var item = arg.Split(':');
                string id = ItemKey(item[0]);
                if (GameData.Item(id) == null)
                {
                    Debug.LogWarning("Нет такого предмета: " + item[0]);
                    break;
                }
                int amount = item.Length > 1 ? int.Parse(item[1]) : 1;
                if (kind == "give")
                {
                    GameSession.Add(id, amount);
                    GameMessages.Show(amount > 1 ? $"Получено: {Items.Name(id)} ×{amount}" : $"Получено: {Items.Name(id)}");
                }
                else
                {
                    GameSession.Remove(id, amount);
                    GameMessages.Show(amount > 1 ? $"Отдано: {Items.Name(id)} ×{amount}" : $"Отдано: {Items.Name(id)}");
                }
                break;
            case "learn":
                GameSession.LearnRecipe(arg);
                break;
            case "glossary":
                GameSession.LearnWord(arg);
                break;
            case "xp":
                var xp = arg.Split(':');
                GameSession.AddXp(xp[0], xp.Length > 1 ? float.Parse(xp[1], System.Globalization.CultureInfo.InvariantCulture) : 20f);
                break;
            case "trade":
                // Окно обмена откроется, когда закроется разговор.
                PendingTrade = arg;
                break;
            case "flag":
                GameSession.SetFlag(arg);
                break;
            case "heal":
                var player = GameObject.FindWithTag("Player");
                if (player != null && player.TryGetComponent(out Health health))
                    health.Heal(float.Parse(arg, System.Globalization.CultureInfo.InvariantCulture));
                break;
            case "objective":
                GameSession.Objective = arg;
                GameMessages.Show("Новая цель: " + arg);
                break;
            case "message":
                GameMessages.Show(arg);
                break;
            default:
                Debug.LogWarning("Непонятное последствие в Story.json: " + effect);
                break;
        }
    }

    // Торговец, с которым после разговора надо открыть обмен (последствие trade:…).
    public static string PendingTrade { get; set; }

    static string ItemKey(string name) => Enum.TryParse(name, out ItemId id) ? Items.Key(id) : name;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        data = null;
        PendingTrade = null;
    }
}
