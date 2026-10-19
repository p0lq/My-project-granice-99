using UnityEngine;
using UnityEngine.InputSystem;

// Ищет рядом с игроком объект для взаимодействия и срабатывает по E.
// Из нескольких ближних выбирается тот, что ближе и больше спереди.
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] float radius = 1.8f;

    InputAction interactAction;
    Health health;
    readonly Collider[] hits = new Collider[16];

    public Interactable Current { get; private set; }

    void Awake()
    {
        interactAction = InputSystem.actions.FindAction("Player/Interact", true);
        health = GetComponent<Health>();
    }

    void Update()
    {
        Current = health != null && health.IsDead ? null : FindBest();
        if (Current != null && interactAction.WasPressedThisFrame())
            Current.Interact(this);
    }

    Interactable FindBest()
    {
        Vector3 center = transform.position + Vector3.up;
        int count = Physics.OverlapSphereNonAlloc(center, radius, hits, ~0, QueryTriggerInteraction.Collide);

        Interactable best = null;
        float bestScore = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            var candidate = hits[i].GetComponentInParent<Interactable>();
            if (candidate == null || !candidate.CanInteract)
                continue;

            Vector3 to = hits[i].ClosestPoint(center) - center;
            to.y = 0f;
            // Объект за спиной считается дальше, чем он есть.
            float behind = Vector3.Dot(transform.forward, to.normalized) < 0f ? 1f : 0f;
            float score = to.magnitude + behind;
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }
        return best;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, radius);
    }
}
