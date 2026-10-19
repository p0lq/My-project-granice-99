using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

// Объект мира, состояние которого нужно сохранять: обысканный ящик, выломанная дверь,
// мёртвый «Серый». Ключ строится из имени и начального места, поэтому совпадает
// между запусками: сборщик сцен ставит объекты всегда в одни и те же точки.
public interface ISaveable
{
    string SaveKey { get; }
    string SaveState();
    void LoadState(string state);
}

public static class SaveKeys
{
    public static string Of(Component c, Vector3 position) =>
        $"{c.GetType().Name}:{c.gameObject.name}:{Mathf.RoundToInt(position.x * 10f)}:{Mathf.RoundToInt(position.z * 10f)}";
}

// Сохранение и загрузка, как F5/F9 в старой игре. Файл — save.json в папке данных игры
// (%USERPROFILE%\AppData\LocalLow\<компания>\<игра>).
public static class SaveSystem
{
    class Dropped
    {
        public ItemStack stack;
        public float x, y, z;
    }

    class Placed
    {
        public string kind;
        public float x, z;
    }

    class SaveFile
    {
        public int version = 1;
        public string session;
        public string scene;
        public float x, y, z, yaw;
        public Dictionary<string, string> states = new();
        // Предметы сцены, которые ещё лежат на месте; остальные подобраны.
        public List<string> collectibles = new();
        public List<Dropped> dropped = new();
        public List<Placed> placed = new();
    }

    static SaveFile pending;

    static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

    public static bool Exists => File.Exists(FilePath);

    public static bool Save()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null || GameManager.Instance == null)
            return false;

        var file = new SaveFile
        {
            session = GameSession.Serialize(),
            scene = SceneManager.GetActiveScene().name,
            x = player.transform.position.x,
            y = player.transform.position.y,
            z = player.transform.position.z,
            yaw = player.transform.eulerAngles.y,
        };
        foreach (var s in Saveables())
            file.states[s.SaveKey] = s.SaveState();
        foreach (var c in Object.FindObjectsByType<Collectible>(FindObjectsInactive.Exclude))
        {
            if (c.PickedUp)
                continue;
            if (Collectible.Dropped.Contains(c))
                file.dropped.Add(new Dropped { stack = c.Stack, x = c.transform.position.x, y = c.transform.position.y, z = c.transform.position.z });
            else
                file.collectibles.Add(SaveKeys.Of(c, c.transform.position));
        }
        foreach (var p in Placeables.All)
            if (p.obj != null)
                file.placed.Add(new Placed { kind = p.kind, x = p.position.x, z = p.position.z });

        File.WriteAllText(FilePath, JsonConvert.SerializeObject(file, Formatting.Indented));
        GameMessages.Show("Игра сохранена");
        return true;
    }

    // Загрузка: состояние героя ставится сразу, сцена грузится, а мир расставляется,
    // когда сцена готова (GameManager.Start вызывает ApplyPending).
    public static bool Load()
    {
        if (!Exists)
        {
            GameMessages.Show("Сохранений нет");
            return false;
        }
        var file = JsonConvert.DeserializeObject<SaveFile>(File.ReadAllText(FilePath));
        if (file == null || string.IsNullOrEmpty(file.scene))
            return false;
        GameSession.Deserialize(file.session);
        pending = file;
        Time.timeScale = 1f;
        SceneManager.LoadScene(file.scene);
        return true;
    }

    public static void ApplyPending()
    {
        var file = pending;
        pending = null;
        if (file == null || file.scene != SceneManager.GetActiveScene().name)
            return;

        var player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            // CharacterController сам держит позицию: на время переноса его выключаем.
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(new Vector3(file.x, file.y, file.z), Quaternion.Euler(0f, file.yaw, 0f));
            if (cc != null) cc.enabled = true;
            var cam = Object.FindAnyObjectByType<IsoCamera>();
            if (cam != null) cam.Snap();
        }

        // Сначала враги (мёртвые оставляют тела, которые тоже можно обыскать), потом остальное.
        foreach (var pass in new[] { true, false })
            foreach (var s in Saveables())
                if ((s is EnemyAI) == pass && file.states.TryGetValue(s.SaveKey, out var state))
                    s.LoadState(state);

        var present = new HashSet<string>(file.collectibles);
        foreach (var c in Object.FindObjectsByType<Collectible>(FindObjectsInactive.Exclude))
            if (!Collectible.Dropped.Contains(c) && !present.Contains(SaveKeys.Of(c, c.transform.position)))
                Object.Destroy(c.gameObject);
        foreach (var d in file.dropped)
            Collectible.Spawn(d.stack, new Vector3(d.x, d.y, d.z), 0f);
        foreach (var p in file.placed)
            Placeables.Place(p.kind, new Vector3(p.x, 0f, p.z));

        GameMessages.Show($"Загружено: {WorldClock.Text}");
    }

    static List<ISaveable> Saveables()
    {
        var list = new List<ISaveable>();
        foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            if (mb is ISaveable s)
                list.Add(s);
        return list;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay() => pending = null;
}
