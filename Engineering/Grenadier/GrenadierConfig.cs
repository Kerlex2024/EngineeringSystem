// ============================================================================
// File: Scripts/Custom/Engineering/Grenadier/GrenadierConfig.cs
// (Replace your current file or merge the additional fields below.)
// ============================================================================
using System;

namespace Server.Custom.Engineering.Grenadier
{
    public static class GrenadierConfig
    {
        public static bool EnableGrenadier = true;

        // When false: guild/allies are NOT harmed by damage bombs nor healed by health bombs (ally-only).
        public static bool FriendlyFire = false;

        // Global cooldown between throws (per item instance)
        public static TimeSpan BombReuseDelay = TimeSpan.FromSeconds(3);

        // Utility heal scaling baseline
        public static int HealthBombBaseHeal = 18;

        // Default radii
        public static int MinorPoisonRadius = 3;
        public static int MidPoisonRadius   = 6;
        public static int MegaPoisonRadius  = 6;
        public static int UltraPoisonRadius = 8;

        public static int HealthBombRadius  = 6;
        public static int CureBombRadius    = 6;
        public static int CleanseBombRadius = 6;

        // Orange Petals immunity buff duration (CureBomb)
        public static TimeSpan PetalBuffDuration = TimeSpan.FromSeconds(300);

        // Claymore defaults
        public static int ClaymoreRadius = 5;
        public static int ClaymoreMinDamage = 35;
        public static int ClaymoreMaxDamage = 65;
        public static int ClaymoreBaseCharges = 5;
        public static TimeSpan ClaymoreArmDelay = TimeSpan.FromSeconds(4);
        
        // ------- Grenadier visual hues (live-tunable) -------
        public static int TacticalHue   = 2605;  // bottle & FX
        public static int StrategicHue  = 2609;  // bottle & FX
        public static int MegaHue       = 2700;  // bottle & FX

        // Optional: choose wave FX art (both hue well)
        public static int WaveFxItemID  = 0x36BD; // 0x36BD or 0x36B0

        // Some clients don't hue the thrown bottle. If true, use a hueable projectile FX id.
        public static bool UseHueableProjectile = true;

        // Hueable projectile FX (e.g., 0x36D4 = energy burst shard). Used only if UseHueableProjectile == true
        public static int ProjectileFxItemID = 0x36D4;

        // --- NEW: stacking cap for grenadier bombs/potions ---
        public static int MaxStack = 500;

        // Optional: diagnostics switch (already suggested)
        public static bool Debug = false;

        // Call this from your existing reload path
        public static void ReloadGrenadierVisuals()
        {
            // If you read from XML/JSON, hydrate TacticalHue/StrategicHue/MegaHue/WaveFxItemID here.
            // If you keep values hardcoded, this method can stay empty.
        }
        
        public static string Summary()
        {
            return string.Format(
                "Grenadier: TacHue={0} StratHue={1} MegaHue={2} WaveFx=0x{3:X} UseHueProj={4} ProjFx=0x{5:X} MaxStack={6}",
                TacticalHue, StrategicHue, MegaHue, WaveFxItemID, UseHueableProjectile, ProjectileFxItemID, MaxStack);
        }

    }
}
