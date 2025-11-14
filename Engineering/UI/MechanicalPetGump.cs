// ============================================================================
// File: Scripts/Custom/MechanicalPets/UI/MechanicalPetGump.cs
// ============================================================================
using System;
using Server.Gumps;
using Server;
using Server.Custom.Engineering;
using Server.Custom.Engineering.Pets;
using BaseMechanicalPet = Server.Custom.Engineering.BaseMechanicalPet;

namespace Server.Custom.Engineering
{
    public class MechanicalPetGump : Gump
    {
        private readonly BaseMechanicalPet _pet;
        private readonly Mobile _from;

        public MechanicalPetGump(BaseMechanicalPet pet, Mobile from) : base(50, 50)
        {
            _pet = pet; _from = from;
            Closable = true; Disposable = true; Dragable = true; Resizable = false;

            AddPage(0);
            AddBackground(0, 0, 360, 210, 9270);
            AddHtml(15, 10, 330, 20, $"<BASEFONT COLOR=#FFFFFF><CENTER>{_pet.Name} — Equipment</CENTER></BASEFONT>", false, false);

            AddLabel(15, 40, 0x480, $"Durability: {_pet.Durability}/{_pet.MaxDurability}");
            AddLabel(15, 60, 0x480, $"Capacity: {_pet.UsedCapacity}/{_pet.EquipmentCapacity}");

            // Slots
            AddLabel(15, 90, 0x480, "Plating:");
            AddButton(120, 90, 4005, 4007, 1, GumpButtonType.Reply, 0); AddLabel(150, 90, 0x480, "Remove");

            AddLabel(15, 110, 0x480, "Servo:");
            AddButton(120, 110, 4005, 4007, 2, GumpButtonType.Reply, 0); AddLabel(150, 110, 0x480, "Remove");

            AddLabel(15, 130, 0x480, "Array:");
            AddButton(120, 130, 4005, 4007, 3, GumpButtonType.Reply, 0); AddLabel(150, 130, 0x480, "Remove");

            AddLabel(15, 150, 0x480, "Core:");
            AddButton(120, 150, 4005, 4007, 4, GumpButtonType.Reply, 0); AddLabel(150, 150, 0x480, "Remove");

            AddButton(260, 175, 4005, 4007, 10, GumpButtonType.Reply, 0); AddLabel(290, 175, 0x480, "Close");
        }

        public override void OnResponse(Server.Network.NetState sender, RelayInfo info)
        {
            if (_pet == null || _pet.Deleted || _from == null) return;
            if (!_pet.Controlled || _pet.ControlMaster != _from) return;

            switch (info.ButtonID)
            {
                case 1: _pet.RemoveEquipment(EquipSlot.Plating, _from); break;
                case 2: _pet.RemoveEquipment(EquipSlot.Servo, _from); break;
                case 3: _pet.RemoveEquipment(EquipSlot.Array, _from); break;
                case 4: _pet.RemoveEquipment(EquipSlot.Core, _from); break;
                default: break;
            }
        }
    }
}