using UnityEngine;

// Показывает в руке героя оружие, которое сейчас выбрано: нож или монтировку.
// Пока нет моделей, оба собраны из примитивов в реальном размере (нож 0,28 м, монтировка 0,8 м).
// Крепятся к кости правой руки скелета Humanoid; у капсулы без скелета — к боку тела.
[RequireComponent(typeof(PlayerCombat))]
public class WeaponHolder : MonoBehaviour
{
    [Tooltip("Сдвиг рукояти от кости кисти, м (в осях кости)")]
    [SerializeField] Vector3 gripOffset = new Vector3(0f, 0.08f, 0.03f);
    [Tooltip("Поворот оружия относительно кисти. Оси костей у разных моделей разные, подгоняется здесь")]
    [SerializeField] Vector3 gripRotation = new Vector3(0f, 0f, 90f);

    GameObject knife;
    GameObject crowbar;
    GameObject gun;

    void Awake()
    {
        var combat = GetComponent<PlayerCombat>();
        combat.WeaponChanged += Show;

        Transform hand = null;
        var animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (hand == null)
            hand = transform;

        knife = Build("Одноручное", hand, BuildKnife);
        crowbar = Build("Двуручное", hand, BuildCrowbar);
        gun = Build("Огнестрел", hand, BuildGun);
        if (hand == transform)
        {
            // Капсула: оружие просто висит у правого бока на уровне кисти.
            foreach (var w in new[] { knife, crowbar, gun })
            {
                w.transform.localPosition = new Vector3(0.38f, 0.9f, 0.15f);
                w.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            }
        }
        Show(combat.CurrentClass);
    }

    // Пустые руки — ничего не показываем, иначе вид оружия по его классу.
    void Show(int weaponClass)
    {
        bool armed = GameSession.Equipped("weapon") != null;
        knife.SetActive(armed && weaponClass == PlayerCombat.OneHanded);
        crowbar.SetActive(armed && weaponClass == PlayerCombat.TwoHanded);
        gun.SetActive(armed && weaponClass == PlayerCombat.Gun);
    }

    GameObject Build(string name, Transform hand, System.Action<Transform> parts)
    {
        var root = new GameObject(name).transform;
        root.SetParent(hand, false);
        root.localPosition = gripOffset;
        root.localRotation = Quaternion.Euler(gripRotation);
        // Скелет Mixamo импортируется с масштабом; оружие должно быть в метрах мира.
        Vector3 s = hand.lossyScale;
        root.localScale = new Vector3(1f / Mathf.Max(s.x, 1e-4f), 1f / Mathf.Max(s.y, 1e-4f), 1f / Mathf.Max(s.z, 1e-4f));
        parts(root);
        return root.gameObject;
    }

    // Оружие направлено вдоль своей оси +Y от рукояти.
    static void BuildKnife(Transform root)
    {
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.03f, 0.11f, 0.03f), new Color(0.25f, 0.17f, 0.1f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.1f, 0f), new Vector3(0.06f, 0.012f, 0.03f), new Color(0.35f, 0.35f, 0.35f));
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.18f, 0f), new Vector3(0.025f, 0.16f, 0.006f), new Color(0.78f, 0.8f, 0.82f));
    }

    static void BuildCrowbar(Transform root)
    {
        var steel = new Color(0.32f, 0.33f, 0.36f);
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.25f, 0f), new Vector3(0.028f, 0.4f, 0.028f), steel);
        // Загнутый конец с раздвоенным «гвоздодёром».
        var hook = Part(root, PrimitiveType.Cylinder, new Vector3(0.035f, 0.67f, 0f), new Vector3(0.026f, 0.06f, 0.026f), steel);
        hook.transform.localRotation = Quaternion.Euler(0f, 0f, -50f);
        Part(root, PrimitiveType.Cube, new Vector3(0.075f, 0.69f, 0f), new Vector3(0.05f, 0.012f, 0.03f), steel);
        // Обмотка рукояти изолентой.
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0f, 0f), new Vector3(0.034f, 0.08f, 0.034f), new Color(0.1f, 0.1f, 0.12f));
    }

    static void BuildGun(Transform root)
    {
        var dark = new Color(0.12f, 0.12f, 0.13f);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.04f, 0f), new Vector3(0.03f, 0.1f, 0.025f), dark);
        Part(root, PrimitiveType.Cube, new Vector3(0f, 0.1f, 0.06f), new Vector3(0.03f, 0.035f, 0.16f), dark);
    }

    static GameObject Part(Transform root, PrimitiveType type, Vector3 pos, Vector3 size, Color color)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(root, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var renderer = go.GetComponent<Renderer>();
        // Материал создаётся в игре: у оружия всего два цвета, ассеты под них не нужны.
        renderer.material.color = color;
        return go;
    }
}
