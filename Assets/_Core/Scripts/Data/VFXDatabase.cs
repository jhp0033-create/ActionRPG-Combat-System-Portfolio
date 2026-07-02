using UnityEngine;

namespace ActionRPG.Data
{
    /// <summary>
    /// 전투 연출에 필요한 VFX 프리팹 참조를 ScriptableObject 에셋으로 관리합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "VFXDatabase", menuName = "ActionRPG/VFXDatabase")]
    public class VFXDatabase : ScriptableObject
    {
        private static VFXDatabase _instance;
        public static VFXDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<VFXDatabase>("VFXDatabase");
                    if (_instance == null)
                    {
                        _instance = Resources.Load<VFXDatabase>("Data/VFXDatabase");
                    }
                    if (_instance == null)
                    {
                        Debug.LogError("[VFXDatabase] Resources 폴더 내에 VFXDatabase 에셋을 찾을 수 없습니다! " +
                                       "Assets/_Core/Resources/VFXDatabase 에셋 생성을 확인해 주세요.");
                    }
                }
                return _instance;
            }
        }

        [Header("Target Indicator Settings")]
        [Tooltip("타겟팅된 적 발밑에 표시할 원형 파티클/데칼 프리팹")]
        public GameObject targetIndicatorPrefab;
        
        [Tooltip("타겟 표시기의 발밑 높이 오프셋")]
        public float indicatorHeightOffset = 0.05f;

        [Tooltip("피격 이펙트 기본 프리팹 (필요 시 확장)")]
        public GameObject defaultHitVFXPrefab;

        [Header("Player Attack Settings")]
        [Tooltip("플레이어 공격 시 발생하는 기본 검기/슬래시 이펙트 프리팹")]
        public GameObject defaultSlashVFXPrefab;
        
        [Tooltip("검기 이펙트 생성 위치 오프셋 (플레이어 발밑 기준)")]
        public Vector3 slashVFXOffset = new Vector3(0, 1f, 0.8f);

        [Tooltip("콤보 단계별 검기 이펙트 회전 오프셋 (1타, 2타, 3타...)")]
        public Vector3[] slashVFXRotations = new Vector3[] { 
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 180),
            new Vector3(0, 0, 45)
        };

        [Header("Enemy Spawn Settings")]
        [Tooltip("몬스터 스폰 시 바닥에 깔리는 마법진/소환 이펙트 프리팹")]
        public GameObject spawnCircleVFXPrefab;

        [Header("Reward Settings")]
        [Tooltip("적 사망 시 발생하는 골드 드랍/폭죽 파티클 이펙트")]
        public GameObject goldDropVFXPrefab;

        [Tooltip("골드 이펙트 생성 높이 오프셋")]
        public float goldDropHeightOffset = 0.5f;

        [Header("UI VFX Settings")]
        [Tooltip("모바일 화면 터치 시 생성될 UI 팝업 이펙트 프리팹 (단발성)")]
        public GameObject touchVFXPrefab;

        [Tooltip("드래그 시 손가락을 따라다닐 트레일 이펙트 프리팹")]
        public GameObject trailVFXPrefab;

        [Header("Footstep VFX Settings")]
        [Tooltip("기본 흙먼지 착지 이펙트 프리팹 (일반 땅)")]
        public GameObject footstepDustPrefab;

        [Tooltip("풀밭 착지 이펙트 (Grass 태그 표면)")]
        public GameObject footstepGrassPrefab;

        [Tooltip("VFX 생성 시 지면에서 살짝 위로 띄울 오프셋 (Z-Fighting 방지)")]
        public float footstepHeightOffset = 0.05f;

        [Tooltip("빛기둥 도착 이펙트")]
        public GameObject arrivalExplosionPrefab;

        /// <summary>
        /// 콜라이더 태그를 받아 적절한 발 착지 VFX 프리팹을 반환합니다.
        /// </summary>
        public GameObject GetFootstepPrefabByTag(string surfaceTag)
        {
            if (surfaceTag == "Grass" && footstepGrassPrefab != null)
                return footstepGrassPrefab;

            return footstepDustPrefab;
        }
    }
}
