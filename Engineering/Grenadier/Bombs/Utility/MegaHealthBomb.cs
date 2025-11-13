// ============================================================================
// File: Custom/Engineering/Grenadier/Bombs/MegaHealthBomb.cs
// (base potion-style health bomb rewritten w/out pattern matching)
// ============================================================================
#region References
using System;
using System.Collections;
using Server.Network;
using Server.Spells;
using Server.Targeting;
using Server.Custom.Engineering.Grenadier;
using Server;
#endregion

namespace Server.Items
{
    public class BaseMegaHealthBomb : BasePotion
    {
        private static readonly bool InstantExplosion = true;
        private static readonly bool RelativeLocation = true;
        private const int ExplosionRange = 8;

        private Timer _timer;
        private ArrayList _users;

        protected DateTime _nextUse;

        public BaseMegaHealthBomb(PotionEffect effect) : base(0xF0D, effect) { }
        public BaseMegaHealthBomb(Serial serial) : base(serial) { }

        public override bool RequireFreeHand { get { return false; } }

        public override void Serialize(GenericWriter writer)
        { base.Serialize(writer); writer.Write(0); writer.Write(_nextUse); }
        public override void Deserialize(GenericReader reader)
        { base.Deserialize(reader); reader.ReadInt(); _nextUse = reader.ReadDateTime(); }

        public virtual object FindParent(Mobile from)
        {
            Mobile m = HeldBy; if (m != null && m.Holding == this) return m;
            object obj = RootParent; if (obj != null) return obj;
            if (Map == Map.Internal) return from;
            return this;
        }

        public override void Drink(Mobile from)
        {
            if (!GrenadierRegionRules.CanUseBomb(from, false))
            { from.SendMessage(38, "You cannot use that here."); return; }

            if (DateTime.UtcNow < _nextUse)
            { from.SendMessage(38, "You must wait a moment before using another bomb."); return; }

            if (from.Skills.Alchemy.Value < 100.0)
            { from.SendMessage("You lack the alchemy skill to use this potion."); return; }

            if (Core.AOS && (from.Paralyzed || from.Frozen || (from.Spell != null && from.Spell.IsCasting)))
            { from.SendLocalizedMessage(1062725); return; }

            ThrowTarget targ = from.Target as ThrowTarget;
            Stackable = false;

            if (targ != null && targ.Potion == this) return;

            from.RevealingAction();

            if (_users == null) _users = new ArrayList();
            if (!_users.Contains(from)) _users.Add(from);

            from.Target = new ThrowTarget(this);

            if (_timer == null)
            {
                from.SendLocalizedMessage(500236);
                _timer = Timer.DelayCall(TimeSpan.FromSeconds(0.75), TimeSpan.FromSeconds(1.0), 4,
                    new TimerStateCallback(Detonate_OnTick), new object[] { from, 3 });

                _nextUse = DateTime.UtcNow + GrenadierConfig.BombReuseDelay;
            }
        }

