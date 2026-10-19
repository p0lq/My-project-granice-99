using System.Collections.Generic;
using UnityEngine;

// Потребности, раны и болезни героя — перенос player.py из старой игры.
// Шкалы: голод, жажда, усталость от 0 (всё хорошо) до 100 (смертельно);
// настроение наоборот, 100 — отлично; температура тела в °C, норма 36,6;
// «пыльная болезнь» и отравление 0..100. Все скорости «за игровую минуту», как в оригинале.
[RequireComponent(typeof(Health))]
public class PlayerNeeds : MonoBehaviour
{
    public static readonly string[] BodyParts = { "голова", "торс", "левая рука", "правая рука", "левая нога", "правая нога" };

    // bleed — кровотечение до перевязки, heal — минут до заживления, pain — боль.
    static readonly Dictionary<string, (bool bleed, float heal, float pain)> WoundInfo = new()
    {
        { "Порез", (true, 24 * 60, 15) },
        { "Укус", (true, 36 * 60, 20) },
        { "Ушиб", (false, 12 * 60, 10) },
        { "Перелом", (false, 7 * 24 * 60, 45) },
    };

    [Tooltip("Сколько «пыльной болезни» в минуту набирается в облаке пыли без респиратора")]
    [SerializeField] float dustRate = 1.5f;

    Health health;
    PlayerMovement movement;
    float dustUntil;

    SessionData S => GameSession.Data;

    public bool Sleeping { get; private set; }
    public bool InDust => Time.time < dustUntil;

    void Awake()
    {
        health = GetComponent<Health>();
        movement = GetComponent<PlayerMovement>();
        health.Damaged += OnHurt;
    }

    void Update()
    {
        var clock = WorldClock.Instance;
        if (clock == null || health.IsDead)
            return;
        float mins = clock.DeltaMinutes;
        if (mins <= 0f)
            return;

        UpdateNeeds(mins, clock.Indoor);
        if (S.disease >= 100f)
            health.Drain(health.Current);

        if (Sleeping && (S.fatigue <= 2f || EnemiesClose()))
            WakeUp(S.fatigue <= 2f ? "Выспался" : "Проснулся: рядом «Серые»!");
    }

    // ---------------------------------------------------------------- потребности

    void UpdateNeeds(float mins, bool indoor)
    {
        bool running = movement != null && movement.IsRunning;
        float kSleep = Sleeping ? 0.5f : 1f;
        float air = indoor ? 14f : WorldClock.AirTemp;
        bool hot = !indoor && air > 30f && WorldClock.Darkness < 0.3f;
        bool nearFire = Campfire.Near(transform.position, 3.5f);
        bool sheltered = indoor || RoofHider.PlayerInside;

        S.hunger = Mathf.Min(100f, S.hunger + 0.065f * (GameSession.HasTrait("glutton") ? 1.3f : 1f) * kSleep * mins);

        float thirstRate = 0.09f;
        if (hot && !sheltered) thirstRate *= 1.6f;
        if (running) thirstRate *= 1.5f;
        if (S.poison > 20f) thirstRate *= 1.3f;
        S.thirst = Mathf.Min(100f, S.thirst + thirstRate * kSleep * mins);

        if (Sleeping)
            S.fatigue = Mathf.Max(0f, S.fatigue - 0.4f * mins);
        else
        {
            float rate = 0.055f;
            if (running) rate *= GameSession.HasTrait("asthma") ? 2.2f : 1.8f;
            S.fatigue = Mathf.Min(100f, S.fatigue + rate * mins);
        }

        UpdateTemp(mins, air, hot, nearFire, sheltered);
        UpdateDisease(mins);
        UpdateWounds(mins);

        S.poison = Mathf.Max(0f, S.poison - 0.05f * mins);
        S.painkiller = Mathf.Max(0f, S.painkiller - mins);

        S.sinceSmoke += mins;
        float moodDelta = 0f;
        if (GameSession.HasTrait("smoker") && S.sinceSmoke > 12 * 60) moodDelta -= 0.03f;
        if (S.hunger > 60f) moodDelta -= 0.02f;
        if (S.thirst > 60f) moodDelta -= 0.02f;
        if (S.pain > 30f && S.painkiller <= 0f) moodDelta -= 0.02f;
        if (nearFire) moodDelta += 0.03f;
        if (moodDelta == 0f && S.mood < 60f) moodDelta = 0.01f;
        S.mood = Mathf.Clamp(S.mood + moodDelta * mins, 0f, 100f);

        // Урон от состояния и медленное восстановление, когда всё в порядке.
        float dmg = 0f;
        if (S.hunger >= 90f) dmg += 0.03f;
        if (S.thirst >= 90f) dmg += 0.06f;
        foreach (var w in S.wounds)
            if (w.bleeding) dmg += 0.035f;
        if (S.poison > 50f) dmg += 0.02f;
        if (S.disease >= 70f) dmg += 0.02f;
        if (S.temp < 35f || S.temp > 39f) dmg += 0.02f;
        foreach (var w in S.wounds)
            if (w.inflamed) { dmg += 0.01f; break; }
        // Шкала здоровья в Unity та же, 0..100, поэтому коэффициенты переносятся один к одному.
        if (dmg > 0f)
            health.Drain(dmg * mins);
        else if (S.hunger < 70f && S.thirst < 70f && S.poison < 30f)
            health.Heal((Sleeping ? 0.06f : 0.02f) * mins);

        float targetPain = 0f;
        foreach (var w in S.wounds)
            targetPain += WoundInfo[w.type].pain * (w.bandaged || w.splinted ? 0.5f : 1f);
        S.pain += (Mathf.Min(100f, targetPain) - S.pain) * Mathf.Min(1f, 0.1f * mins);
    }

