using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Бой героя. В руках — предмет из слота «оружие» (GameSession): его урон, дальность и прочность
// берутся из items.json старой игры. По виду предмета выбирается анимация:
//   одноручное (нож, молоток) — удар ножом, двуручное (монтировка, топор, лопата…) — удар монтировкой,
//   огнестрел (ПМ, ружьё ИЖ) — выстрел, патроны из инвентаря, перезарядка на R.
// Клавиши 1/2/3 берут в руки лучшее одноручное, двуручное и огнестрельное из инвентаря.
//
// Удар состоит из одного момента: в кадр, где анимация бьёт, одновременно проходят урон,
// звук удара героя и звук попадания по врагу.
[RequireComponent(typeof(PlayerMovement))]
public class PlayerCombat : MonoBehaviour
{
    // Номера совпадают с параметром Weapon в Animator Controller героя.
    public const int OneHanded = 1;
    public const int TwoHanded = 2;
    public const int Gun = 3;

    // Сколько длится анимация удара или выстрела. Сборщик аниматора подгоняет под неё скорость клипа.
    public const float AttackDuration = 0.49f;
    const float MeleeCooldown = 0.5f;
    // В старой игре урон шёл по 100 HP «Серых» с другим ритмом боя; так нож бьёт ~18, топор ~44.
    const float DamageScale = 2.2f;

    [Tooltip("Прибавка к урону монтировки после того, как Ким её окуёт (флаг heavy_weapon)")]
    [SerializeField] float heavyWeaponBonus = 15f;
    [Tooltip("Урон голыми руками, когда в руках ничего нет")]
    [SerializeField] float fistDamage = 6f;

    PlayerMovement movement;
    PlayerNeeds needs;
    Health health;
    InputAction attackAction;
    InputAction oneHandedAction;
    InputAction twoHandedAction;
    float nextAttackTime;
    float cooldownLength = 1f;
    float pendingHitTime = -1f;
    int pendingClass;
    int lastClass = -1;
    string lastWeapon;

    public int CurrentClass => ClassOf(GameSession.Equipped("weapon"));
    public string CurrentName => GameSession.Equipped("weapon")?.Def?.name ?? "Кулаки";
    public float CooldownLeft => Mathf.Clamp01((nextAttackTime - Time.time) / cooldownLength);

    public event Action Attacked;
    // Момент удара: анимация бьёт, звучит удар. true — по кому-то попали.
    public event Action<bool> Struck;
    public event Action<int> WeaponChanged;
    public event Action<Vector3, Vector3> Shot;

    public float LastNoiseTime { get; private set; } = float.NegativeInfinity;
    public float AttackNoise { get; private set; } = 6f;

    public static int ClassOf(ItemStack stack)
    {
        var def = stack?.Def;
        if (def == null)
            return OneHanded;
        if (def.type == "gun")
            return Gun;
        // Короткое и лёгкое держат одной рукой, остальное двумя.
        return def.weight < 0.7f ? OneHanded : TwoHanded;
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        needs = GetComponent<PlayerNeeds>();
        health = GetComponent<Health>();
        attackAction = InputSystem.actions.FindAction("Player/Attack", true);
        // Клавиши 1 и 2 в шаблоне действий называются Previous и Next.
        oneHandedAction = InputSystem.actions.FindAction("Player/Previous", true);
        twoHandedAction = InputSystem.actions.FindAction("Player/Next", true);
        GameSession.InventoryChanged += OnInventoryChanged;
    }

    void OnDestroy() => GameSession.InventoryChanged -= OnInventoryChanged;

    void Start() => OnInventoryChanged();

    void OnInventoryChanged()
    {
        string id = GameSession.Equipped("weapon")?.id;
        if (id == lastWeapon && CurrentClass == lastClass)
            return;
        lastWeapon = id;
        lastClass = CurrentClass;
        WeaponChanged?.Invoke(lastClass);
    }

    void Update()
    {
        bool alive = health == null || !health.IsDead;
        bool busy = pendingHitTime > 0f;

        if (alive && !busy)
        {
            if (oneHandedAction.WasPressedThisFrame()) TakeBest(OneHanded);
            else if (twoHandedAction.WasPressedThisFrame()) TakeBest(TwoHanded);
            else if (Keyboard.current != null && Keyboard.current.digit3Key.wasPressedThisFrame) TakeBest(Gun);
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Reload();
        }

        // Зажатая ЛКМ бьёт непрерывно, раз в перезарядку.
        if (alive && !busy && attackAction.IsPressed() && Time.time >= nextAttackTime)
            StartAttack();

        if (pendingHitTime > 0f && Time.time >= pendingHitTime)
        {
            pendingHitTime = -1f;
            if (alive)
            {
                if (pendingClass == Gun) Fire();
                else Strike();
            }
        }
    }

    // Взять в руки самое сильное оружие нужного вида из инвентаря.
    void TakeBest(int weaponClass)
    {
        ItemStack best = null;
        foreach (var st in GameSession.Data.items)
            if (st.Def != null && (st.Def.type == "weapon" || st.Def.type == "gun") && ClassOf(st) == weaponClass &&
                (best == null || st.Def.F("dmg") > best.Def.F("dmg")))
                best = st;
        if (best == null)
        {
            if (ClassOf(GameSession.Equipped("weapon")) != weaponClass)
                GameMessages.Show(weaponClass == Gun ? "Огнестрельного оружия нет" : weaponClass == OneHanded ? "Одноручного оружия нет" : "Двуручного оружия нет");
            return;
        }
        GameSession.Equip(best);
        GameMessages.Show($"В руках: {best.Def.name}");
    }

