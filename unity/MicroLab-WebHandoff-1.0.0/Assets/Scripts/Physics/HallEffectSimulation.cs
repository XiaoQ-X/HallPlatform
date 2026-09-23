using System;
using UnityEngine;

namespace HallEffectLab
{
    /// <summary>
    /// Numerical electron transport model in normalized electromagnetic units.
    /// The charge is negative, so every visible direction follows q(E + v x B).
    /// </summary>
    public sealed class HallEffectSimulation
    {
        public const int HighlightedParticleCount = 7;

        public struct ParticleSnapshot
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public bool IsAbsorbed;
            public int WrapSerial;
        }

        private struct ParticleState
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float ScatterTimer;
            public float AbsorbedTimer;
            public int WrapSerial;
        }

        private const float ElectronCharge = -1f;
        private const float ElectronMass = 1f;
        private const float DefaultRelaxationTime = 0.72f;
        private const float SurfaceFluxGain = 2.6f;

        private readonly ParticleState[] _particles;
        private readonly int _randomSeed;
        private System.Random _random;

        private Vector2 _externalElectricField;
        private float _magneticField;
        private float _surfaceChargeDensity;
        private float _smoothedTransverseVelocity;
        private float _settledTimer;
        private float _elapsedModelTime;
        private float _meanTransverseVelocity;
        private int _particleCount;
        private float _plateHalfWidth;
        private float _plateHalfHeight;
        private float _driftEntrySpeed;

        public HallEffectSimulation(int particleCount = 84, int randomSeed = 170917)
        {
            _particles = new ParticleState[Mathf.Max(particleCount, HighlightedParticleCount)];
            _randomSeed = randomSeed;
            _random = new System.Random(_randomSeed);
        }

        public bool HallFieldEnabled { get; set; } = true;
        public int ParticleCount => _particleCount;
        public float ElapsedModelTime => _elapsedModelTime;
        public float PlateHalfWidth => _plateHalfWidth;
        public float PlateHalfHeight => _plateHalfHeight;
        public float SurfaceChargeDensity => _surfaceChargeDensity;
        public float MeanTransverseVelocity => _meanTransverseVelocity;
        public float SmoothedTransverseVelocity => _smoothedTransverseVelocity;
        public Vector2 ExternalElectricField => _externalElectricField;
        public Vector2 HallElectricField => HallFieldEnabled && _magneticField > 0.0001f
            ? new Vector2(0f, -_surfaceChargeDensity)
            : Vector2.zero;
        public Vector2 DriftVelocity => new Vector2(-DefaultRelaxationTime * _externalElectricField.x, 0f);
        public Vector2 ElectricForce => ElectronCharge * _externalElectricField;
        public float ExternalElectricFieldMagnitude => _externalElectricField.x;
        public float MagneticFieldMagnitude => _magneticField;
        public float HallElectricFieldMagnitude => Mathf.Abs(HallElectricField.y);
        public float HallFieldError => Mathf.Abs(
            HallElectricField.y - TargetHallElectricField.y);
        public float TransverseCurrentDensity => -_meanTransverseVelocity;
        public float HallVoltage => -HallElectricField.y * (_plateHalfHeight * 2f);
        public float SteadyTransverseVelocityTolerance => 0.035f;

        public Vector2 TargetHallElectricField
        {
            get
            {
                float targetY = -Mathf.Abs(DriftVelocity.x) * _magneticField;
                return new Vector2(0f, targetY);
            }
        }

        public float TargetSurfaceChargeDensity => -TargetHallElectricField.y;

        public int CurrentStage
        {
            get
            {
                if (_elapsedModelTime < 0.8f || _surfaceChargeDensity < 0.012f)
                {
                    return 0;
                }

                float target = Mathf.Max(TargetSurfaceChargeDensity, 0.001f);
                if (_surfaceChargeDensity < target * 0.58f)
                {
                    return 1;
                }

                if (_settledTimer < 0.72f || _surfaceChargeDensity < target * 0.82f)
                {
                    return 2;
                }

                return 3;
            }
        }

        public void Reset(float externalElectricField, float magneticField)
        {
            _random = new System.Random(_randomSeed);
            _externalElectricField = new Vector2(Mathf.Max(0.01f, externalElectricField), 0f);
            // Keep the scalar field non-negative: +Z is the positive direction
            // shown by the UI symbol and force arrows.
            _magneticField = Mathf.Clamp(magneticField, 0f, 2f);
            _surfaceChargeDensity = 0f;
            _smoothedTransverseVelocity = 0f;
            _settledTimer = 0f;
            _elapsedModelTime = 0f;
            _meanTransverseVelocity = 0f;
            _particleCount = _particles.Length;
            _plateHalfWidth = 6.8f;
            _plateHalfHeight = 2.45f;
            _driftEntrySpeed = DefaultRelaxationTime * _externalElectricField.x;

            for (int i = 0; i < _particleCount; i++)
            {
                bool highlighted = i < HighlightedParticleCount;
                float lane = highlighted
                    ? -0.57f + i * (1.14f / Mathf.Max(1, HighlightedParticleCount - 1))
                    : -0.92f + NextFloat() * 1.84f;

                float x = highlighted
                    ? -5.2f + i * 1.62f
                    : -_plateHalfWidth + 0.35f + NextFloat() * (_plateHalfWidth * 2f - 0.7f);

                _particles[i] = new ParticleState
                {
                    Position = new Vector2(x, lane * _plateHalfHeight),
                    Velocity = new Vector2(
                        -_driftEntrySpeed,
                        highlighted ? 0.018f * Mathf.Sin(i * 1.37f) : (NextFloat() - 0.5f) * 0.035f),
                    ScatterTimer = 0.35f + NextFloat() * 0.55f,
                    AbsorbedTimer = 0f,
                    WrapSerial = 0
                };
            }

            RecalculateAverages();
        }

        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // Fixed substeps keep the coupled surface-charge/Lorentz integration stable.
            const float maximumSubstep = 1f / 180f;
            float remaining = Mathf.Min(deltaTime, 0.25f);
            while (remaining > 0f)
            {
                float step = Mathf.Min(maximumSubstep, remaining);
                StepInternal(step);
                remaining -= step;
            }
        }

        public void AdvanceTo(float targetModelTime)
        {
            targetModelTime = Mathf.Max(0f, targetModelTime);
            if (targetModelTime < _elapsedModelTime)
            {
                // Replaying the same random seed makes rewinding reproduce the
                // physical state, instead of changing only the time label.
                Reset(_externalElectricField.x, _magneticField);
            }
            if (targetModelTime <= _elapsedModelTime)
            {
                return;
            }

            float remaining = targetModelTime - _elapsedModelTime;
            const float replayStep = 1f / 180f;
            while (remaining > 0f)
            {
                float step = Mathf.Min(replayStep, remaining);
                StepInternal(step);
                remaining -= step;
            }
        }

        public ParticleSnapshot GetParticle(int index)
        {
            ParticleState particle = _particles[Mathf.Clamp(index, 0, _particleCount - 1)];
            return new ParticleSnapshot
            {
                Position = particle.Position,
                Velocity = particle.Velocity,
                IsAbsorbed = particle.AbsorbedTimer > 0f,
                WrapSerial = particle.WrapSerial
            };
        }

        public Vector2 GetLorentzForce(int index)
        {
            ParticleState particle = _particles[Mathf.Clamp(index, 0, _particleCount - 1)];
            return ElectronCharge * CrossWithMagneticField(particle.Velocity);
        }

        public Vector2 GetTotalForce(int index)
        {
            ParticleState particle = _particles[Mathf.Clamp(index, 0, _particleCount - 1)];
            Vector2 magnetic = ElectronCharge * CrossWithMagneticField(particle.Velocity);
            Vector2 electric = ElectronCharge * (_externalElectricField + HallElectricField);
            Vector2 drag = -ElectronMass * particle.Velocity / DefaultRelaxationTime;
            return electric + magnetic + drag;
        }

        public float GetLateralForceBalance(int index)
        {
            ParticleState particle = _particles[Mathf.Clamp(index, 0, _particleCount - 1)];
            float magneticY = ElectronCharge * (-particle.Velocity.x * _magneticField);
            float hallForceY = ElectronCharge * HallElectricField.y;
            float denominator = Mathf.Abs(magneticY) + Mathf.Abs(hallForceY) + 0.0001f;
            return Mathf.Clamp01(1f - Mathf.Abs(magneticY + hallForceY) / denominator);
        }

        private void StepInternal(float deltaTime)
        {
            float activeCount = 0f;
            float transverseVelocitySum = 0f;

            for (int i = 0; i < _particleCount; i++)
            {
                ParticleState particle = _particles[i];

                if (particle.AbsorbedTimer > 0f)
                {
                    particle.AbsorbedTimer -= deltaTime;
                    if (particle.AbsorbedTimer <= 0f)
                    {
                        particle.Position = new Vector2(_plateHalfWidth - 0.28f, NextFloatCentered() * 1.45f);
                        particle.Velocity = new Vector2(
                            -_driftEntrySpeed,
                            (NextFloat() - 0.5f) * 0.035f);
                        particle.WrapSerial++;
                    }

                    _particles[i] = particle;
                    continue;
                }

                Vector2 magneticForce = ElectronCharge * CrossWithMagneticField(particle.Velocity);
                Vector2 electricForce = ElectronCharge * (_externalElectricField + HallElectricField);
                Vector2 dragForce = -ElectronMass * particle.Velocity / DefaultRelaxationTime;
                Vector2 acceleration = (magneticForce + electricForce + dragForce) / ElectronMass;

                // Semi-implicit Euler: v first, then x, which keeps circular motion bounded.
                particle.Velocity += acceleration * deltaTime;
                particle.Position += particle.Velocity * deltaTime;

                particle.ScatterTimer -= deltaTime;
                if (particle.ScatterTimer <= 0f)
                {
                    float scatterAngle = NextFloat() * Mathf.PI * 2f;
                    float scatterStrength = 0.018f + NextFloat() * 0.032f;
                    particle.Velocity += new Vector2(
                        Mathf.Cos(scatterAngle),
                        Mathf.Sin(scatterAngle)) * scatterStrength;
                    particle.ScatterTimer = 0.28f + NextFloat() * 0.48f;
                }

                if (particle.Position.y <= -_plateHalfHeight + 0.035f)
                {
                    particle.Position.y = -_plateHalfHeight + 0.035f;
                    particle.Velocity = Vector2.zero;
                    particle.AbsorbedTimer = 0.22f + NextFloat() * 0.26f;
                }
                else if (particle.Position.y >= _plateHalfHeight - 0.035f)
                {
                    particle.Position.y = _plateHalfHeight - 0.035f;
                    particle.Velocity.y = -Mathf.Abs(particle.Velocity.y) * 0.2f;
                }

                if (particle.Position.x < -_plateHalfWidth - 0.18f)
                {
                    particle.Position.x += _plateHalfWidth * 2f + 0.36f;
                    particle.WrapSerial++;
                }
                else if (particle.Position.x > _plateHalfWidth + 0.18f)
                {
                    particle.Position.x -= _plateHalfWidth * 2f + 0.36f;
                    particle.WrapSerial++;
                }

                _particles[i] = particle;
                activeCount += 1f;
                transverseVelocitySum += particle.Velocity.y;
            }

            _meanTransverseVelocity = activeCount > 0.001f
                ? transverseVelocitySum / activeCount
                : 0f;

            // sigma is the positive magnitude of the exposed +Y ion sheet (equivalently,
            // the magnitude of the electron sheet at -Y). Surface charge follows
            // d(sigma)/dt = J_y = -n e <v_y>. At steady state J_y and this rate both become zero,
            // but the accumulated field remains instead of being artificially damped.
            // Random scattering must not create a rectified Hall field when B=0.
            // This model represents the magnetic transverse response, not
            // thermodynamic fluctuations of surface charge.
            _surfaceChargeDensity = _magneticField > 0.0001f
                ? Mathf.Max(0f, _surfaceChargeDensity - SurfaceFluxGain * _meanTransverseVelocity * deltaTime)
                : 0f;

            float smoothing = Mathf.Clamp01(deltaTime * 2.8f);
            _smoothedTransverseVelocity +=
                (_meanTransverseVelocity - _smoothedTransverseVelocity) * smoothing;

            float target = Mathf.Max(TargetSurfaceChargeDensity, 0.001f);
            bool transverselySettled =
                Mathf.Abs(_smoothedTransverseVelocity) < SteadyTransverseVelocityTolerance &&
                _surfaceChargeDensity > target * 0.82f;
            _settledTimer = transverselySettled ? _settledTimer + deltaTime : 0f;
            _elapsedModelTime += deltaTime;
        }

        private Vector2 CrossWithMagneticField(Vector2 velocity)
        {
            return new Vector2(velocity.y * _magneticField, -velocity.x * _magneticField);
        }

        private void RecalculateAverages()
        {
            float sum = 0f;
            for (int i = 0; i < _particleCount; i++)
            {
                sum += _particles[i].Velocity.y;
            }

            _meanTransverseVelocity = _particleCount > 0 ? sum / _particleCount : 0f;
        }

        private float NextFloat()
        {
            return (float)_random.NextDouble();
        }

        private float NextFloatCentered()
        {
            return NextFloat() * 2f - 1f;
        }
    }
}
