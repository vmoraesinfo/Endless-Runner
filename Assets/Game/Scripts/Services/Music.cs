using UnityEngine;

/// <summary>
/// Trilha sonora sintetizada por código (sem arquivos de áudio), estilo synth/chiptune:
///  - Menu: 96 BPM, acordes suaves, arpejo leve e chimbal discreto.
///  - Corrida: 140 BPM, baixo pulsante, arpejo, melodia, bumbo/caixa/chimbal.
/// As duas usam a progressão Lá menor – Fá – Dó – Sol (8 compassos, loop sem emenda).
/// Gerado uma vez por sessão; troca entre faixas com fade.
/// </summary>
public class Music : MonoBehaviour
{
    const int Rate = 22050;
    const float Volume = 0.32f;

    static Music instance;
    AudioSource a, b;
    AudioClip menu, race;
    AudioSource current;
    float fadeSpeed = 1.5f;

    // Acordes (MIDI) por compasso: Am, F, C, G (repetidos 2x = 8 compassos).
    static readonly int[][] Chords =
    {
        new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 },
    };
    // Melodia da corrida: graus da escala de Lá menor por colcheia (-1 = pausa), 16 colcheias por 2 compassos.
    static readonly int[] Melody =
    {
        76, -1, 74, 72, -1, 72, 74, 76,   77, -1, 76, 74, -1, 72, 69, -1,
        72, -1, 71, 72, -1, 74, 76, -1,   74, -1, 72, 71, -1, 67, 71, 74,
    };

    static Music I
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("Music");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Music>();
            }
            return instance;
        }
    }

    public static void PlayMenu() => I.Play(I.menu ??= Build(false));
    public static void PlayRace() => I.Play(I.race ??= Build(true));

    void Awake()
    {
        a = gameObject.AddComponent<AudioSource>();
        b = gameObject.AddComponent<AudioSource>();
        foreach (var s in new[] { a, b })
        {
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
            s.ignoreListenerPause = true;
        }
    }

    void Play(AudioClip clip)
    {
        if (current != null && current.clip == clip && current.isPlaying) return;
        var next = current == a ? b : a;
        next.clip = clip;
        next.volume = 0f;
        next.Play();
        current = next;
    }

    void Update()
    {
        // Fade: a fonte atual sobe até o volume, a outra desce e para.
        foreach (var s in new[] { a, b })
        {
            float target = s == current ? Volume : 0f;
            s.volume = Mathf.MoveTowards(s.volume, target, fadeSpeed * Time.unscaledDeltaTime * Volume);
            if (s != current && s.volume <= 0f && s.isPlaying) s.Stop();
        }
    }

    // ---------------------------------------------------------------- síntese

    static float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    static AudioClip Build(bool racing)
    {
        float bpm = racing ? 140f : 96f;
        float beat = 60f / bpm;
        int bars = 8;
        float length = bars * 4 * beat;
        int n = Mathf.CeilToInt(length * Rate);
        var data = new float[n];
        var rng = new System.Random(racing ? 11 : 7);
        float lp = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float beatPos = t / beat;               // posição em batidas
            int bar = (int)(beatPos / 4f) % bars;
            int[] chord = Chords[bar % Chords.Length];
            float inBeat = beatPos - Mathf.Floor(beatPos);
            float eighth = beatPos * 2f;
            float inEighth = eighth - Mathf.Floor(eighth);
            float sixteenth = beatPos * 4f;
            float inSixteenth = sixteenth - Mathf.Floor(sixteenth);
            float s = 0f;

            // Pad: acorde com seno + triângulo, ataque suave por compasso.
            float barPos = beatPos / 4f - Mathf.Floor(beatPos / 4f);
            float padEnv = Mathf.Min(1f, barPos * 8f) * (racing ? 0.35f : 0.6f);
            foreach (int note in chord)
                s += (Sine(Freq(note), t) * 0.6f + Tri(Freq(note) * 0.5f, t) * 0.4f) * 0.11f * padEnv;

            // Arpejo: semicolcheias (corrida) ou colcheias (menu) percorrendo o acorde.
            float arpStep = racing ? sixteenth : eighth;
            float arpIn = racing ? inSixteenth : inEighth;
            int arpNote = chord[(int)arpStep % 3] + 12;
            float arpEnv = Mathf.Exp(-arpIn * (racing ? 9f : 5f));
            s += Square(Freq(arpNote), t, 0.25f) * 0.055f * arpEnv;

            if (racing)
            {
                // Baixo em colcheias com oitava alternada.
                int bassNote = chord[0] - 24 + ((int)eighth % 2 == 1 ? 12 : 0);
                float bass = Saw(Freq(bassNote), t) * Mathf.Exp(-inEighth * 4f);
                lp += 0.12f * (bass - lp);
                s += lp * 0.30f;

                // Melodia (a partir do 3º compasso para ter introdução).
                if (bar >= 2)
                {
                    int m = Melody[(int)eighth % Melody.Length];
                    if (m > 0) s += (Square(Freq(m), t, 0.5f) * 0.5f + Tri(Freq(m), t) * 0.5f) * 0.09f * Mathf.Exp(-inEighth * 2.5f);
                }

                // Bumbo em toda batida (queda de frequência), caixa nos tempos 2 e 4, chimbal nas colcheias.
                float kickF = 50f + 110f * Mathf.Exp(-inBeat * beat * 30f);
                s += Mathf.Sin(2f * Mathf.PI * kickF * inBeat * beat) * Mathf.Exp(-inBeat * beat * 14f) * 0.55f;
                int beatInBar = (int)beatPos % 4;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                if (beatInBar == 1 || beatInBar == 3)
                    s += noise * Mathf.Exp(-inBeat * beat * 22f) * 0.28f;
                s += noise * Mathf.Exp(-inEighth * beat * 0.5f * 90f) * 0.07f;
            }
            else
            {
                // Chimbal bem leve no contratempo.
                float noise = (float)rng.NextDouble() * 2f - 1f;
                if ((int)eighth % 2 == 1) s += noise * Mathf.Exp(-inEighth * beat * 0.5f * 120f) * 0.035f;
                // Baixo longo de raiz.
                s += Sine(Freq(chord[0] - 24), t) * 0.18f * Mathf.Min(1f, barPos * 6f);
            }

            data[i] = Mathf.Clamp(s, -1f, 1f);
        }

        var clip = AudioClip.Create(racing ? "music_race" : "music_menu", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static float Sine(float f, float t) => Mathf.Sin(2f * Mathf.PI * f * t);
    static float Saw(float f, float t) => 2f * (t * f - Mathf.Floor(t * f + 0.5f));
    static float Tri(float f, float t) => 2f * Mathf.Abs(Saw(f, t)) - 1f;
    static float Square(float f, float t, float duty)
    {
        float p = t * f - Mathf.Floor(t * f);
        return p < duty ? 1f : -1f;
    }
}
