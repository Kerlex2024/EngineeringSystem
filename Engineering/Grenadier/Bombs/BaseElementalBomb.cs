// File: Scripts/Custom/Engineering/Grenadier/Bombs/Elemental/BaseElementalBomb.cs
#region References
using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;
#endregion

namespace Server.Custom.Engineering.Grenadier.Bombs.Elemental
{
    public enum ElementKind { Physical, Fire, Cold, Poison, Energy }

    /// <summary>
    /// Base for Elemental bombs: stronger than Mega, shared VN-FX (reflection) with ServUO fallback.
    /// Damage is not applied here; we only scale the Mega numbers. No double damage.
    /// </summary>
    public abstract class BaseElementalBomb : Server.Custom.Engineering.Grenadier.Bombs.BaseMegaBomb
    {
        // ---- ctor: must forward PotionEffect to BaseMegaBomb ----
        protected BaseElementalBomb(PotionEffect effect) : base(effect) { }
        protected BaseElementalBomb(Serial s) : base(s) { }

        // ---- Baseline numbers that represent "Mega" defaults ----
        // (We avoid calling abstract members from BaseMegaBomb.)
        protected virtual int BaseMinDamage => 100;
        protected virtual int BaseMaxDamage => 195;
        protected virtual int BaseExplosionRadius => 4;
        protected virtual TimeSpan BaseFuseDelay => TimeSpan.FromSeconds(1.5);
        protected virtual int BaseCooldownSeconds => 18;

        // ---- Elemental tuning knobs (override per child) ----
        public virtual ElementKind Element => ElementKind.Physical;
        public virtual double PowerMultiplier => 1.20; // +20% vs Mega by default
        public virtual int ExtraRadius => 0;           // +tiles to Mega radius
        public virtual bool FxEnabled => true;

        // ---- ServUO baseline visuals (fallback) ----
        protected virtual int SuoExplosionEffectID => 0x36BD;
        protected virtual int SuoExplosionSoundID  => 0x307;

        // ---- VitaNex "lava-like" art (can override per element) ----
        protected virtual int VnHitFlashEffectID => 14000;
        protected virtual int VnTrailEffectID    => 14089;
        protected virtual int VnHitSoundID       => 519;

        // ==== Effective numbers (scaled from baselines) ====
        public override int MinDamage => (int)Math.Round(BaseMinDamage * PowerMultiplier);
        public override int MaxDamage => (int)Math.Round(BaseMaxDamage * PowerMultiplier);

        public override int ExplosionRadius => BaseExplosionRadius + ExtraRadius;
        public override TimeSpan FuseDelay  => BaseFuseDelay;
        public override int CooldownSeconds => BaseCooldownSeconds;

        // Keep Mega falloff shape unless a child changes it.
        protected override double DamageFalloff(Point3D center, Point3D hit) => base.DamageFalloff(center, hit);

        /// <summary>Visuals only. Damage is applied by BaseMegaBomb.</summary>
        protected override void OnDetonate(Mobile from, Point3D loc, Map map)
        {
            if (!FxEnabled || map == null) return;

            if (!TrySendVitaNexFx(loc, map, ExplosionRadius, ExplosionHue))
            {
                SendServUOFx(loc, map, ExplosionRadius, ExplosionHue);
            }
        }

        // ---- ServUO visuals (safe everywhere) ----
        protected virtual void SendServUOFx(Point3D loc, Map map, int radius, int hue)
        {
            Effects.SendLocationEffect(loc, map, SuoExplosionEffectID, 15, hue);
            Effects.PlaySound(loc, map, SuoExplosionSoundID);

            for (int r = 1; r <= radius; r++)
            {
                var delay = TimeSpan.FromMilliseconds(40 * r);
                Timer.DelayCall(delay, () =>
                {
                    foreach (var p in GetRingTiles(loc, r, map))
                    {
                        if (!map.CanFit(p, 0, true, true)) continue;
                        Effects.SendLocationEffect(p, map, SuoExplosionEffectID, 10, hue);
                    }
                });
            }
        }

