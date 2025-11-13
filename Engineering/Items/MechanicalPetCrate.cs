// ============================================================================
// File: Scripts/Custom/Engineering/Items/MechanicalPetCrate.cs
// Purpose: Base crate with assembly timer + live refresh; Spider/Mule crates
// ============================================================================
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Engines.Craft;
using Server.Custom.Engineering;
using System.Reflection;

namespace Server.Custom.Engineering
{
    public abstract class MechanicalPetCrate : Item, ICraftable
    {
        public TimeSpan ProductionTime { get; private set; }
        public bool Ready { get; private set; }

        private Mobile _crafter;
        private DateTime _finish;
        private bool _started;
        private Timer _tick;

        public virtual double AssemblyFailureChance => EngineeringConfig.AssemblyFailureChanceBase;
        protected virtual TimeSpan RefreshInterval => EngineeringConfig.CrateRefreshInterval;
        protected static BaseCreature CreatePetByName(string shortTypeName)
        {
            // Try several likely full names
            Type t =
                ScriptCompiler.FindTypeByName(shortTypeName) ??
                ScriptCompiler.FindTypeByFullName("Server.Custom.Engineering." + shortTypeName) ??
                ScriptCompiler.FindTypeByFullName("Server.Custom.MechanicalPets." + shortTypeName);

            if (t == null || !typeof(BaseCreature).IsAssignableFrom(t))
                return null;

            return Activator.CreateInstance(t) as BaseCreature;
}

        protected MechanicalPetCrate(TimeSpan time) : base(0xE7F)
        {
            Weight = 10.0;
            Hue = 2405;
            Name = "mechanical pet crate (assembling)";
            ProductionTime = time;
            Ready = false;
            Movable = true;

            BeginAssembly(null); // why: GM add or non-craft creation still starts timer
        }

        public MechanicalPetCrate(Serial s) : base(s) { }

        public void BeginAssembly(Mobile crafter)
        {
            if (_started || Ready)
                return;

            _started = true;
            _crafter = crafter;
            _finish = DateTime.UtcNow + ProductionTime;

            StartTick();
            Timer.DelayCall(ProductionTime, CompleteAssembly);
            InvalidateProperties(); // why: refresh OPL cache immediately
        }

        private void StartTick()
        {
            _tick?.Stop();
            _tick = Timer.DelayCall(TimeSpan.Zero, RefreshInterval, () =>
            {
                if (Deleted) { _tick?.Stop(); _tick = null; return; }

                if (!Ready)
                {
                    if (_finish <= DateTime.UtcNow)
                    {
                        CompleteAssembly();
                        return;
                    }

                    InvalidateProperties(); // why: push updated OPL so clients see new time
                }
                else
                {
                    _tick?.Stop();
                    _tick = null;
                }
            });
        }

        private void CompleteAssembly()
        {
            if (Deleted || Ready)
                return;

            _tick?.Stop();
            _tick = null;

            if (Utility.RandomDouble() < EffectiveFailChance)
            {
                Name = "ruined mechanical components";
                Hue = 1109;
                Ready = false; // ruined state; cannot unpack
                _crafter?.SendMessage(33, "Your assembly failed catastrophically.");
                InvalidateProperties();
                return;
            }

            Ready = true;
            Name = "mechanical pet crate";
            Hue = 0;
            _crafter?.SendMessage("Your mechanical pet is ready.");
            InvalidateProperties();
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            if (Prototype)
                list.Add("Build: Prototype");

            if (!Ready)
            {
                TimeSpan remaining = _finish > DateTime.UtcNow ? _finish - DateTime.UtcNow : TimeSpan.Zero;
                list.Add("Status: Assembling ({0} remaining)", remaining.ToString(@"hh\:mm\:ss"));
            }
            else
            {
                list.Add("Status: Ready");
            }
        }

        public override void OnSingleClick(Mobile from)
        {
            base.OnSingleClick(from);

            if (!Ready)
            {
                TimeSpan remaining = _finish > DateTime.UtcNow ? _finish - DateTime.UtcNow : TimeSpan.Zero;
                LabelTo(from, $"Assembling: {remaining:hh\\:mm\\:ss} remaining");
            }
            else LabelTo(from, "Ready to unpack");
        }

