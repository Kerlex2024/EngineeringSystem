// ============================================================================
// File: Scripts/Custom/Engineering/Tools/EngineeringCraftSystem.cs
// ============================================================================
using System;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Custom.Engineering;
using System.Collections.Generic;
using Server.Custom.Engineering.Grenadier;
using Server.Custom.Engineering.Grenadier.Bombs.Poison;
using Server.Custom.Engineering.Grenadier.Bombs.Explosive;
using Server.Custom.Engineering.Grenadier.Bombs.Utility;
using Server.Custom.Engineering.Grenadier.TrapBoxes; // ClaymoreBox, GrenadierKit



namespace Server.Custom.Engineering
{
    public class EngineeringCraftSystem : CraftSystem
    {
        public static readonly EngineeringCraftSystem Instance = new EngineeringCraftSystem();

        private EngineeringCraftSystem() : base(1, 1, 1.25) { }
        
        private static readonly Dictionary<Type, BlueprintId> _bpByType = new Dictionary<Type, BlueprintId>
        {
            { typeof(ClockworkSpiderCrate), BlueprintId.ClockworkSpiderCrate },
            { typeof(AutonomousMuleCrate),  BlueprintId.AutonomousMuleCrate  },
            { typeof(DroneCrate),           BlueprintId.DroneCrate           },
            { typeof(MinionCrate),          BlueprintId.MinionCrate          },
            { typeof(OverseerCrate),        BlueprintId.OverseerCrate        },
            { typeof(JuggernautCrate),      BlueprintId.JuggernautCrate      },
        };
        private void AddGrenadier()
        {
            if (!GrenadierConfig.EnableGrenadier)
                return;

            int id;
            int cd = (int)GrenadierConfig.BombReuseDelay.TotalSeconds;

            // --- Poison ---
            id = AddCraft(typeof(MinorPoisonBomb), "Grenadier",
                $"Minor Poison Bomb  [r={GrenadierConfig.MinorPoisonRadius}, cd={cd}s, EP scales, town-blocked]",
                70.0, 100.0, typeof(Bottle), "bottle", 1);
            AddRes(id, typeof(OrangePetals), "orange petals", 2);

            id = AddCraft(typeof(MidPoisonBomb), "Grenadier",
                $"Poison Bomb  [r={GrenadierConfig.MidPoisonRadius}, cd={cd}s, EP scales, town-blocked]",
                85.0, 115.0, typeof(Bottle), "bottle", 1);
            AddRes(id, typeof(OrangePetals), "orange petals", 4);

            id = AddCraft(typeof(MegaPoisonBomb), "Grenadier",
                $"Mega Poison Bomb  [r={GrenadierConfig.MegaPoisonRadius}, cd={cd}s, EP scales, town-blocked]",
                100.0, 130.0, typeof(Bottle), "bottle", 1);
            AddRes(id, typeof(OrangePetals), "orange petals", 6);

            id = AddCraft(typeof(UltraPoisonBomb), "Grenadier",
                $"Ultra Poison Bomb  [r={GrenadierConfig.UltraPoisonRadius}, cd={cd}s, EP scales, town-blocked]",
                110.0, 140.0, typeof(Bottle), "bottle", 1);
            AddRes(id, typeof(OrangePetals), "orange petals", 8);
            AddRes(id, typeof(ArcaneDampener), "arcane dampener", 1);

            // --- Explosive ---
            id = AddCraft(typeof(TacticalBomb), "Grenadier",
                $"Tactical Bomb  [cd={cd}s, EP scales, town-blocked]",
                90.0, 120.0, typeof(Bottle), "bottle", 1);
            AddRes(id, typeof(ArcanePowerCore), "arcane power core", 1);

            id = AddCraft(typeof(StrategicBomb), "Grenadier",
                $"Strategic Bomb  [cd={cd}s, EP scales, town-blocked]",
                100.0, 130.0, typeof(Bottle), "bottle", 1);
            AddRes(id, typeof(ArcanePowerCore), "arcane power core", 1);

            id = AddCraft(typeof(MegaBombPotion), "Grenadier",
                $"Mega Bomb  [cd={cd}s, EP scales, town-blocked]",
                105.0, 135.0, typeof(Bottle), "bottle", 1);
            AddRes(id, typeof(ArcanePowerCore), "arcane power core", 2);

            // --- Utility ---
            id = AddCraft(typeof(HealthBomb), "Grenadier",
                $"Health Bomb  [r={GrenadierConfig.HealthBombRadius}, cd={cd}s, Healing&EP scale, utility]",
                95.0, 125.0, typeof(ArcanePowerCore), "arcane power core", 1);
            AddRes(id, typeof(Ginseng), "ginseng", 10);
            AddRes(id, typeof(Bottle), "bottle", 5);

            id = AddCraft(typeof(CureBomb), "Grenadier",
                $"Cure Bomb  [r={GrenadierConfig.CureBombRadius}, cd={cd}s, petals immunity { (int)GrenadierConfig.PetalBuffDuration.TotalSeconds }s]",
                95.0, 125.0, typeof(ArcanePowerCore), "arcane power core", 1);
            AddRes(id, typeof(OrangePetals), "orange petals", 10);
            AddRes(id, typeof(Bottle), "bottle", 5);

            id = AddCraft(typeof(CleanseBomb), "Grenadier",
                $"Cleanse Bomb  [r={GrenadierConfig.CleanseBombRadius}, cd={cd}s, removes curses]",
                100.0, 130.0, typeof(ArcanePowerCore), "arcane power core", 1);
            AddRes(id, typeof(EnchantedApple), "enchanted apple", 15);
            AddRes(id, typeof(Bottle), "bottle", 5);

            // --- Traps ---
            id = AddCraft(typeof(ClaymoreBox), "Grenadier",
                $"Claymore Box  [r={GrenadierConfig.ClaymoreRadius}, charges={GrenadierConfig.ClaymoreBaseCharges}, town-blocked]",
                100.0, 130.0, typeof(ArcanePowerCore), "arcane power core", 1);
            AddRes(id, typeof(ArcaneDampener), "arcane dampener", 1);
            AddRes(id, typeof(ServoBundle), "servo bundle", 2);
            AddRes(id, typeof(SensorArray), "sensor array", 1);

            id = AddCraft(typeof(GrenadierKit), "Grenadier",
                "Grenadier Kit  [+3 claymore charges]",
                85.0, 115.0, typeof(MicroActuator), "micro-actuator", 2);
            AddRes(id, typeof(HeatShielding), "heat shielding", 1);
        }


