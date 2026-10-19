using UnityEngine;
using UnityEngine.InputSystem;

// Изометрическая камера: смотрит на игрока сверху под постоянным углом,
// плавно догоняет его, колесо мыши меняет расстояние.
public class IsoCamera : MonoBehaviour
{
    [SerializeField] Transform target;

    [Tooltip("Точка на персонаже, куда смотрит камера (примерно грудь)")]
    [SerializeField] Vector3 targetOffset = new Vector3(0f, 1f, 0f);

    [Header("Угол")]
    [Tooltip("Наклон вниз, градусов")]
    [Range(20f, 80f)]
    [SerializeField] float pitch = 40f;
    [Tooltip("Поворот вокруг вертикали, градусов. 45 даёт классическую изометрию")]
    [SerializeField] float yaw = 45f;

    [Header("Расстояние, м")]
    [SerializeField] float distance = 14f;
    [SerializeField] float minDistance = 8f;
    [SerializeField] float maxDistance = 22f;
    [SerializeField] float zoomStep = 1.5f;

    [Tooltip("Чем больше, тем быстрее камера догоняет игрока")]
    [SerializeField] float followSharpness = 8f;

    public Transform Target
    {
        get => target;
        set
        {
            target = value;
            Snap();
        }
    }

    void Start()
    {
        Snap();
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        if (Mouse.current != null)
        {
            // Шаг прокрутки на разных мышах разный, поэтому берём только знак.
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                distance = Mathf.Clamp(distance - Mathf.Sign(scroll) * zoomStep, minDistance, maxDistance);
        }

        // Такая формула сглаживания одинаково ведёт себя при 30 и при 144 FPS.
        float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, DesiredPosition(), t);
        transform.rotation = Rotation();
    }

    // Мгновенно поставить камеру на место, без догоняния (старт уровня, загрузка).
    public void Snap()
    {
        if (target == null)
            return;
        transform.position = DesiredPosition();
        transform.rotation = Rotation();
    }

    Quaternion Rotation() => Quaternion.Euler(pitch, yaw, 0f);

    Vector3 DesiredPosition() => target.position + targetOffset - Rotation() * Vector3.forward * distance;
}
