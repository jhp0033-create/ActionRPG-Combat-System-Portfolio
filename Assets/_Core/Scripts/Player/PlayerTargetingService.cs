using ActionRPG.Core;
using ActionRPG.Enemy;
using UnityEngine;

namespace ActionRPG.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerTargetingService : MonoBehaviour
    {
        public PlayerTargetingResolver.TargetResult FindClosestTarget(Vector3 origin, float radius)
        {
            if (EnemyManager.Instance == null)
            {
                return default;
            }

            return PlayerTargetingResolver.FindClosest(EnemyManager.Instance.ActiveEnemies, origin, radius);
        }

        public PlayerTargetingResolver.TargetResult FindHorizontalNeighbor(
            Vector3 origin,
            float radius,
            Transform currentTarget,
            bool toLeft,
            Camera camera)
        {
            if (EnemyManager.Instance == null || EnemyManager.Instance.ActiveEnemies.Count == 0)
            {
                return default;
            }

            return PlayerTargetingResolver.FindHorizontalNeighbor(
                EnemyManager.Instance.ActiveEnemies,
                origin,
                radius,
                currentTarget,
                toLeft,
                camera);
        }

        public PlayerTargetingResolver.TargetResult ResolveExplicitTarget(Transform target)
        {
            if (target == null)
            {
                return default;
            }

            return new PlayerTargetingResolver.TargetResult(
                target,
                target.GetComponent<EnemyController>());
        }

        public void ShowAutoTargetLog(Transform target)
        {
            if (target == null || ActionRPG.UI.UI_SystemLogManager.Instance == null)
            {
                return;
            }

            ActionRPG.UI.UI_SystemLogManager.Instance.ShowLog($"자동 전투: <color=#00FF00>{target.name}</color> 타겟팅!");
        }

        public void ShowTargetSwitchLog(Transform target, bool toLeft)
        {
            if (target == null || ActionRPG.UI.UI_SystemLogManager.Instance == null)
            {
                return;
            }

            string direction = toLeft ? "왼쪽" : "오른쪽";
            ActionRPG.UI.UI_SystemLogManager.Instance.ShowLog($"타겟 변경 ({direction}): <color=#00FF00>{target.name}</color> 타겟팅!");
        }
    }
}
