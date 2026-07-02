using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerAutoBattleService : MonoBehaviour
    {
        public PlayerTargetingResolver.TargetResult FindClosestTarget(
            IEnumerable<GameObject> activeEnemies,
            Vector3 origin,
            float radius)
        {
            return PlayerTargetingResolver.FindClosest(activeEnemies, origin, radius);
        }

        public bool ShouldReserveCombo(
            bool isSingleSkillActive,
            bool isSkillComboActive,
            int comboStep,
            bool isAttacking,
            bool canSaveAttack,
            bool saveAttack,
            Transform currentTarget)
        {
            return !isSingleSkillActive
                && !isSkillComboActive
                && comboStep != 3
                && isAttacking
                && canSaveAttack
                && !saveAttack
                && currentTarget != null;
        }
    }
}
