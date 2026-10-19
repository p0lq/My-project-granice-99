using System.Collections.Generic;
using UnityEngine;

// Объекты, которые ставит крафт. Строятся из примитивов в реальном размере и запоминаются
// для сохранения (SaveSystem пишет их тип и место).
public static class Placeables
{
    public class Placed
    {
        public string kind;
        public Vector3 position;
        public GameObject obj;
    }

    public static readonly List<Placed> All = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => All.Clear();

    public static GameObject Place(string kind, Vector3 position)
    {
        position.y = 0f;
        var root = new GameObject(kind);
        root.transform.position = position;
        switch (kind)
        {
            case "campfire":
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(1.1f, 0.05f, 1.1f), new Color(0.35f, 0.33f, 0.3f), true);
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.12f, 0f), new Vector3(0.8f, 0.1f, 0.1f), new Color(0.3f, 0.2f, 0.12f), false).transform.localRotation = Quaternion.Euler(0f, 30f, 0f);
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.14f, 0f), new Vector3(0.8f, 0.1f, 0.1f), new Color(0.3f, 0.2f, 0.12f), false).transform.localRotation = Quaternion.Euler(0f, -40f, 0f);
                Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.22f, 0f), new Vector3(0.4f, 0.25f, 0.4f), new Color(0.95f, 0.5f, 0.15f), false);
                Flame(root);
                // Костёр из хвороста горит 6 игровых часов.
                root.AddComponent<Campfire>().SetBurnTime(6 * 60f);
                break;
            case "burzhuika":
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.45f, 0.6f), new Color(0.2f, 0.2f, 0.22f), true);
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 1.4f, 0f), new Vector3(0.12f, 0.5f, 0.12f), new Color(0.25f, 0.25f, 0.27f), false);
                Flame(root);
                // Печка горит всегда: в ней и греются, и готовят.
                root.AddComponent<Campfire>();
                break;
            case "workbench":
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 0.7f), new Color(0.45f, 0.33f, 0.2f), true);
                root.AddComponent<Workbench>();
                break;
            case "rain_collector":
                Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.4f, 0.8f), new Color(0.3f, 0.4f, 0.5f), true);
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.85f, 0f), new Vector3(1.4f, 0.05f, 1.4f), new Color(0.3f, 0.45f, 0.35f), false);
                root.AddComponent<RainCollector>();
                break;
            case "can_alarm":
                for (int i = 0; i < 3; i++)
                    Part(root, PrimitiveType.Cylinder, new Vector3(-0.3f + i * 0.3f, 0.6f, 0f), new Vector3(0.1f, 0.06f, 0.1f), new Color(0.6f, 0.6f, 0.62f), false);
                Part(root, PrimitiveType.Cube, new Vector3(0f, 0.7f, 0f), new Vector3(1.2f, 0.01f, 0.01f), new Color(0.7f, 0.65f, 0.5f), false);
                root.AddComponent<CanAlarm>();
                break;
        }
        All.Add(new Placed { kind = kind, position = position, obj = root });
        return root;
    }

    static void Flame(GameObject root)
    {
        var light = new GameObject("Огонь").AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.6f, 0.3f);
        light.intensity = 2.5f;
        light.range = 7f;
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = Vector3.up * 0.6f;
    }

    static GameObject Part(GameObject root, PrimitiveType type, Vector3 pos, Vector3 size, Color color, bool collider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(root.transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().material.color = color;
        var c = go.GetComponent<Collider>();
        if (!collider)
            Object.Destroy(c);
        else if (type == PrimitiveType.Cylinder)
        {
            // У плоского цилиндра капсульный коллайдер раздувается в шар.
            Object.Destroy(c);
            go.AddComponent<BoxCollider>();
        }
        return go;
    }
}
