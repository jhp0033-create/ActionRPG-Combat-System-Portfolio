using UnityEngine;
using UnityEngine.AI;
using ActionRPG.Core;

namespace ActionRPG.Enemy
{
    public class EnemyIdleState : State
    {
        private EnemyController enemy;
        private float roamTimer;
        private float nextRoamTime;

        public EnemyIdleState(StateMachine stateMachine, EnemyController enemy) : base(stateMachine)
        {
            this.enemy = enemy;
        }

        public override void Enter()
        {
            roamTimer = 0f;
            SetRandomRoamTime();
        }

        public override void Execute()
        {
            // 1. 타겟(플레이어) 추적 감지 센서 작동
            if (enemy.target != null)
            {
                float distance = Vector3.Distance(enemy.transform.position, enemy.target.position);
                if (distance <= enemy.aggroRadius)
                {
                    enemy.ShowAlertBubble();
                    stateMachine.ChangeState(enemy.chaseState);
                    return;
                }
            }

            // 2. 평시 배회(Roam) 로직
            if (enemy.agent == null || !enemy.agent.isOnNavMesh) return;

            // 목적지에 도착했거나, 아직 움직이지 않고 있다면 타이머 가동
            if (enemy.agent.remainingDistance <= enemy.agent.stoppingDistance)
            {
                enemy.agent.isStopped = true;
                if (enemy.animator != null) enemy.animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);

                roamTimer += Time.deltaTime;
                if (roamTimer >= nextRoamTime)
                {
                    FindNewRoamTarget();
                    roamTimer = 0f;
                    SetRandomRoamTime();
                }
            }
            else
            {
                // 걷는 중
                enemy.agent.isStopped = false;
                if (enemy.animator != null)
                {
                    // 배회할 때는 전력질주(1.0)가 아니라 살살 걷도록(0.5) 애니메이션 블렌딩
                    float walkSpeed = (enemy.agent.velocity.magnitude / enemy.moveSpeed) * 0.5f;
                    enemy.animator.SetFloat("Speed", walkSpeed, 0.1f, Time.deltaTime);
                }
            }
        }

        private void SetRandomRoamTime()
        {
            // 2초에서 5초 사이로 무작위 대기 시간 설정 (자연스러운 몬스터 생태계 연출)
            nextRoamTime = Random.Range(2f, 5f);
        }

        private void FindNewRoamTarget()
        {
            // 스폰된 최초 위치(고향)를 기준으로 반경 N미터 내의 랜덤한 좌표를 도출
            Vector3 randomDirection = Random.insideUnitSphere * enemy.roamRadius;
            randomDirection += enemy.spawnOrigin;

            // 유니티 네비게이션 엔진을 통해 걷기 가능한 진짜 지형(NavMesh) 좌표로 보정
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, enemy.roamRadius, 1))
            {
                enemy.agent.SetDestination(hit.position);
                // 배회 속도는 추적 속도보다 느리게 설정
                enemy.agent.speed = enemy.moveSpeed * 0.5f; 
            }
        }
    }
}
