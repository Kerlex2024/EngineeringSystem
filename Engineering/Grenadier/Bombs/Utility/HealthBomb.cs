// ============================================================================
// File: Custom/Engineering/Grenadier/Bombs/Utility/HealthBomb.cs
// ============================================================================
using System;
using Server;
using Server.Custom.Engineering.Grenadier;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Custom.Engineering.Grenadier.Bombs.Utility
{
    public partial class HealthBomb : Item
    {
        private DateTime _nextUse;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Radius { get; set; }

        [Constructable]
        public HealthBomb() : base(0xF0C)
        {
            Name = "health bomb";
            Hue = 1153;
            Movable = true;
            Weight = 1.0;
            Radius = GrenadierConfig.HealthBombRadius;
        }

        public HealthBomb(Serial s) : base(s) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Radius: {0}, Cooldown: {1}s (Healing & EP scale)", Radius, (int)GrenadierConfig.BombReuseDelay.TotalSeconds);
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack)) { from.SendLocalizedMessage(1060640); return; }
            if (!GrenadierRegionRules.CanUseBomb(from, false)) { from.SendMessage(38, "You cannot use that here."); return; }
            if (DateTime.UtcNow < _nextUse) { from.SendMessage(38, "You must wait a moment."); return; }

            from.SendMessage("Choose where to throw.");
            from.Target = new ThrowTarget(this);
        }

        private sealed class ThrowTarget : Target
        {
            private readonly HealthBomb _b;
            public ThrowTarget(HealthBomb b) : base(12, true, TargetFlags.None) { _b = b; }

            protected override void OnTarget(Mobile from, object o)
            {
                if (_b.Deleted) return;

                IPoint3D ip = o as IPoint3D; if (ip == null) return;
                Point3D p = new Point3D(ip);
                Map map = from.Map; if (map == null || !from.InLOS(p)) { from.SendMessage(38, "No line of sight."); return; }

                _b._nextUse = DateTime.UtcNow + GrenadierConfig.BombReuseDelay;
                IEntity to = new Entity(Serial.Zero, p, map);
                Effects.SendMovingEffect(from, to, 0xF0D, 5, 0, false, false);
                Timer.DelayCall(TimeSpan.FromMilliseconds(350), () => _b.DoExplode(from, p, map));

            }
        }

        private void DoExplode(Mobile from, Point3D where, Map map)
        {
            Effects.PlaySound(where, map, 0x1F2);
            Effects.SendLocationEffect(where, map, 0x376A, 20, 0x9C2);

            int ep = AosAttributes.GetValue(from, AosAttribute.EnhancePotions);
            double scale = 1.0 + (ep > 0 ? ep : 0) / 100.0;
            double baseHeal = GrenadierConfig.HealthBombBaseHeal + ((from != null ? from.Skills.Healing.Value : 0) / 5.0);
            int heal = (int)Math.Round(baseHeal * scale);

            IPooledEnumerable e = map.GetMobilesInRange(where, Radius);
            foreach (Mobile m in e)
            {
                if (m == null || m.Deleted || !m.Alive) continue;

                bool ally = (from == m) || (from != null && from.Guild != null && from.Guild == m.Guild);
                if (!GrenadierConfig.FriendlyFire && !ally) continue;

                if (from != null) from.DoBeneficial(m);
                m.Heal(heal);
                m.SendMessage(0x55, "+" + heal.ToString() + " health restored.");
            }
            e.Free();
        }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); w.Write(_nextUse); w.Write(Radius); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); _nextUse = r.ReadDateTime(); Radius = r.ReadInt(); }
    }
}
