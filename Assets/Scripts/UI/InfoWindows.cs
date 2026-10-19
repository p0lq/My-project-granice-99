using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

// Журнал [J]: задания, навыки с опытом, черты персонажа.
public class JournalWindow : GameWindows.Window
{
    Text quests;
    Text skills;

    public override void Build(Transform canvas)
    {
        var box = UIFactory.Window(canvas, "Журнал", new Vector2(1400f, 820f), out root, GameWindows.Close);
        var q = UIFactory.ScrollList(box, new Vector2(0f, 1f), new Vector2(30f, -86f), new Vector2(780f, 700f));
        quests = UIFactory.Line(q, "");
        var s = UIFactory.ScrollList(box, new Vector2(0f, 1f), new Vector2(840f, -86f), new Vector2(530f, 700f));
        skills = UIFactory.Line(s, "");
    }

    public override void Refresh()
    {
        var sb = new System.Text.StringBuilder("<b>Задания</b>\n\n");
        foreach (var quest in Story.Quests)
        {
            int stage = GameSession.QuestStage(quest.id);
            if (stage <= 0 || quest.stages == null)
                continue;
            bool done = stage >= quest.stages.Length - 1;
            sb.Append(done ? "<color=#a0d080>" : "<color=#e8c070>").Append(quest.name).Append("</color>\n")
              .Append(quest.stages[Mathf.Min(stage, quest.stages.Length - 1)]).Append("\n\n");
        }
        quests.text = sb.ToString();

        var data = GameSession.Data;
        var sk = new System.Text.StringBuilder($"<b>{data.heroName}</b>\n\n<b>Навыки</b>\n");
        foreach (var pair in GameData.Skills)
        {
            data.skills.TryGetValue(pair.Key, out var state);
            int lvl = state?.lvl ?? 0;
            float need = 60f * (lvl + 1);
            sk.Append($"{pair.Value}: {lvl}").Append(lvl < 10 ? $"   ({state?.xp ?? 0:0}/{need:0})" : "").Append('\n');
        }
        sk.Append("\n<b>Черты</b>\n");
        if (data.traits.Count == 0)
            sk.Append("нет\n");
        foreach (var t in data.traits)
            if (GameData.Traits.TryGetValue(t, out var def))
                sk.Append($"{def.name} — {def.desc}\n");
        skills.text = sk.ToString();
    }
}

// Словарик [G]: казахские и узбекские слова, которые герой услышал или прочитал.
public class GlossaryWindow : GameWindows.Window
{
    Text text;

    public override void Build(Transform canvas)
    {
        var box = UIFactory.Window(canvas, "Словарик", new Vector2(1000f, 760f), out root, GameWindows.Close);
        var list = UIFactory.ScrollList(box, new Vector2(0f, 1f), new Vector2(30f, -86f), new Vector2(940f, 640f));
        text = UIFactory.Line(list, "");
    }

    public override void Refresh()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var lang in new[] { ("kk", "Казахский"), ("uz", "Узбекский") })
        {
            sb.Append($"<b>{lang.Item2}</b>\n");
            int n = 0;
            foreach (var word in GameSession.Data.glossary)
                if (GameData.Glossary.TryGetValue(word, out var w) && w.lang == lang.Item1)
                {
                    sb.Append($"{w.word} — {w.ru}\n");
                    n++;
                }
            if (n == 0)
                sb.Append("пока ни одного слова\n");
            sb.Append('\n');
        }
        sb.Append($"Выучено {GameSession.Data.glossary.Count} из {GameData.Glossary.Count}. Слова дают жители и книги.");
        text.text = sb.ToString();
    }
}

// Радио «Океан»: три станции, сообщения меняются по дням (radio.json старой игры).
public class RadioWindow : GameWindows.Window
{
    int station;
    Text title;
    Text body;

