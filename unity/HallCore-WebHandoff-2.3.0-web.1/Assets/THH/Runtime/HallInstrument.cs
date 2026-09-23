using System.Globalization;

namespace HallLab
{
    public static class HallInstrument
    {
        // User-supplied TH-H reference ranges, not a verified manufacturer calibration.
        public const float MaxIsMilliamps = 10f;
        public const float MaxImAmps = 1f;
        public const float VoltmeterRangeMillivolts = 200f;

        public static bool IsReadable(bool powered, bool circuitValid, float millivolts) =>
            powered && circuitValid && HallEffectMath.IsFinite(millivolts) &&
            System.Math.Abs(millivolts) <= VoltmeterRangeMillivolts;

        public static string Display(bool powered, bool circuitValid, float millivolts)
        {
            if (!powered) return "OFF";
            if (!circuitValid) return "接线无效";
            if (!HallEffectMath.IsFinite(millivolts)) return "ERR";
            if (System.Math.Abs(millivolts) > VoltmeterRangeMillivolts) return "OL";
            // Keep C0 display decimals. These are not a claim about hardware resolution.
            return millivolts.ToString("0.000", CultureInfo.InvariantCulture) + " mV";
        }
    }
}