    // Температура тела тянется к «ощущаемой»: степь днём жарит, ночью морозит.
    void UpdateTemp(float mins, float air, bool hot, bool nearFire, bool sheltered)
    {
        float felt = air + GameSession.EquipSum("warmth") * 4f;
        if (nearFire) felt += 14f;
        if (sheltered) felt += 4f;
        float target = 36.6f;
        if (felt < 12f)
            target = 36.6f - (12f - felt) * 0.12f;
        else if (felt > 30f && hot && !sheltered)
            target = 36.6f + (felt - 30f) * 0.08f * (1f - 0.6f * Mathf.Min(1f, GameSession.EquipSum("sun")));
        bool inflamed = S.wounds.Exists(w => w.inflamed);
        if (inflamed || (S.disease >= 40f && S.disease < 70f))
            target += 1.2f;
        S.temp += (target - S.temp) * Mathf.Min(1f, 0.01f * mins);
    }

    void UpdateDisease(float mins)
    {
        float exposure = InDust ? dustRate : 0f;
        var face = GameSession.Equipped("face");
        if (exposure > 0f && face != null && face.charge > 0f)
        {
            // Фильтр защищает, но тратится.
            face.charge = Mathf.Max(0f, face.charge - mins * 3f);
            if (face.charge <= 0f)
                GameMessages.Show("Фильтр забился! Нужен новый");
            exposure = 0f;
        }
        if (GameSession.HasTrait("asthma"))
            exposure *= 1.5f;

        float before = S.disease;
        if (exposure > 0f)
            S.disease += exposure * mins;
        else if (S.disease >= 40f)
            S.disease += 0.006f * mins; // развивается сама
        else
            S.disease = Mathf.Max(0f, S.disease - 0.004f * mins);
        S.disease = Mathf.Min(100f, S.disease);

        if (before < 15f && S.disease >= 15f) GameMessages.Show("Ты закашлялся. Горло дерёт пылью");
        if (before < 40f && S.disease >= 40f) GameMessages.Show("Тебя лихорадит. Пыльная болезнь развивается");
        if (before < 70f && S.disease >= 70f) GameMessages.Show("Голова плывёт. Голоса... Нужен антидот!");
    }

    void UpdateWounds(float mins)
    {
        for (int i = S.wounds.Count - 1; i >= 0; i--)
        {
            var w = S.wounds[i];
            // Грязная перевязка без обработки может воспалиться.
            if (w.dirty && !w.disinfected && !w.inflamed && Random.value < 0.0008f * mins)
            {
                w.inflamed = true;
                GameMessages.Show($"Рана ({w.part}) воспалилась. Нужна зелёнка!");
            }
            float speed = 1f;
            if (w.bandaged) speed *= 1.5f;
            if (w.splinted) speed *= 2f;
            if (w.inflamed) speed *= 0.2f;
            speed *= 1f + 0.1f * GameSession.Skill("firstaid");
            w.left -= mins * speed;
            if (w.left <= 0f)
            {
                S.wounds.RemoveAt(i);
                GameMessages.Show($"Рана зажила: {w.type.ToLower()} ({w.part})");
            }
        }
    }

