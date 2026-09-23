using System;
using System.Collections.Generic;
using System.Linq;

namespace HallLab
{
    // Conductor connectivity only. Resistive specimens/coils are loads, NOT zero-ohm graph edges.
    // Reference: HZNU PDF p6/p12, specimen (a): D,E current; A,A' Hall; A',C' longitudinal; C unused.
    public enum Pin
    {
        IsPlus, IsMinus, VPlus, VMinus, ImPlus, ImMinus,
        CaseIsPlus, CaseIsMinus, CaseVPlus, CaseVMinus, CaseImPlus, CaseImMinus,
        IsUpperL, IsUpperR, IsLowerL, IsLowerR,
        VUpperL, VUpperR, VLowerL, VLowerR,
        ImUpperL, ImUpperR, ImLowerL, ImLowerR,
        SampleD, SampleE, SampleA, SampleAprime, SampleCprime, CoilStart, CoilEnd
    }

    public readonly struct CircuitEdge
    {
        public readonly Pin A, B;
        public CircuitEdge(Pin a, Pin b) { A = a; B = b; }
        public bool Matches(Pin a, Pin b) => (A == a && B == b) || (A == b && B == a);
    }

    public sealed class StudentCable
    {
        public int Id { get; }
        public Pin A { get; }
        public Pin B { get; }
        public StudentCable(int id, Pin a, Pin b) { Id = id; A = a; B = b; }
    }

    public sealed class CircuitReport
    {
        public bool CrossCircuit, SourceShort, MeterShort;
        public int IsDirection, ImDirection, VoltageDirection;
        public bool HallMode;
        public bool Ready => !CrossCircuit && !SourceShort && !MeterShort && IsDirection != 0 && ImDirection != 0 && VoltageDirection != 0;
        public bool Hazard => CrossCircuit || SourceShort;
        public string Summary
        {
            get
            {
                if (SourceShort) return "危险：电流源正负端被导线短接，禁止通电";
                if (CrossCircuit) return "危险：IS、IM 或测量回路相互跨接，禁止通电";
                if (MeterShort) return "测量输入被短接，不能作为有效测量接线";
                if (!Ready) return "回路未完整：检查漏接、同侧接线和开关中位";
                return (IsDirection < 0 || ImDirection < 0 || VoltageDirection < 0)
                    ? "回路有效，含反向连接；请核对下方实际极性"
                    : "回路有效，可以在电流设定归零后通电";
            }
        }
        private static string Direction(int sign, string positive, string negative) => sign == 0 ? "未闭合" : sign > 0 ? positive : negative;
        public string Detail =>
            "IS   " + Direction(IsDirection, "D → E  (+)", "E → D  (−)") + "\n" +
            "IM   " + Direction(ImDirection, "线圈起点 → 终点  (+)", "线圈终点 → 起点  (−)") + "\n" +
            "电压   " + Direction(VoltageDirection, HallMode ? "A − A′  (VH)" : "A′ − C′  (Vσ)", HallMode ? "A′ − A  (−VH)" : "C′ − A′  (−Vσ)");
    }

    public sealed class THHCircuit
    {
        private readonly List<StudentCable> cables = new List<StudentCable>();
        private readonly int[] positions = { 1, 1, 1 };
        private int nextId = 1;
        public IReadOnlyList<StudentCable> Cables => cables.AsReadOnly();
        public bool Powered { get; private set; }
        public float IsSetpoint { get; private set; }
        public float ImSetpoint { get; private set; }
        public int Position(int index) => positions[index];
        public static bool IsExternal(Pin pin) => (int)pin >= 0 && (int)pin < 12;

        public static IReadOnlyList<CircuitEdge> FactoryEdges { get; } = Array.AsReadOnly(new[] {
            new CircuitEdge(Pin.IsUpperL, Pin.SampleD), new CircuitEdge(Pin.IsUpperR, Pin.SampleE),
            new CircuitEdge(Pin.IsLowerL, Pin.IsUpperR), new CircuitEdge(Pin.IsLowerR, Pin.IsUpperL),
            new CircuitEdge(Pin.VUpperL, Pin.SampleA), new CircuitEdge(Pin.VUpperR, Pin.SampleAprime),
            new CircuitEdge(Pin.VLowerL, Pin.VUpperR), new CircuitEdge(Pin.VLowerR, Pin.SampleCprime),
            new CircuitEdge(Pin.ImUpperL, Pin.CoilStart), new CircuitEdge(Pin.ImUpperR, Pin.CoilEnd),
            new CircuitEdge(Pin.ImLowerL, Pin.ImUpperR), new CircuitEdge(Pin.ImLowerR, Pin.ImUpperL)
        });

