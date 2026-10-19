using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Обмен с торговцем (Галя, Ким), как в старой игре: деньги — бумага, меняются вещами
// по ценности (value в items.json). Слева твоё, справа его товар; клик кладёт одну штуку
// в сделку, повторный клик по строке сделки убирает её. Обмен проходит, если отданное
// стоит не меньше взятого. Навык «Торговля» поднимает цену твоих вещей.
public class TradeWindow : GameWindows.Window
{
    public static string Trader;

    RectTransform mine;
    RectTransform theirs;
    RectTransform deal;
    Text balance;
    Button confirm;
    readonly Dictionary<string, int> give = new();
    readonly Dictionary<string, int> take = new();

    static readonly Dictionary<string, string> TraderNames = new() { { "galya", "Тётя Галя" }, { "kim", "Виктор Ким" } };

    public override void Build(Transform canvas)
    {
        var box = UIFactory.Window(canvas, "Обмен", new Vector2(1500f, 820f), out root, GameWindows.Close);
        Header(box, "Твои вещи (клик — отдать)", 30f);
        Header(box, "Товар (клик — взять)", 540f);
        Header(box, "Сделка", 1050f);
        mine = UIFactory.ScrollList(box, new Vector2(0f, 1f), new Vector2(30f, -130f), new Vector2(480f, 600f), 4f);
        theirs = UIFactory.ScrollList(box, new Vector2(0f, 1f), new Vector2(540f, -130f), new Vector2(480f, 600f), 4f);
        deal = UIFactory.ScrollList(box, new Vector2(0f, 1f), new Vector2(1050f, -130f), new Vector2(420f, 470f), 4f);
        balance = UIFactory.Text("Баланс", box, "", 22, TextAnchor.UpperLeft);
        balance.supportRichText = true;
        UIFactory.Place(balance.rectTransform, new Vector2(0f, 1f), new Vector2(1050f, -610f), new Vector2(420f, 60f));
        confirm = UIFactory.Button(box, "Обменять", Confirm);
        UIFactory.Place((RectTransform)confirm.transform, new Vector2(0f, 1f), new Vector2(1050f, -680f), new Vector2(420f, 60f));
    }

    static void Header(Transform box, string text, float x)
    {
        var t = UIFactory.Text("Заголовок", box, text, 22, TextAnchor.UpperLeft);
        UIFactory.Place(t.rectTransform, new Vector2(0f, 1f), new Vector2(x, -90f), new Vector2(480f, 34f));
    }

    Dictionary<string, int> Stock()
    {
        var all = GameSession.Data.traderStock;
        if (!all.TryGetValue(Trader ?? "", out var stock))
            all[Trader ?? ""] = stock = GameData.NpcStock(Trader);
        return stock;
    }

    // Твоя вещь у торговца стоит дешевле, чем его у тебя; навык торговли сокращает разницу.
    static float SellPrice(string id) => (GameData.Item(id)?.value ?? 0) * Mathf.Min(1f, 0.6f + 0.05f * GameSession.Skill("trade"));
    static float BuyPrice(string id) => GameData.Item(id)?.value ?? 0;

    public override void Refresh()
    {
        var header = root.GetComponentInChildren<Text>();
        if (header != null)
            header.text = $"Обмен: {(Trader != null && TraderNames.TryGetValue(Trader, out var n) ? n : "торговец")}";

        UIFactory.Clear(mine);
        var counts = new Dictionary<string, int>();
        foreach (var st in GameSession.Data.items)
            counts[st.id] = (counts.TryGetValue(st.id, out int c) ? c : 0) + st.count;
        foreach (var pair in counts)
        {
            var def = GameData.Item(pair.Key);
            if (def == null || def.B("quest") || def.value <= 0)
                continue;
            int left = pair.Value - (give.TryGetValue(pair.Key, out int g) ? g : 0);
            if (left <= 0)
                continue;
            string id = pair.Key;
            Row(mine, $"{def.name} ×{left}   цена {SellPrice(id):0}", () => Add(give, id));
        }

        UIFactory.Clear(theirs);
        foreach (var pair in Stock())
        {
            int left = pair.Value - (take.TryGetValue(pair.Key, out int t) ? t : 0);
            if (left <= 0 || GameData.Item(pair.Key) == null)
                continue;
            string id = pair.Key;
            Row(theirs, $"{Items.Name(id)} ×{left}   цена {BuyPrice(id):0}", () => Add(take, id));
        }

        UIFactory.Clear(deal);
        foreach (var pair in give)
        {
            string id = pair.Key;
            Row(deal, $"Отдаю: {Items.Name(id)} ×{pair.Value}", () => Sub(give, id));
        }
        foreach (var pair in take)
        {
            string id = pair.Key;
            Row(deal, $"Беру: {Items.Name(id)} ×{pair.Value}", () => Sub(take, id));
        }

        float sum = Sum(give, SellPrice) - Sum(take, BuyPrice);
        balance.text = sum >= 0f ? $"Баланс: <color=#a0d080>+{sum:0}</color>" : $"Баланс: <color=#e06050>{sum:0}</color> — добавь своих вещей";
        confirm.interactable = sum >= 0f && (give.Count > 0 || take.Count > 0);
    }

    static float Sum(Dictionary<string, int> d, System.Func<string, float> price)
    {
        float s = 0f;
        foreach (var pair in d)
            s += price(pair.Key) * pair.Value;
        return s;
    }

    static void Row(Transform list, string text, UnityEngine.Events.UnityAction onClick)
    {
        var b = UIFactory.SmallButton(list, text, onClick, 440f);
        var label = b.GetComponentInChildren<Text>();
        label.alignment = TextAnchor.MiddleLeft;
        label.fontSize = 19;
        label.rectTransform.offsetMin = new Vector2(10f, 0f);
    }

    void Add(Dictionary<string, int> d, string id)
    {
        d[id] = (d.TryGetValue(id, out int n) ? n : 0) + 1;
        Refresh();
    }

    void Sub(Dictionary<string, int> d, string id)
    {
        if (d.TryGetValue(id, out int n) && n > 1) d[id] = n - 1;
        else d.Remove(id);
        Refresh();
    }

    void Confirm()
    {
        var stock = Stock();
        foreach (var pair in give)
        {
            GameSession.Remove(pair.Key, pair.Value);
            stock[pair.Key] = (stock.TryGetValue(pair.Key, out int n) ? n : 0) + pair.Value;
        }
        foreach (var pair in take)
        {
            GameSession.Add(pair.Key, pair.Value);
            stock[pair.Key] -= pair.Value;
            if (stock[pair.Key] <= 0)
                stock.Remove(pair.Key);
        }
        GameSession.AddXp("trade", 15f);
        give.Clear();
        take.Clear();
        GameMessages.Show("Честный обмен");
        Sfx.PlayUI(SoundId.Pickup, 0.3f);
        Refresh();
    }
}
