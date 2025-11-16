// ============================================================================
// Path: Scripts/Custom/Engineering/Grenadier/Diagnostics/VNFXDiag.cs
// ServUO-compatible VN FX inspector: [vnfxdiag explode|wave|all]
// Lists VitaNex.FX types + public Create* factories + public ctors.
// ============================================================================
using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Commands;

namespace Server.Commands
{
    public static class VNFXDiag
    {
        // ServUO command registration
        public static void Initialize()
        {
            CommandSystem.Register("vnfxdiag", AccessLevel.GameMaster, OnCommand);
        }

        private static void OnCommand(CommandEventArgs e)
        {
            string filter = (e.ArgString ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(filter))
            {
                e.Mobile.SendMessage(1153, "Usage: [vnfxdiag explode|wave|all|<substring>");
                return;
            }

            Func<string, bool> want = s =>
            {
                s = (s ?? string.Empty).ToLowerInvariant();
                var f = filter.ToLowerInvariant();
                if (f == "all") return true;
                if (f == "explode") return s.Contains("explod");   // explode/explosion
                if (f == "wave") return s.Contains("wave");
                return s.Contains(f);
            };

            int found = 0;

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); }
                catch { continue; }

                foreach (var t in types)
                {
                    var full = t.FullName ?? "";
                    if (!full.StartsWith("VitaNex.FX", StringComparison.Ordinal)) continue;
                    if (!want(full)) continue;

                    found++;
                    e.Mobile.SendMessage(1153, $"Type: {full}");

                    // Public static factories named Create* (Create / CreateInstance etc.)
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                    {
                        if (!m.Name.StartsWith("Create", StringComparison.Ordinal)) continue;
                        var sig = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        e.Mobile.SendMessage(33, $"  {m.Name}({sig})");
                    }

                    // Nested types (e.g., Fire/Energy)
                    foreach (var nt in t.GetNestedTypes(BindingFlags.Public))
                    {
                        e.Mobile.SendMessage(1153, $"  Nested: {nt.FullName}");

                        foreach (var m in nt.GetMethods(BindingFlags.Public | BindingFlags.Static))
                        {
                            if (!m.Name.StartsWith("Create", StringComparison.Ordinal)) continue;
                            var sig = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                            e.Mobile.SendMessage(33, $"    {m.Name}({sig})");
                        }

                        foreach (var ctor in nt.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                        {
                            var sig = string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                            e.Mobile.SendMessage(53, $"    ctor({sig})");
                        }
                    }
                }
            }

            if (found == 0)
            {
                e.Mobile.SendMessage(38, "No VitaNex.FX types matched. Is VitaNex FX loaded?");
            }
            else
            {
                e.Mobile.SendMessage(1153, $"Done. Found {found} VitaNex.FX candidate types.");
            }
        }
    }
}
