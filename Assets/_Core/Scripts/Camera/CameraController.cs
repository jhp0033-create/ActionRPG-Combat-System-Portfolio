using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.AI;

namespace ActionRPG.CameraSystem
{
    public class CameraController : MonoBehaviour
    {
        [Header("Target Settings")]
        public Transform target;
        public Vector3 offset = new Vector3(0, 3f, -5f);

        [Header("Follow & Rotation Settings")]
        public float smoothSpeed = 10f;
        public float rotationSpeed = 0.4f;
        public float autoAlignSpeed = 2f;

        [Header("Collision Settings")]
        public LayerMask obstacleLayer;
        public float minZoomDistance = 1.5f;

        private float currentYaw = 0f;
        private float currentPitch = 15f;
        private Vector3 currentOffset;
        private bool hasCustomRotation = false;
        [Header("Game Feel / Shake & Zoom")]
        private float shakeDuration = 0f;
        private float shakeMagnitude = 0f;
        private Vector3 shakeOffset = Vector3.zero;
        
        private Camera cam;
        private NavMeshAgent targetNavAgent;
        private float baseFOV = 60f;
        private float zoomDuration = 0f;

        private ActionRPG.Player.NetworkPlayerController playerController;
        private bool isEventsBound = false;

        // 소환 연출 중에는 지표면 기준점을 유지해 카메라 포커스를 안정화합니다.
        private Vector3 _spawnFocusPoint;
        private bool _focusingOnSpawn = false;

        /// <summary>
        /// 타격 피드백용 카메라 흔들림을 시작합니다.
        /// </summary>
        public void TriggerShake(float duration, float magnitude)
        {
            shakeDuration = duration;
            shakeMagnitude = magnitude;
        }

        private float targetFOV;
        
        [Header("Cinematic Focus")]
        public bool isInputLocked = false;
        private Transform cinematicTarget = null;
        private float cinematicZoomModifier = 1f;
        private float currentZoomModifier = 1f;
        private Vector3 currentCinematicLookAt;

        /// <summary>
        /// 지정한 대상에 시선을 두는 시네마틱 포커스를 시작합니다.
        /// </summary>
        public void SetCinematicFocus(Transform newTarget, float zoomModifier = 0.5f)
        {
            cinematicTarget = newTarget;
            cinematicZoomModifier = zoomModifier;
            if (newTarget != null && target != null)
            {
                currentCinematicLookAt = target.position + Vector3.up * 1.5f;
            }
        }

        public void ClearCinematicFocus()
        {
            cinematicTarget = null;
            cinematicZoomModifier = 1f;
        }

