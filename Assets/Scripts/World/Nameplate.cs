using UnityEngine;

// Подпись над жителем («Ерлан-ага»), всегда повёрнутая к камере.
// Видна только вблизи, чтобы над посёлком не висел лес надписей.
[RequireComponent(typeof(TextMesh))]
public class Nameplate : MonoBehaviour
{
    [SerializeField] float visibleDistance = 14f;

    TextMesh text;
    MeshRenderer meshRenderer;
    Transform player;

    void Awake()
    {
        text = GetComponent<TextMesh>();
        meshRenderer = GetComponent<MeshRenderer>();
        // У TextMesh шрифт и материал задаются вместе, иначе текст рисуется квадратами.
        text.font = UIFactory.Font;
        meshRenderer.sharedMaterial = UIFactory.Font.material;
    }

    void Start()
    {
        var p = GameObject.FindWithTag("Player");
        player = p != null ? p.transform : null;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null)
            return;
        transform.rotation = cam.transform.rotation;
        if (player != null)
            meshRenderer.enabled = Vector3.Distance(player.position, transform.position) < visibleDistance;
    }
}
