using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.UI
{
    /// <summary>
    /// 적 캐릭터 머리 위 체력바(WorldHealthBar)를 오브젝트 풀링으로 관리합니다.
    /// DamageTextManager와 동일한 Queue 기반 풀링 패턴을 사용합니다.
    /// - 싱글톤으로 어디서든 WorldHealthBarManager.Instance.GetHealthBar()로 요청 가능
    /// - 적이 죽으면 ReturnHealthBar()로 풀에 반납
    /// </summary>
    public class WorldHealthBarManager : MonoBehaviour
    {
        public static WorldHealthBarManager Instance { get; private set; }

        [Header("Pooling Settings")]
        [Tooltip("WorldHealthBar.cs 가 붙어있는 UI 프리팹")]
        public GameObject healthBarPrefab;

        [Tooltip("미리 만들어둘 체력바 갯수 (최대 동시 등장 몬스터 수보다 여유있게)")]
        public int poolSize = 20;

        [Header("Canvas Reference")]
        [Tooltip("Screen Space - Camera 모드의 WorldUICanvas. 체력바가 이 캔버스 아래에 생성됩니다.")]
        public RectTransform worldUICanvas;

        private Queue<WorldHealthBar> barPool = new Queue<WorldHealthBar>();

        private void Awake()
        {
            // 싱글톤 초기화
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            // Awake 실행 순서 문제 방지: 모든 Awake가 끝난 후 풀 초기화
            InitializePool();
        }

        /// <summary>
        /// 씬 시작 시 poolSize만큼 체력바를 미리 생성하여 큐에 넣어둡니다.
        /// </summary>
        private void InitializePool()
        {
            if (healthBarPrefab == null)
            {
                Debug.LogWarning("[WorldHealthBarManager] 체력바 프리팹이 할당되지 않았습니다.");
                return;
            }

            for (int i = 0; i < poolSize; i++)
            {
                CreateNewBar();
            }
        }

        private WorldHealthBar CreateNewBar()
        {
            // WorldUICanvas 아래에 생성 (Screen Space Canvas의 자식)
            Transform parent = worldUICanvas != null ? worldUICanvas : transform;
            GameObject obj = Instantiate(healthBarPrefab, parent);
            obj.SetActive(false);

            WorldHealthBar bar = obj.GetComponent<WorldHealthBar>();
            if (bar != null)
            {
                barPool.Enqueue(bar);
            }
            return bar;
        }

        /// <summary>
        /// 적이 스폰될 때 호출합니다. 풀에서 체력바 하나를 꺼내 초기화하여 반환합니다.
        /// EnemyController.InitializeEnemy()에서 호출합니다.
        /// </summary>
        public WorldHealthBar GetHealthBar(Transform target, string enemyName, float maxHealth)
        {
            if (healthBarPrefab == null) return null;

            // Start 이전 요청이 들어와도 풀을 사용할 수 있도록 초기화합니다.
            if (barPool.Count == 0)
            {
                InitializePool();
            }

            WorldHealthBar bar = null;

            // 풀에서 꺼내기 (꺼져있는 것만 사용)
            while (barPool.Count > 0)
            {
                var candidate = barPool.Dequeue();
                barPool.Enqueue(candidate); // 돌려쓰기용 순환 큐

                if (!candidate.gameObject.activeInHierarchy)
                {
                    bar = candidate;
                    break;
                }
            }

            // 풀이 가득 찼으면 새로 하나 더 생성 (자동 확장)
            if (bar == null)
            {
                bar = CreateNewBar();
            }

            // 초기화 후 반환
            bar.gameObject.SetActive(true);
            bar.Initialize(target, enemyName, maxHealth);
            return bar;
        }

        /// <summary>
        /// 적이 죽을 때 호출합니다. 체력바를 비활성화하여 풀에 반납합니다.
        /// EnemyController.Die()에서 호출합니다.
        /// </summary>
        public void ReturnHealthBar(WorldHealthBar bar)
        {
            if (bar == null) return;
            bar.gameObject.SetActive(false);
        }
    }
}