        public override int CanCraft(Mobile from, ITool tool, Type itemType)
        {
            // tool null/deleted
            if (tool == null || tool.Deleted)
                return 1044038; // You must have the tool to use it.

            // CheckAccessible requires `ref int num` on Pub57
            int num = 0;
            if (!tool.CheckAccessible(from, ref num))
                return num; // localization code provided by tool

            if (tool is BaseTool bt && bt.UsesRemaining <= 0)
                return 1044039; // You have worn out your tool!

            if (itemType != null && _bpByType.TryGetValue(itemType, out var bp))
            {
                if (!BlueprintStore.IsLearned(from, bp))
                {
                    if (!BlueprintSettings.PrototypeEnabled)
                    {
                        from.SendMessage(38, "You have not learned this blueprint.");
                        return 1044037;
                    }

                    from.SendMessage(0x22, "You have not learned this blueprint: crafting as a prototype.");
                }
            }

            return 0;
        }


        public override SkillName MainSkill => SkillName.Tinkering;
        public override int GumpTitleNumber => 0;
        public override string GumpTitleString => "<basefont color=#FFFFFF>Engineering</basefont>";

        public override double GetChanceAtMin(CraftItem item) => 0.50;


        public override void PlayCraftEffect(Mobile from) { from.PlaySound(0x241); }

