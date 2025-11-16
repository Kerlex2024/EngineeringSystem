// ============================================================================
// File: Scripts/Custom/Engineering/Items/RepairKit.cs
// ============================================================================
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Server.Custom.Engineering;

namespace Server.Custom.Engineering
{
    public class RepairKit : Item
    {
        // Amount == charges. Weight rule: 5 charges -> 20 stones => weight per charge = 4.0
        public const int RepairPerCharge = 50; // easy to tweak later

        [Constructable]
        public RepairKit() : base(0x1EBE)
        {
            Name = "repair kit";
            Stackable = true;
            Weight = 4.0; // per charge
            Amount = 5;   // default 5 charges = 20 stones
        }

        public RepairKit(Serial s) : base(s) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add($"Charges: {Amount}");
            int bundles = (int)Math.Ceiling(Amount / 5.0);
            list.Add($"Weight rule: {bundles * 20} stones for {bundles * 5} charges");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.Target = new RepairTarget(this);
            from.SendMessage("Target your mechanical pet to repair it by one charge.");
        }

        private class RepairTarget : Target
        {
            private readonly RepairKit _kit;
            public RepairTarget(RepairKit kit) : base(12, false, TargetFlags.None) { _kit = kit; }

            protected override void OnTarget(Mobile from, object o)
            {
                if (_kit == null || _kit.Deleted) return;

                if (o is BaseCreature pet)
                {
                    if (!pet.Controlled || pet.ControlMaster != from)
                    {
                        from.SendMessage("You can only repair a creature you control.");
                        return;
                    }

                    if (_kit.Amount <= 0)
                    {
                        from.SendMessage(38, "This kit has no charges.");
                        return;
                    }

                    var mi = pet.GetType().GetMethod(
                        "UseRepair",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                    if (mi == null)
                    {
                        from.SendMessage("This creature cannot be repaired with a kit.");
                        return;
                    }

                    // expected signature: int UseRepair(int amount)
                    var restoredObj = mi.Invoke(pet, new object[] { RepairKit.RepairPerCharge });
                    int restored = (restoredObj is int i) ? i : 0;

                    if (restored > 0)
                    {
                        _kit.Amount -= 1;
                        from.SendMessage($"You repair the pet (+{restored} durability). Remaining charges: {_kit.Amount}.");
                        if (_kit.Amount <= 0) _kit.Delete();
                    }
                    else from.SendMessage("No repairs were needed.");
                }
                else from.SendMessage("That is not a valid creature.");
            }

        }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }
}