using UnityEngine;

namespace ActionRPG.Player
{
    /// <summary>
    /// 애니메이터의 각 공격 노드(State)에 부착되어,
    /// 애니메이션이 끝나는 정확한 프레임에 플레이어의 공격 잠금(Lock)을 해제합니다.
    /// </summary>
    public class PlayerAttackBehaviour : StateMachineBehaviour
    {
        // 최적화를 위해 컨트롤러 캐싱
        private NetworkPlayerController playerController;

        // 히트박스(콜라이더) 제어 플래그
        private bool hasEnabledHitbox = false;
        private bool hasDisabledHitbox = false;
        private int stateAttackSequence = 0;

        [Header("Timing")]
        [Tooltip("1번 스킬 자동 3연격의 다음 타격으로 넘어가는 진행도입니다. 평타 선입력 창과 분리됩니다.")]
        [Range(0.25f, 0.8f)]
        public float skillComboAdvanceFrame = 0.5f;

        // 이 State에 진입할 때 한 번 실행됩니다 (예: 검을 휘두르기 시작할 때)
        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (playerController == null)
            {
                // 애니메이터가 자식 모델에 붙어있을 확률이 높으므로 부모(Root)까지 타고 올라가서 찾습니다.
                playerController = animator.GetComponentInParent<NetworkPlayerController>();
                
                if (playerController == null)
                {
                    Debug.LogError("[PlayerAttackBehaviour] 부모 오브젝트에서 NetworkPlayerController를 찾을 수 없습니다! 구조를 확인해주세요.");
                }
            }

            hasEnabledHitbox = false;
            hasDisabledHitbox = false;

            // 새로운 공격 시작 시, 무조건 예약 창을 닫고 안전장치로 타격 판정도 끕니다.
            if (playerController != null)
            {
                stateAttackSequence = playerController.AttackSequence;
                playerController.canSaveAttack = false;
                playerController.DisableWeaponHitbox();
            }
        }

        // 매 프레임 실행됩니다. 애니메이션 진행률을 감시합니다.
        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (playerController != null)
            {
                if (stateAttackSequence != playerController.AttackSequence)
                {
                    return;
                }

                float progress = stateInfo.normalizedTime % 1f; // 반복 애니메이션 대비 나머지 연산

                // 애니메이션 타격 구간에 맞춰 판정을 켭니다.
                if (progress >= 0.2f && !hasEnabledHitbox)
                {
                    playerController.EnableWeaponHitbox();
                    hasEnabledHitbox = true;
                }

                // 타격 구간이 끝나면 판정을 끄고 콤보 선입력 창을 엽니다.
                if (progress >= 0.45f && !hasDisabledHitbox)
                {
                    playerController.DisableWeaponHitbox();
                    hasDisabledHitbox = true;
                    
                    if (!playerController.isSkillComboActive && !playerController.canSaveAttack)
                    {
                        playerController.canSaveAttack = true;
                    }
                }

                if (playerController.isSkillComboActive && progress >= skillComboAdvanceFrame)
                {
                    playerController.OpenSkillComboAdvanceWindow();
                }

                // 평타 3타는 즉시 재진입을 제한하고 말미 후딜만 줄입니다.
                float transitionThreshold = (playerController.comboStep == 3) ? 0.90f : 0.6f;

                if (!playerController.isSkillComboActive && progress >= transitionThreshold && playerController.TryConsumeSavedComboAttack())
                {
                    hasEnabledHitbox = false;
                    hasDisabledHitbox = false;
                }

            }
        }

        // 이 State가 완전히 끝나고 빠져나갈 때 한 번 실행됩니다 (예: 검을 다 휘두르고 거둘 때)
        public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (playerController != null)
            {
                // 애니메이션 길이에 상관없이, 끝나는 순간 정확하게 판정과 락을 풉니다!
                playerController.DisableWeaponHitbox();
                playerController.CompleteAttackState(stateAttackSequence);
            }
        }
    }
}
