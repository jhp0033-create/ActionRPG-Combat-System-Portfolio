using UnityEngine;
using UnityEngine.AI;
using ActionRPG.Core;
using ActionRPG.Data;
using ActionRPG.UI;

namespace ActionRPG.Player
{
    /// <summary>
    /// 플레이어 전투 흐름을 조율하고 세부 기능은 전담 서비스 컴포넌트에 위임합니다.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(PlayerAutoBattleService))]
    [RequireComponent(typeof(PlayerComboService))]
    [RequireComponent(typeof(PlayerSkillService))]
    [RequireComponent(typeof(PlayerNavigationService))]
    [RequireComponent(typeof(PlayerTargetingService))]
    [RequireComponent(typeof(PlayerQuestNavigationService))]
    [RequireComponent(typeof(PlayerSkillExecutor))]
    [RequireComponent(typeof(PlayerChargedSkillService))]
    [RequireComponent(typeof(PlayerNetworkSyncService))]
    public partial class NetworkPlayerController : MonoBehaviour, IDamageable
    {
        [Header("Movement Settings")]
        public float moveSpeed = 3.5f;
        public float rotationSpeed = 10f;

        [Header("Player Stats")]
        public float maxHealth = 500f;
        private float currentHealth;

        [Header("World Bar UI")]
        [Tooltip("WorldUICanvas 위에 배치된 PlayerWorldBar 컴포넌트를 연결하세요.")]
        public PlayerWorldBar playerWorldBar;

        [Header("Profile HUD UI")]
        [Tooltip("통합 프로필 HUD UI 컴포넌트를 연결하세요. (비어있을 시 자동 탐색)")]
        [SerializeField] private ActionRPGBattleSystem.UI.UI_PlayerProfileHUD profileHUD;

        private Renderer[] childRenderers;
        private MaterialPropertyBlock propBlock;
        private Coroutine hitFlashCoroutine;
        
        [Header("Targeting & Auto Battle")]
        public bool isAutoMode = true;
        private bool ignoreJoystickUntilRelease = false;

        public System.Action<Transform, Transform> OnTargetChanged;

        private Transform _currentTarget;
        public Transform currentTarget
        {
            get => _currentTarget;
            set
            {
                if (_currentTarget != value)
                {
                    Transform previous = _currentTarget;
                    _currentTarget = value;
                    OnTargetChanged?.Invoke(previous, _currentTarget);
                }
            }
        }

        public ActionRPG.Enemy.EnemyController targetEnemyController;
        
        public float autoTargetRadius = 15f;
        public float attackRadius = 2.5f;
        public float autoAttackCooldown = 0.5f;
        private float lastAutoAttackTime;

        [Header("Quest Navigation")]
        public Transform questTarget => questNavigationService != null ? questNavigationService.Target : null;
        public bool isQuestNavigating => questNavigationService != null && questNavigationService.IsNavigating;
        public float questArrivalRadius = 2.0f;
        public System.Action<bool> OnQuestNavigationChanged;

