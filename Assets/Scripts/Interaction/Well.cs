using UnityEngine;

// Колодец: по E даёт бутылку воды. Вода в нём кончается, чтобы её нельзя было
// набрать бесконечно и выпить посреди любой драки.
public class Well : Interactable
{
    [SerializeField] int charges = 3;
    [Tooltip("Ворот, который прокручивается, когда набираешь воду (необязательно)")]
    [SerializeField] Transform crank;

    float crankSpin;

    public override string Prompt => $"Набрать воды (осталось {charges})";
    public override bool CanInteract => charges > 0;

    public override void Interact(PlayerInteractor player)
    {
        charges--;
        GameSession.Add(ItemId.Water);
        GameMessages.Show(charges > 0 ? "Набрал бутылку воды" : "Набрал последнюю воду, колодец пуст");
        crankSpin = 720f;
        Sfx.Play(SoundId.Well, transform.position + Vector3.up, 0.9f);
    }

    void Update()
    {
        if (crank == null || crankSpin <= 0f)
            return;
        float step = Mathf.Min(crankSpin, 720f * Time.deltaTime);
        crank.Rotate(step, 0f, 0f, Space.Self);
        crankSpin -= step;
    }
}
