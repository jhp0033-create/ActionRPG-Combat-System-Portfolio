using UnityEngine;
using ActionRPG.Core; 
using ActionRPG.Enemy;
using ActionRPG.Data;
using System.Collections;
using UnityEngine.AI;
using System.Collections.Generic;

namespace ActionRPG.Managers
{
    /// <summary>
    /// 플레이어가 일정 거리 이내로 접근하면 마법진 이펙트와 함께 
    /// 몬스터들이 순서대로 시간 차를 두고 땅 밑에서 점프하며 솟구치도록 연출하는 구역 스포너 매니저입니다.
    /// 모든 연출용 VFX 프리팹은 VFXDatabase.Instance SO 싱글톤을 통해 중앙 집약적으로 관리합니다.
    /// </summary>
    public class AreaSpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("풀링 매니저에 등록된 몬스터의 태그 이름 (예: Skeleton)")]
        public string enemyTag = "Skeleton";
        
        [Tooltip("스폰할 마리 수")]
        public int spawnCount = 5;
        
        [Tooltip("스폰 중심점(현재 오브젝트)으로부터의 반경 (미터)")]
        public float spawnRadius = 5f;

        [Header("Spawn Layout")]
        [Tooltip("소환 위치끼리 유지할 최소 간격입니다. Skeleton 캡슐 반경 0.5보다 충분히 크게 잡아 마법진/VFX 겹침도 줄입니다.")]
        public float spawnMinDistance = 2.25f;

        [Tooltip("후보 지점에서 NavMesh를 찾을 허용 반경입니다. 너무 크면 의도한 포메이션에서 벗어날 수 있습니다.")]
        public float navMeshSampleRadius = 1.2f;

        [Tooltip("소환 위치 주변에 다른 적/플레이어/장애물이 있는지 확인하는 물리 반경입니다.")]
        public float spawnClearanceRadius = 0.8f;

        [Tooltip("소환 점유 검사에 사용할 레이어입니다. 기본값은 전체 레이어입니다.")]
        public LayerMask spawnBlockerLayer = ~0;

        [Header("Options")]
        [Tooltip("게임 시작 시 몬스터들이 같은 곳을 보게 할까요? 아니면 무작위 방향을 보게 할까요?")]
        public bool randomRotation = true;

        [Tooltip("땅바닥으로 인식할 레이어 마스크 (기본값: Default)")]
        public LayerMask groundLayer = ~0; // 기본적으로 모든 레이어

        [Header("Trigger & Emergence Sequence Settings")]
        [Tooltip("플레이어가 이 반경 내로 접근하면 스폰 연출을 시작합니다 (미터)")]
        public float detectionRadius = 12f;

        [Tooltip("몬스터들 사이의 순차 소환 시간 간격 (초)")]
        public float spawnInterval = 0.8f;

        [Tooltip("마법진이 켜지고 몬스터가 솟구쳐 오르기 전까지 대기할 딜레이 시간 (초)")]
        public float delayBeforeJump = 1.5f;

        [Tooltip("도착 폭발이 먼저 보인 뒤 소환 마법진/몬스터 생성이 시작되기까지의 짧은 딜레이입니다.")]
        public float arrivalSpawnDelay = 0.6f;

        [Header("Light Pillar Effect Transform")]
        [Tooltip("네비게이션 용 빛 기둥 이펙트 트랜스폼")]
        public Transform pillarTransform;

        [Header("Story / Dialogue (Optional)")]
        [Tooltip("적들이 솟아오르기 직전 폭발과 함께 띄울 말풍선 대사 ID (예: Dialogue_Arrival)")]
        public string arrivalDialogueID = "Dialogue_Arrival";



        private bool isSpawned = false;
        private Transform playerTransform;
        private readonly List<Vector3> reservedSpawnPositions = new List<Vector3>();
        private readonly Collider[] spawnOverlapBuffer = new Collider[24];
        private const int RandomFallbackAttempts = 60;
        private const float BrakeTransitionDelay = 3.0f;

        private string ToRoman(int number)
        {
            if ((number < 0) || (number > 3999)) return number.ToString();
            if (number < 1) return string.Empty;            
            if (number >= 1000) return "M" + ToRoman(number - 1000);
            if (number >= 900) return "CM" + ToRoman(number - 900); 
            if (number >= 500) return "D" + ToRoman(number - 500);
            if (number >= 400) return "CD" + ToRoman(number - 400);
            if (number >= 100) return "C" + ToRoman(number - 100);            
            if (number >= 90) return "XC" + ToRoman(number - 90);
            if (number >= 50) return "L" + ToRoman(number - 50);
            if (number >= 40) return "XL" + ToRoman(number - 40);
            if (number >= 10) return "X" + ToRoman(number - 10);
            if (number >= 9) return "IX" + ToRoman(number - 9);
            if (number >= 5) return "V" + ToRoman(number - 5);
            if (number >= 4) return "IV" + ToRoman(number - 4);
            if (number >= 1) return "I" + ToRoman(number - 1);
            return number.ToString();
        }

        private void Start()
        {
            // 플레이어 캐싱
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        private void Update()
        {
            if (isSpawned) return;

            // 런타임 스폰 대응 플레이어 지연 탐색
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
                return;
            }

            // 플레이어와의 수평 거리 검사
            float distToPlayer = Vector3.Distance(transform.position, playerTransform.position);
            if (distToPlayer <= detectionRadius)
            {
                isSpawned = true;

                var playerController = ResolvePlayerController();
                bool waitForBrakeTransition = playerController != null && playerController.isQuestNavigating;
                if (playerController != null)
                {
                    playerController.StopQuestNavigation(true, smoothBrake: true);
                }

                var dynamicButtonAnimator = UnityEngine.Object.FindFirstObjectByType<ActionRPG.UI.DynamicButtonAnimator>();
                if (dynamicButtonAnimator != null)
                {
                    dynamicButtonAnimator.PlayBattleEntryNavigationExit();
                }

                if (SoundManager.Instance != null)
                {
                    SoundManager.Instance.PlaySFXByKey("Arrival");
                }

                StartCoroutine(StartSpawnSequence(waitForBrakeTransition));
            }
        }

        private IEnumerator StartSpawnSequence(bool waitForBrakeTransition)
        {
            CombatEffects.SpawnPooledVFX(
                VFXDatabase.Instance.arrivalExplosionPrefab,
                pillarTransform.position,
                pillarTransform.rotation,
                3f);

            ParticleSystem[] pillarParticles = GetComponentsInChildren<ParticleSystem>();
            foreach (var p in pillarParticles)
            {
                var main = p.main;
                main.loop = false;
                p.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            float spawnDelay = Mathf.Max(0f, arrivalSpawnDelay);
            yield return new WaitForSeconds(spawnDelay);
            float elapsedBeforeBattle = spawnDelay;

            if (!string.IsNullOrEmpty(arrivalDialogueID) && ActionRPG.UI.UI_DialogueManager.Instance != null)
            {
                ActionRPG.UI.UI_DialogueManager.Instance.StartDialogueByKey(arrivalDialogueID, autoAdvanceLines: true, lineHoldDuration: 1.5f, target: playerTransform);
            }

            Coroutine spawnRoutine = StartCoroutine(SpawnEnemiesRoutine());

            if (waitForBrakeTransition && elapsedBeforeBattle < BrakeTransitionDelay)
            {
                yield return new WaitForSeconds(BrakeTransitionDelay - elapsedBeforeBattle);
            }

            BattleManager.Instance?.StartBattle();

            yield return spawnRoutine;
        }

        private System.Collections.IEnumerator SpawnEnemiesRoutine()
        {
            // 아직 EnemyManager가 준비되지 않았을 수 있으므로 체크
            if (EnemyManager.Instance == null)
            {
                UnityEngine.Debug.LogError("[AreaSpawner] 씬에 EnemyManager가 존재하지 않습니다! 스폰을 취소합니다.");
                yield break;
            }

            // 마법진 이펙트 프리팹 캐싱 (VFXDatabase SO Singleton 사용)
            UnityEngine.GameObject spawnVfxPrefab = null;
            if (VFXDatabase.Instance != null)
            {
                spawnVfxPrefab = VFXDatabase.Instance.spawnCircleVFXPrefab;
            }
            reservedSpawnPositions.Clear();

            int requestedSpawnCount = ResolveInitialSpawnCount();
            int spawnedCount = 0;
            for (int slotIndex = 0; slotIndex < requestedSpawnCount; slotIndex++)
            {
                if (!TryFindFormationNavMeshPoint(slotIndex, requestedSpawnCount, reservedSpawnPositions, out Vector3 finalSpawnPos))
                {
                    UnityEngine.Debug.LogWarning($"[AreaSpawner] {slotIndex + 1}번째 포메이션 슬롯의 유효한 NavMesh 소환 지점을 찾지 못했습니다.");
                    continue;
                }

                GameObject spawnedEnemy = SpawnEnemyAtGround(finalSpawnPos, $"{enemyTag} {ToRoman(spawnedCount + 1)}", spawnVfxPrefab);
                if (spawnedEnemy == null)
                {
                    continue;
                }

                spawnedCount++;

                if (spawnedCount < requestedSpawnCount)
                {
                    yield return new WaitForSeconds(spawnInterval);
                }
            }

            if (spawnedCount < requestedSpawnCount)
            {
                UnityEngine.Debug.LogWarning($"[AreaSpawner] 요청 수량 {requestedSpawnCount}마리 중 {spawnedCount}마리만 소환했습니다. AreaSpawner 위치 주변 NavMesh를 확인하세요.");
            }
        }

        private int ResolveInitialSpawnCount()
        {
            int requested = Mathf.Max(0, spawnCount);
            if (BattleManager.Instance == null)
            {
                return requested;
            }

            int targetLimit = BattleManager.Instance.TargetKillCount;
            if (targetLimit <= 0)
            {
                return requested;
            }

            return Mathf.Min(requested, targetLimit);
        }

        private bool TryFindFormationNavMeshPoint(int slotIndex, int totalCount, List<Vector3> reservedPositions, out Vector3 result)
        {
            foreach (Vector3 localOffset in EnumerateFormationOffsets(slotIndex, totalCount))
            {
                Vector3 candidate = transform.position + transform.right * localOffset.x + transform.forward * localOffset.z;
                candidate.y = transform.position.y;

                if (TryResolveSpawnCandidate(candidate, reservedPositions, out result))
                {
                    return true;
                }
            }

            result = Vector3.zero;
            return false;
        }

        private IEnumerable<Vector3> EnumerateFormationOffsets(int slotIndex, int totalCount)
        {
            float spacing = Mathf.Max(spawnMinDistance, 1.5f);
            float centerIndex = (totalCount - 1) * 0.5f;
            float x = (slotIndex - centerIndex) * spacing;
            float normalizedSide = totalCount > 1 ? Mathf.Abs(slotIndex - centerIndex) / centerIndex : 0f;
            float z = Mathf.Lerp(1.2f, -0.75f, normalizedSide);

            Vector3 primary = ClampOffsetToSpawnRadius(new Vector3(x, 0f, z));
            yield return primary;

            // 같은 슬롯이 지형/장애물에 걸릴 때, 포메이션 형태를 크게 해치지 않는 작은 보정 후보를 순서대로 검사합니다.
            yield return ClampOffsetToSpawnRadius(primary + new Vector3(0f, 0f, 0.8f));
            yield return ClampOffsetToSpawnRadius(primary + new Vector3(0f, 0f, -0.8f));
            yield return ClampOffsetToSpawnRadius(primary + new Vector3(Mathf.Sign(x == 0f ? 1f : x) * 0.7f, 0f, 0.35f));
            yield return ClampOffsetToSpawnRadius(primary + new Vector3(Mathf.Sign(x == 0f ? -1f : -x) * 0.7f, 0f, -0.35f));

            for (int attempt = 0; attempt < RandomFallbackAttempts; attempt++)
            {
                Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
                yield return new Vector3(randomCircle.x, 0f, randomCircle.y);
            }
        }

        private Vector3 ClampOffsetToSpawnRadius(Vector3 offset)
        {
            Vector2 flat = new Vector2(offset.x, offset.z);
            float maxRadius = Mathf.Max(0.1f, spawnRadius - 0.25f);
            if (flat.magnitude <= maxRadius)
            {
                return offset;
            }

            flat = flat.normalized * maxRadius;
            return new Vector3(flat.x, 0f, flat.y);
        }

        private bool TryResolveSpawnCandidate(Vector3 candidate, List<Vector3> reservedPositions, out Vector3 result)
        {
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
            {
                result = Vector3.zero;
                return false;
            }

            Vector2 centerToHit = new Vector2(hit.position.x - transform.position.x, hit.position.z - transform.position.z);
            if (centerToHit.magnitude > spawnRadius)
            {
                result = Vector3.zero;
                return false;
            }

            if (reservedPositions.Exists(p => Vector3.Distance(p, hit.position) < spawnMinDistance))
            {
                result = Vector3.zero;
                return false;
            }

            if (!IsSpawnAreaClear(hit.position))
            {
                result = Vector3.zero;
                return false;
            }

            result = hit.position;
            reservedPositions.Add(result);
            return true;
        }

        private bool IsSpawnAreaClear(Vector3 position)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                position + Vector3.up * 0.8f,
                spawnClearanceRadius,
                spawnOverlapBuffer,
                spawnBlockerLayer,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider col = spawnOverlapBuffer[i];
                if (col == null)
                {
                    continue;
                }

                if (col is TerrainCollider)
                {
                    continue;
                }

                if (col.CompareTag("Enemy") || col.CompareTag("Player"))
                {
                    return false;
                }

                if (col.GetComponentInParent<EnemyController>() != null ||
                    col.GetComponentInParent<ActionRPG.Player.NetworkPlayerController>() != null)
                {
                    return false;
                }
            }

            return true;
        }

        public bool SpawnRandomReplacement(string inheritedName)
        {
            List<Vector3> activeEnemyPositions = CollectActiveEnemyPositions();

            if (!TryFindReplacementNavMeshPoint(activeEnemyPositions, out Vector3 finalSpawnPos))
            {
                UnityEngine.Debug.LogWarning("[AreaSpawner] 교체 스폰용 NavMesh 지점을 찾지 못했습니다.");
                return false;
            }

            return SpawnEnemyAtGround(finalSpawnPos, inheritedName) != null;
        }

        private bool TryFindReplacementNavMeshPoint(List<Vector3> activeEnemyPositions, out Vector3 result)
        {
            int startSlot = Random.Range(0, Mathf.Max(1, spawnCount));
            for (int i = 0; i < spawnCount; i++)
            {
                int slotIndex = (startSlot + i) % spawnCount;
                foreach (Vector3 localOffset in EnumerateFormationOffsets(slotIndex, spawnCount))
                {
                    Vector3 candidate = transform.position + transform.right * localOffset.x + transform.forward * localOffset.z;
                    candidate.y = transform.position.y;

                    if (TryResolveSpawnCandidate(candidate, activeEnemyPositions, out result))
                    {
                        return true;
                    }
                }
            }

            result = Vector3.zero;
            return false;
        }

        private List<Vector3> CollectActiveEnemyPositions()
        {
            List<Vector3> positions = new List<Vector3>();
            if (EnemyManager.Instance == null)
            {
                return positions;
            }

            foreach (GameObject enemyObject in EnemyManager.Instance.ActiveEnemies)
            {
                if (enemyObject == null || !enemyObject.activeInHierarchy)
                {
                    continue;
                }

                EnemyController enemy = enemyObject.GetComponent<EnemyController>();
                if (enemy == null)
                {
                    enemy = enemyObject.GetComponentInChildren<EnemyController>();
                }

                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                positions.Add(enemyObject.transform.position);
            }

            return positions;
        }

        public GameObject SpawnEnemyAtGround(Vector3 groundPosition, string inheritedName)
        {
            GameObject spawnVfxPrefab = VFXDatabase.Instance != null
                ? VFXDatabase.Instance.spawnCircleVFXPrefab
                : null;

            string displayName = EnemyManager.Instance != null
                ? EnemyManager.Instance.GetNextRomanName(inheritedName)
                : inheritedName;

            return SpawnEnemyAtGround(groundPosition, displayName, spawnVfxPrefab);
        }
        private GameObject SpawnEnemyAtGround(Vector3 groundPosition, string displayName, GameObject spawnVfxPrefab)
        {
            if (EnemyManager.Instance == null)
            {
                return null;
            }

            EnsurePlayerTransform();

            Quaternion spawnRot = ResolveSpawnRotation(groundPosition);

            if (spawnVfxPrefab != null)
            {
                // 변경: Quaternion.identity 대신 지형 노멀에 맞춘 회전 사용
                Quaternion vfxRot = GetGroundAlignedRotation(groundPosition);
                CombatEffects.SpawnPooledVFX(spawnVfxPrefab, groundPosition, vfxRot, delayBeforeJump + 2.0f);
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySpawnSFX();
            }

            Vector3 startBuriedPos = new Vector3(groundPosition.x, groundPosition.y - 1.5f, groundPosition.z);
            GameObject obj = EnemyManager.Instance.SpawnEnemy(enemyTag, startBuriedPos, spawnRot);

            if (obj == null) return null;

            EnemyController enemyCtrl = obj.GetComponent<EnemyController>();
            if (enemyCtrl == null) enemyCtrl = obj.GetComponentInChildren<EnemyController>();

            if (enemyCtrl != null)
            {
                enemyCtrl.SetEnemyName(displayName);
                // groundPosition은 이제 NavMesh.SamplePosition 결과이므로,
                // EnemyController.StartSpawnSequence 내부에서 연출 종료 시 agent.Warp(groundPosition) 호출 필요
                // (EnemyController.cs 쪽 수정 — 이전 답변의 "수정사항 3" 참고)
                enemyCtrl.StartSpawnSequence(groundPosition, delayBeforeJump);

            }

            return obj;
        }

        // 신규 추가 헬퍼: VFX용 지형 정렬 회전값 계산 (groundLayer는 여기서 보조용으로 재사용)
        private Quaternion GetGroundAlignedRotation(Vector3 position)
        {
            Vector3 rayOrigin = position + Vector3.up * 2f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5f, groundLayer))
            {
                return Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
            return Quaternion.identity;
        }


        private Quaternion ResolveSpawnRotation(Vector3 spawnPosition)
        {
            if (randomRotation)
            {
                return Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            return transform.rotation;
        }

        private void EnsurePlayerTransform()
        {
            if (playerTransform != null) return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        private ActionRPG.Player.NetworkPlayerController ResolvePlayerController()
        {
            EnsurePlayerTransform();
            if (playerTransform == null)
            {
                return null;
            }

            var controller = playerTransform.GetComponentInParent<ActionRPG.Player.NetworkPlayerController>();
            if (controller == null)
            {
                controller = playerTransform.GetComponentInChildren<ActionRPG.Player.NetworkPlayerController>();
            }

            return controller;
        }

        // 유니티 에디터 창에서 스폰 반경과 플레이어 감지 범위를 직관적으로 시각화하는 툴(기즈모)
        private void OnDrawGizmos()
        {
            // 스폰 반경 (반투명 붉은색)
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f); 
            Gizmos.DrawSphere(transform.position, spawnRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, spawnRadius);

            // 플레이어 감지 반경 (반투명 노란색)
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.05f); 
            Gizmos.DrawSphere(transform.position, detectionRadius);
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
        }
    }
}
