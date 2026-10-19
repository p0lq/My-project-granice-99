using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Главное меню: новая игра, титры, выход. Титры читаются из Assets/Resources/Credits.txt,
// чтобы список авторов и ассетов правился в текстовом файле, без кода.
public class MainMenuUI : MonoBehaviour
{
    GameObject menuColumn;
    GameObject creditsPanel;
    Button firstButton;

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
        firstButton = UIFactory.Button(column, "Новая игра", NewGame);
        UIFactory.Button(column, "Титры", () => ShowCredits(true));
        UIFactory.Button(column, "Выйти", GameManager.Quit);
        menuColumn = column.gameObject;

        BuildCredits(canvas);
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

    static void NewGame()
    {
        GameSession.NewGame();
        SceneManager.LoadScene(SceneNames.Village);
    }

    static void Select(Button button)
    {
        if (EventSystem.current != null && button != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
