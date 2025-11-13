using System;
using Server;
using Server.Items; // PotionEffect

namespace Server.Custom.Engineering.Grenadier.Bombs.Explosive
{
    public partial class StrategicBomb : BaseStrategicBomb
    {
        [Constructable]
        public StrategicBomb() : base(Server.Items.PotionEffect.ExplosionGreater)
        {
            Hue = 2609;
            Name = "Strategic Bomb";
        }

        public StrategicBomb(Serial s) : base(s) { }

        public override int MinDamage { get { return 40; } }
        public override int MaxDamage { get { return 80; } }

        public override void Drink(Mobile from)
        {
            if (from == null)
                return;

            if (from.Skills.Alchemy.Value < 75.0)
            {
                from.SendMessage("You lack the alchemy skill to use this potion.");
                return;
            }

            base.Drink(from);
        }

        public override void Serialize(GenericWriter w)
        {
            base.Serialize(w);
            w.Write(0);
        }
        // STRATEGIC (Tier 2)
        public override int ExplosionRadius { get { return 3; } }      // 2–3; choose 3
        public override TimeSpan FuseDelay { get { return TimeSpan.FromSeconds(1.0); } }
        public override int CooldownSeconds { get { return 10; } }


        // stronger falloff (fragmentation ring flavor)
        protected override double DamageFalloff(Point3D center, Point3D hit)
        {
            int dx = hit.X - center.X;
            int dy = hit.Y - center.Y;
            int d = (int)Math.Sqrt(dx * dx + dy * dy);

            if (d <= 0) return 1.0;  // center full damage
            if (d == 1) return 0.85; // near ring
            if (d == 2) return 0.60; // mid ring
            return 0.40;             // outer ring
        }


        protected override void OnDetonate(Mobile from, Point3D loc, Map map)
        {
            // short stun flavor: tiny dex drop (1 sec) via stamina hit
            IPooledEnumerable e = map.GetMobilesInRange(loc, ExplosionRadius);
            foreach (Mobile m in e)
            {
                if (m == null || !m.Alive) continue;
                if (!m.InLOS(loc)) continue;
                try { m.Stam = Math.Max(0, m.Stam - 15); } catch { }
            }
            e.Free();
        }


        public override void Deserialize(GenericReader r)
        {
            base.Deserialize(r);
            r.ReadInt();

            // Align bottle with config after reloads/saves
            if (Hue != ExplosionHue)
                Hue = ExplosionHue;
        }

    }
}
