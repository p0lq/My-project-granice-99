using System;
using UnityEngine;

// Звуки, собранные из шума и простых волн прямо в коде. Это временная замена настоящим
// записям: ничего не нужно скачивать и проверять лицензии. Любой звук заменяется файлом
// в Assets/Resources/Audio (см. SoundId).
//
// Приёмы везде одни и те же: шум или пилообразная волна, фильтр нижних частот
// (убирает резкость), огибающая громкости (как быстро звук нарастает и затухает).
public static class SoundSynth
{
    const int Rate = 44100;

    public static AudioClip Create(SoundId id, int variant)
    {
        var rnd = new System.Random((int)id * 101 + variant * 7919);
        float[] data = id switch
        {
            SoundId.Footstep => Footstep(rnd),
            SoundId.Swing => Swing(rnd),
            SoundId.Hit => Hit(rnd),
            SoundId.PlayerHurt => Hurt(rnd),
            SoundId.ZombieGroan => Groan(rnd, 62f + variant * 9f),
            SoundId.ZombieScream => Scream(rnd),
            SoundId.ZombieAttack => ZombieAttack(rnd),
            SoundId.ZombieDeath => ZombieDeath(rnd),
            SoundId.Pickup => Pickup(),
            SoundId.Door => Creak(rnd),
            SoundId.Locked => Clank(rnd, 0.35f, 0.5f),
            SoundId.Crate => Rustle(rnd),
            SoundId.Well => Well(rnd),
            SoundId.Lever => Lever(rnd),
            SoundId.Click => Tick(rnd, 2400f, 0.04f),
            SoundId.UIClick => Tick(rnd, 1300f, 0.06f),
            SoundId.Win => Arpeggio(new[] { 523f, 659f, 784f, 1047f }, 0.2f, 0.9f),
            SoundId.Lose => Arpeggio(new[] { 330f, 262f, 196f, 147f }, 0.35f, 1.4f),
            SoundId.AmbientWind => Wind(rnd),
            SoundId.AmbientHum => Hum(rnd),
            _ => new float[Rate / 10],
        };
        Normalize(data, 0.9f);
        var clip = AudioClip.Create($"{id}_{variant}", data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Сколько вариантов звука делать, чтобы повторы не звучали одинаково.
    public static int Variants(SoundId id) => id switch
    {
        SoundId.Footstep => 4,
        SoundId.ZombieGroan => 3,
        SoundId.Hit => 2,
        SoundId.Swing => 2,
        _ => 1,
    };

    // ---------------------------------------------------------------- звуки

    static float[] Footstep(System.Random rnd)
    {
        var d = Buffer(0.14f);
        var lp = new LowPass(700f + (float)rnd.NextDouble() * 400f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            float scuff = lp.Run(Noise(rnd)) * Decay(t, 0.03f);
            float thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * Decay(t, 0.025f) * 0.6f;
            d[i] = scuff + thump;
        }
        return d;
    }

    static float[] Swing(System.Random rnd)
    {
        var d = Buffer(0.3f);
        var lp = new LowPass(400f);
        for (int i = 0; i < d.Length; i++)
        {
            float x = T(i) / 0.3f;
            // Свист: полоса шума, которая быстро уходит вверх и опускается обратно.
            lp.SetCutoff(300f + 2600f * Mathf.Sin(Mathf.PI * x));
            d[i] = lp.Run(Noise(rnd)) * Mathf.Sin(Mathf.PI * x);
        }
        return d;
    }

    static float[] Hit(System.Random rnd)
    {
        var d = Buffer(0.25f);
        var lp = new LowPass(1600f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            phase += 2f * Mathf.PI * (110f - 60f * Mathf.Min(1f, t / 0.15f)) / Rate;
            float body = Mathf.Sin(phase) * Decay(t, 0.07f);
            float crack = lp.Run(Noise(rnd)) * Decay(t, 0.02f);
            d[i] = body + crack * 0.8f;
        }
        return d;
    }

    static float[] Hurt(System.Random rnd)
    {
        var d = Buffer(0.35f);
        var lp = new LowPass(900f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            phase += (150f - 50f * t / 0.35f) / Rate;
            float voice = Saw(phase);
            d[i] = lp.Run(voice * 0.8f + Noise(rnd) * 0.25f) * Attack(t, 0.02f) * Decay(t, 0.12f);
        }
        return d;
    }

    static float[] Groan(System.Random rnd, float baseFreq)
    {
        var d = Buffer(1.5f);
        var lp = new LowPass(450f);
        float phase = 0f, sub = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            float f = baseFreq + 6f * Mathf.Sin(2f * Mathf.PI * 4.5f * t) + 10f * Mathf.Sin(Mathf.PI * t / 1.5f);
            phase += f / Rate;
            sub += f * 0.5f / Rate;
            lp.SetCutoff(350f + 250f * Mathf.Sin(2f * Mathf.PI * 1.3f * t));
            float voice = Saw(phase) + 0.5f * Saw(sub) + Noise(rnd) * 0.15f;
            float env = Attack(t, 0.25f) * Release(t, 1.5f, 0.5f) * (0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 7f * t));
            d[i] = lp.Run(voice) * env;
        }
        return d;
    }

