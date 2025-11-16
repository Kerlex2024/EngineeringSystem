// ============================================================================
// Path: Scripts/Custom/Engineering/Grenadier/FX/CataclysmFX.cs
// VN-ONLY (robust): Fire explode + Fire wave + secondary VN detonations + lava pools + damage falloff.
// - Uses reflection with multiple overload attempts (no silent failures).
// - Logs any VN failure to console and (if source!=null) to source via SendMessage.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
using VitaNex.FX;

namespace Server
{
    public enum FXPerfPreset { Low, Medium, High, Ultra }

    public static class CataclysmFX
    {
        public static void SendVN(
            Map map, Point3D center, int hue, int radius, FXPerfPreset preset,
            int speed, int repeat, bool reverse,
            int centerDamage, int edgeDamage,
            int subExplosions, int subMinDamage, int subMaxDamage, int subAoeRadius,
            Mobile source,
            FXLayers layers,                // NEW
            int waveDirs,                   // NEW: 1, 2 or 4
            int poolDensityPct              // NEW: 0–200
        )
        {
            if (map == null || map == Map.Internal) return;

            radius = Math.Max(2, radius);
            int s = Math.Max(1, Math.Min(10, speed));
            var interval = TimeSpan.FromMilliseconds(1000 - ((s - 1) * 100));
            poolDensityPct = Math.Max(0, Math.Min(200, poolDensityPct));
            subAoeRadius = Math.Max(2, Math.Min(3, subAoeRadius));
            waveDirs = Math.Max(1, Math.Min(4, waveDirs));

            // 1) Primary explode (VN)
            if (layers.HasFlag(FXLayers.Primary))
            {
                if (!TryExplodeVN(center, map, radius, repeat, interval, hue, reverse, out var err1))
                    Report(source, "[VN] Explode failed: " + err1);
            }

            // 2) Fire wave (VN)
            if (layers.HasFlag(FXLayers.Waves))
            {
                if (!TryWaveVN(center, map, hue, radius + 1, interval, out var err2))
                    Report(source, "[VN] Wave failed: " + err2);
            }

            // 3) Damage falloff (impact->edge) – always apply once
            try { DamageFalloff(map, center, radius, centerDamage, edgeDamage, source, hue); }
            catch (Exception ex) { Report(source, "[VN] Falloff error: " + ex.Message); }

            // 4) Lava pools (center + scattered)
            if (layers.HasFlag(FXLayers.Pools))
            {
                try { DropLavaPools(map, center, hue, radius, poolDensityPct); }  // << pass densityPct
                catch (Exception ex) { Report(source, "[VN] Lava error: " + ex.Message); }
            }

            // 5) Secondary detonations (VN mini-explodes) with AoE 2–3 tiles
            if (layers.HasFlag(FXLayers.Subs) && subExplosions > 0)
            {
                // energyFlicker is controlled by the mask
                bool energyFlicker = layers.HasFlag(FXLayers.EnergyFlicker);
                try
                {
                    TrySecondaryVN(
                        map, center, hue, Math.Max(1, radius - 1),
                        subExplosions,
                        Math.Min(subMinDamage, subMaxDamage), Math.Max(subMinDamage, subMaxDamage),
                        subAoeRadius, source, interval, energyFlicker);           // << NO 'out' arg
                }
                catch (Exception ex) { Report(source, "[VN] Secondary dets error: " + ex.Message); }
            }
        }

        
        // VN Secondary mini-explosions + AoE damage
        private static bool TrySecondaryVN(
            Map map, Point3D c, int hue, int radius, int count,
            int minDmg, int maxDmg, int aoe, Mobile src, TimeSpan interval, bool energyFlicker)
        {
            try
            {
                count = Math.Max(0, count);
                for (int i = 0; i < count; i++)
                {
                    double a = Utility.RandomDouble() * Math.PI * 2.0;
                    int dist = Utility.RandomMinMax(1, radius);
                    var at = new Point3D(
                        c.X + (int)Math.Round(Math.Cos(a) * dist),
                        c.Y + (int)Math.Round(Math.Sin(a) * dist),
                        c.Z);

                    Timer.DelayCall(TimeSpan.FromMilliseconds(Utility.RandomMinMax(80, 300)), () =>
                    {
                        try
                        {
                            var sub = ExplodeFX.Fire.CreateInstance(
                                at, map, 2, 0, (TimeSpan?)interval,
                                e => { if (e != null) e.Hue = hue; }, null);
                            sub?.Send();

                            if (energyFlicker)
                            {
                                var flick = ExplodeFX.Energy.CreateInstance(at, map, 1, 0, (TimeSpan?)interval, null, null);
                                flick?.Send();
                            }
                        }
                        catch { }

                        foreach (Mobile m in map.GetMobilesInRange(at, aoe))
                        {
                            if (m == null || m.Deleted || !m.Alive) continue;
                            if (m.AccessLevel > AccessLevel.Player) continue;

                            src?.DoHarmful(m);
                            AOS.Damage(m, src, Utility.RandomMinMax(minDmg, maxDmg), 0, 100, 0, 0, 0);
                            try { m.FixedParticles(0x3709, 6, 12, 5052, hue, 0, EffectLayer.Waist); m.PlaySound(0x208); } catch { }
                        }
                    });
                }
                return true;
            }
            catch { return false; }
        }
    



