// Custom/Engineering/Items/TieredCrates.cs

using System;
using Server;
using Server.Mobiles;

namespace Server.Custom.Engineering
{
    public class DroneCrate : MechanicalPetCrate
    {
        protected override BlueprintId? RequiredBlueprint => BlueprintId.DroneCrate;

        [Constructable] public DroneCrate() : base(TimeSpan.FromMinutes(90)) { }
        public DroneCrate(Serial s) : base(s) { }

        protected override BaseCreature CreatePet() => CreatePetByName("MechanicalDrone");
        public override void OnSingleClick(Mobile from) { LabelTo(from, "mechanical drone crate"); base.OnSingleClick(from); }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    public class MinionCrate : MechanicalPetCrate
    {
        protected override BlueprintId? RequiredBlueprint => BlueprintId.MinionCrate;

        [Constructable] public MinionCrate() : base(TimeSpan.FromMinutes(120)) { }
        public MinionCrate(Serial s) : base(s) { }

        protected override BaseCreature CreatePet() => CreatePetByName("MechanicalMinion");
        public override void OnSingleClick(Mobile from) { LabelTo(from, "mechanical minion crate"); base.OnSingleClick(from); }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    public class OverseerCrate : MechanicalPetCrate
    {
        protected override BlueprintId? RequiredBlueprint => BlueprintId.OverseerCrate;

        [Constructable] public OverseerCrate() : base(TimeSpan.FromMinutes(120)) { }
        public OverseerCrate(Serial s) : base(s) { }

        protected override BaseCreature CreatePet() => CreatePetByName("MechanicalOverseer");
        public override void OnSingleClick(Mobile from) { LabelTo(from, "mechanical overseer crate"); base.OnSingleClick(from); }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    public class JuggernautCrate : MechanicalPetCrate
    {
        protected override BlueprintId? RequiredBlueprint => BlueprintId.JuggernautCrate;

        [Constructable] public JuggernautCrate() : base(TimeSpan.FromMinutes(180)) { }
        public JuggernautCrate(Serial s) : base(s) { }

        protected override BaseCreature CreatePet() => CreatePetByName("MechanicalJuggernaut");
        public override void OnSingleClick(Mobile from) { LabelTo(from, "mechanical juggernaut crate"); base.OnSingleClick(from); }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }
}