        public void Explode(Mobile from, bool direct, Point3D loc, Map map)
        {
            if (Deleted) return;

            Consume();

            if (_users != null)
            {
                for (int i = 0; i < _users.Count; ++i)
                {
                    Mobile m = (Mobile)_users[i];
                    ThrowTarget targ = m.Target as ThrowTarget;
                    if (targ != null && targ.Potion == this) Target.Cancel(m);
                }
            }

            if (map == null) return;

            Effects.PlaySound(loc, map, 0x307);
            Effects.PlaySound(loc, map, 0x0FE);
            Effects.SendLocationEffect(loc, map, 0x36B0, 9, 10, 0, 0);

            IPooledEnumerable eable = map.GetMobilesInRange(loc, ExplosionRange);
            ArrayList toHeal = new ArrayList();

            foreach (object o in eable)
            {
                Mobile mob = o as Mobile;
                if (mob != null && (from == null || (SpellHelper.ValidIndirectTarget(from, mob) && from.CanBeBeneficial(mob, false) && from.InLOS(mob))))
                {
                    toHeal.Add(o);
                }
            }

            eable.Free();

            int ep = AosAttributes.GetValue(from, AosAttribute.EnhancePotions);
            double scale = 1.0 + (ep > 0 ? ep : 0) / 100.0;
            double baseHeal = GrenadierConfig.HealthBombBaseHeal + ((from != null ? from.Skills.Healing.Value : 0) / 5.0);
            int healAmount = (int)Math.Round(baseHeal * scale);

            for (int i = 0; i < toHeal.Count; ++i)
            {
                Mobile m = (Mobile)toHeal[i];

                bool ally = (from == m) ||
                            (from != null && from.AccessLevel == AccessLevel.Player &&
                             m.AccessLevel == AccessLevel.Player &&
                             from.Guild != null && from.Guild == m.Guild);

                if (!GrenadierConfig.FriendlyFire && !ally)
                    continue;

                if (from != null) from.DoBeneficial(m);

                m.Heal(healAmount);
                m.CurePoison(from);
            }

            // sparkle wave
            for (int radius = 0; radius <= ExplosionRange; radius++)
            {
                Timer.DelayCall(TimeSpan.FromMilliseconds(10 * radius), delegate
                {
                    for (int x = -radius; x <= radius; x++)
                    for (int y = -radius; y <= radius; y++)
                    {
                        if (x * x + y * y <= radius * radius)
                        {
                            Point3D l = new Point3D(loc.X + x, loc.Y + y, loc.Z);
                            Effects.SendLocationEffect(l, map, 0x373A, 30, 10, 2623, 0);
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

            object parent = FindParent(from);

            if (timer == 0)
            {
                Point3D loc; Map map;
                Item item = parent as Item;
                Mobile mob = parent as Mobile;

                if (item != null) { loc = item.GetWorldLocation(); map = item.Map; }
                else if (mob != null) { loc = mob.Location; map = mob.Map; }
                else return;

                Explode(from, true, loc, map);
                _timer = null;
            }
            else
            {
                Item it = parent as Item;
                Mobile mo = parent as Mobile;

                if (it != null) it.PublicOverheadMessage(MessageType.Regular, 0x22, false, timer.ToString());
                else if (mo != null) mo.PublicOverheadMessage(MessageType.Regular, 0x22, false, timer.ToString());

                states[1] = timer - 1;
            }
        }

        private void Reposition_OnTick(object state)
        {
            if (Deleted) return;

            object[] states = (object[])state;
            Mobile from = (Mobile)states[0];
            IPoint3D p = (IPoint3D)states[1];
            Map map = (Map)states[2];

            Point3D loc = new Point3D(p);
            if (InstantExplosion) Explode(from, true, loc, map);
            else MoveToWorld(loc, map);
        }

        private class ThrowTarget : Target
        {
            private readonly BaseMegaHealthBomb _potion;
            public ThrowTarget(BaseMegaHealthBomb potion) : base(12, true, TargetFlags.None) { _potion = potion; }
            public BaseMegaHealthBomb Potion { get { return _potion; } }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (_potion.Deleted || _potion.Map == Map.Internal) return;

                IPoint3D p = targeted as IPoint3D; if (p == null) return;

                Map map = from.Map; if (map == null) return;

                SpellHelper.GetSurfaceTop(ref p);
                from.RevealingAction();

                IEntity to = new Entity(Serial.Zero, new Point3D(p), map);
                Mobile mob = p as Mobile;
                if (mob != null)
                {
                    if (!RelativeLocation) p = mob.Location;
                    else to = mob;
                }

                Effects.SendMovingEffect(from, to, _potion.ItemID, 7, 0, false, false, _potion.Hue, 0);

                if (_potion.Amount > 1) Mobile.LiftItemDupe(_potion, 1);

                _potion.Internalize();
                Timer.DelayCall(TimeSpan.FromSeconds(1.0),
                    new TimerStateCallback(_potion.Reposition_OnTick), new object[] { from, p, map });
            }
        }
    }
}
