using UnityEngine;

// Выход с уровня: люк в бункер, лестница наверх. Может требовать предмет.
// Если сцена следующего уровня не указана, выход означает победу.
public class LevelExit : Interactable
{
    [SerializeField] string prompt = "Спуститься в люк";
    [SerializeField] bool needsItem;
    [SerializeField] ItemId requiredItem = ItemId.BunkerKey;
    [SerializeField] string missingMessage = "Люк заперт. Нужен ключ";
    [Tooltip("Имя сцены из SceneNames. Пусто — победа")]
    [SerializeField] string nextScene = SceneNames.Bunker;

    public override string Prompt => prompt;

    public override void Interact(PlayerInteractor player)
    {
        if (needsItem && !GameSession.Has(requiredItem))
        {
            GameMessages.Show(missingMessage);
            return;
        }

        if (GameManager.Instance == null)
            return;
        if (string.IsNullOrEmpty(nextScene))
            GameManager.Instance.Win();
        else
            GameManager.Instance.LoadLevel(nextScene);
    }
}