        /// <summary>
        /// 일시적인 줌 인 연출을 시작합니다.
        /// </summary>
        public void TriggerZoom(float zoomInAmount, float duration = 0.2f)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null)
            {
                targetFOV = Mathf.Clamp(baseFOV - zoomInAmount, 20f, 100f);
                zoomDuration = duration;
            }
        }

        private void Start()
        {
            currentOffset = offset;
            
            cam = GetComponent<Camera>();
            if (cam != null)
            {
                baseFOV = cam.fieldOfView;
                targetFOV = baseFOV;
            }

            InitializePlayerReferences();
        }

        private void InitializePlayerReferences()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }

            if (target != null && playerController == null)
            {
                playerController = target.GetComponent<ActionRPG.Player.NetworkPlayerController>();
                if (playerController == null)
                {
                    playerController = target.GetComponentInParent<ActionRPG.Player.NetworkPlayerController>();
                }
                if (playerController == null)
                {
                    playerController = target.GetComponentInChildren<ActionRPG.Player.NetworkPlayerController>();
                }
            }

            if (target != null && targetNavAgent == null)
            {
                targetNavAgent = target.GetComponent<NavMeshAgent>();
                if (targetNavAgent == null)
                {
                    targetNavAgent = target.GetComponentInParent<NavMeshAgent>();
                }
                if (targetNavAgent == null)
                {
                    targetNavAgent = target.GetComponentInChildren<NavMeshAgent>();
                }
            }

            BindPlayerEvents();
        }

        private void BindPlayerEvents()
        {
            if (playerController != null && !isEventsBound)
            {
                playerController.OnTargetChanged -= HandleTargetChanged;
                playerController.OnTargetChanged += HandleTargetChanged;
                isEventsBound = true;
            }
        }

        private void UnbindPlayerEvents()
        {
            if (playerController != null && isEventsBound)
            {
                playerController.OnTargetChanged -= HandleTargetChanged;
                isEventsBound = false;
            }
        }

        private void OnDestroy()
        {
            UnbindPlayerEvents();
        }

        /// <summary>
        /// 소환 지점 기준으로 카메라 포커스를 전환합니다.
        /// </summary>
        public void SetSpawnFocus(Vector3 spawnWorldPoint)
        {
            _spawnFocusPoint = spawnWorldPoint;
            _focusingOnSpawn = true;
            hasCustomRotation = false;

            if (target == null) return;
            Vector3 dir = spawnWorldPoint - target.position;
            dir.y = 0f;
            if (dir != Vector3.zero)
            {
                currentYaw = Quaternion.LookRotation(dir).eulerAngles.y;
            }
        }
        private void HandleTargetChanged(Transform previousTarget, Transform newTarget)
        {
            if (newTarget != null && target != null)
            {
                hasCustomRotation = false;

            }
        }

        /// <summary>
        /// 지정 지점을 부드럽게 바라보도록 카메라 방향을 보간합니다.
        /// </summary>
        public void LookAtPointSmoothly(Vector3 point, float? targetPitch = null)
        {
            if (target == null) return;
            
            hasCustomRotation = false;
            
            Vector3 dirToPoint = point - target.position;
            dirToPoint.y = 0f;
            
            if (dirToPoint != Vector3.zero)
            {
                StopCoroutine("LerpYawRoutine");
                StartCoroutine("LerpYawRoutine", new float[] { Quaternion.LookRotation(dirToPoint).eulerAngles.y, targetPitch ?? currentPitch, targetPitch.HasValue ? 1f : 0f });
            }
        }

        private System.Collections.IEnumerator LerpYawRoutine(float[] args)
        {
            float targetYaw = args[0];
            float targetPitch = args[1];
            bool lerpPitch = args[2] > 0.5f;

            float t = 0f;
            float startYaw = currentYaw;
            float startPitch = currentPitch;
            
            while (t < 1f)
            {
                t += Time.deltaTime * 3f;
                currentYaw = Mathf.LerpAngle(startYaw, targetYaw, t);
                if (lerpPitch)
                {
                    currentPitch = Mathf.LerpAngle(startPitch, targetPitch, t);
                }
                yield return null;
            }
        }

        private void LateUpdate()
        {
            if (target == null || playerController == null)
            {
                InitializePlayerReferences();
                if (target == null || playerController == null) return;
            }

            float horizontalInput = 0f;
            float verticalInput = 0f;
            bool isManualControl = false;
            bool hasTarget = playerController != null && playerController.currentTarget != null;
            bool isChargeCameraLocked = playerController != null &&
                (playerController.IsChargingAttack || playerController.IsChargeCameraLocked);
            bool isQuestNavigating = playerController != null && playerController.isQuestNavigating;

            // 타겟 락온과 차지 연출 중에는 수동 카메라 입력보다 전투 포커스를 우선합니다.
            if (!hasTarget && !isChargeCameraLocked && !isInputLocked && !isQuestNavigating)
            {
                if (Player.MobileTouchManager.Instance != null && Player.MobileTouchManager.Instance.CameraDelta.sqrMagnitude > 0)
                {
                    isManualControl = true;
                    hasCustomRotation = true;
                    horizontalInput = Player.MobileTouchManager.Instance.CameraDelta.x;
                    verticalInput = Player.MobileTouchManager.Instance.CameraDelta.y;
                }
            }

            if (isManualControl)
            {
                currentYaw += horizontalInput * rotationSpeed;
                currentPitch -= verticalInput * rotationSpeed;
                currentPitch = Mathf.Clamp(currentPitch, -10f, 60f);
            }
            else
            {
                if (!isChargeCameraLocked && _focusingOnSpawn && playerController != null && !playerController.IsInCombat)
                {
                    Vector3 dirToSpawn = _spawnFocusPoint - target.position;
                    dirToSpawn.y = 0f;
                    if (dirToSpawn != Vector3.zero)
                    {
                        float targetYaw = Quaternion.LookRotation(dirToSpawn).eulerAngles.y;
                        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, 5f * Time.deltaTime);
                        currentPitch = Mathf.Lerp(currentPitch, 18f, 8f * Time.deltaTime);
                    }
                }
                else if (!isChargeCameraLocked && hasTarget)
                {
                    Vector3 dirToTarget;
                    bool targetIsSpawning = playerController.targetEnemyController != null && playerController.targetEnemyController.IsSpawning;

                    if (_focusingOnSpawn && targetIsSpawning)
                    {
                        dirToTarget = _spawnFocusPoint - target.position;
                    }
                    else
                    {
                        _focusingOnSpawn = false;
                        dirToTarget = playerController.currentTarget.position - target.position;
                    }

                    dirToTarget.y = 0f;
                    if (dirToTarget != Vector3.zero)
                    {
                        float targetYaw = Quaternion.LookRotation(dirToTarget).eulerAngles.y;
                        currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, 5f * Time.deltaTime);
                        currentPitch = Mathf.Lerp(currentPitch, 18f, 8f * Time.deltaTime);
                    }
                }
                else if (!isChargeCameraLocked && playerController != null && (playerController.isAutoMode || playerController.isQuestNavigating))
                {
                    if (isQuestNavigating)
                    {
                        hasCustomRotation = false;
                    }

                    if (!hasCustomRotation)
                    {
                        if (TryGetAutoCameraDirection(out Vector3 cameraDirection))
                        {
                            float targetYaw = Quaternion.LookRotation(cameraDirection).eulerAngles.y;
                            currentYaw = Mathf.LerpAngle(currentYaw, targetYaw, autoAlignSpeed * Time.deltaTime);
                        }
                    }
                }
            }

            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            
            Vector3 focusPosition = target.position; 
            
            currentZoomModifier = Mathf.Lerp(currentZoomModifier, cinematicZoomModifier, Time.deltaTime * 3f);
            Vector3 activeOffset = offset * currentZoomModifier;
            
            Vector3 desiredPosition = focusPosition + rotation * activeOffset;

            Vector3 targetHeadPos = focusPosition + Vector3.up * 1.5f;
            RaycastHit hit;
            
            if (Physics.Linecast(targetHeadPos, desiredPosition, out hit, obstacleLayer))
            {
                desiredPosition = hit.point + (targetHeadPos - hit.point).normalized * 0.2f;
            }

            if (Vector3.Distance(targetHeadPos, desiredPosition) < minZoomDistance)
            {
                desiredPosition = targetHeadPos + (desiredPosition - targetHeadPos).normalized * minZoomDistance;
            }

            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            
            Vector3 lookAtTargetPos = targetHeadPos;
            if (cinematicTarget != null)
            {
                currentCinematicLookAt = Vector3.Lerp(currentCinematicLookAt, cinematicTarget.position, Time.deltaTime * 1.0f);
                lookAtTargetPos = currentCinematicLookAt;
            }
            else if (!isChargeCameraLocked && hasTarget && playerController.currentTarget != null)
            {
                Vector3 enemyChestPos = playerController.currentTarget.position + Vector3.up * 1.2f;
                lookAtTargetPos = Vector3.Lerp(targetHeadPos, enemyChestPos, 0.5f);
            }
            
            transform.LookAt(lookAtTargetPos);

            if (shakeDuration > 0)
            {
                shakeOffset = Random.insideUnitSphere * shakeMagnitude;
                
                shakeDuration -= Time.unscaledDeltaTime; 
                
                shakeMagnitude = Mathf.Lerp(shakeMagnitude, 0f, Time.unscaledDeltaTime * 5f);

                transform.position += shakeOffset;
            }
            else
            {
                shakeOffset = Vector3.zero;
            }

            if (zoomDuration > 0)
            {
                zoomDuration -= Time.unscaledDeltaTime;
                if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, Time.unscaledDeltaTime * 15f);
            }
            else if (cam != null && Mathf.Abs(cam.fieldOfView - baseFOV) > 0.1f)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFOV, Time.unscaledDeltaTime * 7f);
            }
        }

        private bool TryGetAutoCameraDirection(out Vector3 direction)
        {
            direction = Vector3.zero;

            if (playerController != null && playerController.isQuestNavigating && targetNavAgent != null && targetNavAgent.enabled && targetNavAgent.isOnNavMesh)
            {
                direction = targetNavAgent.desiredVelocity.sqrMagnitude > 0.01f
                    ? targetNavAgent.desiredVelocity
                    : targetNavAgent.velocity;
            }

            if (direction.sqrMagnitude <= 0.01f && target != null)
            {
                CharacterController charCtrl = target.GetComponent<CharacterController>();
                if (charCtrl != null)
                {
                    direction = charCtrl.velocity;
                }
            }

            if (direction.sqrMagnitude <= 0.01f && target != null)
            {
                Animator anim = target.GetComponent<Animator>();
                if (anim != null && anim.GetFloat("Speed") > 0.1f)
                {
                    direction = target.forward;
                }
            }

            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.01f)
            {
                return false;
            }

            direction.Normalize();
            return true;
        }
    }
}
