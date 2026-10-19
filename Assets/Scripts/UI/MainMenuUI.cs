using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Главное меню: продолжить (если есть сохранение), новая игра с созданием персонажа,
// титры, выход. Титры читаются из Assets/Resources/Credits.txt, чтобы список авторов
// и ассетов правился в текстовом файле, без кода.
public class MainMenuUI : MonoBehaviour
{
    GameObject menuColumn;
    GameObject creditsPanel;
    GameObject heroPanel;
    Button firstButton;
    InputField nameField;
    Text pointsText;
    Button startButton;
    readonly System.Collections.Generic.List<string> chosen = new();
    readonly System.Collections.Generic.Dictionary<string, Image> traitButtons = new();

    void Start()
    {
        Time.timeScale = 1f;
        Build();
        Select(firstButton);
    }

    void Build()
    {
        var canvas = UIFactory.Canvas("Главное меню", 0).transform;

        var background = UIFactory.Image("Фон", canvas, new Color(0.16f, 0.13f, 0.1f, 0.55f));
        UIFactory.Stretch(background.rectTransform);

        var title = UIFactory.Text("Название", canvas, "ГРАНИЦА-99", 110, TextAnchor.MiddleCenter);
        title.color = UIFactory.AccentColor;
        UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1400f, 140f));

        var subtitle = UIFactory.Text("Подзаголовок", canvas,
            "Юг Казахстана, 1999 год. После «Пыльного фронта» в посёлке Сарыкудук остались одни «Серые».",
            28, TextAnchor.MiddleCenter);
        UIFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1400f, 80f));

        var column = UIFactory.Column("Кнопки", canvas, 18f);
        UIFactory.Place(column, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), Vector2.zero);
        if (SaveSystem.Exists)
            firstButton = UIFactory.Button(column, "Продолжить", () => SaveSystem.Load());
        var newGame = UIFactory.Button(column, "Новая игра", () => ShowHero(true));
        if (firstButton == null)
            firstButton = newGame;
        UIFactory.Button(column, "Титры", () => ShowCredits(true));
        UIFactory.Button(column, "Выйти", GameManager.Quit);
        menuColumn = column.gameObject;

        BuildCredits(canvas);
        BuildHero(canvas);
    }

    // Создание персонажа: имя и черты. Черты с минусом стоят очков, с плюсом их дают;
    // остаток не может быть меньше нуля (character.json старой игры).
    void BuildHero(Transform canvas)
    {
        var panel = UIFactory.Image("Персонаж", canvas, UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 940f));
        heroPanel = panel.gameObject;

        var title = UIFactory.Text("Заголовок", panel.transform, "Кто ты?", 44, TextAnchor.UpperLeft);
        title.color = UIFactory.AccentColor;
        UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -26f), new Vector2(600f, 60f));
        nameField = UIFactory.Input(panel.transform, "Имя (по умолчанию — Выживший)", new Vector2(0f, 1f), new Vector2(40f, -100f), new Vector2(700f, 60f));

        pointsText = UIFactory.Text("Очки", panel.transform, "", 26, TextAnchor.UpperRight);
        UIFactory.Place(pointsText.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -110f), new Vector2(600f, 40f));

        var list = UIFactory.ScrollList(panel.transform, new Vector2(0f, 1f), new Vector2(40f, -180f), new Vector2(1420f, 600f), 6f);
        foreach (var trait in GameData.Traits.Values)
        {
            string id = trait.id;
            string cost = trait.cost < 0 ? $"стоит {-trait.cost}" : $"даёт {trait.cost}";
            var b = UIFactory.SmallButton(list, $"{trait.name} ({cost}) — {trait.desc}", () => ToggleTrait(id), 1380f);
            var label = b.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(14f, 0f);
            traitButtons[id] = b.GetComponent<Image>();
        }

        startButton = UIFactory.Button(panel.transform, "Начать", StartGame);
        UIFactory.Place((RectTransform)startButton.transform, new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(420f, 64f));
        var back = UIFactory.Button(panel.transform, "Назад", () => ShowHero(false));
        UIFactory.Place((RectTransform)back.transform, new Vector2(0f, 0f), new Vector2(40f, 30f), new Vector2(420f, 64f));
        heroPanel.SetActive(false);
        UpdatePoints();
    }

    int Points()
    {
        int points = GameData.StartPoints;
        foreach (var t in chosen)
            points += GameData.Traits[t].cost;
        return points;
    }

    void ToggleTrait(string id)
    {
        if (!chosen.Remove(id))
            chosen.Add(id);
        UpdatePoints();
    }

    void UpdatePoints()
    {
        int points = Points();
        pointsText.text = points >= 0 ? $"Свободных очков: {points}" : $"<color=#e06050>Не хватает очков: {-points}</color>";
        pointsText.supportRichText = true;
        startButton.interactable = points >= 0;
        foreach (var pair in traitButtons)
            pair.Value.color = chosen.Contains(pair.Key) ? new Color(0.45f, 0.36f, 0.2f) : UIFactory.ButtonColor;
    }

    void ShowHero(bool show)
    {
        heroPanel.SetActive(show);
        menuColumn.SetActive(!show);
        Select(show ? startButton : firstButton);
    }

    void StartGame()
    {
        if (Points() < 0)
            return;
        string name = string.IsNullOrWhiteSpace(nameField.text) ? "Выживший" : nameField.text.Trim();
        GameSession.NewGame(name, chosen);
        SceneManager.LoadScene(SceneNames.Village);
    }

    void BuildCredits(Transform canvas)
    {
        var panel = UIFactory.Image("Титры", canvas, UIFactory.PanelColor);
        UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1300f, 900f));

        var file = Resources.Load<TextAsset>("Credits");
        var text = UIFactory.Text("Текст", panel.transform, file != null ? file.text : "Файл Credits.txt не найден", 24, TextAnchor.UpperLeft);
        var rect = UIFactory.Stretch(text.rectTransform);
        rect.offsetMin = new Vector2(50f, 120f);
        rect.offsetMax = new Vector2(-50f, -50f);
        text.verticalOverflow = VerticalWrapMode.Truncate;

        var back = UIFactory.Button(panel.transform, "Назад", () => ShowCredits(false));
        UIFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(420f, 64f));

        creditsPanel = panel.gameObject;
        creditsPanel.SetActive(false);
    }

    void ShowCredits(bool show)
    {
        creditsPanel.SetActive(show);
        menuColumn.SetActive(!show);
        Select(show ? creditsPanel.GetComponentInChildren<Button>() : firstButton);
    }

    static void Select(Button button)
    {
        if (EventSystem.current != null && button != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
