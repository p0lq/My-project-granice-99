using System.Collections.Generic;
using UnityEngine;

// Костёр: у огня тепло (температура тела), поднимается настроение, на нём готовят (крафт
// со станцией fire). Свет пламени слегка мерцает. Сложенный игроком костёр со временем гаснет.
public class Campfire : MonoBehaviour
{
    static readonly List<Campfire> all = new();

    [Tooltip("Сколько игровых минут горит. 0 — горит всегда (костёр в «Заре»)")]
    [SerializeField] float burnMinutes;

    Light flame;
    float baseIntensity;
    float burnedAt = -1f;

    public static bool Near(Vector3 point, float radius)
    {
        foreach (var f in all)
            if (f != null && Vector3.Distance(f.transform.position, point) < radius)
                return true;
        return false;
    }

    public void SetBurnTime(float minutes) => burnMinutes = minutes;

    void OnEnable() => all.Add(this);

    void OnDisable() => all.Remove(this);

    void Start()
    {
        flame = GetComponentInChildren<Light>();
        baseIntensity = flame != null ? flame.intensity : 0f;
        if (burnMinutes > 0f)
            burnedAt = WorldClock.Minutes + burnMinutes;
    }

    void Update()
    {
        if (flame != null)
            flame.intensity = baseIntensity * (0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 3f, transform.position.x));
        if (burnedAt > 0f && WorldClock.Minutes > burnedAt)
        {
            GameMessages.Show("Костёр догорел");
            Destroy(gameObject);
        }
    }
}
