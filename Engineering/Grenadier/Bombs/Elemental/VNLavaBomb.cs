// ============================================================================
// Path: Scripts/Custom/Engineering/Grenadier/Bombs/Elemental/VNLavaBomb.cs
// VN-only Lava Bomb. Calls CataclysmFX.SendVN with wave + subs + falloff + lava.
// ============================================================================
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using VitaNex;
using VitaNex.FX;

namespace Server.Items
{
    [Flipable(0xF0D, 0xF0E)]
    public partial class VNLavaBomb : Item
    {
        // NEW GM knobs
        [CommandProperty(AccessLevel.GameMaster)] public FXLayers EffectsMask { get; set; } = FXLayers.All;
        [CommandProperty(AccessLevel.GameMaster)] public int WaveDirs { get; set; } = 4;          // 1, 2, 4
        [CommandProperty(AccessLevel.GameMaster)] public int PoolDensityPct { get; set; } = 100; // 0–200

        [CommandProperty(AccessLevel.GameMaster)] public TimeSpan FuseDelay { get; set; } = TimeSpan.FromSeconds(0.6);

        // VN FX params
        [CommandProperty(AccessLevel.GameMaster)] public int Range { get; set; } = 10;
        [CommandProperty(AccessLevel.GameMaster)] public int Speed { get; set; } = 10;
        [CommandProperty(AccessLevel.GameMaster)] public int Repeat { get; set; } = 1;
        [CommandProperty(AccessLevel.GameMaster)] public bool Reverse { get; set; } = false;
        [CommandProperty(AccessLevel.GameMaster)] public int LavaHue { get; set; } = 0x489;
        [CommandProperty(AccessLevel.GameMaster)] public FXPerfPreset Performance { get; set; } = FXPerfPreset.Medium;

        // Damage falloff (impact → edge)
        [CommandProperty(AccessLevel.GameMaster)] public int ImpactDamage { get; set; } = 60;
        [CommandProperty(AccessLevel.GameMaster)] public int EdgeDamage { get; set; } = 20;

        // Secondary dets
        [CommandProperty(AccessLevel.GameMaster)] public int SubExplosions { get; set; } = 3;
        [CommandProperty(AccessLevel.GameMaster)] public int SubMinDamage { get; set; } = 18;
        [CommandProperty(AccessLevel.GameMaster)] public int SubMaxDamage { get; set; } = 26;
        [CommandProperty(AccessLevel.GameMaster)] public int SubAoeRadius { get; set; } = 3; // 2–3 tiles

        // Force VN even if SafeMode is On
        [CommandProperty(AccessLevel.GameMaster)] public bool IgnoreAssetSafeMode { get; set; } = true;

        [Constructable]
        public VNLavaBomb() : base(0xF0D)
        {
            Name = "VN Lava Bomb";
            Hue = LavaHue;
            Weight = 1.0;
        }

        public VNLavaBomb(Serial s) : base(s) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.Target = new ThrowTarget(this);
            from.SendMessage(0x22, "Choose where to throw the Lava Bomb.");
        }

        private class ThrowTarget : Target
        {
            private readonly VNLavaBomb _bomb;
            public ThrowTarget(VNLavaBomb bomb) : base(12, true, TargetFlags.None) { _bomb = bomb; CheckLOS = true; }

            protected override void OnTarget(Mobile from, object o)
            {
                if (_bomb.Deleted || from == null) return;

                Point3D p; Map map;
                if (o is IPoint3D ip) { p = new Point3D(ip); map = from.Map; }
                else if (o is Mobile m) { p = m.Location; map = m.Map; }
                else { from.SendMessage(0x22, "Invalid target."); return; }

                if (map == null || map == Map.Internal) return;
                if (!from.InRange(_bomb.GetWorldLocation(), 12)) { from.SendLocalizedMessage(500295); return; }

                _bomb.ThrowAndDetonate(from, map, p);
            }
        }

        private void ThrowAndDetonate(Mobile from, Map map, Point3D p)
        {
            Effects.SendMovingEffect(
                new Entity(from.Serial, from.Location, from.Map),
                new Entity(Serial.Zero, p, map),
                0x36D4, 10, 0, false, false, Hue, 0);

            double tiles = from.GetDistanceToSqrt(p);
            var travel = TimeSpan.FromMilliseconds(Math.Max(150, tiles * 50));
            var detonateIn = travel + FuseDelay;

            Timer.DelayCall(detonateIn, () =>
            {
                if (Deleted || map == null) return;
                Detonate(from, map, p);
                Consume();
            });
        }

        private void Detonate(Mobile from, Map map, Point3D p)
        {
            // ... your pre-checks kept as-is ...

            CataclysmFX.SendVN(
                map, p, LavaHue, Range, Performance,
                Speed, Repeat, Reverse,
                ImpactDamage, EdgeDamage,
                SubExplosions, SubMinDamage, SubMaxDamage, Math.Max(2, Math.Min(3, SubAoeRadius)),
                from,
                EffectsMask,            // <— layer control
                Math.Max(1, Math.Min(4, WaveDirs)),
                Math.Max(0, Math.Min(200, PoolDensityPct))
            );
        }

        public override void Serialize(GenericWriter w)
        {
            base.Serialize(w);
            w.Write((int)EffectsMask);
            w.Write(WaveDirs);
            w.Write(PoolDensityPct);
            w.Write(7);
            w.Write(FuseDelay);

            w.Write(Range);
            w.Write(Speed);
            w.Write(Repeat);
            w.Write(Reverse);
            w.Write(LavaHue);
            w.Write((int)Performance);

            w.Write(ImpactDamage);
            w.Write(EdgeDamage);

            w.Write(SubExplosions);
            w.Write(SubMinDamage);
            w.Write(SubMaxDamage);
            w.Write(SubAoeRadius);

            w.Write(IgnoreAssetSafeMode);
        }

        public override void Deserialize(GenericReader r)
        {
            base.Deserialize(r);
            EffectsMask = (FXLayers)r.ReadInt();
            WaveDirs = r.ReadInt();
            PoolDensityPct = r.ReadInt();
            int v = r.ReadInt();

            if (v >= 1)
            {
                FuseDelay = r.ReadTimeSpan();
                Range = r.ReadInt();
                Speed = r.ReadInt();
                Repeat = r.ReadInt();
                Reverse = r.ReadBool();
                LavaHue = r.ReadInt();
                Performance = (FXPerfPreset)r.ReadInt();
            }

            if (v >= 2)
            {
                ImpactDamage = r.ReadInt();
                EdgeDamage = r.ReadInt();

                SubExplosions = r.ReadInt();
                SubMinDamage = r.ReadInt();
                SubMaxDamage = r.ReadInt();
                SubAoeRadius = r.ReadInt();
            }
            else
            {
                ImpactDamage = 60;
                EdgeDamage = 20;
                SubExplosions = 3;
                SubMinDamage = 18;
                SubMaxDamage = 26;
                SubAoeRadius = 3;
            }

            if (v >= 7)
                IgnoreAssetSafeMode = r.ReadBool();
            else
                IgnoreAssetSafeMode = true;
        }
    }
}
