using System;
using System.Collections.Generic;

namespace HallLab
{
    // Fit one sweep only; changing the held current invalidates a shared trend line.
    public static class ExperimentFit
    {
        public static bool TryFit(IReadOnlyList<ExperimentRow> rows, bool sweepIs,
            out float slope, out float intercept, out float rSquared, out string reason)
        {
            slope = intercept = rSquared = 0f;
            reason = "至少需要两个不同电流的完整四方向数据点";
            if (rows == null || rows.Count < 2) return false;
            double sx = 0, sy = 0;
            float lowFixed = float.PositiveInfinity, highFixed = float.NegativeInfinity;
            foreach (var row in rows)
            {
                if (row == null || row.label != (sweepIs ? "VH-IS" : "VH-IM") ||
                    !HallEffectMath.IsFinite(row.primaryCurrent) || !HallEffectMath.IsFinite(row.secondaryCurrent) || !HallEffectMath.IsFinite(row.vh))
                { reason = "数据类型不一致或包含非有限值，无法拟合"; return false; }
                if (Math.Abs(row.voltageDirection) != 1 || row.seriesId != rows[0].seriesId)
                { reason = "请选择同一实验批次的数据"; return false; }
                sx += row.SweepCurrent; sy += row.NormalizedHall;
                float held = sweepIs ? row.secondaryCurrent : row.primaryCurrent;
                lowFixed = Math.Min(lowFixed, held); highFixed = Math.Max(highFixed, held);
            }
            if (highFixed - lowFixed > (sweepIs ? .008f : .05f))
            { reason = sweepIs ? "IM 幅值不一致：VH-IS 拟合须固定 IM" : "IS 幅值不一致：VH-IM 拟合须固定 IS"; return false; }
            double mx = sx / rows.Count, my = sy / rows.Count, xx = 0, xy = 0, yy = 0;
            foreach (var row in rows) {
                double dx = row.SweepCurrent - mx, dy = row.NormalizedHall - my;
                xx += dx * dx; xy += dx * dy; yy += dy * dy;
            }
            if (xx < 1e-12) { reason = "横坐标相同，无法计算斜率；请改变扫描电流后记录"; return false; }
            slope = (float)(xy / xx); intercept = (float)(my - slope * mx);
            rSquared = yy < 1e-20 ? 1f : (float)Math.Max(0, Math.Min(1, xy * xy / (xx * yy)));
            reason = ""; return true;
        }
    }
}
