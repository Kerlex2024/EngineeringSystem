// ============================================================================
// File: Scripts/Custom/Engineering/Tools/EngineeringTools.cs
// ============================================================================
using System;
using Server;
using Server.Items;
using Server.Engines.Craft;
using Server.Custom.Engineering;

namespace Server.Custom.Engineering
{
    public class EngineeringTools : BaseTool
    {
        public override CraftSystem CraftSystem => EngineeringCraftSystem.Instance;

        [Constructable]
        public EngineeringTools() : base(0x1EB8)
        {
            Name = "engineering tools";
            Weight = 2.0;
            UsesRemaining = 50;
            ShowUsesRemaining = true;
        }

        [Constructable]
        public EngineeringTools(int uses) : base(uses, 0x1EB8)
        {
            Name = "engineering tools";
            Weight = 2.0;
            ShowUsesRemaining = true;
        }

        public EngineeringTools(Serial s) : base(s) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            var system = CraftSystem;
            int num = system.CanCraft(from, this, null);

            if (num > 0)
            {
                from.SendLocalizedMessage(num);
                return;
            }

            from.SendGump(new CraftGump(from, system, this, null));
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }
}