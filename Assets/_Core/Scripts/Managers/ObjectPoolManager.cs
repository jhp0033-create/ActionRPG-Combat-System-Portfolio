using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Managers
{
    /// <summary>
    /// VFX와 발사체 등 반복 생성 오브젝트를 프리팹 기준으로 재사용합니다.
    /// </summary>
    public class ObjectPoolManager : MonoBehaviour
    {
        public static ObjectPoolManager Instance { get; private set; }

        private Dictionary<int, Queue<GameObject>> poolDictionary = new Dictionary<int, Queue<GameObject>>();
        
        private Dictionary<int, int> spawnedObjectToPrefabId = new Dictionary<int, int>();

        [Tooltip("오브젝트들이 하이어라키에서 지저분해지지 않도록 담아둘 부모 트랜스폼")]
        public Transform poolContainer;

        [Header("UI / Screen Effects")]
        [Tooltip("화면을 덮거나 캔버스에 종속되어야 하는 UI 이펙트(쉴드배쉬 등)를 모아둘 캔버스 부모입니다. 여기에 Canvas_Effect를 끌어다 넣으세요!")]
        public Transform effectCanvasContainer;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (poolContainer == null)
                {
                    GameObject container = new GameObject("PoolContainer");
                    container.transform.SetParent(transform);
                    poolContainer = container.transform;
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 풀에서 오브젝트를 꺼내고 필요하면 새 인스턴스를 생성합니다.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                return null;
            }

            int prefabId = prefab.GetInstanceID();

            if (!poolDictionary.ContainsKey(prefabId))
            {
                poolDictionary.Add(prefabId, new Queue<GameObject>());
            }

            GameObject objToSpawn = null;
            Queue<GameObject> queue = poolDictionary[prefabId];

            while (queue.Count > 0)
            {
                GameObject candidate = queue.Dequeue();
                if (candidate != null && !candidate.activeInHierarchy)
                {
                    objToSpawn = candidate;
                    break;
                }
            }

            if (objToSpawn == null)
            {
                objToSpawn = Instantiate(prefab);
                
                int instanceId = objToSpawn.GetInstanceID();
                spawnedObjectToPrefabId[instanceId] = prefabId;

                PooledObject pooledInfo = objToSpawn.GetComponent<PooledObject>();
                if (pooledInfo == null)
                {
                    pooledInfo = objToSpawn.AddComponent<PooledObject>();
                }
                pooledInfo.prefabId = prefabId;
            }

            objToSpawn.transform.SetParent(parent == null ? poolContainer : parent);
            objToSpawn.transform.position = position;
            objToSpawn.transform.rotation = rotation;
            
            objToSpawn.transform.localScale = prefab.transform.localScale;
            
            objToSpawn.SetActive(true);

            return objToSpawn;
        }

        /// <summary>
        /// 사용이 끝난 오브젝트를 풀로 돌려보냅니다.
        /// </summary>
        public void Despawn(GameObject obj)
        {
            if (obj == null) return;

            if (!obj.activeInHierarchy) return;

            int instanceId = obj.GetInstanceID();
            int targetPrefabId = -1;

            // 1. 컴포넌트로 소속 풀 확인 (가장 확실함)
            PooledObject pooledInfo = obj.GetComponent<PooledObject>();
            if (pooledInfo != null)
            {
                targetPrefabId = pooledInfo.prefabId;
            }
            // 2. 딕셔너리로 소속 풀 확인 (방어적 코드)
            else if (spawnedObjectToPrefabId.TryGetValue(instanceId, out int storedPrefabId))
            {
                targetPrefabId = storedPrefabId;
            }

            if (targetPrefabId != -1 && poolDictionary.ContainsKey(targetPrefabId))
            {
                // 초기화 및 풀 반환
                obj.SetActive(false);
                obj.transform.SetParent(poolContainer);
                poolDictionary[targetPrefabId].Enqueue(obj);
            }
            else
            {
                // 매니저를 통해 생성되지 않은 오브젝트라면 그냥 파괴
                Destroy(obj);
            }
        }
    }
}
