using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Меню паузы, победы и проигрыша. Появляются по событию StateChanged у GameManager.
public class GameMenus : MonoBehaviour
{
    [SerializeField] string winTitle = "Антидот у тебя";
    [TextArea]
    [SerializeField] string winText = "Ты выбрался из «Подземки» с ампулами «ПФ-2». Сарыкудук получит шанс.";
    [SerializeField] string loseTitle = "Ты погиб";
    [TextArea]
    [SerializeField] string loseText = "Степь не прощает ошибок. Попробуй ещё раз: «Серые» слышат бег и видят свет.";

    GameObject pausePanel;
    GameObject endPanel;
    Text endTitle;
    Text endBody;
    Button pauseFirst;
    Button endFirst;

    void Start()
    {
        Build();
        if (GameManager.Instance != null)
            GameManager.Instance.StateChanged += OnStateChanged;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.StateChanged -= OnStateChanged;
    }

    void Build()
    {
        var canvas = UIFactory.Canvas("Меню", 10).transform;
        var gm = GameManager.Instance;

        pausePanel = Panel(canvas, "Пауза", out var pauseColumn);
        Title(pauseColumn, "Пауза");
        pauseFirst = UIFactory.Button(pauseColumn, "Продолжить", () => gm.Resume());
        UIFactory.Button(pauseColumn, "Сохранить [F5]", () => { gm.Resume(); gm.SaveGame(); });
        UIFactory.Button(pauseColumn, "Загрузить [F9]", () => gm.LoadGame());
        UIFactory.Button(pauseColumn, "Начать уровень заново", () => gm.Restart());
        UIFactory.Button(pauseColumn, "Главное меню", () => gm.ToMainMenu());
        UIFactory.Button(pauseColumn, "Выйти из игры", GameManager.Quit);

        endPanel = Panel(canvas, "Конец", out var endColumn);
        endTitle = Title(endColumn, "");
        endBody = UIFactory.Text("Текст", endColumn, "", 26, TextAnchor.MiddleCenter);
        endBody.gameObject.AddComponent<LayoutElement>().preferredWidth = 800f;
        endFirst = UIFactory.Button(endColumn, "Заново", () => gm.Restart());
        UIFactory.Button(endColumn, "Загрузить сохранение", () => gm.LoadGame());
        UIFactory.Button(endColumn, "Главное меню", () => gm.ToMainMenu());
        UIFactory.Button(endColumn, "Выйти из игры", GameManager.Quit);

        pausePanel.SetActive(false);
        endPanel.SetActive(false);
    }

    static GameObject Panel(Transform canvas, string name, out RectTransform column)
    {
        var back = UIFactory.Image(name, canvas, new Color(0f, 0f, 0f, 0.6f));
        UIFactory.Stretch(back.rectTransform);
        var box = UIFactory.Image("Окно", back.transform, UIFactory.PanelColor);
        UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 780f));
        column = UIFactory.Column("Кнопки", box.transform, 18f);
        UIFactory.Place(column, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        return back.gameObject;
    }

    static Text Title(Transform parent, string value)
    {
        var title = UIFactory.Text("Заголовок", parent, value, 54, TextAnchor.MiddleCenter);
        title.color = UIFactory.AccentColor;
        title.gameObject.AddComponent<LayoutElement>().preferredWidth = 800f;
        return title;
    }

    void OnStateChanged(GameManager.State state)
    {
        pausePanel.SetActive(state == GameManager.State.Paused);
        endPanel.SetActive(state == GameManager.State.Won || state == GameManager.State.Lost);

        if (state == GameManager.State.Won)
        {
            endTitle.text = winTitle;
            endBody.text = winText;
        }
        else if (state == GameManager.State.Lost)
        {
            endTitle.text = loseTitle;
            endBody.text = loseText;
        }

        // Выделяем первую кнопку, чтобы меню работало и с геймпада.
        // В диалоге кнопки выделяет DialogueUI.
        if (state == GameManager.State.Dialogue)
            return;
        var first = state == GameManager.State.Paused ? pauseFirst : state == GameManager.State.Playing ? null : endFirst;
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(first != null ? first.gameObject : null);
    }
}
