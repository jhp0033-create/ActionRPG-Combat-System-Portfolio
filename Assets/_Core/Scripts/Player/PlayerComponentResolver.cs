using UnityEngine;
using UnityEngine.AI;

namespace ActionRPG.Player
{
    /// <summary>
    /// Resolves player prefab dependencies without burying lookup logic in the controller.
    /// </summary>
    public static class PlayerComponentResolver
    {
        public static void ResolveRequiredComponents(
            MonoBehaviour owner,
            ref CharacterController controller,
            ref NavMeshAgent navAgent,
            ref Animator animator,
            ref WeaponManager weaponManager)
        {
            controller = Resolve(owner, controller);
            navAgent = Resolve(owner, navAgent);
            animator = Resolve(owner, animator);
            weaponManager = Resolve(owner, weaponManager);
        }

        public static void ConfigureManualNavigation(NavMeshAgent navAgent, float moveSpeed)
        {
            if (navAgent == null)
            {
                return;
            }

            navAgent.speed = moveSpeed;
            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
            navAgent.enabled = false;
        }

        private static T Resolve<T>(MonoBehaviour owner, T assignedComponent) where T : Component
        {
            if (assignedComponent != null)
            {
                return assignedComponent;
            }

            T component = owner.GetComponent<T>();
            if (component != null)
            {
                return component;
            }

            component = owner.GetComponentInChildren<T>();
            return component != null ? component : owner.GetComponentInParent<T>();
        }
    }
}
