using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Проигрывание звуков из любого скрипта: Sfx.Play(SoundId.Door, transform.position).
// Сам создаёт объект со списком AudioSource, который живёт между сценами,
// и включает фон уровня: ветер в посёлке и меню, гул в бункере.
public class Sfx : MonoBehaviour
{
    const int PoolSize = 24;

    static Sfx instance;
    static readonly Dictionary<SoundId, AudioClip[]> clips = new();

    readonly List<AudioSource> pool = new();
    AudioSource uiSource;
    AudioSource ambientSource;
    Transform listener;
    Transform player;
    int next;

    public static float Volume { get; set; } = 1f;

    // Перезагрузка домена в Play выключена (см. GameSession), статику сбрасываем сами.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
        clips.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        var go = new GameObject("Звук");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<Sfx>();
    }

    // Звук в точке мира: громче рядом с игроком, тише вдали, слышно с нужной стороны.
    public static void Play(SoundId id, Vector3 position, float volume = 1f, float pitchJitter = 0.08f)
    {
        if (instance == null)
            return;
        var source = instance.NextSource();
        source.transform.position = position;
        source.clip = Clip(id);
        source.volume = volume * Volume;
        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.Play();
    }

    // Звук без положения в мире: кнопки меню, победа, проигрыш. Играет и на паузе.
    public static void PlayUI(SoundId id, float volume = 1f)
    {
        if (instance == null)
            return;
        instance.uiSource.PlayOneShot(Clip(id), volume * Volume);
    }

    static AudioClip Clip(SoundId id)
    {
        if (!clips.TryGetValue(id, out var variants))
        {
            // Свой файл в Resources/Audio важнее синтезированного звука.
            var file = Resources.Load<AudioClip>($"Audio/{id}");
            if (file != null)
                variants = new[] { file };
            else
            {
                variants = new AudioClip[SoundSynth.Variants(id)];
                for (int i = 0; i < variants.Length; i++)
                    variants[i] = SoundSynth.Create(id, i);
            }
            clips[id] = variants;
        }
        return variants[Random.Range(0, variants.Length)];
    }

    void Awake()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            var source = new GameObject("Источник").AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 2f;
            source.maxDistance = 28f;
            source.dopplerLevel = 0f;
            pool.Add(source);
        }

        uiSource = gameObject.AddComponent<AudioSource>();
        uiSource.playOnAwake = false;
        uiSource.spatialBlend = 0f;
        uiSource.ignoreListenerPause = true;

        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.playOnAwake = false;
        ambientSource.loop = true;
        ambientSource.spatialBlend = 0f;

        // Слушатель стоит у игрока, а не на камере: камера висит в 14 м над ним,
        // и с её высоты все звуки казались бы одинаково далёкими.
        listener = new GameObject("Слушатель").transform;
        listener.SetParent(transform, false);
        listener.gameObject.AddComponent<AudioListener>();

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Два AudioListener в сцене Unity не любит, поэтому слушатель на камере выключаем.
        foreach (var other in FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude))
            if (other.transform != listener)
                other.enabled = false;

        var playerObject = GameObject.FindWithTag("Player");
        player = playerObject != null ? playerObject.transform : null;
        AudioListener.pause = false;

        SoundId? ambient = scene.name switch
        {
            SceneNames.Bunker => SoundId.AmbientHum,
            SceneNames.Village or SceneNames.MainMenu or "Test" => SoundId.AmbientWind,
            _ => null,
        };
        if (ambient.HasValue)
        {
            ambientSource.clip = Clip(ambient.Value);
            ambientSource.volume = (ambient == SoundId.AmbientHum ? 0.25f : 0.35f) * Volume;
            ambientSource.Play();
        }
        else
            ambientSource.Stop();
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        Vector3 position = player != null ? player.position + Vector3.up * 1.5f : cam != null ? cam.transform.position : Vector3.zero;
        listener.position = position;
        // Поворот берём у камеры по горизонтали: звук справа на экране слышен справа.
        if (cam != null)
            listener.rotation = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f);
    }

    AudioSource NextSource()
    {
        // Ищем свободный источник; если все заняты, перебиваем самый старый.
        for (int i = 0; i < pool.Count; i++)
        {
            var source = pool[(next + i) % pool.Count];
            if (!source.isPlaying)
            {
                next = (next + i + 1) % pool.Count;
                return source;
            }
        }
        var oldest = pool[next];
        next = (next + 1) % pool.Count;
        return oldest;
    }
}
