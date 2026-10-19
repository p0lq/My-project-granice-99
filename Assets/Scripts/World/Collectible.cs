using UnityEngine;

// Предмет, который подбирается, если пройти через него. Крутится и покачивается,
// чтобы его было видно с изометрической камеры, а свет под ним заметен в темноте.
[RequireComponent(typeof(Collider))]
public class Collectible : MonoBehaviour
{
    [SerializeField] ItemId item = ItemId.Water;
    [SerializeField] int amount = 1;

    [Tooltip("Модель предмета, которая крутится. Если пусто, крутится весь объект")]
    [SerializeField] Transform visual;
    [SerializeField] float spinSpeed = 90f;
    [SerializeField] float bobHeight = 0.1f;

    Vector3 visualStart;

    public ItemId Item => item;

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void Awake()
    {
        if (visual == null)
            visual = transform;
        visualStart = visual.localPosition;
    }

    void Update()
    {
        visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        visual.localPosition = visualStart + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobHeight);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        GameSession.Add(item, amount);
        GameMessages.Show(amount > 1 ? $"Подобрал: {Items.Name(item)} ×{amount}" : $"Подобрал: {Items.Name(item)}");
        Destroy(gameObject);
    }
}
