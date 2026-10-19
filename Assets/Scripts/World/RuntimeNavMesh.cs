using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Строит навигационную сетку для врагов при загрузке сцены.
// Пакет AI Navigation (NavMeshSurface) в проекте не стоит, а встроенный модуль AI
// умеет строить сетку из коллайдеров кодом. Для уровня 60×60 м это доли секунды.
[DefaultExecutionOrder(-100)]
public class RuntimeNavMesh : MonoBehaviour
{
    [Tooltip("Размер области, где враги могут ходить, м")]
    [SerializeField] Vector3 size = new Vector3(80f, 20f, 80f);

    NavMeshDataInstance instance;

    void Awake()
    {
        var bounds = new Bounds(transform.position, size);
        var markups = new List<NavMeshBuildMarkup>();

        // Персонажи и триггеры не должны становиться препятствиями в сетке.
        foreach (var c in FindObjectsByType<CharacterController>(FindObjectsInactive.Include))
            markups.Add(Ignore(c.transform));
        foreach (var a in FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include))
            markups.Add(Ignore(a.transform));
        foreach (var c in FindObjectsByType<Collider>(FindObjectsInactive.Include))
            if (c.isTrigger)
                markups.Add(Ignore(c.transform));

        var sources = new List<NavMeshBuildSource>();
        NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);

        var settings = NavMesh.GetSettingsByID(0);
        var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
        instance = NavMesh.AddNavMeshData(data);
    }

    void OnDestroy()
    {
        instance.Remove();
    }

    static NavMeshBuildMarkup Ignore(Transform t) => new NavMeshBuildMarkup { root = t, ignoreFromBuild = true };

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, size);
    }
}