    // Вызывают «Серые» и бандиты. Урон по здоровью уже прошёл в Health.
    void OnHurt(float amount)
    {
        if (Sleeping)
            WakeUp("Проснулся от удара!");
        if (Random.value >= 0.55f)
            return;
        string kind = health.LastDamageKind;
        string type = kind switch
        {
            "infected" => Random.value < 0.6f ? "Укус" : "Порез",
            "bullet" => "Порез",
            _ => Random.value < 0.5f ? "Порез" : "Ушиб",
        };
        if (kind == "hit" && Random.value < 0.08f)
            type = "Перелом";
        string part = BodyParts[Random.Range(0, BodyParts.Length)];
        var info = WoundInfo[type];
        S.wounds.Add(new Wound { type = type, part = part, bleeding = info.bleed, left = info.heal });
        GameMessages.Show($"{type}: {part}!");

        float protection = Mathf.Min(0.6f, GameSession.EquipSum("protect"));
        if (kind == "infected" && Random.value < 0.35f * (1f - protection))
        {
            S.disease = Mathf.Min(100f, S.disease + Random.Range(8f, 16f));
            GameMessages.Show("Рана жжёт... Штамм попал в кровь");
        }
    }

    // ---------------------------------------------------------------- влияние на героя

    public float SpeedMultiplier
    {
        get
        {
            float k = 1f;
            if (S.fatigue > 80f) k *= 0.75f;
            foreach (var w in S.wounds)
                if (w.type == "Перелом" && w.part.Contains("нога"))
                    k *= w.splinted ? 0.85f : 0.6f;
            float over = GameSession.Weight() / GameSession.CarryLimit();
            if (over > 1.5f) k *= 0.4f;
            else if (over > 1f) k *= 0.7f;
            return k;
        }
    }

    public float MeleeMultiplier
    {
        get
        {
            float k = 1f + 0.08f * GameSession.Skill("melee");
            if (S.fatigue > 80f) k *= 0.7f;
            if (GameSession.HasTrait("weak")) k *= 0.8f;
            if (S.pain > 60f && S.painkiller <= 0f) k *= 0.8f;
            return k;
        }
    }

    public float NoiseMultiplier
    {
        get
        {
            float k = GameSession.HasTrait("clumsy") ? 1.3f : 1f;
            if (movement != null && movement.IsCrouching)
                k *= Mathf.Max(0.25f, 0.55f - 0.04f * GameSession.Skill("sneak")) / 0.55f;
            return k;
        }
    }

    // Отметка «стою в пыли». Ставит DamageZone каждый кадр, пока игрок внутри.
    public void MarkDust() => dustUntil = Time.time + 0.2f;

    // ---------------------------------------------------------------- сон

    public bool CanSleep(out string reason)
    {
        reason = null;
        if (S.fatigue < 25f)
            reason = "Спать пока не хочется";
        else if (EnemiesClose())
            reason = "Нельзя спать: рядом «Серые»";
        return reason == null;
    }

    public void Sleep()
    {
        Sleeping = true;
        if (movement != null) movement.CanMove = false;
        if (WorldClock.Instance != null) WorldClock.Instance.Sleeping = true;
        GameMessages.Show("Ты лёг спать...");
    }

    public void WakeUp(string message)
    {
        if (!Sleeping)
            return;
        Sleeping = false;
        if (movement != null && !health.IsDead) movement.CanMove = true;
        if (WorldClock.Instance != null) WorldClock.Instance.Sleeping = false;
        GameMessages.Show($"{message}. {WorldClock.Text}");
    }

    bool EnemiesClose()
    {
        foreach (var e in EnemyAI.All)
            if (e != null && e.Current != EnemyAI.State.Dead && Vector3.Distance(e.transform.position, transform.position) < 12f)
                return true;
        return false;
    }

    // ---------------------------------------------------------------- лечение и еда

    // Применить средство к ране. Возвращает текст результата для сообщения.
    public string Treat(Wound wound, string med)
    {
        int fa = GameSession.Skill("firstaid") + (GameSession.HasTrait("doctor") ? 3 : 0);
        GameSession.AddXp("firstaid", 15f);
        switch (med)
        {
            case "bandage":
            case "bandage_dirty":
                if (wound.bandaged) return "Рана уже перевязана";
                wound.bandaged = true;
                wound.bleeding = false;
                wound.dirty = med == "bandage_dirty";
                wound.left -= 60f * fa;
                return "Перевязал рану." + (wound.dirty ? " Тряпка грязная — лучше обработать" : "");
            case "disinfect":
                wound.disinfected = true;
                if (wound.inflamed)
                {
                    wound.inflamed = false;
                    return "Обработал воспалённую рану. Жжёт, но поможет";
                }
                return "Обработал рану";
            case "splint":
                if (wound.type != "Перелом") return "Шина нужна только при переломе";
                wound.splinted = true;
                return "Наложил шину";
        }
        return "Не помогает";
    }

