using System;
using System.Collections.Generic;
using UnityEngine;

// Ящик, шкаф или тумбочка, которые можно обыскать один раз.
// Обыск шумный: «Серые» поблизости его слышат так же, как удар.
public class SearchContainer : Interactable
{
    [Serializable]
    public struct Loot
    {
        public ItemId item;
        public int amount;
    }

    [SerializeField] string containerName = "ящик";
    [SerializeField] List<Loot> contents = new();
    [Tooltip("Крышка, которая откидывается после обыска (необязательно). Ось вращения на заднем ребре")]
    [SerializeField] Transform lid;

    bool searched;

    public override string Prompt => $"Обыскать {containerName}";
    public override bool CanInteract => !searched;

    public override void Interact(PlayerInteractor player)
    {
        searched = true;
        Sfx.Play(SoundId.Crate, transform.position + Vector3.up * 0.5f, 0.9f);
        if (lid != null)
            lid.localRotation *= Quaternion.Euler(-110f, 0f, 0f);

        var combat = player.GetComponent<PlayerCombat>();
        if (combat != null)
            combat.MakeNoise();

        if (contents.Count == 0)
        {
            GameMessages.Show($"Пусто: {containerName}");
            return;
        }
        foreach (var loot in contents)
        {
            GameSession.Add(loot.item, loot.amount);
            GameMessages.Show(loot.amount > 1 ? $"Нашёл: {Items.Name(loot.item)} ×{loot.amount}" : $"Нашёл: {Items.Name(loot.item)}");
        }
    }
}
