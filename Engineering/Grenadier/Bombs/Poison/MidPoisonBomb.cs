// ============================================================================
// File: Custom/Engineering/Grenadier/Bombs/Poison/MidPoisonBomb.cs
// ============================================================================
using System;
using System.Collections;
using Server;
using Server.Custom.Engineering.Grenadier;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
using Server.Targeting;

namespace Server.Custom.Engineering.Grenadier.Bombs.Poison
{
    public partial class MidPoisonBomb : Item
    {
        private const int ExplosionRange = 6;
        private Timer _timer;
        private ArrayList _users;
        private bool _exploded;
        private DateTime _nextUse;

        [Constructable]
        public MidPoisonBomb() : base(0xF0D)
        {
            Hue = 2273;
            Weight = 1.0;
            Name = "Poison Bomb";
        }

        public MidPoisonBomb(Serial serial) : base(serial) { }

        public virtual int MinDamage { get { return 20; } }
        public virtual int MaxDamage { get { return 40; } }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); writer.Write(_nextUse); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); _nextUse = reader.ReadDateTime(); }

        public override void OnDoubleClick(Mobile from)
        {
            if (!GrenadierRegionRules.CanUseBomb(from, true)) { from.SendMessage(38, "You cannot use damaging bombs in town."); return; }
            if (DateTime.UtcNow < _nextUse) { from.SendMessage(38, "You must wait a moment before using another bomb."); return; }
            if (!IsChildOf(from.Backpack)) { from.SendLocalizedMessage(1060640); return; }
            if (from.Skills[SkillName.Poisoning].Base < 75.0) { from.SendMessage("You need at least 75 Poisoning skill to use this bomb."); return; }

            from.RevealingAction();

            if (_users == null) _users = new ArrayList();
            if (!_users.Contains(from)) _users.Add(from);

            from.Target = new ThrowTarget(this);

            if (_timer == null)
            {
                _timer = Timer.DelayCall(TimeSpan.FromSeconds(0.75), TimeSpan.FromSeconds(1.0), 4,
                    Detonate_OnTick, new object[] { from, 3 });

                _nextUse = DateTime.UtcNow + GrenadierConfig.BombReuseDelay;
            }
        }

        public void Explode(Mobile from, Point3D loc, Map map)
        {
            if (Deleted || map == null || _exploded) return;

            _exploded = true;
            Consume();

            Effects.PlaySound(loc, map, 0x307);
            Effects.PlaySound(loc, map, 0x231);
            Effects.SendLocationEffect(loc, map, 0x11A6, 50, 10, 2473, 0);

            int ep = AosAttributes.GetValue(from, AosAttribute.EnhancePotions);
            double scale = 1.0 + (ep > 0 ? ep : 0) / 100.0;

            IPooledEnumerable eable = map.GetMobilesInRange(loc, ExplosionRange);
            foreach (Mobile m in eable)
            {
                if (from == null || (SpellHelper.ValidIndirectTarget(from, m) && from.CanBeHarmful(m, false) && from.InLOS(m)))
                {
                    if (from != null) from.DoHarmful(m);

                    bool ally = from != null && from.AccessLevel == AccessLevel.Player &&
                                m.AccessLevel == AccessLevel.Player &&
                                from.Guild != null && from.Guild == m.Guild;
                    if (!GrenadierConfig.FriendlyFire && ally) continue;

                    int damage = (int)Math.Round(Server.Utility.RandomMinMax(MinDamage, MaxDamage) * scale);
                    AOS.Damage(m, from, damage, 0, 100, 0, 0, 0);

                    m.ApplyPoison(from, Server.Poison.Lethal);
                    m.SendLocalizedMessage(1070820);
                }
            }
            eable.Free();

            TriggerPoisonCloudWave(loc, map);
        }

        private static void TriggerPoisonCloudWave(Point3D center, Map map)
        {
            for (int radius = 0; radius <= ExplosionRange; radius++)
            {
                Timer.DelayCall(TimeSpan.FromMilliseconds(10 * radius), delegate
                {
                    for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                    {
                        if (x * x + y * y <= radius * radius)
                        {
                            Point3D loc = new Point3D(center.X + x, center.Y + y, center.Z);
                            Effects.SendLocationEffect(loc, map, 0x11A6, 30, 10, 2511, 0);
                        }
                    }
                });
            }
        }

        private void Detonate_OnTick(object state)
        {
            if (Deleted) return;

            object[] states = (object[])state;
            Mobile from = (Mobile)states[0];
            int timer = (int)states[1];

            if (timer == 0)
            {
                Explode(from, GetWorldLocation(), Map);
                _timer = null;
            }
            else
            {
                PublicOverheadMessage(MessageType.Regular, 0x22, false, timer.ToString());
                states[1] = timer - 1;
            }
        }

        private class ThrowTarget : Target
        {
            private readonly MidPoisonBomb _bomb;
            public ThrowTarget(MidPoisonBomb b) : base(12, true, TargetFlags.None) { _bomb = b; }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (_bomb.Deleted || _bomb.Map == Map.Internal) return;

                IPoint3D p = targeted as IPoint3D; if (p == null) return;

                Map map = from.Map; if (map == null) return;
                SpellHelper.GetSurfaceTop(ref p);
                from.RevealingAction();

                IEntity to = new Entity(Serial.Zero, new Point3D(p), map);
                Effects.SendMovingEffect(from, to, _bomb.ItemID, 7, 0, false, false, _bomb.Hue, 0);

                _bomb.MoveToWorld(new Point3D(p), map);
                Timer.DelayCall(TimeSpan.FromSeconds(1.0), delegate { _bomb.Explode(from, new Point3D(p), map); });
            }
        }
    }
}