    // Съесть, выпить или принять лекарство без раны (анальгин, уголь, антидот).
    public bool Consume(ItemStack stack)
    {
        var def = stack.Def;
        if (def == null)
            return false;

        if (def.type == "medical")
        {
            string med = def.S("med");
            switch (med)
            {
                case "painkiller":
                    S.painkiller = 4 * 60f;
                    GameMessages.Show("Принял анальгин. Боль отступает");
                    break;
                case "charcoal":
                    S.poison = Mathf.Max(0f, S.poison - 40f);
                    GameMessages.Show("Выпил активированный уголь");
                    break;
                case "antidote":
                    S.disease = 0f;
                    GameMessages.Show("Ввёл антидот «ПФ-2». Кашель стихает");
                    break;
                default:
                    return false; // бинты и шина — через окно здоровья, к конкретной ране
            }
            GameSession.RemoveStack(stack);
            return true;
        }

        if (def.type != "food" && def.type != "drink")
            return false;
        S.hunger = Mathf.Clamp(S.hunger + def.F("hunger"), 0f, 100f);
        S.thirst = Mathf.Clamp(S.thirst + def.F("thirst"), 0f, 100f);
        S.mood = Mathf.Clamp(S.mood + def.F("mood"), 0f, 100f);
        S.temp += def.F("temp");
        if (def.Has("disease"))
            S.disease = Mathf.Max(0f, S.disease + def.F("disease"));
        if (def.B("smoke"))
            S.sinceSmoke = 0f;
        if (def.Has("poison"))
        {
            float chance = def.F("poison_chance", 1f);
            float amount = def.F("poison");
            if (GameSession.HasTrait("stomach")) { chance /= 2f; amount /= 2f; }
            if (Random.value < chance)
            {
                S.poison = Mathf.Min(100f, S.poison + amount);
                GameMessages.Show("Живот скрутило... Похоже на отравление");
            }
        }
        string verb = def.type == "drink" ? "Выпил" : def.B("smoke") ? "Использовал" : "Съел";
        GameMessages.Show($"{verb}: {def.name}");
        Sfx.Play(SoundId.Pickup, transform.position, 0.2f);
        GameSession.RemoveStack(stack);
        if (def.Has("leaves"))
            GameSession.Add(def.S("leaves"));
        return true;
    }

    // ---------------------------------------------------------------- состояния для HUD

    // Список состояний (текст, уровень 1..3), как «мудлы» в старой игре.
    public List<(string text, int level)> Moodles()
    {
        var list = new List<(string, int)>();
        void Add(float value, float[] th, string[] names)
        {
            for (int i = th.Length - 1; i >= 0; i--)
                if (value >= th[i]) { list.Add((names[i], i + 1)); return; }
        }
        Add(S.hunger, new[] { 40f, 70f, 90f }, new[] { "Голод", "Сильный голод", "Истощение" });
        Add(S.thirst, new[] { 40f, 70f, 90f }, new[] { "Жажда", "Сильная жажда", "Обезвоживание" });
        Add(S.fatigue, new[] { 50f, 80f, 95f }, new[] { "Усталость", "Изнеможение", "Валится с ног" });
        Add(S.disease, new[] { 15f, 40f, 70f }, new[] { "Кашель", "Лихорадка", "Бред" });
        Add(S.poison, new[] { 20f, 50f, 80f }, new[] { "Тошнота", "Отравление", "Сильное отравление" });
        if (S.painkiller <= 0f)
            Add(S.pain, new[] { 25f, 50f, 80f }, new[] { "Боль", "Сильная боль", "Невыносимая боль" });
        if (S.temp > 37.4f) list.Add((S.temp < 38.8f ? "Жар" : "Тепловой удар", S.temp < 38.8f ? 1 : 3));
        else if (S.temp < 35.9f) list.Add((S.temp > 35f ? "Озноб" : "Переохлаждение", S.temp > 35f ? 1 : 3));
        if (S.wounds.Exists(w => w.bleeding)) list.Add(("Кровотечение", 3));
        if (S.mood < 30f) list.Add((S.mood > 12f ? "Тоска" : "Депрессия", S.mood > 12f ? 1 : 2));
        if (GameSession.Weight() > GameSession.CarryLimit()) list.Add(("Перегруз", 1));
        return list;
    }
}