        private StateMachine fsm;
        [Header("Required Components")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private NavMeshAgent navAgent;
        [SerializeField] private Animator animator;
        [SerializeField] private WeaponManager weaponManager;
        private PlayerAutoBattleService autoBattleService;
        private PlayerComboService comboService;
        private PlayerSkillService skillService;
        private PlayerNavigationService navigationService;
        private PlayerTargetingService targetingService;
        private PlayerQuestNavigationService questNavigationService;
        private PlayerSkillExecutor skillExecutor;
        private PlayerChargedSkillService chargedSkillService;
        private PlayerNetworkSyncService networkSyncService;

        [Header("State Flags")]
        public bool isAttacking = false;
        private bool isMovingAttack = false;
        private bool isAttackReserved = false;

        [Header("Physics Settings")]
        [Tooltip("적용할 중력 가속도 (기본 -9.81)")]
        public float gravity = -9.81f;
        private float verticalVelocity = 0f;

        [Header("Combo System")]
        public bool canSaveAttack = false;
        public bool saveAttack = false;
        public int comboStep = 0;
        public int AttackSequence { get; private set; } = 0;
        private bool comboInProgress = false;
        [HideInInspector] public bool isSkillComboActive = false;
        [HideInInspector] public bool skillComboAdvanceReady = false;
        [HideInInspector] public int activeSkillIndex = -1;
        [HideInInspector] public int pendingSkillIndex = -1;
        public bool IsChargingAttack { get; private set; } = false;
        public bool IsChargeCameraLocked { get; private set; } = false;
        public bool IsChargePostHitFrozen { get; private set; } = false;
        [Header("Charge Camera Lock")]
        [SerializeField] private float chargeCameraLockDuration = 0.45f;
        private Coroutine chargeCameraLockCoroutine;
        private Coroutine autoComboCoroutine;
        private int autoComboCastSequence = 0;
        private int lastComboVfxCastSequence = -1;
        private int lastComboVfxAttackIndex = -1;
        private int lastComboSkillStartFrame = -1;
        private float lastComboSkillStartTime = -999f;
        private const float COMBO_SKILL_DUPLICATE_WINDOW = 0.25f;
        private float comboSkillRecastLockedUntil = -999f;
        private const float COMBO_SKILL_RECAST_LOCK_DURATION = 1.25f;

        [Header("Skill System")]
        public SkillData[] equippedSkills = new SkillData[6];
        private float[] skillCooldownTimers = new float[6];
        public float[] SkillCooldownTimers => skillCooldownTimers;
        
        private int[] skillCharges = new int[6];
        public int[] SkillCharges => skillCharges;

        private float ComboDelayTime = 0.32f;
        private float lastBasicComboEndTime = 0f;

        [Header("Attack VFX Angles")]
        public Vector3 comboStep1Angle = new Vector3(0f, 10f, 0f);
        public Vector3 comboStep2Angle = new Vector3(0f, -10f, -15f);
        public Vector3 comboStep3Angle = new Vector3(0f, 10f, 15f);

        private float lastVfxSpawnTime = 0f;
        private Coroutine delayedReservedAttackCoroutine;

        [Header("Gold System")]
        private readonly PlayerProgressionState progressionState = new PlayerProgressionState();
        public int CurrentGold => progressionState.Gold;
        public System.Action<int> OnGoldChanged;

        [Header("Level & EXP System")]
        public int CurrentLevel => progressionState.Level;
        public float CurrentExp => progressionState.Exp;

        private float attackLockTimeout = 0f;
        public float lastAttackStartTime { get; private set; }
        private const float MAX_ATTACK_LOCK_DURATION = 3.0f;
        private float suppressAutoChaseUntil = 0f;
        
        [Header("Cinematic State")]
        public bool isInputLocked = false;

        [Header("Combat State System")]
        public bool IsInCombat { get; private set; } = false;
        private float lastCombatActivityTime = 0f;
        private const float COMBAT_TIMEOUT = 5.0f;
        public event System.Action<bool> OnCombatStateChanged;
        public System.Action<bool> OnAutoModeChanged;

        private bool hasEngagedInFirstCombat = false;

        private Vector3 networkPosition;
        private Quaternion networkRotation;
        public Vector3 NetworkPosition => networkPosition;
        public Quaternion NetworkRotation => networkRotation;
        public float NetworkPositionError => networkSyncService != null ? networkSyncService.ServerPositionError : 0f;

        private void Awake()
        {
            PlayerComponentResolver.ResolveRequiredComponents(this, ref controller, ref navAgent, ref animator, ref weaponManager);
            ResolveRuntimeServices();

            fsm = new StateMachine();
            PlayerComponentResolver.ConfigureManualNavigation(navAgent, moveSpeed);

            networkPosition = transform.position;
            networkRotation = transform.rotation;

            childRenderers = GetComponentsInChildren<Renderer>(true);
            propBlock = new MaterialPropertyBlock();
        }

        private void ResolveRuntimeServices()
        {
            autoBattleService = ResolveRequiredService<PlayerAutoBattleService>();
            comboService = ResolveRequiredService<PlayerComboService>();
            skillService = ResolveRequiredService<PlayerSkillService>();
            navigationService = ResolveRequiredService<PlayerNavigationService>();
            targetingService = ResolveRequiredService<PlayerTargetingService>();
            questNavigationService = ResolveRequiredService<PlayerQuestNavigationService>();
            skillExecutor = ResolveRequiredService<PlayerSkillExecutor>();
            chargedSkillService = ResolveRequiredService<PlayerChargedSkillService>();
            networkSyncService = ResolveRequiredService<PlayerNetworkSyncService>();

            if (autoBattleService == null || comboService == null || skillService == null || navigationService == null || targetingService == null || questNavigationService == null || skillExecutor == null || chargedSkillService == null || networkSyncService == null)
            {
                enabled = false;
                return;
            }

            navigationService.Initialize(controller, navAgent, animator);
            questNavigationService.Initialize(navigationService, navAgent, animator, GetComponent<PlayerMovement>());
            chargedSkillService.Initialize(this, navigationService, navAgent, animator);
            networkSyncService.Initialize(transform);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (activeSkillIndex < 0 || equippedSkills == null || activeSkillIndex >= equippedSkills.Length)
            {
                return;
            }

            SkillData skill = equippedSkills[activeSkillIndex];
            if (skill == null || skill.skillType == SkillType.ComboAttack || skill.skillType == SkillType.ChargeAttack)
            {
                return;
            }

            ChargeAttackHitArea.GetHitShapeFromSkill(skill, out float forwardOffset, out float radius, out float heightOffset);
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();

            Vector3 center = transform.position + forward * forwardOffset + Vector3.up * heightOffset;
            Color skillDamageGizmoColor = new Color(0.2f, 0.85f, 1f, 0.35f);
            Gizmos.color = skillDamageGizmoColor;
            Gizmos.DrawSphere(center, radius);
            Gizmos.color = new Color(skillDamageGizmoColor.r, skillDamageGizmoColor.g, skillDamageGizmoColor.b, 0.95f);
            Gizmos.DrawWireSphere(center, radius);
            Gizmos.DrawLine(transform.position + Vector3.up * heightOffset, center);
        }
#endif

        private T ResolveRequiredService<T>() where T : Component
        {
            if (TryGetComponent(out T service))
            {
                return service;
            }

            Debug.LogError($"[NetworkPlayerController] Required service component missing: {typeof(T).Name}", this);
            return null;
        }

        private void Start()
        {
            currentHealth = maxHealth;

            playerWorldBar = PlayerStatusPresenter.ResolveWorldBar(playerWorldBar);
            profileHUD = PlayerStatusPresenter.ResolveProfileHud(profileHUD);
            PlayerStatusPresenter.Initialize(playerWorldBar, profileHUD, currentHealth, maxHealth, progressionState.Exp, progressionState.RequiredExp, progressionState.Level);

            skillExecutor.Initialize(ref equippedSkills, skillCooldownTimers, skillCharges);

            var overlay = FindFirstObjectByType<ActionRPG.UI.UI_GamePlayOverlay>();
            if (overlay == null)
            {
                GameObject uiOverlayObj = new GameObject("UI_GamePlayOverlay");
                overlay = uiOverlayObj.AddComponent<ActionRPG.UI.UI_GamePlayOverlay>();
            }
            overlay.Initialize(this);

            fsm.Initialize(new PlayerIdleState(fsm, this, animator));

            if (isAutoMode)
            {
                FindAutoTarget();
            }
        }

        private void Update()
        {
            fsm.Update();
            UpdateSkillCooldowns();
            UpdateCombatState();
            UpdateAnimatorLayers();

            if (isInputLocked)
            {
                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = true;
                }
                if (animator != null)
                {
                    animator.SetFloat("Speed", 0f, 0.15f, Time.deltaTime);
                }
                return;
            }

            if (animator != null && !isAttacking)
            {
                animator.speed = 1.0f;
            }

            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                UseSkill(1);
            }

