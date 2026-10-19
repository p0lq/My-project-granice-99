using UnityEngine;

// Сигнализация из банок: звенит, когда рядом проходит «Серый», и предупреждает игрока.
public class CanAlarm : MonoBehaviour
{
    float nextRing;

    void Update()
    {
        if (Time.time < nextRing)
            return;
        foreach (var e in EnemyAI.All)
            if (e != null && e.Current != EnemyAI.State.Dead && Vector3.Distance(e.transform.position, transform.position) < 2.5f)
            {
                nextRing = Time.time + 6f;
                Sfx.Play(SoundId.Locked, transform.position, 0.7f);
                GameMessages.Show("Звенят банки: рядом кто-то ходит!");
                return;
            }
    }
}
