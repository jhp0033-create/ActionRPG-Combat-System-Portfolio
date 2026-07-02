using UnityEngine;
using ActionRPG.Core;

namespace ActionRPG.Enemy
{
    public class EnemyChaseState : State
    {
        private EnemyController enemy;

        public EnemyChaseState(StateMachine stateMachine, EnemyController enemy) : base(stateMachine)
        {
            this.enemy = enemy;
        }

        public override void Enter()
        {
            if (enemy.agent != null && enemy.agent.isOnNavMesh)
            {
                enemy.agent.isStopped = false;
                enemy.agent.speed = enemy.moveSpeed;
                enemy.agent.stoppingDistance = Mathf.Max(0.1f, enemy.attackRadius * 0.75f);
            }
        }

        public override void Execute()
        {
            if (enemy.target == null || enemy.agent == null) return;

            float distance = Vector3.Distance(enemy.transform.position, enemy.target.position);

            // 1. 공격 사거리 안으로 들어왔다면 공격(Attack) 상태로 전이
            if (distance <= enemy.attackRadius)
            {
                stateMachine.ChangeState(enemy.attackState);
                return;
            }

            // 2. 어그로가 풀렸다면 다시 대기(Idle) 상태로 전이
            if (distance > enemy.aggroRadius * 1.5f) // 약간의 여유(hysteresis)를 주어 핑퐁 방지
            {
                stateMachine.ChangeState(enemy.idleState);
                return;
            }

            // 3. 계속 추적 및 애니메이션 업데이트
            if (enemy.agent.isOnNavMesh)
            {
                enemy.agent.stoppingDistance = Mathf.Max(0.1f, enemy.attackRadius * 0.75f);
                enemy.agent.SetDestination(enemy.target.position);
                if (enemy.animator != null)
                {
                    // 이동 속도에 따라 애니메이션 Speed 파라미터 조절 (Walk/Run 블렌딩)
                    enemy.animator.SetFloat("Speed", enemy.agent.velocity.magnitude / enemy.agent.speed);
                }
            }
        }

        public override void Exit()
        {
            if (enemy.agent != null && enemy.agent.isOnNavMesh)
            {
                // 다른 상태로 넘어갈 때(공격 등) 멈춰 세웁니다.
                enemy.agent.isStopped = true;
            }
        }
    }
}
