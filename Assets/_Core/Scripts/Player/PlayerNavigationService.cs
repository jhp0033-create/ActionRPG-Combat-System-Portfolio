using UnityEngine;
using UnityEngine.AI;

namespace ActionRPG.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerNavigationService : MonoBehaviour
    {
        private CharacterController controller;
        private NavMeshAgent navAgent;
        private Animator animator;

        public void Initialize(
            CharacterController characterController,
            NavMeshAgent agent,
            Animator characterAnimator)
        {
            controller = characterController;
            navAgent = agent;
            animator = characterAnimator;
        }

        public void EnableAgentNavigation()
        {
            PlayerNavigationBridge.EnableAgentNavigation(controller, navAgent, transform);
        }

        public void DisableAgentForManualControl()
        {
            PlayerNavigationBridge.DisableAgentForManualControl(controller, navAgent, transform);
        }

        public void MoveAgentToward(Vector3 destination, float rotationSpeed, float deltaTime)
        {
            PlayerNavigationBridge.MoveAgentToward(navAgent, animator, transform, destination, rotationSpeed, deltaTime);
        }

        public void StopAgent(float deltaTime)
        {
            PlayerNavigationBridge.StopAgent(navAgent, animator, deltaTime);
        }

        public void StopAndClearAgent(float deltaTime)
        {
            PlayerNavigationBridge.StopAndClearAgent(navAgent, animator, deltaTime);
        }

        public void FaceHorizontalPosition(Vector3 worldPosition)
        {
            Vector3 dir = worldPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }

        public float ManualMove(
            Vector2 joystickInput,
            bool lockMovement,
            float moveSpeed,
            float rotationSpeed,
            float gravity,
            float verticalVelocity,
            float deltaTime)
        {
            if (lockMovement)
            {
                joystickInput = Vector2.zero;
            }

            DisableAgentForManualControl();

            if (controller.isGrounded)
            {
                verticalVelocity = -0.5f;
            }
            else
            {
                verticalVelocity += gravity * deltaTime;
            }

            Vector3 moveDirection = Vector3.zero;

            if (joystickInput.magnitude >= 0.1f)
            {
                Transform camTransform = Camera.main.transform;
                Vector3 forward = camTransform.forward;
                Vector3 right = camTransform.right;

                forward.y = 0f;
                right.y = 0f;
                forward.Normalize();
                right.Normalize();

                moveDirection = forward * joystickInput.y + right * joystickInput.x;

                if (moveDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                    float maxDegreesPerSecond = rotationSpeed * 50f;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, maxDegreesPerSecond * deltaTime);
                }

                animator.SetFloat("Speed", joystickInput.magnitude, 0.1f, deltaTime);
            }
            else
            {
                animator.SetFloat("Speed", 0f, 0.1f, deltaTime);
            }

            Vector3 finalMove = moveDirection * moveSpeed;
            finalMove.y = verticalVelocity;
            controller.Move(finalMove * deltaTime);

            return verticalVelocity;
        }
    }
}
