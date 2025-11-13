// ============================================================================
// File: Custom/Engineering/Grenadier/TrapBoxes/ClaymoreBox.cs
// ============================================================================
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Server.Network;

namespace Server.Custom.Engineering.Grenadier.TrapBoxes
{
    public class ClaymoreBox : Item
    {
        private bool _armed;
        private int _charges;
        private DateTime _nextArm;

        [CommandProperty(AccessLevel.GameMaster)]
        public Mobile Owner { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool Armed { get { return _armed; } set { _armed = value; } }

        [CommandProperty(AccessLevel.GameMaster)]
        public int Charges { get { return _charges; } set { _charges = Math.Max(0, value); } }

        [CommandProperty(AccessLevel.GameMaster)]
        public TimeSpan ArmDelay { get; set; } = Server.Custom.Engineering.Grenadier.GrenadierConfig.ClaymoreArmDelay;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Radius { get; set; } = Server.Custom.Engineering.Grenadier.GrenadierConfig.ClaymoreRadius;

        [CommandProperty(AccessLevel.GameMaster)]
        public int MinDamage { get; set; } = Server.Custom.Engineering.Grenadier.GrenadierConfig.ClaymoreMinDamage;

        [CommandProperty(AccessLevel.GameMaster)]
        public int MaxDamage { get; set; } = Server.Custom.Engineering.Grenadier.GrenadierConfig.ClaymoreMaxDamage;

        [Constructable]
        public ClaymoreBox() : base(0x1B7A)
        {
            Hue = 2406;
            Name = "Claymore Box";
            Movable = true;
            Weight = 8.0;
            _charges = Server.Custom.Engineering.Grenadier.GrenadierConfig.ClaymoreBaseCharges;
        }

        public ClaymoreBox(Serial s) : base(s) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Owner: {0}", Owner == null ? "none" : Owner.Name);
            list.Add("Charges: {0}", Charges);
            if (!_armed) list.Add("Arming...");
        }

        public override void Serialize(GenericWriter w)
        {
            base.Serialize(w);
            w.Write(0);
            w.Write(_armed);
            w.Write(_charges);
            w.Write(_nextArm);
            w.Write(Owner);
            w.Write(ArmDelay);
            w.Write(Radius);
            w.Write(MinDamage);
            w.Write(MaxDamage);
        }

        public override void Deserialize(GenericReader r)
        {
            base.Deserialize(r);
            r.ReadInt();
            _armed = r.ReadBool();
            _charges = r.ReadInt();
            _nextArm = r.ReadDateTime();
            Owner = r.ReadMobile();
            ArmDelay = r.ReadTimeSpan();
            Radius = r.ReadInt();
            MinDamage = r.ReadInt();
            MaxDamage = r.ReadInt();
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            { from.SendLocalizedMessage(1060640); return; } // must be in pack

            if (Charges <= 0)
            { from.SendMessage(38, "This trap is out of charges."); return; }

            from.SendMessage("Choose a placement location.");
            from.Target = new PlaceTarget(this);
        }

        private class PlaceTarget : Target
        {
            private readonly ClaymoreBox _box;
            public PlaceTarget(ClaymoreBox b) : base(6, true, TargetFlags.None) { _box = b; }

            protected override void OnTarget(Mobile from, object o)
            {
                if (_box.Deleted || !_box.IsChildOf(from.Backpack)) return;

                IPoint3D ip = o as IPoint3D;
                if (ip == null) return;

                if (!Server.Custom.Engineering.Grenadier.GrenadierRegionRules.CanUseBomb(from, true))
                { from.SendMessage(38, "You cannot place damaging traps in town."); return; }

                Point3D p = new Point3D(ip);
                Map map = from.Map;

                _box.MoveToWorld(p, map);
                _box.Owner = from;
                _box._armed = false;
                _box._nextArm = DateTime.UtcNow + _box.ArmDelay;
                from.SendMessage("Trap placed. Arming...");

                Timer.DelayCall(_box.ArmDelay, new TimerCallback(delegate
                {
                    if (_box.Deleted) return;
                    _box._armed = true;
                    from.SendMessage("Trap armed.");
                }));
            }
        }

        public override bool HandlesOnMovement
        {
            get { return true; }
        }

        public override void OnMovement(Mobile m, Point3D oldLocation)
        {
            if (Deleted || !_armed || _charges <= 0 || m == null || !m.Alive || m.AccessLevel > AccessLevel.Player)
                return;

            // spare owner/guild if FriendlyFire = false
            bool ally = Owner != null && m.AccessLevel == AccessLevel.Player &&
                        Owner.AccessLevel == AccessLevel.Player &&
                        Owner.Guild != null && Owner.Guild == m.Guild;

            if (!Server.Custom.Engineering.Grenadier.GrenadierConfig.FriendlyFire && ally)
                return;

            if (m.InRange(Location, Radius) && m.InLOS(this))
                Detonate(m);
        }

        private void Detonate(Mobile target)
        {
            if (!_armed || _charges <= 0) return;

            _armed = false;
            _charges--;

            Effects.PlaySound(Location, Map, 0x307);
            Effects.SendLocationEffect(Location, Map, 0x36B0, 9, 10, 0, 0);

            int dmg = Server.Utility.RandomMinMax(MinDamage, MaxDamage);

            IPooledEnumerable e = Map.GetMobilesInRange(Location, Radius);
            foreach (Mobile m in e)
            {
                if (m == null || !m.Alive) continue;

                bool ally = Owner != null && m.AccessLevel == AccessLevel.Player &&
                            Owner.AccessLevel == AccessLevel.Player &&
                            Owner.Guild != null && Owner.Guild == m.Guild;

                if (!Server.Custom.Engineering.Grenadier.GrenadierConfig.FriendlyFire && ally)
                    continue;

                AOS.Damage(m, Owner, dmg, 0, 100, 0, 0, 0);
            }
            e.Free();

            if (_charges > 0)
            {
                Timer.DelayCall(TimeSpan.FromSeconds(2.0), new TimerCallback(delegate
                {
                    if (!Deleted) _armed = true;
                }));
            }
            else
            {
                PublicOverheadMessage(MessageType.Label, 38, false, "depleted");
            }
        }
    }
}
