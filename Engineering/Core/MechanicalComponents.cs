// ============================================================================
// File: Scripts/Custom/Engineering/Core/MechanicalComponents.cs
// Purpose: Core player-crafted parts used by Engineering recipes
// Back-compat note: No version ints written. Derived classes override
// Serialize/Deserialize only to satisfy the verifier.
// ============================================================================
using System;
using Server;
using Server.Items;

namespace Server.Custom.Engineering
{
    public abstract class MechanicalPartBase : Item
    {
        public override bool ForceShowProperties => true;

        protected MechanicalPartBase(int itemID) : base(itemID)
        {
            Weight = 1.0;
            Stackable = true;
        }

        public MechanicalPartBase(Serial s) : base(s) { }

        // Base-only serialization – do NOT write version ints
        public override void Serialize(GenericWriter writer)
        {
            base.Serialize(writer);
        }

        public override void Deserialize(GenericReader reader)
        {
            base.Deserialize(reader);
        }
    }

    [Flipable(0x1B73, 0x1B74)]
    public class MicroActuator : MechanicalPartBase
    {
        [Constructable] public MicroActuator() : base(0x1B73) { Name = "micro-actuator"; }
        public MicroActuator(Serial s) : base(s) { }
        public override void Serialize(GenericWriter w) { base.Serialize(w); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); }
    }

    public class ArcanePowerCore : MechanicalPartBase
    {
        [Constructable] public ArcanePowerCore() : base(0x1F19) { Name = "arcane power core"; }
        public ArcanePowerCore(Serial s) : base(s) { }
        public override void Serialize(GenericWriter w) { base.Serialize(w); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); }
    }

    public class AlloyChassis : MechanicalPartBase
    {
        [Constructable] public AlloyChassis() : base(0x1B72) { Name = "precision alloy chassis"; }
        public AlloyChassis(Serial s) : base(s) { }
        public override void Serialize(GenericWriter w) { base.Serialize(w); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); }
    }

    public class SensorArray : MechanicalPartBase
    {
        [Constructable] public SensorArray() : base(0x1F2E) { Name = "sensor array"; }
        public SensorArray(Serial s) : base(s) { }
        public override void Serialize(GenericWriter w) { base.Serialize(w); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); }
    }

    public class ServoBundle : MechanicalPartBase
    {
        [Constructable] public ServoBundle() : base(0x105B) { Name = "servo bundle"; }
        public ServoBundle(Serial s) : base(s) { }
        public override void Serialize(GenericWriter w) { base.Serialize(w); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); }
    }

    public class HeatShielding : MechanicalPartBase
    {
        [Constructable] public HeatShielding() : base(0x13F8) { Name = "heat shielding"; }
        public HeatShielding(Serial s) : base(s) { }
        public override void Serialize(GenericWriter w) { base.Serialize(w); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); }
    }

    public class ArcaneDampener : MechanicalPartBase
    {
        [Constructable] public ArcaneDampener() : base(0x1F14) { Name = "arcane dampener"; }
        public ArcaneDampener(Serial s) : base(s) { }
        public override void Serialize(GenericWriter w) { base.Serialize(w); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); }
    }
}
