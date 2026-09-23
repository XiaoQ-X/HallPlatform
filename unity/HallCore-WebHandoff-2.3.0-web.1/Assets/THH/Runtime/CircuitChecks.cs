using System;
using System.Collections.Generic;
using System.Linq;

namespace HallLab
{
    // Pure graph checks, runnable both in the Unity editor and in the actual player.
    public static class CircuitChecks
    {
        public static string Run()
        {
            int checks = 0;
            void Check(bool condition, string name) { checks++; if (!condition) throw new Exception("Circuit check failed: " + name); }
            var c = new THHCircuit(); Check(!c.Evaluate().Ready, "empty wiring");
            Check(!c.Add(Pin.SampleD, Pin.IsPlus, out _), "factory terminal locked");
            Check(!c.Add(Pin.IsPlus, Pin.IsPlus, out _), "self loop rejected");
            c.ConnectStandard(); Check(c.Cables.Count == 6 && c.Evaluate().Ready, "six standard leads");
            Check(!c.Add(Pin.CaseIsPlus, Pin.IsPlus, out _), "reverse duplicate rejected");
            Check(c.Evaluate().IsDirection == 1 && c.Evaluate().ImDirection == 1 && c.Evaluate().VoltageDirection == 1, "standard signs");
            for (int s = -1; s <= 1; s++) for (int m = -1; m <= 1; m++) for (int v = -1; v <= 1; v++) {
                c.SetSwitch(0, s); c.SetSwitch(1, v); c.SetSwitch(2, m); var r = c.Evaluate();
                Check(r.IsDirection == s && r.ImDirection == m, "source switching");
                Check(r.VoltageDirection == (v == 0 ? 0 : 1), "VH/Vsigma select is not voltage reversal");
                Check(r.Ready == (s != 0 && m != 0 && v != 0), "27 switch combinations");
                Check(!r.Hazard, "fixed topology has no short across switch positions");
            }
            c = new THHCircuit(); c.ConnectStandard();
            foreach (var cable in c.Cables.ToArray()) { c.Remove(cable.Id); Check(!c.Evaluate().Ready, "each missing lead"); c.Add(cable.A, cable.B, out _); }
            c.Add(Pin.IsPlus, Pin.IsMinus, out _); Check(c.Evaluate().SourceShort && !c.TurnOn(out _), "IS short");
            c.Clear(); c.Add(Pin.ImPlus, Pin.ImMinus, out _); Check(c.Evaluate().SourceShort, "IM short");
            c.Clear(); c.Add(Pin.VPlus, Pin.VMinus, out _); Check(c.Evaluate().MeterShort && !c.Evaluate().Ready, "meter shunt");
            c.Clear(); c.Add(Pin.ImPlus, Pin.CaseVPlus, out _); Check(c.Evaluate().CrossCircuit, "IM into voltage");
            c.SetSwitch(1, 0); Check(c.Evaluate().CrossCircuit, "cross circuit with open switch");
            c = new THHCircuit(); c.ConnectStandard(); c.SetCurrent(3, .4f); Check(!c.TurnOn(out _), "zero before turn on");
            c.SetCurrent(0, 0); Check(c.TurnOn(out _), "enable valid zero-input circuit");
            Check(!c.Clear() && !c.ConnectStandard() && !c.Remove(c.Cables[0].Id) && !c.Add(Pin.IsPlus, Pin.ImPlus, out _) && !c.SetSwitch(0, -1), "all energized topology mutations blocked");
            c.SetCurrent(4, .5f); c.TurnOff(); Check(!c.Powered && c.IsSetpoint == 0 && c.ImSetpoint == 0, "controlled reset off");
            c.SetCurrent(-1, 8); Check(c.IsSetpoint == 0 && c.ImSetpoint == 1, "range clamp");
            bool rejected = false; try { c.SetCurrent(float.NaN, 0); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Check(rejected, "nonfinite input");
            int permutations = 0, valid = 0;
            foreach (var p in Permutations(new[] { 6, 7, 8, 9, 10, 11 }, 0)) {
                var net = new THHCircuit();
                for (int i = 0; i < 6; i++) net.Add((Pin)i, (Pin)p[i], out _);
                var report = net.Evaluate(); permutations++;
                bool sameDomains = Enumerable.Range(0, 6).All(i => i / 2 == (p[i] - 6) / 2);
                Check(report.Ready == sameDomains, "permutation " + permutations);
                if (report.Ready) {
                    valid++;
                    Check(report.IsDirection == (p[0] == 6 ? 1 : -1) && report.ImDirection == (p[4] == 10 ? 1 : -1) && report.VoltageDirection == (p[2] == 8 ? 1 : -1), "reversed cable signs");
                }
            }
            Check(permutations == 720 && valid == 8, "all bijective routings, exactly eight polarity variants");
            return "PASS: " + checks + " assertions; 720 cable permutations; 27 switch states; 8 valid polarity variants.\n";
        }
        private static IEnumerable<int[]> Permutations(int[] values, int index)
        {
            if (index == values.Length) { yield return (int[])values.Clone(); yield break; }
            for (int i = index; i < values.Length; i++) {
                (values[index], values[i]) = (values[i], values[index]);
                foreach (var item in Permutations(values, index + 1)) yield return item;
                (values[index], values[i]) = (values[i], values[index]);
            }
        }
    }
}
