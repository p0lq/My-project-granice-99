using UnityEngine;

// Голос персонажа. Сам определяет, кто он: на игроке это замах и стон от урона,
// на «Сером» рычание, крик при обнаружении, атака и предсмертный хрип.
public class CharacterSounds : MonoBehaviour
{
    [Tooltip("Как часто «Серый» рычит, когда бродит, с (от и до)")]
    [SerializeField] Vector2 groanInterval = new Vector2(5f, 12f);

    Health health;
    EnemyAI enemy;
    float nextGroan;

    void Awake()
    {
        health = GetComponent<Health>();
        enemy = GetComponent<EnemyAI>();
        var combat = GetComponent<PlayerCombat>();

        if (combat != null)
            combat.Attacked += () => Sfx.Play(SoundId.Swing, transform.position, 0.6f);

        if (enemy != null)
        {
            enemy.Alerted += () => Sfx.Play(SoundId.ZombieScream, Head(), 1f, 0.12f);
            enemy.Attacked += () => Sfx.Play(SoundId.ZombieAttack, Head(), 0.9f, 0.12f);
            nextGroan = Time.time + Random.Range(1f, groanInterval.y);
        }

        if (health != null)
        {
            health.Damaged += _ =>
            {
                if (enemy == null && !health.IsDead)
                    Sfx.Play(SoundId.PlayerHurt, Head(), 0.8f);
            };
            health.Died += () =>
            {
                if (enemy != null)
                    Sfx.Play(SoundId.ZombieDeath, Head(), 1f);
                else
                    Sfx.Play(SoundId.PlayerHurt, Head(), 1f, 0f);
            };
        }
    }

    void Update()
    {
        if (enemy == null || health == null || health.IsDead || Time.time < nextGroan)
            return;
        // В погоне рычит чаще.
        float k = enemy.Current == EnemyAI.State.Wander ? 1f : 0.4f;
        nextGroan = Time.time + Random.Range(groanInterval.x, groanInterval.y) * k;
        Sfx.Play(SoundId.ZombieGroan, Head(), 0.7f, 0.15f);
    }

    Vector3 Head() => transform.position + Vector3.up * 1.6f;
}
