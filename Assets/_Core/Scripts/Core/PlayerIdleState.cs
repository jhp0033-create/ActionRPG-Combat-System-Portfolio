using UnityEngine;

namespace ActionRPG.Core
{
    /// <summary>
    /// 플레이어의 기본 상태 (대기 및 이동).
    /// 이 상태일 때만 조이스틱/WASD 입력을 통한 자유 이동이 허용됩니다.
    /// </summary>
    public class PlayerIdleState : State
    {
        public PlayerIdleState(StateMachine stateMachine, MonoBehaviour controller, Animator animator) : base(stateMachine)
        {
        }

        public override void Enter()
        {
        }

        public override void Execute()
        {
        }

        public override void Exit()
        {
        }
    }
}
