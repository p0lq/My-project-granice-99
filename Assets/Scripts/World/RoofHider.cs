using UnityEngine;

// Крыша дома, в который можно зайти. Камера смотрит сверху, поэтому, пока игрок
// внутри, крыша прячется, а когда выходит — появляется снова.
public class RoofHider : MonoBehaviour
{
    [Tooltip("Размер помещения, м. Центр — позиция этого объекта")]
    [SerializeField] Vector3 roomSize = new Vector3(8f, 4f, 6f);
    [SerializeField] Renderer[] roof = new Renderer[0];

    Transform player;
    bool hidden;

    // Игрок под крышей хоть какого-то дома: там прохладнее днём и теплее ночью (PlayerNeeds).
    public static bool PlayerInside { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => PlayerInside = false;

    void OnDestroy()
    {
        if (hidden)
            PlayerInside = false;
    }

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        player = p != null ? p.transform : null;
    }

    void Update()
    {
        if (player == null)
            return;
        Vector3 local = player.position - transform.position;
        bool inside = Mathf.Abs(local.x) < roomSize.x / 2f && Mathf.Abs(local.z) < roomSize.z / 2f && local.y < roomSize.y;
        if (inside == hidden)
            return;
        hidden = inside;
        PlayerInside = inside;
        foreach (var r in roof)
            if (r != null)
                r.enabled = !inside;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position + Vector3.up * roomSize.y / 2f, roomSize);
    }
}
