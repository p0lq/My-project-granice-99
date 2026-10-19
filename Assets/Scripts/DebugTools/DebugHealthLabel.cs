using UnityEngine;

// Временная надпись со здоровьем в углу экрана, пока нет настоящего HUD.
// Удалить, когда появится интерфейс.
public class DebugHealthLabel : MonoBehaviour
{
    [SerializeField] Health health;

    GUIStyle style;

    void OnGUI()
    {
        if (health == null)
            return;

        style ??= new GUIStyle(GUI.skin.label) { fontSize = 24 };
        string text = health.IsDead ? "Погиб" : $"Здоровье: {health.Current:0} / {health.Max:0}";
        GUI.Label(new Rect(20, 20, 400, 40), text, style);
    }
}
