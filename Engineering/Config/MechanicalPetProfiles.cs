// ============================================================================
// File: Scripts/Custom/Engineering/Config/MechanicalPetProfiles.cs
// Purpose: Profile-driven, hot-reloadable configuration for mechanical pets.
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Server;
using Server.Mobiles;

namespace Server.Custom.Engineering
{
    [Flags]
    public enum PetAbilityFlags
    {
        None             = 0,
        HeatTraverse     = 1 << 0, // lava/heat tiles allowed
        SwitchActuation  = 1 << 1, // triggers plates/switches
        DecodeLocks      = 1 << 2, // t-map side lockboxes decode
        Grapple          = 1 << 3, // ledge traverse hooks
        TreasureAssist   = 1 << 4, // carry/decode extras
        PackMule         = 1 << 5, // bonus pack capacity
        Mountable        = 1 << 6  // can be mounted (use carefully)
    }

    public sealed class PetProfile
    {
        // Core
        public int ControlSlots { get; set; } = 4;
        public int EquipmentCapacity { get; set; } = 4;

        // Durability / upkeep
        public int MaxDurability { get; set; } = 1000;
        public double CriticalFrac { get; set; } = 0.10;
        public int RepairPerCharge { get; set; } = 50;   // Phase 2 kit synergy
        public int RepairWeightPer5 { get; set; } = 20;  // 5 charges weigh 20 stones

        // Behavior / capabilities
        public bool Mountable { get; set; } = false;
        public PetAbilityFlags Abilities { get; set; } = PetAbilityFlags.None;
        public string AllowedModulesCsv { get; set; } = ""; // names, comma-separated
        public int PlatingCap { get; set; } = 2; // per-slot capacity caps (engineering equipment)
        public int ServoCap { get; set; } = 2;
        public int ArrayCap { get; set; } = 2;
        public int CoreCap { get; set; } = 2;

        // Economy / crate
        public int CrateMinutes { get; set; } = 30;
        public double FailChance { get; set; } = 0.15;

        // Utility payloads
        public int CargoStones { get; set; } = 0; // for pack mules
        public string Notes { get; set; } = "";

        public bool HasAbility(PetAbilityFlags flag) => (Abilities & flag) != 0;
    }

    public static class MechanicalPetProfiles
    {
        private static readonly Dictionary<string, PetProfile> _byName = new Dictionary<string, PetProfile>(StringComparer.OrdinalIgnoreCase);
        private static readonly string _path = Path.Combine(Core.BaseDirectory, "Data/Engineering/MechanicalPetProfiles.xml");

        public static IReadOnlyDictionary<string, PetProfile> All => _byName;

        static MechanicalPetProfiles()
        {
            EnsureDefaults();
            Load();
        }

        public static void Reload()
        {
            try
            {
                Load();
                Console.WriteLine("[MechanicalPetProfiles] Reloaded.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MechanicalPetProfiles] Reload failed: {0}", ex);
            }
        }

        public static PetProfile GetByTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                return GetDefault();

            if (_byName.TryGetValue(typeName, out var p))
                return p;

            return GetDefault();
        }

        public static PetProfile GetFor(BaseCreature bc)
            => bc == null ? GetDefault() : GetByTypeName(bc.GetType().Name);

        public static PetProfile GetDefault()
            => _byName.TryGetValue("Default", out var p) ? p : new PetProfile();

        // ----------------- internal: XML I/O -----------------
        private static void EnsureDefaults()
        {
            if (File.Exists(_path))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? Core.BaseDirectory);

