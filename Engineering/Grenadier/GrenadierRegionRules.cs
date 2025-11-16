using Server;
using Server.Mobiles;
using Server.Regions; // GuardedRegion

namespace Server.Custom.Engineering.Grenadier
{
    public static class GrenadierRegionRules
    {
        // damage=true => disallow in guarded / safe regions
        public static bool CanUseBomb(Mobile from, bool damage)
        {
            if (from == null || from.Deleted || from.Map == null || from.Map == Map.Internal)
                return false;

            if (!damage) // utility bombs allowed anywhere
                return true;

            Region r = from.Region;
            if (r == null)
                return true;

            // Guarded check (ServUO variants)
            bool guarded = false;
            try { guarded = r.IsPartOf(typeof(GuardedRegion)); } catch { }

            if (guarded || r.IsPartOf("Town") || r.IsPartOf("SafeZone") || r.IsPartOf("Bank"))
                return false;

            return true;
        }
    }
}
