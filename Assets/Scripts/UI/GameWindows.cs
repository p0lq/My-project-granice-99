using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Окна поверх игры, как в старой игре: инвентарь [I]/[Tab], крафт [K], здоровье [H],
// журнал [J], словарик [G], а также радио, записка и обмен, которые открываются из игры.
// Пока окно открыто, игра стоит (GameManager в состоянии Dialogue).
//
// Клавиши читаются напрямую с клавиатуры, а не через файл действий ввода: правка того файла
// при открытом редакторе ломала Input System до перезапуска Unity.
public class GameWindows : MonoBehaviour
{
    public abstract class Window
    {
        public GameObject root;
        public abstract void Build(Transform canvas);
        public abstract void Refresh();
        public bool IsOpen => root != null && root.activeSelf;
    }

    static GameWindows instance;
    readonly Dictionary<string, Window> windows = new();
    Window current;

    public static bool AnyOpen => instance != null && instance.current != null;

    void Awake()
    {
        instance = this;
        windows["inventory"] = new InventoryWindow();
        windows["health"] = new HealthWindow();
        windows["craft"] = new CraftWindow();
        windows["journal"] = new JournalWindow();
        windows["glossary"] = new GlossaryWindow();
        windows["radio"] = new RadioWindow();
        windows["note"] = new NoteWindow();
        windows["trade"] = new TradeWindow();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    void Start()
    {
        var canvas = UIFactory.Canvas("Окна", 8).transform;
        foreach (var w in windows.Values)
        {
            w.Build(canvas);
            w.root.SetActive(false);
        }
        GameSession.InventoryChanged += RefreshCurrent;
    }

    void OnDisable() => GameSession.InventoryChanged -= RefreshCurrent;

    void RefreshCurrent()
    {
        if (current != null)
            current.Refresh();
    }

    public static void Open(string id)
    {
        if (instance == null || !instance.windows.TryGetValue(id, out var w))
            return;
        var gm = GameManager.Instance;
        if (gm == null || (gm.Current != GameManager.State.Playing && gm.Current != GameManager.State.Dialogue))
            return;
        if (instance.current != null)
            instance.current.root.SetActive(false);
        instance.current = w;
        w.root.SetActive(true);
        w.Refresh();
        if (gm.Current == GameManager.State.Playing)
            gm.EnterDialogue();
    }

    public static Window Get(string id) => instance != null && instance.windows.TryGetValue(id, out var w) ? w : null;

    public static void Close()
    {
        if (instance == null || instance.current == null)
            return;
        instance.current.root.SetActive(false);
        instance.current = null;
        if (GameManager.Instance != null && GameManager.Instance.Current == GameManager.State.Dialogue && !DialogueUI.IsOpen)
            GameManager.Instance.Resume();
    }

    void Update()
    {
        var kb = Keyboard.current;
        var gm = GameManager.Instance;
        if (kb == null || gm == null)
            return;

        // После разговора с торговцем открывается обмен (последствие trade:… в Story.json).
        if (Story.PendingTrade != null && !DialogueUI.IsOpen && gm.Current == GameManager.State.Playing)
        {
            TradeWindow.Trader = Story.PendingTrade;
            Story.PendingTrade = null;
            Open("trade");
            return;
        }

        if (current != null && kb.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }
        if (DialogueUI.IsOpen || (gm.Current != GameManager.State.Playing && current == null))
            return;

        Toggle(kb.iKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame, "inventory");
        Toggle(kb.kKey.wasPressedThisFrame, "craft");
        Toggle(kb.hKey.wasPressedThisFrame, "health");
        Toggle(kb.jKey.wasPressedThisFrame, "journal");
        Toggle(kb.gKey.wasPressedThisFrame, "glossary");
    }

    void Toggle(bool pressed, string id)
    {
        if (!pressed)
            return;
        if (current == windows[id])
            Close();
        else
            Open(id);
    }
}
