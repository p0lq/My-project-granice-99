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

    RectTransform healthFill;
    RectTransform batteryFill;
    Text healthText;
    Text itemsText;
    Text objectiveText;
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

        var batteryBar = UIFactory.Bar("Фонарик", canvas, new Color(0.9f, 0.8f, 0.35f), out batteryFill);
        UIFactory.Place(batteryBar, new Vector2(0f, 1f), new Vector2(30f, -72f), new Vector2(240f, 18f));
        var batteryLabel = UIFactory.Text("Подпись", canvas, "Фонарик [F]", 20, TextAnchor.MiddleLeft);
        UIFactory.Place(batteryLabel.rectTransform, new Vector2(0f, 1f), new Vector2(282f, -68f), new Vector2(200f, 26f));

        itemsText = UIFactory.Text("Предметы", canvas, "", 22, TextAnchor.UpperLeft);
        UIFactory.Place(itemsText.rectTransform, new Vector2(0f, 1f), new Vector2(30f, -104f), new Vector2(600f, 100f));

        objectiveText = UIFactory.Text("Цель", canvas, "", 28, TextAnchor.UpperCenter);
        objectiveText.color = UIFactory.AccentColor;
        UIFactory.Place(objectiveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 40f));

        promptText = UIFactory.Text("Подсказка", canvas, "", 30, TextAnchor.MiddleCenter);
        UIFactory.Place(promptText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(900f, 50f));

        var controls = UIFactory.Text("Управление", canvas,
            "WASD — идти   Shift — бег   C — красться   ЛКМ — удар   E — действие   Q — пить   Esc — пауза",
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

        itemsText.text = ItemsLine();
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

    static string ItemsLine()
    {
        string line = $"Вода: {GameSession.Count(ItemId.Water)} [Q]    Батарейки: {GameSession.Count(ItemId.Batteries)}";
        if (GameSession.Has(ItemId.BunkerKey))
            line += "\nКлюч от люка";
        if (GameSession.Has(ItemId.Antidote))
            line += "\nАнтидот «ПФ-2»";
        return line;
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
