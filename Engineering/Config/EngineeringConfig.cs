// ============================================================================
// File: Scripts/Custom/Engineering/Config/EngineeringConfig.cs
// ============================================================================
using System;
using System.IO;
using System.Xml;
using Server;

namespace Server.Custom.Engineering
{
    public static class EngineeringConfig
    {
        public static TimeSpan CrateRefreshInterval { get; private set; } = TimeSpan.FromSeconds(5);
        public static double AssemblyFailureChanceBase { get; private set; } = 0.15;
        public static bool AllowPackingAnywhere { get; private set; } = true; // D3: Yes
        public static int MaxQueuePerCrafter { get; private set; } = 3;
        public static bool EnableGrenadierTab { get; private set; } = true;   // stub only in M1

        private static readonly string ConfigPath = Path.Combine(Core.BaseDirectory, "Data/EngineeringConfig.xml");

        public static void Load()
        {
            if (!File.Exists(ConfigPath))
                return;

            var doc = new XmlDocument();
            doc.Load(ConfigPath);

            XmlElement root = doc["EngineeringConfig"];
            if (root == null) return;

            CrateRefreshInterval = ReadTimeSpan(root, "CrateRefreshSeconds", CrateRefreshInterval);
            AssemblyFailureChanceBase = ReadDouble(root, "AssemblyFailureChanceBase", AssemblyFailureChanceBase);
            AllowPackingAnywhere = ReadBool(root, "AllowPackingAnywhere", AllowPackingAnywhere);
            MaxQueuePerCrafter = ReadInt(root, "MaxQueuePerCrafter", MaxQueuePerCrafter);
            EnableGrenadierTab = ReadBool(root, "EnableGrenadierTab", EnableGrenadierTab);
        }

        public static void SaveDefaultsIfMissing()
        {
            if (File.Exists(ConfigPath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            using (var w = XmlWriter.Create(ConfigPath, new XmlWriterSettings { Indent = true }))
            {
                w.WriteStartElement("EngineeringConfig");
                w.WriteElementString("CrateRefreshSeconds", "5");
                w.WriteElementString("AssemblyFailureChanceBase", "0.15");
                w.WriteElementString("AllowPackingAnywhere", "true");
                w.WriteElementString("MaxQueuePerCrafter", "3");
                w.WriteElementString("EnableGrenadierTab", "true");
                w.WriteEndElement();
            }
        }

        private static TimeSpan ReadTimeSpan(XmlElement root, string name, TimeSpan fallback)
        {
            var node = root[name];
            if (node == null) return fallback;
            if (int.TryParse(node.InnerText, out int s) && s >= 1 && s <= 300) return TimeSpan.FromSeconds(s);
            return fallback;
        }
        private static int ReadInt(XmlElement root, string name, int fallback)
        {
            var node = root[name];
            if (node == null) return fallback;
            return int.TryParse(node.InnerText, out int v) ? v : fallback;
        }
        private static double ReadDouble(XmlElement root, string name, double fallback)
        {
            var node = root[name];
            if (node == null) return fallback;
            return double.TryParse(node.InnerText, out double v) ? v : fallback;
        }
        private static bool ReadBool(XmlElement root, string name, bool fallback)
        {
            var node = root[name];
            if (node == null) return fallback;
            return bool.TryParse(node.InnerText, out bool v) ? v : fallback;
        }

        static EngineeringConfig()
        {
            try
            {
                SaveDefaultsIfMissing();
                Load();
            }
            catch (Exception e)
            {
                Console.WriteLine($"[EngineeringConfig] load error: {e}");
            }
        }
    }
}