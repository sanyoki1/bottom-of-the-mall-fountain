// Particle bursts (dust, sparks, glints, coin showers, bubbles) plus pooled "flyers":
// loot that arcs from one point to another (crust → pocket, wishes → compressor, ...).
using System;
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class FX
    {
        ParticleSystem dust, sparks, glints, coins, bubbles, confetti;
        Transform root;
        readonly List<Flyer> flyers = new List<Flyer>();
        readonly Stack<Flyer> flyerPool = new Stack<Flyer>();
        readonly List<RippleFx> ripples = new List<RippleFx>();
        readonly Stack<RippleFx> ripplePool = new Stack<RippleFx>();
        Material rippleMat;
        public float Quality = 1f;

        sealed class Flyer
        {
            public GameObject Go;
            public MeshFilter Mf;
            public MeshRenderer Mr;
            public Vector3 From, To, Ctrl, Spin;
            public float T, Dur, Scale;
            public Action Done;
            public Func<Vector3> Target;
        }

        sealed class RippleFx
        {
            public Transform T;
            public MeshRenderer R;
            public MaterialPropertyBlock Mpb;
            public float Age, Life, Size;
            public Color Col;
        }

        public void Init(Transform parent)
        {
            root = new GameObject("FX").transform;
            root.SetParent(parent, false);
            var dustMat = Mats.NewSoft(TexKit.SoftDot);
            dust = MakePS("Dust", dustMat, false, null, 0.4f, 3000);
            var sz = dust.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 0.5f, 1, 1.6f));
            Fade(dust, 0.7f);

            sparks = MakePS("Sparks", Mats.NewGlow(TexKit.SoftDot, 2.4f, Color.white), false, null, 1.2f, 3000);
            Fade(sparks, 1f);
            glints = MakePS("Glints", Mats.NewGlow(TexKit.Sparkle, 3.2f, Color.white), false, null, 0f, 1500);
            var gsz = glints.sizeOverLifetime;
            gsz.enabled = true;
            gsz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.3f, 1), new Keyframe(1, 0)));
            var gr = glints.rotationOverLifetime;
            gr.enabled = true;
            gr.z = new ParticleSystem.MinMaxCurve(1.5f);

            var coinMesh = new MeshKit().Cylinder(Vector3.zero, 0.12f, 0.024f, 12, Color.white).ToMesh("particle coin");
            coins = MakePS("Coin Shower", Loot.LitNoEmit, true, coinMesh, 1.6f, 1500);
            Fade(coins, 0.85f, true);

            bubbles = MakePS("Bubbles", Mats.NewGlow(TexKit.Ring, 1.4f, Color.white), false, null, -0.25f, 1500);
            Fade(bubbles, 1f);
            confetti = MakePS("Confetti", Loot.LitNoEmit, true, new MeshKit().Box(Vector3.zero, new Vector3(0.12f, 0.01f, 0.07f), Color.white).ToMesh("confetti"), 0.35f, 1500);
            Fade(confetti, 0.9f, true);

            rippleMat = Mats.NewGlow(TexKit.Ring, 2.2f, Color.white);
        }

        ParticleSystem MakePS(string name, Material mat, bool mesh, Mesh m, float gravity, int max)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startSpeed = 0;
            main.startLifetime = 1;
            main.duration = 1;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (mesh)
            {
                r.renderMode = ParticleSystemRenderMode.Mesh;
                r.mesh = m;
                r.alignment = ParticleSystemRenderSpace.Local;
                main.startRotation3D = true;
                var rol = ps.rotationOverLifetime;
                rol.enabled = true;
                rol.separateAxes = true;
                rol.x = new ParticleSystem.MinMaxCurve(-12f, 12f);
                rol.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
                rol.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
            }
            else
            {
                r.renderMode = ParticleSystemRenderMode.Billboard;
                r.sortMode = ParticleSystemSortMode.None;
            }
            ps.Play();
            return ps;
        }

        static void Fade(ParticleSystem ps, float startAlpha, bool late = false)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                      late ? new[] { new GradientAlphaKey(startAlpha, 0), new GradientAlphaKey(startAlpha, 0.75f), new GradientAlphaKey(0, 1) }
                           : new[] { new GradientAlphaKey(startAlpha, 0), new GradientAlphaKey(startAlpha * 0.7f, 0.4f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(g);
        }

        int Q(int n) => Mathf.Max(1, Mathf.RoundToInt(n * Quality));

        static Vector3 Rand(float s) => UnityEngine.Random.insideUnitSphere * s;

        public void Dust(Vector3 pos, Color col, int count, float size = 0.8f, float spread = 0.6f, float up = 1.2f)
        {
            count = Q(count);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Rand(spread * 0.5f);
                ep.velocity = new Vector3(UnityEngine.Random.Range(-1f, 1f) * spread * 1.4f, UnityEngine.Random.Range(0.4f, 1f) * up, UnityEngine.Random.Range(-1f, 1f) * spread * 1.4f);
                ep.startSize = size * UnityEngine.Random.Range(0.6f, 1.3f);
                ep.startLifetime = UnityEngine.Random.Range(0.6f, 1.2f);
                var c = col;
                c.a = UnityEngine.Random.Range(0.35f, 0.7f);
                ep.startColor = c;
                ep.rotation = UnityEngine.Random.Range(0, 360);
                dust.Emit(ep, 1);
            }
        }

        public void Sparks(Vector3 pos, Color col, int count, float speed = 4f, float size = 0.12f, float life = 0.6f)
        {
            count = Q(count);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos;
                var d = UnityEngine.Random.onUnitSphere;
                d.y = Mathf.Abs(d.y) * 1.2f + 0.2f;
                ep.velocity = d * speed * UnityEngine.Random.Range(0.4f, 1f);
                ep.startSize = size * UnityEngine.Random.Range(0.6f, 1.4f);
                ep.startLifetime = life * UnityEngine.Random.Range(0.6f, 1.3f);
                ep.startColor = col;
                sparks.Emit(ep, 1);
            }
        }

        public void Glint(Vector3 pos, Color col, float size = 0.9f, int count = 1, float spread = 0.2f)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Rand(spread);
                ep.velocity = Vector3.zero;
                ep.startSize = size * UnityEngine.Random.Range(0.7f, 1.2f);
                ep.startLifetime = UnityEngine.Random.Range(0.4f, 0.8f);
                ep.startColor = col;
                ep.rotation = UnityEngine.Random.Range(0, 90);
                glints.Emit(ep, 1);
            }
        }

        public void CoinShower(Vector3 pos, Color[] colors, int count, float power = 5f, float size = 1f)
        {
            count = Q(count);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Rand(0.2f);
                var d = UnityEngine.Random.insideUnitCircle;
                ep.velocity = new Vector3(d.x * power * 0.45f, UnityEngine.Random.Range(0.6f, 1f) * power, d.y * power * 0.45f);
                ep.startSize = size * UnityEngine.Random.Range(0.8f, 1.25f);
                ep.startLifetime = UnityEngine.Random.Range(0.7f, 1.2f);
                ep.startColor = colors[UnityEngine.Random.Range(0, colors.Length)];
                ep.rotation3D = new Vector3(UnityEngine.Random.Range(0, 360), UnityEngine.Random.Range(0, 360), 0);
                coins.Emit(ep, 1);
            }
        }

        public void Confetti(Vector3 pos, int count, float power = 7f)
        {
            count = Q(count);
            var ep = new ParticleSystem.EmitParams();
            Color[] cs = { new Color(1, 0.35f, 0.5f), new Color(0.2f, 0.9f, 0.8f), new Color(1, 0.85f, 0.2f), new Color(0.55f, 0.45f, 1f), Color.white };
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + Rand(0.4f);
                var d = UnityEngine.Random.insideUnitCircle;
                ep.velocity = new Vector3(d.x * power * 0.6f, UnityEngine.Random.Range(0.5f, 1f) * power, d.y * power * 0.6f);
                ep.startSize = UnityEngine.Random.Range(0.8f, 1.4f);
                ep.startLifetime = UnityEngine.Random.Range(1.4f, 2.4f);
                ep.startColor = cs[UnityEngine.Random.Range(0, cs.Length)];
                ep.rotation3D = new Vector3(UnityEngine.Random.Range(0, 360), UnityEngine.Random.Range(0, 360), UnityEngine.Random.Range(0, 360));
                confetti.Emit(ep, 1);
            }
        }

        public void Bubbles(Vector3 pos, Color col, int count, float spread = 0.5f, float size = 0.18f)
        {
            count = Q(count);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                ep.position = pos + new Vector3(UnityEngine.Random.Range(-spread, spread), 0, UnityEngine.Random.Range(-spread, spread));
                ep.velocity = new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), UnityEngine.Random.Range(0.4f, 1.1f), UnityEngine.Random.Range(-0.2f, 0.2f));
                ep.startSize = size * UnityEngine.Random.Range(0.5f, 1.4f);
                ep.startLifetime = UnityEngine.Random.Range(0.8f, 1.6f);
                ep.startColor = col;
                bubbles.Emit(ep, 1);
            }
        }

        public void Ripple(Vector3 pos, Color col, float size = 1.6f, float life = 0.45f)
        {
            var r = ripplePool.Count > 0 ? ripplePool.Pop() : NewRipple();
            r.T.gameObject.SetActive(true);
            r.T.position = pos + Vector3.up * 0.06f;
            r.Age = 0;
            r.Life = life;
            r.Size = size;
            r.Col = col;
            ripples.Add(r);
        }

        RippleFx NewRipple()
        {
            var go = new GameObject("ripple");
            go.transform.SetParent(root, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rippleMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return new RippleFx { T = go.transform, R = mr, Mpb = new MaterialPropertyBlock() };
        }

        static Mesh quad;
        public static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "flat quad" };
                quad.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
                quad.colors = new[] { Color.white, Color.white, Color.white, Color.white };
                quad.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                quad.RecalculateNormals();
                quad.RecalculateBounds();
                return quad;
            }
        }

        /// <summary>Loot arcing from one point to another; target may move (e.g. a pile).</summary>
        public void Fly(Vector3 from, Func<Vector3> target, Mesh mesh, float scale, float duration, float arc, Action done = null, bool gloss = true)
        {
            var f = flyerPool.Count > 0 ? flyerPool.Pop() : NewFlyer();
            f.Go.SetActive(true);
            f.Mf.sharedMesh = mesh;
            f.Mr.sharedMaterials = mesh.subMeshCount > 1 ? new[] { Mats.Lit, Mats.Gloss } : new[] { Mats.Lit };
            f.From = from;
            f.Target = target;
            f.To = target();
            f.Ctrl = (from + f.To) * 0.5f + Vector3.up * arc;
            f.T = 0;
            f.Dur = duration;
            f.Scale = scale;
            f.Spin = UnityEngine.Random.insideUnitSphere * 720;
            f.Done = done;
            f.Go.transform.position = from;
            f.Go.transform.localScale = Vector3.one * scale;
            flyers.Add(f);
        }

        Flyer NewFlyer()
        {
            var go = new GameObject("flyer");
            go.transform.SetParent(root, false);
            var f = new Flyer { Go = go, Mf = go.AddComponent<MeshFilter>(), Mr = go.AddComponent<MeshRenderer>() };
            f.Mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return f;
        }

        public int FlyerCount => flyers.Count;

        public void Update(float dt)
        {
            for (int i = flyers.Count - 1; i >= 0; i--)
            {
                var f = flyers[i];
                f.T += dt / f.Dur;
                if (f.Target != null) f.To = f.Target();
                float t = Mathf.Clamp01(f.T);
                float e = t * t * (3 - 2 * t) * 0.35f + t * 0.65f;
                Vector3 a = Vector3.Lerp(f.From, f.Ctrl, e), b = Vector3.Lerp(f.Ctrl, f.To, e);
                f.Go.transform.position = Vector3.Lerp(a, b, e);
                f.Go.transform.Rotate(f.Spin * dt, Space.Self);
                float s = f.Scale * (t > 0.85f ? Mathf.Lerp(1, 0.3f, (t - 0.85f) / 0.15f) : 1);
                f.Go.transform.localScale = Vector3.one * s;
                if (f.T >= 1)
                {
                    f.Done?.Invoke();
                    f.Go.SetActive(false);
                    flyers.RemoveAt(i);
                    flyerPool.Push(f);
                }
            }
            for (int i = ripples.Count - 1; i >= 0; i--)
            {
                var r = ripples[i];
                r.Age += dt;
                float t = r.Age / r.Life;
                if (t >= 1)
                {
                    r.T.gameObject.SetActive(false);
                    ripples.RemoveAt(i);
                    ripplePool.Push(r);
                    continue;
                }
                r.T.localScale = Vector3.one * Mathf.Lerp(0.2f, r.Size, 1 - (1 - t) * (1 - t));
                var c = r.Col;
                c.a *= 1 - t;
                r.Mpb.SetColor("_Color", c);
                r.R.SetPropertyBlock(r.Mpb);
            }
        }
    }
}
