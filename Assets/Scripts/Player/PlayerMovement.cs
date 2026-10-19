using UnityEngine;
using UnityEngine.InputSystem;

// Ходьба, бег и присед. Направление считается от камеры: W всегда ведёт
// «вверх по экрану», как бы ни была повёрнута изометрическая камера.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Скорость, м/с")]
    [SerializeField] float walkSpeed = 3f;
    [SerializeField] float runSpeed = 5.5f;
    [SerializeField] float crouchSpeed = 1.5f;

    [Header("Повороты и падение")]
    [Tooltip("Градусов в секунду")]
    [SerializeField] float turnSpeed = 720f;
    [SerializeField] float gravity = -20f;

    [Header("Выносливость (бег)")]
    [SerializeField] float maxStamina = 100f;
    [Tooltip("Сколько тратит бег за секунду. 100 / 22 ≈ 4,5 с бега с полной выносливостью")]
    [SerializeField] float staminaDrain = 22f;
    [Tooltip("Сколько восстанавливается за секунду, когда не бежишь")]
    [SerializeField] float staminaRegen = 14f;
    [Tooltip("Пауза перед восстановлением после бега, с")]
    [SerializeField] float regenDelay = 0.8f;
    [Tooltip("Выбившись из сил, снова побежать можно только с этим запасом")]
    [SerializeField] float recoverThreshold = 30f;

    [Header("Шум: на каком расстоянии враги слышат шаги, м")]
    [SerializeField] float walkNoise = 6f;
    [SerializeField] float runNoise = 12f;
    [SerializeField] float crouchNoise = 2f;

    [Tooltip("Камера, от которой считается направление. Если пусто, берётся Camera.main")]
    [SerializeField] Transform cameraTransform;

    CharacterController controller;
    InputAction moveAction;
    InputAction sprintAction;
    InputAction crouchAction;
    float verticalVelocity;
    float lockedUntil;
    PlayerNeeds needs;
    float stamina;
    float regenAt;
    bool exhausted;

    // Аниматор и ИИ будут читать эти значения: скорость для смены Idle/Walk/Run,
    // присед для шума (враги слышат бегущего дальше, чем крадущегося).
    public float CurrentSpeed { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool CanMove { get; set; } = true;
    public float Stamina01 => stamina / maxStamina;
    public bool Exhausted => exhausted;

    public float NoiseRadius
    {
        get
        {
            if (CurrentSpeed < 0.1f)
                return 0f;
            float noise = IsCrouching ? crouchNoise : IsRunning ? runNoise : walkNoise;
            return needs != null ? noise * needs.NoiseMultiplier : noise;
        }
    }

    // Остановить игрока на время, например пока идёт замах.
    public void Lock(float seconds)
    {
        lockedUntil = Mathf.Max(lockedUntil, Time.time + seconds);
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        needs = GetComponent<PlayerNeeds>();
        stamina = maxStamina;

        // Действия заданы в Assets/InputSystem_Actions, карта Player.
        moveAction = InputSystem.actions.FindAction("Player/Move", true);
        sprintAction = InputSystem.actions.FindAction("Player/Sprint", true);
        crouchAction = InputSystem.actions.FindAction("Player/Crouch", true);

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        var health = GetComponent<Health>();
        if (health != null)
            health.Died += () => CanMove = false;
    }

    void Update()
    {
        bool locked = !CanMove || Time.time < lockedUntil;
        Vector2 input = locked ? Vector2.zero : moveAction.ReadValue<Vector2>();
        Vector3 direction = CameraRelative(input);

        IsCrouching = CanMove && crouchAction.IsPressed();
        IsRunning = !IsCrouching && !exhausted && stamina > 0f && sprintAction.IsPressed() && direction.sqrMagnitude > 0.01f;
        UpdateStamina();
        // Навык «Скрытность» растёт, пока крадёшься (в старой игре так же).
        if (IsCrouching && CurrentSpeed > 0.5f)
            GameSession.AddXp("sneak", 0.4f * Time.deltaTime);
        float speed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;
        // Усталость, перелом ноги и перегруз замедляют (PlayerNeeds).
        if (needs != null)
            speed *= needs.SpeedMultiplier;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
        }

        // Небольшая постоянная сила вниз, иначе isGrounded мигает на ровном полу.
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = direction * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        Vector3 horizontal = controller.velocity;
        horizontal.y = 0f;
        CurrentSpeed = horizontal.magnitude;
    }

    void UpdateStamina()
    {
        if (IsRunning)
        {
            stamina = Mathf.Max(0f, stamina - staminaDrain * Time.deltaTime);
            regenAt = Time.time + regenDelay;
            if (stamina <= 0f)
            {
                exhausted = true;
                GameMessages.Show("Выбился из сил");
            }
        }
        else if (Time.time >= regenAt)
        {
            stamina = Mathf.Min(maxStamina, stamina + staminaRegen * Time.deltaTime);
            if (exhausted && stamina >= recoverThreshold)
                exhausted = false;
        }
    }

    Vector3 CameraRelative(Vector2 input)
    {
        if (cameraTransform == null)
            return new Vector3(input.x, 0f, input.y);

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 right = cameraTransform.right;
        right.y = 0f;
        right.Normalize();

        // По диагонали без ограничения игрок шёл бы в 1,4 раза быстрее.
        return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
    }
}
