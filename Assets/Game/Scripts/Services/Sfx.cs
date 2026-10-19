using UnityEngine;

/// <summary>
/// Efeitos sonoros gerados por código (sem arquivos de áudio): moeda, raspão, batida, power-up,
/// nitro, clique e um motor em loop cujo tom acompanha a velocidade.
/// Troque por arquivos .wav/.ogg quando tiver sons definitivos — basta substituir os AudioClips.
/// </summary>
public class Sfx : MonoBehaviour
{
    const int Rate = 44100;
    static Sfx instance;

    AudioSource[] voices;
    int nextVoice;
    AudioSource engine;
    AudioClip coin, nearMiss, perfect, crash, powerUp, nitro, click, engineLoop;

    public static Sfx I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("Sfx");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Sfx>();
            }
            return instance;
        }
    }

    public static void ApplyVolume() => AudioListener.volume = SaveData.Sound ? 1f : 0f;

    public static void StopEngine()
    {
        if (instance != null && instance.engine != null) { instance.engine.Stop(); instance.engine.volume = 0f; }
    }

    void Awake()
    {
        ApplyVolume();
        voices = new AudioSource[8];
        for (int i = 0; i < voices.Length; i++)
        {
            voices[i] = gameObject.AddComponent<AudioSource>();
            voices[i].playOnAwake = false;
        }
        engine = gameObject.AddComponent<AudioSource>();
        engine.loop = true;
        engine.playOnAwake = false;
        engine.volume = 0f;

        var rng = new System.Random(7);
        coin = Make("coin", 0.16f, (t, s) => Sine(t < 0.05f ? 988f : 1319f, t) * Mathf.Exp(-t * 18f) * 0.45f);
        click = Make("click", 0.04f, (t, s) => Sine(1600f, t) * Mathf.Exp(-t * 90f) * 0.4f);

        float lp = 0f;
        nearMiss = Make("nearmiss", 0.38f, (t, s) =>
        {
            float k = t / 0.38f;
            float cutoff = Mathf.Lerp(0.05f, 0.35f, Mathf.Sin(k * Mathf.PI));
            lp += cutoff * ((float)rng.NextDouble() * 2f - 1f - lp);
            return lp * Mathf.Sin(k * Mathf.PI) * 1.2f;
        });
        perfect = Make("perfect", 0.5f, (t, s) =>
            (Sine(1047f, t) + Sine(1319f, t) * 0.7f + Sine(1568f, t) * 0.5f) * Mathf.Exp(-t * 7f) * 0.22f);

        float lp2 = 0f;
        crash = Make("crash", 0.8f, (t, s) =>
        {
            lp2 += 0.25f * ((float)rng.NextDouble() * 2f - 1f - lp2);
            return lp2 * Mathf.Exp(-t * 5f) * 1.4f + Sine(55f, t) * Mathf.Exp(-t * 7f) * 0.6f;
        });

        powerUp = Make("powerup", 0.36f, (t, s) =>
        {
            float[] notes = { 523f, 659f, 784f, 1047f };
            int n = Mathf.Min(3, (int)(t / 0.08f));
            return Square(notes[n], t) * 0.18f * Mathf.Exp(-(t - n * 0.08f) * 12f);
        });

        float lp3 = 0f;
        nitro = Make("nitro", 0.9f, (t, s) =>
        {
            float k = t / 0.9f;
            lp3 += Mathf.Lerp(0.05f, 0.3f, k) * ((float)rng.NextDouble() * 2f - 1f - lp3);
            float f = Mathf.Lerp(120f, 520f, k);
            return (Saw(f, t) * 0.25f + lp3 * 0.9f) * Mathf.Sin(k * Mathf.PI) * 0.8f;
        });

        // Loop de 1s com frequências inteiras para emendar sem clique.
        float lp4 = 0f;
        engineLoop = Make("engine", 1f, (t, s) =>
        {
            float raw = Saw(55f, t) * 0.6f + Saw(110f, t) * 0.3f + Square(27f, t) * 0.2f;
            lp4 += 0.08f * (raw - lp4);
            return lp4 * 0.5f;
        });
        engine.clip = engineLoop;
    }

    static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
    static float Saw(float f, float t) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));
    static float Square(float f, float t) => Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f * t));

    static AudioClip Make(string name, float duration, System.Func<float, int, float> wave)
    {
        int n = Mathf.CeilToInt(duration * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave(i / (float)Rate, i), -1f, 1f);
        // Fade de 5ms nas pontas evita estalos.
        int fade = Mathf.Min(n / 2, Rate / 200);
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] *= k;
            data[n - 1 - i] *= k;
        }
        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        var v = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        v.pitch = pitch;
        v.PlayOneShot(clip, volume);
    }

    public void Coin(int streak) => Play(coin, 0.6f, 1f + Mathf.Min(streak, 10) * 0.04f);
    public void NearMiss(int combo) => Play(nearMiss, 0.9f, 1f + Mathf.Min(combo, 8) * 0.06f);
    public void Perfect() => Play(perfect, 0.8f);
    public void Crash() => Play(crash, 1f);
    public void Smash() => Play(crash, 0.6f, 1.4f);
    public void PowerUp() => Play(powerUp, 0.8f);
    public void Nitro() => Play(nitro, 0.9f);
    public void Click() => Play(click, 0.6f);

    /// <summary>Liga/desliga o motor. speed01 = 0 (devagar) a 1 (máximo).</summary>
    public void Engine(bool on, float speed01)
    {
        if (on && !engine.isPlaying) engine.Play();
        engine.volume = Mathf.MoveTowards(engine.volume, on ? 0.25f : 0f, Time.unscaledDeltaTime * 2f);
        engine.pitch = Mathf.Lerp(0.8f, 1.9f, speed01);
        if (!on && engine.volume <= 0f && engine.isPlaying) engine.Stop();
    }
}
