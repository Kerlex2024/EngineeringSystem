// ============================================================================
// Fix: add missing usings so CommandSystem resolves on Pub57
// ============================================================================
using System;
using Server;
using Server.Mobiles;
using Server.Targeting;    // <- target a pet
using Server.Commands;     // <- CommandSystem lives here

namespace Server.Custom.Engineering
{
    public static class MechanicalPetProfileCommands
    {
        // ServUO calls any public static Initialize() at boot
        public static void Initialize()
        {
            CommandSystem.Register("mp.profiles.reload", AccessLevel.GameMaster, e =>
            {
                MechanicalPetProfiles.Reload();
                e.Mobile.SendMessage(0x55, "MechanicalPetProfiles reloaded.");
            });

            CommandSystem.Register("mp.profile", AccessLevel.GameMaster, e =>
            {
                var from = e.Mobile;
                var args = e.ArgString?.Trim();

                if (!string.IsNullOrEmpty(args))
                {
                    var p = MechanicalPetProfiles.GetByTypeName(args);
                    DumpProfile(from, args, p);
                    return;
                }

                from.Target = new ProfileTarget();
                from.SendMessage("Target a mechanical pet to view its profile.");
            });
        }

        private static void DumpProfile(Mobile to, string name, PetProfile p)
        {
            to.SendMessage(0x55, $"-- Profile: {name} --");
            to.SendMessage($"Slots: {p.ControlSlots}, EquipCap: {p.EquipmentCapacity}, MaxDur: {p.MaxDurability} (crit {p.CriticalFrac:P0})");
            to.SendMessage($"Mountable: {p.Mountable}, Abilities: {(int)p.Abilities} ({p.Abilities})");
            to.SendMessage($"Caps P/S/A/C: {p.PlatingCap}/{p.ServoCap}/{p.ArrayCap}/{p.CoreCap}");
            to.SendMessage($"Crate: {p.CrateMinutes}m, Fail: {p.FailChance:P0}, Cargo: {p.CargoStones}");
            to.SendMessage($"Repair: +{p.RepairPerCharge}/charge, weight per 5: {p.RepairWeightPer5}");
            if (!string.IsNullOrEmpty(p.AllowedModulesCsv))
                to.SendMessage($"AllowedModules: {p.AllowedModulesCsv}");
            if (!string.IsNullOrEmpty(p.Notes))
                to.SendMessage($"Notes: {p.Notes}");
        }

        private sealed class ProfileTarget : Target
        {
            public ProfileTarget() : base(12, false, TargetFlags.None) { }

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is BaseCreature bc)
                {
                    var tname = bc.GetType().Name;
                    var p = MechanicalPetProfiles.GetFor(bc);
                    DumpProfile(from, tname, p);
                }
                else
                {
                    from.SendMessage(38, "That is not a valid creature.");
                }
            }
        }
    }
}
