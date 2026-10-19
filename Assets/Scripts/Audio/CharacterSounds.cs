using UnityEngine;

// Голос персонажа. Сам определяет, кто он: на игроке это удар и стон от урона,
// на «Сером» дыхание, крик при обнаружении, удар и предсмертный хрип.
public class CharacterSounds : MonoBehaviour
{
    // Записи из Downloads громкие, по просьбе их тише исходной громкости файла на 35 %.
    const float RecordedVolume = 0.65f;

    [Header("Дыхание «Серого»")]
    [Tooltip("Ближе этого расстояния дыхание слышно в полную силу, м")]
    [SerializeField] float breathFullDistance = 1.5f;
    [Tooltip("Дальше этого расстояния дыхание не слышно совсем, м")]
    [SerializeField] float breathMaxDistance = 9f;

    Health health;
    EnemyAI enemy;
    AudioSource breath;

    void Awake()
    {
        health = GetComponent<Health>();
        enemy = GetComponent<EnemyAI>();
        var combat = GetComponent<PlayerCombat>();

        // Звук удара героя — в момент удара анимации, вместе с уроном и звуком попадания.
        if (combat != null)
        {
            combat.Struck += _ =>
            {
                if (combat.CurrentClass != PlayerCombat.Gun)
                    Sfx.Play(SoundId.PlayerAttack, transform.position + Vector3.up, RecordedVolume, 0.05f);
            };
            combat.Shot += Tracer.Show;
        }

        if (enemy != null && enemy.Human)
            enemy.Fired += Tracer.Show;
        else if (enemy != null)
        {
            enemy.Alerted += () => Sfx.Play(SoundId.ZombieScream, Head(), 0.6f, 0.1f);
            enemy.Attacked += () => Sfx.Play(SoundId.ZombieAttack, Head(), RecordedVolume, 0.05f);
        }

        if (health != null)
        {
            health.Damaged += _ =>
            {
                if (enemy == null && !health.IsDead)
                    Sfx.Play(SoundId.PlayerHurt, Head(), 0.35f);
            };
            health.Died += () =>
            {
                if (breath != null)
                    breath.Stop();
                if (enemy != null && !enemy.Human)
                    Sfx.Play(SoundId.ZombieDeath, Head(), RecordedVolume, 0.05f);
                else
                    Sfx.Play(SoundId.PlayerHurt, Head(), 0.42f, 0f);
            };
        }
    }

    void Start()
    {
        if (enemy != null && !enemy.Human)
            StartBreathing();
    }

    // Дыхание звучит постоянно, но только вблизи: громкость падает с расстоянием
    // до полной тишины на breathMaxDistance. Подкрадываясь, игрок слышит «Серого» раньше,
    // чем видит его в темноте.
    void StartBreathing()
    {
        var clip = Sfx.Clip(SoundId.ZombieBreath);
        if (clip == null)
            return;

        var go = new GameObject("Дыхание");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 1.6f;
        breath = go.AddComponent<AudioSource>();
        breath.clip = clip;
        breath.loop = true;
        Sfx.Make3D(breath, breathFullDistance, breathMaxDistance);
        breath.volume = RecordedVolume * Sfx.Volume;
        breath.pitch = Random.Range(0.92f, 1.08f);
        // С разных мест записи, чтобы несколько «Серых» рядом не дышали хором.
        breath.time = Random.Range(0f, clip.length * 0.9f);
        breath.Play();
    }

    Vector3 Head() => transform.position + Vector3.up * 1.6f;
}
