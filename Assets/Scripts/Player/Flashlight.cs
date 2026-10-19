using UnityEngine;
using UnityEngine.InputSystem;

// Дополнительная механика: фонарик на батарейках. В тёмном бункере без него не видно
// дороги, но «Серые» замечают свет издалека и с любой стороны (см. EnemyAI).
// Заряд хранится в GameSession и переходит между сценами.
public class Flashlight : MonoBehaviour
{
    [Tooltip("Spot Light в руке или на груди игрока")]
    [SerializeField] Light lamp;
    [Tooltip("Сколько заряда (из 100) тратится за секунду")]
    [SerializeField] float drainPerSecond = 1.2f;
    [Tooltip("Ниже этого заряда фонарик начинает мигать")]
    [SerializeField] float lowBattery = 15f;
    [Tooltip("На каком расстоянии «Серые» замечают включённый фонарик, м")]
    [SerializeField] float attractRange = 14f;

    InputAction toggleAction;
    Health health;
    float baseIntensity;

    public bool IsOn { get; private set; }
    public float AttractRange => attractRange;

    void Awake()
    {
        toggleAction = InputSystem.actions.FindAction("Player/Flashlight", true);
        health = GetComponent<Health>();
        if (lamp == null)
            lamp = GetComponentInChildren<Light>();
        baseIntensity = lamp != null ? lamp.intensity : 1f;
        SetOn(false);
    }

    void Update()
    {
        if (health != null && health.IsDead)
        {
            SetOn(false);
            return;
        }

        if (toggleAction.WasPressedThisFrame())
        {
            if (!IsOn && GameSession.Battery <= 0f && !TryChangeBatteries())
                GameMessages.Show("Батарейки сели. Нужны новые");
            else
                SetOn(!IsOn);
        }

        if (!IsOn)
            return;

        GameSession.Battery = Mathf.Max(0f, GameSession.Battery - drainPerSecond * Time.deltaTime);
        if (GameSession.Battery <= 0f && !TryChangeBatteries())
        {
            SetOn(false);
            GameMessages.Show("Фонарик погас");
            return;
        }

        if (lamp != null)
        {
            // На последних процентах свет неровно моргает, предупреждая игрока.
            bool flicker = GameSession.Battery < lowBattery && Mathf.PerlinNoise(Time.time * 8f, 0f) < 0.3f;
            lamp.intensity = flicker ? baseIntensity * 0.2f : baseIntensity;
        }
    }

    bool TryChangeBatteries()
    {
        if (!GameSession.Remove(ItemId.Batteries))
            return false;
        GameSession.Battery = Mathf.Min(100f, GameSession.Battery + Items.BatteryCharge);
        GameMessages.Show("Поменял батарейки");
        return true;
    }

    void SetOn(bool on)
    {
        IsOn = on;
        if (lamp != null)
            lamp.enabled = on;
    }
}
