// ============================================================================
// File: Scripts/Custom/MechanicalPets/Pets/BaseMechanicalPet.cs  (UPDATED)
// ============================================================================
using System;
using System.Collections.Generic;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Custom.Engineering;

namespace Server.Custom.Engineering
{
    public interface IMechanicalUtility
    {
        bool CanActivatePlates { get; }
        bool CanTraverseHeat { get; }
    }

    public abstract class BaseMechanicalPet : BaseCreature, IMechanicalUtility
    {
        private readonly HashSet<Type> _modules = new HashSet<Type>();
        private readonly Dictionary<EquipSlot, PetEquipment> _equipment = new Dictionary<EquipSlot, PetEquipment>();

        // --- Durability & capacity ---
        [CommandProperty(AccessLevel.GameMaster)]
        public int MaxDurability { get; set; } = 1000;

        [CommandProperty(AccessLevel.GameMaster)]
        public int Durability { get; set; } = 1000;

        [CommandProperty(AccessLevel.GameMaster)]
        public double CriticalDurabilityFraction { get; set; } = 0.10;

        [CommandProperty(AccessLevel.GameMaster)]
        public int EquipmentCapacity { get; set; } = 4;

        public int UsedCapacity
        {
            get
            {
                int sum = 0;
                foreach (var kv in _equipment) if (kv.Value != null) sum += kv.Value.CapacityCost;
                return sum;
            }
        }

        public int RemainingCapacity => Math.Max(0, EquipmentCapacity - UsedCapacity);
        public bool IsCriticalDurability => Durability <= (int)(MaxDurability * CriticalDurabilityFraction);

        public IReadOnlyCollection<Type> InstalledModuleTypes => _modules;
        public override FoodType FavoriteFood => FoodType.None;
        public override bool BardImmune => true;
        public override bool BleedImmune => true;

        public bool CanActivatePlates => _modules.Contains(typeof(Module_SwitchActuator));
        public bool CanTraverseHeat
            => _modules.Contains(typeof(Module_HeatShielding)) || _equipment.ContainsKey(EquipSlot.Plating) && _equipment[EquipSlot.Plating] is Plating_Heat;

        protected BaseMechanicalPet(AIType ai, FightMode mode, int perception, int fight, double activeSpeed, double passiveSpeed)
            : base(ai, mode, perception, fight, activeSpeed, passiveSpeed)
        {
            Tamable = true;
        }

        // --- Equipment API ---
        public bool InstallEquipment(PetEquipment eq, Mobile actor)
        {
            if (eq == null || eq.Deleted) return false;

            if (RemainingCapacity < eq.CapacityCost)
            {
                actor?.SendMessage(38, "Not enough capacity for that equipment.");
                return false;
            }

            if (_equipment.TryGetValue(eq.Slot, out var existing) && existing != null)
            {
                actor?.SendMessage(38, $"The {eq.Slot} slot is already occupied.");
                return false;
            }

            if (!eq.IsChildOf(actor.Backpack))
            {
                actor?.SendLocalizedMessage(1042001);
                return false;
            }

            _equipment[eq.Slot] = eq;
            eq.Delete(); // consume equipment item into the pet
            InvalidateProperties();
            return true;
        }

        public bool RemoveEquipment(EquipSlot slot, Mobile actor)
        {
            if (_equipment.TryGetValue(slot, out var eq) && eq != null)
            {
                _equipment.Remove(slot);
                actor?.AddToBackpack(Activator.CreateInstance(eq.GetType()) as Item);
                InvalidateProperties();
                return true;
            }
            actor?.SendMessage("Nothing installed in that slot.");
            return false;
        }

        // --- Repair ---
        public int UseRepair(int amount)
        {
            int before = Durability;
            Durability = Math.Min(MaxDurability, Durability + amount);
            InvalidateProperties();
            return Durability - before;
        }

        // --- Module API (legacy) ---
        public void InstallModule(Type moduleType, Mobile installer)
        {
            if (_modules.Contains(moduleType))
            {
                installer?.SendMessage("That module is already installed.");
                return;
            }

            _modules.Add(moduleType);
            installer?.SendMessage("Module installed.");
            InvalidateProperties();
        }

        // --- Combat hooks ---
        public override void OnDamage(int amount, Mobile from, bool willKill)
        {
            base.OnDamage(amount, from, willKill);

            // Low per-hit wear
            if (amount > 0)
            {
                int wear = Math.Max(1, amount / 10); // 10% of incoming damage, min 1
                Durability = Math.Max(0, Durability - wear);
                InvalidateProperties();
            }
        }

