using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Удар ближнего боя по ЛКМ в сторону курсора. Урон проходит не сразу,
// а через hitDelay: так он совпадёт с кадром удара в анимации Attack.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] float damage = 25f;
    [Tooltip("Насколько далеко перед игроком достаёт удар, м")]
    [SerializeField] float reach = 1.6f;
    [Tooltip("Радиус зоны удара. Её передний край находится на расстоянии reach")]
    [SerializeField] float hitRadius = 0.7f;
    [Tooltip("Пауза между ударами, с")]
    [SerializeField] float cooldown = 0.7f;
    [Tooltip("Через сколько секунд после нажатия засчитывается попадание")]
    [SerializeField] float hitDelay = 0.25f;
    [Tooltip("Сколько игрок стоит на месте во время замаха, с")]
    [SerializeField] float rootTime = 0.4f;
    [Tooltip("Удар слышно на этом расстоянии, м")]
    [SerializeField] float attackNoise = 8f;

    PlayerMovement movement;
    Health health;
    InputAction attackAction;
    float nextAttackTime;
    float pendingHitTime = -1f;

    public event Action Attacked;
    public float LastNoiseTime { get; private set; } = float.NegativeInfinity;
    public float AttackNoise => attackNoise;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        attackAction = InputSystem.actions.FindAction("Player/Attack", true);
    }

    void Update()
    {
        bool alive = health == null || !health.IsDead;
        if (alive && attackAction.WasPressedThisFrame() && Time.time >= nextAttackTime)
            StartAttack();

        if (pendingHitTime > 0f && Time.time >= pendingHitTime)
        {
            pendingHitTime = -1f;
            if (alive)
                DealDamage();
        }
    }

    void StartAttack()
    {
        FaceCursor();
        nextAttackTime = Time.time + cooldown;
        pendingHitTime = Time.time + hitDelay;
        LastNoiseTime = Time.time;
        movement.Lock(rootTime);
        Attacked?.Invoke();
    }

    // Шум без удара: обыск ящика, открытый люк. EnemyAI слышит его на расстоянии attackNoise.
    public void MakeNoise()
    {
        LastNoiseTime = Time.time;
    }

    void DealDamage()
    {
        foreach (var hit in Physics.OverlapSphere(HitCenter(), hitRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var target = hit.GetComponentInParent<Health>();
            if (target != null && target != health && !target.IsDead)
            {
                target.TakeDamage(damage);
                Sfx.Play(SoundId.Hit, hit.ClosestPoint(HitCenter()), 0.9f);
            }
        }
    }

    // Поворачиваем игрока к точке на земле под курсором.
    void FaceCursor()
    {
        var cam = Camera.main;
        if (cam == null || Mouse.current == null)
            return;

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        var ground = new Plane(Vector3.up, transform.position);
        if (!ground.Raycast(ray, out float distance))
            return;

        Vector3 dir = ray.GetPoint(distance) - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(HitCenter(), hitRadius);
    }

    Vector3 HitCenter() => transform.position + Vector3.up + transform.forward * (reach - hitRadius);
}
