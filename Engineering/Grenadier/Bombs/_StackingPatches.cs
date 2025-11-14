// GG1: Stackable Poison + Utility bombs without touching your long base files.
// Applies to: MinorPoisonBomb, MidPoisonBomb, PoisonBomb, PoisonCloudBomb, UltraPoisonBomb,
//             HealthBomb, MegaHealthBomb, CureBomb, CleanseBomb.

using System;
using Server;
using Server.Items;
using Server.Custom.Engineering.Grenadier;

namespace Server.Custom.Engineering.Grenadier.Bombs.Poison
{
    public partial class MinorPoisonBomb : Item
    {
        private void EnsureStack()
        {
            Stackable = true;
            if (Amount < 1) Amount = 1;
            if (Amount > GrenadierConfig.MaxStack) Amount = GrenadierConfig.MaxStack;
        }

        public override void OnAdded(object parent) { base.OnAdded(parent); EnsureStack(); }

        public override void Deserialize(GenericReader r)
        {
            base.Deserialize(r);
            EnsureStack(); // migrate old items
        }

        public override bool CanStackWith(Item item)
        {
            return base.CanStackWith(item)
                && item != null
                && item.GetType() == GetType()
                && item.Hue == Hue;
        }

        public override bool OnStack(Mobile from, Item dropped, bool playSound)
        {
            if (dropped == null || dropped.Deleted || !CanStackWith(dropped)) return false;

            int room = GrenadierConfig.MaxStack - Amount;
            if (room <= 0) return false;

            int take = Math.Min(room, dropped.Amount);
            Amount += take;
            dropped.Amount -= take;
            if (dropped.Amount <= 0) dropped.Delete();
            if (playSound) from?.SendSound(0x2E6);
            return true;
        }
    }

    public partial class MidPoisonBomb : MinorPoisonBomb { }
    public partial class PoisonBomb : MinorPoisonBomb { }
    public partial class PoisonCloudBomb : MinorPoisonBomb { }
    public partial class UltraPoisonBomb : MinorPoisonBomb { }
}

namespace Server.Custom.Engineering.Grenadier.Bombs.Utility
{
    public partial class HealthBomb : Item
    {
        private void EnsureStack()
        {
            Stackable = true;
            if (Amount < 1) Amount = 1;
            if (Amount > GrenadierConfig.MaxStack) Amount = GrenadierConfig.MaxStack;
        }

        public override void OnAdded(object parent) { base.OnAdded(parent); EnsureStack(); }

        public override void Deserialize(GenericReader r)
        {
            base.Deserialize(r);
            EnsureStack();
        }

        public override bool CanStackWith(Item item)
        {
            return base.CanStackWith(item)
                && item != null
                && item.GetType() == GetType()
                && item.Hue == Hue;
        }

        public override bool OnStack(Mobile from, Item dropped, bool playSound)
        {
            if (dropped == null || dropped.Deleted || !CanStackWith(dropped)) return false;

            int room = GrenadierConfig.MaxStack - Amount;
            if (room <= 0) return false;

            int take = Math.Min(room, dropped.Amount);
            Amount += take;
            dropped.Amount -= take;
            if (dropped.Amount <= 0) dropped.Delete();
            if (playSound) from?.SendSound(0x2E6);
            return true;
        }
    }

    public partial class MegaHealthBomb : HealthBomb { }
    public partial class CureBomb : HealthBomb { }
    public partial class CleanseBomb : HealthBomb { }
}
