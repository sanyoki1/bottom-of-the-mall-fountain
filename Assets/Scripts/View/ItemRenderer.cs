// Draws every loose item in the fountain with GPU instancing (one batch per item type and
// submesh) straight from Sim.Loose, and answers "what is under the crosshair" queries.
// Items have no GameObjects or colliders: positions come from the Core data plus the crust height.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class ItemRenderer
    {
        sealed class Batch
        {
            public Mesh Mesh;
            public Material[] Mats;
            public float Scale, Lift;
            public Matrix4x4[] M = new Matrix4x4[64];
            public int Count;
        }

        readonly Dictionary<int, Batch> batches = new Dictionary<int, Batch>();
        Sim sim;
        FountainView fountain;
        Material glowMat;
        Mesh quad;
        public int HighlightUid = -1;
        public readonly HashSet<int> AreaHighlight = new HashSet<int>();
        public int Drawn { get; private set; }

        public void Init(Sim s, FountainView f)
        {
            sim = s;
            fountain = f;
            glowMat = Mats.NewGlow(TexKit.SoftDot, 1.8f, new Color(1f, 0.95f, 0.6f));
            quad = FX.Quad;
        }

        Batch For(int type)
        {
            if (batches.TryGetValue(type, out var b)) return b;
            var t = Content.Items[type];
            var mesh = Loot.Tinted(t.Shape, MeshKit.Hex(t.Color));
            b = new Batch
            {
                Mesh = mesh,
                Mats = mesh.subMeshCount > 1 ? new[] { Mats.Lit, Mats.Gloss } : new[] { Mats.Lit },
                Scale = t.Scale,
                Lift = -mesh.bounds.min.y * t.Scale,
            };
            batches[type] = b;
            return b;
        }

        /// <summary>World position of a loose item right now (arc, sinking or resting).</summary>
        public Vector3 PositionOf(LooseItem it)
        {
            var b = For(it.Type);
            float rest = fountain.HeightAt(it.X, it.Z) + b.Lift;
            switch (it.State)
            {
                case LooseState.Airborne:
                {
                    float t = Mathf.Clamp01(it.Timer / (float)Balance.TossFlight);
                    float water = fountain.WaterY;
                    Vector3 a = new Vector3(it.FromX, it.FromY, it.FromZ), e = new Vector3(it.X, water, it.Z);
                    Vector3 p = Vector3.Lerp(a, e, t);
                    p.y += Mathf.Sin(t * Mathf.PI) * (1.2f + Vector3.Distance(a, e) * 0.18f);
                    return p;
                }
                case LooseState.Sinking:
                {
                    float t = Mathf.Clamp01(it.Timer / (float)Balance.SinkTime);
                    float water = Mathf.Max(fountain.WaterY, rest);
                    float e = 1 - (1 - t) * (1 - t);
                    return new Vector3(it.X + Mathf.Sin(t * 9 + it.Uid) * 0.04f * (1 - t), Mathf.Lerp(water, rest, e), it.Z);
                }
                default:
                    return new Vector3(it.X, rest, it.Z);
            }
        }

        Quaternion RotationOf(LooseItem it, float time)
        {
            float tilt = ((it.Uid * 37) % 23 - 11) * 0.9f;
            if (it.State == LooseState.Airborne) return Quaternion.Euler(time * 540 + it.Uid, it.Yaw, time * 300);
            if (it.State == LooseState.Sinking)
            {
                float t = Mathf.Clamp01(it.Timer / (float)Balance.SinkTime);
                return Quaternion.Euler(Mathf.Lerp(70, tilt, t), it.Yaw + t * 90, Mathf.Sin(t * 12) * 20 * (1 - t));
            }
            return Quaternion.Euler(tilt, it.Yaw, tilt * 0.4f);
        }

        /// <summary>Queue an item that isn't loose in the fountain (on a belt, in a machine) for this frame's draw.</summary>
        public void AddExtra(int type, Vector3 pos, Quaternion rot)
        {
            var b = For(type);
            if (b.Count == b.M.Length) System.Array.Resize(ref b.M, b.M.Length * 2);
            b.M[b.Count++] = Matrix4x4.TRS(pos + Vector3.up * b.Lift, rot, Vector3.one * b.Scale);
        }

        public void Render(Camera cam, float time, System.Action<ItemRenderer> extra = null)
        {
            foreach (var b in batches.Values) b.Count = 0;
            extra?.Invoke(this);
            var loose = sim.Loose;
            Vector3 camPos = cam.transform.position;
            Drawn = 0;
            for (int i = 0; i < loose.Count; i++)
            {
                var it = loose[i];
                var b = For(it.Type);
                var p = PositionOf(it);
                // tiny items far away aren't worth drawing
                float d2 = (p - camPos).sqrMagnitude;
                if (b.Scale < 1.2f && d2 > 60f * 60f) continue;
                if (b.Count == b.M.Length) System.Array.Resize(ref b.M, b.M.Length * 2);
                b.M[b.Count++] = Matrix4x4.TRS(p, RotationOf(it, time), Vector3.one * b.Scale);
                Drawn++;
            }
            foreach (var b in batches.Values)
            {
                if (b.Count == 0) continue;
                for (int s = 0; s < b.Mesh.subMeshCount && s < b.Mats.Length; s++)
                {
                    var rp = new RenderParams(b.Mats[s]) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true };
                    for (int start = 0; start < b.Count; start += 1023)
                        Graphics.RenderMeshInstanced(rp, b.Mesh, s, b.M, Mathf.Min(1023, b.Count - start), start);
                }
            }

            // highlight: a soft glow behind whatever the crosshair picks
            Quaternion face = cam.transform.rotation * Quaternion.Euler(-90, 0, 0);
            void Glow(LooseItem it, float size)
            {
                var p = PositionOf(it);
                var rp = new RenderParams(glowMat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
                float pulse = 1 + Mathf.Sin(time * 8) * 0.1f;
                Graphics.RenderMesh(rp, quad, 0, Matrix4x4.TRS(p + (camPos - p).normalized * 0.05f, face, Vector3.one * size * pulse));
            }
            if (HighlightUid >= 0)
            {
                var it = sim.FindLoose(HighlightUid);
                if (it != null) Glow(it, 0.28f + For(it.Type).Scale * 0.15f);
            }
            foreach (int u in AreaHighlight)
            {
                var it = sim.FindLoose(u);
                if (it != null) Glow(it, 0.2f);
            }
        }

        /// <summary>
        /// The loose item closest to the aim ray within reach, or null. maxDist lets walls and the
        /// rim hide items behind them.
        /// </summary>
        public LooseItem PickNearest(Ray ray, float reach, float maxDist, float slack = 0.06f)
        {
            LooseItem best = null;
            float bestScore = float.MaxValue;
            var loose = sim.Loose;
            for (int i = 0; i < loose.Count; i++)
            {
                var it = loose[i];
                if (it.State == LooseState.Airborne) continue;
                Vector3 p = PositionOf(it);
                Vector3 v = p - ray.origin;
                float t = Vector3.Dot(v, ray.direction);
                if (t < 0.2f || t > reach || t > maxDist + 0.35f) continue;
                float d = (v - ray.direction * t).magnitude;
                float allow = slack + For(it.Type).Scale * 0.09f + t * 0.012f;
                if (d > allow) continue;
                float score = d / allow + t * 0.05f;
                if (score < bestScore) { bestScore = score; best = it; }
            }
            return best;
        }

        /// <summary>Every item within radius (xz) of a point and within reach of the eye.</summary>
        public void PickArea(Vector3 center, float radius, Vector3 eye, float reach, List<int> into, int max)
        {
            into.Clear();
            var loose = sim.Loose;
            float r2 = radius * radius, reach2 = (reach + radius) * (reach + radius);
            for (int i = 0; i < loose.Count && into.Count < max; i++)
            {
                var it = loose[i];
                if (it.State == LooseState.Airborne) continue;
                float dx = it.X - center.x, dz = it.Z - center.z;
                if (dx * dx + dz * dz > r2) continue;
                Vector3 p = PositionOf(it);
                if (Mathf.Abs(p.y - center.y) > 1.2f || (p - eye).sqrMagnitude > reach2) continue;
                into.Add(it.Uid);
            }
        }
    }
}