    public override void Build(Transform canvas)
    {
        var box = UIFactory.Window(canvas, "Радиоприёмник «Океан»", new Vector2(1000f, 600f), out root, GameWindows.Close);
        title = UIFactory.Text("Станция", box, "", 26, TextAnchor.UpperLeft);
        title.color = UIFactory.AccentColor;
        UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -90f), new Vector2(940f, 40f));
        body = UIFactory.Text("Эфир", box, "", 24, TextAnchor.UpperLeft);
        UIFactory.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -140f), new Vector2(940f, 330f));
        var prev = UIFactory.Button(box, "← Частота", () => Tune(-1));
        UIFactory.Place((RectTransform)prev.transform, new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(300f, 60f));
        var next = UIFactory.Button(box, "Частота →", () => Tune(1));
        UIFactory.Place((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(300f, 60f));
    }

    void Tune(int d)
    {
        int count = Mathf.Max(1, GameData.RadioStations.Count);
        station = (station + d + count) % count;
        Sfx.PlayUI(SoundId.Click, 0.4f);
        Refresh();
    }

    public override void Refresh()
    {
        var stations = GameData.RadioStations;
        if (stations.Count == 0)
        {
            body.text = "Тишина в эфире.";
            return;
        }
        var st = stations[station];
        title.text = st.Value<string>("name");
        // Самое свежее сообщение, которое уже должно звучать в этот день.
        string text = "...шипение...";
        foreach (var m in st["messages"])
            if (m.Value<int>("day") <= WorldClock.Day && !(m["front"]?.Value<bool>() ?? false))
                text = m.Value<string>("text");
        body.text = Glossed(text);
        GameSession.Data.radioHeard.Add($"{station}:{WorldClock.Day}");
    }

    // [[слово]] → слово (перевод), и слово попадает в словарик, как в старой игре.
    public static string Glossed(string text)
    {
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < text.Length)
        {
            int a = text.IndexOf("[[", i, System.StringComparison.Ordinal);
            if (a < 0) { sb.Append(text, i, text.Length - i); break; }
            int b = text.IndexOf("]]", a, System.StringComparison.Ordinal);
            if (b < 0) { sb.Append(text, i, text.Length - i); break; }
            sb.Append(text, i, a - i);
            string word = text.Substring(a + 2, b - a - 2);
            GameSession.LearnWord(word);
            sb.Append(GameData.Glossary.TryGetValue(word, out var w) ? $"<i>{word}</i> ({w.ru})" : word);
            i = b + 2;
        }
        return sb.ToString();
    }
}

// Записка: оригинал и перевод. Перевод виден, если навык «Языки» не ниже нужного
// или записку уже перевёл Бахтияр (стадия задания «Колодец» ≥ 3).
public class NoteWindow : GameWindows.Window
{
    static string noteId;
    Text body;

    public static void Show(string id)
    {
        noteId = id;
        GameWindows.Open("note");
    }

    public override void Build(Transform canvas)
    {
        var box = UIFactory.Window(canvas, "Записка", new Vector2(900f, 640f), out root, GameWindows.Close);
        body = UIFactory.Text("Текст", box, "", 24, TextAnchor.UpperLeft);
        body.supportRichText = true;
        UIFactory.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -90f), new Vector2(840f, 520f));
    }

    public override void Refresh()
    {
        var def = GameData.Item(noteId);
        if (def == null)
            return;
        int req = (int)def.F("req");
        bool translated = noteId == "note_uz" && GameSession.QuestStage("well") >= 3;
        bool canRead = GameSession.Skill("languages") >= req || translated || def.S("lang") == "ru";
        string orig = def.S("orig");
        var sb = new System.Text.StringBuilder($"<b>{def.name}</b>\n\n");
        if (!string.IsNullOrEmpty(orig))
            sb.Append($"<i>{orig}</i>\n\n");
        if (canRead)
            sb.Append(def.S("ru"));
        else
            sb.Append($"<color=#e0b050>Не понять: нужен навык «Языки» {req} или переводчик.</color>");
        body.text = sb.ToString();
        if (canRead)
            foreach (var e in def.List("on_read"))
                if (e.StartsWith("glossary:"))
                    GameSession.LearnWord(e.Substring(9));
    }
}
