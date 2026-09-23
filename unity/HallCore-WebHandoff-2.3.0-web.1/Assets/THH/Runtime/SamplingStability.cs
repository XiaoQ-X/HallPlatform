using UnityEngine;

namespace HallLab
{
    // Numerical acceptance criteria for this simulation, not TH-H instrument accuracy.
    public sealed class SamplingStability
    {
        public const float IsToleranceMilliamps = 0.025f;
        public const float ImToleranceAmps = 0.004f;
        public const float FieldToleranceTesla = 0.001f;
        public const float FieldRateToleranceTeslaPerSecond = 0.005f;
        public const float HoldSeconds = 0.25f;
        private float held, lastField, targetIs, targetIm;
        private int isSign, imSign;
        private bool hasPrevious;
        private int stableObservations;
        public bool Ready => held >= HoldSeconds && stableObservations >= 4;

        public void Reset() { held = 0f; stableObservations = 0; hasPrevious = false; }

        public bool Matches(float isSetpoint, float imSetpoint, int isPolarity, int imPolarity) =>
            hasPrevious && targetIs == isSetpoint && targetIm == imSetpoint &&
            isSign == isPolarity && imSign == imPolarity;

        public void Update(bool eligible, float actualIs, float actualIm,
            float isSetpoint, float imSetpoint, int isPolarity, int imPolarity, float field, float dt)
        {
            if (!eligible || !HallEffectMath.IsFinite(actualIs) || !HallEffectMath.IsFinite(actualIm) ||
                !HallEffectMath.IsFinite(isSetpoint) || !HallEffectMath.IsFinite(imSetpoint) ||
                !HallEffectMath.IsFinite(field) || !HallEffectMath.IsFinite(dt) || dt <= 0f ||
                Mathf.Abs(isPolarity) != 1 || Mathf.Abs(imPolarity) != 1)
            { Reset(); return; }

            bool same = Matches(isSetpoint, imSetpoint, isPolarity, imPolarity);
            float rate = same ? Mathf.Abs(field - lastField) / dt : float.PositiveInfinity;
            float target = HallEffectMath.MagneticFieldTarget(imSetpoint * imPolarity);
            bool near = Mathf.Abs(actualIs - isSetpoint) <= IsToleranceMilliamps &&
                Mathf.Abs(actualIm - imSetpoint) <= ImToleranceAmps && field * target > 0f &&
                Mathf.Abs(field - target) <= FieldToleranceTesla &&
                rate <= FieldRateToleranceTeslaPerSecond;
            // Require multiple independent observations; a slow frame earns at most 0.1s.
            // A long interruption invalidates the previous observation sequence.
            bool accepted = same && near && dt <= 0.5f;
            held = accepted ? held + Mathf.Min(dt, 0.1f) : 0f;
            stableObservations = accepted ? stableObservations + 1 : 0;
            lastField = field;
            targetIs = isSetpoint; targetIm = imSetpoint;
            isSign = isPolarity; imSign = imPolarity;
            hasPrevious = true;
        }
    }
}

