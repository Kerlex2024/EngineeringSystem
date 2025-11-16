// ============================================================================
// File: Scripts/Custom/Engineering/Equipment/PetEquipment.cs
// ============================================================================
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Server.Custom.Engineering;

namespace Server.Custom.Engineering
{
    public enum EquipSlot { Plating, Servo, Array, Core }

    public abstract class PetEquipment : Item
    {
        public abstract EquipSlot Slot { get; }
        public abstract int CapacityCost { get; } // capacity budget used
        public virtual string TierName => "Standard";

        protected PetEquipment(int itemID) : base(itemID)
        {
            Weight = 1.0;
            LootType = LootType.Regular;
        }

        public PetEquipment(Serial s) : base(s) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.Target = new InstallTarget(this);
            from.SendMessage("Target your mechanical pet to install this equipment.");
        }

        private class InstallTarget : Target
        {
            private readonly PetEquipment _eq;
            public InstallTarget(PetEquipment eq) : base(12, false, TargetFlags.None) { _eq = eq; }

            protected override void OnTarget(Mobile from, object o)
            {
                if (_eq == null || _eq.Deleted) return;

                if (o is BaseCreature pet)
                {
                    if (!pet.Controlled || pet.ControlMaster != from)
                    {
                        from.SendMessage("You can only equip a creature you control.");
                        return;
                    }

                    var mi = pet.GetType().GetMethod(
                        "InstallEquipment",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                    if (mi == null)
                    {
                        from.SendMessage("This creature cannot accept mechanical equipment.");
                        return;
                    }

                    // expected signature: bool InstallEquipment(PetEquipment eq, Mobile actor)
                    var ok = mi.Invoke(pet, new object[] { _eq, from });
                    if (ok is bool b && b)
                    {
                        from.SendMessage("Equipment installed.");
                        // InstallEquipment will delete the item if it consumes it; if not, do it here:
                        if (!_eq.Deleted) _eq.Delete();
                    }
                    else
                    {
                        from.SendMessage("The equipment could not be installed.");
                    }
                }
                else from.SendMessage("That is not a valid creature.");
            }

        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Slot: {Slot}");
            list.Add($"Capacity Cost: {CapacityCost}");
            list.Add($"Tier: {TierName}");
        }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    // Concrete items (ensure both Serialize/Deserialize exist)
    public class Plating_Heat : PetEquipment
    {
        [Constructable] public Plating_Heat() : base(0x13F8) { Name = "heat plating"; }
        public Plating_Heat(Serial s) : base(s) { }

        public override EquipSlot Slot => EquipSlot.Plating;
        public override int CapacityCost => 2;

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    public class Servo_Stability : PetEquipment
    {
        [Constructable] public Servo_Stability() : base(0x105B) { Name = "stability servo"; }
        public Servo_Stability(Serial s) : base(s) { }

        public override EquipSlot Slot => EquipSlot.Servo;
        public override int CapacityCost => 1;

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    public class Array_Decoder : PetEquipment
    {
        [Constructable] public Array_Decoder() : base(0x1F2E) { Name = "decoder array"; }
        public Array_Decoder(Serial s) : base(s) { }

        public override EquipSlot Slot => EquipSlot.Array;
        public override int CapacityCost => 2;

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    public class Core_Standard : PetEquipment
    {
        [Constructable] public Core_Standard() : base(0x1F19) { Name = "standard core"; }
        public Core_Standard(Serial s) : base(s) { }

        public override EquipSlot Slot => EquipSlot.Core;
        public override int CapacityCost => 1;

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
}
}