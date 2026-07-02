using UnityEngine;
using System.Collections.Generic;

namespace ActionRPG.Core
{
    /// <summary>
    /// 적 스폰, 회수, 활성 적 목록을 관리합니다.
    /// </summary>
    public class EnemyManager : MonoBehaviour
    {
        public static EnemyManager Instance { get; private set; }

        [System.Serializable]
        public class PoolData
        {
            [Tooltip("풀링할 몬스터의 이름")]
            public string poolTag;
            public GameObject prefab;
            public int initialSize;
        }

        [Header("Object Pooling Settings")]
        [Tooltip("생성할 몬스터들의 종류와 초기 개수를 설정하세요.")]
        public List<PoolData> pools;
        
        private Dictionary<string, Queue<GameObject>> poolDictionary;
        
        private Dictionary<string, GameObject> prefabDictionary;

        public List<GameObject> ActiveEnemies { get; private set; } = new List<GameObject>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            InitializePools();
        }

        private void InitializePools()
        {
            poolDictionary = new Dictionary<string, Queue<GameObject>>();
            prefabDictionary = new Dictionary<string, GameObject>();

            foreach (PoolData pool in pools)
            {
                Queue<GameObject> objectPool = new Queue<GameObject>();
                
                prefabDictionary.Add(pool.poolTag, pool.prefab);

                for (int i = 0; i < pool.initialSize; i++)
                {

                    bool wasActive = pool.prefab.activeSelf;
                    pool.prefab.SetActive(false);
                    
                    GameObject obj = Instantiate(pool.prefab, transform);
                    obj.name = pool.poolTag;
                    
                    pool.prefab.SetActive(wasActive);
                    objectPool.Enqueue(obj);
                }

                poolDictionary.Add(pool.poolTag, objectPool);
            }
        }

        /// <summary>
        /// 지정한 풀에서 적을 꺼내 스폰합니다.
        /// </summary>
        public GameObject SpawnEnemy(string poolTag, Vector3 position, Quaternion rotation)
        {
            if (!poolDictionary.ContainsKey(poolTag))
            {
                return null;
            }

            Queue<GameObject> pool = poolDictionary[poolTag];
            GameObject obj;

            if (pool.Count == 0)
            {
                obj = Instantiate(prefabDictionary[poolTag], transform);
                obj.name = poolTag;
            }
            else
            {
                obj = pool.Dequeue();
            }

            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
            
            ActiveEnemies.Add(obj);
            return obj;
        }

        /// <summary>
        /// 비활성화된 적을 원래 풀로 반환합니다.
        /// </summary>
        public void ReturnToPool(GameObject enemy)
        {
            enemy.SetActive(false);
            ActiveEnemies.Remove(enemy);
            
            string poolTag = enemy.name;
            if (poolDictionary.ContainsKey(poolTag))
            {
                poolDictionary[poolTag].Enqueue(enemy);
            }
            else
            {
                Destroy(enemy);
            }
        }

        /// <summary>
        /// 플레이어 오토 타겟팅을 위해 반경 내 가장 가까운 적을 반환합니다.
        /// </summary>
        public Transform GetClosestEnemy(Vector3 origin, float radius)
        {
            Transform closest = null;
            float minDistance = radius * radius; // sqrMagnitude 비교용

            foreach (var enemy in ActiveEnemies)
            {
                if (!enemy.activeInHierarchy) continue;

                float distSqr = (enemy.transform.position - origin).sqrMagnitude;
                if (distSqr < minDistance)
                {
                    minDistance = distSqr;
                    closest = enemy.transform;
                }
            }

            return closest;
        }

        #region Roman Numeral Logic
        private Dictionary<string, int> spawnCounters = new Dictionary<string, int>();

        public string ToRoman(int number)
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

        private int RomanToInt(string s)
        {
            if (string.IsNullOrEmpty(s)) return -1;
            Dictionary<char, int> values = new Dictionary<char, int>
            {
                {'I', 1}, {'V', 5}, {'X', 10}, {'L', 50}, {'C', 100}, {'D', 500}, {'M', 1000}
            };
            int total = 0;
            for (int i = 0; i < s.Length; i++)
            {
                if (!values.ContainsKey(s[i])) return -1; 
                
                if (i + 1 < s.Length && values[s[i]] < values[s[i + 1]])
                {
                    total -= values[s[i]];
                }
                else
                {
                    total += values[s[i]];
                }
            }
            return total;
        }

        public string GetNextRomanName(string baseName)
        {
            baseName = baseName.Replace(" (Wave)", "").Trim();
            
            string[] parts = baseName.Split(' ');
            string pureName = baseName;
            int currentNum = 0;

            if (parts.Length > 0)
            {
                string lastPart = parts[parts.Length - 1];
                int val = RomanToInt(lastPart);
                if (val > 0)
                {
                    pureName = baseName.Substring(0, baseName.Length - lastPart.Length).Trim();
                    currentNum = val;
                }
            }

            if (!spawnCounters.ContainsKey(pureName))
            {
                spawnCounters[pureName] = currentNum;
            }
            else
            {
                spawnCounters[pureName] = Mathf.Max(spawnCounters[pureName], currentNum);
            }

            spawnCounters[pureName]++;
            return pureName + " " + ToRoman(spawnCounters[pureName]);
        }
        #endregion
    }
}
