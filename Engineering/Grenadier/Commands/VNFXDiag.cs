// Path: Scripts/Custom/Engineering/Grenadier/Commands/VNFXDiag.cs
using System;
using Server;
using Server.Commands;
using Server.Targeting;
using VitaNex;
using VitaNex.FX;

public static class VNFXDiag
{
    public static void Initialize()
    {
        CommandSystem.Register("vnfxdiag", AccessLevel.Administrator, e =>
        {
            var m = e.Mobile;
            if (m == null) return;

            m.SendMessage(0x59, $"AssetSafeMode.Enabled = {AssetSafeMode.Enabled}");
            m.Target = new DiagTarget();
            m.SendMessage(0x59, "Target a location to test VN + vanilla FX.");
        });
    }

    private sealed class DiagTarget : Target
    {
        public DiagTarget() : base(12, true, TargetFlags.None) { }

        protected override void OnTarget(Mobile from, object o)
        {
            if (from == null) return;

            Point3D p; Map map;
            if (o is IPoint3D ip) { p = new Point3D(ip); map = from.Map; }
            else if (o is Mobile m2) { p = m2.Location; map = m2.Map; }
            else { from.SendMessage(0x22, "Invalid target."); return; }

            Console.WriteLine("[VNFXDiag] AssetSafeMode={0}", AssetSafeMode.Enabled);

            if (!AssetSafeMode.Enabled)
            {
                try
                {
                    new EnergyExplodeEffect(p, map, 0x489, 0, null, null, null).Send();
                    from.SendMessage(0x59, "VN EnergyExplodeEffect sent.");
                }
                catch (Exception ex)
                {
                    from.SendMessage(0x22, "VN direct send failed: " + ex.Message);
                }
            }
            else
            {
                from.SendMessage(0x22, "AssetSafeMode is ON; VN effects will be skipped.");
            }

            // Always show a big vanilla ring so you see something huge either way
            BigVanillaRing(map, p, 0x489, 12);
        }
    }

    // Moved out of the method; no local static functions.
    private static void BigVanillaRing(Map map, Point3D c, int hue, int radius)
    {
        for (int r = 2; r <= radius; r += 2)
        {
            int points = 16 + r * 2;
            for (int i = 0; i < points; i++)
            {
                double a = (Math.PI * 2.0) * i / points;
                int dx = (int)Math.Round(Math.Cos(a) * r);
                int dy = (int)Math.Round(Math.Sin(a) * r);
                var q = new Point3D(c.X + dx, c.Y + dy, c.Z);

                Timer.DelayCall(TimeSpan.FromMilliseconds(r * 25 + (i % 3) * 15), () =>
                {
                    Effects.SendLocationParticles(
                        new Entity(Serial.Zero, q, map),
                        0x36FE, 12, 20, hue, 0, 5052, 0);
                });
            }
        }

        Effects.PlaySound(c, map, 0x307);
    }
}
