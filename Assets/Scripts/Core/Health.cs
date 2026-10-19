using System;
using UnityEngine;

// Здоровье для игрока и врагов. Остальные скрипты (HUD, аниматор, ИИ)
// подписываются на события и сами решают, что показать или сделать.
public class Health : MonoBehaviour
{
    [SerializeField] float maxHealth = 100f;

    [Tooltip("Сколько секунд после удара новый урон не проходит, чтобы один удар не засчитался несколько раз")]
    [SerializeField] float invulnerableTime = 0.5f;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead => Current <= 0f;

    // текущее значение, максимум
    public event Action<float, float> Changed;
    public event Action<float> Damaged;
    public event Action Died;

    float lastHitTime = float.NegativeInfinity;

    void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
            return;
        if (Time.time - lastHitTime < invulnerableTime)
            return;

        lastHitTime = Time.time;
        Current = Mathf.Max(0f, Current - amount);
        Damaged?.Invoke(amount);
        Changed?.Invoke(Current, maxHealth);

        if (IsDead)
            Died?.Invoke();
    }

    // Для перехода между сценами: здоровье берётся из GameSession, а не всегда полное.
    public void SetCurrent(float value)
    {
        Current = Mathf.Clamp(value, 0f, maxHealth);
        Changed?.Invoke(Current, maxHealth);
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        Current = Mathf.Min(maxHealth, Current + amount);
        Changed?.Invoke(Current, maxHealth);
    }
}
