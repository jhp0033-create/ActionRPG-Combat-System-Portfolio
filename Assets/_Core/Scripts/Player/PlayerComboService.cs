using UnityEngine;

namespace ActionRPG.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerComboService : MonoBehaviour
    {
        public bool IsWithinPostComboDelay(float currentTime, float comboEndTime, float comboDelay)
        {
            return currentTime < comboEndTime + comboDelay;
        }

        public int GetNextBasicComboStep(int currentComboStep)
        {
            int nextStep = currentComboStep + 1;
            return nextStep > 3 ? 1 : nextStep;
        }

        public int GetAttackIndex(int comboStep)
        {
            return Mathf.Clamp(comboStep - 1, 0, 2);
        }

        public bool CanConsumeSavedBasicAttack(bool isSkillComboActive, bool saveAttack, int comboStep)
        {
            return !isSkillComboActive && saveAttack && comboStep != 3;
        }
    }
}
