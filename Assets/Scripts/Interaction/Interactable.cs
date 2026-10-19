using UnityEngine;

// Основа для всего, с чем игрок взаимодействует по E: двери, колодец, ящики, рубильник, люк.
// PlayerInteractor находит ближайший такой объект, HUD показывает подсказку Prompt.
public abstract class Interactable : MonoBehaviour
{
    // Текст подсказки без клавиши, например «Открыть дверь».
    public abstract string Prompt { get; }

    // false — подсказка не показывается (ящик уже обыскан, колодец пуст).
    public virtual bool CanInteract => true;

    public abstract void Interact(PlayerInteractor player);
}
