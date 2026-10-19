using UnityEngine;
using UnityEngine.SceneManagement;

// Шаги по пройденному расстоянию, а не по таймеру: при беге шаги чаще,
// стоишь на месте — тишина. Работает и для игрока, и для «Серых».
public class Footsteps : MonoBehaviour
{
    [Tooltip("Длина шага, м")]
    [SerializeField] float stepLength = 0.75f;
    [SerializeField] float volume = 0.28f;

    // Общее ослабление шагов на 15 %. Множителем в коде, а не правкой volume:
    // volume сохранён в уже собранных сценах и без пересборки бы не поменялся.
    const float Quieter = 0.85f;

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
        float loud = volume * Quieter;
        if (movement != null)
        {
            // Бегом шаги длиннее и громче, крадучись короче и почти беззвучны.
            if (movement.IsRunning) { length *= 1.35f; loud *= 1.4f; }
            if (movement.IsCrouching) { length *= 0.7f; loud *= 0.3f; }
        }

        if (travelled >= length)
        {
            travelled = 0f;
            // В бункере бетон, в посёлке трава и земля.
            var sound = SceneManager.GetActiveScene().name == SceneNames.Bunker ? SoundId.FootstepHard : SoundId.Footstep;
            Sfx.Play(sound, transform.position, loud, 0.08f);
        }
    }
}
