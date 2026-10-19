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
//   Weapon int     — оружие героя: 1 нож, 2 монтировка; включает слой рук этого оружия
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
    static readonly int WeaponId = Animator.StringToHash("Weapon");

    [Tooltip("Если пусто, ищется в дочерних объектах (Animator висит на импортированной модели)")]
    [SerializeField] Animator animator;
    [Tooltip("Сглаживание скорости, с. Без него переход Walk/Run дёргается")]
    [SerializeField] float speedDamping = 0.1f;
    [Tooltip("Объект, который наклоняется при смерти, если Animator нет")]
    [SerializeField] Transform fallbackBody;

    PlayerMovement playerMovement;
    EnemyAI enemy;
    int crouchArmsLayer = -1;
    float crouchArmsWeight;
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
            // Слой рук для приседа есть только у героя (его создаёт сборщик аниматора).
            crouchArmsLayer = animator.GetLayerIndex("Руки крадучись");
        }

        playerMovement = GetComponent<PlayerMovement>();
        enemy = GetComponent<EnemyAI>();

        var combat = GetComponent<PlayerCombat>();
        if (combat != null)
        {
            combat.Attacked += () => Trigger(AttackId);
            combat.WeaponChanged += SetWeapon;
        }
        if (enemy != null)
        {
            enemy.Attacked += () => Trigger(AttackId);
            enemy.Alerted += () => Trigger(ScreamId);
            enemy.Bashed += () => Trigger(AttackId);
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
        bool crouching = playerMovement != null && playerMovement.IsCrouching;
        if (Has(CrouchId))
            animator.SetBool(CrouchId, crouching);
        if (crouchArmsLayer > 0)
        {
            // Плавно, за ту же четверть секунды, что и переход в присед.
            crouchArmsWeight = Mathf.MoveTowards(crouchArmsWeight, crouching ? 1f : 0f, Time.deltaTime * 4f);
            animator.SetLayerWeight(crouchArmsLayer, crouchArmsWeight);
        }
    }

    void SetWeapon(int weapon)
    {
        if (animator != null && Has(WeaponId))
            animator.SetInteger(WeaponId, weapon);
    }

    void OnDied()
    {
        if (animator != null)
        {
            // Падает всё тело: слой удара больше не перекрывает руки.
            for (int layer = 1; layer < animator.layerCount; layer++)
                animator.SetLayerWeight(layer, 0f);
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
