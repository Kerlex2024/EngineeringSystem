// ============================================================================
// File: Scripts/Custom/Engineering/Init/EngineeringInit.cs
// ============================================================================
using Server;

namespace Server.Custom.Engineering
{
    public class EngineeringInit
    {
        public static void Initialize()
        {
            // Touch config singletons so they load on server boot.
            var _ = EngineeringConfig.AllowPackingAnywhere;
        }
    }
}