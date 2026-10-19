using UnityEngine;
using UnityEngine.UI;

// Окно здоровья [H]: шкалы потребностей, состояния («Голод», «Лихорадка»…) и раны.
// У каждой раны кнопки лечения тем, что есть в инвентаре: бинт, тряпка, зелёнка, шина.
public class HealthWindow : GameWindows.Window
{
    Text stats;
    RectTransform wounds;

    public override void Build(Transform canvas)
    {
        var box = UIFactory.Window(canvas, "Здоровье и раны", new Vector2(1300f, 800f), out root, GameWindows.Close);
        stats = UIFactory.Text("Состояние", box, "", 22, TextAnchor.UpperLeft);
        stats.supportRichText = true;
        UIFactory.Place(stats.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -90f), new Vector2(520f, 680f));
        var title = UIFactory.Text("Раны", box, "Раны", 26, TextAnchor.UpperLeft);
        UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(580f, -90f), new Vector2(400f, 34f));
        wounds = UIFactory.ScrollList(box, new Vector2(0f, 1f), new Vector2(580f, -130f), new Vector2(690f, 640f), 10f);
    }

    public override void Refresh()
    {
        var s = GameSession.Data;
        var health = GameObject.FindWithTag("Player")?.GetComponent<Health>();
        var needs = GameObject.FindWithTag("Player")?.GetComponent<PlayerNeeds>();
        string Bar(string name, float v, bool inverted = false)
        {
            float bad = inverted ? 100f - v : v;
            string color = bad > 70f ? "#e06050" : bad > 40f ? "#e0b050" : "#a0d080";
            return $"{name}: <color={color}>{v:0}</color>";
        }

        var lines = new System.Collections.Generic.List<string>
        {
            $"<b>{s.heroName}</b>   {WorldClock.Text}",
            "",
            health != null ? $"Здоровье: {health.Current:0} / {health.Max:0}" : "",
            Bar("Голод", s.hunger),
            Bar("Жажда", s.thirst),
            Bar("Усталость", s.fatigue),
            Bar("Настроение", s.mood, true),
            $"Температура тела: {s.temp:0.0} °C",
            Bar("Пыльная болезнь", s.disease),
            Bar("Отравление", s.poison),
            Bar("Боль", s.pain) + (s.painkiller > 0f ? $"  (анальгин ещё {s.painkiller / 60f:0.#} ч)" : ""),
            "",
            "<b>Состояния</b>",
        };
        if (needs != null)
            foreach (var (text, level) in needs.Moodles())
                lines.Add(new string('!', level) + " " + text);
        lines.Add("");
        lines.Add("Спать — на кровати [E]. Пыльная болезнь лечится антидотом «ПФ-2», в пыли спасает респиратор.");
        stats.text = string.Join("\n", lines);

        UIFactory.Clear(wounds);
        if (s.wounds.Count == 0)
            UIFactory.Line(wounds, "Ран нет.");
        foreach (var w in s.wounds)
        {
            var state = new System.Collections.Generic.List<string>();
            if (w.bleeding) state.Add("<color=#e06050>кровоточит</color>");
            if (w.bandaged) state.Add(w.dirty ? "перевязано тряпкой" : "перевязано");
            if (w.disinfected) state.Add("обработано");
            if (w.splinted) state.Add("шина");
            if (w.inflamed) state.Add("<color=#e0b050>воспалилась</color>");
            UIFactory.Line(wounds, $"<b>{w.type}</b> — {w.part}. {string.Join(", ", state)}. Заживёт через {w.left / 60f:0} ч");

            var row = UIFactory.Rect("Лечение", wounds);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            var wound = w;
            if (!w.bandaged)
            {
                Treatment(row, wound, needs, "bandage", "Бинт", "bandage");
                Treatment(row, wound, needs, "bandage_home", "Самод. бинт", "bandage");
                Treatment(row, wound, needs, "rag", "Тряпка", "bandage_dirty");
            }
            if (!w.disinfected)
                Treatment(row, wound, needs, "antiseptic", "Зелёнка", "disinfect");
            if (w.type == "Перелом" && !w.splinted)
                Treatment(row, wound, needs, "splint", "Шина", "splint");
        }
    }

    void Treatment(Transform row, Wound wound, PlayerNeeds needs, string item, string label, string med)
    {
        if (!GameSession.Has(item) || needs == null)
            return;
        UIFactory.SmallButton(row, $"{label} ({GameSession.Count(item)})", () =>
        {
            GameMessages.Show(needs.Treat(wound, med));
            GameSession.Remove(item);
        }, 170f);
    }
}