    void StartAttack()
    {
        var weapon = GameSession.Equipped("weapon");
        int cls = ClassOf(weapon);
        if (cls == Gun && weapon.charge <= 0f)
        {
            nextAttackTime = Time.time + 0.4f;
            Sfx.Play(SoundId.Locked, transform.position + Vector3.up, 0.3f);
            GameMessages.Show("Пусто. Перезарядка — R");
            return;
        }

        FaceCursor();
        float impact = cls == Gun ? 0.1f : AttackDuration * (cls == TwoHanded ? 0.65f : 0.5f);
        cooldownLength = cls == Gun ? Mathf.Max(weapon.Def.F("cd", 0.5f), AttackDuration) : MeleeCooldown;
        nextAttackTime = Time.time + cooldownLength;
        pendingHitTime = Time.time + impact;
        pendingClass = cls;
        AttackNoise = weapon?.Def != null ? weapon.Def.F("noise", 4f) * 2f : 4f;
        LastNoiseTime = Time.time;
        movement.Lock(Mathf.Max(cls == TwoHanded ? 0.25f : 0.15f, impact));
        Attacked?.Invoke();
    }

    // Шум без удара: обыск ящика, открытый люк. EnemyAI слышит его на расстоянии AttackNoise.
    public void MakeNoise()
    {
        LastNoiseTime = Time.time;
    }

    void Strike()
    {
        var weapon = GameSession.Equipped("weapon");
        var def = weapon?.Def;
        float damage = def != null ? def.F("dmg") * DamageScale : fistDamage;
        if (weapon != null && weapon.id == "crowbar" && GameSession.HasFlag("heavy_weapon"))
            damage += heavyWeaponBonus;
        if (needs != null)
            damage *= needs.MeleeMultiplier;
        float reach = def != null ? 0.6f + def.F("range", 1f) : 1.2f;
        float radius = Mathf.Min(0.8f, reach * 0.45f);
        Vector3 center = transform.position + Vector3.up + transform.forward * (reach - radius);

        bool hitAny = false;
        var struck = new HashSet<Health>();
        foreach (var hit in Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            var target = hit.GetComponentInParent<Health>();
            // У цели может быть несколько коллайдеров: урон один раз за удар.
            if (target == null || target == health || target.IsDead || !struck.Add(target))
                continue;
            target.TakeDamage(damage, "hit");
            Sfx.Play(SoundId.Hit, hit.ClosestPoint(center), 0.42f);
            hitAny = true;
        }
        LastNoiseTime = Time.time;
        Struck?.Invoke(hitAny);
        if (hitAny)
        {
            GameSession.AddXp("melee", 4f);
            Wear(weapon);
        }
    }

    // Прочность оружия падает с каждым попаданием; сломанное пропадает из рук.
    void Wear(ItemStack weapon)
    {
        if (weapon == null || weapon.dur < 0f)
            return;
        weapon.dur -= 1f;
        if (weapon.dur <= 0f)
        {
            GameMessages.Show($"Сломалось: {weapon.Def.name}");
            GameSession.RemoveStack(weapon);
        }
    }

    void Fire()
    {
        var gun = GameSession.Equipped("weapon");
        if (gun == null || gun.charge <= 0f)
            return;
        gun.charge -= 1f;
        var def = gun.Def;
        int pellets = Mathf.Max(1, (int)def.F("pellets", 1f));
        float range = def.F("range", 8f) * 1.5f;
        // Чем выше навык «Стрельба», тем меньше разброс.
        float spread = Mathf.Max(1.5f, 7f - GameSession.Skill("aim")) * (pellets > 1 ? 2f : 1f);
        Vector3 origin = transform.position + Vector3.up * 1.35f + transform.forward * 0.4f;
        bool hitAny = false;

        for (int i = 0; i < pellets; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, UnityEngine.Random.Range(-spread, spread), 0f) * transform.forward;
            Vector3 end = origin + dir * range;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, range, ~0, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var target = hit.collider.GetComponentInParent<Health>();
                if (target != null && target != health && !target.IsDead)
                {
                    target.TakeDamage(def.F("dmg") * 1.5f, "bullet");
                    Sfx.Play(SoundId.Hit, hit.point, 0.5f);
                    hitAny = true;
                }
            }
            Shot?.Invoke(origin, end);
        }
        Sfx.Play(SoundId.Gunshot, origin, 0.8f, 0.05f);
        LastNoiseTime = Time.time;
        GameSession.AddXp("aim", hitAny ? 6f : 2f);
        GameSession.NotifyChanged();
        Struck?.Invoke(hitAny);
    }

    void Reload()
    {
        var gun = GameSession.Equipped("weapon");
        if (gun?.Def == null || gun.Def.type != "gun")
            return;
        int mag = (int)gun.Def.F("mag", 1f);
        int need = mag - (int)Mathf.Max(0f, gun.charge);
        string ammo = gun.Def.S("ammo");
        int have = GameSession.Count(ammo);
        if (need <= 0) { GameMessages.Show("Оружие заряжено"); return; }
        if (have <= 0) { GameMessages.Show("Нет патронов"); return; }
        int n = Mathf.Min(need, have);
        GameSession.Remove(ammo, n);
        gun.charge = Mathf.Max(0f, gun.charge) + n;
        nextAttackTime = Time.time + 1.5f;
        cooldownLength = 1.5f;
        Sfx.Play(SoundId.Click, transform.position + Vector3.up, 0.5f);
        GameMessages.Show($"Зарядил: {gun.charge:0}/{mag}");
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
}