        public override void OnAfterDelete()
        {
            base.OnAfterDelete();
            _tick?.Stop();
            _tick = null;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (!Ready)
            {
                from.SendMessage("Assembly is still in progress.");
                return;
            }

            if (!from.Alive)
                return;

            BaseCreature pet = CreatePet();
            if (pet == null)
            {
                from.SendMessage("This crate appears defective.");
                return;
            }
            if (Prototype && BlueprintSettings.PrototypeEnabled && pet != null)
            {
                try
                {
                    // Durability penalty: reduce MaxDurability, clamp Durability
                    var maxProp = pet.GetType().GetProperty("MaxDurability", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var curProp = pet.GetType().GetProperty("Durability",    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (maxProp != null && curProp != null && maxProp.PropertyType == typeof(int) && curProp.PropertyType == typeof(int))
                    {
                        int max = (int)maxProp.GetValue(pet, null);
                        int loss = (int)Math.Round(max * BlueprintSettings.PrototypeDurabilityPenalty);
                        int newMax = Math.Max(1, max - loss);
                        maxProp.SetValue(pet, newMax, null);

                        int cur = (int)curProp.GetValue(pet, null);
                        if (cur > newMax) curProp.SetValue(pet, newMax, null);
                    }

                    // Optional: flag the frame as prototype if method exists
                    var setProto = pet.GetType().GetMethod("SetPrototype", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (setProto != null) setProto.Invoke(pet, new object[] { true });
                }
                catch { /* safe no-op if not a mechanical pet */ }
            }

            if (!pet.Controlled)
                pet.SetControlMaster(from);

            if ((from.Followers + pet.ControlSlots) > from.FollowersMax)
            {
                from.SendMessage("You have too many followers to unpack this crate.");
                pet.Delete();
                return;
            }

            pet.MoveToWorld(from.Location, from.Map);
            from.SendMessage("You release the mechanical pet from the crate.");
            Delete();
        }

        protected abstract BaseCreature CreatePet();
        // --- Phase 3: blueprint/prototype ---
        [CommandProperty(AccessLevel.GameMaster)]
        public bool Prototype { get; private set; }

        protected virtual BlueprintId? RequiredBlueprint => null;

        protected double EffectiveFailChance =>
            Math.Min(1.0, AssemblyFailureChance +
                (Prototype && BlueprintSettings.PrototypeEnabled ? BlueprintSettings.PrototypeFailBonus : 0.0));


        // ICraftable
        public int OnCraft(int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, ITool tool, CraftItem craftItem, int resHue)
        {
            // Mark prototype if the player hasn't learned this crate's blueprint
            Prototype = false;
            var bp = RequiredBlueprint;
            if (bp.HasValue && !BlueprintStore.IsLearned(from, bp.Value))
            {
                if (!BlueprintSettings.PrototypeEnabled)
                {
                    from.SendMessage(38, "You have not learned this blueprint.");
                    return 1044037; // generic craft failure code
                }

                Prototype = true;

                if (BlueprintSettings.PrototypeMakersMarkDisabled)
                    makersMark = false;

                from.SendMessage(0x22, "Crafting as a prototype.");
            }

            // Start the assembly like before
            if (!Deleted)
                BeginAssembly(from);

            return quality;
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1);
            writer.Write(ProductionTime);
            writer.Write(Ready);
            writer.Write(_finish);
            writer.Write(_crafter);
            writer.Write(_started);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            ProductionTime = reader.ReadTimeSpan();
            Ready = reader.ReadBool();
            _finish = reader.ReadDateTime();
            _crafter = reader.ReadMobile();
            _started = version >= 1 && reader.ReadBool();

            if (!Ready)
            {
                if (_finish == DateTime.MinValue)
                    BeginAssembly(_crafter);
                else if (_finish > DateTime.UtcNow)
                {
                    _started = true;
                    StartTick();
                    Timer.DelayCall(_finish - DateTime.UtcNow, CompleteAssembly);
                }
                else
                    CompleteAssembly();
            }
        }
    }

    public class ClockworkSpiderCrate : MechanicalPetCrate
    {
        [Constructable] public ClockworkSpiderCrate() : base(TimeSpan.FromMinutes(60)) { }
        protected override BlueprintId? RequiredBlueprint => BlueprintId.ClockworkSpiderCrate;
        public ClockworkSpiderCrate(Serial s) : base(s) { }
        protected override BaseCreature CreatePet() => CreatePetByName("ClockworkSpider");
        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
        public override void OnSingleClick(Mobile from) { LabelTo(from, "clockwork spider crate"); base.OnSingleClick(from); }
    }

    public class AutonomousMuleCrate : MechanicalPetCrate
    {
        [Constructable] public AutonomousMuleCrate() : base(TimeSpan.FromMinutes(30)) { }
        protected override BlueprintId? RequiredBlueprint => BlueprintId.AutonomousMuleCrate;

        public AutonomousMuleCrate(Serial s) : base(s) { }
        protected override BaseCreature CreatePet() => CreatePetByName("AutonomousMule");
        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
        public override void OnSingleClick(Mobile from) { LabelTo(from, "autonomous mule crate"); base.OnSingleClick(from); }
    }
}