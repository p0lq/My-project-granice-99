using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Проигрывание звуков из любого скрипта: Sfx.Play(SoundId.DoorOpen, transform.position).
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

    // Общий множитель громкости (на будущее, для ползунка в настройках).
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
        var clip = Clip(id);
        if (instance == null || clip == null)
            return;
        var source = instance.NextSource();
        source.transform.position = position;
        source.clip = clip;
        source.volume = volume * Volume;
        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.Play();
    }

    // Звук без положения в мире: кнопки меню, победа, проигрыш. Играет и на паузе.
    public static void PlayUI(SoundId id, float volume = 1f)
    {
        var clip = Clip(id);
        if (instance == null || clip == null)
            return;
        instance.uiSource.PlayOneShot(clip, volume * Volume);
    }

    // Все файлы из Resources/Audio/<id> — варианты одного звука, чтобы повторы
    // (шаги, удары) не звучали одинаково. null, если папки нет.
    public static AudioClip Clip(SoundId id)
    {
        if (!clips.TryGetValue(id, out var variants))
        {
            variants = Resources.LoadAll<AudioClip>($"Audio/{id}");
            // В файле удара зомби много ударов подряд: режем на отдельные.
            if (id == SoundId.ZombieAttack && variants.Length == 1)
                variants = SoundSlicer.Split(variants[0]);
            clips[id] = variants;
        }
        return variants.Length > 0 ? variants[Random.Range(0, variants.Length)] : null;
    }

    void Awake()
    {
        for (int i = 0; i < PoolSize; i++)
        {
            var source = new GameObject("Источник").AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false;
            Make3D(source, 1.5f, 20f);
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
        var ambientClip = ambient.HasValue ? Clip(ambient.Value) : null;
        if (ambientClip != null)
        {
            ambientSource.clip = ambientClip;
            // Ветер — запись light-breeze, по просьбе на 30 % тише файла; гул бункера из Kenney.
            ambientSource.volume = (ambient == SoundId.AmbientWind ? 0.7f : 0.18f) * Volume;
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

    // Источник в мире с естественным затуханием: громкость падает с квадратом расстояния
    // (как в жизни, только быстрее), а дальше maxDistance звук не слышен совсем.
    // Стандартное линейное затухание Unity оставляло далёкие звуки слишком громкими.
    public static void Make3D(AudioSource source, float fullVolumeDistance, float maxDistance)
    {
        source.spatialBlend = 1f;
        source.dopplerLevel = 0f;
        source.minDistance = fullVolumeDistance;
        source.maxDistance = maxDistance;
        source.rolloffMode = AudioRolloffMode.Custom;
        float start = Mathf.Clamp01(fullVolumeDistance / maxDistance);
        var curve = new AnimationCurve();
        curve.AddKey(0f, 1f);
        curve.AddKey(start, 1f);
        // Точки кривой (доля от maxDistance → громкость), примерно (1 − x)².
        for (int i = 1; i <= 8; i++)
        {
            float x = start + (1f - start) * i / 8f;
            float k = (x - start) / (1f - start);
            curve.AddKey(x, (1f - k) * (1f - k));
        }
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
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
