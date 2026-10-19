using UnityEngine;
using UnityEngine.InputSystem;

// Выпить воду по Q: тратит бутылку из GameSession и лечит.
[RequireComponent(typeof(Health))]
public class PlayerSupplies : MonoBehaviour
{
    InputAction drinkAction;
    Health health;

    void Awake()
    {
        drinkAction = InputSystem.actions.FindAction("Player/Drink", true);
        health = GetComponent<Health>();
    }

    void Update()
    {
        if (!drinkAction.WasPressedThisFrame() || health.IsDead)
            return;

        if (health.Current >= health.Max)
            GameMessages.Show("Пить пока не хочется");
        else if (GameSession.Remove(ItemId.Water))
        {
            health.Heal(Items.WaterHeal);
            GameMessages.Show("Выпил воды");
        }
        else
            GameMessages.Show("Воды нет. Набери в колодце");
    }
}
