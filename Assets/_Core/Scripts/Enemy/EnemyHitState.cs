using UnityEngine;
using ActionRPG.Core;

namespace ActionRPG.Enemy
{
    public class EnemyHitState : State
    {
        private EnemyController enemy;
        private float hitDuration = 0.5f; // 피격 모션(경직)이 유지되는 시간
        private float timer = 0f;

        public EnemyHitState(StateMachine stateMachine, EnemyController enemy) : base(stateMachine)
        {
            this.enemy = enemy;
        }

        // 외부(TakeDamage)에서 상태를 바꿀 때 타격 방향을 주입받는 용도의 초기화 메서드
        public void SetKnockback(Vector3 attackerPos, float force = 4f)
        {
        }

        public override void Enter()
        {
            timer = 0f;

            // 네비게이션 길찾기를 잠시 멈춥니다.
            if (enemy.agent != null && enemy.agent.isOnNavMesh)
            {
                enemy.agent.isStopped = true;
                enemy.agent.ResetPath();
                enemy.agent.velocity = Vector3.zero;
            }

            // 피격 모션 재생
            if (enemy.animator != null)
            {
                // Hit 파라미터가 있다면 피격 모션이 나오고, 없어도 잠깐 멈칫하게 됩니다.
                enemy.animator.SetTrigger("Hit");
                enemy.animator.SetFloat("Speed", 0f);
            }
        }

        public override void Execute()
        {
            timer += Time.deltaTime;

            if (enemy.agent != null && enemy.agent.isOnNavMesh)
            {
                enemy.agent.isStopped = true;
                enemy.agent.velocity = Vector3.zero;
            }

            // 경직 시간이 끝나면 다시 쫓아가거나(Chase), 체력이 없으면 죽음 대기(나중에 확장 가능)
            if (timer >= hitDuration)
            {
                if (enemy.target != null && !enemy.IsDead)
                {
                    stateMachine.ChangeState(enemy.chaseState);
                }
                else
                {
                    stateMachine.ChangeState(enemy.idleState);
                }
            }
        }

        public override void Exit()
        {
            // 넉백 종료 후 완전히 브레이크
            if (enemy.agent != null && enemy.agent.isOnNavMesh)
            {
                enemy.agent.velocity = Vector3.zero;
            }
        }
    }
}