            using (var w = XmlWriter.Create(_path, new XmlWriterSettings { Indent = true }))
            {
                w.WriteStartElement("MechanicalPetProfiles");

                // Default applies to any pet not explicitly listed
                WriteProfile(w, "Default", new PetProfile
                {
                    ControlSlots = 4,
                    EquipmentCapacity = 4,
                    MaxDurability = 1000,
                    CriticalFrac = 0.10,
                    RepairPerCharge = 50,
                    RepairWeightPer5 = 20,
                    Abilities = PetAbilityFlags.None,
                    PlatingCap = 2, ServoCap = 2, ArrayCap = 2, CoreCap = 2,
                    CrateMinutes = 30,
                    FailChance = 0.15,
                    CargoStones = 0,
                    Notes = "Default profile for any mechanical pet."
                });

                // Clockwork Spider – utility scout, optional mountable via config
                WriteProfile(w, "ClockworkSpider", new PetProfile
                {
                    ControlSlots = 4,
                    EquipmentCapacity = 4,
                    MaxDurability = 900,
                    CriticalFrac = 0.10,
                    Abilities = PetAbilityFlags.SwitchActuation | PetAbilityFlags.DecodeLocks | PetAbilityFlags.Mountable,
                    Mountable = true,
                    CrateMinutes = 60,
                    FailChance = 0.15,
                    Notes = "Entry tier (Gather/Craft) — mountable scout; switch + decoder utility."
                });

                // Autonomous Mule – pack specialist
                WriteProfile(w, "AutonomousMule", new PetProfile
                {
                    ControlSlots = 4,
                    EquipmentCapacity = 4,
                    MaxDurability = 1100,
                    CriticalFrac = 0.10,
                    Abilities = PetAbilityFlags.PackMule | PetAbilityFlags.TreasureAssist | PetAbilityFlags.Mountable,
                    CargoStones = 500,
                    Mountable = true,
                    CrateMinutes = 30,
                    FailChance = 0.12,
                    Notes = "Entry tier — mountable cargo specialist."
                });

                
                // Mid: Drone (Warrior utility)
                WriteProfile(w, "MechanicalDrone", new PetProfile
                {
                    ControlSlots = 4,
                    EquipmentCapacity = 5,  // +1 vs entry
                    MaxDurability = 1000,
                    CriticalFrac = 0.10,
                    Abilities = PetAbilityFlags.SwitchActuation, // light utility
                    CrateMinutes = 90,
                    FailChance = 0.18,
                    Notes = "Mid tier — more capacity, longer assembly."
                });

                // High: Minion (Tamer/Mage utility)
                WriteProfile(w, "MechanicalMinion", new PetProfile
                {
                    ControlSlots = 4,
                    EquipmentCapacity = 6,
                    MaxDurability = 1150,
                    CriticalFrac = 0.10,
                    Abilities = PetAbilityFlags.DecodeLocks | PetAbilityFlags.SwitchActuation,
                    CrateMinutes = 120,
                    FailChance = 0.22,
                    Notes = "High tier — expanded capacity; utility focus."
                });

                // High: Overseer (alt high)
                WriteProfile(w, "MechanicalOverseer", new PetProfile
                {
                    ControlSlots = 4,
                    EquipmentCapacity = 6,
                    MaxDurability = 1200,
                    CriticalFrac = 0.10,
                    Abilities = PetAbilityFlags.DecodeLocks | PetAbilityFlags.Grapple,
                    CrateMinutes = 120,
                    FailChance = 0.22,
                    Notes = "High tier — decoder + grapple hooks if enabled."
                });

                // Prestige: Juggernaut (slots+cosmetic, no stat creep)
                WriteProfile(w, "MechanicalJuggernaut", new PetProfile
                {
                    ControlSlots = 4,
                    EquipmentCapacity = 7, // prestige bump (cosmetic plating encouraged)
                    MaxDurability = 1250,
                    CriticalFrac = 0.10,
                    Abilities = PetAbilityFlags.SwitchActuation, // utility, not damage
                    CrateMinutes = 180,
                    FailChance = 0.25,
                    Notes = "Prestige tier — extra equip capacity + cosmetic identity; no raw power creep."
                });

                w.WriteEndElement();
                w.Flush();
            }
        }

        private static void WriteProfile(XmlWriter w, string name, PetProfile p)
        {
            w.WriteStartElement("Pet");
            w.WriteAttributeString("Name", name);
            w.WriteElementString("ControlSlots", p.ControlSlots.ToString());
            w.WriteElementString("EquipmentCapacity", p.EquipmentCapacity.ToString());
            w.WriteElementString("MaxDurability", p.MaxDurability.ToString());
            w.WriteElementString("CriticalFrac", p.CriticalFrac.ToString(System.Globalization.CultureInfo.InvariantCulture));
            w.WriteElementString("RepairPerCharge", p.RepairPerCharge.ToString());
            w.WriteElementString("RepairWeightPer5", p.RepairWeightPer5.ToString());
            w.WriteElementString("Mountable", p.Mountable.ToString());
            w.WriteElementString("Abilities", ((int)p.Abilities).ToString());
            w.WriteElementString("AllowedModules", p.AllowedModulesCsv ?? "");
            w.WriteElementString("PlatingCap", p.PlatingCap.ToString());
            w.WriteElementString("ServoCap", p.ServoCap.ToString());
            w.WriteElementString("ArrayCap", p.ArrayCap.ToString());
            w.WriteElementString("CoreCap", p.CoreCap.ToString());
            w.WriteElementString("CrateMinutes", p.CrateMinutes.ToString());
            w.WriteElementString("FailChance", p.FailChance.ToString(System.Globalization.CultureInfo.InvariantCulture));
            w.WriteElementString("CargoStones", p.CargoStones.ToString());
            w.WriteElementString("Notes", p.Notes ?? "");
            w.WriteEndElement();
        }

        private static void Load()
        {
            _byName.Clear();

            var doc = new XmlDocument();
            doc.Load(_path);

            var root = doc["MechanicalPetProfiles"];
            if (root == null)
                throw new InvalidDataException("MechanicalPetProfiles root not found.");

            foreach (XmlElement node in root.GetElementsByTagName("Pet"))
            {
                var name = node.GetAttribute("Name");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                PetProfile p = new PetProfile
                {
                    ControlSlots     = ReadInt(node, "ControlSlots", 4),
                    EquipmentCapacity= ReadInt(node, "EquipmentCapacity", 4),
                    MaxDurability    = ReadInt(node, "MaxDurability", 1000),
                    CriticalFrac     = ReadDouble(node, "CriticalFrac", 0.10),
                    RepairPerCharge  = ReadInt(node, "RepairPerCharge", 50),
                    RepairWeightPer5 = ReadInt(node, "RepairWeightPer5", 20),
                    Mountable        = ReadBool(node, "Mountable", false),
                    Abilities        = (PetAbilityFlags)ReadInt(node, "Abilities", 0),
                    AllowedModulesCsv= ReadString(node, "AllowedModules", ""),
                    PlatingCap       = ReadInt(node, "PlatingCap", 2),
                    ServoCap         = ReadInt(node, "ServoCap", 2),
                    ArrayCap         = ReadInt(node, "ArrayCap", 2),
                    CoreCap          = ReadInt(node, "CoreCap", 2),
                    CrateMinutes     = ReadInt(node, "CrateMinutes", 30),
                    FailChance       = ReadDouble(node, "FailChance", 0.15),
                    CargoStones      = ReadInt(node, "CargoStones", 0),
                    Notes            = ReadString(node, "Notes", "")
                };

                _byName[name] = p;
            }
        }

        private static int ReadInt(XmlElement e, string n, int d)
            => int.TryParse(e[n]?.InnerText, out var v) ? v : d;

        private static double ReadDouble(XmlElement e, string n, double d)
            => double.TryParse(e[n]?.InnerText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : d;

        private static bool ReadBool(XmlElement e, string n, bool d)
            => bool.TryParse(e[n]?.InnerText, out var v) ? v : d;

        private static string ReadString(XmlElement e, string n, string d)
            => e[n]?.InnerText ?? d;
    }
}