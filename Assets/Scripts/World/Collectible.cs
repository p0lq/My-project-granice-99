using System.Collections.Generic;
using UnityEngine;

// Предмет, который подбирается, если пройти через него. Крутится и покачивается,
// чтобы его было видно с изометрической камеры, а свет под ним заметен в темноте.
[RequireComponent(typeof(Collider))]
public class Collectible : MonoBehaviour
{
    [Tooltip("id предмета из Resources/Data/Legacy/items.json")]
    [SerializeField] string itemId = "water_clean";
    [SerializeField] int amount = 1;

    [Tooltip("Модель предмета, которая крутится. Если пусто, крутится весь объект")]
    [SerializeField] Transform visual;
    [SerializeField] float spinSpeed = 90f;
    [SerializeField] float bobHeight = 0.1f;

    Vector3 visualStart;
    // Выброшенный предмет хранит своё состояние (прочность ножа, патроны в ПМ).
    ItemStack stack;
    float pickupAfter;

    // Брошенные игроком предметы: их нет в сцене, поэтому их помнит сохранение.
    public static readonly List<Collectible> Dropped = new();

    public string ItemKey => itemId;
    public ItemStack Stack => stack ?? new ItemStack { id = itemId, count = amount };
    public bool PickedUp { get; private set; }

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

    void OnDestroy() => Dropped.Remove(this);

    void Update()
    {
        visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        visual.localPosition = visualStart + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobHeight);
    }

    // Stay, а не Enter: только что выброшенный предмет лежит под ногами, и подобрать его
    // можно, лишь отойдя и вернувшись (или подождав полторы секунды).
    void OnTriggerStay(Collider other)
    {
        if (PickedUp || Time.time < pickupAfter || !other.CompareTag("Player"))
            return;

        PickedUp = true;
        if (stack != null)
            GameSession.AddStack(stack);
        else
            GameSession.Add(itemId, amount);
        Sfx.Play(SoundId.Pickup, transform.position, 0.32f, 0.03f);
        int n = stack?.count ?? amount;
        GameMessages.Show(n > 1 ? $"Подобрал: {Items.Name(itemId)} ×{n}" : $"Подобрал: {Items.Name(itemId)}");
        Destroy(gameObject);
    }

    // Создать предмет на земле: выброшенный из инвентаря или выпавший из тела бандита.
    public static Collectible Spawn(ItemStack stack, Vector3 position, float delay = 1.5f)
    {
        var root = new GameObject($"Предмет: {Items.Name(stack.id)}");
        root.transform.position = position;
        var trigger = root.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 0.6f;
        trigger.center = new Vector3(0f, 0.5f, 0f);

        var model = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(model.GetComponent<Collider>());
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        model.transform.localScale = new Vector3(0.2f, 0.12f, 0.15f);
        model.GetComponent<Renderer>().material.color = new Color(0.3f, 0.7f, 0.9f);

        var glow = new GameObject("Подсветка").AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.85f, 0.5f);
        glow.intensity = 1.5f;
        glow.range = 2f;
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = Vector3.up * 0.4f;

        var c = root.AddComponent<Collectible>();
        c.itemId = stack.id;
        c.amount = stack.count;
        c.stack = stack;
        c.visual = model.transform;
        c.visualStart = model.transform.localPosition;
        c.pickupAfter = Time.time + delay;
        Dropped.Add(c);
        return c;
    }
}
