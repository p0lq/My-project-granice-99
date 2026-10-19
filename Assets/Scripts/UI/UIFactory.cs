using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Создание элементов uGUI из кода. Интерфейс в игре простой, и так его проще менять
// и проверять, чем руками собирать префабы. Шрифт встроенный в Unity: в нём есть кириллица.
public static class UIFactory
{
    public static readonly Color PanelColor = new Color(0.08f, 0.07f, 0.06f, 0.85f);
    public static readonly Color TextColor = new Color(0.93f, 0.89f, 0.8f);
    public static readonly Color AccentColor = new Color(0.85f, 0.65f, 0.3f);
    public static readonly Color ButtonColor = new Color(0.25f, 0.21f, 0.17f);

    static Font font;

    public static Font Font
    {
        get
        {
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static Canvas Canvas(string name, int order)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;

        // Размеры элементов заданы под 1920×1080 и масштабируются под любой экран.
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        EnsureEventSystem();
        return canvas;
    }

    // Без EventSystem кнопки не нажимаются. Модуль ввода нужен от Input System,
    // потому что старый Input Manager в проекте выключен.
    public static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null)
            return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // anchor — точка привязки (0,0 левый низ, 1,1 правый верх), pivot ставится туда же.
    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Image Image(string name, Transform parent, Color color)
    {
        var rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static Text Text(string name, Transform parent, string value, int size, TextAnchor alignment)
    {
        var rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<Text>();
        text.font = Font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = TextColor;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        // Тень, чтобы текст читался и на светлой земле, и в тёмном бункере.
        var shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    public static Button Button(Transform parent, string label, UnityAction onClick)
    {
        var image = Image("Кнопка " + label, parent, ButtonColor);
        var button = image.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.4f, 1.3f, 1.1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = new Color(1.25f, 1.2f, 1.05f);
        button.colors = colors;
        button.onClick.AddListener(() => Sfx.PlayUI(SoundId.UIClick, 0.6f));
        button.onClick.AddListener(onClick);

        var layout = image.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 64f;
        layout.preferredWidth = 420f;

        var text = Text("Надпись", image.transform, label, 30, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        return button;
    }

    // Колонка, в которой элементы встают друг под другом с отступом.
    public static RectTransform Column(string name, Transform parent, float spacing)
    {
        var rect = Rect(name, parent);
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var fitter = rect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rect;
    }

    // Полоска-индикатор: фон и заливка. Заливка тянется по ширине через anchorMax.x,
    // поэтому не нужен спрайт, который требует Image.Type.Filled.
    public static RectTransform Bar(string name, Transform parent, Color fill, out RectTransform fillRect)
    {
        var back = Image(name, parent, new Color(0f, 0f, 0f, 0.6f));
        var inner = Image("Заливка", back.transform, fill);
        fillRect = Stretch(inner.rectTransform);
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);
        return back.rectTransform;
    }

    public static void SetBar(RectTransform fillRect, float fraction)
    {
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
    }
}
