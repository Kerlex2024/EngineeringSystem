// ============================================================================
// File: Scripts/Custom/Engineering/Blueprints/Blueprints.cs
// ============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Commands;
using Server.Targeting;
using System.Globalization;

namespace Server.Custom.Engineering
{
    public enum BlueprintId
    {
        ClockworkSpiderCrate = 1,
        AutonomousMuleCrate  = 2,

        // --- Phase 5 (Tiers) ---
        DroneCrate           = 10, // Mid tier (Warrior)
        MinionCrate          = 20, // High tier (Tamer/Mage)
        OverseerCrate        = 21, // High tier (Tamer/Mage)
        JuggernautCrate      = 30  // Prestige (Grenadier/Juggernaut)
    }

    public static class BlueprintStore
    {
        private static readonly Dictionary<Serial, HashSet<int>> _learned = new Dictionary<Serial, HashSet<int>>();
        private static readonly string _path = Path.Combine(Core.BaseDirectory, "Data/Engineering/Blueprints.xml");

        public static void Initialize()
        {
            EventSink.Login += e => EnsureEntry(e.Mobile);
            EventSink.WorldSave += e => Save();
            EnsureFile(); Load();
        }

        private static void EnsureFile()
        {
            var dir = Path.GetDirectoryName(_path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(_path)) return;
            using (var w = XmlWriter.Create(_path, new XmlWriterSettings { Indent = true }))
            {
                w.WriteStartElement("Blueprints"); w.WriteEndElement();
            }
        }

        private static void EnsureEntry(Mobile m)
        {
            if (m == null || m.Deleted) return;
            if (!_learned.ContainsKey(m.Serial))
                _learned[m.Serial] = new HashSet<int>();
        }

        public static bool IsLearned(Mobile m, BlueprintId id)
        {
            if (m == null || m.Deleted) return false;
            EnsureEntry(m);
            return _learned[m.Serial].Contains((int)id);
        }

        public static bool Learn(Mobile m, BlueprintId id)
        {
            if (m == null || m.Deleted) return false;
            EnsureEntry(m);
            if (_learned[m.Serial].Add((int)id)) { m.SendMessage(0x55, $"You learned blueprint: {id}."); return true; }
            m.SendMessage(38, "You already know that blueprint.");
            return false;
        }

        public static IReadOnlyCollection<int> GetLearned(Mobile m)
        {
            EnsureEntry(m);
            return _learned[m.Serial];
        }

        public static void Save()
        {
            try
            {
                using (var w = XmlWriter.Create(_path, new XmlWriterSettings { Indent = true }))
                {
                    w.WriteStartElement("Blueprints");
                    foreach (var kv in _learned)
                    {
                        w.WriteStartElement("Character");
                        w.WriteAttributeString("Serial", kv.Key.Value.ToString("X"));
                        foreach (var id in kv.Value)
                        {
                            w.WriteStartElement("BP");
                            w.WriteAttributeString("Id", id.ToString());
                            w.WriteEndElement();
                        }
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                }
            }
            catch (Exception ex) { Console.WriteLine("[BlueprintStore] Save error: {0}", ex); }
        }

        public static void Load()
        {
            try
            {
                _learned.Clear();
                var doc = new XmlDocument(); doc.Load(_path);
                var root = doc["Blueprints"]; if (root == null) return;

                foreach (XmlElement ch in root.GetElementsByTagName("Character"))
                {
                    var sAttr = ch.GetAttribute("Serial");
                    if (string.IsNullOrWhiteSpace(sAttr)) continue;

                    sAttr = sAttr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? sAttr.Substring(2) : sAttr;
                    if (!int.TryParse(sAttr, System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out var serInt))
                        continue;

                    Serial ser = (Serial)serInt;
                    // var ser = new Serial(serInt);
                    var set = new HashSet<int>();
                    foreach (XmlElement bp in ch.GetElementsByTagName("BP"))
                    {
                        if (int.TryParse(bp.GetAttribute("Id"), out int id)) set.Add(id);
                    }
                    _learned[ser] = set;
                }
            }
            catch (Exception ex) { Console.WriteLine("[BlueprintStore] Load error: {0}", ex); }
        }
    }

    public class BlueprintDeed : Item
    {
        [CommandProperty(AccessLevel.GameMaster)]
        public BlueprintId Blueprint { get; set; }

        [Constructable]
        public BlueprintDeed() : base(0x14F0)
        {
            Name = "blueprint deed";
            Hue = 1150;
            Weight = 1.0;
            Blueprint = BlueprintId.ClockworkSpiderCrate;
            LootType = LootType.Regular;
        }

        [Constructable]
        public BlueprintDeed(BlueprintId id) : this() { Blueprint = id; }

        public BlueprintDeed(Serial s) : base(s) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Blueprint: {0}", Blueprint);
            list.Add("Double-click to learn (character-bound).");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack)) { from.SendLocalizedMessage(1042001); return; }
            from.SendGump(new ConfirmationGump(this, from));
        }

        private sealed class ConfirmationGump : Server.Gumps.Gump
        {
            private readonly BlueprintDeed _deed; private readonly Mobile _from;
            public ConfirmationGump(BlueprintDeed deed, Mobile from) : base(150, 150)
            {
                _deed = deed; _from = from;
                AddBackground(0, 0, 300, 140, 9270);
                AddHtml(15, 15, 270, 60, $"<BASEFONT COLOR=#FFFFFF>Learn blueprint: <b>{deed.Blueprint}</b> on this character?</BASEFONT>", false, false);
                AddButton(40, 90, 4005, 4007, 1, Server.Gumps.GumpButtonType.Reply, 0); AddLabel(75, 90, 0x480, "Learn");
                AddButton(160, 90, 4005, 4007, 0, Server.Gumps.GumpButtonType.Reply, 0); AddLabel(195, 90, 0x480, "Cancel");
            }
            public override void OnResponse(Server.Network.NetState ns, Server.Gumps.RelayInfo info)
            {
                if (_deed == null || _deed.Deleted) return;
                if (info.ButtonID == 1)
                {
                    if (BlueprintStore.Learn(_from, _deed.Blueprint))
                        _deed.Delete();
                }
            }
        }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); w.Write((int)Blueprint); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); Blueprint = (BlueprintId)r.ReadInt(); }
    }

    public static class BlueprintCommands
    {
        public static void Initialize()
        {
            BlueprintStore.Initialize();

            CommandSystem.Register("mp.grantblueprint", AccessLevel.GameMaster, e =>
            {
                var args = e.ArgString.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (args.Length < 1) { e.Mobile.SendMessage("Usage: [mp.grantblueprint <BlueprintId> [<targetName>]"); return; }

                if (!Enum.TryParse<BlueprintId>(args[0], true, out var id)) { e.Mobile.SendMessage("Unknown id."); return; }

                Mobile target = e.Mobile;
                if (args.Length >= 2)
                {
                    foreach (Mobile m in World.Mobiles.Values)
                    {
                        if (m != null && m.Name != null && m.Name.IndexOf(args[1], StringComparison.OrdinalIgnoreCase) >= 0) { target = m; break; }
                    }
                }

                if (BlueprintStore.Learn(target, id))
                    e.Mobile.SendMessage(0x55, $"Granted {id} to {target.Name}.");
            });
        }
    }
}