            // 공격 중에도 타겟 방향을 유지해 타격 판정과 연출을 정렬합니다.
            if (isAttacking && !IsChargingAttack && !IsChargePostHitFrozen && currentTarget != null)
            {
                Vector3 dirToTarget = (currentTarget.position - transform.position).normalized;
                dirToTarget.y = 0f;
                if (dirToTarget != Vector3.zero)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * 2f * Time.deltaTime);
                }
            }

            // 타겟 생명주기와 이동 상태를 함께 정리합니다.
            if (currentTarget != null)
            {
                if (!currentTarget.gameObject.activeInHierarchy || (targetEnemyController != null && targetEnemyController.IsDead))
                {
                    currentTarget = null;
                    targetEnemyController = null;
                    
                    if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                    {
                        navAgent.isStopped = true;
                        navAgent.ResetPath();
                        navAgent.velocity = Vector3.zero;
                    }
                    if (animator != null)
                    {
                        animator.SetFloat("Speed", 0f);
                    }
                }
            }

            if (isAttacking)
            {
                attackLockTimeout += Time.deltaTime;
                if (attackLockTimeout >= MAX_ATTACK_LOCK_DURATION)
                {

                    ResetAttackLock();
                    attackLockTimeout = 0f;
                }
            }
            else
            {
                attackLockTimeout = 0f;
            }

            bool hasManualMoveInput = false;

            if (isAttacking)
            {
                if (!IsChargePostHitFrozen && TryHandleManualMoveInput(ref hasManualMoveInput))
                {
                    return;
                }

                StopAutoNavigation();
                TryReserveAutoComboDuringAttack();
                return;
            }

            if (!hasEngagedInFirstCombat && currentTarget != null)
            {
                hasEngagedInFirstCombat = true;
                ignoreJoystickUntilRelease = true;

                if (!isAutoMode)
                {
                    ToggleAutoMode();
                }
            }
            else if (!IsChargingAttack && !IsChargePostHitFrozen && MobileTouchManager.Instance != null && MobileTouchManager.Instance.MoveInput.sqrMagnitude > 0)
            {
                if (!ignoreJoystickUntilRelease)
                {
                    if (isAutoMode) ToggleAutoMode();
                    if (isQuestNavigating) StopQuestNavigation(true);
                    pendingSkillIndex = -1;
                    ManualMove(MobileTouchManager.Instance.MoveInput);
                    hasManualMoveInput = true;
                }
            }
            else if (MobileTouchManager.Instance != null && MobileTouchManager.Instance.MoveInput.sqrMagnitude == 0)
            {
                ignoreJoystickUntilRelease = false;
            }
            
            if (UnityEngine.InputSystem.Keyboard.current != null && !hasManualMoveInput)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                float h = 0f; float v = 0f;
                if (keyboard.wKey.isPressed) v += 1f;
                if (keyboard.sKey.isPressed) v -= 1f;
                if (keyboard.dKey.isPressed) h += 1f;
                if (keyboard.aKey.isPressed) h -= 1f;
                
                Vector2 inputVector = new Vector2(h, v).normalized;
                if (!IsChargingAttack && !IsChargePostHitFrozen && inputVector.sqrMagnitude > 0)
                {
                    if (isAutoMode) ToggleAutoMode();
                    if (isQuestNavigating) StopQuestNavigation(true);
                    pendingSkillIndex = -1;
                    ManualMove(inputVector);
                    hasManualMoveInput = true;
                }
            }

            // 사거리 밖 스킬은 타겟 접근 후 자동 시전합니다.
            if (pendingSkillIndex != -1 && currentTarget != null && !hasManualMoveInput && !isAttacking)
            {
                float dist = Vector3.Distance(transform.position, currentTarget.position);
                if (dist <= equippedSkills[pendingSkillIndex].range)
                {
                    int indexToCast = pendingSkillIndex;
                    pendingSkillIndex = -1;
                    
                    if (navAgent.enabled)
                    {
                        navAgent.isStopped = true;
                        animator.SetFloat("Speed", 0f, 0.15f, Time.deltaTime);
                    }
                    UseSkill(indexToCast);
                }
                else
                {
                    if (!navAgent.enabled)
                    {
                        controller.enabled = false;
                        navAgent.enabled = true;
                        navAgent.updatePosition = true;
                        navAgent.Warp(transform.position);
                    }
                    navAgent.isStopped = false;
                    navAgent.SetDestination(currentTarget.position);
                    
                    Vector3 moveDir = navAgent.desiredVelocity.normalized;
                    if (moveDir.sqrMagnitude > 0.1f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(moveDir);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * 50f * Time.deltaTime);
                    }
                    animator.SetFloat("Speed", 1f, 0.1f, Time.deltaTime);
                }
                return;
            }
            else if (pendingSkillIndex != -1 && (currentTarget == null || isAttacking || hasManualMoveInput))
            {
                pendingSkillIndex = -1;
            }

            if (questNavigationService.IsCinematicBraking)
            {
            }
            else if (isQuestNavigating && !hasManualMoveInput && !isAttacking && !isAttackReserved)
            {
                ProcessQuestNavigation();
            }
            else if (isAutoMode && !hasManualMoveInput && !isAttacking && !isAttackReserved)
            {
                ProcessAutoBattle();
            }
            else if (!hasManualMoveInput)
            {
                ManualMove(Vector2.zero);
            }

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard.digit1Key.wasPressedThisFrame) UseSkill(0);
                if (keyboard.digit2Key.wasPressedThisFrame) UseSkill(1);
                if (keyboard.digit3Key.wasPressedThisFrame) UseSkill(2);

                if (!isAttacking)
                {
                    if (keyboard.qKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                    {
                        ChangeTargetLeftRight(true);
                    }
                    if (keyboard.eKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                    {
                        ChangeTargetLeftRight(false); // 오른쪽 타겟으로 전환
                    }
                }
            }

            // 4. 스페이스바: 자동 전투(Auto-Battle) 토글
            if (!IsChargingAttack && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ToggleAutoMode();
            }
        }

        private void LateUpdate()
        {
            UpdateNetworkSync();
        }

        private void UpdateNetworkSync()
        {
            if (networkSyncService == null)
            {
                networkPosition = transform.position;
                networkRotation = transform.rotation;
                return;
            }

            networkSyncService.Tick(transform.position, transform.rotation);
            networkPosition = networkSyncService.InterpolatedPosition;
            networkRotation = networkSyncService.InterpolatedRotation;
        }

        public void ToggleAutoMode()
        {
            isAutoMode = !isAutoMode;

            
            OnAutoModeChanged?.Invoke(isAutoMode); // UI 연동용 이벤트 호출

            if (isAutoMode)
            {
                FindAutoTarget(); // 켜자마자 바로 타겟 스캔
            }
            else
            {
                // 수동으로 전환 시 네비게이션 정지 및 대기 상태로
                if (navAgent.enabled) navAgent.isStopped = true;
                currentTarget = null;
                targetEnemyController = null;
                animator.SetFloat("Speed", 0f, 0.15f, Time.deltaTime);
            }
        }

        // --- 애니메이션/이펙트 관련 ---
        [Header("Effects & Bones")]
        [Tooltip("스킬 시전 시 기 모으는 파티클이 부착될 트랜스폼 (예: RightHand 또는 무기 끝)")]
        public Transform skillChargePoint;

        [Tooltip("차지어택 판정 범위 미리보기 (비어 있으면 자식에서 자동 탐색/생성)")]
        public ChargeAttackHitArea chargeHitAreaPreview;

        private GameObject currentChargeVfx;
        private int skillCastSequence = 0;
        private int lastSkillDamageSequence = -1;

        // --- 스킬 시스템 (수동 오버라이드) ---

        private void UpdateSkillCooldowns()
        {
            skillExecutor.TickCooldowns(Time.deltaTime);
        }

        internal void SetChargingAttack(bool isCharging)
        {
            IsChargingAttack = isCharging;
        }

        internal void SetChargePostHitFrozen(bool isFrozen)
        {
            IsChargePostHitFrozen = isFrozen;
        }

        internal void SetMovingAttack(bool movingAttack)
        {
            isMovingAttack = movingAttack;
        }

        internal void MarkAttackStarted()
        {
            lastAttackStartTime = Time.time;
        }

        internal void SetCurrentChargeVfx(GameObject chargeVfx)
        {
            currentChargeVfx = chargeVfx;
        }

        internal void ReleaseCurrentChargeVfx()
        {
            if (currentChargeVfx == null)
            {
                return;
            }

            PlayerSkillEffects.Release(currentChargeVfx);
            currentChargeVfx = null;
        }

        /// <summary>
        /// 수동 스킬 입력을 처리하고 필요한 경우 자동 접근 후 시전합니다.
        /// </summary>
        public void UseSkill(int index)
        {
            if (IsChargingAttack) return;

            SetCombatActivity();

            if (!isAutoMode)
            {
                ToggleAutoMode();
            }

            if (!skillExecutor.CanBeginCast(index))
            {
                return;
            }

            SkillData skill = skillExecutor.GetSkill(index);
            bool isComboAttack = skillService.IsComboAttack(skill);

            if (isAttacking)
            {
                bool isCurrentAttackBasic = skillService.IsBasicOrComboAttack(activeSkillIndex, equippedSkills);

                if (skillService.IsComboAttack(skill) && isSkillComboActive)
                {
                    return;
                }
                else if (isCurrentAttackBasic)
                {
                    DisableWeaponHitbox();
                    saveAttack = false;
                    canSaveAttack = false;
                    isSkillComboActive = false;
                    comboStep = 0;
                    
                    chargedSkillService.Cancel();
                    
                    if (animator != null)
                    {
                        animator.ResetTrigger("Attack");
                        animator.ResetTrigger("Attack_Full");
                        animator.Play("Locomotion", 0, 0f);
                    }
                    
                }
                else
                {
                    return;
                }
            }

            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                FindAutoTarget();
            }

            if (currentTarget != null)
            {
                float dist = Vector3.Distance(transform.position, currentTarget.position);
                if (dist > skill.range)
                {

                    pendingSkillIndex = index;
                    return;
                }
            }

            if (navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
                animator.SetFloat("Speed", 0f, 0.15f, Time.deltaTime);
            }

            if (currentTarget != null)
            {
                Vector3 dirToTarget = (currentTarget.position - transform.position).normalized;
                dirToTarget.y = 0f;
                if (dirToTarget != Vector3.zero) transform.rotation = Quaternion.LookRotation(dirToTarget);
            }

            if (isComboAttack &&
                (Time.frameCount == lastComboSkillStartFrame ||
                 Time.time - lastComboSkillStartTime < COMBO_SKILL_DUPLICATE_WINDOW ||
                 Time.time < comboSkillRecastLockedUntil))
            {
                return;
            }

            isAttacking = true;
            canSaveAttack = false;
            saveAttack = false;

            if (animator != null)
            {
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("Attack_Full");
            }

            activeSkillIndex = index;
            skillCastSequence++;
            lastSkillDamageSequence = -1;

            if (skillService.IsComboAttack(skill))
            {
                isSkillComboActive = true;
                lastComboVfxCastSequence = -1;
                lastComboVfxAttackIndex = -1;
                lastComboSkillStartFrame = Time.frameCount;
                lastComboSkillStartTime = Time.time;
                comboSkillRecastLockedUntil = Time.time + COMBO_SKILL_RECAST_LOCK_DURATION;
                
                if (autoComboCoroutine != null) StopCoroutine(autoComboCoroutine);
                autoComboCastSequence = skillCastSequence;
                autoComboCoroutine = StartCoroutine(AutoComboRoutine(index, autoComboCastSequence));
            }
            else if (skill.skillType == SkillType.ChargeAttack)
            {
                isSkillComboActive = false;
                comboStep = 0;
                
                chargedSkillService.Cast(index);
            }
            else
            {
                isSkillComboActive = false;
                comboStep = 0;

                if (animator != null)
                {
                    float speedScale = skill.animSpeedMultiplier;
                    animator.speed = 1.3f * speedScale;
                }

                isMovingAttack = false;
                isAttacking = true;
                lastAttackStartTime = Time.time;
                if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
                {
                    navAgent.isStopped = true;
                    navAgent.ResetPath();
                    navAgent.velocity = Vector3.zero;
                }
                animator.SetTrigger(skill.animationTriggerName + "_Full");

            }
            
            if (!skillService.IsComboAttack(skill))
            {
                PlayerSkillEffects.PlayCastAudio(skill);
                PlayerSkillEffects.SpawnCastVFX(skill, transform, currentTarget, GetEffectCanvasParent());
            }

            if (currentChargeVfx != null)
            {
                PlayerSkillEffects.Release(currentChargeVfx);
                currentChargeVfx = null;
            }

            if (skill.chargeVfxPrefab != null)
            {
                Transform parentBone = skillChargePoint != null ? skillChargePoint : transform;
                currentChargeVfx = PlayerSkillEffects.SpawnChargeVFX(skill.chargeVfxPrefab, parentBone);
            }
            
            skillExecutor.ConsumeChargeAndStartCooldown(index);
        }

        private System.Collections.IEnumerator AutoComboRoutine(int skillIndex, int castSequence)
        {
            isSkillComboActive = true;
            
            ExecuteComboAttackStep(0, skillIndex, castSequence);

            yield return new WaitUntil(() => skillComboAdvanceReady || !IsCurrentAutoCombo(skillIndex, castSequence));
            if (!IsCurrentAutoCombo(skillIndex, castSequence)) yield break;
            
            ExecuteComboAttackStep(1, skillIndex, castSequence);
            
            yield return null; 
            
            yield return new WaitUntil(() => skillComboAdvanceReady || !IsCurrentAutoCombo(skillIndex, castSequence));
            if (!IsCurrentAutoCombo(skillIndex, castSequence)) yield break;

            ExecuteComboAttackStep(2, skillIndex, castSequence);
            
            autoComboCoroutine = null;
        }

        private bool IsCurrentAutoCombo(int skillIndex, int castSequence)
        {
            return isAttacking
                && isSkillComboActive
                && activeSkillIndex == skillIndex
                && skillCastSequence == castSequence
                && autoComboCastSequence == castSequence;
        }

        private void ExecuteComboAttackStep(int attackIndex, int skillIndex, int castSequence)
        {
            if (!IsCurrentAutoCombo(skillIndex, castSequence)) return;

            SetCombatActivity();
            isAttacking = true;
            canSaveAttack = false;
            saveAttack = false;
            skillComboAdvanceReady = false;
            lastAttackStartTime = Time.time;
            comboInProgress = true;
            AttackSequence++;
            comboStep = Mathf.Clamp(attackIndex + 1, 1, 3);

            if (currentTarget != null)
            {
                Vector3 dirToTarget = (currentTarget.position - transform.position).normalized;
                dirToTarget.y = 0f;
                if (dirToTarget != Vector3.zero) transform.rotation = Quaternion.LookRotation(dirToTarget);
            }

            if (animator != null)
            {
                float speedScale = equippedSkills[skillIndex].animSpeedMultiplier;
                animator.speed = PlayerAttackAnimationProfile.GetSkillComboSpeed(attackIndex, speedScale);

                animator.SetInteger("AttackIndex", attackIndex);

                isMovingAttack = false;
                animator.SetTrigger("Attack_Full");
            }
            
            Vector3 eulerOffset = GetSkillComboVfxEulerOffset(equippedSkills[skillIndex], attackIndex);
            
            PlayerSkillEffects.PlayCastAudio(equippedSkills[skillIndex]);
            if (lastComboVfxCastSequence != castSequence || lastComboVfxAttackIndex != attackIndex)
            {
                lastComboVfxCastSequence = castSequence;
                lastComboVfxAttackIndex = attackIndex;
                PlayerSkillEffects.SpawnCastVFX(equippedSkills[skillIndex], transform, currentTarget, GetEffectCanvasParent(), Quaternion.Euler(eulerOffset));
            }
        }

        private Vector3 GetSkillComboVfxEulerOffset(SkillData skill, int attackIndex)
        {
            if (skill == null || skill.skillType != SkillType.ComboAttack)
            {
                return Vector3.zero;
            }

            if (attackIndex == 1) return skill.comboVfxStep2EulerOffset;
            if (attackIndex == 2) return skill.comboVfxStep3EulerOffset;
            return skill.comboVfxStep1EulerOffset;
        }

        public void OpenSkillComboAdvanceWindow()
        {
            if (isSkillComboActive)
            {
                skillComboAdvanceReady = true;
            }
        }

        // --- 퀘스트 네비게이션 시스템 ---

        public void StartQuestNavigation(Transform target)
        {
            if (!questNavigationService.StartNavigation(target)) return;

            currentTarget = null;
            targetEnemyController = null;

            OnQuestNavigationChanged?.Invoke(true);
        }

        public void StopQuestNavigation(bool clearVisual = true, bool smoothBrake = false)
        {
            if (questNavigationService.StopNavigation(clearVisual, smoothBrake))
            {
                OnQuestNavigationChanged?.Invoke(false);
            }
        }

        // 다이나믹 버튼 전용: 스폰 구역(AreaSpawner)으로 자동 이동 / 중지 토글
        public void ToggleSpawnAreaNavigation()
        {
            if (isQuestNavigating)
            {
                // 이미 자동 이동 중이면 중지 (시각 효과도 끄기)
                StopQuestNavigation(true);
            }
            else
            {
                Transform spawnAreaTarget = questNavigationService.FindSpawnAreaTarget();
                if (spawnAreaTarget != null)
                {
                    StartQuestNavigation(spawnAreaTarget);
                }
              
            }
        }

        private void ProcessQuestNavigation()
        {
            if (questNavigationService.ProcessNavigation(questArrivalRadius, rotationSpeed, Time.deltaTime))
            {
                if (isAutoMode) FindAutoTarget(); 
            }
        }

        /// <summary>
        /// 자동 전투의 탐색, 접근, 공격 예약을 처리합니다.
        /// </summary>
        private void ProcessAutoBattle()
        {
            if (IsChargePostHitFrozen)
            {
                StopAutoNavigation();
                return;
            }

            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                currentTarget = null;
                targetEnemyController = null;
                FindAutoTarget();
            }
            else
            {
                if (targetEnemyController != null && targetEnemyController.IsDead)
                {
                    currentTarget = null;
                    targetEnemyController = null;
                    FindAutoTarget();
                }
                else if (targetEnemyController != null && targetEnemyController.IsSpawning)
                {
                }
            }

            if (IsWatchingSpawnFocus && spawnFocusLookPosition != Vector3.zero && !IsInCombat)
            {
                StopAutoNavigation();
                navigationService.FaceHorizontalPosition(spawnFocusLookPosition);
                return;
            }

            if (targetEnemyController != null && targetEnemyController.IsSpawning)
            {
                StopAutoNavigation();

                Vector3 lookPoint = spawnFocusLookPosition != Vector3.zero
                    ? spawnFocusLookPosition
                    : currentTarget.position;
                navigationService.FaceHorizontalPosition(lookPoint);
                return;
            }

            if (IsWatchingSpawnFocus && (targetEnemyController == null || !targetEnemyController.IsSpawning))
            {
                IsWatchingSpawnFocus = false;
                spawnFocusLookPosition = Vector3.zero;
            }

            // 자동 모드에서는 애니메이션 예약 창에 맞춰 다음 기본 공격을 버퍼링합니다.
            bool isSingleSkillActive = skillService.IsSingleSkillActive(activeSkillIndex, isSkillComboActive);

            if (autoBattleService.ShouldReserveCombo(isSingleSkillActive, isSkillComboActive, comboStep, isAttacking, canSaveAttack, saveAttack, currentTarget))
            {
                saveAttack = true;
            }

            if (isAttacking)
            {
                StopAutoNavigation();
                return;
            }

            if (Time.time < suppressAutoChaseUntil)
            {
                StopAutoNavigation();
                return;
            }

            if (currentTarget == null)
            {
                StopAutoNavigation();
                return;
            }

            navigationService.EnableAgentNavigation();

            float distance = Vector3.Distance(transform.position, currentTarget.position);

            if (distance > attackRadius)
            {
                navigationService.MoveAgentToward(currentTarget.position, rotationSpeed, Time.deltaTime);
            }
            else
            {
                navigationService.StopAgent(Time.deltaTime);

                navigationService.FaceHorizontalPosition(currentTarget.position);

                if (Time.time >= lastAutoAttackTime + autoAttackCooldown)
                {
                    lastAutoAttackTime = Time.time;
                    PerformAttack();
                }
            }
        }

        /// <summary>
        /// 기본 공격 진입점. 공격 중이면 예약 입력으로 처리합니다.
        /// </summary>
        public void PerformAttack()
        {
            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                FindAutoTarget();
            }

            if (isAttacking)
            {
                if (isSkillComboActive)
                {
                    return;
                }

                if (skillService.IsSingleSkillActive(activeSkillIndex, isSkillComboActive))
                {
                    return;
                }

                if (canSaveAttack && !saveAttack)
                {
                    saveAttack = true;
                }
                return;
            }

            if (comboService.IsWithinPostComboDelay(Time.time, lastBasicComboEndTime, ComboDelayTime))
            {
                if (!isAttackReserved)
                {
                    isAttackReserved = true;
                    if (delayedReservedAttackCoroutine != null) StopCoroutine(delayedReservedAttackCoroutine);
                    delayedReservedAttackCoroutine = StartCoroutine(DelayedReservedAttackRoutine(lastBasicComboEndTime + ComboDelayTime));
                }
                return;
            }

            ExecuteAttack();
        }

        /// <summary>
        /// 기본 공격 애니메이션과 콤보 단계를 실행합니다.
        /// </summary>
        public void ExecuteAttack()
        {
            if (isSkillComboActive)
            {
                Debug.LogError("[NetworkPlayerController] ExecuteAttack은 평타 전용입니다. 스킬 1번 콤보는 AutoComboRoutine/ExecuteComboAttackStep 경로만 사용해야 합니다.", this);
                return;
            }

            SetCombatActivity();
            isAttacking = true;
            canSaveAttack = false;
            saveAttack = false;
            lastAttackStartTime = Time.time;
            comboInProgress = true;
            AttackSequence++;

            comboStep = comboService.GetNextBasicComboStep(comboStep);

            if (currentTarget != null)
            {
                Vector3 dirToTarget = (currentTarget.position - transform.position).normalized;
                dirToTarget.y = 0f;
                if (dirToTarget != Vector3.zero) transform.rotation = Quaternion.LookRotation(dirToTarget);
            }

            if (animator != null)
            {
                animator.speed = PlayerAttackAnimationProfile.GetBasicComboSpeed(comboStep);
            }


            int attackIndexMapping = comboService.GetAttackIndex(comboStep);
            animator.SetInteger("AttackIndex", attackIndexMapping);

            isMovingAttack = false;
            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.ResetPath();
                navAgent.velocity = Vector3.zero;
            }
            animator.SetTrigger("Attack_Full");
        }



        /// <summary>
        /// 예약된 기본 공격을 소비해 다음 콤보 단계로 진입합니다.
        /// </summary>
        public bool TryConsumeSavedComboAttack()
        {
            if (isSkillComboActive)
            {
                return false;
            }

            if (comboService.CanConsumeSavedBasicAttack(isSkillComboActive, saveAttack, comboStep))
            {
                ExecuteAttack();
                return true;
            }
            return false;
        }

        public float GetCurrentWeaponDamageMultiplier()
        {
            return skillService.GetWeaponDamageMultiplier(activeSkillIndex, equippedSkills);
        }

        /// <summary>
        /// 현재 공격 시퀀스가 종료되었을 때만 공격 잠금을 해제합니다.
        /// </summary>
        public void CompleteAttackState(int sequence)
        {
            if (AttackSequence == sequence && isAttacking)
            {
                comboInProgress = false;
                ResetAttackLock();
            }
        }

        /// <summary>
        /// 공격 상태 종료 시 잠금과 예약 상태를 정리합니다.
        /// </summary>
        public void ResetAttackLock()
        {
            if (comboInProgress)
            {
                comboInProgress = false;
            }

            if (Time.time - lastAttackStartTime < 0.1f) return;

            if (IsChargingAttack)
            {
                return;
            }

            if (animator != null)
            {
                animator.speed = 1f;
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("Attack_Full"); 
            }

            int prevSkillIndex = activeSkillIndex;
            int endedComboStep = comboStep;
            bool endedBasicFinalCombo = endedComboStep == 3 && !isSkillComboActive && activeSkillIndex == -1;
            
            if (comboStep == 3 || isSkillComboActive || activeSkillIndex != -1) 
            {
                lastBasicComboEndTime = Time.time;
            }
            
            bool hadSavedAttack = saveAttack;

            if (autoComboCoroutine != null)
            {
                StopCoroutine(autoComboCoroutine);
                autoComboCoroutine = null;
            }
            autoComboCastSequence = 0;
            lastComboVfxCastSequence = -1;
            lastComboVfxAttackIndex = -1;

            isAttacking = false;
            isMovingAttack = false;
            IsChargingAttack = false;
            canSaveAttack = false;
            saveAttack = false;
            skillComboAdvanceReady = false;
            comboStep = 0;
            isSkillComboActive = false;
            activeSkillIndex = -1;

            if (hadSavedAttack)
            {
                if (delayedReservedAttackCoroutine != null) StopCoroutine(delayedReservedAttackCoroutine);
                delayedReservedAttackCoroutine = StartCoroutine(DelayedReservedAttackRoutine(lastBasicComboEndTime + ComboDelayTime));
            }

            if (prevSkillIndex >= 0 && prevSkillIndex < equippedSkills.Length && equippedSkills[prevSkillIndex] != null)
            {
                if (equippedSkills[prevSkillIndex].skillType == ActionRPG.Data.SkillType.ChargeAttack)
                {
                    lastAutoAttackTime = Time.time;
                }
              
            }

            if (endedBasicFinalCombo)
            {
                lastAutoAttackTime = Time.time - autoAttackCooldown + ComboDelayTime;
            }

        }

        private System.Collections.IEnumerator DelayedReservedAttackRoutine(float targetTime)
        {
            isAttackReserved = true;
            
            while (Time.time < targetTime)
            {
                if (MobileTouchManager.Instance != null && MobileTouchManager.Instance.MoveInput.sqrMagnitude > 0.01f)
                {
                    isAttackReserved = false;
                    delayedReservedAttackCoroutine = null;
                    yield break;
                }
                
                if (currentTarget != null)
                {
                    Vector3 directionToTarget = currentTarget.position - transform.position;
                    directionToTarget.y = 0;
                    if (directionToTarget.sqrMagnitude > 0.1f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(directionToTarget);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * 50f * Time.deltaTime);
                    }
                }
                
                yield return null;
            }
            
            isAttackReserved = false;
            delayedReservedAttackCoroutine = null;

            if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
            {
                FindAutoTarget();
            }

            if (currentTarget == null)
            {
                yield break;
            }

            if (isAttacking)
            {
                if (canSaveAttack && !saveAttack && !isSkillComboActive && !skillService.IsSingleSkillActive(activeSkillIndex, isSkillComboActive))
                {
                    saveAttack = true;
                }
                yield break;
            }

            ExecuteAttack();
        }

        private void UpdateAnimatorLayers()
        {
            if (animator != null && animator.layerCount > 1)
            {
                float targetWeight = isMovingAttack ? 1f : 0f;
                float currentWeight = animator.GetLayerWeight(1);
                animator.SetLayerWeight(1, Mathf.MoveTowards(currentWeight, targetWeight, Time.deltaTime * 15f));
            }
        }

        #region Combat State Management
        public void SetCombatActivity()
        {
            lastCombatActivityTime = Time.time;
            if (!IsInCombat)
            {
                if (!isAutoMode)
                {
                    ToggleAutoMode();
                }

                IsInCombat = true;

                OnCombatStateChanged?.Invoke(true);
            }
        }

        public void ForceExitCombatState()
        {
            lastCombatActivityTime = 0f;
            currentTarget = null;
            targetEnemyController = null;
            StopAutoNavigation();

            if (!IsInCombat)
            {
                OnCombatStateChanged?.Invoke(false);
                return;
            }

            IsInCombat = false;
            OnCombatStateChanged?.Invoke(false);
        }

        private void UpdateCombatState()
        {
            bool hasTarget = currentTarget != null && currentTarget.gameObject.activeInHierarchy;

            if (IsInCombat)
            {
                if (!hasTarget && Time.time - lastCombatActivityTime > COMBAT_TIMEOUT)
                {
                    IsInCombat = false;

                    OnCombatStateChanged?.Invoke(false);
                }
            }
            else
            {
                if (hasTarget)
                {
                    SetCombatActivity();
                }
                else
                {
                    if (Time.frameCount % 15 == 0)
                    {
                        FindAutoTarget();
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// 모바일 좌측 가상 조이스틱 입력을 받아 이동을 처리합니다. (수동 개입)
        /// </summary>
        public void ManualMove(Vector2 joystickInput)
        {
            bool lockMovement = isAttacking;
            verticalVelocity = navigationService.ManualMove(
                joystickInput,
                lockMovement,
                moveSpeed,
                rotationSpeed,
                gravity,
                verticalVelocity,
                Time.deltaTime);
        }

        // OverlapSphereNonAlloc에 사용할 재사용 버퍼입니다.
        private Collider[] overlapColliders = new Collider[40];

        // --- 외부 개입 (UI 등) 수동 타겟팅 지원 ---
        public void SetTarget(Transform newTarget)
        {
            if (IsChargingAttack) return;
            if (newTarget == null) return;

            ApplyTarget(targetingService.ResolveExplicitTarget(newTarget), faceTarget: true);
        }

        // --- 자동 타겟 스캔 ---
        private void FindAutoTarget()
        {
            if (IsChargingAttack) return;

            ApplyTarget(targetingService.FindClosestTarget(transform.position, autoTargetRadius), faceTarget: true);
            
            if (currentTarget != null)
            {
                targetingService.ShowAutoTargetLog(currentTarget);
            }
          
        }

        private void ApplyTarget(PlayerTargetingResolver.TargetResult result, bool faceTarget)
        {
            currentTarget = result.Target;
            targetEnemyController = result.Controller;

            if (faceTarget && currentTarget != null)
            {
                navigationService.FaceHorizontalPosition(currentTarget.position);
            }
        }

        /// <summary>
        /// 비전투 상태에서 첫 적 소환(마법진) 지점을 바라봅니다.
        /// AreaSpawner 중심이 아닌, 실제 마법진/적이 나타나는 월드 좌표(lookPosition)를 사용합니다.
        /// </summary>
        public void FocusSpawnedEnemy(ActionRPG.Enemy.EnemyController enemy, Vector3 lookPosition)
        {
            if (IsChargingAttack) return;
            if (IsInCombat) return;
            if (lookPosition == Vector3.zero) return;
            if (enemy != null && enemy.IsDead) return;

            // 이미 유효한 타겟이 있으면 소환 포커스로 덮어쓰지 않습니다.
            if (currentTarget != null && currentTarget.gameObject.activeInHierarchy &&
                targetEnemyController != null && !targetEnemyController.IsDead)
            {
                return;
            }

            spawnFocusLookPosition = lookPosition;
            IsWatchingSpawnFocus = true;

            if (enemy != null)
            {
                currentTarget = enemy.transform;
                targetEnemyController = enemy;
            }

            StopAutoNavigation();
            navigationService.FaceHorizontalPosition(lookPosition);

            var camCtrl = Camera.main?.GetComponent<ActionRPG.CameraSystem.CameraController>();
            if (camCtrl != null)
            {
                camCtrl.SetSpawnFocus(lookPosition);
            }
        }

        private void StopAutoNavigation()
        {
            navigationService.StopAndClearAgent(Time.deltaTime);
        }

        private bool TryHandleManualMoveInput(ref bool hasManualMoveInput)
        {
            if (IsChargingAttack)
            {
                return false;
            }

            if (MobileTouchManager.Instance != null && MobileTouchManager.Instance.MoveInput.sqrMagnitude > 0f)
            {
                ignoreJoystickUntilRelease = false;
                CancelAttackAndMove();
                if (isAutoMode) ToggleAutoMode();
                if (isQuestNavigating) StopQuestNavigation(true);
                pendingSkillIndex = -1;
                ManualMove(MobileTouchManager.Instance.MoveInput);
                hasManualMoveInput = true;
                return true;
            }
            else if (MobileTouchManager.Instance != null && MobileTouchManager.Instance.MoveInput.sqrMagnitude == 0f)
            {
                ignoreJoystickUntilRelease = false;
            }

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                float h = 0f;
                float v = 0f;
                if (keyboard.wKey.isPressed) v += 1f;
                if (keyboard.sKey.isPressed) v -= 1f;
                if (keyboard.dKey.isPressed) h += 1f;
                if (keyboard.aKey.isPressed) h -= 1f;

                Vector2 inputVector = new Vector2(h, v).normalized;
                if (inputVector.sqrMagnitude > 0f)
                {
                    CancelAttackAndMove();
                    if (isAutoMode) ToggleAutoMode();
                    if (isQuestNavigating) StopQuestNavigation(true);
                    pendingSkillIndex = -1;
                    ManualMove(inputVector);
                    hasManualMoveInput = true;
                    return true;
                }
            }

            return false;
        }

        private void TryReserveAutoComboDuringAttack()
        {
            if (!isAutoMode)
            {
                return;
            }

            bool isSingleSkillActive = skillService.IsSingleSkillActive(activeSkillIndex, isSkillComboActive);
            if (autoBattleService.ShouldReserveCombo(isSingleSkillActive, isSkillComboActive, comboStep, isAttacking, canSaveAttack, saveAttack, currentTarget))
            {
                saveAttack = true;
            }
        }

        internal Transform GetEffectCanvasParent()
        {
            if (ActionRPG.Managers.ObjectPoolManager.Instance != null &&
                ActionRPG.Managers.ObjectPoolManager.Instance.effectCanvasContainer != null)
            {
                return ActionRPG.Managers.ObjectPoolManager.Instance.effectCanvasContainer;
            }

            return null;
        }

        // --- 소환 포커스 상태 ---
        private Vector3 spawnFocusLookPosition; // 소환 연출 시 바라볼 지표면 위치 (마법진/첫 적 스폰 지점)
        public bool IsWatchingSpawnFocus { get; private set; }

        internal ChargeAttackHitArea EnsureChargeHitAreaPreview()
        {
            if (chargeHitAreaPreview != null) return chargeHitAreaPreview;

            chargeHitAreaPreview = GetComponentInChildren<ChargeAttackHitArea>(true);
            if (chargeHitAreaPreview != null) return chargeHitAreaPreview;

            var hitAreaGo = new GameObject("ChargeAttackHitArea_Preview");
            hitAreaGo.transform.SetParent(transform, false);
            chargeHitAreaPreview = hitAreaGo.AddComponent<ChargeAttackHitArea>();
            return chargeHitAreaPreview;
        }

        internal void ShowChargeHitAreaPreview(SkillData skill)
        {
            var preview = EnsureChargeHitAreaPreview();
            preview?.SyncFromSkill(skill);
            preview?.Show(transform, skill);
        }

        internal void HideChargeHitAreaPreview()
        {
            if (chargeHitAreaPreview != null)
            {
                chargeHitAreaPreview.Hide();
            }
        }

        /// <summary>
        /// 차지어택 타격 후 플레이어 이동·입력 잠금만 해제합니다. 카메라는 이펙트 종료까지 유지됩니다.
        /// </summary>
        internal void ReleaseChargeAttackMovementLock()
        {
            isAttacking = false;
            isMovingAttack = false;
            canSaveAttack = false;
            saveAttack = false;
            comboStep = 0;
            isSkillComboActive = false;
            activeSkillIndex = -1;
            lastAutoAttackTime = Time.time;

            DisableWeaponHitbox();

            if (animator != null)
            {
                animator.speed = 1f;
            }

            if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = false;
            }
        }

        internal void BeginChargeCameraLock(GameObject slashVfx, float lockDuration)
        {
            if (chargeCameraLockCoroutine != null)
            {
                StopCoroutine(chargeCameraLockCoroutine);
            }

            chargeCameraLockCoroutine = StartCoroutine(ChargeCameraLockRoutine(slashVfx, lockDuration));
        }

        internal void EndChargeCameraLock()
        {
            if (chargeCameraLockCoroutine != null)
            {
                StopCoroutine(chargeCameraLockCoroutine);
                chargeCameraLockCoroutine = null;
            }

            IsChargeCameraLocked = false;
        }

        private System.Collections.IEnumerator ChargeCameraLockRoutine(GameObject slashVfx, float lockDuration)
        {
            IsChargeCameraLocked = true;
            float elapsed = 0f;

            while (elapsed < lockDuration)
            {
                if (slashVfx != null && !slashVfx.activeInHierarchy)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            IsChargeCameraLocked = false;
            chargeCameraLockCoroutine = null;
        }
        /// <summary>
        /// 스킬 타입별 범위 판정과 타격 피드백을 처리합니다.
        /// </summary>
        public void ExecuteSkillDamage()
        {
            if (activeSkillIndex == -1 || equippedSkills[activeSkillIndex] == null) return;
            if (equippedSkills[activeSkillIndex].skillType == ActionRPG.Data.SkillType.ComboAttack) return;

            if (Time.time - lastVfxSpawnTime < 0.1f) return;
            lastVfxSpawnTime = Time.time;

            var skill = equippedSkills[activeSkillIndex];

            float baseDamage = weaponManager != null ? weaponManager.GetCurrentWeaponDamage() : 10f;
            float finalDamage = baseDamage * skill.damageMultiplier;
            
            float critChance = weaponManager != null ? weaponManager.GetCurrentWeaponCriticalChance() : 0.2f;
            float critMult = weaponManager != null ? weaponManager.GetCurrentWeaponCriticalMultiplier() : 1.5f;
            bool isCritical = UnityEngine.Random.value <= critChance;
            if (isCritical) finalDamage *= critMult;

            Collider[] hitTargets = new Collider[30];
            int count;

            if (skill.skillType == ActionRPG.Data.SkillType.ChargeAttack)
            {
                var hitPreview = EnsureChargeHitAreaPreview();
                hitPreview.SyncFromSkill(skill);
                count = hitPreview.OverlapHitTargets(transform, hitTargets);
            }
            else
            {
                ChargeAttackHitArea.GetHitShapeFromSkill(skill, out float forwardOffset, out float hitRadius, out float heightOffset);
                Vector3 hitCenter = transform.position + transform.forward * forwardOffset + Vector3.up * heightOffset;
                count = Physics.OverlapSphereNonAlloc(hitCenter, hitRadius, hitTargets, ~0, QueryTriggerInteraction.Collide);
            }

            System.Collections.Generic.HashSet<ActionRPG.Core.IDamageable> damagedSet = new System.Collections.Generic.HashSet<ActionRPG.Core.IDamageable>();

            bool hitAny = false;
            Vector3 firstHitPoint = Vector3.zero;

            for (int i = 0; i < count; i++)
            {
                Collider col = hitTargets[i];
                if (col == null) continue;

                var target = col.GetComponent<ActionRPG.Core.IDamageable>();
                if (target == null) target = col.GetComponentInParent<ActionRPG.Core.IDamageable>();
                if (ReferenceEquals(target, this)) continue;

                if (target != null && !damagedSet.Contains(target))
                {
                    damagedSet.Add(target);
                    
                    Vector3 hitPoint = col.ClosestPoint(transform.position);
                    if (!hitAny) firstHitPoint = hitPoint;
                    hitAny = true;

                    Vector3 pushNormal = (col.transform.position - transform.position).normalized;
                    pushNormal.y = 0f;

                    if (ActionRPG.Network.MockNetworkManager.Instance != null)
                    {
                        ActionRPG.Network.MockNetworkManager.Instance.SendHitRequest(target, finalDamage, hitPoint, pushNormal, isCritical, (bool isHitConfirmed) =>
                        {
                            if (isHitConfirmed)
                            {

                            }
                            else
                            {

                            }
                        });
                    }
                    else
                    {
                        target.TakeDamage(finalDamage, hitPoint, pushNormal, isCritical);
                    }
                }
            }

            if (currentChargeVfx != null)
            {
                PlayerSkillEffects.Release(currentChargeVfx);
                currentChargeVfx = null;
            }
            
            if (skill.vfxPrefab != null)
            {
                Quaternion? offsetRot = null;
                if (skill.skillType == ActionRPG.Data.SkillType.ComboAttack)
                {
                    Vector3 eulerOffset = comboStep1Angle;
                    if (comboStep == 2) eulerOffset = comboStep2Angle;
                    else if (comboStep == 3) eulerOffset = comboStep3Angle;
                    offsetRot = Quaternion.Euler(eulerOffset);
                }

                GameObject vfx = PlayerSkillEffects.SpawnImpactVFX(skill, transform, currentTarget, out float vfxLifeTime, offsetRot);
                if (vfx != null && skill.skillType == ActionRPG.Data.SkillType.ChargeAttack)
                {
                    BeginChargeCameraLock(vfx, chargeCameraLockDuration);
                }
            }

            // 타격 성공(hitAny) 여부와 상관없이, 스킬 고유의 폭발음/파열음은 무조건 출력되도록 밖으로 뺍니다.
            PlayerSkillEffects.PlayImpactAudio(skill, hitAny);

            // 스킬 타격 연출은 적중 여부와 무관하게 카메라 반응을 먼저 전달합니다.
            ActionRPG.Managers.CombatEffects.ShakeAndZoomCamera(0.4f, 1.0f, 18f, 0.15f);

            // 역경직(HitStop)은 실제로 적중했을 때만(hitAny == true) 발생합니다.
            if (hitAny)
            {
                if (ActionRPG.Managers.GameFeelManager.Instance != null) ActionRPG.Managers.GameFeelManager.Instance.TriggerHitStop(0.12f);
            }
        }

        public void ExecuteSkillDamageOnce()
        {
            if (activeSkillIndex < 0 || activeSkillIndex >= equippedSkills.Length || equippedSkills[activeSkillIndex] == null) return;
            if (lastSkillDamageSequence == skillCastSequence) return;

            lastSkillDamageSequence = skillCastSequence;
            ExecuteSkillDamage();
        }

        /// <summary>
        /// 제자리 공격 중 이동 조작이 개입할 때 즉각 공격 예약을 취소하고 
        /// 애니메이터를 이동(Locomotion) 상태로 강제 복귀시킵니다.
        /// </summary>
        private void CancelAttackAndMove()
        {
            AttackSequence++;
            chargedSkillService.Cancel();
            ReleaseCurrentChargeVfx();
            if (autoComboCoroutine != null)
            {
                StopCoroutine(autoComboCoroutine);
                autoComboCoroutine = null;
            }
            if (delayedReservedAttackCoroutine != null)
            {
                StopCoroutine(delayedReservedAttackCoroutine);
                delayedReservedAttackCoroutine = null;
            }

            isAttacking = false;
            isMovingAttack = false;
            IsChargingAttack = false;
            IsChargePostHitFrozen = false;
            EndChargeCameraLock();
            HideChargeHitAreaPreview();
            canSaveAttack = false;
            saveAttack = false;
            comboStep = 0;
            isSkillComboActive = false; // 스킬 콤보 해제
            skillComboAdvanceReady = false;
            activeSkillIndex = -1;
            pendingSkillIndex = -1;
            isAttackReserved = false;
            verticalVelocity = 0f;
            navigationService.DisableAgentForManualControl();

            if (animator != null)
            {
                animator.speed = 1f;
                animator.ResetTrigger("Attack_Full");
                animator.ResetTrigger("Attack");
                
                // Base Layer(0층)의 Idle_Run 또는 Locomotion 기본 이동 상태로 즉각 강제 복구
                animator.Play("Locomotion", 0, 0f); 
            }

            // 무기 콜라이더 안전 정지
            DisableWeaponHitbox();

        }

        /// <summary>
        /// 화면 상에서 현재 타겟의 좌측/우측에 있는 몬스터로 타겟을 변경합니다.
        /// toLeft가 true면 왼쪽, false면 오른쪽 적을 타겟팅합니다.
        /// </summary>
        public void ChangeTargetLeftRight(bool toLeft)
        {
            if (IsChargingAttack) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            PlayerTargetingResolver.TargetResult result = targetingService.FindHorizontalNeighbor(
                transform.position,
                autoTargetRadius,
                currentTarget,
                toLeft,
                cam);

            // 찾은 적이 있다면 타겟 갱신
            if (result.HasTarget)
            {
                ApplyTarget(result, faceTarget: true);
                targetingService.ShowTargetSwitchLog(currentTarget, toLeft);
            }
        }
    }
}



