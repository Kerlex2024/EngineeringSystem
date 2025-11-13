using System;
using Server.Custom.Engineering;
using Server; 

namespace Server.Custom.Engineering.Grenadier.Bombs.Poison
{
    public partial class MegaPoisonBomb : BasePoisonBomb
    {
        [Constructable]
        public MegaPoisonBomb() : base(Server.Items.PotionEffect.ExplosionGreater
)
        {
            Hue = 2511;
            Name = "Mega Poison Bomb";
        }

        public MegaPoisonBomb(Serial serial) : base(serial) { }

        public override int MinDamage => 50;
        public override int MaxDamage => 100;

        // Leaf shouldn't duplicate region/cooldown; base handles it.
        public override void Drink(Mobile from)
        {
            if (from.Skills.Alchemy.Value < 100.0)
            { from.SendMessage("You lack the alchemy skill to use this potion."); return; }

            base.Drink(from);
        }

        public override void Serialize(GenericWriter writer) { base.Serialize(writer); writer.Write(0); }
        public override void Deserialize(GenericReader reader) { base.Deserialize(reader); reader.ReadInt(); }
    }
}
