using System;
using System.Collections.Generic;
using UnityEngine;

// Состояние игрока, которое переживает смену сцен: здоровье, заряд фонарика, предметы.
// Обычные объекты сцены уничтожаются при загрузке следующей, а статический класс нет.
//
// При входе на уровень GameManager делает снимок (Checkpoint). «Заново» в меню
// проигрыша возвращает этот снимок, чтобы игрок начинал уровень с тем, с чем пришёл.
public static class GameSession
{
    public const float StartHealth = 100f;
    public const float StartBattery = 100f;

    static readonly Dictionary<ItemId, int> items = new();
    static Dictionary<ItemId, int> checkpointItems = new();
    static float checkpointHealth = StartHealth;
    static float checkpointBattery = StartBattery;

    public static float Health { get; set; } = StartHealth;
    public static float Battery { get; set; } = StartBattery;

    public static event Action<ItemId, int> ItemsChanged;

    // В проекте выключена перезагрузка домена при входе в Play (Project Settings → Editor →
    // Enter Play Mode Settings), поэтому статические поля сами между запусками не сбрасываются:
    // без этого предметы из прошлого запуска оставались бы у игрока.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        ItemsChanged = null;
        NewGame();
    }

    public static void NewGame()
    {
        items.Clear();
        Health = StartHealth;
        Battery = StartBattery;
        Checkpoint();
    }

    public static void Checkpoint()
    {
        checkpointItems = new Dictionary<ItemId, int>(items);
        checkpointHealth = Health;
        checkpointBattery = Battery;
    }

    public static void RestoreCheckpoint()
    {
        items.Clear();
        foreach (var pair in checkpointItems)
            items[pair.Key] = pair.Value;
        Health = checkpointHealth;
        Battery = checkpointBattery;
    }

    public static int Count(ItemId id) => items.TryGetValue(id, out int n) ? n : 0;

    public static bool Has(ItemId id) => Count(id) > 0;

    public static void Add(ItemId id, int amount = 1)
    {
        items[id] = Count(id) + amount;
        ItemsChanged?.Invoke(id, items[id]);
    }

    public static bool Remove(ItemId id, int amount = 1)
    {
        if (Count(id) < amount)
            return false;
        items[id] = Count(id) - amount;
        ItemsChanged?.Invoke(id, items[id]);
        return true;
    }
}
