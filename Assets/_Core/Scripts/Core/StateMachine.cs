using UnityEngine;

namespace ActionRPG.Core
{
    public abstract class State
    {
        protected StateMachine stateMachine;
        public State(StateMachine stateMachine) { this.stateMachine = stateMachine; }
        public virtual void Enter() { }
        public virtual void Execute() { }
        public virtual void PhysicsExecute() { }
        public virtual void Exit() { }
    }

    public class StateMachine
    {
        public State CurrentState { get; private set; }

        public void Initialize(State startingState)
        {
            CurrentState = startingState;
            CurrentState?.Enter();
        }

        public void ChangeState(State newState)
        {
            if (CurrentState == newState) return;
            CurrentState?.Exit();
            CurrentState = newState;
            CurrentState?.Enter();
        }

        public void Update() { CurrentState?.Execute(); }
        public void FixedUpdate() { CurrentState?.PhysicsExecute(); }
    }
}
