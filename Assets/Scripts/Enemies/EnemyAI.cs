using System;
using UnityEngine;
using UnityEngine.AI;

// «Серый». Бродит вокруг точки появления, замечает игрока глазами или на слух,
// преследует и бьёт. Видит плохо и недалеко (ночью ещё хуже), слышит хорошо: бег и удары
// выдают игрока, присед почти нет.
// С галочкой human и ranged это бандит «Волков степи»: видит дальше, не кричит и стреляет
// из ПМ, если игрок в прямой видимости. Мёртвого врага можно обыскать (таблица corpseLoot).
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Health))]
public class EnemyAI : MonoBehaviour, ISaveable
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

    [Header("Двери")]
    [Tooltip("С какого расстояния бьёт в закрытую дверь, м")]
    [SerializeField] float bashRange = 1.6f;
    [Tooltip("Насколько далеко от игрока ищет дверь, за которой он спрятался, м")]
    [SerializeField] float doorSearchRadius = 10f;

    [Header("Бандит")]
    [Tooltip("Человек, а не «Серый»: не кричит, не дышит хрипло, ночью видит как днём")]
    [SerializeField] bool human;
    [Tooltip("Стреляет издалека")]
    [SerializeField] bool ranged;
    [SerializeField] float shootRange = 10f;
    [SerializeField] float shootCooldown = 1.6f;
    [SerializeField] float shotDamage = 12f;
    [Tooltip("Шанс попасть вблизи; с расстоянием падает")]
    [SerializeField] float accuracy = 0.65f;
    [Tooltip("Таблица добычи тела из loot.json")]
    [SerializeField] string corpseLoot = "corpse_infected";
    [SerializeField] string corpseName = "тело «Серого»";

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
    float pendingShotTime = -1f;
    Vector3 startPosition;

    public State Current { get; private set; } = State.Wander;

    // Все «Серые» сцены: по ним проверяется, можно ли спать.
    public static readonly System.Collections.Generic.List<EnemyAI> All = new();

    void OnEnable() => All.Add(this);

    void OnDisable() => All.Remove(this);
    public float CurrentSpeed => agent != null && agent.enabled ? agent.velocity.magnitude : 0f;
    public event Action Attacked;
    public event Action<Vector3, Vector3> Fired;
    public bool Human => human;
    // Заметил игрока после блуждания: CharacterAnimator включает анимацию Scream.
    public event Action Alerted;
    // Ударил в дверь: та же анимация Attack, но без звука удара по игроку.
    public event Action Bashed;

    void Awake()
    {
        startPosition = transform.position;
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
        else if (ranged && Vector3.Distance(transform.position, player.position) <= shootRange && SeesPlayer())
            Shoot();
        else if (Vector3.Distance(transform.position, player.position) <= attackRange && DoorBetween() == null)
            Attack();
        else
            Chase();

        if (pendingShotTime > 0f && Time.time >= pendingShotTime)
        {
            pendingShotTime = -1f;
            if (playerAlive)
                ResolveShot();
        }

        if (pendingHitTime > 0f && Time.time >= pendingHitTime)
        {
            pendingHitTime = -1f;
            if (playerAlive && Vector3.Distance(transform.position, player.position) <= attackRange + 0.3f && DoorBetween() == null)
                playerHealth?.TakeDamage(attackDamage, "infected");
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
        // «Серые» ночью видят плохо, люди — как днём.
        float sight = human || !WorldClock.IsNight ? sightRange : sightRange * 0.6f;
        bool inSight = distance <= sight && Vector3.Angle(transform.forward, toPlayer) <= sightAngle * 0.5f;
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

        // Путь до игрока не достраивается до конца (PathPartial) — значит, он закрылся
        // за дверью. Идём к этой двери и ломимся в неё.
        bool blocked = agent.hasPath && !agent.pathPending && agent.pathStatus == NavMeshPathStatus.PathPartial;
        var door = DoorBetween() ?? (blocked ? Door.NearestBashable(player.position, doorSearchRadius) : null);
        if (door != null)
        {
            Vector3 toDoor = door.Center - transform.position;
            toDoor.y = 0f;
            if (toDoor.magnitude <= bashRange)
            {
                BashDoor(door, toDoor);
                return;
            }
            agent.SetDestination(door.Center);
            return;
        }
        agent.SetDestination(player.position);
    }

    void BashDoor(Door door, Vector3 toDoor)
    {
        Current = State.Attack;
        Hold();
        if (toDoor.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toDoor), 540f * Time.deltaTime);

        // Пока ломится, игрока не теряет: за дверью слышно, что тот внутри.
        if (Vector3.Distance(transform.position, player.position) < doorSearchRadius)
            lastSeenTime = Time.time;

        if (Time.time < nextAttackTime)
            return;
        nextAttackTime = Time.time + attackCooldown;
        attackBusyUntil = Time.time + Mathf.Max(attackAnimTime, attackHitDelay + 0.2f);
        door.Bash(attackDamage);
        Bashed?.Invoke();
    }

    // Закрытая дверь, которая стоит прямо между «Серым» и игроком (или null).
    Door DoorBetween()
    {
        Vector3 from = transform.position + Vector3.up * 1.2f;
        Vector3 to = player.position + Vector3.up * 1.2f;
        if (!Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return null;
        var door = hit.collider.GetComponentInParent<Door>();
        return door != null && door.CanBeBashed ? door : null;
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

        // Тело можно обыскать: невидимый триггер для E и «ящик» с добычей из loot.json.
        var trigger = gameObject.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.8f;
        trigger.center = new Vector3(0f, 0.4f, 0f);
        var corpse = gameObject.AddComponent<SearchContainer>();
        corpse.Setup(corpseName, corpseLoot, "corpse:" + SaveKey);
    }

    bool SeesPlayer()
    {
        Vector3 eye = transform.position + Vector3.up * 1.5f;
        Vector3 target = player.position + Vector3.up * 1.2f;
        if (!Physics.Linecast(eye, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
            return true;
        return hit.transform == player || hit.transform.IsChildOf(player);
    }

    // Бандит встаёт и стреляет; выстрел звучит через 0,15 с, когда анимация поднимает оружие.
    void Shoot()
    {
        Current = State.Attack;
        Hold();
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(dir), 540f * Time.deltaTime);
        if (Time.time < nextAttackTime)
            return;
        nextAttackTime = Time.time + shootCooldown * UnityEngine.Random.Range(0.85f, 1.2f);
        attackBusyUntil = Time.time + 0.6f;
        pendingShotTime = Time.time + 0.15f;
        Attacked?.Invoke();
    }

    void ResolveShot()
    {
        Vector3 from = transform.position + Vector3.up * 1.35f + transform.forward * 0.4f;
        Vector3 to = player.position + Vector3.up * 1.1f;
        float distance = Vector3.Distance(transform.position, player.position);
        float chance = accuracy * Mathf.Clamp01(1f - distance / (shootRange * 1.6f));
        if (playerMovement != null && playerMovement.IsCrouching)
            chance *= 0.75f;
        bool hit = SeesPlayer() && UnityEngine.Random.value < chance;
        if (hit)
            playerHealth?.TakeDamage(shotDamage, "bullet");
        else
            to += new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-0.3f, 0.5f), UnityEngine.Random.Range(-1f, 1f));
        Sfx.Play(SoundId.Gunshot, from, 0.75f, 0.05f);
        Fired?.Invoke(from, to);
    }

    // ---------------------------------------------------------------- сохранение

    public string SaveKey => SaveKeys.Of(this, startPosition);

    public string SaveState()
    {
        var p = transform.position;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        return $"{(Current == State.Dead ? "dead" : "alive")};{p.x.ToString(ci)};{p.z.ToString(ci)}";
    }

    public void LoadState(string state)
    {
        var parts = state.Split(';');
        if (parts.Length < 3)
            return;
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var pos = new Vector3(float.Parse(parts[1], ci), transform.position.y, float.Parse(parts[2], ci));
        if (agent.enabled && agent.isOnNavMesh)
            agent.Warp(pos);
        else
            transform.position = pos;
        if (parts[0] == "dead" && !health.IsDead)
            health.Drain(health.Current);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
