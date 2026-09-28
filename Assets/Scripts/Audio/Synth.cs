// Procedural sound: every effect and the per-mall "dead mall muzak" loops are synthesised
// from sines, FM and filtered noise at startup. Pure math (no UnityEngine) so music can be
// rendered on a worker thread.
using System;

namespace WishExtractor.Audio
{
    public static class Synth
    {
        public const int Rate = 44100;

        static float Env(float t, float attack, float decay) => t < attack ? t / attack : (float)Math.Exp(-(t - attack) / decay);
        static float Sin(double phase) => (float)Math.Sin(phase * 2 * Math.PI);

        public static float[] Buffer(float seconds) => new float[(int)(seconds * Rate)];

        public static void Normalize(float[] b, float peak = 0.9f)
        {
            float m = 1e-6f;
            foreach (var v in b) m = Math.Max(m, Math.Abs(v));
            float k = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        static void Fade(float[] b, float outSec = 0.01f)
        {
            int n = Math.Min(b.Length, (int)(outSec * Rate));
            for (int i = 0; i < n; i++) b[b.Length - 1 - i] *= i / (float)n;
        }

        // ── effects ────────────────────────────────────────────────────────

        public static float[] Dig(int seed)
        {
            var r = new Random(seed);
            var b = Buffer(0.22f);
            float lp = 0;
            double ph = 0;
            float f0 = 110 + (float)r.NextDouble() * 40;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(r.NextDouble() * 2 - 1);
                lp += (noise - lp) * 0.18f;
                float crunch = lp * Env(t, 0.002f, 0.045f) * 1.2f;
                float f = f0 * (1 - t * 2.5f);
                ph += Math.Max(30, f) / Rate;
                float thump = Sin(ph) * Env(t, 0.003f, 0.07f) * 0.9f;
                b[i] = crunch + thump;
            }
            // a couple of tiny coin clinks in the rubble
            AddClink(b, 0.03f + (float)r.NextDouble() * 0.03f, 3000 + (float)r.NextDouble() * 1200, 0.18f, 0.05f);
            AddClink(b, 0.07f + (float)r.NextDouble() * 0.05f, 3600 + (float)r.NextDouble() * 1500, 0.12f, 0.04f);
            Normalize(b, 0.85f);
            Fade(b);
            return b;
        }

        static void AddClink(float[] b, float at, float freq, float amp, float decay)
        {
            int s = (int)(at * Rate);
            double p1 = 0, p2 = 0, p3 = 0;
            for (int i = s; i < b.Length; i++)
            {
                float t = (i - s) / (float)Rate;
                p1 += freq / Rate; p2 += freq * 1.51 / Rate; p3 += freq * 2.37 / Rate;
                b[i] += (Sin(p1) + 0.6f * Sin(p2) + 0.35f * Sin(p3)) * amp * Env(t, 0.0008f, decay);
            }
        }

        public static float[] Coin(int seed)
        {
            var r = new Random(seed);
            var b = Buffer(0.45f);
            float f = 2300 + (float)r.NextDouble() * 900;
            AddClink(b, 0, f, 0.6f, 0.12f);
            AddClink(b, 0.004f, f * 1.37f, 0.3f, 0.09f);
            Normalize(b, 0.7f);
            Fade(b);
            return b;
        }

        /// <summary>Something landing in the fountain: a filtered noise slap plus a falling "bloop" bubble.</summary>
        public static float[] Splash(int seed, bool big)
        {
            var r = new Random(seed);
            var b = Buffer(big ? 0.7f : 0.35f);
            float lp = 0, bp = 0;
            double ph = 0;
            float f0 = (big ? 380 : 900) + (float)r.NextDouble() * 200;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(r.NextDouble() * 2 - 1);
                lp += (noise - lp) * (big ? 0.25f : 0.45f);
                bp += (lp - bp) * 0.1f;
                float slap = (lp - bp) * Env(t, 0.002f, big ? 0.09f : 0.04f) * 1.3f;
                // bubble: a sine sweeping upward, as bubbles do
                float bt = t - 0.015f;
                float bub = 0;
                if (bt > 0)
                {
                    ph += f0 * (1 + bt * 6) / Rate;
                    bub = Sin(ph) * Env(bt, 0.003f, big ? 0.08f : 0.05f) * 0.7f;
                }
                b[i] = slap + bub;
            }
            if (big)
                for (int k = 0; k < 4; k++) AddClink(b, 0.12f + k * 0.07f + (float)r.NextDouble() * 0.04f, 700 + (float)r.NextDouble() * 500, 0.06f, 0.04f);
            Normalize(b, 0.8f);
            Fade(b);
            return b;
        }

