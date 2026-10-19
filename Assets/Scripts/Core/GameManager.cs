using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Один на каждый игровой уровень. Следит за ходом игры: пауза, победа, проигрыш,
// переход на следующий уровень. Меню и HUD только читают отсюда состояние и вызывают методы.
public class GameManager : MonoBehaviour
{
    public enum State { Playing, Paused, Won, Lost }

    public static GameManager Instance { get; private set; }

    [Header("Цель уровня")]
    [Tooltip("Предмет, после которого цель меняется на вторую")]
    [SerializeField] ItemId objectiveItem = ItemId.BunkerKey;
    [SerializeField] string objectiveBefore = "Найди ключ от люка";
    [SerializeField] string objectiveAfter = "Спустись в люк";

    [Tooltip("Сколько секунд после смерти показывается мир, прежде чем выйдет меню проигрыша")]
    [SerializeField] float loseDelay = 2f;

    Health playerHealth;
    InputAction pauseAction;
    InputActionMap playerMap;
    float loseTime = -1f;

    public State Current { get; private set; } = State.Playing;
    public string Objective => GameSession.Has(objectiveItem) ? objectiveAfter : objectiveBefore;
    public Health PlayerHealth => playerHealth;

    public event Action<State> StateChanged;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
        pauseAction = InputSystem.actions.FindAction("UI/Pause", true);
        playerMap = InputSystem.actions.FindActionMap("Player", true);
        playerMap.Enable();

        var player = GameObject.FindWithTag("Player");
        if (player != null)
            playerHealth = player.GetComponent<Health>();
    }

    void Start()
    {
        if (playerHealth != null)
        {
            // Если здоровье кончилось на прошлом уровне (так быть не должно), начинаем с полного.
            playerHealth.SetCurrent(GameSession.Health > 0f ? GameSession.Health : GameSession.StartHealth);
            playerHealth.Died += OnPlayerDied;
        }
        GameSession.Checkpoint();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (playerHealth != null && !playerHealth.IsDead)
            GameSession.Health = playerHealth.Current;

        if (pauseAction.WasPressedThisFrame())
        {
            if (Current == State.Playing)
                Pause();
            else if (Current == State.Paused)
                Resume();
        }

        if (loseTime > 0f && Time.time >= loseTime)
        {
            loseTime = -1f;
            SetState(State.Lost);
        }
    }

    public void Pause() => SetState(State.Paused);

    public void Resume() => SetState(State.Playing);

    public void Win() => SetState(State.Won);

    public void Restart()
    {
        GameSession.RestoreCheckpoint();
        Load(SceneManager.GetActiveScene().name);
    }

    public void LoadLevel(string scene)
    {
        if (playerHealth != null)
            GameSession.Health = playerHealth.Current;
        Load(scene);
    }

    public void ToMainMenu() => Load(SceneNames.MainMenu);

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnPlayerDied()
    {
        loseTime = Time.time + loseDelay;
    }

    void SetState(State state)
    {
        Current = state;
        bool playing = state == State.Playing;
        // Время стоит в меню; карта Player выключена, чтобы клик по кнопке не был ударом.
        Time.timeScale = playing ? 1f : 0f;
        if (playing)
            playerMap.Enable();
        else
            playerMap.Disable();
        // На паузе и в финальных меню звуки мира замирают, кнопки и музыка финала слышны.
        AudioListener.pause = !playing;
        if (state == State.Won)
            Sfx.PlayUI(SoundId.Win);
        else if (state == State.Lost)
            Sfx.PlayUI(SoundId.Lose);
        StateChanged?.Invoke(state);
    }

    static void Load(string scene)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }
}
