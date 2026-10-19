using System.Collections.Generic;
using UnityEngine;

// Связывает игровую логику с Animator. Сам ничего не решает: берёт скорость,
// присед, удары, урон и смерть у других скриптов и передаёт в параметры аниматора.
//
// Параметры Animator Controller (создаёт меню «Граница-99 → Создать Animator Controller»):
//   Speed  float   — скорость в м/с, по ней смешиваются Idle, Walk и Run
//   Crouch bool    — идёт крадучись
//   Attack trigger — замах
//   Hit    trigger — получил урон
//   Dead   bool    — погиб
//   Scream trigger — «Серый» заметил игрока
//
// Пока модели нет и Animator не найден, показывает смерть простым наклоном тела,
// чтобы на капсулах было видно, что персонаж погиб.
public class CharacterAnimator : MonoBehaviour
{
    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int CrouchId = Animator.StringToHash("Crouch");
    static readonly int AttackId = Animator.StringToHash("Attack");
    static readonly int HitId = Animator.StringToHash("Hit");
    static readonly int DeadId = Animator.StringToHash("Dead");
    static readonly int ScreamId = Animator.StringToHash("Scream");

    [Tooltip("Если пусто, ищется в дочерних объектах (Animator висит на импортированной модели)")]
    [SerializeField] Animator animator;
    [Tooltip("Сглаживание скорости, с. Без него переход Walk/Run дёргается")]
    [SerializeField] float speedDamping = 0.1f;
    [Tooltip("Объект, который наклоняется при смерти, если Animator нет")]
    [SerializeField] Transform fallbackBody;

    PlayerMovement playerMovement;
    EnemyAI enemy;
    readonly HashSet<int> parameters = new();

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            // Модель двигает CharacterController или NavMeshAgent, а не анимация.
            animator.applyRootMotion = false;
            foreach (var p in animator.parameters)
                parameters.Add(p.nameHash);
        }

        playerMovement = GetComponent<PlayerMovement>();
        enemy = GetComponent<EnemyAI>();

        var combat = GetComponent<PlayerCombat>();
        if (combat != null)
            combat.Attacked += () => Trigger(AttackId);
        if (enemy != null)
        {
            enemy.Attacked += () => Trigger(AttackId);
            enemy.Alerted += () => Trigger(ScreamId);
        }

        var health = GetComponent<Health>();
        if (health != null)
        {
            health.Damaged += _ => { if (!health.IsDead) Trigger(HitId); };
            health.Died += OnDied;
        }
    }

    // Длина клипа в секундах, 0 если такого нет. EnemyAI по ней ждёт конца крика и удара,
    // иначе «Серый» поехал бы дальше, пока анимация ещё не доиграла.
    public float ClipLength(string clipName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return 0f;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name == clipName)
                return clip.length;
        return 0f;
    }

    void Update()
    {
        if (animator == null)
            return;

        float speed = playerMovement != null ? playerMovement.CurrentSpeed
                    : enemy != null ? enemy.CurrentSpeed
                    : 0f;
        if (Has(SpeedId))
            animator.SetFloat(SpeedId, speed, speedDamping, Time.deltaTime);
        if (Has(CrouchId))
            animator.SetBool(CrouchId, playerMovement != null && playerMovement.IsCrouching);
    }

    void OnDied()
    {
        if (animator != null)
        {
            if (Has(DeadId))
                animator.SetBool(DeadId, true);
        }
        else if (fallbackBody != null)
        {
            fallbackBody.localRotation = Quaternion.Euler(90f, 0f, 0f) * fallbackBody.localRotation;
            fallbackBody.localPosition = new Vector3(fallbackBody.localPosition.x, 0.3f, fallbackBody.localPosition.z);
        }
    }

    void Trigger(int id)
    {
        if (animator != null && Has(id))
            animator.SetTrigger(id);
    }

    // В контроллере может не быть какого-то параметра (например, Crouch у «Серого»).
    // Без проверки Unity засыпал бы консоль предупреждениями.
    bool Has(int id) => parameters.Contains(id);
}
