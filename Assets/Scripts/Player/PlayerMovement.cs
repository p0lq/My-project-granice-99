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

    [Tooltip("Камера, от которой считается направление. Если пусто, берётся Camera.main")]
    [SerializeField] Transform cameraTransform;

    CharacterController controller;
    InputAction moveAction;
    InputAction sprintAction;
    InputAction crouchAction;
    float verticalVelocity;

    // Аниматор и ИИ будут читать эти значения: скорость для смены Idle/Walk/Run,
    // присед для шума (враги слышат бегущего дальше, чем крадущегося).
    public float CurrentSpeed { get; private set; }
    public bool IsRunning { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool CanMove { get; set; } = true;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

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
        Vector2 input = CanMove ? moveAction.ReadValue<Vector2>() : Vector2.zero;
        Vector3 direction = CameraRelative(input);

        IsCrouching = CanMove && crouchAction.IsPressed();
        IsRunning = !IsCrouching && sprintAction.IsPressed() && direction.sqrMagnitude > 0.01f;
        float speed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;

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
