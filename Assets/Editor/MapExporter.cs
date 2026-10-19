using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// «Условная карта» для ТЗ: снимок открытой сцены строго сверху в Docs/Map_<сцена>.png.
// Снимается настоящая сцена, поэтому карта всегда совпадает с уровнем.
public static class MapExporter
{
    const int Resolution = 2048;

    [MenuItem("Граница-99/Сохранить карту открытой сцены", priority = 60)]
    static void Export()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var bounds = SceneBounds();
        if (bounds.size == Vector3.zero)
        {
            Debug.LogError("В сцене нет видимых объектов для карты");
            return;
        }

        var go = new GameObject("Камера карты") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) * 1.05f;
        cam.transform.position = bounds.center + Vector3.up * 100f;
        cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
        cam.farClipPlane = 300f;

        // В бункере почти чёрный рассеянный свет; для карты временно светлее.
        var ambientMode = RenderSettings.ambientMode;
        var ambient = RenderSettings.ambientLight;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.8f, 0.8f, 0.8f);

        var rt = new RenderTexture(Resolution, Resolution, 24);
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture.active = rt;
        var image = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
        image.Apply();
        RenderTexture.active = null;

        RenderSettings.ambientMode = ambientMode;
        RenderSettings.ambientLight = ambient;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);

        // Docs лежит рядом с Assets, а не внутри, чтобы Unity не импортировал картинки.
        Directory.CreateDirectory("Docs");
        string path = $"Docs/Map_{scene.name}.png";
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image);
        Debug.Log($"Карта сохранена: {Path.GetFullPath(path)}");
        EditorUtility.RevealInFinder(path);
    }

    static Bounds SceneBounds()
    {
        var bounds = new Bounds();
        bool first = true;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
        {
            // Земля в бункере больше самих помещений, по ней рамку не считаем.
            if (r.name == "Земля")
                continue;
            if (first)
            {
                bounds = r.bounds;
                first = false;
            }
            else
                bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }
}
