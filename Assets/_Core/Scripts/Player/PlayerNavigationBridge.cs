using UnityEngine;
using UnityEngine.AI;

namespace ActionRPG.Player
{
    /// <summary>
    /// Centralizes ownership switches between CharacterController movement and NavMeshAgent navigation.
    /// </summary>
    public static class PlayerNavigationBridge
    {
        public static void EnableAgentNavigation(
            CharacterController controller,
            NavMeshAgent navAgent,
            Transform owner)
        {
            if (navAgent == null || owner == null)
            {
                return;
            }

            if (controller != null)
            {
                controller.enabled = false;
            }

            if (!navAgent.enabled)
            {
                navAgent.enabled = true;
                navAgent.updatePosition = true;
                navAgent.Warp(owner.position);
            }
        }

        public static void DisableAgentForManualControl(
            CharacterController controller,
            NavMeshAgent navAgent,
            Transform owner)
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
                if (owner != null)
                {
                    navAgent.Warp(owner.position);
                }

                navAgent.enabled = false;
            }

            if (controller != null)
            {
                controller.enabled = true;
            }
        }

        public static void StopAgent(NavMeshAgent navAgent, Animator animator, float deltaTime)
        {
            if (navAgent != null && navAgent.enabled)
            {
                navAgent.isStopped = true;
            }

            SetAnimatorSpeed(animator, 0f, 0.15f, deltaTime);
        }

        public static void StopAndClearAgent(NavMeshAgent navAgent, Animator animator, float deltaTime)
        {
            SetAnimatorSpeed(animator, 0f, 0.15f, deltaTime);

            if (navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh)
            {
                return;
            }

            navAgent.isStopped = true;
            navAgent.ResetPath();
            navAgent.velocity = Vector3.zero;
        }

        public static void MoveAgentToward(
            NavMeshAgent navAgent,
            Animator animator,
            Transform owner,
            Vector3 destination,
            float rotationSpeed,
            float deltaTime)
        {
            if (navAgent == null || owner == null)
            {
                return;
            }

            navAgent.isStopped = false;
            navAgent.SetDestination(destination);
            RotateToDesiredVelocity(navAgent, owner, rotationSpeed, deltaTime);
            SetAnimatorSpeed(animator, 1f, 0.1f, deltaTime);
        }

        private static void RotateToDesiredVelocity(
            NavMeshAgent navAgent,
            Transform owner,
            float rotationSpeed,
            float deltaTime)
        {
            Vector3 moveDir = navAgent.desiredVelocity.normalized;
            if (moveDir.sqrMagnitude <= 0.1f)
            {
                return;
            }

            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            owner.rotation = Quaternion.RotateTowards(owner.rotation, targetRot, rotationSpeed * 50f * deltaTime);
        }

        private static void SetAnimatorSpeed(Animator animator, float speed, float dampTime, float deltaTime)
        {
            if (animator != null)
            {
                animator.SetFloat("Speed", speed, dampTime, deltaTime);
            }
        }
    }
}
