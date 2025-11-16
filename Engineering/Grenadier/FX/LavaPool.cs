// ============================================================================
// File: Scripts/Custom/Engineering/Grenadier/FX/LavaPool.cs  (REPLACE)
// Purpose: Damaging lava decal that reliably hits targets on its tile.
// ============================================================================
using System;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Server.Items
{
    public class LavaPool : Item
    {
        [CommandProperty(AccessLevel.GameMaster)] public int DamagePerTick { get; set; } = 7;
        [CommandProperty(AccessLevel.GameMaster)] public TimeSpan TickRate { get; set; } = TimeSpan.FromMilliseconds(750);
        [CommandProperty(AccessLevel.GameMaster)] public TimeSpan Lifetime { get; set; } = TimeSpan.FromSeconds(6.0);
        [CommandProperty(AccessLevel.GameMaster)] public int FireHue { get; set; } = 0x489;

        // Set true temporarily if you want *everyone* (except staff) to be damaged for testing.
        [CommandProperty(AccessLevel.GameMaster)] public bool DamageAllForDebug { get; set; } = false;

        private DateTime _expires;
        private Timer _timer;

        [Constructable]
        public LavaPool() : base(0x122A) // pick a molten-looking tile ID
        {
            Name = "lava";
            Movable = false;
            Hue = FireHue;
            _expires = DateTime.UtcNow + Lifetime;
            _timer = Timer.DelayCall(TimeSpan.Zero, TickRate, OnTick);
        }

        public LavaPool(int hue, TimeSpan lifetime, TimeSpan tick, int dmg) : this()
        {
            FireHue = hue;
            Hue = hue;
            Lifetime = lifetime;
            TickRate = tick;
            DamagePerTick = dmg;

            _expires = DateTime.UtcNow + Lifetime;

            _timer?.Stop();
            _timer = Timer.DelayCall(TimeSpan.Zero, TickRate, OnTick);
        }

        public LavaPool(Serial s) : base(s) { }

        private void OnTick()
        {
            try
            {
                if (Deleted || Map == null || Map == Map.Internal) { Delete(); return; }
                if (DateTime.UtcNow >= _expires) { Delete(); return; }

                // Same-tile occupants (range 0) is the most reliable for decals
                foreach (Mobile m in Map.GetMobilesInRange(Location, 0))
                {
                    if (!ShouldDamage(m, DamageAllForDebug))
                        continue;

                    // generous vertical tolerance to handle slope
                    if (Math.Abs(m.Z - Z) > 24)
                        continue;

                    AOS.Damage(m, null, DamagePerTick, 0, 100, 0, 0, 0);

                    try
                    {
                        m.FixedParticles(0x3709, 6, 12, 5052, FireHue, 0, EffectLayer.Waist);
                        m.PlaySound(0x208);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[LavaPool] tick error: {0}", ex);
            }
        }


        private static bool ShouldDamage(Mobile m, bool debugAll)
        {
            if (m == null || m.Deleted || !m.Alive)
                return false;

            // skip staff
            if (m.AccessLevel > AccessLevel.Player)
                return false;

            // debug: hurt everyone (non-staff)
            if (debugAll)
                return true;

            // Wild monsters
            if (m is BaseCreature bc && !bc.Controlled && !bc.Summoned)
                return true;

            // Players: criminals (grey) or murderers (red)
            if (m.Player && (m.Criminal || m.Kills >= 5))
                return true;

            return false;
        }

        public override void OnDelete()
        {
            try { _timer?.Stop(); } catch { }
            _timer = null;
            base.OnDelete();
        }

        public override void Serialize(GenericWriter w)
        {
            base.Serialize(w);
            w.Write(3);
            w.Write(DamagePerTick);
            w.Write(TickRate);
            w.Write(Lifetime);
            w.Write(FireHue);
            w.Write(DamageAllForDebug);
            w.WriteDeltaTime(_expires);
        }

        public override void Deserialize(GenericReader r)
        {
            base.Deserialize(r);
            int v = r.ReadInt();

            if (v >= 1)
            {
                DamagePerTick = r.ReadInt();
                TickRate = r.ReadTimeSpan();
                Lifetime = r.ReadTimeSpan();
                FireHue = r.ReadInt();
            }
            if (v >= 3)
            {
                DamageAllForDebug = r.ReadBool();
                _expires = r.ReadDeltaTime();
            }

            if (Deleted) return;

            if (DateTime.UtcNow >= _expires) { Delete(); return; }

            _timer = Timer.DelayCall(TimeSpan.Zero, TickRate, OnTick);
        }
    }
}
