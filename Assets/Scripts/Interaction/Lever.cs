using UnityEngine;

// Рубильник: один раз включает питание. Отпирает гермодверь и зажигает лампы.
public class Lever : Interactable
{
    [SerializeField] Door door;
    [Tooltip("Лампы, которые загораются вместе с питанием")]
    [SerializeField] Light[] lamps;
    [Tooltip("Ручка рубильника, которая опускается (необязательно)")]
    [SerializeField] Transform handle;

    bool used;

    public override string Prompt => "Опустить рубильник";
    public override bool CanInteract => !used;

    void Start()
    {
        foreach (var lamp in lamps)
            if (lamp != null)
                lamp.enabled = false;
    }

    public override void Interact(PlayerInteractor player)
    {
        used = true;
        if (handle != null)
            handle.localRotation *= Quaternion.Euler(90f, 0f, 0f);
        foreach (var lamp in lamps)
            if (lamp != null)
                lamp.enabled = true;
        if (door != null)
            door.Unlock(true);
        GameMessages.Show("Загудел генератор. Гермодверь открылась");
    }
}
