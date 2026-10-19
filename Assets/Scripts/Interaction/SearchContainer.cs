using System;
using System.Collections.Generic;
using UnityEngine;

// Ящик, шкаф или тумбочка, которые можно обыскать один раз.
// Обыск шумный: «Серые» поблизости его слышат так же, как удар.
public class SearchContainer : Interactable, ISaveable
{
    [Serializable]
    public struct Loot
    {
        [Tooltip("id предмета из items.json")]
        public string item;
        public int amount;
    }

    [SerializeField] string containerName = "ящик";
    [SerializeField] List<Loot> contents = new();
    [Tooltip("Таблица добычи из loot.json (home, kitchen, medical, crate…). Бросается при обыске")]
    [SerializeField] string lootTable = "";
    [Tooltip("Крышка, которая откидывается после обыска (необязательно). Ось вращения на заднем ребре")]
    [SerializeField] Transform lid;
    [Tooltip("Что ещё случается после обыска, в формате последствий Story.json (quest:well=2, objective:…)")]
    [SerializeField] string[] storyEffects = new string[0];

    bool searched;
    string saveKey;

    public string SaveKey => saveKey ??= SaveKeys.Of(this, transform.position);
    public string SaveState() => searched ? "1" : "0";
    public void LoadState(string state)
    {
        if (state != "1" || searched)
            return;
        searched = true;
        if (lid != null)
            lid.localRotation *= Quaternion.Euler(-110f, 0f, 0f);
    }

    // Тело врага, которое можно обыскать: создаётся при его смерти (EnemyAI).
    public void Setup(string name, string table, string key)
    {
        containerName = name;
        lootTable = table;
        saveKey = key;
    }

    public override string Prompt => $"Обыскать {containerName}";
    public override bool CanInteract => !searched;

    public override void Interact(PlayerInteractor player)
    {
        searched = true;
        Sfx.Play(SoundId.Crate, transform.position + Vector3.up * 0.5f, 0.35f);
        if (lid != null)
            lid.localRotation *= Quaternion.Euler(-110f, 0f, 0f);

        var combat = player.GetComponent<PlayerCombat>();
        if (combat != null)
            combat.MakeNoise();

        Story.Apply(storyEffects);

        var found = new List<(string id, int count)>();
        foreach (var loot in contents)
            found.Add((loot.item, loot.amount));
        if (!string.IsNullOrEmpty(lootTable))
            found.AddRange(GameData.RollLoot(lootTable, new System.Random()));

        if (found.Count == 0)
        {
            GameMessages.Show($"Пусто: {containerName}");
            return;
        }
        foreach (var (id, count) in found)
        {
            GameSession.Add(id, count);
            GameMessages.Show(count > 1 ? $"Нашёл: {Items.Name(id)} ×{count}" : $"Нашёл: {Items.Name(id)}");
        }
    }
}
