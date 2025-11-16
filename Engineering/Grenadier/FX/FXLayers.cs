// ============================================================================
// Path: Scripts/Custom/Engineering/Grenadier/FX/FXLayers.cs
// Defines the effect-layer mask used by VNLavaBomb & CataclysmFX.
// ============================================================================
using System;

namespace Server
{
    [Flags]
    public enum FXLayers
    {
        None          = 0,
        Primary       = 1 << 0, // main explosion
        Waves         = 1 << 1, // fire wave(s)
        Subs          = 1 << 2, // secondary detonations
        EnergyFlicker = 1 << 3, // small extra flicker on subs
        Pools         = 1 << 4, // lava pools
        All           = Primary | Waves | Subs | EnergyFlicker | Pools
    }
}
