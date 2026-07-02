using UnityEngine;
using ActionRPG.Data;

namespace ActionRPG.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerSkillService : MonoBehaviour
    {
        public bool IsComboAttack(SkillData skill)
        {
            return skill != null && skill.skillType == SkillType.ComboAttack;
        }

        public bool IsSingleSkillActive(int activeSkillIndex, bool isSkillComboActive)
        {
            return activeSkillIndex != -1 && !isSkillComboActive;
        }

        public bool IsBasicOrComboAttack(int activeSkillIndex, SkillData[] equippedSkills)
        {
            if (activeSkillIndex == -1)
            {
                return true;
            }

            return activeSkillIndex >= 0
                && activeSkillIndex < equippedSkills.Length
                && IsComboAttack(equippedSkills[activeSkillIndex]);
        }

        public float GetWeaponDamageMultiplier(int activeSkillIndex, SkillData[] equippedSkills)
        {
            if (activeSkillIndex < 0 || activeSkillIndex >= equippedSkills.Length)
            {
                return 1f;
            }

            SkillData skill = equippedSkills[activeSkillIndex];
            if (!IsComboAttack(skill))
            {
                return 1f;
            }

            return skill.damageMultiplier > 0f ? skill.damageMultiplier : 1f;
        }
    }
}