        public static float[] Register()
        {
            var b = Buffer(1.4f);
            var r = new Random(4);
            // "ka": drawer mechanism
            float lp = 0;
            for (int i = 0; i < (int)(0.08f * Rate); i++)
            {
                float t = i / (float)Rate;
                lp += ((float)(r.NextDouble() * 2 - 1) - lp) * 0.35f;
                b[i] += lp * Env(t, 0.001f, 0.02f) * 0.8f;
            }
            // "ching": bright bell
            int s = (int)(0.07f * Rate);
            double[] partials = { 1318.5, 1975.5, 2637, 3520, 5274 };
            float[] amps = { 0.5f, 0.35f, 0.3f, 0.18f, 0.1f };
            double[] ph = new double[partials.Length];
            for (int i = s; i < b.Length; i++)
            {
                float t = (i - s) / (float)Rate;
                float v = 0;
                for (int k = 0; k < partials.Length; k++)
                {
                    ph[k] += partials[k] * (1 + 0.002 * Math.Sin(t * 30)) / Rate;
                    v += Sin(ph[k]) * amps[k] * Env(t, 0.001f, 0.35f / (1 + k * 0.4f));
                }
                b[i] += v;
            }
            AddClink(b, 0.12f, 2800, 0.25f, 0.08f);
            AddClink(b, 0.16f, 3400, 0.2f, 0.07f);
            Normalize(b, 0.8f);
            Fade(b, 0.05f);
            return b;
        }

        static void Tone(float[] b, float at, double freq, float amp, float attack, float decay, float fmIndex = 0, double fmRatio = 2, float dur = 99)
        {
            int s = (int)(at * Rate);
            double pc = 0, pm = 0;
            int end = Math.Min(b.Length, s + (int)(dur * Rate));
            for (int i = s; i < end; i++)
            {
                float t = (i - s) / (float)Rate;
                float e = Env(t, attack, decay);
                pm += freq * fmRatio / Rate;
                pc += freq / Rate;
                float mod = fmIndex * e * Sin(pm);
                b[i] += (float)Math.Sin((pc + mod * 0.15) * 2 * Math.PI) * amp * e;
            }
        }

        static double Midi(double n) => 440.0 * Math.Pow(2, (n - 69) / 12.0);

        public static float[] Blip(int up)
        {
            var b = Buffer(0.28f);
            Tone(b, 0, Midi(84), 0.5f, 0.002f, 0.05f, 1.2f, 1);
            Tone(b, 0.055f, Midi(84 + up), 0.5f, 0.002f, 0.09f, 1.2f, 1);
            Normalize(b, 0.6f);
            Fade(b);
            return b;
        }

        public static float[] Arp(int[] notes, float step, float decay, float fm, float len, float peak = 0.75f)
        {
            var b = Buffer(len);
            for (int i = 0; i < notes.Length; i++) Tone(b, i * step, Midi(notes[i]), 0.45f, 0.003f, decay, fm, 2);
            Normalize(b, peak);
            Fade(b, 0.05f);
            return b;
        }

        public static float[] WishCatch() => Arp(new[] { 79, 83, 86, 90, 91, 95 }, 0.045f, 0.35f, 0.8f, 1.1f, 0.7f);
        public static float[] WishSpawn()
        {
            var b = Buffer(0.9f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                double f = 1400 + 1200 * t + 40 * Math.Sin(t * 40);
                ph += f / Rate;
                b[i] = Sin(ph) * Env(t, 0.25f, 0.25f) * 0.3f + Sin(ph * 1.5) * Env(t, 0.3f, 0.2f) * 0.12f;
            }
            Normalize(b, 0.35f);
            Fade(b);
            return b;
        }

