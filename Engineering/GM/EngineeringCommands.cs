// ============================================================================
// File: Scripts/Custom/Engineering/GM/EngineeringCommands.cs
// ============================================================================
using System;
using System.Linq;
using Server;
using Server.Mobiles;
using Server.Targeting;
using Server.Custom.Engineering;

namespace Server.Commands
{
    public static class EngineeringCommands
    {
        public static void Initialize()
        {
            CommandSystem.Register("mp.reload", AccessLevel.GameMaster, e =>
            {
                EngineeringConfig.Load();
                Server.Custom.Engineering.Grenadier.GrenadierConfig.ReloadGrenadierVisuals();
                e.Mobile.SendMessage(0x55, "EngineeringConfig reloaded.");
                e.Mobile.SendMessage(0x55, "Grenadier visuals reloaded (hues/wave FX).");
            });


            CommandSystem.Register("mp.stats", AccessLevel.GameMaster, e =>
            {
                var s = EngineeringStats.GetLiveSnapshot();
                e.Mobile.SendMessage(0x55, "-- Engineering Stats --");
                e.Mobile.SendMessage($"Crates (active): {s.ActiveCrates}");
                e.Mobile.SendMessage($"Crates (ready): {s.ReadyCrates}");
                e.Mobile.SendMessage($"Crates (ruined items): {s.RuinedComponents}");
                e.Mobile.SendMessage($"Mechanical pets active: {s.ActiveMechanicalPets}");
                e.Mobile.SendMessage($"Crafted (session): {EngineeringStats.CraftedCratesThisSession}");
            });
        }
    }

    public struct EngineeringSnapshot
    {
        public int ActiveCrates;
        public int ReadyCrates;
        public int RuinedComponents;
        public int ActiveMechanicalPets;
    }

    public static class EngineeringStats
    {
        public static int CraftedCratesThisSession;

        public static void OnCrateCrafted() { CraftedCratesThisSession++; }

        public static EngineeringSnapshot GetLiveSnapshot()
        {
            int activeCrates = 0, readyCrates = 0, ruined = 0, pets = 0;

            foreach (var item in Server.World.Items.Values)
            {
                if (item == null || item.Deleted) continue;

                // Ready/Active crates from your MechanicalPetCrate
                if (item.GetType().Namespace != null && item.GetType().Namespace.Contains("Server.Custom.Engineering"))
                {
                    if (item.GetType().Name.EndsWith("Crate"))
                    {
                        // reflect Ready bool if present
                        bool ready = false;
                        var prop = item.GetType().GetProperty("Ready");
                        if (prop != null && prop.PropertyType == typeof(bool))
                            ready = (bool)prop.GetValue(item, null);

                        if (ready) readyCrates++; else activeCrates++;
                    }

                    // ruined components by name
                    if (item.Name != null && item.Name.IndexOf("ruined mechanical components", StringComparison.OrdinalIgnoreCase) >= 0)
                        ruined++;
                }
            }

            foreach (var m in Server.World.Mobiles.Values)
            {
                if (m == null || m.Deleted) continue;
                if (m.GetType().Namespace != null && m.GetType().Namespace.Contains("Server.Custom.Engineering"))
                {
                    if (m.GetType().Name.Contains("Mule") || m.GetType().Name.Contains("Spider") || m.GetType().Name.Contains("Clockwork") || m.GetType().Name.Contains("Mechanical"))
                        pets++;
                }
            }

            return new EngineeringSnapshot
            {
                ActiveCrates = activeCrates,
                ReadyCrates = readyCrates,
                RuinedComponents = ruined,
                ActiveMechanicalPets = pets
            };
        }
    }
}