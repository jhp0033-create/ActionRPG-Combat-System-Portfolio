using ActionRPG.Managers;
using UnityEngine;
using UnityEngine.AI;

namespace ActionRPG.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerQuestNavigationService : MonoBehaviour
    {
        private PlayerNavigationService navigationService;
        private NavMeshAgent navAgent;
        private Animator animator;
        private PlayerMovement playerMovement;
        private Coroutine brakeCoroutine;

        [SerializeField] private float smoothBrakeDuration = 3.0f;

        public Transform Target { get; private set; }
        public bool IsNavigating { get; private set; }
        public bool IsCinematicBraking { get; private set; }

        public void Initialize(
            PlayerNavigationService navigation,
            NavMeshAgent agent,
            Animator characterAnimator,
            PlayerMovement movement)
        {
            navigationService = navigation;
            navAgent = agent;
            animator = characterAnimator;
            playerMovement = movement;
        }

        public bool StartNavigation(Transform target)
        {
            if (target == null)
            {
                return false;
            }

            StopBrakeRoutine();
            Target = target;
            IsNavigating = true;
            navigationService.EnableAgentNavigation();

            if (animator != null)
            {
                animator.SetFloat("Speed", 1f);
            }

            SetQuestVisualActive(true);
            return true;
        }

        public bool StopNavigation(bool clearVisual, bool smoothBrake)
        {
            if (!IsNavigating && !smoothBrake)
            {
                return false;
            }

            bool wasNavigating = IsNavigating;
            IsNavigating = false;
            Target = null;

            StopBrakeRoutine();

            if (navAgent != null && navAgent.enabled)
            {
                if (smoothBrake && navAgent.isOnNavMesh)
                {
                    brakeCoroutine = StartCoroutine(SmoothBrakeRoutine());
                }
                else
                {
                    navigationService.StopAgent(Time.deltaTime);
                }
            }

            if (clearVisual)
            {
                SetQuestVisualActive(false);
            }

            return wasNavigating;
        }

        public bool ProcessNavigation(float arrivalRadius, float rotationSpeed, float deltaTime)
        {
            if (Target == null)
            {
                StopNavigation(clearVisual: true, smoothBrake: false);
                return false;
            }

            float distance = Vector3.Distance(transform.position, Target.position);
            if (distance <= arrivalRadius)
            {
                StopNavigation(clearVisual: false, smoothBrake: false);
                return true;
            }

            navigationService.MoveAgentToward(Target.position, rotationSpeed, deltaTime);
            return false;
        }

        public Transform FindSpawnAreaTarget()
        {
            AreaSpawner spawner = Object.FindFirstObjectByType<AreaSpawner>();
            return spawner != null ? spawner.transform : null;
        }

        private void StopBrakeRoutine()
        {
            if (brakeCoroutine == null)
            {
                return;
            }

            StopCoroutine(brakeCoroutine);
            brakeCoroutine = null;
            IsCinematicBraking = false;
        }

        private void SetQuestVisualActive(bool active)
        {
            if (playerMovement != null && playerMovement.questParticle != null)
            {
                playerMovement.questParticle.SetActive(active);
            }
        }

        private System.Collections.IEnumerator SmoothBrakeRoutine()
        {
            IsCinematicBraking = true;

            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, smoothBrakeDuration);
            float originalSpeed = navAgent != null ? navAgent.speed : 3.5f;
            float originalAcceleration = navAgent != null ? navAgent.acceleration : 8f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
                {
                    break;
                }

                navAgent.isStopped = false;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                navAgent.speed = Mathf.Lerp(originalSpeed, 0.05f, eased);
                navAgent.acceleration = Mathf.Max(0.1f, originalAcceleration * 0.35f);

                if (animator != null)
                {
                    float speedRatio = originalSpeed > 0f ? navAgent.velocity.magnitude / originalSpeed : 0f;
                    animator.SetFloat("Speed", speedRatio, 0.1f, Time.deltaTime);
                }

                yield return null;
            }

            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.speed = originalSpeed;
                navAgent.acceleration = originalAcceleration;
                navigationService.StopAndClearAgent(Time.deltaTime);
            }

            IsCinematicBraking = false;
            brakeCoroutine = null;
        }
    }
}
