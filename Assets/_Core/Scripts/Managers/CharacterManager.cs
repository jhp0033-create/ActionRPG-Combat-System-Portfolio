using UnityEngine;

namespace ActionRPG.Core
{
    /// <summary>
    /// 플레이어 캐릭터의 스폰 지점을 관리합니다.
    /// </summary>
    public class CharacterManager : MonoBehaviour
    {
        public static CharacterManager Instance { get; private set; }

        [Header("Player Settings")]
        public GameObject playerPrefab;
        public Transform spawnPoint;

        // 런타임에 인스턴스화된 플레이어 참조
        public GameObject CurrentPlayer { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            SpawnPlayer();
        }

        private void SpawnPlayer()
        {
            if (playerPrefab == null || spawnPoint == null) return;

            CurrentPlayer = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        }
    }
}
