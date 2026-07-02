using UnityEngine;
using ActionRPG.Managers;

namespace ActionRPG.Enemy
{
    /// <summary>
    /// 적군(몬스터)의 공격 애니메이션 상태에 부착되어,
    /// 특정 프레임(예: 무기를 휘두르는 정점)에서 실제 데미지 판정을 트리거합니다.
    /// </summary>
    public class EnemyAttackBehaviour : StateMachineBehaviour
    {
        private EnemyController enemy;
        private bool hasAttacked = false;

        public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (enemy == null)
            {
                enemy = animator.GetComponentInParent<EnemyController>();
            }
            hasAttacked = false;

            // [사운드 연동] 몬스터가 무기를 휘두르기 시작하는 초반에 스윙 사운드 재생
            if (SoundManager.Instance != null && enemy != null && !enemy.IsDead)
            {
                SoundManager.Instance.PlayMonsterSwingSFX();
            }
        }

        public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            if (enemy == null || enemy.IsDead) return;

            float progress = stateInfo.normalizedTime % 1f;

            // 애니메이션 진행률 40% 지점에서 타격 판정 실행
            if (progress >= 0.4f && !hasAttacked)
            {
                enemy.PerformAttackDamage();
                hasAttacked = true;
            }
        }
    }
}
