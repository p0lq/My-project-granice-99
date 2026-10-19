using UnityEngine;

// Шаги по пройденному расстоянию, а не по таймеру: при беге шаги чаще,
// стоишь на месте — тишина. Работает и для игрока, и для «Серых».
public class Footsteps : MonoBehaviour
{
    [Tooltip("Длина шага, м")]
    [SerializeField] float stepLength = 0.75f;
    [SerializeField] float volume = 0.5f;

    PlayerMovement movement;
    Health health;
    Vector3 lastPosition;
    float travelled;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        health = GetComponent<Health>();
        lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 delta = transform.position - lastPosition;
        delta.y = 0f;
        lastPosition = transform.position;
        if (health != null && health.IsDead)
            return;

        travelled += delta.magnitude;
        float length = stepLength;
        float loud = volume;
        if (movement != null)
        {
            // Бегом шаги длиннее и громче, крадучись короче и почти беззвучны.
            if (movement.IsRunning) { length *= 1.35f; loud *= 1.4f; }
            if (movement.IsCrouching) { length *= 0.7f; loud *= 0.3f; }
        }

        if (travelled >= length)
        {
            travelled = 0f;
            Sfx.Play(SoundId.Footstep, transform.position, loud, 0.12f);
        }
    }
}
