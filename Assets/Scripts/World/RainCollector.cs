using UnityEngine;

// Сборщик дождевой воды: раз в 6 игровых часов в нём набирается бутылка воды.
public class RainCollector : Interactable
{
    float lastTaken;

    public override string Prompt => Ready ? "Набрать дождевой воды" : "Сборщик воды (пусто)";
    public override bool CanInteract => true;
    bool Ready => WorldClock.Minutes - lastTaken >= 6 * 60f;

    void Start() => lastTaken = WorldClock.Minutes;

    public override void Interact(PlayerInteractor player)
    {
        if (!Ready)
        {
            GameMessages.Show("Воды пока нет, дождевая набирается медленно");
            return;
        }
        lastTaken = WorldClock.Minutes;
        GameSession.Add("water_clean");
        GameMessages.Show("Набрал дождевой воды");
    }
}
