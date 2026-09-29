// Build mode (hotbar 3): a translucent ghost of the selected buildable snaps to the floor grid
// where you aim. Green = valid, red = not (the reason shows in the prompt). Click to build; R
// rotates; rim machines turn to face the fountain on their own; dragging with a belt lays a
// line that bends round corners; X toggles demolish (full refund).
using System;
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class BuildMode
    {
        public bool Active { get; private set; }
        public bool Demolish { get; private set; }
        public BuildDef Selected { get; private set; }
        public int Rot;
        public bool HasSpot { get; private set; }
        public bool Valid { get; private set; }
        public string Reason { get; private set; }
        public int AX { get; private set; }
        public int AZ { get; private set; }
        public int ARot { get; private set; }
        public int HoverBuilding { get; private set; } = -1;
        public event Action<Building> Placed;
        public event Action<string> Denied;
        public event Action<Building> Removed;

        Sim sim;
        Transform ghostRoot;
        GameObject ghost;
        BuildDef ghostDef;
        Material ghostMat;
        bool dragging;
        int lastX, lastZ;
        Building lastBelt;
        static readonly Color Ok = new Color(0.35f, 1f, 0.55f, 0.32f), Bad = new Color(1f, 0.3f, 0.3f, 0.32f);

        public void Init(Sim s, Transform parent)
        {
            sim = s;
            ghostRoot = new GameObject("Build Ghost").transform;
            ghostRoot.SetParent(parent, false);
            ghostMat = Mats.NewGhost(Ok);
        }

        /// <summary>Is anything buildable yet? (No allocation: the HUD asks every frame.)</summary>
        public bool AnyUnlocked
        {
            get { foreach (var d in Content.Buildables) if (sim.BuildUnlocked(d)) return true; return false; }
        }

        public List<BuildDef> Unlocked()
        {
            var list = new List<BuildDef>();
            foreach (var d in Content.Buildables) if (sim.BuildUnlocked(d)) list.Add(d);
            return list;
        }

        public bool SetActive(bool on)
        {
            if (on && !AnyUnlocked) { Denied?.Invoke("Nothing to build yet (Terminal)"); return false; }
            Active = on;
            if (on && (Selected == null || !sim.BuildUnlocked(Selected))) Select(Unlocked()[0]);
            if (!on) { Demolish = false; dragging = false; HideGhost(); }
            return true;
        }

        public void Select(BuildDef d)
        {
            Selected = d;
            Demolish = false;
            if (!Active) Active = true;
        }

        void HideGhost() { if (ghost != null) ghost.SetActive(false); }

        void ShowGhost(BuildDef d, Vector3 pos, int rot, bool ok)
        {
            if (ghostDef != d)
            {
                if (ghost != null) UnityEngine.Object.Destroy(ghost);
                ghost = FactoryView.MakeModel(d, ghostRoot);
                foreach (var r in ghost.GetComponentsInChildren<MeshRenderer>())
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = ghostMat;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                ghostDef = d;
            }
            ghost.SetActive(true);
            ghost.transform.position = pos + Vector3.up * 0.02f;
            ghost.transform.rotation = Quaternion.Euler(0, rot * 90, 0);
            ghostMat.SetColor("_Color", ok ? Ok : Bad);
        }

        static int DirOf(int dx, int dz) => dz > 0 ? 0 : dx > 0 ? 1 : dz < 0 ? 2 : 3;

        /// <summary>Anchor cell so the footprint centre lands near the aimed point.</summary>
        static (int x, int z) AnchorFor(BuildDef d, Vector3 p, int rot)
        {
            int fx = Building.DX[rot], fz = Building.DZ[rot], rx = Building.DX[(rot + 1) & 3], rz = Building.DZ[(rot + 1) & 3];
            float ox = (rx * (d.W - 1) + fx * (d.D - 1)) * 0.5f, oz = (rz * (d.W - 1) + fz * (d.D - 1)) * 0.5f;
            return (Mathf.RoundToInt(p.x - 0.5f - ox), Mathf.RoundToInt(p.z - 0.5f - oz));
        }

        /// <summary>Where the aim ray meets the floor (not through walls), or false.</summary>
        static bool FloorPoint(Ray ray, float wallDist, out Vector3 p)
        {
            p = default;
            if (ray.direction.y > -0.05f) return false;
            float t = (0f - ray.origin.y) / ray.direction.y;
            if (t < 0.5f || t > 16f || t > wallDist + 0.6f) return false;
            p = ray.origin + ray.direction * t;
            return true;
        }

        public void Tick(FPInput input, Ray ray, float wallDist, int hoverUid, bool frozen)
        {
            HoverBuilding = hoverUid;
            HasSpot = false;
            if (!Active || frozen) { HideGhost(); if (frozen) dragging = false; return; }
            if (input.Demolish) { Demolish = !Demolish; dragging = false; }
            if (input.Cycle != 0 && !Demolish)
            {
                var list = Unlocked();
                int i = Mathf.Max(0, list.IndexOf(Selected));
                Select(list[((i + input.Cycle) % list.Count + list.Count) % list.Count]);
            }
            if (input.Rotate) Rot = (Rot + 1) & 3;

            if (Demolish)
            {
                HideGhost();
                if (input.PrimaryDown && hoverUid >= 0)
                {
                    var b = sim.FindBuilding(hoverUid);
                    if (b != null && sim.Remove(hoverUid)) Removed?.Invoke(b);
                }
                return;
            }
            var d = Selected;
            if (d == null || !FloorPoint(ray, wallDist, out var p)) { HideGhost(); if (!input.Primary) dragging = false; return; }

            int rot = d.RimOnly || d.MaxRange > 0 ? Sim.FacingRotation(p.x, p.z) : Rot;
            var (ax, az) = AnchorFor(d, p, rot);
            if (d.IsBelt && dragging) { ax = Mathf.FloorToInt(p.x); az = Mathf.FloorToInt(p.z); }
            AX = ax; AZ = az; ARot = rot;
            HasSpot = true;
            Valid = sim.CanPlace(d, ax, az, rot, out var why);
            Reason = why;
            var probe = new Building { Def = d, X = ax, Z = az, Rot = rot };
            var (cx, cz) = probe.Center;
            ShowGhost(d, new Vector3(cx, 0, cz), rot, Valid);

            if (d.IsBelt)
            {
                if (input.PrimaryDown)
                {
                    if (Valid) { lastBelt = sim.Place(d, ax, az, rot); if (lastBelt != null) Placed?.Invoke(lastBelt); }
                    else { lastBelt = sim.At(ax, az); if (lastBelt != null && !lastBelt.Def.IsBelt) lastBelt = null; if (lastBelt == null) Denied?.Invoke(why); }
                    dragging = true;
                    lastX = ax; lastZ = az;
                }
                else if (dragging && input.Primary)
                {
                    int tx = Mathf.FloorToInt(p.x), tz = Mathf.FloorToInt(p.z);
                    int guard = 0;
                    while ((lastX != tx || lastZ != tz) && guard++ < 24)
                    {
                        int dx = Math.Sign(tx - lastX), dz = dx != 0 ? 0 : Math.Sign(tz - lastZ);
                        int dir = DirOf(dx, dz);
                        if (lastBelt != null) sim.SetBeltRotation(lastBelt, dir);
                        int nx = lastX + dx, nz = lastZ + dz;
                        var existing = sim.At(nx, nz);
                        if (existing != null) lastBelt = existing.Def.IsBelt ? existing : null;
                        else if (sim.CanPlace(d, nx, nz, dir, out var why2)) { lastBelt = sim.Place(d, nx, nz, dir); if (lastBelt != null) Placed?.Invoke(lastBelt); }
                        else { Denied?.Invoke(why2); lastBelt = null; }
                        lastX = nx; lastZ = nz;
                        Rot = dir;
                    }
                }
                if (!input.Primary) dragging = false;
                return;
            }
            if (input.PrimaryDown)
            {
                if (!Valid) { Denied?.Invoke(why); return; }
                var b = sim.Place(d, ax, az, rot);
                if (b != null) Placed?.Invoke(b);
            }
        }
    }
}
