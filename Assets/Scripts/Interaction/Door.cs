using UnityEngine;
using UnityEngine.AI;

// Дверь на петлях. Объект с этим скриптом стоит на оси петель, полотно его дочерний объект,
// поэтому дверь поворачивается вокруг края, а не вокруг центра.
// Может быть заперта: открывается ключом (requiredItem) или рубильником (Unlock).
public class Door : Interactable
{
    [SerializeField] string doorName = "дверь";
    [SerializeField] float openAngle = 100f;
    [SerializeField] float speed = 4f;

    [Header("Замок")]
    [SerializeField] bool locked;
    [Tooltip("Если заперта и нужен предмет — каким ключом открывается")]
    [SerializeField] bool needsItem;
    [SerializeField] ItemId requiredItem = ItemId.BunkerKey;
    [Tooltip("Что написать, если дверь заперта и ключа нет")]
    [SerializeField] string lockedMessage = "Заперто";

    Quaternion closedRotation;
    Quaternion openRotation;
    bool open;

    public override string Prompt
    {
        get
        {
            if (locked)
                return needsItem && GameSession.Has(requiredItem) ? $"Отпереть {doorName}" : lockedMessage;
            return open ? $"Закрыть {doorName}" : $"Открыть {doorName}";
        }
    }

    void Awake()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);

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
                return;
            }
        }
        open = !open;
    }

    public void Unlock(bool openNow)
    {
        locked = false;
        if (openNow)
            open = true;
    }
}
