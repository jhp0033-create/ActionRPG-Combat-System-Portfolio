using UnityEngine;
using UnityEngine.AI;
using ActionRPG.Core;
using ActionRPG.Data;
using ActionRPG.Managers;
using ActionRPG.UI;

namespace ActionRPG.Enemy
{
    /// <summary>
    /// 적 AI 상태, 피격 처리, 월드 UI 연동을 관리합니다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour, IDamageable
    {
        [SerializeField] public Animator animator;
        [HideInInspector] public NavMeshAgent agent;

        [Header("Animation State Names")]
        [Tooltip("사용하지 않음 - 애니메이터 기본 트리(Entry)를 따릅니다.")]
        [SerializeField] private string obsoleteField;

        [Header("AI Settings")]
        public Transform target;
        public float aggroRadius = 10f;
        public float attackRadius = 1.35f;
        public float attackCooldown = 2f;
        public float moveSpeed = 3.5f;
        public float roamRadius = 5f;
        [HideInInspector] public Vector3 spawnOrigin;
        private CharacterController playerBodyController;

        [Header("VFX")]
        public GameObject hitVFXPrefab;

        [Header("Enemy Stats")]
        public string enemyName = "Monster";
        public float maxHealth = 100f;
        private float currentHealth;
        public bool IsDead => currentHealth <= 0f;

        private StateMachine fsm;
        public EnemyIdleState idleState { get; private set; }
        public EnemyChaseState chaseState { get; private set; }
        public EnemyAttackState attackState { get; private set; }
        public EnemyHitState hitState { get; private set; }

        private WorldHealthBar healthBar;

        private MaterialPropertyBlock propBlock;
        private Renderer[] childRenderers;

        private Material[] originalSharedMaterials;
        public bool IsSpawning { get; private set; } = false;

        private Coroutine hitFlashCoroutine;
        private bool hasShownAlert;
        private bool hasTakenDamage;
        private Coroutine healthBarReturnCoroutine;

        private void Awake()
        {
            if (CompareTag("Player") || GetComponent<ActionRPG.Player.NetworkPlayerController>() != null)
            {
                Debug.LogWarning($"[EnemyController] EnemyController detected on Player object ({gameObject.name}). Disabling this component immediately to prevent errors.");
                enabled = false;
                return;
            }

            agent = GetComponent<NavMeshAgent>();

            if (agent != null) agent.angularSpeed = 720f;

            fsm = new StateMachine();
            idleState = new EnemyIdleState(fsm, this);
            chaseState = new EnemyChaseState(fsm, this);
            attackState = new EnemyAttackState(fsm, this);
            hitState = new EnemyHitState(fsm, this);

            propBlock = new MaterialPropertyBlock();
            childRenderers = GetComponentsInChildren<Renderer>(true);

            if (childRenderers != null)
            {
                originalSharedMaterials = new Material[childRenderers.Length];
                for (int i = 0; i < childRenderers.Length; i++)
                {
                    if (childRenderers[i] != null)
                    {
                        originalSharedMaterials[i] = childRenderers[i].sharedMaterial;
                    }
                }
            }
        }

        private void OnEnable()
        {
            InitializeEnemy();
        }

        private void OnDisable()
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }

            if (healthBarReturnCoroutine != null)
            {
                StopCoroutine(healthBarReturnCoroutine);
                healthBarReturnCoroutine = null;
            }