    static float[] Scream(System.Random rnd)
    {
        var d = Buffer(1.6f);
        var lp = new LowPass(3200f);
        var hp = new LowPass(600f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            float x = t / 1.6f;
            float f = 260f + 300f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, x * 1.4f)) + 25f * (float)(rnd.NextDouble() - 0.5);
            phase += f / Rate;
            float n = Noise(rnd);
            float breath = n - hp.Run(n);
            float voice = Saw(phase) + breath * 0.5f;
            // Перегрузка (tanh) даёт хриплый, рваный тембр.
            d[i] = (float)Math.Tanh(lp.Run(voice) * 3f) * Attack(t, 0.05f) * Release(t, 1.6f, 0.5f);
        }
        return d;
    }

    static float[] ZombieAttack(System.Random rnd)
    {
        var d = Buffer(0.55f);
        var lp = new LowPass(1200f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            phase += (120f + 40f * Mathf.Sin(2f * Mathf.PI * 9f * t)) / Rate;
            float voice = Saw(phase) + Noise(rnd) * 0.4f;
            d[i] = (float)Math.Tanh(lp.Run(voice) * 2.5f) * Attack(t, 0.03f) * Decay(t, 0.2f);
        }
        return d;
    }

    static float[] ZombieDeath(System.Random rnd)
    {
        var d = Buffer(1.3f);
        var lp = new LowPass(500f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            phase += (95f - 50f * t / 1.3f) / Rate;
            float voice = Saw(phase) + Noise(rnd) * 0.2f;
            d[i] = lp.Run(voice) * Attack(t, 0.05f) * Decay(t, 0.5f);
        }
        return d;
    }

    static float[] Pickup()
    {
        var d = Buffer(0.35f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            float a = t < 0.12f ? Mathf.Sin(2f * Mathf.PI * 880f * t) * Decay(t, 0.08f) : 0f;
            float b = t > 0.09f ? Mathf.Sin(2f * Mathf.PI * 1320f * t) * Decay(t - 0.09f, 0.1f) : 0f;
            d[i] = a + b;
        }
        return d;
    }

    static float[] Creak(System.Random rnd)
    {
        var d = Buffer(0.8f);
        var lp = new LowPass(1400f);
        float phase = 0f;
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            // Скрип петель: неровный тон, который дрожит и плавает.
            float f = 170f + 90f * Mathf.Sin(2f * Mathf.PI * 1.4f * t) + 30f * (float)(rnd.NextDouble() - 0.5);
            phase += f / Rate;
            float grain = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 23f * t);
            d[i] = lp.Run(Saw(phase) * grain) * Attack(t, 0.05f) * Release(t, 0.8f, 0.25f);
        }
        return d;
    }

    static float[] Clank(System.Random rnd, float length, float decay)
    {
        var d = Buffer(length);
        // Металл звенит негармоничными обертонами: частоты не кратны друг другу.
        float[] partials = { 523f, 1187f, 1769f, 2401f, 3130f };
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            float s = 0f;
            for (int k = 0; k < partials.Length; k++)
                s += Mathf.Sin(2f * Mathf.PI * partials[k] * t) * Decay(t, decay / (k + 1)) / (k + 1);
            d[i] = s + Noise(rnd) * Decay(t, 0.005f) * 0.5f;
        }
        return d;
    }

    static float[] Rustle(System.Random rnd)
    {
        var d = Buffer(0.6f);
        var lp = new LowPass(2500f);
        float[] bumps = { 0f, 0.09f, 0.21f, 0.3f, 0.44f };
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            float env = 0f;
            foreach (float b in bumps)
                if (t >= b)
                    env += Decay(t - b, 0.04f);
            float knock = Mathf.Sin(2f * Mathf.PI * 210f * t) * Decay(t, 0.05f);
            d[i] = lp.Run(Noise(rnd)) * env * 0.6f + knock;
        }
        return d;
    }

    static float[] Well(System.Random rnd)
    {
        var chain = Clank(rnd, 1.1f, 0.25f);
        var lp = new LowPass(2800f);
        for (int i = 0; i < chain.Length; i++)
        {
            float t = T(i);
            float splash = t > 0.45f ? lp.Run(Noise(rnd)) * Decay(t - 0.45f, 0.18f) * 1.2f : 0f;
            chain[i] = chain[i] * 0.6f + splash;
        }
        return chain;
    }

    static float[] Lever(System.Random rnd)
    {
        var d = Clank(rnd, 1.6f, 0.4f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            // Запуск генератора: низкий гул нарастает и поднимается от 40 до 100 Гц.
            float f = 40f + 60f * Mathf.Min(1f, t / 1.2f);
            float hum = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Min(1f, t / 0.8f) * 0.7f;
            d[i] = d[i] * 0.7f + hum * Release(t, 1.6f, 0.3f);
        }
        return d;
    }

    static float[] Tick(System.Random rnd, float freq, float length)
    {
        var d = Buffer(length);
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            d[i] = (Mathf.Sin(2f * Mathf.PI * freq * t) + Noise(rnd) * 0.3f) * Decay(t, length / 4f);
        }
        return d;
    }

    static float[] Arpeggio(float[] notes, float step, float tail)
    {
        float length = step * notes.Length + tail;
        var d = Buffer(length);
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            float s = 0f;
            for (int k = 0; k < notes.Length; k++)
            {
                float start = k * step;
                if (t < start)
                    break;
                float local = t - start;
                // Треугольная волна мягче синуса по тембру и не режет слух, как пила.
                float tri = 2f * Mathf.Abs(2f * (notes[k] * local % 1f) - 1f) - 1f;
                s += tri * Decay(local, tail * 0.4f);
            }
            d[i] = s;
        }
        return d;
    }

    // Фоновые петли: все колебания укладываются в длину целое число раз,
    // поэтому конец петли совпадает с началом и щелчка на стыке нет.
    static float[] Wind(System.Random rnd)
    {
        const float length = 12f;
        var d = Buffer(length);
        var lp = new LowPass(500f);
        var lp2 = new LowPass(500f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            float gust = 0.55f + 0.3f * Mathf.Sin(2f * Mathf.PI * t / 6f) + 0.15f * Mathf.Sin(2f * Mathf.PI * t / 2f);
            lp.SetCutoff(250f + 450f * gust);
            lp2.SetCutoff(250f + 450f * gust);
            d[i] = lp2.Run(lp.Run(Noise(rnd))) * gust;
        }
        return Crossfade(d, Rate / 2);
    }

    static float[] Hum(System.Random rnd)
    {
        const float length = 6f;
        var d = Buffer(length);
        var lp = new LowPass(180f);
        for (int i = 0; i < d.Length; i++)
        {
            float t = T(i);
            float hum = 0.5f * Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.25f * Mathf.Sin(2f * Mathf.PI * 100f * t)
                      + 0.1f * Mathf.Sin(2f * Mathf.PI * 150f * t);
            float wobble = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * t / 3f);
            d[i] = hum * wobble + lp.Run(Noise(rnd)) * 0.6f;
        }
        return Crossfade(d, Rate / 2);
    }

    // ---------------------------------------------------------------- основа

    static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

    static float T(int i) => (float)i / Rate;

    static float Noise(System.Random rnd) => (float)(rnd.NextDouble() * 2.0 - 1.0);

    // Пилообразная волна по фазе в оборотах (0..1).
    static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase)) - 1f;

    static float Decay(float t, float tau) => Mathf.Exp(-t / tau);

    static float Attack(float t, float time) => Mathf.Clamp01(t / time);

    static float Release(float t, float length, float time) => Mathf.Clamp01((length - t) / time);

    static void Normalize(float[] data, float peak)
    {
        float max = 0f;
        foreach (float s in data)
            max = Mathf.Max(max, Mathf.Abs(s));
        if (max < 1e-5f)
            return;
        float k = peak / max;
        for (int i = 0; i < data.Length; i++)
            data[i] *= k;
    }

    // Конец петли плавно перетекает в её начало: шум на стыке иначе дал бы щелчок.
    static float[] Crossfade(float[] data, int samples)
    {
        int n = data.Length;
        for (int i = 0; i < samples; i++)
        {
            float k = (float)i / samples;
            data[i] = data[i] * k + data[n - samples + i] * (1f - k);
        }
        Array.Resize(ref data, n - samples);
        return data;
    }

    // Фильтр нижних частот первого порядка: пропускает низкие частоты, срезает высокие.
    class LowPass
    {
        float a;
        float y;

        public LowPass(float cutoff) => SetCutoff(cutoff);

        public void SetCutoff(float cutoff) => a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate);

        public float Run(float x)
        {
            y += a * (x - y);
            return y;
        }
    }
}
