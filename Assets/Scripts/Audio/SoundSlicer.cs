using System.Collections.Generic;
using UnityEngine;

// Делит длинный файл с несколькими звуками подряд (например, «multi-group» с десятком
// ударов ножом) на отдельные звуки по паузам тишины. Каждый кусок становится вариантом:
// при каждом ударе играет один короткий звук, а не весь файл на 25 секунд.
public static class SoundSlicer
{
    const float WindowSeconds = 0.01f;
    // Окно тише этой доли от самого громкого считается тишиной.
    const float SilenceLevel = 0.08f;
    // Паузы короче этой не разрывают звук на два.
    const float MinGap = 0.08f;
    const float MinLength = 0.08f;
    const float MaxLength = 1.2f;

    public static AudioClip[] Split(AudioClip source)
    {
        // GetData работает только для клипов, которые распаковываются при загрузке
        // (Load Type: Decompress On Load, его выставляет AudioImport).
        int channels = source.channels;
        int rate = source.frequency;
        var data = new float[source.samples * channels];
        if (!source.GetData(data, 0))
            return new[] { source };

        // Сводим в моно: так проще искать паузы, а 3D-звук всё равно моно.
        int frames = source.samples;
        var mono = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++)
                sum += data[i * channels + c];
            mono[i] = sum / channels;
        }

        int window = Mathf.Max(1, Mathf.RoundToInt(WindowSeconds * rate));
        int windows = frames / window;
        var loudness = new float[windows];
        float max = 0f;
        for (int w = 0; w < windows; w++)
        {
            float sum = 0f;
            for (int i = w * window; i < (w + 1) * window; i++)
                sum += mono[i] * mono[i];
            loudness[w] = Mathf.Sqrt(sum / window);
            max = Mathf.Max(max, loudness[w]);
        }
        if (max <= 0f)
            return new[] { source };

        var pieces = new List<AudioClip>();
        int gapWindows = Mathf.CeilToInt(MinGap / WindowSeconds);
        int start = -1, quiet = 0;
        for (int w = 0; w <= windows; w++)
        {
            bool loud = w < windows && loudness[w] >= max * SilenceLevel;
            if (loud)
            {
                if (start < 0)
                    start = w;
                quiet = 0;
            }
            else if (start >= 0 && (++quiet >= gapWindows || w == windows))
            {
                int end = w - quiet + 1;
                AddPiece(pieces, mono, rate, start * window, end * window, source.name);
                start = -1;
                quiet = 0;
            }
        }
        return pieces.Count > 0 ? pieces.ToArray() : new[] { source };
    }

    static void AddPiece(List<AudioClip> pieces, float[] mono, int rate, int from, int to, string name)
    {
        // Немного тишины до и после, чтобы не срезать начало удара и его затухание.
        from = Mathf.Max(0, from - rate / 50);
        to = Mathf.Min(mono.Length, to + rate / 15);
        int length = Mathf.Min(to - from, Mathf.RoundToInt(MaxLength * rate));
        if (length < MinLength * rate)
            return;

        var piece = new float[length];
        System.Array.Copy(mono, from, piece, 0, length);
        // Плавное затухание в конце, если кусок обрезан по MaxLength посреди звука.
        int fade = Mathf.Min(length / 4, rate / 20);
        for (int i = 0; i < fade; i++)
            piece[length - 1 - i] *= (float)i / fade;

        var clip = AudioClip.Create($"{name}_{pieces.Count}", length, 1, rate, false);
        clip.SetData(piece, 0);
        pieces.Add(clip);
    }
}