        // Damage falloff (impact -> edge)
        private static void DamageFalloff(Map map, Point3D c, int radius, int dmgCenter, int dmgEdge, Mobile src, int hue)
        {
            int r = Math.Max(1, radius);
            foreach (Mobile m in map.GetMobilesInRange(c, r))
            {
                if (!ValidTarget(m)) continue;

                int dx = Math.Abs(m.X - c.X);
                int dy = Math.Abs(m.Y - c.Y);
                int d = Math.Min(Math.Max(dx, dy), r);
                int dmg = Lerp(dmgCenter, dmgEdge, (double)d / r);
                if (dmg <= 0) continue;

                src?.DoHarmful(m);
                AOS.Damage(m, src, dmg, 0, 100, 0, 0, 0);
                try { m.FixedParticles(0x3709, 8, 18, 5052, hue, 0, EffectLayer.Waist); m.PlaySound(0x208); } catch { }
            }
        }

        private static bool ValidTarget(Mobile m)
        {
            if (m == null || m.Deleted || !m.Alive) return false;
            if (m.AccessLevel > AccessLevel.Player) return false;
            return true;
        }

        private static int Lerp(int a, int b, double t)
        {
            if (t < 0) t = 0; if (t > 1) t = 1;
            return a + (int)Math.Round((b - a) * t);
        }

        private static void DropLavaPools(Map map, Point3D c, int hue, int radius, int densityPct)
        {
            int cz = map.GetAverageZ(c.X, c.Y);
            var centerGround = new Point3D(c.X, c.Y, cz);
            var centerPool = new LavaPool(hue, TimeSpan.FromSeconds(7.0), TimeSpan.FromMilliseconds(750), 8) { Hue = hue };
            centerPool.MoveToWorld(centerGround, map);

            int basePools = Math.Max(3, radius / 3);
            int pools = (int)Math.Round(basePools * (densityPct / 100.0));

            for (int i = 0; i < pools; i++)
            {
                double ang = Utility.RandomDouble() * Math.PI * 2.0;
                int dist = Utility.RandomMinMax(1, Math.Max(1, radius / 2));
                int x = c.X + (int)Math.Round(Math.Cos(ang) * dist);
                int y = c.Y + (int)Math.Round(Math.Sin(ang) * dist);
                int z = map.GetAverageZ(x, y);

                var at = new Point3D(x, y, z);
                var lp = new LavaPool(hue, TimeSpan.FromSeconds(5.0), TimeSpan.FromMilliseconds(900), 6) { Hue = hue };
                lp.MoveToWorld(at, map);
            }
        }
        // ---------------- Reflection utils -----------------------------------

        // ---- Universal VN factory/ctor resolver ----
        private static object VNCreateAny(string[] nestedTypeNames, string[] factoryNames, object[] args)
        {
            foreach (var tn in nestedTypeNames)
            {
                var t = FindType(tn);
                if (t == null) continue;

                // Try factories first
                foreach (var fname in factoryNames)
                {
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (m.Name != fname) continue;
                        var ps = m.GetParameters();
                        if (ps.Length != args.Length) continue;
                        try { return m.Invoke(null, args); } catch { }
                    }
                }

                // Try public ctors with matching arity
                foreach (var ctor in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                {
                    var ps = ctor.GetParameters();
                    if (ps.Length != args.Length) continue;
                    try { return ctor.Invoke(args); } catch { }
                }
            }
            return null;
        }

        // Minimal CreateInstance resolver (kept for internal calls)
        private static object VNCreate(string nestedTypeName, object[] args)
        {
            var t = FindType(nestedTypeName);
            if (t == null) return null;

            foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (m.Name != "CreateInstance") continue;
                try
                {
                    if (m.GetParameters().Length == args.Length)
                        return m.Invoke(null, args); // null fine for delegate args
                }
                catch { /* try next overload */ }
            }
            return null;
        }

        // Try to set Hue either directly or on nested Info.Hue
        private static void VNTrySetHue(object fx, int hue)
        {
            if (fx == null) return;
            if (!VNTrySet(fx, "Hue", hue))
            {
                var info = VNTryGet(fx, "Info") ?? VNTryGet(VNTryGet(fx, "Effect"), "Info");
                if (info != null) VNTrySet(info, "Hue", hue);
            }
        }

        // SINGLE definitive version (keep this one)
        private static bool VNTrySet(object obj, string prop, object value)
        {
            if (obj == null) return false;
            try
            {
                var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (pi != null && pi.CanWrite) { pi.SetValue(obj, value, null); return true; }
            }
            catch { }
            return false;
        }

        private static object VNTryGet(object obj, string prop)
        {
            if (obj == null) return null;
            try
            {
                var pi = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
                if (pi != null) return pi.GetValue(obj, null);
            }
            catch { }
            return null;
        }

        private static void VNSend(object fx)
        {
            if (fx == null) return;
            try { fx.GetType().GetMethod("Send", BindingFlags.Public | BindingFlags.Instance)?.Invoke(fx, null); } catch { }
        }

        private static Type FindType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { var t = a.GetType(fullName); if (t != null) return t; } catch { }
            }
            return null;
        }

        private static void Report(Mobile to, string msg)
        {
            Console.WriteLine(msg);
            try { to?.SendMessage(33, msg); } catch { }
        }

        // Back-compat (old signature)
        public static void Send(Map map, Point3D center, int hue, int requestedRadius, FXPerfPreset preset, int vnSpeed, int vnRepeat, bool vnReverse)
        {
            SendVN(map, center, hue, requestedRadius, preset, vnSpeed, vnRepeat, vnReverse,
                60, 20, 3, 18, 26, 2, null);
        }
    } 
}       