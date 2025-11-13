// ============================================================================
// File: Scripts/Custom/Engineering/Equipment/Modules.cs
// Purpose: Legacy on/off modules (kept for Phase-1 utility triggers)
// ============================================================================
using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Server.Custom.Engineering;

namespace Server.Custom.Engineering
{
    public abstract class MechanicalModuleBase : Item
    {
        protected MechanicalModuleBase(int id) : base(id)
        {
            Weight = 1.0;
            LootType = LootType.Regular;
        }

        public MechanicalModuleBase(Serial s) : base(s) { }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001);
                return;
            }

            from.Target = new InstallTarget(this);
            from.SendMessage("Target your mechanical pet to install this module.");
        }

        // --- inside MechanicalModuleBase ---
        private sealed class InstallTarget : Target
        {
            private readonly MechanicalModuleBase _mod;
            public InstallTarget(MechanicalModuleBase mod) : base(12, false, TargetFlags.None) { _mod = mod; }

            protected override void OnTarget(Mobile from, object o)
            {
                if (_mod == null || _mod.Deleted) return;

                // Accept any BaseCreature; use reflection to install if supported
                if (o is BaseCreature pet)
                {
                    if (!pet.Controlled || pet.ControlMaster != from)
                    {
                        from.SendMessage("You can only install modules on a creature you control.");
                        return;
                    }

                    var mi = pet.GetType().GetMethod(
                        "InstallModule",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                    if (mi == null)
                    {
                        from.SendMessage("This creature cannot accept mechanical modules.");
                        return;
                    }

                    // call InstallModule(Type moduleType, Mobile installer)
                    mi.Invoke(pet, new object[] { _mod.GetType(), from });
                    from.SendMessage("Module installed.");
                    _mod.Delete();
                }
                else
                {
                    from.SendMessage("That is not a valid creature.");
                }
            }
        }


        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    public class Module_SwitchActuator : MechanicalModuleBase
    {
        [Constructable] public Module_SwitchActuator() : base(0x105F) { Name = "module: switch actuator"; }
        public Module_SwitchActuator(Serial s) : base(s) { }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    public class Module_HeatShielding : MechanicalModuleBase
    {
        [Constructable] public Module_HeatShielding() : base(0x13F9) { Name = "module: heat shielding"; }
        public Module_HeatShielding(Serial s) : base(s) { }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }
}