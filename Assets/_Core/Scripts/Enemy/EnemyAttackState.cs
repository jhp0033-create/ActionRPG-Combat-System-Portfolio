using UnityEngine;
using ActionRPG.Core;

namespace ActionRPG.Enemy
{
    public class EnemyAttackState : State
    {
        private EnemyController enemy;
        private float lastAttackTime;

        public EnemyAttackState(StateMachine stateMachine, EnemyController enemy) : base(stateMachine)
        {
            this.enemy = enemy;
        }

        public override void Enter()
        {
            if (enemy.animator != null)
            {
                enemy.animator.SetFloat("Speed", 0f);
            }
            if (enemy.agent != null && enemy.agent.isOnNavMesh)
            {
                enemy.agent.isStopped = true;
                enemy.agent.ResetPath();
                enemy.agent.velocity = Vector3.zero;
                Vector3 direction = (enemy.target.position - enemy.transform.position).normalized;
                direction.y = 0f;
                if (direction != Vector3.zero)
                {
                    enemy.transform.rotation = Quaternion.LookRotation(direction);
                }
            }

            if (Time.time >= lastAttackTime + enemy.attackCooldown)
            {
                PerformAttack();
            }
        }

        public override void Execute()
        {
            if (enemy.target == null) return;

            if (enemy.agent != null && enemy.agent.isOnNavMesh)
            {
                enemy.agent.isStopped = true;
                enemy.agent.velocity = Vector3.zero;
            }

            float distance = Vector3.Distance(enemy.transform.position, enemy.target.position);

            if (distance > enemy.attackRadius * 1.2f)
            {
                stateMachine.ChangeState(enemy.chaseState);
            }
            else
            {
                if (Time.time >= lastAttackTime + enemy.attackCooldown)
                {
                    PerformAttack();
                }
                else
                {
                    Vector3 direction = (enemy.target.position - enemy.transform.position).normalized;
                    direction.y = 0f;
                    if (direction != Vector3.zero)
                    {
                        enemy.transform.rotation = Quaternion.Slerp(enemy.transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
                    }
                }
            }
        }

        private void PerformAttack()
        {
            if (enemy.animator != null)
            {
                enemy.animator.SetTrigger("Attack");
            }

            lastAttackTime = Time.time;
        }
    }
}
