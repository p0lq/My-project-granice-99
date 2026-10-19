using System;
using UnityEngine;
using UnityEngine.AI;

// «Серый». Бродит вокруг точки появления, замечает игрока глазами или на слух,
// преследует и бьёт. Видит плохо и недалеко, слышит хорошо: бег и удары
// выдают игрока, присед почти нет.
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour
{
    public enum State { Wander, Chase, Attack, Dead }

    [Header("Скорость, м/с")]
    [SerializeField] float wanderSpeed = 1.2f;
    [Tooltip("Быстрее ходьбы игрока (3), но медленнее бега (5,5): от «Серого» можно убежать")]
    [SerializeField] float chaseSpeed = 3.4f;

    [Header("Чувства")]
    [SerializeField] float sightRange = 9f;
    [Tooltip("Угол обзора, градусов")]
    [SerializeField] float sightAngle = 110f;
    [Tooltip("Множитель к шуму игрока: 1 — слышит шаги на том расстоянии, которое указано у игрока")]
    [SerializeField] float hearing = 1f;
    [Tooltip("Сколько секунд ищет игрока после того, как потерял его")]
    [SerializeField] float giveUpTime = 5f;
    [Tooltip("Сколько секунд кричит на месте, заметив игрока. Крик даёт игроку время убежать")]
    [SerializeField] float screamTime = 1.2f;

    [Header("Атака")]
    [SerializeField] float attackRange = 1.4f;
    [SerializeField] float attackDamage = 15f;
    [SerializeField] float attackCooldown = 1.6f;
    [Tooltip("Задержка до попадания, под кадр удара в анимации")]
    [SerializeField] float attackHitDelay = 0.45f;

    [Header("Блуждание")]
    [SerializeField] float wanderRadius = 6f;
    [SerializeField] float wanderPause = 3f;

    NavMeshAgent agent;
    Health health;
    Transform player;
    PlayerMovement playerMovement;
    PlayerCombat playerCombat;
    Flashlight playerFlashlight;
    Health playerHealth;

    Vector3 home;
    float lastSeenTime = float.NegativeInfinity;
    float nextWanderTime;
    float nextAttackTime;
    float pendingHitTime = -1f;
    float screamUntil;
    float attackBusyUntil;
    float attackAnimTime;
    bool wasHunting;

    public State Current { get; private set; } = State.Wander;
    public float CurrentSpeed => agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
    public event Action Attacked;
    // Заметил игрока после блуждания: CharacterAnimator включает анимацию Scream.
    public event Action Alerted;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<Health>();
        health.Died += OnDied;
        health.Damaged += _ => lastSeenTime = Time.time; // ударили — значит, знает, где игрок
    }

    void Start()
    {
        // Агента включаем только здесь: сетку строит RuntimeNavMesh в Awake,
        // а включённый раньше агент не найдёт её и выдаст предупреждение.
        agent.enabled = true;
        home = transform.position;

        // С моделью паузы подстраиваются под длину её анимаций (выход из клипа на 90 %).
        var anim = GetComponent<CharacterAnimator>();
        if (anim != null)
        {
            float scream = anim.ClipLength("Scream");
            if (scream > 0f)
                screamTime = scream * 0.9f;
            attackAnimTime = anim.ClipLength("Attack") * 0.9f;
        }

        var playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
            playerMovement = playerObject.GetComponent<PlayerMovement>();
            playerCombat = playerObject.GetComponent<PlayerCombat>();
            playerFlashlight = playerObject.GetComponent<Flashlight>();
            playerHealth = playerObject.GetComponent<Health>();
        }
    }

    void Update()
    {
        if (Current == State.Dead || !agent.isOnNavMesh)
            return;

        bool playerAlive = player != null && (playerHealth == null || !playerHealth.IsDead);
        if (playerAlive && Notices())
            lastSeenTime = Time.time;
        bool hunting = playerAlive && Time.time - lastSeenTime < giveUpTime;
        if (hunting && !wasHunting)
        {
            screamUntil = Time.time + screamTime;
            Alerted?.Invoke();
        }
        wasHunting = hunting;

        if (hunting && Time.time < screamUntil)
            Scream();
        else if (Time.time < attackBusyUntil)
            Hold();
        else if (!hunting)
            Wander();
        else if (Vector3.Distance(transform.position, player.position) <= attackRange)
            Attack();
        else
            Chase();

        if (pendingHitTime > 0f && Time.time >= pendingHitTime)
        {
            pendingHitTime = -1f;
            if (playerAlive && Vector3.Distance(transform.position, player.position) <= attackRange + 0.3f)
                playerHealth?.TakeDamage(attackDamage);
        }
    }

    bool Notices()
    {
        Vector3 toPlayer = player.position - transform.position;
        float distance = toPlayer.magnitude;

        float noise = playerMovement != null ? playerMovement.NoiseRadius : 0f;
        if (playerCombat != null && Time.time - playerCombat.LastNoiseTime < 0.5f)
            noise = Mathf.Max(noise, playerCombat.AttackNoise);
        if (distance < noise * hearing)
            return true;

        // Свет фонарика видно с любой стороны и дальше, чем самого игрока.
        bool lightVisible = playerFlashlight != null && playerFlashlight.IsOn && distance < playerFlashlight.AttractRange;
        bool inSight = distance <= sightRange && Vector3.Angle(transform.forward, toPlayer) <= sightAngle * 0.5f;
        if (!lightVisible && !inSight)
            return false;

        // Стены и дома загораживают обзор.
        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 target = player.position + Vector3.up * 1.2f;
        if (Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);
        return true;
    }

    void Wander()
    {
        Current = State.Wander;
        agent.speed = wanderSpeed;
        agent.isStopped = false;

        if (agent.pathPending || agent.remainingDistance > 0.3f || Time.time < nextWanderTime)
            return;

        Vector2 offset = UnityEngine.Random.insideUnitCircle * wanderRadius;
        Vector3 point = home + new Vector3(offset.x, 0f, offset.y);
        if (NavMesh.SamplePosition(point, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
            agent.SetDestination(navHit.position);
        nextWanderTime = Time.time + wanderPause + UnityEngine.Random.Range(0f, wanderPause);
    }

    void Scream()
    {
        Hold();
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 360f * Time.deltaTime);
    }

    // Стоять на месте, пока играет крик или удар. Скорость обнуляем сразу:
    // isStopped только перестаёт вести по пути, а разгон ещё несёт агента вперёд.
    void Hold()
    {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
    }

    void Chase()
    {
        Current = State.Chase;
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        agent.SetDestination(player.position);
    }

    void Attack()
    {
        Current = State.Attack;
        Hold();

        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 540f * Time.deltaTime);

        if (Time.time < nextAttackTime)
            return;
        nextAttackTime = Time.time + attackCooldown;
        pendingHitTime = Time.time + attackHitDelay;
        attackBusyUntil = Time.time + Mathf.Max(attackAnimTime, attackHitDelay + 0.2f);
        Attacked?.Invoke();
    }

    void OnDied()
    {
        Current = State.Dead;
        pendingHitTime = -1f;
        if (agent.isOnNavMesh)
            agent.isStopped = true;
        agent.enabled = false;

        // Тело остаётся лежать, но больше не мешает проходу и ударам.
        foreach (var c in GetComponentsInChildren<Collider>())
            c.enabled = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
