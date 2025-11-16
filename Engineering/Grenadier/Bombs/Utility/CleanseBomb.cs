// ============================================================================
// File: Custom/Engineering/Grenadier/Bombs/Utility/CleanseBomb.cs
// ============================================================================
using System;
using System.Reflection;
using Server;
using Server.Spells;
using Server.Custom.Engineering.Grenadier;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Server.Spells.First;          // Weaken/Clumsy/Feeblemind
using Server.Spells.Fourth;         // Curse
using Server.Spells.Necromancy;     // EvilOmen/Strangle/CorpseSkin/BloodOath/MindRot
using Server.Spells.Mysticism;      // SpellPlague/Sleep


namespace Server.Custom.Engineering.Grenadier.Bombs.Utility
{
    public partial class CleanseBomb : Item
    {
        private DateTime _nextUse;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Radius { get; set; }

        [Constructable]
        public CleanseBomb() : base(0xF0C)
        {
            Name = "cleanse bomb";
            Hue = 1150;
            Radius = GrenadierConfig.CleanseBombRadius;
        }

        public CleanseBomb(Serial s) : base(s) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Radius: {0}, Cooldown: {1}s (removes curses)", Radius, (int)GrenadierConfig.BombReuseDelay.TotalSeconds);
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
            private readonly CleanseBomb _b;
            public ThrowTarget(CleanseBomb b) : base(12, true, TargetFlags.None) { _b = b; }

            protected override void OnTarget(Mobile from, object o)
            {
                if (_b.Deleted) return;

                IPoint3D ip = o as IPoint3D; if (ip == null) return;

                Point3D p = new Point3D(ip); Map map = from.Map; if (map == null || !from.InLOS(p)) { from.SendMessage(38, "No line of sight."); return; }
                _b._nextUse = DateTime.UtcNow + GrenadierConfig.BombReuseDelay;
                IEntity to = new Entity(Serial.Zero, p, map);
                Effects.SendMovingEffect(from, to, 0xF0D, 5, 0, false, false);
                Timer.DelayCall(TimeSpan.FromMilliseconds(350), () => _b.DoExplode(from, p, map));

            }
        }

        private void DoExplode(Mobile from, Point3D where, Map map)
        {
            Effects.PlaySound(where, map, 0x1ED);
            Effects.SendLocationEffect(where, map, 0x37B9, 20, 0x47E);

            IPooledEnumerable e = map.GetMobilesInRange(where, Radius);
            foreach (Mobile m in e)
            {
                if (m == null || m.Deleted || !m.Alive) continue;

                bool ally = (from == m) || (from != null && from.Guild != null && from.Guild == m.Guild);
                if (!GrenadierConfig.FriendlyFire && !ally) continue;

                try
                {
                    Type t = ScriptCompiler.FindTypeByFullName("Server.Items.EnchantedApple");
                    var mi = t != null ? t.GetMethod("DoEffect", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) : null;
                    if (mi != null) { mi.Invoke(null, new object[] { m }); continue; }
                }
                catch { }

                // Enchanted-Apple style cleansing
                try { EvilOmenSpell.TryEndEffect(m); } catch { }
                try { StrangleSpell.RemoveCurse(m); } catch { }
                try { CorpseSkinSpell.RemoveCurse(m); } catch { }
                try { WeakenSpell.RemoveEffects(m); } catch { }
                try { FeeblemindSpell.RemoveEffects(m); } catch { }
                try { ClumsySpell.RemoveEffects(m); } catch { }
                try { CurseSpell.RemoveEffect(m); } catch { }
                try { MortalStrike.EndWound(m); } catch { }
                try { BloodOathSpell.RemoveCurse(m); } catch { }
                try { MindRotSpell.ClearMindRotScalar(m); } catch { }
                try { SpellPlagueSpell.RemoveFromList(m); } catch { }
                try { SleepSpell.EndSleep(m); } catch { }

                // buff icons commonly used for curses
                try { BuffInfo.RemoveBuff(m, BuffIcon.MassCurse); } catch { }
                try { BuffInfo.RemoveBuff(m, BuffIcon.Curse); } catch { }
                try { BuffInfo.RemoveBuff(m, BuffIcon.EvilOmen); } catch { }
                try { BuffInfo.RemoveBuff(m, BuffIcon.BloodOathCurse); } catch { }

                m.SendMessage(0x55, "Curses cleansed.");
            }
            e.Free();
        }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); w.Write(_nextUse); w.Write(Radius); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); _nextUse = r.ReadDateTime(); Radius = r.ReadInt(); }
    }
}