            if (healthBar != null)
            {
                ReturnHealthBar(healthBar);
                healthBar = null;
            }
        }

        private void Update()
        {
            if (IsDead) return;

            if (healthBar != null && healthBar.gameObject != null)
            {
                bool shouldShow = !IsSpawning && currentHealth > 0;
                if (healthBar.gameObject.activeSelf != shouldShow)
                {
                    healthBar.gameObject.SetActive(shouldShow);
                }
            }

            fsm?.Update();
        }

        /// <summary>
        /// 풀에서 재사용될 때 전투 상태와 표시 상태를 초기화합니다.
        /// </summary>
        private void InitializeEnemy()
        {
            spawnOrigin = transform.position;

            currentHealth = maxHealth;
            hasShownAlert = false;
            hasTakenDamage = false;

            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }

            if (agent != null)
            {
                agent.enabled = false;
                agent.enabled = true;

                UnityEngine.AI.NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out navHit, 5.0f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position);
                    spawnOrigin = navHit.position;
                }
                
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }
                else
                {
                    Debug.LogWarning($"[EnemyController:{gameObject.name}] 반경 5m 이내에 NavMesh를 찾지 못했습니다! 현재 스폰 좌표: {transform.position} (여기에 파란색 네비메쉬가 깔려있나요?)");
                }
            }

            fsm.Initialize(idleState);

            if (animator != null)
            {
                animator.speed = 1f;
                animator.SetFloat("Speed", 0f);
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("Hit");
                animator.ResetTrigger("Death");

                animator.Update(0f);
            }
           

            Collider col = GetComponent<Collider>();
            if (col == null) col = GetComponentInChildren<Collider>();
            if (col != null) col.enabled = true;
            IgnorePlayerBodyCollision();

            if (HasHealthBarManager())
            {
                AssignHealthBar();
            }

            // 풀 재사용 전 투명화 잔상과 런타임 머티리얼 인스턴스를 정리합니다.
            IsSpawning = false;

            if (childRenderers != null && originalSharedMaterials != null)
            {
                for (int i = 0; i < childRenderers.Length; i++)
                {
                    if (childRenderers[i] == null) continue;
                    
                    // 런타임 복제 머티리얼은 원본으로 되돌린 뒤 제거합니다.
                    if (childRenderers[i].sharedMaterial != originalSharedMaterials[i])
                    {
                        Material tempMat = childRenderers[i].sharedMaterial;
                        childRenderers[i].sharedMaterial = originalSharedMaterials[i];
                        if (tempMat != null)
                        {
                            Destroy(tempMat); // 메모리 누수 방지
                        }
                    }
                }
            }

            if (childRenderers != null && propBlock != null)
            {
                foreach (var r in childRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(propBlock);
                    
                    Color originalColor = Color.white;
                    if (r.sharedMaterial != null)
                    {
                        if (r.sharedMaterial.HasProperty("_BaseColor"))
                            originalColor = r.sharedMaterial.GetColor("_BaseColor");
                        else if (r.sharedMaterial.HasProperty("_Color"))
                            originalColor = r.sharedMaterial.GetColor("_Color");
                    }
                    originalColor.a = 1f;
                    propBlock.SetColor("_BaseColor", originalColor);
                    propBlock.SetColor("_Color", originalColor);
                    r.SetPropertyBlock(propBlock);
                }
            }
            // 매니저 초기화 순서상 할당되지 않은 경우 Start에서 한 번 더 확인합니다.
        }

        private void Start()
        {
            // OnEnable 단계에서 매니저가 아직 Awake 되지 않아 체력바 할당을 못 했을 수 있으므로,
            // Start()에서 한 번 더 체크해서 안전하게 할당해 줍니다.
            if (healthBar == null && HasHealthBarManager())
            {
                AssignHealthBar();
            }

            IgnorePlayerBodyCollision();
        }

        private void IgnorePlayerBodyCollision()
        {
            if (playerBodyController == null)
            {
                if (target != null)
                {
                    playerBodyController = target.GetComponent<CharacterController>();
                    if (playerBodyController == null) playerBodyController = target.GetComponentInChildren<CharacterController>();
                    if (playerBodyController == null) playerBodyController = target.GetComponentInParent<CharacterController>();
                }

                if (playerBodyController == null)
                {
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        playerBodyController = player.GetComponent<CharacterController>();
                        if (playerBodyController == null) playerBodyController = player.GetComponentInChildren<CharacterController>();
                        if (playerBodyController == null) playerBodyController = player.GetComponentInParent<CharacterController>();
                    }
                }
            }

            if (playerBodyController == null) return;

            Collider playerCollider = playerBodyController;
            Collider[] enemyColliders = GetComponentsInChildren<Collider>(true);
            foreach (Collider enemyCollider in enemyColliders)
            {
                if (enemyCollider == null || enemyCollider == playerCollider || enemyCollider.isTrigger) continue;
                Physics.IgnoreCollision(playerCollider, enemyCollider, true);
            }
        }

        private void AssignHealthBar()
        {
            if (healthBar != null)
                ReturnHealthBar(healthBar);

            healthBar = RequestHealthBar();
                
            if (healthBar == null)
                Debug.LogError($"[EnemyController:{gameObject.name}] GetHealthBar() 반환값이 null입니다. FloatingUIManager/WorldHealthBarManager 설정을 확인하세요.");
        }

        public void SetEnemyName(string newName)
        {
            enemyName = newName;
            if (healthBar != null && healthBar.nameText != null)
            {
                healthBar.nameText.text = newName;
            }
        }

        private bool HasHealthBarManager()
        {
            return FloatingUIManager.Instance != null || WorldHealthBarManager.Instance != null;
        }

        private WorldHealthBar RequestHealthBar()
        {
            string displayName = string.IsNullOrWhiteSpace(enemyName) ? gameObject.name : enemyName;

            if (FloatingUIManager.Instance != null)
                return FloatingUIManager.Instance.GetHealthBar(transform, displayName, maxHealth);

            if (WorldHealthBarManager.Instance != null)
                return WorldHealthBarManager.Instance.GetHealthBar(transform, displayName, maxHealth);

            return null;
        }

        private void ReturnHealthBar(WorldHealthBar bar)
        {
            if (bar == null) return;

            if (FloatingUIManager.Instance != null)
            {
                FloatingUIManager.Instance.ReturnHealthBar(bar);
                return;
            }

            if (WorldHealthBarManager.Instance != null)
            {
                WorldHealthBarManager.Instance.ReturnHealthBar(bar);
            }
        }

        private void SpawnDamagePopup(Vector3 position, float damageAmount, string statusText = "")
        {
            if (FloatingUIManager.Instance != null)
            {
                float visualHeight = 2.0f;
                if (TryGetComponent(out CharacterController cc)) visualHeight = cc.center.y + cc.height * 0.5f;
                else if (agent != null) visualHeight = agent.height;

                Vector3 floatPosition = transform.position + Vector3.up * visualHeight;

                FloatingUIManager.Instance.SpawnDamageText(transform, floatPosition, damageAmount, statusText);
                return;
            }

        }

        public void ShowAlertBubble()
        {
            if (hasShownAlert) return;
            hasShownAlert = true;

            if (FloatingUIManager.Instance != null)
            {
                FloatingUIManager.Instance.ShowAlert(transform);
            }
        }

        /// <summary>
        /// IDamageable 인터페이스 구현체. 플레이어 무기 콜라이더에 맞으면 호출됩니다.
        /// </summary>
        public void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            if (currentHealth <= 0 || IsSpawning) return;

            string damageStatusText = GetDamageStatusText(damageAmount, isCritical);
            hasTakenDamage = true;
            currentHealth -= damageAmount;
            ShowAlertBubble();

            SpawnDamagePopup(hitPoint, damageAmount, damageStatusText);

            if (ComboManager.Instance != null)
            {
                ComboManager.Instance.AddCombo();
            }

            if (healthBar != null)
            {
                healthBar.UpdateHealth(currentHealth, maxHealth);
            }

            if (hitVFXPrefab != null)
            {
                if (hitVFXPrefab is GameObject hitPrefab)
                {
                    CombatEffects.SpawnPooledVFX(hitPrefab, hitPoint, Quaternion.LookRotation(hitNormal), 1.0f);
                }
            }

            TriggerHitFlash();

            if (currentHealth <= 0)
            {
                Die();
            }
            else
            {
                Vector3 attackerPos = target != null ? target.position : hitPoint + hitNormal;
                hitState.SetKnockback(attackerPos, 7.5f);
                
                fsm.ChangeState(hitState);
            }
        }

        private string GetDamageStatusText(float damageAmount, bool isCritical)
        {
            bool isFinishingHit = currentHealth - damageAmount <= 0f;
            bool isFirstAttack = !hasTakenDamage;

            if (isFinishingHit && isCritical)
                return "치명적 피니시";

            if (isFinishingHit)
                return "피니시";

            if (isFirstAttack)
                return "퍼스트 어택";

            if (isCritical)
                return "치명타";

            return string.Empty;
        }

        public void TriggerHitFlash()
        {
            if (IsDead) return;
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }
            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private System.Collections.IEnumerator HitFlashRoutine()
        {
            if (childRenderers != null && propBlock != null)
            {
                foreach (var r in childRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(propBlock);
                    
                    Color flashColor = new Color(2.5f, 2.5f, 2.5f, 1f); 
                    propBlock.SetColor("_BaseColor", flashColor);
                    propBlock.SetColor("_Color", flashColor);
                    r.SetPropertyBlock(propBlock);
                }
            }

            yield return new WaitForSeconds(0.12f);

            if (childRenderers != null && propBlock != null && !IsDead)
            {
                foreach (var r in childRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(propBlock);
                    
                    Color originalColor = Color.white;
                    if (r.sharedMaterial != null)
                    {
                        if (r.sharedMaterial.HasProperty("_BaseColor"))
                            originalColor = r.sharedMaterial.GetColor("_BaseColor");
                        else if (r.sharedMaterial.HasProperty("_Color"))
                            originalColor = r.sharedMaterial.GetColor("_Color");
                    }
                    originalColor.a = 1f;
                    propBlock.SetColor("_BaseColor", originalColor);
                    propBlock.SetColor("_Color", originalColor);
                    r.SetPropertyBlock(propBlock);
                }
            }
            hitFlashCoroutine = null;
        }

        /// <summary>
        /// 애니메이션 이벤트 또는 EnemyAttackBehaviour에서 호출되어 실제 데미지를 입힙니다.
        /// </summary>
        public void PerformAttackDamage()
        {
            if (IsDead || target == null) return;

            float checkRadius = attackRadius * 0.65f;
            Vector3 checkCenter = transform.position + transform.forward * (attackRadius * 0.35f);
            Collider[] hitColliders = Physics.OverlapSphere(checkCenter, checkRadius);

            foreach (var col in hitColliders)
            {
                if (col.CompareTag("Player"))
                {
                    IDamageable damageable = col.GetComponent<IDamageable>();
                    if (damageable == null) damageable = col.GetComponentInParent<IDamageable>();

                    if (damageable != null)
                    {
                        float damage = 10f; 
                        damageable.TakeDamage(damage, col.ClosestPoint(transform.position), -transform.forward);

                        break;
                    }
                }
            }
        }

        private void Die()
        {
            if (ActionRPG.Core.BattleManager.Instance != null)
            {
                ActionRPG.Core.BattleManager.Instance.NotifyEnemyKilled(this, transform.position, gameObject.name);
            }

            if (QuestManager.Instance != null)
            {
                QuestManager.Instance.ReportEnemyKilled(enemyName);
            }

            if (target != null)
            {
                var playerCtrl = target.GetComponent<ActionRPG.Player.NetworkPlayerController>();
                if (playerCtrl == null) playerCtrl = target.GetComponentInParent<ActionRPG.Player.NetworkPlayerController>();
                
                if (playerCtrl != null)
                {
                    int goldAmount = Random.Range(10, 30); // 10 ~ 30 골드 무작위 획득
                    playerCtrl.AddGold(goldAmount);
                    
                    float expAmount = Random.Range(20f, 40f); // 20 ~ 40 경험치 무작위 획득
                    playerCtrl.AddExp(expAmount);
                    
                    PlayGoldDropRewardEffects();
                }
            }
            
            if (healthBarReturnCoroutine != null)
                StopCoroutine(healthBarReturnCoroutine);
            healthBarReturnCoroutine = StartCoroutine(ReturnHealthBarAfterDelay(0.75f));

            // 사망 애니메이션 연출 및 지연 회수 루틴 가동
            StartCoroutine(DieRoutine());
        }

        private void PlayGoldDropRewardEffects()
        {
            VFXDatabase vfxDatabase = VFXDatabase.Instance;
            if (vfxDatabase != null && vfxDatabase.goldDropVFXPrefab != null)
            {
                Vector3 spawnPosition = transform.position + Vector3.up * vfxDatabase.goldDropHeightOffset;
                CombatEffects.SpawnPooledVFX(vfxDatabase.goldDropVFXPrefab, spawnPosition, Quaternion.identity, 2.0f);
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFXByKey("Gold_Drop");
            }
        }

        private System.Collections.IEnumerator ReturnHealthBarAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (healthBar != null)
            {
                ReturnHealthBar(healthBar);
                healthBar = null;
            }

            healthBarReturnCoroutine = null;
        }

        private System.Collections.IEnumerator DieRoutine()
        {
            // 사망 피드백이 늘어지지 않도록 데스 애니메이션 속도를 높입니다.
            if (animator != null)
            {
                animator.speed = 1.5f;
                animator.SetTrigger("Death");
            }

            // 사망 후 추가 피격과 이동 충돌을 막기 위해 물리 충돌체를 끕니다.
            Collider col = GetComponent<Collider>();
            if (col == null) col = GetComponentInChildren<Collider>();
            if (col != null) col.enabled = false;

            // 3. 에이전트 정지 및 컴포넌트 비활성화
            if (agent != null)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                agent.enabled = false;
            }

            // 잠시 사망 포즈를 보여준 뒤 머티리얼 알파를 낮춰 페이드아웃합니다.
            yield return new WaitForSeconds(0.4f);

            // 런타임에 머티리얼을 복제하여 투명 렌더링 세팅 적용 및 캐싱
            System.Collections.Generic.List<Material> spawnedMaterials = new System.Collections.Generic.List<Material>();
            if (childRenderers != null)
            {
                foreach (var r in childRenderers)
                {
                    if (r == null || r.sharedMaterial == null) continue;
                    
                    // 피격 플래시 PropertyBlock을 제거해 머티리얼 알파 변경이 반영되도록 합니다.
                    r.SetPropertyBlock(null);

                    // r.material을 호출해 머티리얼 복제본 인스턴스 생성 및 캐시
                    Material mat = r.material;
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetFloat("_Surface", 1f); // Transparent
                    mat.SetFloat("_Blend", 0f);   // Alpha blend
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.renderQueue = 3000;
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    spawnedMaterials.Add(mat);
                }
            }

            float fadeDuration = 0.8f;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));

                foreach (var mat in spawnedMaterials)
                {
                    if (mat == null) continue;

                    // 복제된 머티리얼 인스턴스의 알파값 조정
                    Color c = Color.white;
                    if (mat.HasProperty("_BaseColor"))
                        c = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color"))
                        c = mat.GetColor("_Color");
                        
                    c.a = alpha;
                    
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", c);
                    if (mat.HasProperty("_Color"))
                        mat.SetColor("_Color", c);
                }
                yield return null;
            }

            // 생성된 런타임 머티리얼을 제거합니다.
            foreach (var mat in spawnedMaterials)
            {
                if (mat != null) Destroy(mat);
            }

            // 비활성화하여 오브젝트 풀에 반환합니다.
            if (ActionRPG.Core.EnemyManager.Instance != null)
            {
                string poolTag = gameObject.name.Replace("(Clone)", "").Trim();
                if (poolTag.Contains(" ")) poolTag = poolTag.Split(' ')[0];
                if (poolTag == "Monster") poolTag = "Skeleton";

                ActionRPG.Core.EnemyManager.Instance.ReturnToPool(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }


        // 유니티 에디터에서 어그로 범위와 사거리를 보기 쉽게 시각화합니다.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRadius); // 인식 반경

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRadius); // 공격 반경
        }

        /// <summary>
        /// 몬스터가 씬에 처음 스폰될 때 호출되어 등장 연출(마법진 대기 후 솟구치기)을 시작합니다.
        /// </summary>
        public void StartSpawnSequence(Vector3 originalGroundPos, float delayBeforeJump)
        {
            IsSpawning = true;
            if (healthBar != null)
            {
                healthBar.RefreshTargetVisibility(0f);
                healthBar.gameObject.SetActive(false);
            }

            StartCoroutine(SpawnSequenceRoutine(originalGroundPos, delayBeforeJump));
        }

        private System.Collections.IEnumerator SpawnSequenceRoutine(Vector3 originalGroundPos, float delayBeforeJump)
        {
            IsSpawning = true;

            // 1. 연출용 상태 정지 및 물리 충돌 방지
            if (agent != null)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                agent.enabled = false;
            }

            Collider col = GetComponent<Collider>();
            if (col == null) col = GetComponentInChildren<Collider>();
            if (col != null) col.enabled = false;

            // 스폰 연출 중에는 AI 상태 갱신을 멈춥니다.
            fsm.Initialize(null);

            // 최초 등장 전 1.5m 잠긴 위치 강제 유지
            Vector3 buriedPos = new Vector3(originalGroundPos.x, originalGroundPos.y - 1.5f, originalGroundPos.z);
            transform.position = buriedPos;

            if (animator != null)
            {
                animator.SetFloat("Speed", 0f);
            }

            // 2. 마법진 이펙트 발동 시간 대기
            yield return new WaitForSeconds(delayBeforeJump);

            // 3. 점프 애니메이션 트리거 및 부드럽게 위로 상승 (Lerp)
            if (animator != null)
            {
                animator.SetTrigger("Jump");
                animator.SetTrigger("Spawn"); // 에셋 호환성을 위해 둘 다 트리거
            }

            float duration = 0.6f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // 땅 위로 치솟을 때 탄성감을 주기 위해 Ease Out 보간(Mathf.Sin) 활용
                float curvedT = Mathf.Sin(t * Mathf.PI * 0.5f);
                transform.position = Vector3.Lerp(buriedPos, originalGroundPos, curvedT);
                yield return null;
            }

            // 최종 원본 좌표로 강제 안착
            transform.position = originalGroundPos;
            IsSpawning = false;

            if (healthBar != null)
            {
                healthBar.gameObject.SetActive(true);
                healthBar.RefreshTargetVisibility(0.12f);
            }

            // 4. 물리 및 정상 전투 AI 패턴 복구
            if (col != null) col.enabled = true;
            IgnorePlayerBodyCollision();

            if (agent != null)
            {
                agent.enabled = true;
                UnityEngine.AI.NavMeshHit navHit;
                if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out navHit, 0.5f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    agent.Warp(navHit.position);
                    spawnOrigin = navHit.position;
                }
                else
                {
                    Debug.LogWarning($"[EnemyController:{gameObject.name}] 솟아오른 위치 근처(0.5m)에 NavMesh가 없습니다. AreaSpawner 좌표 검증을 확인하세요.");
                    agent.Warp(transform.position);
                }
                if (agent.isOnNavMesh) agent.isStopped = true;
            }

            // 스폰 연출이 끝나면 대기 상태로 복귀합니다.
            fsm.Initialize(idleState);
        }
    }
}