        // ---- VitaNex FX via reflection (no compile-time dependency) ----
        protected virtual bool TrySendVitaNexFx(Point3D center, Map map, int radius, int hue)
        {
            try
            {
                var effectInfoType = FindType("VitaNex.FX.EffectInfo");
                if (effectInfoType == null) return false;

                // EffectInfo(Point3D, Map, int effectId, int speed, int duration, int render)
                var ctor = effectInfoType.GetConstructor(new[]
                {
                    typeof(Point3D), typeof(Map), typeof(int), typeof(int), typeof(int), typeof(int)
                });
                if (ctor == null) return false;

                var propHue     = effectInfoType.GetProperty("Hue");
                var propSoundID = effectInfoType.GetProperty("SoundID");
                var methodSend  = effectInfoType.GetMethod("Send");
                if (propHue == null || methodSend == null) return false;

                // Center flash
                var flash = ctor.Invoke(new object[] { center, map, VnHitFlashEffectID, 0, 10, 30 });
                propHue.SetValue(flash, hue, null);
                if (propSoundID != null) propSoundID.SetValue(flash, VnHitSoundID, null);
                methodSend.Invoke(flash, null);

                // Rays: 5..10, 8-way stepping
                int rays     = Server.Utility.RandomMinMax(5, 10);
                double arc   = 360.0 / rays;
                int maxSteps = radius + 2;

                for (int i = 0; i < rays; i++)
                {
                    double angle = arc * i + Server.Utility.RandomMinMax(-10, 10);
                    var d = Snap8(angle);
                    var path = BuildRay(center, d.dx, d.dy, maxSteps, map);

                    for (int step = 0; step < path.Count; step++)
                    {
                        var p = path[step];
                        int perStepMs  = 25;
                        int rayStagger = 120 * i;

                        Timer.DelayCall(TimeSpan.FromMilliseconds(rayStagger + perStepMs * step), () =>
                        {
                            var puff = ctor.Invoke(new object[] { p, map, VnTrailEffectID, 0, 8, 15 });
                            propHue.SetValue(puff, hue, null);
                            methodSend.Invoke(puff, null);
                        });
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // ---- shared helpers ----
        protected static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        protected static IEnumerable<Point3D> GetRingTiles(Point3D c, int r, Map map)
        {
            yield return WithAvgZ(c.X + r, c.Y,     map);
            yield return WithAvgZ(c.X - r, c.Y,     map);
            yield return WithAvgZ(c.X,     c.Y + r, map);
            yield return WithAvgZ(c.X,     c.Y - r, map);
            yield return WithAvgZ(c.X + r, c.Y + r, map);
            yield return WithAvgZ(c.X + r, c.Y - r, map);
            yield return WithAvgZ(c.X - r, c.Y + r, map);
            yield return WithAvgZ(c.X - r, c.Y - r, map);
        }

        protected static (int dx, int dy) Snap8(double degrees)
        {
            double rad = degrees * Math.PI / 180.0;
            double x = Math.Cos(rad), y = Math.Sin(rad);
            int dx = x >  0.5 ? 1 : x < -0.5 ? -1 : 0;
            int dy = y >  0.5 ? 1 : y < -0.5 ? -1 : 0;
            if (dx == 0 && dy == 0) dx = 1;
            return (dx, dy);
        }

        protected static List<Point3D> BuildRay(Point3D start, int dx, int dy, int steps, Map map)
        {
            var list = new List<Point3D>(steps);
            for (int i = 1; i <= steps; i++)
            {
                int nx = start.X + dx * i;
                int ny = start.Y + dy * i;
                int nz = map.GetAverageZ(nx, ny);
                var p = new Point3D(nx, ny, nz);
                if (!map.CanFit(p, 0, true, true)) break;
                list.Add(p);
            }
            return list;
        }

        protected static Point3D WithAvgZ(int x, int y, Map map) => new Point3D(x, y, map.GetAverageZ(x, y));
    }
}