using System.Collections.Generic;
using UnityEngine;

// Верстак: станция для рецептов со station = workbench (буржуйка, сборщик воды).
// Один стоит в мастерской Кима, ещё один можно собрать самому.
public class Workbench : MonoBehaviour
{
    static readonly List<Workbench> all = new();

    public static bool Near(Vector3 point, float radius)
    {
        foreach (var w in all)
            if (w != null && Vector3.Distance(w.transform.position, point) < radius)
                return true;
        return false;
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);
}