        public bool Add(Pin a, Pin b, out string message)
        {
            if (Powered) { message = "请先归零断电，禁止带电改线"; return false; }
            if (!IsExternal(a) || !IsExternal(b)) { message = "厂家固定端子不可作为学生接线点"; return false; }
            if (a == b) { message = "同一端子不能连接自身"; return false; }
            if (cables.Any(c => (c.A == a && c.B == b) || (c.A == b && c.B == a))) { message = "这两个端子已经连接"; return false; }
            if (cables.Any(c => c.A == a || c.B == a || c.A == b || c.B == b)) { message = "端子已经占用，请先撤销或移除原接线"; return false; }
            if (cables.Count >= 6) { message = "实验外接线最多 6 根，请先撤销或移除接错的导线"; return false; }
            // Incorrect wiring is retained for diagnosis, never silently repaired or energized.
            cables.Add(new StudentCable(nextId++, a, b)); message = Evaluate().Summary; return true;
        }
        public bool Remove(int id)
        {
            if (Powered) return false;
            int index = cables.FindIndex(c => c.Id == id);
            if (index < 0) return false;
            cables.RemoveAt(index); return true;
        }
        public bool Clear()
        { if (Powered) return false; cables.Clear(); return true; }
        public bool ConnectStandard()
        {
            if (Powered) return false;
            cables.Clear();
            for (int i = 0; i < 6; i++) Add((Pin)i, (Pin)(i + 6), out _);
            return true;
        }
        public bool SetSwitch(int index, int position)
        {
            if (index < 0 || index > 2 || position < -1 || position > 1) throw new ArgumentOutOfRangeException();
            if (Powered) return false;
            positions[index] = position; return true;
        }
        public void SetCurrent(float isMilliamps, float imAmps)
        {
            if (float.IsNaN(isMilliamps) || float.IsInfinity(isMilliamps) || float.IsNaN(imAmps) || float.IsInfinity(imAmps))
                throw new ArgumentOutOfRangeException("Nonfinite current setpoint");
            IsSetpoint = Math.Max(0, Math.Min(10, isMilliamps)); ImSetpoint = Math.Max(0, Math.Min(1, imAmps));
        }
        public bool TurnOn(out string message)
        {
            var report = Evaluate();
            if (!report.Ready) { message = report.Summary; return false; }
            if (IsSetpoint != 0 || ImSetpoint != 0) { message = "请先将 IS、IM 两个电流设定归零"; return false; }
            Powered = true; message = "已接通输出；当前回路有效，电压待计算"; return true;
        }
        public void TurnOff() { IsSetpoint = ImSetpoint = 0; Powered = false; }

        private static int Domain(Pin pin)
        {
            int p = (int)pin;
            if (p < 6) return p / 2;
            if (p < 12) return (p - 6) / 2;
            if (p < 24) return (p - 12) / 4;
            if (p <= 25) return 0;
            if (p <= 28) return 1;
            return 2;
        }
        public CircuitReport Evaluate()
        {
            var graph = new Nets(31);
            foreach (var edge in FactoryEdges) graph.Join(edge.A, edge.B);
            for (int index = 0; index < 3; index++) {
                if (positions[index] == 0) continue;
                int contacts = 12 + 4 * index + (positions[index] > 0 ? 0 : 2);
                graph.Join((Pin)(6 + index * 2), (Pin)contacts);
                graph.Join((Pin)(7 + index * 2), (Pin)(contacts + 1));
            }
            foreach (var cable in cables) graph.Join(cable.A, cable.B);
            var report = new CircuitReport {
                HallMode = positions[1] >= 0,
                SourceShort = graph.Same(Pin.IsPlus, Pin.IsMinus) || graph.Same(Pin.ImPlus, Pin.ImMinus),
                MeterShort = graph.Same(Pin.VPlus, Pin.VMinus),
                IsDirection = graph.Orientation(Pin.IsPlus, Pin.IsMinus, Pin.SampleD, Pin.SampleE),
                ImDirection = graph.Orientation(Pin.ImPlus, Pin.ImMinus, Pin.CoilStart, Pin.CoilEnd),
                VoltageDirection = positions[1] == 0 ? 0 : graph.Orientation(Pin.VPlus, Pin.VMinus,
                    positions[1] > 0 ? Pin.SampleA : Pin.SampleAprime,
                    positions[1] > 0 ? Pin.SampleAprime : Pin.SampleCprime)
            };
            // A conductor net may not combine supply/measurement domains, even with the switch open.
            for (int a = 0; a < 31; a++) for (int b = a + 1; b < 31; b++)
                if (Domain((Pin)a) != Domain((Pin)b) && graph.Same((Pin)a, (Pin)b)) report.CrossCircuit = true;
            return report;
        }
        private sealed class Nets
        {
            private readonly int[] parent;
            public Nets(int count) { parent = Enumerable.Range(0, count).ToArray(); }
            private int Root(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
            public void Join(Pin a, Pin b) { parent[Root((int)a)] = Root((int)b); }
            public bool Same(Pin a, Pin b) => Root((int)a) == Root((int)b);
            public int Orientation(Pin positive, Pin negative, Pin a, Pin b)
            {
                if (Same(positive, negative) || Same(a, b)) return 0;
                if (Same(positive, a) && Same(negative, b)) return 1;
                if (Same(positive, b) && Same(negative, a)) return -1;
                return 0;
            }
        }
        public static string Name(Pin pin)
        {
            string[] names = { "测试仪 IS+", "测试仪 IS−", "测试仪 V+", "测试仪 V−", "测试仪 IM+", "测试仪 IM−",
                "实验箱 IS+ 公共端", "实验箱 IS− 公共端", "实验箱 V+ 公共端", "实验箱 V− 公共端", "实验箱 IM+ 公共端", "实验箱 IM− 公共端" };
            return IsExternal(pin) ? names[(int)pin] : pin.ToString();
        }
    }
}
