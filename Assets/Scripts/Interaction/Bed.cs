using UnityEngine;

// Кровать: по E герой ложится спать, время идёт в 60 раз быстрее, усталость уходит.
// Просыпается сам, когда выспался, или от удара и приближения «Серых».
public class Bed : Interactable
{
    public override string Prompt => "Лечь спать";

    public override void Interact(PlayerInteractor player)
    {
        var needs = player.GetComponent<PlayerNeeds>();
        if (needs == null)
            return;
        if (needs.Sleeping)
        {
            needs.WakeUp("Встал с кровати");
            return;
        }
        if (!needs.CanSleep(out string reason))
        {
            GameMessages.Show(reason);
            return;
        }
        // Ложимся на кровать, чтобы спящего было видно на месте.
        player.transform.position = new Vector3(transform.position.x, player.transform.position.y, transform.position.z);
        needs.Sleep();
    }
}
