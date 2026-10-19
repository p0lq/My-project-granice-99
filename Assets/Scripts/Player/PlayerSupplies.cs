using UnityEngine;
using UnityEngine.InputSystem;

// Быстрое питьё по Q: выпить чистую воду (или любой другой напиток, кроме грязной воды),
// не открывая инвентарь. Жажда и здоровье считаются в PlayerNeeds.
[RequireComponent(typeof(PlayerNeeds))]
public class PlayerSupplies : MonoBehaviour
{
    InputAction drinkAction;
    PlayerNeeds needs;
    Health health;

    void Awake()
    {
        drinkAction = InputSystem.actions.FindAction("Player/Drink", true);
        needs = GetComponent<PlayerNeeds>();
        health = GetComponent<Health>();
    }

    void Update()
    {
        if (!drinkAction.WasPressedThisFrame() || health.IsDead)
            return;

        var drink = GameSession.FindStack("water_clean")
                    ?? GameSession.Data.items.Find(s => s.Def != null && s.Def.type == "drink" && s.id != "water_dirty");
        if (drink == null)
            GameMessages.Show("Чистой воды нет. Набери в колодце или вскипяти грязную");
        else if (GameSession.Data.thirst < 5f)
            GameMessages.Show("Пить пока не хочется");
        else
            needs.Consume(drink);
    }
}