        public override int PlayEndingEffect(Mobile from, bool failed, bool lostMaterial, bool toolBroken, int quality, bool makersMark, CraftItem craftItem)
        {
            if (toolBroken) from.SendLocalizedMessage(1044038);
            if (failed) from.SendMessage(38, "You fail to assemble the parts.");
            else from.SendMessage("You carefully assemble the mechanism.");
            return 0;
        }

        public override void InitCraftList()
        {
            AddGrenadier(); 
            int index;

            // --- Core Parts ---
            index = AddCraft(typeof(MicroActuator), "Core Parts", "Micro-Actuator", 80.0, 105.0, typeof(IronIngot), 1044036, 15, 1044037);
            AddRes(index, typeof(Gears), 1044254, 2, 1044253);
            ForceNonExceptional(index);

            index = AddCraft(typeof(ArcanePowerCore), "Core Parts", "Arcane Power Core", 90.0, 110.0, typeof(BlankScroll), 1044377, 25, 1044253);
            AddRes(index, typeof(Gears), 1044254, 1, 1044253);
            ForceNonExceptional(index);

            index = AddCraft(typeof(AlloyChassis), "Core Parts", "Precision Alloy Chassis", 85.0, 110.0, typeof(IronIngot), 1044036, 50, 1044037);
            AddRes(index, typeof(Board), 1044041, 30, 1044351);
            ForceNonExceptional(index);

            index = AddCraft(typeof(SensorArray), "Core Parts", "Sensor Array", 80.0, 105.0, typeof(Gears), 1044254, 4, 1044253);
            AddRes(index, typeof(Springs), 1024179, 2, 1044253);
            ForceNonExceptional(index);

            index = AddCraft(typeof(ServoBundle), "Core Parts", "Servo Bundle", 75.0, 100.0, typeof(IronIngot), 1044036, 25, 1044037);
            AddRes(index, typeof(Springs), 1024179, 4, 1044253);

            index = AddCraft(typeof(HeatShielding), "Core Parts", "Heat Shielding", 90.0, 110.0, typeof(IronIngot), 1044036, 60, 1044037);
            ForceNonExceptional(index);

            index = AddCraft(typeof(ArcaneDampener), "Core Parts", "Arcane Dampener", 90.0, 110.0, typeof(BlankScroll), 1044377, 50, 1044253);
            ForceNonExceptional(index);

            // --- Modules ---
            index = AddCraft(typeof(Module_SwitchActuator), "Modules", "Module: Switch Actuator", 95.0, 115.0, typeof(MicroActuator), "micro-actuator", 5, 1044253);
            AddRes(index, typeof(SensorArray), "sensor array", 2, 1044253);
            AddRes(index, typeof(ArcanePowerCore), "arcane power core", 1, 1044253);
            ForceNonExceptional(index);

            index = AddCraft(typeof(Module_HeatShielding), "Modules", "Module: Heat Shielding", 95.0, 115.0, typeof(HeatShielding), "heat shielding", 2, 1044253);
            AddRes(index, typeof(AlloyChassis), "precision alloy chassis", 1, 1044253);
            ForceNonExceptional(index);

            // --- Maintenance ---
            index = AddCraft(typeof(RepairKit), "Maintenance", "Repair Kit", 85.0, 110.0, typeof(MicroActuator), "micro-actuator", 2, 1044253);
            AddRes(index, typeof(ServoBundle), "servo bundle", 1, 1044253);
            AddRes(index, typeof(HeatShielding), "heat shielding", 1, 1044253);
            ForceNonExceptional(index);


            {
                index = AddCraft(typeof(EmptyGrenadierShell), "Grenadier", "Empty Claymore Shell", 90.0, 110.0, typeof(IronIngot), 1044036, 40, 1044037);
                AddRes(index, typeof(Gears), 1044254, 4, 1044253);
                ForceNonExceptional(index);
            }

            // --- Pet Crates ---
            index = AddCraft(typeof(ClockworkSpiderCrate), "Pet Crates", "Clockwork Spider (Crate)", 100.0, 120.0, typeof(AlloyChassis), "precision alloy chassis", 2, 1044253);
            AddRes(index, typeof(ServoBundle), "servo bundle", 5, 1044253);
            AddRes(index, typeof(SensorArray), "sensor array", 2, 1044253);
            AddRes(index, typeof(ArcanePowerCore), "arcane power core", 1, 1044253);
            AddRes(index, typeof(MicroActuator), "micro-actuator", 10, 1044253);
            ForceNonExceptional(index);


            index = AddCraft(typeof(AutonomousMuleCrate), "Pet Crates", "Autonomous Mule (Crate)", 95.0, 120.0, typeof(AlloyChassis), "precision alloy chassis", 1, 1044253);
            AddRes(index, typeof(ServoBundle), "servo bundle", 4, 1044253);
            AddRes(index, typeof(ArcanePowerCore), "arcane power core", 1, 1044253);
            AddRes(index, typeof(SensorArray), "sensor array", 1, 1044253);
            ForceNonExceptional(index);

            index = AddCraft(typeof(DroneCrate), "Pet Crates", "Drone Crate",
                90.0, 120.0, typeof(AlloyChassis), "precision alloy chassis", 2);
            AddRes(index, typeof(ServoBundle), "servo bundle", 4);
            AddRes(index, typeof(SensorArray), "sensor array", 2);
            AddRes(index, typeof(ArcanePowerCore), "arcane power core", 1);
            

            index = AddCraft(typeof(MinionCrate), "Pet Crates", "Minion Crate",
                100.0, 130.0, typeof(AlloyChassis), "precision alloy chassis", 3);
            AddRes(index, typeof(ServoBundle), "servo bundle", 6);
            AddRes(index, typeof(SensorArray), "sensor array", 3);
            AddRes(index, typeof(ArcanePowerCore), "arcane power core", 2);
            AddRes(index, typeof(ArcaneDampener), "arcane dampener", 1);
           

            index = AddCraft(typeof(OverseerCrate), "Pet Crates", "Overseer Crate",
                105.0, 135.0, typeof(AlloyChassis), "precision alloy chassis", 3);
            AddRes(index, typeof(ServoBundle), "servo bundle", 7);
            AddRes(index, typeof(SensorArray), "sensor array", 4);
            AddRes(index, typeof(ArcanePowerCore), "arcane power core", 2);
            AddRes(index, typeof(ArcaneDampener), "arcane dampener", 2);
            

            index = AddCraft(typeof(JuggernautCrate), "Pet Crates", "Juggernaut Crate",
                110.0, 140.0, typeof(AlloyChassis), "precision alloy chassis", 4);
            AddRes(index, typeof(ServoBundle), "servo bundle", 10);
            AddRes(index, typeof(SensorArray), "sensor array", 5);
            AddRes(index, typeof(ArcanePowerCore), "arcane power core", 3);
            AddRes(index, typeof(ArcaneDampener), "arcane dampener", 3);
            


            // --- Packing Crates ---
            index = AddCraft(typeof(PackingCrate), "Packing Crates", "Packing Crate", 80.0, 105.0, typeof(Board), 1044041, 25, 1044351);
            AddRes(index, typeof(IronIngot), 1044036, 15, 1044037);
            ForceNonExceptional(index);
        }



        public override bool RetainsColorFrom(CraftItem item, Type type) => false;
    }

   

    public class EmptyGrenadierShell : Item
    {
        [Constructable] public EmptyGrenadierShell() : base(0x1B7A) { Name = "empty claymore shell"; Weight = 5.0; }
        public EmptyGrenadierShell(Serial s) : base(s) { }
        public override void Serialize(GenericWriter w) { base.Serialize(w); w.Write(0); }
        public override void Deserialize(GenericReader r) { base.Deserialize(r); r.ReadInt(); }
    }
}