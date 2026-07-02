using UnityEngine;
using UnityEngine.AI;
using ActionRPG.Core;
using System;

namespace ActionRPG.Player
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class PlayerMovement : MonoBehaviour
    {
        public NetworkPlayerController core;
        public CharacterController characterController;
        public NavMeshAgent navAgent;

        [Header("Movement Settings")]
        public float moveSpeed = 3.5f;
        public float rotationSpeed = 10f;
        
        [Header("Quest Settings")]
        public float questArrivalRadius = 2.0f;
        public Transform questTarget;
        public GameObject questParticle; // 자동이동 시 켜질 퀘스트 파티클
        public bool isQuestNavigating = false;
        
        public event Action<bool> OnQuestNavigationChanged;

        public void Initialize(NetworkPlayerController coreCtrl, CharacterController ctrl, NavMeshAgent agent)
        {
            this.core = coreCtrl;
            this.characterController = ctrl;
            this.navAgent = agent;
        }

        public void StopMove()
        {
            if (navAgent != null && navAgent.isOnNavMesh && navAgent.enabled)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
        }

        public void StopAutoNavigation()
        {
            if (navAgent != null && navAgent.isOnNavMesh && navAgent.enabled)
            {
                if (!navAgent.isStopped) navAgent.isStopped = true;
            }
            if (isQuestNavigating)
            {
                StopQuestNavigation();
            }
        }

        public void FaceHorizontalPosition(Vector3 targetPos)
        {
            Vector3 dir = targetPos - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }

        public void MoveToPoint(Vector3 targetPos, float stoppingDistance)
        {
            if (navAgent != null && navAgent.isOnNavMesh && navAgent.enabled)
            {
                navAgent.stoppingDistance = stoppingDistance;
                navAgent.isStopped = false;
                navAgent.SetDestination(targetPos);
            }
        }

        public void ManualMove(Vector2 joystickInput)
        {
            if (joystickInput.sqrMagnitude > 0.01f)
            {
                StopAutoNavigation();

                Vector3 moveDir = new Vector3(joystickInput.x, 0f, joystickInput.y).normalized;
                if (moveDir.sqrMagnitude > 0.1f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
                    characterController.Move(moveDir * moveSpeed * Time.deltaTime);
                }
            }
        }

        public void StartQuestNavigation(Transform target)
        {
            if (target == null) return;
            questTarget = target;
            isQuestNavigating = true;
            if (questParticle != null) questParticle.SetActive(true);
            OnQuestNavigationChanged?.Invoke(true);
        }

        public void StopQuestNavigation()
        {
            isQuestNavigating = false;
            questTarget = null;
            if (questParticle != null) questParticle.SetActive(false);
            OnQuestNavigationChanged?.Invoke(false);
            if (navAgent != null && navAgent.isOnNavMesh && navAgent.enabled)
            {
                navAgent.isStopped = true;
            }
        }

        public void ProcessQuestNavigation()
        {
            if (!isQuestNavigating || questTarget == null) return;

            if (navAgent != null && navAgent.isOnNavMesh && navAgent.enabled)
            {
                navAgent.stoppingDistance = questArrivalRadius;
                navAgent.isStopped = false;
                navAgent.SetDestination(questTarget.position);

                if (Vector3.Distance(transform.position, questTarget.position) <= questArrivalRadius)
                {
                    StopQuestNavigation();
                }
            }
        }

        public void ToggleSpawnAreaNavigation(bool isOn)
        {
            if (isOn)
            {
                if (!navAgent.enabled)
                {
                    navAgent.enabled = true;
                    navAgent.updatePosition = true;
                    navAgent.Warp(transform.position);
                }
            }
            else
            {
                if (navAgent.enabled)
                {
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                    navAgent.velocity = Vector3.zero;
                }
            }
        }
    }
}
