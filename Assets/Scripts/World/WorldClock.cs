using UnityEngine;

// Время суток. Одна реальная секунда — одна игровая минута, сутки длятся 24 минуты,
// во сне время идёт в 60 раз быстрее. Время хранится в GameSession и не сбрасывается
// при переходе в бункер.
//
// В посёлке двигает солнце и меняет освещение: день, закат, ночь (как darkness()
// в старой игре: светло с 7 до 19, сумерки по 2 часа, ночью почти темно).
public class WorldClock : MonoBehaviour
{
    public static WorldClock Instance { get; private set; }

    [Tooltip("Игровых минут за реальную секунду")]
    [SerializeField] float minutesPerSecond = 1f;
    [SerializeField] float sleepSpeed = 60f;
    [Tooltip("Солнце сцены. Пусто — помещение (бункер), свет не меняется")]
    [SerializeField] Light sun;
    [SerializeField] Color dayAmbient = new Color(0.55f, 0.53f, 0.55f);
    [SerializeField] Color nightAmbient = new Color(0.05f, 0.06f, 0.1f);
    [SerializeField] float daySunIntensity = 1.2f;

    public bool Indoor => sun == null;
    public bool Sleeping { get; set; }

    public static float Minutes => GameSession.Data.minutes;
    public static int Day => (int)(Minutes / 1440f) + 1;
    public static float HourF => Minutes / 60f % 24f;
    public static string Text => $"День {Day}, {(int)HourF:00}:{(int)(Minutes % 60f):00}";

    // 0 — светлый день, 0,86 — ночь.
    public static float Darkness
    {
        get
        {
            float h = HourF;
            if (h >= 7f && h < 19f) return 0f;
            if (h >= 5f && h < 7f) return 0.86f * (7f - h) / 2f;
            if (h >= 19f && h < 21f) return 0.86f * (h - 19f) / 2f;
            return 0.86f;
        }
    }

    public static bool IsNight => Darkness > 0.5f;

    // Резко-континентальная степь: днём до +35, ночью до +5.
    public static float AirTemp => 20f + 15f * Mathf.Sin((HourF - 9f) / 24f * 2f * Mathf.PI);

    // Сколько игровых минут прошло за этот кадр. Читают потребности героя.
    public float DeltaMinutes { get; private set; }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        DeltaMinutes = Time.deltaTime * minutesPerSecond * (Sleeping ? sleepSpeed : 1f);
        GameSession.Data.minutes += DeltaMinutes;
        if (sun == null)
            return;

        float dark = Darkness;
        // Солнце обходит небо за сутки: в полдень высоко, ночью под горизонтом.
        float angle = (HourF - 6f) / 24f * 360f;
        sun.transform.rotation = Quaternion.Euler(Mathf.Clamp(angle, -20f, 200f), -30f, 0f);
        sun.intensity = daySunIntensity * (1f - dark / 0.86f) + 0.08f;
        sun.color = Color.Lerp(Color.white, new Color(1f, 0.6f, 0.4f), Mathf.Clamp01(dark * 2f));
        RenderSettings.ambientLight = Color.Lerp(dayAmbient, nightAmbient, dark / 0.86f);
        if (Camera.main != null)
            Camera.main.backgroundColor = Color.Lerp(new Color(0.78f, 0.72f, 0.6f), new Color(0.03f, 0.04f, 0.07f), dark / 0.86f);
    }
}
