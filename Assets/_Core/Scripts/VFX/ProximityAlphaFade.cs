using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.VFX
{
    /// <summary>
    /// Fades particle VFX near the camera so large world-space effects do not cover the view.
    /// Prefer shader-level Soft Particles/Camera Fade for final materials; this component is a
    /// safe per-effect fallback for mixed third-party particle hierarchies.
    /// </summary>
    public class ProximityAlphaFade : MonoBehaviour
    {
        [Tooltip("Distance from the camera where fading starts.")]
        public float startFadeDistance = 5f;

        [Tooltip("Distance from the camera where the effect reaches its minimum alpha.")]
        public float fullFadeDistance = 2f;

        [Tooltip("Minimum alpha when the camera is inside the full fade range.")]
        [Range(0f, 1f)] public float minAlpha = 0f;

        [Header("Runtime")]
        [SerializeField] private bool includeChildren = true;
        [Tooltip("Use this object's position as the distance source. This is safer for tall particle pillars whose renderer bounds can overlap the camera while the actual effect is still far away.")]
        [SerializeField] private bool useEffectOriginDistance = true;
        [SerializeField] private Transform distanceAnchor;
        [SerializeField] private bool horizontalDistanceOnly = true;
        [SerializeField] private bool useRendererBounds = true;
        [SerializeField] private bool scaleColorIntensity = true;
        [SerializeField] private float fadeSmoothing = 16f;
        [SerializeField] private float rendererHideThreshold = 0f;
        [SerializeField] private bool disablePermanentlyAtFullFade = true;
        [SerializeField] private float activationGraceDuration = 0.75f;
        [SerializeField] private float fullFadeHoldDuration = 0.25f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int AlphaMultId = Shader.PropertyToID("_AlphaMult");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private readonly List<ParticleState> particleStates = new();
        private readonly List<RendererState> rendererStates = new();
        private readonly List<LightState> lightStates = new();

        private Transform cameraTransform;
        private float currentAlpha = 1f;
        private float lastAppliedAlpha = 1f;
        private float enabledAt;
        private float fullFadeElapsed;
        private bool permanentlyDisabled = false;

        private void Awake()
        {
            CacheTargets();
        }

        private void OnEnable()
        {
            enabledAt = Time.time;
            fullFadeElapsed = 0f;
            currentAlpha = 1f;
            lastAppliedAlpha = 1f;
            permanentlyDisabled = false;
            ResolveCamera();
            ApplyAlpha(1f);
        }

        private void OnDisable()
        {
            ApplyAlpha(1f);
        }

        private void LateUpdate()
        {
            if (permanentlyDisabled || !ResolveCamera()) return;

            float distance = GetCameraDistance();
            bool isInsideFullFade = Time.time - enabledAt >= Mathf.Max(0f, activationGraceDuration)
                && distance <= Mathf.Max(0f, fullFadeDistance);

            if (isInsideFullFade)
            {
                fullFadeElapsed += Time.deltaTime;
            }
            else
            {
                fullFadeElapsed = 0f;
            }

            if (disablePermanentlyAtFullFade && fullFadeElapsed >= Mathf.Max(0f, fullFadeHoldDuration))
            {
                DisablePermanently();
                return;
            }

            float targetAlpha = CalculateTargetAlpha(distance);
            float lerpSpeed = Mathf.Max(1f, fadeSmoothing) * Time.deltaTime;
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, lerpSpeed);
            ApplyAlpha(currentAlpha);
        }

        private void CacheTargets()
        {
            particleStates.Clear();
            rendererStates.Clear();
            lightStates.Clear();

            ParticleSystem[] particles = includeChildren
                ? GetComponentsInChildren<ParticleSystem>(true)
                : GetComponents<ParticleSystem>();

            foreach (ParticleSystem particle in particles)
            {
                ParticleSystem.MainModule main = particle.main;
                particleStates.Add(new ParticleState(particle, main.startColor));
            }

            Renderer[] renderers = includeChildren
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                rendererStates.Add(new RendererState(renderer));
            }

            Light[] lights = includeChildren
                ? GetComponentsInChildren<Light>(true)
                : GetComponents<Light>();

            foreach (Light targetLight in lights)
            {
                lightStates.Add(new LightState(targetLight));
            }
        }

        private bool ResolveCamera()
        {
            if (cameraTransform != null) return true;

            Camera mainCamera = Camera.main;
            if (mainCamera == null) return false;

            cameraTransform = mainCamera.transform;
            return true;
        }

        private float CalculateTargetAlpha(float distance)
        {
            float fadeStart = Mathf.Max(startFadeDistance, fullFadeDistance + 0.01f);
            float fadeEnd = Mathf.Max(0f, fullFadeDistance);

            if (distance >= fadeStart) return 1f;
            if (distance <= fadeEnd) return minAlpha;

            float t = Mathf.InverseLerp(fadeEnd, fadeStart, distance);
            return Mathf.Lerp(minAlpha, 1f, t);
        }

        private float GetCameraDistance()
        {
            Vector3 cameraPosition = cameraTransform.position;

            if (useEffectOriginDistance)
            {
                Transform anchor = distanceAnchor != null ? distanceAnchor : transform;
                Vector3 anchorPosition = anchor.position;
                if (horizontalDistanceOnly)
                {
                    cameraPosition.y = anchorPosition.y;
                }

                return Vector3.Distance(anchorPosition, cameraPosition);
            }

            if (!useRendererBounds || rendererStates.Count == 0)
            {
                return Vector3.Distance(transform.position, cameraPosition);
            }

            float minDistance = float.MaxValue;
            foreach (RendererState rendererState in rendererStates)
            {
                Renderer renderer = rendererState.Renderer;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                Vector3 closestPoint;
                if (renderer is ParticleSystemRenderer)
                {
                    closestPoint = renderer.transform.position;
                }
                else
                {
                    Bounds bounds = renderer.bounds;
                    if (!IsFinite(bounds.center) || !IsFinite(bounds.extents))
                    {
                        continue;
                    }

                    closestPoint = bounds.ClosestPoint(cameraPosition);
                }

                if (!IsFinite(closestPoint)) continue;

                float distance = Vector3.Distance(closestPoint, cameraPosition);
                if (distance < minDistance) minDistance = distance;
            }

            return minDistance == float.MaxValue
                ? Vector3.Distance(transform.position, cameraPosition)
                : minDistance;
        }

        private void DisablePermanently()
        {
            permanentlyDisabled = true;

            foreach (ParticleState state in particleStates)
            {
                state.Stop();
            }

            foreach (RendererState state in rendererStates)
            {
                state.SetHidden(true);
            }

            foreach (LightState state in lightStates)
            {
                state.SetHidden(true);
            }

            gameObject.SetActive(false);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private void ApplyAlpha(float alphaMultiplier)
        {
            float previousAlpha = Mathf.Max(0.0001f, lastAppliedAlpha);

            foreach (ParticleState state in particleStates)
            {
                if (state.Particle == null) continue;

                ParticleSystem.MainModule main = state.Particle.main;
                main.startColor = ScaleGradient(state.OriginalStartColor, alphaMultiplier, scaleColorIntensity);
                state.ApplyLiveParticleAlpha(alphaMultiplier, previousAlpha, scaleColorIntensity);
            }

            bool hideRenderer = rendererHideThreshold > 0f && alphaMultiplier <= rendererHideThreshold;
            foreach (RendererState state in rendererStates)
            {
                state.ApplyAlpha(alphaMultiplier, hideRenderer, scaleColorIntensity);
            }

            foreach (LightState state in lightStates)
            {
                state.ApplyAlpha(alphaMultiplier, hideRenderer);
            }

            lastAppliedAlpha = alphaMultiplier;
        }

        private static ParticleSystem.MinMaxGradient ScaleGradient(ParticleSystem.MinMaxGradient source, float alphaMultiplier, bool scaleRgb)
        {
            switch (source.mode)
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(ScaleColor(source.color, alphaMultiplier, scaleRgb));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(
                        ScaleColor(source.colorMin, alphaMultiplier, scaleRgb),
                        ScaleColor(source.colorMax, alphaMultiplier, scaleRgb));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(ScaleGradient(source.gradient, alphaMultiplier, scaleRgb));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        ScaleGradient(source.gradientMin, alphaMultiplier, scaleRgb),
                        ScaleGradient(source.gradientMax, alphaMultiplier, scaleRgb));
                default:
                    ParticleSystem.MinMaxGradient scaled = new(ScaleGradient(source.gradient, alphaMultiplier, scaleRgb));
                    scaled.mode = source.mode;
                    return scaled;
            }
        }

        private static Gradient ScaleGradient(Gradient source, float alphaMultiplier, bool scaleRgb)
        {
            if (source == null)
            {
                Gradient fallback = new();
                fallback.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(alphaMultiplier, 0f), new GradientAlphaKey(alphaMultiplier, 1f) });
                return fallback;
            }

            GradientColorKey[] colorKeys = source.colorKeys;
            if (scaleRgb)
            {
                for (int i = 0; i < colorKeys.Length; i++)
                {
                    colorKeys[i].color *= alphaMultiplier;
                }
            }

            GradientAlphaKey[] alphaKeys = source.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
            {
                alphaKeys[i].alpha *= alphaMultiplier;
            }

            Gradient scaled = new();
            scaled.mode = source.mode;
            scaled.SetKeys(colorKeys, alphaKeys);
            return scaled;
        }

        private static Color ScaleColor(Color source, float alphaMultiplier, bool scaleRgb)
        {
            if (scaleRgb)
            {
                source.r *= alphaMultiplier;
                source.g *= alphaMultiplier;
                source.b *= alphaMultiplier;
            }

            source.a *= alphaMultiplier;
            return source;
        }

        private sealed class ParticleState
        {
            private ParticleSystem.Particle[] particles;

            public ParticleState(ParticleSystem particle, ParticleSystem.MinMaxGradient originalStartColor)
            {
                Particle = particle;
                OriginalStartColor = originalStartColor;
            }

            public ParticleSystem Particle { get; }
            public ParticleSystem.MinMaxGradient OriginalStartColor { get; }

            public void ApplyLiveParticleAlpha(float alphaMultiplier, float previousAlpha, bool scaleRgb)
            {
                if (Particle == null) return;

                int maxParticles = Particle.main.maxParticles;
                if (maxParticles <= 0) return;

                if (particles == null || particles.Length < maxParticles)
                {
                    particles = new ParticleSystem.Particle[maxParticles];
                }

                int count = Particle.GetParticles(particles);
                float alphaRatio = alphaMultiplier / previousAlpha;
                for (int i = 0; i < count; i++)
                {
                    Color32 startColor32 = particles[i].startColor;
                    Color startColor = startColor32;
                    particles[i].startColor = ScaleColor(startColor, alphaRatio, scaleRgb);
                }

                Particle.SetParticles(particles, count);
            }

            public void Stop()
            {
                if (Particle == null) return;
                Particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private sealed class RendererState
        {
            private readonly MaterialPropertyBlock propertyBlock = new();
            private readonly Color baseColor;
            private readonly Color color;
            private readonly Color tintColor;
            private readonly bool hasBaseColor;
            private readonly bool hasColor;
            private readonly bool hasTintColor;
            private readonly bool hasAlphaMult;
            private readonly bool hasOpacity;
            private readonly float alphaMult;
            private readonly float opacity;

            public RendererState(Renderer renderer)
            {
                Renderer = renderer;

                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material == null) return;

                hasBaseColor = material.HasProperty(BaseColorId);
                hasColor = material.HasProperty(ColorId);
                hasTintColor = material.HasProperty(TintColorId);
                hasAlphaMult = material.HasProperty(AlphaMultId);
                hasOpacity = material.HasProperty(OpacityId);

                if (hasBaseColor) baseColor = material.GetColor(BaseColorId);
                if (hasColor) color = material.GetColor(ColorId);
                if (hasTintColor) tintColor = material.GetColor(TintColorId);
                if (hasAlphaMult) alphaMult = material.GetFloat(AlphaMultId);
                if (hasOpacity) opacity = material.GetFloat(OpacityId);
            }

            public Renderer Renderer { get; }

            public void ApplyAlpha(float alphaMultiplier, bool hideRenderer, bool scaleRgb)
            {
                if (Renderer == null) return;

                Renderer.forceRenderingOff = hideRenderer;
                Renderer.GetPropertyBlock(propertyBlock);

                if (hasBaseColor) propertyBlock.SetColor(BaseColorId, ScaleColor(baseColor, alphaMultiplier, scaleRgb));
                if (hasColor) propertyBlock.SetColor(ColorId, ScaleColor(color, alphaMultiplier, scaleRgb));
                if (hasTintColor) propertyBlock.SetColor(TintColorId, ScaleColor(tintColor, alphaMultiplier, scaleRgb));
                if (hasAlphaMult) propertyBlock.SetFloat(AlphaMultId, alphaMult * alphaMultiplier);
                if (hasOpacity) propertyBlock.SetFloat(OpacityId, opacity * alphaMultiplier);

                Renderer.SetPropertyBlock(propertyBlock);
            }

            public void SetHidden(bool hidden)
            {
                if (Renderer == null) return;
                Renderer.forceRenderingOff = hidden;
            }
        }

        private sealed class LightState
        {
            private readonly float intensity;
            private readonly bool enabled;

            public LightState(Light targetLight)
            {
                Light = targetLight;
                if (targetLight == null) return;

                intensity = targetLight.intensity;
                enabled = targetLight.enabled;
            }

            public Light Light { get; }

            public void ApplyAlpha(float alphaMultiplier, bool hide)
            {
                if (Light == null) return;

                Light.intensity = intensity * alphaMultiplier;
                Light.enabled = enabled && !hide;
            }

            public void SetHidden(bool hidden)
            {
                if (Light == null) return;
                Light.enabled = enabled && !hidden;
            }
        }
    }
}
