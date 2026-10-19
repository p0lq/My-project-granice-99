using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Игровой интерфейс: здоровье, заряд фонарика, предметы, цель уровня,
// подсказка взаимодействия, сообщения и красная вспышка при уроне.
public class GameHUD : MonoBehaviour
{
    [SerializeField] float messageLifetime = 4f;
    [SerializeField] int maxMessages = 4;

    struct Message
    {
        public Text text;
        public float expires;
    }

    Health health;
    Flashlight flashlight;
    PlayerInteractor interactor;
    PlayerMovement movement;
    PlayerCombat combat;
    PlayerNeeds needs;

    RectTransform healthFill;
    RectTransform batteryFill;
    RectTransform staminaFill;
    Image staminaFillImage;
    RectTransform cooldownFill;
    Text weaponText;
    Text healthText;
    Text itemsText;
    Text objectiveText;
    Text journalText;
    Text promptText;
    Image damageFlash;
    RectTransform messageColumn;
    readonly List<Message> messages = new();

    void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            health = player.GetComponent<Health>();
            flashlight = player.GetComponent<Flashlight>();
            interactor = player.GetComponent<PlayerInteractor>();
            movement = player.GetComponent<PlayerMovement>();
            combat = player.GetComponent<PlayerCombat>();
            needs = player.GetComponent<PlayerNeeds>();
            if (health != null)
                health.Damaged += OnDamaged;
        }

        Build();
        GameMessages.Posted += OnMessage;
        if (GameManager.Instance != null)
            GameManager.Instance.StateChanged += OnStateChanged;
    }

    // Статические события живут дольше сцены: без отписки после перезагрузки
    // уровня они вызывали бы методы уничтоженного HUD.
    void OnDestroy()
    {
        GameMessages.Posted -= OnMessage;
        if (health != null)
            health.Damaged -= OnDamaged;
        if (GameManager.Instance != null)
            GameManager.Instance.StateChanged -= OnStateChanged;
    }

    void Build()
    {
        var canvas = UIFactory.Canvas("HUD", 0).transform;

        damageFlash = UIFactory.Image("Вспышка урона", canvas, new Color(0.7f, 0f, 0f, 0f));
        UIFactory.Stretch(damageFlash.rectTransform);
        damageFlash.raycastTarget = false;

        var healthBar = UIFactory.Bar("Здоровье", canvas, new Color(0.7f, 0.15f, 0.12f), out healthFill);
        UIFactory.Place(healthBar, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(360f, 34f));
        healthText = UIFactory.Text("Число", healthBar, "", 22, TextAnchor.MiddleCenter);
        UIFactory.Stretch(healthText.rectTransform);

        var staminaBar = UIFactory.Bar("Выносливость", canvas, new Color(0.35f, 0.7f, 0.35f), out staminaFill);
        UIFactory.Place(staminaBar, new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(240f, 16f));
        staminaFillImage = staminaFill.GetComponent<Image>();
        var staminaLabel = UIFactory.Text("Подпись", canvas, "Бег [Shift]", 20, TextAnchor.MiddleLeft);
        UIFactory.Place(staminaLabel.rectTransform, new Vector2(0f, 1f), new Vector2(282f, -65f), new Vector2(200f, 26f));

        var batteryBar = UIFactory.Bar("Фонарик", canvas, new Color(0.9f, 0.8f, 0.35f), out batteryFill);
        UIFactory.Place(batteryBar, new Vector2(0f, 1f), new Vector2(30f, -94f), new Vector2(240f, 16f));
        var batteryLabel = UIFactory.Text("Подпись", canvas, "Фонарик [F]", 20, TextAnchor.MiddleLeft);
        UIFactory.Place(batteryLabel.rectTransform, new Vector2(0f, 1f), new Vector2(282f, -89f), new Vector2(200f, 26f));

        weaponText = UIFactory.Text("Оружие", canvas, "", 22, TextAnchor.UpperLeft);
        UIFactory.Place(weaponText.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -120f), new Vector2(600f, 30f));
        var cooldownBar = UIFactory.Bar("Перезарядка размаха", canvas, new Color(0.85f, 0.65f, 0.3f), out cooldownFill);
        UIFactory.Place(cooldownBar, new Vector2(0f, 1f), new Vector2(30f, -150f), new Vector2(240f, 8f));

        itemsText = UIFactory.Text("Предметы", canvas, "", 22, TextAnchor.UpperLeft);
        UIFactory.Place(itemsText.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -168f), new Vector2(700f, 300f));
        itemsText.supportRichText = true;

        objectiveText = UIFactory.Text("Цель", canvas, "", 28, TextAnchor.UpperCenter);
        objectiveText.color = UIFactory.AccentColor;
        UIFactory.Place(objectiveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 40f));

        journalText = UIFactory.Text("Задания", canvas, "", 20, TextAnchor.UpperRight);
        UIFactory.Place(journalText.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(520f, 400f));

        promptText = UIFactory.Text("Подсказка", canvas, "", 30, TextAnchor.MiddleCenter);
        UIFactory.Place(promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(900f, 50f));

        var controls = UIFactory.Text("Управление", canvas,
            "WASD идти  Shift бег  C красться  ЛКМ удар  1/2/3 оружие  R перезарядка  E действие  Q пить  I инвентарь  K крафт  H здоровье  J журнал  G словарь  F5/F9 сохранить/загрузить",
            18, TextAnchor.LowerRight);
        controls.color = new Color(1f, 1f, 1f, 0.55f);
        UIFactory.Place(controls.rectTransform, new Vector2(1f, 0f), new Vector2(-24f, 18f), new Vector2(1200f, 30f));

        messageColumn = UIFactory.Rect("Сообщения", canvas);
        UIFactory.Place(messageColumn, new Vector2(0f, 0f), new Vector2(30f, 30f), new Vector2(700f, 200f));
        var layout = messageColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.spacing = 4f;
    }

    void Update()
    {
        if (health != null)
        {
            UIFactory.SetBar(healthFill, health.Current / health.Max);
            healthText.text = $"{health.Current:0} / {health.Max:0}";
        }
        UIFactory.SetBar(batteryFill, GameSession.Battery / 100f);
        if (movement != null)
        {
            UIFactory.SetBar(staminaFill, movement.Stamina01);
            // Выбился из сил — полоска краснеет, пока не отдышится.
            staminaFillImage.color = movement.Exhausted ? new Color(0.7f, 0.25f, 0.2f) : new Color(0.35f, 0.7f, 0.35f);
        }
        if (combat != null)
        {
            weaponText.text = WeaponLine();
            // Полоска пустеет к готовности: пустая — можно бить снова.
            UIFactory.SetBar(cooldownFill, combat.CooldownLeft);
        }

        itemsText.text = ItemsLine();
        journalText.text = JournalText();
        objectiveText.text = GameManager.Instance != null ? GameManager.Instance.Objective : "";
        promptText.text = interactor != null && interactor.Current != null ? $"[E] {interactor.Current.Prompt}" : "";

        // Вспышка гаснет по реальному времени, чтобы не застыть на паузе.
        var c = damageFlash.color;
        c.a = Mathf.MoveTowards(c.a, 0f, Time.unscaledDeltaTime * 1.5f);
        damageFlash.color = c;

        for (int i = messages.Count - 1; i >= 0; i--)
        {
            float left = messages[i].expires - Time.unscaledTime;
            if (left <= 0f)
            {
                Destroy(messages[i].text.gameObject);
                messages.RemoveAt(i);
                continue;
            }
            var color = messages[i].text.color;
            color.a = Mathf.Clamp01(left);
            messages[i].text.color = color;
        }
    }

    // Часы, вода и батарейки под рукой, вес и состояния героя («мудлы» старой игры).
    string ItemsLine()
    {
        var sb = new System.Text.StringBuilder();
        float air = WorldClock.Instance != null && WorldClock.Instance.Indoor ? 14f : WorldClock.AirTemp;
        sb.Append($"{WorldClock.Text}    {(WorldClock.IsNight ? "ночь" : "день")}, {air:0} °C").Append('\n');
        sb.Append($"Вода: {GameSession.Count(ItemId.Water)} [Q]    Батарейки: {GameSession.Count(ItemId.Batteries)}    ");
        float w = GameSession.Weight(), limit = GameSession.CarryLimit();
        sb.Append(w > limit ? $"<color=#e07050>Вес {w:0.0}/{limit:0}</color>" : $"Вес {w:0.0}/{limit:0}");
        if (needs != null)
            foreach (var (text, level) in needs.Moodles())
                sb.Append('\n').Append(level >= 3 ? "<color=#e06050>" : level == 2 ? "<color=#e0a050>" : "<color=#e8d8a0>").Append(text).Append("</color>");
        return sb.ToString();
    }

    string WeaponLine()
    {
        var weapon = GameSession.Equipped("weapon");
        string line = $"В руках: <b>{combat.CurrentName}</b>";
        if (weapon?.Def?.type == "gun")
            line += $"   {Mathf.Max(0f, weapon.charge):0}/{weapon.Def.F("mag"):0}, запас {GameSession.Count(weapon.Def.S("ammo"))} [R]";
        else if (weapon != null && weapon.dur >= 0f)
            line += $"   прочность {weapon.dur:0}";
        return line + "   [1][2][3]";
    }

    // Начатые и ещё не выполненные задания, как журнал в исходной игре.
    static string JournalText()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var quest in Story.Quests)
        {
            int stage = GameSession.QuestStage(quest.id);
            if (stage <= 0 || quest.stages == null || stage >= quest.stages.Length - 1)
                continue;
            sb.Append("<b>").Append(quest.name).Append("</b>\n").Append(quest.stages[stage]).Append("\n\n");
        }
        return sb.ToString();
    }

    void OnMessage(string value)
    {
        if (messages.Count >= maxMessages)
        {
            Destroy(messages[0].text.gameObject);
            messages.RemoveAt(0);
        }
        var text = UIFactory.Text("Сообщение", messageColumn, value, 24, TextAnchor.LowerLeft);
        messages.Add(new Message { text = text, expires = Time.unscaledTime + messageLifetime });
    }

    void OnDamaged(float amount)
    {
        var c = damageFlash.color;
        c.a = Mathf.Clamp(c.a + amount / 60f, 0f, 0.5f);
        damageFlash.color = c;
    }

    void OnStateChanged(GameManager.State state)
    {
        promptText.gameObject.SetActive(state == GameManager.State.Playing);
    }
}