        public static float[] Whoosh(bool down)
        {
            var b = Buffer(0.6f);
            var r = new Random(9);
            float lp = 0, bp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float cut = down ? 0.25f * (1 - t) + 0.02f : 0.03f + 0.25f * t;
                float n = (float)(r.NextDouble() * 2 - 1);
                lp += (n - lp) * cut;
                bp += (lp - bp) * cut * 0.5f;
                b[i] = (lp - bp) * Env(t, 0.12f, 0.18f);
            }
            Normalize(b, 0.4f);
            Fade(b);
            return b;
        }

        public static float[] Fanfare(bool big)
        {
            int[] a = big ? new[] { 60, 64, 67, 72, 76, 79, 84 } : new[] { 67, 71, 74, 79 };
            var b = Buffer(big ? 2.6f : 1.3f);
            for (int i = 0; i < a.Length; i++) Tone(b, i * (big ? 0.09f : 0.07f), Midi(a[i]), 0.35f, 0.004f, big ? 0.7f : 0.4f, 1.6f, 1);
            if (big)
            {
                foreach (var n in new[] { 72, 76, 79, 84 }) Tone(b, 0.75f, Midi(n), 0.3f, 0.02f, 1.2f, 1.0f, 1);
                foreach (var n in new[] { 48, 55 }) Tone(b, 0.75f, Midi(n), 0.4f, 0.01f, 1.4f, 0.5f, 1);
            }
            Normalize(b, 0.8f);
            Fade(b, 0.1f);
            return b;
        }

        public static float[] Golden() => Arp(new[] { 88, 91, 95, 100, 103, 100, 107 }, 0.035f, 0.25f, 1.4f, 0.9f, 0.7f);
        public static float[] Ting() => Arp(new[] { 96, 103 }, 0.06f, 0.2f, 1.0f, 0.5f, 0.35f);
        public static float[] Achievement() => Arp(new[] { 76, 83, 88 }, 0.08f, 0.45f, 1.2f, 1.1f, 0.65f);
        public static float[] UIClick()
        {
            var b = Buffer(0.05f);
            Tone(b, 0, 1900, 0.4f, 0.001f, 0.008f);
            Normalize(b, 0.35f);
            Fade(b);
            return b;
        }

        public static float[] Clang()
        {
            var b = Buffer(0.7f);
            double[] fs = { 410, 967, 1433, 2210 };
            for (int k = 0; k < fs.Length; k++) Tone(b, 0, fs[k], 0.35f / (k + 1), 0.001f, 0.25f / (1 + k * 0.5f));
            var r = new Random(2);
            for (int i = 0; i < 1500; i++) b[i] += (float)(r.NextDouble() * 2 - 1) * 0.3f * (1 - i / 1500f);
            Normalize(b, 0.6f);
            Fade(b);
            return b;
        }

        public static float[] Squeak()
        {
            var b = Buffer(0.35f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                float chirp = t < 0.12f ? t / 0.12f : (t - 0.16f) / 0.12f;
                bool on = t < 0.12f || (t > 0.16f && t < 0.3f);
                double f = 2400 + 1800 * Math.Max(0, chirp);
                ph += f / Rate;
                b[i] = on ? Sin(ph) * 0.4f * (float)Math.Sin(Math.PI * Math.Min(1, (t < 0.12f ? t / 0.12f : (t - 0.16f) / 0.14f))) : 0;
            }
            Normalize(b, 0.4f);
            return b;
        }

        public static float[] Denied()
        {
            var b = Buffer(0.16f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                ph += 140.0 / Rate;
                b[i] = (ph % 1 < 0.5 ? 0.3f : -0.3f) * Env(t, 0.005f, 0.06f);
            }
            Normalize(b, 0.3f);
            Fade(b);
            return b;
        }

        public static float[] Rumble()
        {
            var b = Buffer(1.8f);
            var r = new Random(12);
            float lp = 0;
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                lp += ((float)(r.NextDouble() * 2 - 1) - lp) * 0.02f;
                ph += (55 - t * 12) / Rate;
                b[i] = (lp * 3f + Sin(ph) * 0.6f) * Env(t, 0.05f, 0.5f);
            }
            Tone(b, 0.15f, Midi(76), 0.25f, 0.005f, 0.6f, 1.2f, 3.5);
            Tone(b, 0.3f, Midi(83), 0.2f, 0.005f, 0.7f, 1.2f, 3.5);
            Normalize(b, 0.8f);
            Fade(b, 0.1f);
            return b;
        }

        public static float[] Siren()
        {
            var b = Buffer(1.2f);
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = i / (float)Rate;
                double f = 880 + 330 * Math.Sin(t * Math.PI * 4);
                ph += f / Rate;
                b[i] = Sin(ph) * 0.3f * Env(t, 0.02f, 0.6f);
            }
            Normalize(b, 0.45f);
            Fade(b, 0.05f);
            return b;
        }

        // ── music ──────────────────────────────────────────────────────────

        /// <summary>
        /// A 16-bar lo-fi loop: FM electric piano chords, soft bass, brushed hats, a pentatonic
        /// melody, tape wobble and a low-pass. mode picks the chord progression and groove.
        /// </summary>
        public static float[] Music(int root, float tempo, int mode, int seed, int rate)
        {
            var rng = new Random(seed);
            int[][] prog;   // chord tones as semitone offsets from root
            switch (mode)
            {
                case 1: prog = new[] { new[] { -12, 3, 7, 10, 14 }, new[] { -7, 4, 7, 10, 14 }, new[] { -12, 3, 7, 10, 14 }, new[] { -2, 2, 5, 9, 12 } }; break;
                case 2: prog = new[] { new[] { -12, 0, 3, 7, 12 }, new[] { -16, -4, 0, 3, 8 }, new[] { -9, 3, 7, 10, 15 }, new[] { -14, -2, 2, 5, 10 } }; break;
                case 3: prog = new[] { new[] { -12, 4, 7, 11, 14 }, new[] { -7, 4, 9, 12, 16 }, new[] { -8, 2, 7, 11, 14 }, new[] { -10, 5, 9, 12, 16 } }; break;
                case 4: prog = new[] { new[] { -12, 4, 7, 9, 14 }, new[] { -15, 0, 4, 7, 12 }, new[] { -10, 2, 5, 9, 12 }, new[] { -5, 2, 5, 7, 11 } }; break;
                case 5: prog = new[] { new[] { -12, 4, 7, 11, 14 }, new[] { -14, 2, 6, 9, 14 }, new[] { -12, 4, 7, 11, 16 }, new[] { -17, 2, 5, 9, 12 } }; break;
                default: prog = new[] { new[] { -12, 4, 7, 11, 14 }, new[] { -15, 0, 3, 7, 10 }, new[] { -10, 2, 5, 9, 12 }, new[] { -5, 2, 7, 9, 12 } }; break;
            }
            bool swing = mode == 4 || mode == 1;
            double beat = 60.0 / tempo;
            int bars = 16;
            double total = bars * 4 * beat;
            int n = (int)(total * rate);
            var outBuf = new float[n];
            int[] scale = { 0, 2, 4, 7, 9, 12, 14, 16 };
            if (mode == 2) scale = new[] { 0, 3, 5, 7, 10, 12, 15, 17 };
            if (mode == 1) scale = new[] { 0, 3, 5, 7, 9, 12, 15, 17 };

            void Note(double start, double dur, double freq, float amp, float fm, double ratio, float decay, float attack = 0.01f)
            {
                int s = (int)(start * rate);
                int e = Math.Min(n, s + (int)((dur + decay * 3) * rate));
                double pc = 0, pm = 0;
                for (int i = s; i < e; i++)
                {
                    float t = (i - s) / (float)rate;
                    float env = t < attack ? t / attack : (float)Math.Exp(-(t - attack) / decay);
                    if (t > dur) env *= (float)Math.Exp(-(t - dur) / 0.08);
                    pc += freq / rate;
                    pm += freq * ratio / rate;
                    float mod = fm * env * (float)Math.Sin(pm * 2 * Math.PI);
                    outBuf[i] += (float)Math.Sin((pc + mod * 0.2) * 2 * Math.PI) * amp * env;
                }
            }

            void Noise(double start, float amp, float decay, float hp)
            {
                int s = (int)(start * rate);
                int e = Math.Min(n, s + (int)(decay * 5 * rate));
                float lp = 0;
                for (int i = s; i < e; i++)
                {
                    float t = (i - s) / (float)rate;
                    float x = (float)(rng.NextDouble() * 2 - 1);
                    lp += (x - lp) * hp;
                    outBuf[i] += (x - lp) * amp * (float)Math.Exp(-t / decay);
                }
            }

            for (int bar = 0; bar < bars; bar++)
            {
                var chord = prog[(bar / 2) % prog.Length];
                double t0 = bar * 4 * beat;
                bool second = bar % 2 == 1;
                // electric piano: chord stabs (half notes with a little strum)
                for (int k = 1; k < chord.Length; k++)
                {
                    double f = Midi(root + chord[k]);
                    Note(t0 + k * 0.012, beat * 1.8, f, 0.05f, 1.6f, 1.0, 0.9f);
                    Note(t0 + 2 * beat + k * 0.012 + (second ? beat * 0.5 : 0), beat * 1.3, f, 0.035f, 1.2f, 1.0, 0.7f);
                }
                // bass
                double bf = Midi(root + chord[0] - 12);
                Note(t0, beat * 1.6, bf, 0.16f, 0.6f, 1.0, 0.6f, 0.005f);
                Note(t0 + 2.5 * beat, beat * 0.8, Midi(root + chord[0] - 12 + (second ? 7 : 0)), 0.12f, 0.6f, 1.0, 0.4f, 0.005f);
                // drums
                for (int b8 = 0; b8 < 8; b8++)
                {
                    double at = t0 + b8 * beat / 2 + (swing && b8 % 2 == 1 ? beat * 0.12 : 0);
                    Noise(at, b8 % 2 == 0 ? 0.035f : 0.022f, 0.025f, 0.6f);
                }
                Note(t0, 0.12, 60, 0.25f, 0.3f, 1.0, 0.09f, 0.002f);
                Note(t0 + 2 * beat, 0.12, 60, 0.2f, 0.3f, 1.0, 0.09f, 0.002f);
                Noise(t0 + beat, 0.06f, 0.06f, 0.15f);
                Noise(t0 + 3 * beat, 0.06f, 0.06f, 0.15f);
                // melody on the back half of the loop
                if (bar >= 4)
                {
                    for (int s = 0; s < 8; s++)
                    {
                        if (rng.NextDouble() < (bar >= 12 ? 0.45 : 0.3)) continue;
                        int deg = scale[rng.Next(scale.Length)];
                        double at = t0 + s * beat / 2 + (swing && s % 2 == 1 ? beat * 0.12 : 0);
                        Note(at, beat * 0.45, Midi(root + 12 + deg), 0.045f, 2.2f, 3.0, 0.35f, 0.005f);
                    }
                }
            }

            // tape wobble + low-pass + soft clip
            var wet = new float[n];
            float lpState = 0;
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate;
                double wob = 0.0035 * Math.Sin(t * 2 * Math.PI * 0.35) + 0.0012 * Math.Sin(t * 2 * Math.PI * 5.1);
                double src = i - wob * rate;
                int i0 = (int)Math.Floor(src);
                float frac = (float)(src - i0);
                float a = outBuf[((i0 % n) + n) % n], b2 = outBuf[(((i0 + 1) % n) + n) % n];
                float v = a + (b2 - a) * frac;
                lpState += (v - lpState) * 0.32f;
                wet[i] = (float)Math.Tanh(lpState * 1.6f) * 0.6f;
            }
            // seamless loop: short crossfade of the tail into the head
            int xf = rate / 5;
            for (int i = 0; i < xf; i++)
            {
                float k = i / (float)xf;
                wet[i] = wet[i] * k + wet[n - xf + i] * (1 - k);
            }
            var trimmed = new float[n - xf];
            Array.Copy(wet, trimmed, n - xf);
            return trimmed;
        }
    }
}
