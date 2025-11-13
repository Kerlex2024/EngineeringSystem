// ============================================================================
// File: Scripts/Custom/Engineering/Config/EngineeringConfig.Blueprints.cs
// (extend your existing EngineeringConfig)
// ============================================================================
namespace Server.Custom.Engineering
{
    public static partial class EngineeringConfigPart // helper to split file safely
    {
        // Intentionally empty; marker for partial if you keep EngineeringConfig split
    }
}

namespace Server.Custom.Engineering
{
    public static class BlueprintSettings
    {
        public static bool PrototypeEnabled { get; private set; } = true;
        public static double PrototypeDurabilityPenalty { get; private set; } = 0.20; // 20%
        public static double PrototypeFailBonus { get; private set; } = 0.10;         // +10% abs
        public static bool PrototypeMakersMarkDisabled { get; private set; } = true;

        // (Optional) wire into your EngineeringConfig.Load() if you like centralization
    }
}