using UnityEngine;

// След пули и вспышка у ствола: на долю секунды, чтобы было видно, откуда стреляют.
public class Tracer : MonoBehaviour
{
    static Material lineMaterial;

    float dieAt;
    LineRenderer line;
    Light flash;

    public static void Show(Vector3 from, Vector3 to)
    {
        var go = new GameObject("Выстрел");
        var t = go.AddComponent<Tracer>();
        t.line = go.AddComponent<LineRenderer>();
        if (lineMaterial == null)
            lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(1f, 0.9f, 0.6f) };
        t.line.sharedMaterial = lineMaterial;
        t.line.positionCount = 2;
        t.line.SetPosition(0, from);
        t.line.SetPosition(1, to);
        t.line.startWidth = 0.04f;
        t.line.endWidth = 0.01f;
        t.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        t.flash = new GameObject("Вспышка").AddComponent<Light>();
        t.flash.transform.SetParent(go.transform, false);
        t.flash.transform.position = from;
        t.flash.type = LightType.Point;
        t.flash.color = new Color(1f, 0.75f, 0.4f);
        t.flash.intensity = 4f;
        t.flash.range = 5f;
        t.dieAt = Time.time + 0.07f;
    }

    void Update()
    {
        if (Time.time >= dieAt)
            Destroy(gameObject);
    }
}
