// ============================================================================
// File: Scripts/Custom/Engineering/Items/PackingCrate.cs
// ============================================================================
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Server.Custom.Engineering;
using System.Reflection;


namespace Server.Custom.Engineering
{
    public class PackingCrate : Item
    {
        [CommandProperty(AccessLevel.GameMaster)]
        public Serial PackedPetSerial { get; private set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public string PackedPetName { get; private set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public bool IsPacked => PackedPetSerial.IsMobile;

        [Constructable]
        public PackingCrate() : base(0xE3F)
        {
            Name = "packing crate";
            Weight = 5.0;
        }

        public PackingCrate(Serial s) : base(s) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            if (!IsPacked)
            {
                from.Target = new PackTarget(this);
                from.SendMessage("Target your mechanical pet to pack it into this crate.");
            }
            else
            {
                TryUnpack(from);
            }
        }

        private class PackTarget : Target
        {
            private readonly PackingCrate _crate;
            public PackTarget(PackingCrate crate) : base(12, false, TargetFlags.None) { _crate = crate; }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (_crate == null || _crate.Deleted) return;

                if (targeted is BaseCreature pet)
                {
                    if (!pet.Controlled || pet.ControlMaster != from)
                    {
                        from.SendMessage("You can only pack a mechanical pet you control.");
                        return;
                    }

                    // D3: allow packing anywhere (even houses).
                    pet.Internalize();
                    _crate.PackedPetSerial = pet.Serial;
                    _crate.PackedPetName = pet.Name ?? "mechanical pet";
                    _crate.Name = $"packing crate ({_crate.PackedPetName})";
                    from.SendMessage("You carefully pack the pet into the crate.");
                    _crate.InvalidateProperties();
                }
                else
                {
                    from.SendMessage("That is not a mechanical pet.");
                }
            }
        }

        private void TryUnpack(Mobile from)
        {
            var pet = World.FindMobile(PackedPetSerial) as BaseCreature;
            if (pet == null || pet.Deleted)
            {
                from.SendMessage(38, "The packed pet could not be found.");
                return;
            }

            if (!pet.Controlled || pet.ControlMaster != from)
            {
                from.SendMessage("Only the controlling owner can unpack this pet.");
                return;
            }

            // Safeguard: follower slots
            if ((from.Followers + pet.ControlSlots) > from.FollowersMax)
            {
                int needed = (from.Followers + pet.ControlSlots) - from.FollowersMax;
                from.SendMessage(38, $"You need {needed} more follower slot(s) to unpack this pet.");
                return;
            }

            // Region restrictions intentionally relaxed per D3 (allowed anywhere).
            bool isCritical = false;
            var critProp = pet.GetType().GetProperty(
                "IsCriticalDurability",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (critProp != null && critProp.PropertyType == typeof(bool))
            {
                isCritical = (bool)critProp.GetValue(pet, null);
            }

            if (isCritical)
            {
                from.SendMessage(38, "The pet is in critical condition and cannot be unpacked until repaired.");
                return;
            }
            
            pet.MoveToWorld(from.Location, from.Map);
            from.SendMessage("You release the mechanical pet from the crate.");
            Delete();
        }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            if (IsPacked)
                list.Add("Contains: {0}", PackedPetName);
            else
                list.Add("Empty");
        }

        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
            writer.Write(0);

            var pet = World.FindMobile(PackedPetSerial);
            writer.Write(pet);                 // write Mobile ref (may be null)
            writer.Write(PackedPetName);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
            reader.ReadInt();

            Mobile pet = reader.ReadMobile();  // read Mobile, store Serial safely
            PackedPetSerial = pet != null ? pet.Serial : Serial.MinusOne;
            PackedPetName = reader.ReadString();

            if (IsPacked && string.IsNullOrEmpty(Name))
                Name = $"packing crate ({PackedPetName})";
        }
    }
}