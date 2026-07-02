using UnityEngine;

namespace ActionRPG.Player
{
    /// <summary>
    /// 단발성 스킬(예: 방패치기, 광역기 등)의 애니메이션 State에 부착되어
    /// 스킬 전용 타격 시스템(ExecuteSkillDamage)과 잠금 해제를 제어하는 동작 스크립트입니다.
    /// </summary>
    public class PlayerSkillBehaviour : StateMachineBehaviour
    {
        private NetworkPlayerController playerController;

        [Header("Skill Settings")]
        [Tooltip("스킬 데미지가 발동될 애니메이션 진행도 (0.0 ~ 1.0)")]
        [Range(0f, 1f)]
        public float damageFrame = 0.2f;

        // 프레임 진행률별 1회 실행 보장 플래그
        private bool hasDealtDamage = false;

        // ─────────────────────────────────────────────
        // OnStateEnter
        // ─────────────────────────────────────────────
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (playerController == null)
            {
                playerController = animator.GetComponentInParent<NetworkPlayerController>();
                if (playerController == null)
                    Debug.LogError("[PlayerSkillBehaviour] NetworkPlayerController를 찾을 수 없습니다!");
            }

            hasDealtDamage = false;

            if (playerController != null)
            {
                // [고도화] 스킬 시전 중 하드 락(Lock)
                // 방패치기 시전 중에는 다른 공격 상태로 전환하지 않습니다.
                playerController.isAttacking = true; 
                playerController.canSaveAttack = false;
                
                // 기존 무기(검) 콜라이더가 혹시 켜져있다면 강제 종료
                playerController.DisableWeaponHitbox();
            }
        }

        // ─────────────────────────────────────────────
        // OnStateUpdate
        // ─────────────────────────────────────────────
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (playerController == null) return;

            float progress = stateInfo.normalizedTime % 1f;

            // 지정된 데미지 프레임(예: 20%)에 도달하면 스킬 전용 확정 데미지를 단 1회 발사합니다.
            if (progress >= damageFrame && !hasDealtDamage)
            {
                hasDealtDamage = true;
                
                // 스킬 데이터 기반 정면 범위 타격 시스템 호출.
                // 캐스트 단위 가드를 거쳐 같은 스킬 타격이 중복 적용되지 않게 합니다.
                playerController.ExecuteSkillDamageOnce();
                
                // 콤보 스킬이 아니므로 VFX는 NetworkPlayerController.UseSkill()에서 이미 스폰되었을 수 있습니다.
                // 만약 애니메이션 타격 시점에 이펙트를 터트리고 싶다면 여기서 별도 VFX를 스폰할 수도 있습니다.
            }
        }

        // ─────────────────────────────────────────────
        // OnStateExit
        // ─────────────────────────────────────────────
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (playerController != null)
            {
                // 애니메이션이 완전히 끝나면 스킬 사용 상태를 해제하고 기본 상태로 복귀시킵니다.
                playerController.ResetAttackLock();
            }
        }
    }
}
