// ============================================================================
// File: Scripts/Custom/Engineering/World/UtilityInteractables.cs
// Phase 4 — Utility Content Pack #1
// ============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;
using Server.Network;
using Server.Regions;

namespace Server.Custom.Engineering
{
    // ---------------------------- Config knobs ----------------------------
    public static class UtilityPackConfig
    {
        // D10
        public static bool EnableDecoderLocks = true;
        public static TimeSpan DecoderCooldown = TimeSpan.FromSeconds(15);

        // D11
        public static bool EnableGrapple = true; // set false if pathing proves infeasible
        public static TimeSpan GrappleCooldown = TimeSpan.FromSeconds(20);

        // D12
        public static bool EnableLava = true;
        public static int LavaDamageMin = 12;
        public static int LavaDamageMax = 20;
        public static bool LavaRequiresMountablePet = false; // keep false by default
    }

    // ---------------------------- Helper utils ----------------------------
    internal static class EngPetUtil
    {
        // cache reflection lookups
        private static readonly Dictionary<Type, MethodInfo> _hasEqType = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> _hasEqName = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> _getEquip = new Dictionary<Type, MethodInfo>();

        public static bool TryFindMyMechPet(Mobile from, int range, out BaseCreature pet)
        {
            pet = null;
            if (from == null || from.Deleted)
                return false;

            Map map = from.Map;
            if (map == null || map == Map.Internal)
                return false;

            // Search nearby mobiles for controlled pet owned by 'from'
            foreach (Mobile m in from.GetMobilesInRange(range))
            {
                if (m is BaseCreature bc && bc.Controlled && bc.ControlMaster == from)
                {
                    // Optional: check if it's actually a mechanical pet by type name
                    var tn = bc.GetType().Name;
                    if (tn.IndexOf("Clockwork", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tn.IndexOf("Mechanical", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        tn.IndexOf("AutonomousMule", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        pet = bc;
                        return true;
                    }

                    // Or if it exposes a marker property IsMechanical
                    var prop = bc.GetType().GetProperty("IsMechanical", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop != null && prop.PropertyType == typeof(bool) && (bool)prop.GetValue(bc, null))
                    {
                        pet = bc;
                        return true;
                    }
                }
            }

            return false;
        }

        public static bool HasEquipment(BaseCreature bc, params string[] requiredTypeNames)
        {
            if (bc == null) return false;

            var t = bc.GetType();

            // 1) Try HasEquipment(Type)
            if (!_hasEqType.TryGetValue(t, out var miType))
            {
                miType = t.GetMethod("HasEquipment", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Type) }, null);
                _hasEqType[t] = miType; // cache (can be null)
            }

            if (miType != null)
            {
                foreach (var name in requiredTypeNames)
                {
                    var et = FindTypeByName(name);
                    if (et != null && (bool)miType.Invoke(bc, new object[] { et }))
                        return true;
                }
            }

            // 2) Try HasEquipment(string)
            if (!_hasEqName.TryGetValue(t, out var miName))
            {
                miName = t.GetMethod("HasEquipment", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
                _hasEqName[t] = miName;
            }
            if (miName != null)
            {
                foreach (var name in requiredTypeNames)
                {
                    if ((bool)miName.Invoke(bc, new object[] { name }))
                        return true;
                }
            }

            // 3) Try to get list of equipment items and compare by type-name
            if (!_getEquip.TryGetValue(t, out var miGet))
            {
                miGet = t.GetMethod("GetEquipment", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                _getEquip[t] = miGet;
            }
            if (miGet != null)
            {
                try
                {
                    var list = miGet.Invoke(bc, null) as System.Collections.IEnumerable;
                    if (list != null)
                    {
                        var needed = new HashSet<string>(requiredTypeNames, StringComparer.OrdinalIgnoreCase);
                        foreach (var obj in list)
                        {
                            if (obj is Item it)
                            {
                                var nm = it.GetType().Name;
                                if (needed.Any(req => nm.IndexOf(req, StringComparison.OrdinalIgnoreCase) >= 0))
                                    return true;
                            }
                        }
                    }
                }
                catch { /* ignore */ }
            }

            // 4) Fallback to profile abilities if equipment API missing
            try
            {
                var prof = MechanicalPetProfiles.GetFor(bc);
                // map equipment name hints to abilities
                foreach (var name in requiredTypeNames)
                {
                    if (name.IndexOf("Heat", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        prof.HasAbility(PetAbilityFlags.HeatTraverse))
                        return true;

                    if (name.IndexOf("Decoder", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        prof.HasAbility(PetAbilityFlags.DecodeLocks))
                        return true;

                    if (name.IndexOf("Servo", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        prof.HasAbility(PetAbilityFlags.Grapple))
                        return true;

                    if (name.IndexOf("Switch", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        prof.HasAbility(PetAbilityFlags.SwitchActuation))
                        return true;
                }
            }
            catch { /* ignore */ }

            return false;
        }

        private static Type FindTypeByName(string shortName)
        {
            // Try common namespaces first
            return ScriptCompiler.FindTypeByName(shortName) ??
                   ScriptCompiler.FindTypeByFullName("Server.Custom.Engineering." + shortName) ??
                   ScriptCompiler.FindTypeByFullName("Server.Custom.MechanicalPets." + shortName);
        }
    }

    // ---------------------------- Decoder Lock ----------------------------
    public class DecoderLock : Item
    {
        private static readonly Dictionary<Serial, DateTime> _cool = new Dictionary<Serial, DateTime>();

        [Constructable]
        public DecoderLock() : base(0x1A7A) // wall switch graphic
        {
            Name = "decoder lock";
            Hue = 1150;
            Movable = false;
        }

        public DecoderLock(Serial s) : base(s) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Requires: Array + Core");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!UtilityPackConfig.EnableDecoderLocks)
            {
                from.SendMessage(38, "The lock mechanisms seem inactive.");
                return;
            }

            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            if (!EngPetUtil.TryFindMyMechPet(from, 5, out var pet))
            {
                from.SendMessage(38, "Your mechanical pet must be nearby.");
                return;
            }

            if (_cool.TryGetValue(pet.Serial, out var last) && DateTime.UtcNow < last)
            {
                var rem = (last - DateTime.UtcNow);
                from.SendMessage(38, $"The decoder is recharging ({rem.Seconds}s).");
                return;
            }

            // Need Array_Decoder AND Core_* (any core)
            bool okArray = EngPetUtil.HasEquipment(pet, "Array_Decoder");
            bool okCore  = EngPetUtil.HasEquipment(pet, "Core_", "CoreStandard", "Core_Standard"); // flexible name match

            if (!(okArray && okCore))
            {
                from.SendMessage(38, "Requires a Decoder Array and a Core equipped on your mechanical pet.");
                return;
            }

            _cool[pet.Serial] = DateTime.UtcNow + UtilityPackConfig.DecoderCooldown;

            // Success: let derived maps react here; by default, open a small side chest
            TryOpenSideChest(from);
            Effects.PlaySound(GetWorldLocation(), Map, 0x249);
            PublicOverheadMessage(MessageType.Emote, 0x55, false, "*decoder lock disengages*");
        }

        protected virtual void TryOpenSideChest(Mobile opener)
        {
            // Default behavior: spawn a locked chest with modest loot next to the lock.
            try
            {
                var chest = new MetalGoldenChest { Movable = true, Name = "decoded cache" };
                chest.MoveToWorld(new Point3D(X + 1, Y, Z), Map);

                // Keep it simple: unlock and drop some gold/reagents
                chest.Locked = false;
                chest.DropItem(new Gold(Utility.RandomMinMax(500, 900)));
                chest.DropItem(new BlackPearl(Utility.RandomMinMax(10, 25)));
                chest.DropItem(new MandrakeRoot(Utility.RandomMinMax(10, 25)));
            }
            catch { /* allow shard to replace with quest logic later */ }
        }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }

    // ---------------------------- Grapple Point ----------------------------
    public class GrapplePoint : Item
    {
        private static readonly Dictionary<Serial, DateTime> _cool = new Dictionary<Serial, DateTime>();

        [CommandProperty(AccessLevel.GameMaster)]
        public Point3D TargetPoint { get; set; }

        [CommandProperty(AccessLevel.GameMaster)]
        public Map TargetMap { get; set; }

        [Constructable]
        public GrapplePoint() : base(0x1E89) // hook-like art
        {
            Name = "grapple point";
            Hue = 2406;
            Movable = false;
            TargetPoint = Point3D.Zero;
            TargetMap = Map.Internal;
        }

        public GrapplePoint(Serial s) : base(s) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Requires: Servo Tier 2");
            if (TargetMap != null && TargetMap != Map.Internal)
                list.Add("Target: {0},{1},{2}", TargetPoint.X, TargetPoint.Y, TargetPoint.Z);
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!UtilityPackConfig.EnableGrapple)
            {
                from.SendMessage(38, "The anchor cannot be secured here.");
                return;
            }

            if (!from.InRange(GetWorldLocation(), 2))
            {
                from.SendLocalizedMessage(500446);
                return;
            }

            if (!EngPetUtil.TryFindMyMechPet(from, 5, out var pet))
            {
                from.SendMessage(38, "Your mechanical pet must be nearby.");
                return;
            }

            if (_cool.TryGetValue(pet.Serial, out var last) && DateTime.UtcNow < last)
            {
                var rem = (last - DateTime.UtcNow);
                from.SendMessage(38, $"Your servo is cooling down ({rem.Seconds}s).");
                return;
            }

            // Treat Servo_Stability as "Tier 2" for now.
            if (!EngPetUtil.HasEquipment(pet, "Servo_Stability"))
            {
                from.SendMessage(38, "Your pet lacks a Servo Tier 2 to anchor the line.");
                return;
            }

            if (TargetMap == null || TargetMap == Map.Internal || TargetPoint == Point3D.Zero)
            {
                from.SendMessage(38, "This grapple has nowhere to connect.");
                return;
            }

            // Simple teleport (safe default). If your shard wants pathing/bridge, replace here.
            from.MoveToWorld(TargetPoint, TargetMap);
            _cool[pet.Serial] = DateTime.UtcNow + UtilityPackConfig.GrappleCooldown;
            Effects.SendLocationEffect(TargetPoint, TargetMap, 0x3728, 10, 1153);
            from.PlaySound(0x1FB);
        }

        public override void Serialize(GenericWriter w)
        {
            base.Serialize(w);
            w.Write(0);
            w.Write(TargetPoint);
            w.Write(TargetMap);
        }

        public override void Deserialize(GenericReader r)
        {
            base.Deserialize(r);
            int v = r.ReadInt();
            TargetPoint = r.ReadPoint3D();
            TargetMap = r.ReadMap();
        }
    }

    // ---------------------------- Lava Field ----------------------------
    public class LavaField : Item
    {
        [CommandProperty(AccessLevel.GameMaster)]
        public bool Enabled { get; set; }

        [Constructable]
        public LavaField() : base(0x398C) // lava tile art (static)
        {
            Name = "searing lava";
            Hue = 0;
            Movable = false;
            Enabled = true;
        }

        public LavaField(Serial s) : base(s) { }

        public override void GetProperties(ObjectPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Requires: Heat Plating on a nearby mechanical pet");
        }

        public override bool OnMoveOver(Mobile m)
        {
            if (!UtilityPackConfig.EnableLava || !Enabled || m == null)
                return base.OnMoveOver(m);

            bool safe = false;

            if (EngPetUtil.TryFindMyMechPet(m, 5, out var pet))
            {
                if (!UtilityPackConfig.LavaRequiresMountablePet || (pet is IMount))
                {
                    safe = EngPetUtil.HasEquipment(pet, "Plating_Heat", "HeatShielding", "Module_HeatShielding");
                }
            }

            if (!safe)
            {
                int dmg = Utility.RandomMinMax(UtilityPackConfig.LavaDamageMin, UtilityPackConfig.LavaDamageMax);
                AOS.Damage(m, null, dmg, 0, 100, 0, 0, 0);
                m.SendMessage(38, "The lava scorches you!");
                m.PlaySound(0x208);
                Effects.SendLocationEffect(m.Location, m.Map, 0x3709, 30, 1161);
            }
            else
            {
                m.SendMessage(0x55, "Your pet's heat plating shields you from the worst of the heat.");
            }

            return base.OnMoveOver(m);
        }

        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); w.Write(Enabled); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); Enabled = r.ReadBool(); }
    }
}