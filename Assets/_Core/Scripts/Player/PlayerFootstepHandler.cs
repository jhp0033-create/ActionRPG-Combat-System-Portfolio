using UnityEngine;
using ActionRPG.Managers;

namespace ActionRPG.Player
{
    /// <summary>
    /// 애니메이션 창에서 '발이 바닥에 닿는 프레임'에 꽂아넣은 
    /// Event 마커를 수신하여 발소리를 재생하는 전담 컴포넌트입니다.
    /// </summary>
    public class PlayerFootstepHandler : MonoBehaviour
    {
        [Header("Data-Driven Sound Settings")]
        [Tooltip("재생할 발소리의 Data Asset Key (예: 'Footstep')")]
        public string footstepSoundID = "Footstep";

        [Header("Foot Bone Tracking Settings")]
        [Tooltip("캐릭터 모델 안의 '왼발 뼈(Transform)'를 끌어다 놓으세요.")]
        public Transform leftFoot;
        
        [Tooltip("캐릭터 모델 안의 '오른발 뼈(Transform)'를 끌어다 놓으세요.")]
        public Transform rightFoot;

        [Tooltip("발의 기본 높이 기준 오차 허용 범위 (기본 0.02)")]
        public float groundThresholdOffset = 0.02f;


        // 이전 프레임의 발 높이 (델타 계산용)
        private float lastLeftFootY;
        private float lastRightFootY;

        // 이전 프레임에 발이 '하강(아래로 이동)' 중이었는가?
        private bool wasLeftDescending = false;
        private bool wasRightDescending = false;

        private float lastFootstepTime = 0f;
        private const float FOOTSTEP_COOLDOWN = 0.15f;

        private Animator animator;

        private void Start()
        {
            animator = GetComponent<Animator>();

            if (leftFoot != null) lastLeftFootY = leftFoot.position.y;
            if (rightFoot != null) lastRightFootY = rightFoot.position.y;
        }

        private void LateUpdate()
        {
            if (string.IsNullOrEmpty(footstepSoundID)) return;

            // 캐릭터가 움직이고 있을 때만 발소리 추적 (Idle 시 미세한 흔들림 방지)
            if (animator != null && animator.GetFloat("Speed") < 0.1f) return;

            if (leftFoot != null)
                CheckFootInflection(leftFoot, ref lastLeftFootY, ref wasLeftDescending);

            if (rightFoot != null)
                CheckFootInflection(rightFoot, ref lastRightFootY, ref wasRightDescending);
        }

        /// <summary>
        /// 발 높이 변곡점을 기준으로 착지 프레임을 판정합니다.
        /// </summary>
        private void CheckFootInflection(Transform footTransform, ref float lastFootY, ref bool wasDescending)
        {
            float currentY = footTransform.position.y;
            float deltaY = currentY - lastFootY;

            // 발이 유의미하게 아래로 내려가고 있으면 '하강 중'으로 마킹
            bool isDescending = deltaY < -0.001f;
            
            // 이전 프레임까지 하강 중이었는데, 지금 상승(또는 평형)으로 꺾였다면? -> 바닥을 찍고 올라가는 순간(Impact)!
            if (wasDescending && deltaY > 0.001f)
            {
                // 양발이 동시에 닿거나 너무 짧은 주기로 겹치는 것 방지 (Debouncing)
                if (Time.time - lastFootstepTime >= FOOTSTEP_COOLDOWN)
                {
                    // 재질 상관없이 통일된 발소리 사운드 재생
                    SoundManager.Instance.PlaySFXByKey(footstepSoundID);

                    // 발걸음 이펙트 (오브젝트 풀링 연동)
                    if (ActionRPG.Data.VFXDatabase.Instance != null)
                    {
                        var prefab = ActionRPG.Data.VFXDatabase.Instance.GetFootstepPrefabByTag("Default");
                        if (prefab != null)
                        {
                            Vector3 spawnPos = footTransform.position + Vector3.up * ActionRPG.Data.VFXDatabase.Instance.footstepHeightOffset;
                            CombatEffects.SpawnPooledVFX(prefab, spawnPos, Quaternion.identity, 1.0f);
                        }
                    }

                    lastFootstepTime = Time.time;
                }
                
                // 중복 재생 판정을 막기 위해 상태 해제
                isDescending = false; 
            }

            // 현재 프레임의 상태를 다음 프레임을 위해 저장
            if (deltaY < -0.001f) wasDescending = true;
            else if (deltaY > 0.001f) wasDescending = false;

            lastFootY = currentY;
        }
    }
}
