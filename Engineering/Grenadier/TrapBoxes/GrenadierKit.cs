// ============================================================================
// File: Scripts/Custom/Engineering/Grenadier/GrenadierKit.cs  (reload Claymore)
// ============================================================================
using Server;
using Server.Items;
using Server.Custom.Engineering;
using Server.Custom.Engineering.Grenadier.TrapBoxes;

namespace Server.Custom.Engineering.Grenadier.TrapBoxes
{
    public class GrenadierKit : Item
    {
        [Constructable]
        public GrenadierKit() : base(0x1EBA) // tool graphic
        {
            Name = "grenadier kit";
            Hue = 2101;
            Weight = 1.0;
        }

        public GrenadierKit(Serial s) : base(s) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack)) { from.SendLocalizedMessage(1060640); return; }

            var loc = from.Location; var map = from.Map; if (map == null) return;

            ClaymoreBox nearest = null; int best = 3; // within 3 tiles
            foreach (Item it in map.GetItemsInRange(loc, 3))
            {
                if (it is ClaymoreBox box && box.Owner == from)
                {
                    int d = (int)box.GetDistanceToSqrt(loc);
                    if (d < best) { best = d; nearest = box; }
                }
            }

            if (nearest == null) { from.SendMessage(38, "No owned claymore nearby to reload."); return; }

            nearest.Charges += 3; // +3 charges per kit
            from.SendMessage("You reload the claymore (+3 charges).");
            Consume();
        }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }
}
