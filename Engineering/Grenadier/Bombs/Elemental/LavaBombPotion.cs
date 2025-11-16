// File: Scripts/Custom/Engineering/Grenadier/Bombs/Elemental/LavaBombPotion.cs
#region References
using System;
using Server;
using Server.Items;
#endregion

namespace Server.Custom.Engineering.Grenadier.Bombs.Elemental
{
    /// <summary>
    /// Fire-element bomb: stronger than Mega, potion bottle look, Bomb hue, VN FX if present.
    /// </summary>
    public class LavaBombPotion : BaseElementalBomb
    {
        private const int PotionBottleItemID = 0x0F0D; // potion bottle art

        [Constructable]
        public LavaBombPotion() : base(PotionEffect.ExplosionGreater)
        {
            Name  = "Lava Bomb Potion";
            ItemID = PotionBottleItemID; // bottle icon & thrown anim
            Hue    = ExplosionHue;       // Bomb.cs hue for icon/FX
        }

        public LavaBombPotion(Serial s) : base(s) { }

        public override ElementKind Element => ElementKind.Fire;

        // Elementals: +25% power, +1 radius vs. Mega baseline
        public override double PowerMultiplier => 1.25;
        public override int ExtraRadius => 1;

        // Keep Mega falloff curve (center-heavy)
        protected override double DamageFalloff(Point3D center, Point3D hit)
        {
            int dx = hit.X - center.X, dy = hit.Y - center.Y;
            int d = (int)Math.Sqrt(dx * dx + dy * dy);
            if (d <= 0) return 1.00;
            if (d == 1) return 0.80;
            if (d == 2) return 0.55;
            if (d == 3) return 0.35;
            return 0.25;
        }

        // (Optional) Tweak VN art IDs for fire if desired; defaults are already lava-like
        protected override int VnHitFlashEffectID => 14000;
        protected override int VnTrailEffectID    => 14089;
        protected override int VnHitSoundID       => 519;
    }
}