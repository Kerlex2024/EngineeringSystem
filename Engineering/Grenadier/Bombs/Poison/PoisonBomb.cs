// ============================================================================
// File: Custom/Engineering/Grenadier/Bombs/Poison/PoisonBomb.cs  (BasePoisonBomb)
// ============================================================================
#region References
using System;
using System.Collections;
using Server;
using Server.Items;                    // <-- BasePotion, PotionEffect
using Server.Network;
using Server.Spells;
using Server.Targeting;
using Server.Mobiles;
using Server.Custom.Engineering.Grenadier; // <-- GrenadierConfig
#endregion


namespace Server.Custom.Engineering.Grenadier.Bombs.Poison
{
    public abstract class BasePoisonBomb : BasePotion
    {
        private static readonly bool LeveledExplosion = true;
        private static readonly bool InstantExplosion = true;
        private const int ExplosionRange = 6;

        private Timer _timer;
        private ArrayList _users;

        protected DateTime _nextUse;

        protected BasePoisonBomb(PotionEffect effect) : base(0xF0D, effect) { }
        protected BasePoisonBomb(Serial serial) : base(serial) { }

        public abstract int MinDamage { get; }
        public abstract int MaxDamage { get; }

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
            if (!GrenadierRegionRules.CanUseBomb(from, true)) { from.SendMessage(38, "You cannot use damaging bombs in town."); return; }
            if (DateTime.UtcNow < _nextUse) { from.SendMessage(38, "You must wait a moment before using another bomb."); return; }

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
                from.SendLocalizedMessage(500236); // throw it now
                _timer = Timer.DelayCall(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.25), 5,
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

            Effects.PlaySound(loc, map, 0x231);
            Effects.SendLocationEffect(loc, map, 0x11A6, 10, 1, 1167, 0);

            IPooledEnumerable eable = LeveledExplosion
                ? map.GetObjectsInRange(loc, ExplosionRange)
                : (IPooledEnumerable)map.GetMobilesInRange(loc, ExplosionRange);

            ArrayList toHit = new ArrayList();

            foreach (object o in eable)
            {
                Mobile mob = o as Mobile;
                if (mob != null && (from == null || (SpellHelper.ValidIndirectTarget(from, mob) && from.CanBeHarmful(mob, false) && from.InLOS(mob))))
                {
                    toHit.Add(o);
                }
                else if (o is BasePoisonBomb && o != this)
                {
                    toHit.Add(o);
                }
            }

            eable.Free();

            int ep = AosAttributes.GetValue(from, AosAttribute.EnhancePotions);
            double epScale = 1.0 + (ep > 0 ? ep : 0) / 100.0;

            for (int i = 0; i < toHit.Count; ++i)
            {
                object o = toHit[i];

                Mobile m = o as Mobile;
                if (m != null)
                {
                    if (from != null) from.DoHarmful(m);

                    bool ally = from != null && from.AccessLevel == AccessLevel.Player &&
                                m.AccessLevel == AccessLevel.Player &&
                                from.Guild != null && from.Guild == m.Guild;
                    if (!GrenadierConfig.FriendlyFire && ally) continue;

                    int damage = (int)Math.Round(Server.Utility.RandomMinMax(MinDamage, MaxDamage) * epScale);
                    AOS.Damage(m, from, damage, 0, 100, 0, 0, 0);

                    m.ApplyPoison(from, Server.Poison.Lethal);
                    m.SendLocalizedMessage(1070820);
                }
                else
                {
                    BasePoisonBomb pot = o as BasePoisonBomb;
                    if (pot != null) pot.Explode(from, false, pot.GetWorldLocation(), pot.Map);
                }
            }

            TriggerPoisonWave(loc, map);
        }

        private static void TriggerPoisonWave(Point3D center, Map map)
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
            private readonly BasePoisonBomb _potion;
            public ThrowTarget(BasePoisonBomb potion) : base(12, true, TargetFlags.None) { _potion = potion; }
            public BasePoisonBomb Potion { get { return _potion; } }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (_potion.Deleted || _potion.Map == Map.Internal) return;

                IPoint3D p = targeted as IPoint3D; if (p == null) return;

                Map map = from.Map; if (map == null) return;

                SpellHelper.GetSurfaceTop(ref p);
                from.RevealingAction();

                IEntity to = new Entity(Serial.Zero, new Point3D(p), map);
                Mobile mob = p as Mobile;
                if (mob != null) to = mob;

                Effects.SendMovingEffect(from, to, _potion.ItemID, 7, 0, false, false, _potion.Hue, 0);

                if (_potion.Amount > 1) Mobile.LiftItemDupe(_potion, 1);

                _potion.Internalize();
                Timer.DelayCall(TimeSpan.FromSeconds(1.0),
                    new TimerStateCallback(_potion.Reposition_OnTick), new object[] { from, p, map });
            }
        }
    }
}
