using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HallLab
{
    public enum TerminalId
    {
        IsSourcePlus,
        IsSourceMinus,
        IsSwitchInPlus,
        IsSwitchInMinus,
        IsSwitchOutPlus,
        IsSwitchOutMinus,
        IsSwitchAlternatePlus,
        IsSwitchAlternateMinus,

        SampleCurrentPlus,
        SampleCurrentMinus,
        ImSourcePlus,
        ImSourceMinus,
        ImSwitchInPlus,
        ImSwitchInMinus,
        ImSwitchOutPlus,
        ImSwitchOutMinus,
        ImSwitchAlternatePlus,
        ImSwitchAlternateMinus,

        CoilPlus,
        CoilMinus,
        SampleHallPlus,
        SampleHallMinus,
        SampleConductivityPlus,
        SampleConductivityMinus,
        VhSwitchInPlus,
        VhSwitchInMinus,
        VhSwitchOutPlus,
        VhSwitchOutMinus,
        VhSwitchAlternatePlus,
        VhSwitchAlternateMinus,

        MeterPlus,
        MeterMinus
    }

    public enum KnifeSwitchKind
    {
        IsPolarity,
        Measurement,
        ImPolarity
    }

    public enum MeasurementMode
    {
        HallVoltage,
        ConductivityVoltage
    }

    public enum ExperimentTab
    {
        TableIs,
        TableIm,
        Curves,
        Theory
    }

    [Serializable]
    public class ExperimentRow
    {
        public string id = Guid.NewGuid().ToString("N");
        public string seriesId = "legacy";
        public int voltageDirection = 1;
        public float is1, is2, is3, is4, im1, im2, im3, im4;
        public float primaryCurrent;
        public float secondaryCurrent;
        // Retain the existing serialized/CSV meaning: primary is IS in mA,
        // secondary is IM in A. Only the plotted independent variable changes.
        public float SweepCurrent => label == "VH-IM" ? secondaryCurrent : primaryCurrent;
        public float NormalizedHall => vh * voltageDirection;
        public float v1;
        public float v2;
        public float v3;
        public float v4;
        public float vh;
        public float b1, b2, b3, b4; // Signed simulated field at each reading, tesla.
        public string label = "";
    }

    public struct ElectricalReading
    {
        public float hallMillivolts;
        public float misalignmentMillivolts;
        public float ettinghausenMillivolts;
        public float nernstMillivolts;
        public float righiLeducMillivolts;
        public float noiseMillivolts;
        public float conductivityMillivolts;
        public float totalMillivolts;
    }

    public struct MagneticFieldState
    {
        public float magneticFieldAmpsPerMeter;
        public float magnetizationAmpsPerMeter;
        public float fluxDensityTesla;
        public float anhystereticMagnetizationAmpsPerMeter;
    }

    public static class HallEffectMath
    {
        public const float ElementaryChargeCoulombs = 1.602176634e-19f;
        // Supplied TH-H reference specimen. These are physical dimensions, not
        // the enlarged world-space dimensions used by the scene builder.
        public const string SampleMaterialName = "N型硅单晶（参考样品）";
        public const float CarrierChargeSign = -1f;
        public const float SampleThicknessMm = 0.5f;
        public const float SampleLengthMm = 3.0f; // Voltage-electrode spacing L.
        public const float SampleWidthMm = 4.0f;
        // Retained demonstration assumptions: no measured n or mobility supplied yet.
        public const float CarrierVolumeDensityPerCubicMeter = 2.30e22f;
        public const float CarrierMobilitySquareMetersPerVoltSecond = 0.50f;
        public const float HallGeometryFactor = 1.0f; // Ideal geometry in the supplied formula.
        public const float SampleResistivityOhmMeters =
            1f /
            (CarrierVolumeDensityPerCubicMeter *
             ElementaryChargeCoulombs *
             CarrierMobilitySquareMetersPerVoltSecond);
        public const float CoilTurns = 1200.0f;
        public const float MagneticPathLengthMeters = 0.70f;
        public const float MagneticCircuitCoupling = 0.755f;
        public const float MagneticSaturationMagnetizationAmpsPerMeter = 750000.0f;
        public const float MagneticAnhystereticShapeAmpsPerMeter = 50.0f;
        public const string MagneticModelId = "C0-Langevin-relaxation-v1 (uncalibrated; no hysteresis/remanence)";
        public const float MagneticResponsePerSecond = 5.0f;
        public const float CoilWindingSign = 1.0f;
        public const float CoilResistanceOhms = 7.6f;
        public const float CoilInductanceHenries = 1.2f;

        public static float InstrumentOffset(float noiseSeed)
        {
            return Mathf.Sin(noiseSeed * 0.731f) * 0.028f +
                   Mathf.Sin(noiseSeed * 2.137f + 0.8f) * 0.011f;
        }

        public static ElectricalReading Evaluate(
            float isMilliamps,
            int isPolarity,
            float imAmps,
            int imPolarity,
            MeasurementMode mode,
            float phase,
            float noiseSeed)
        {
            float signedImAmps = imAmps * imPolarity;
            float signedB = MagneticFieldTarget(signedImAmps);
            return Evaluate(
                isMilliamps,
                isPolarity,
                signedB,
                mode,
                phase,
                noiseSeed,
                SampleThicknessMm);
        }

        public static ElectricalReading Evaluate(
            float isMilliamps,
            int isPolarity,
            float signedMagneticField,
            MeasurementMode mode,
            float phase,
            float noiseSeed,
            float sampleThicknessMm)
        {
            float signedIsMilliamps = isMilliamps * isPolarity;
            float safeThicknessMm = Mathf.Max(0.05f, sampleThicknessMm);
            float signedB = signedMagneticField;
            float thicknessMeters = safeThicknessMm * 0.001f;
            float signedCarrierCharge = CarrierChargeSign * ElementaryChargeCoulombs;
            float hallCoefficientVoltsPerAmpereTesla =
                1f / Mathf.Max(
                    0.0000001f,
                    CarrierVolumeDensityPerCubicMeter *
                    Mathf.Abs(signedCarrierCharge) *
                    thicknessMeters);

            // The current is in mA. The Hall coefficient returns V/(A*T), so
            // the A conversion and the V-to-mV display conversion cancel.
            float hallVoltageMillivolts =
                HallGeometryFactor *
                signedCarrierCharge /
                Mathf.Abs(signedCarrierCharge) *
                signedIsMilliamps *
                signedB *
                hallCoefficientVoltsPerAmpereTesla;

            float sampleResistanceOhms =
                SampleResistanceOhms(safeThicknessMm);
            var result = new ElectricalReading
            {
                hallMillivolts = hallVoltageMillivolts,
                misalignmentMillivolts = 0.032f * signedIsMilliamps,
                ettinghausenMillivolts =
                    0.010f * signedIsMilliamps * signedB,
                nernstMillivolts = 0.021f * signedB,
                righiLeducMillivolts = 0.008f * signedB,
                // mA multiplied by ohms gives mV.
                conductivityMillivolts =
                    signedIsMilliamps * sampleResistanceOhms
            };

            float deterministicNoise =
                Mathf.Sin((phase * 11.3f) + noiseSeed) * 0.0010f +
                Mathf.Sin((phase * 4.7f) + noiseSeed * 1.7f) * 0.0005f;
            float thermalDrift = Mathf.Sin(phase * 0.19f + noiseSeed * 0.1f) * 0.006f;
            result.noiseMillivolts =
                InstrumentOffset(noiseSeed) + deterministicNoise + thermalDrift;

            if (mode == MeasurementMode.ConductivityVoltage)
            {
                result.totalMillivolts =
                    result.conductivityMillivolts + result.noiseMillivolts;
            }
            else
            {
                // Four transverse magnetic effects are shown explicitly:
                // V_perp = VH + VE + VN + VRL.  Contact misalignment and
                // deterministic instrument noise are separate measurement
                // terms; they are not counted as additional magnetic effects.
                result.totalMillivolts =
                    result.hallMillivolts +
                    result.misalignmentMillivolts +
                    result.ettinghausenMillivolts +
                    result.nernstMillivolts +
                    result.righiLeducMillivolts +
                    result.noiseMillivolts;
            }

            return result;
        }

        public static float MagneticFieldTarget(float signedImAmps)
        {
            if (!IsFinite(signedImAmps)) throw new ArgumentException("Current must be finite.");
            var state = default(MagneticFieldState);
            state.magneticFieldAmpsPerMeter =
                AppliedMagneticFieldAmpsPerMeter(signedImAmps);
            state.magnetizationAmpsPerMeter = AnhystereticMagnetization(
                state.magneticFieldAmpsPerMeter);
            state.anhystereticMagnetizationAmpsPerMeter =
                state.magnetizationAmpsPerMeter;
            state.fluxDensityTesla = FluxDensityTesla(
                state.magneticFieldAmpsPerMeter,
                state.magnetizationAmpsPerMeter);
            return state.fluxDensityTesla;
        }

        public static float UpdateMagneticField(
            float signedIm,
            float previousField,
            float deltaTime)
        {
            if (!IsFinite(signedIm) || !IsFinite(previousField) || !IsFinite(deltaTime))
                throw new ArgumentException("Magnetic input must be finite.");
            if (deltaTime <= 0f) return previousField;
            // Existing C0 equilibrium curve with an exact first-order step response.
            // This demonstration model has no hysteresis or remanence; it is not TH-H calibration.
            float response = (float)(1.0 - Math.Exp(-MagneticResponsePerSecond * deltaTime));
            return Mathf.Lerp(previousField, MagneticFieldTarget(signedIm), response);
        }

        public static void UpdateMagneticField(
            float signedImAmps, ref MagneticFieldState state, float deltaTime)
        {
            float next = UpdateMagneticField(signedImAmps, state.fluxDensityTesla, deltaTime);
            if (deltaTime <= 0f) return;
            float response = (float)(1.0 - Math.Exp(-MagneticResponsePerSecond * deltaTime));
            state.magneticFieldAmpsPerMeter = Mathf.Lerp(state.magneticFieldAmpsPerMeter,
                AppliedMagneticFieldAmpsPerMeter(signedImAmps), response);
            state.fluxDensityTesla = next;
            state.magnetizationAmpsPerMeter = next / 1.25663706e-6f - state.magneticFieldAmpsPerMeter;
            state.anhystereticMagnetizationAmpsPerMeter =
                AnhystereticMagnetization(AppliedMagneticFieldAmpsPerMeter(signedImAmps));
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static float CoilTerminalVoltageVolts(float signedImAmps, float signedDiDt)
        {
            return CoilResistanceOhms * signedImAmps +
                   CoilInductanceHenries * signedDiDt;
        }

        public static float SampleResistanceOhms(float thicknessMm)
        {
            float lengthMeters = SampleLengthMm * 0.001f;
            float widthMeters = SampleWidthMm * 0.001f;
            float thicknessMeters = Mathf.Max(0.05f, thicknessMm) * 0.001f;
            return SampleResistivityOhmMeters * lengthMeters /
                   Mathf.Max(0.000001f, widthMeters * thicknessMeters);
        }

        public static float AppliedMagneticFieldAmpsPerMeter(float signedCurrentAmps)
        {
            return signedCurrentAmps *
                   CoilWindingSign *
                   CoilTurns /
                   Mathf.Max(0.01f, MagneticPathLengthMeters) *
                   MagneticCircuitCoupling;
        }

        public static float MagneticAxisSign(float signedCurrentAmps)
        {
            float field = AppliedMagneticFieldAmpsPerMeter(signedCurrentAmps);
            if (Mathf.Abs(field) < 0.001f)
            {
                return 0f;
            }

            return Mathf.Sign(field);
        }

        private static float AnhystereticMagnetization(float effectiveFieldAmpsPerMeter)
        {
            float shape = Mathf.Max(
                0.001f,
                MagneticAnhystereticShapeAmpsPerMeter);
            float x = effectiveFieldAmpsPerMeter / shape;
            float langevin;
            if (Mathf.Abs(x) < 0.001f)
            {
                langevin = x / 3f;
            }
            else
            {
                float absoluteX = Mathf.Abs(x);
                float coth = (float)(1.0 / Math.Tanh(absoluteX));
                langevin = Mathf.Sign(x) * (coth - 1f / absoluteX);
            }

            return MagneticSaturationMagnetizationAmpsPerMeter *
                   Mathf.Clamp(langevin, -1f, 1f);
        }

        private static float FluxDensityTesla(
            float fieldAmpsPerMeter,
            float magnetizationAmpsPerMeter)
        {
            const float permeabilityOfFreeSpace = 1.25663706e-6f;
            return permeabilityOfFreeSpace *
                   (fieldAmpsPerMeter + magnetizationAmpsPerMeter);
        }

    }

    public readonly struct TerminalConnectionPair : IEquatable<TerminalConnectionPair>
    {
        public readonly TerminalId A;
        public readonly TerminalId B;

        public TerminalConnectionPair(TerminalId first, TerminalId second)
        {
            if ((int)first <= (int)second)
            {
                A = first;
                B = second;
            }
            else
            {
                A = second;
                B = first;
            }
        }

        public bool Equals(TerminalConnectionPair other)
        {
            return A == other.A && B == other.B;
        }

        public override bool Equals(object obj)
        {
            return obj is TerminalConnectionPair other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)A * 397) ^ (int)B;
            }
        }

        public static bool operator ==(TerminalConnectionPair left, TerminalConnectionPair right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TerminalConnectionPair left, TerminalConnectionPair right)
        {
            return !left.Equals(right);
        }
    }
}