        public override bool OnBeforeDeath()
        {
            // Heavy wear on death
            Durability = Math.Max(0, Durability - (MaxDurability / 5)); // -20%
            InvalidateProperties();
            return base.OnBeforeDeath();
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Durability: {Durability}/{MaxDurability}");
            list.Add($"Equipment Capacity: {UsedCapacity}/{EquipmentCapacity}");

            if (_equipment.Count > 0)
            {
                foreach (var kv in _equipment)
                {
                    if (kv.Value != null)
                        list.Add($"{kv.Key}: {kv.Value.Name} (Cost {kv.Value.CapacityCost})");
                }
            }
            if (IsCriticalDurability)
                list.Add("Status: CRITICAL (repairs required)");
        }

        public override void OnDoubleClick(Mobile from)
        {
            base.OnDoubleClick(from);

            if (Controlled && ControlMaster == from)
                from.SendGump(new MechanicalPetGump(this, from));
        }

        public BaseMechanicalPet(Serial s) : base(s) { }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(1);
            writer.Write(MaxDurability);
            writer.Write(Durability);
            writer.Write(CriticalDurabilityFraction);
            writer.Write(EquipmentCapacity);

            // modules
            writer.Write(_modules.Count);
            foreach (var t in _modules) writer.Write(t.FullName);

            // equipment
            writer.Write(_equipment.Count);
            foreach (var kv in _equipment)
            {
                writer.Write((int)kv.Key);
                writer.Write(kv.Value != null ? kv.Value.GetType().FullName : string.Empty);
            }
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            int version = reader.ReadInt();

            MaxDurability = reader.ReadInt();
            Durability = reader.ReadInt();
            CriticalDurabilityFraction = reader.ReadDouble();
            EquipmentCapacity = reader.ReadInt();

            // modules
            int mcount = reader.ReadInt();
            _modules.Clear();
            for (int i = 0; i < mcount; i++)
            {
                string name = reader.ReadString();
                Type t = ScriptCompiler.FindTypeByFullName(name);
                if (t != null) _modules.Add(t);
            }

            // equipment
            _equipment.Clear();
            int ecount = reader.ReadInt();
            for (int i = 0; i < ecount; i++)
            {
                var slot = (EquipSlot)reader.ReadInt();
                string eqTypeName = reader.ReadString();
                if (!string.IsNullOrEmpty(eqTypeName))
                {
                    Type t = ScriptCompiler.FindTypeByFullName(eqTypeName);
                    if (t != null && typeof(PetEquipment).IsAssignableFrom(t))
                    {
                        // re-create item instance to display in properties; not placed in world
                        PetEquipment eq = Activator.CreateInstance(t) as PetEquipment;
                        _equipment[slot] = eq;
                    }
                }
            }
        }
    }

    // ------- Example Pet 1: Clockwork Spider (now 4 slots) -------
    public class ClockworkSpider : BaseMechanicalPet
    {
        [Constructable]
        public ClockworkSpider() : base(AIType.AI_Mage, FightMode.Closest, 10, 1, 0.2, 0.4)
        {
            Name = "a clockwork spider";
            Body = 732;
            Hue = 2213;

            SetStr(150, 175);
            SetDex(120, 140);
            SetInt(30, 40);

            SetHits(250, 280);
            SetDamage(8, 12);

            SetSkill(SkillName.Wrestling, 70.0, 85.0);
            SetSkill(SkillName.Tactics, 70.0, 85.0);
            SetSkill(SkillName.MagicResist, 50.0, 70.0);

            Fame = 1000; Karma = 0;

            Tamable = true;
            ControlSlots = 4; // Phase 2: 4 slots per mechpet
            MinTameSkill = 108.0;

            EquipmentCapacity = 4;
        }

        public ClockworkSpider(Serial s) : base(s) { }
    }

    // ------- Example Pet 2: Autonomous Mule (now 4 slots) -------
    public class AutonomousMule : BaseMechanicalPet
    {
        [Constructable]
        public AutonomousMule() : base(AIType.AI_Animal, FightMode.Aggressor, 10, 1, 0.3, 0.6)
        {
            Name = "an autonomous mule";
            Body = 0xE2;

            SetStr(80, 100);
            SetDex(60, 80);
            SetInt(20, 30);

            SetHits(180, 210);
            SetDamage(3, 6);

            SetSkill(SkillName.MagicResist, 25.0, 40.0);

            Fame = 0; Karma = 0;

            Tamable = true;
            ControlSlots = 4;
            MinTameSkill = 95.0;

            EquipmentCapacity = 4;

            if (Backpack is Backpack bp)
            {
                bp.Delete();
                AddItem(new StrongBackpack { Movable = false });
            }
        }

        public override bool CanBeRenamedBy(Mobile from) => true;

        public AutonomousMule(Serial s) : base(s) { }
    }
}