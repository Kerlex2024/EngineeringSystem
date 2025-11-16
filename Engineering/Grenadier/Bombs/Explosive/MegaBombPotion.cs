using System;
using Server;
using Server.Items; // PotionEffect

namespace Server.Custom.Engineering.Grenadier.Bombs.Explosive
{
    public partial class MegaBombPotion : BaseMegaBomb
    {
        [Constructable]
        public MegaBombPotion() : base(Server.Items.PotionEffect.ExplosionGreater)
        {
            Name = "Mega Bomb";
        }

        public MegaBombPotion(Serial s) : base(s) { }

        public override int MinDamage { get { return 80; } }
        public override int MaxDamage { get { return 150; } }

        public override void Drink(Mobile from)
        {
            if (from == null)
                return;

            if (from.Skills.Alchemy.Value < 100.0)
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
                // MEGA (Tier 3)
        public override int ExplosionRadius { get { return 4; } }
        public override TimeSpan FuseDelay { get { return TimeSpan.FromSeconds(1.5); } }
        public override int CooldownSeconds { get { return 18; } }

        // heavier falloff; center hurts most
        protected override double DamageFalloff(Point3D center, Point3D hit)
        {
            int dx = hit.X - center.X;
            int dy = hit.Y - center.Y;
            int d = (int)Math.Sqrt(dx * dx + dy * dy);

            if (d <= 0) return 1.0;  // center
            if (d == 1) return 0.80;
            if (d == 2) return 0.55;
            if (d == 3) return 0.35;
            return 0.25;
        }


        protected override void OnDetonate(Mobile from, Point3D loc, Map map)
        {
            // incendiary patch: apply small DoT to mobiles within 1 tile of center
            IPooledEnumerable e = map.GetMobilesInRange(loc, 1);
            foreach (Mobile m in e)
            {
                if (m == null || !m.Alive) continue;
                if (!m.InLOS(loc)) continue;

                int ticks = 3;
                Timer.DelayCall(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0), ticks, new TimerStateCallback(delegate(object o)
                {
                    Mobile mm = o as Mobile;
                    if (mm != null && mm.Alive)
                        AOS.Damage(mm, from, 5, 100, 0, 0, 0, 0); // small burn per tick
                }), m);
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
