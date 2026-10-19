using System.Collections.Generic;
using UnityEngine;

// Область, которая периодически наносит урон всем, у кого есть Health.
// Пока нужна для проверки здоровья, потом станет облаком «Пыльного фронта».
[RequireComponent(typeof(Collider))]
public class DamageZone : MonoBehaviour
{
    [SerializeField] float damage = 10f;
    [SerializeField] float interval = 1f;

    // Список ведём сами через Enter/Exit: OnTriggerStay с CharacterController
    // вызывается не каждый кадр, и урон шёл бы неравномерно.
    readonly Dictionary<Health, float> nextHitTime = new();
    readonly List<Health> inside = new();
    readonly List<Health> dead = new();

    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponentInParent<Health>();
        if (health != null && !nextHitTime.ContainsKey(health))
            nextHitTime.Add(health, Time.time);
    }

    void OnTriggerExit(Collider other)
    {
        var health = other.GetComponentInParent<Health>();
        if (health != null)
            nextHitTime.Remove(health);
    }

    void Update()
    {
        // словарь нельзя менять во время обхода, поэтому обходим копию ключей
        inside.Clear();
        inside.AddRange(nextHitTime.Keys);
        dead.Clear();
        foreach (var health in inside)
        {
            if (health == null || health.IsDead)
            {
                dead.Add(health);
                continue;
            }
            if (Time.time < nextHitTime[health])
                continue;

            health.TakeDamage(damage);
            nextHitTime[health] = Time.time + interval;
        }
        foreach (var health in dead)
            nextHitTime.Remove(health);
    }
}
