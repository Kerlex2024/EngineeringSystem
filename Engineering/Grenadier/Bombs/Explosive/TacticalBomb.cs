using System;
using Server;
using Server.Items; // PotionEffect

namespace Server.Custom.Engineering.Grenadier.Bombs.Explosive
{
    public partial class TacticalBomb : BaseTacticalBomb
    {
        [Constructable]
        public TacticalBomb() : base(Server.Items.PotionEffect.ExplosionGreater)
        {
            Hue = 2605;
            Name = "Tactical Bomb";
        }

        public TacticalBomb(Serial s) : base(s) { }

        // Bases typically expose these:
        public override int MinDamage { get { return 20; } }
        public override int MaxDamage { get { return 40; } }

        public override void Drink(Mobile from)
        {
            if (from == null)
                return;

            // Tier requirement
            if (from.Skills.Alchemy.Value < 50.0)
            {
                from.SendMessage("You lack the alchemy skill to use this potion.");
                return;
            }
        
            base.Drink(from); // base handles throw/fuse/explode visuals currently configured
        }
        // TACTICAL (Tier 1)
        public override int ExplosionRadius { get { return 1; } }
        public override TimeSpan FuseDelay { get { return TimeSpan.FromSeconds(0.5); } }
        public override int CooldownSeconds { get { return 6; } }

        protected override void OnDetonate(Mobile from, Point3D loc, Map map)
        {
            // small knockback + brief slow (0.5s)
            IPooledEnumerable e = map.GetMobilesInRange(loc, ExplosionRadius);
            foreach (Mobile m in e)
            {
                if (m == null || !m.Alive) continue;
                if (!m.InLOS(loc)) continue;

                // knockback 1 tile away from center (cheap + safe)
                int dx = m.X - loc.X;
                int dy = m.Y - loc.Y;
                if (dx == 0 && dy == 0) dx = 1; // nudge
                Point3D to = new Point3D(m.X + Math.Sign(dx), m.Y + Math.Sign(dy), m.Z);
                if (map.CanSpawnMobile(to))
                    m.Location = to;

                // brief slow: reduce stamina briefly
                try { m.Stam = Math.Max(0, m.Stam - 10); } catch { }
            }
            e.Free();
        }
        public override void Serialize(GenericWriter w)
        {
            base.Serialize(w);
            w.Write(0);
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
