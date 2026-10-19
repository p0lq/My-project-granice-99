using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Окно разговора внизу экрана: имя, реплика, варианты ответа кнопками.
// Варианты выбираются мышью или клавишами 1–6. Пока окно открыто, игра стоит.
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.panel != null && Instance.panel.activeSelf;

    GameObject panel;
    Text speakerText;
    Text lineText;
    RectTransform optionColumn;
    readonly List<Button> buttons = new();
    readonly List<Story.OptionDef> visibleOptions = new();

    Npc npc;
    Story.DialogueDef dialogue;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        Build();
    }

    void Build()
    {
        var canvas = UIFactory.Canvas("Диалог", 5).transform;
        var box = UIFactory.Image("Окно диалога", canvas, UIFactory.PanelColor);
        UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1300f, 470f));
        panel = box.gameObject;

        speakerText = UIFactory.Text("Имя", box.transform, "", 30, TextAnchor.UpperLeft);
        speakerText.color = UIFactory.AccentColor;
        UIFactory.Place(speakerText.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -24f), new Vector2(1200f, 40f));

        lineText = UIFactory.Text("Реплика", box.transform, "", 26, TextAnchor.UpperLeft);
        UIFactory.Place(lineText.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -72f), new Vector2(1230f, 150f));

        optionColumn = UIFactory.Rect("Ответы", box.transform);
        UIFactory.Place(optionColumn, new Vector2(0f, 0f), new Vector2(36f, 24f), new Vector2(1230f, 220f));
        var layout = optionColumn.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.LowerLeft;
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        panel.SetActive(false);
    }

    public void Open(Npc speaker, string dialogueId)
    {
        dialogue = Story.Dialogue(dialogueId);
        if (dialogue == null || GameManager.Instance == null)
        {
            Debug.LogWarning($"Диалога «{dialogueId}» нет в Story.json");
            return;
        }
        npc = speaker;
        npc.Talking = true;
        GameManager.Instance.EnterDialogue();
        panel.SetActive(true);
        Show(dialogue.start);
    }

    void Show(string nodeId)
    {
        var node = Story.Node(dialogue, nodeId);
        if (node == null)
        {
            Close();
            return;
        }
        Story.Apply(node.effects);

        speakerText.text = string.IsNullOrEmpty(npc.Title) ? npc.NpcName : $"{npc.NpcName}, {npc.Title}";
        lineText.text = node.text;

        foreach (var b in buttons)
            Destroy(b.gameObject);
        buttons.Clear();
        visibleOptions.Clear();

        if (node.options != null)
            foreach (var option in node.options)
                if (Story.Allowed(option))
                    visibleOptions.Add(option);

        for (int i = 0; i < visibleOptions.Count; i++)
        {
            var option = visibleOptions[i];
            var button = UIFactory.Button(optionColumn, $"{i + 1}. {option.text}", () => Choose(option));
            var label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.fontSize = 24;
            label.rectTransform.offsetMin = new Vector2(16f, 0f);
            var element = button.GetComponent<LayoutElement>();
            element.preferredHeight = 44f;
            buttons.Add(button);
        }
        if (buttons.Count > 0 && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(buttons[0].gameObject);
    }

    void Choose(Story.OptionDef option)
    {
        Story.Apply(option.effects);
        if (string.IsNullOrEmpty(option.next) || option.next == "end")
            Close();
        else
            Show(option.next);
    }

    void Close()
    {
        panel.SetActive(false);
        if (npc != null)
            npc.Talking = false;
        npc = null;
        if (GameManager.Instance != null)
            GameManager.Instance.Resume();
    }

    void Update()
    {
        if (!panel.activeSelf || Keyboard.current == null)
            return;
        for (int i = 0; i < buttons.Count && i < 6; i++)
            if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
            {
                buttons[i].onClick.Invoke();
                return;
            }
    }
}
