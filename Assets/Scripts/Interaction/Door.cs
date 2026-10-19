using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Дверь на петлях. Объект с этим скриптом стоит на оси петель, полотно его дочерний объект,
// поэтому дверь поворачивается вокруг края, а не вокруг центра.
// Может быть заперта: открывается ключом (requiredItem) или рубильником (Unlock).
// Деревянную закрытую дверь «Серые» выламывают (Bash), металлическую нет.
public class Door : Interactable, ISaveable
{
    // Все двери сцены, чтобы «Серый» нашёл ту, что отделяет его от игрока.
    static readonly List<Door> all = new();

    [SerializeField] string doorName = "дверь";
    [SerializeField] float openAngle = 100f;
    [SerializeField] float speed = 4f;
    [Tooltip("Металлическая (гермодверь): другой звук, выломать нельзя")]
    [SerializeField] bool metal;

    [Header("Замок")]
    [SerializeField] bool locked;
    [Tooltip("Если заперта и нужен предмет — каким ключом открывается")]
    [SerializeField] bool needsItem;
    [SerializeField] ItemId requiredItem = ItemId.BunkerKey;
    [Tooltip("Что написать, если дверь заперта и ключа нет")]
    [SerializeField] string lockedMessage = "Заперто";

    [Header("Прочность")]
    [Tooltip("Сколько урона выдерживает от «Серых». Один удар — 15, то есть около шести ударов")]
    [SerializeField] float strength = 90f;

    Quaternion closedRotation;
    Quaternion openRotation;
    bool open;
    bool broken;
    float lastBashTime = float.NegativeInfinity;
    AudioSource bashSound;
    Collider panel;

    public bool CanBeBashed => !metal && !broken && !open;
    public Vector3 Center => panel != null ? panel.bounds.center : transform.position;
    public override bool CanInteract => !broken;

    public override string Prompt
    {
        get
        {
            if (locked)
                return needsItem && GameSession.Has(requiredItem) ? $"Отпереть {doorName}" : lockedMessage;
            return open ? $"Закрыть {doorName}" : $"Открыть {doorName}";
        }
    }

    // Ближайшая к точке закрытая дверь, которую можно выломать, не дальше maxDistance.
    public static Door NearestBashable(Vector3 point, float maxDistance)
    {
        Door best = null;
        float bestDistance = maxDistance;
        foreach (var door in all)
        {
            if (!door.CanBeBashed)
                continue;
            float d = Vector3.Distance(point, door.Center);
            if (d < bestDistance)
            {
                bestDistance = d;
                best = door;
            }
        }
        return best;
    }

    string saveKey;
    public string SaveKey => saveKey ??= SaveKeys.Of(this, transform.position);
    public string SaveState() => $"{(open ? 1 : 0)};{(broken ? 1 : 0)};{(locked ? 1 : 0)};{strength.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    public void LoadState(string state)
    {
        var p = state.Split(';');
        if (p.Length < 4)
            return;
        open = p[0] == "1";
        locked = p[2] == "1";
        strength = float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture);
        if (p[1] == "1" && !broken)
            Break(false);
        transform.localRotation = open ? openRotation : closedRotation;
    }

    void OnEnable() => all.Add(this);

    void OnDisable() => all.Remove(this);

    void Awake()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        panel = GetComponentInChildren<Collider>();

        // Дверь меняет проходимость, поэтому в сетку навигации её не запекаем,
        // а вырезаем препятствием, которое двигается вместе с полотном.
        foreach (var c in GetComponentsInChildren<BoxCollider>())
        {
            if (c.GetComponent<NavMeshObstacle>() != null)
                continue;
            var obstacle = c.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = c.center;
            obstacle.size = c.size;
            obstacle.carving = true;
        }
    }

    void Update()
    {
        var target = open ? openRotation : closedRotation;
        transform.localRotation = Quaternion.Slerp(transform.localRotation, target, 1f - Mathf.Exp(-speed * Time.deltaTime));

        // Звук ломки идёт, пока в дверь бьют, и затихает через пару секунд после последнего удара.
        if (bashSound != null && bashSound.isPlaying && Time.time - lastBashTime > 2f)
        {
            bashSound.volume = Mathf.MoveTowards(bashSound.volume, 0f, Time.deltaTime * 0.5f);
            if (bashSound.volume <= 0f)
                bashSound.Stop();
        }
    }

    public override void Interact(PlayerInteractor player)
    {
        if (locked)
        {
            if (needsItem && GameSession.Has(requiredItem))
            {
                locked = false;
                GameMessages.Show($"{Items.Name(requiredItem)} подошёл");
            }
            else
            {
                GameMessages.Show(lockedMessage);
                Sfx.Play(SoundId.Locked, transform.position + Vector3.up, 0.28f);
                return;
            }
        }
        open = !open;
        Sfx.Play(OpenCloseSound(), transform.position + Vector3.up, 0.42f);
    }

    SoundId OpenCloseSound() => metal ? SoundId.MetalDoor : open ? SoundId.DoorOpen : SoundId.DoorClose;

    public void Unlock(bool openNow)
    {
        locked = false;
        if (openNow && !open)
        {
            open = true;
            Sfx.Play(OpenCloseSound(), transform.position + Vector3.up, 0.5f, 0f);
        }
    }

    // Удар «Серого» по закрытой двери.
    public void Bash(float damage)
    {
        if (!CanBeBashed)
            return;
        lastBashTime = Time.time;
        PlayBashSound();

        // Полотно вздрагивает от удара, потом Update возвращает его на место.
        transform.localRotation = closedRotation * Quaternion.Euler(0f, Random.Range(-4f, 4f), 0f);

        strength -= damage;
        if (strength <= 0f)
            Break();
    }

    void PlayBashSound()
    {
        if (bashSound == null)
        {
            var clip = Sfx.Clip(SoundId.DoorBash);
            if (clip == null)
                return;
            bashSound = gameObject.AddComponent<AudioSource>();
            bashSound.clip = clip;
            bashSound.loop = true;
            // Ломку слышно дальше обычных звуков: это предупреждение игроку.
            Sfx.Make3D(bashSound, 3f, 26f);
        }
        // Записи из Downloads тише исходной громкости на 35 % (как в CharacterSounds).
        bashSound.volume = 0.65f * Sfx.Volume;
        if (!bashSound.isPlaying)
        {
            bashSound.time = Random.Range(0f, bashSound.clip.length * 0.8f);
            bashSound.Play();
        }
    }

    void Break(bool announce = true)
    {
        broken = true;
        locked = false;
        open = true;
        // Выломанная дверь висит криво и больше не закрывается.
        openRotation = closedRotation * Quaternion.Euler(8f, openAngle * 0.8f, 0f);
        foreach (var obstacle in GetComponentsInChildren<NavMeshObstacle>())
            obstacle.enabled = false;
        if (!announce)
            return;
        Sfx.Play(SoundId.DoorClose, transform.position + Vector3.up, 0.6f, 0f);
        GameMessages.Show($"«Серые» выломали {doorName}!");
    }
}
