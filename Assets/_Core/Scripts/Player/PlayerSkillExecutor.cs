using ActionRPG.Data;
using UnityEngine;

namespace ActionRPG.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerSkillExecutor : MonoBehaviour
    {
        private SkillData[] equippedSkills;
        private float[] cooldownTimers;
        private int[] charges;

        public void Initialize(ref SkillData[] skills, float[] skillCooldownTimers, int[] skillCharges)
        {
            equippedSkills = skills;
            cooldownTimers = skillCooldownTimers;
            charges = skillCharges;

            PlayerSkillLoadout.EnsureSlotCount(ref equippedSkills);
            skills = equippedSkills;
            PlayerSkillLoadout.EnsureDefaultSkills(equippedSkills);
            PlayerSkillLoadout.FillCharges(equippedSkills, charges);
        }

        public void TickCooldowns(float deltaTime)
        {
            PlayerSkillLoadout.TickCooldowns(equippedSkills, charges, cooldownTimers, deltaTime);
        }

        public bool CanBeginCast(int index)
        {
            return IsValidSkillIndex(index) && charges[index] > 0;
        }

        public SkillData GetSkill(int index)
        {
            return IsValidSkillIndex(index) ? equippedSkills[index] : null;
        }

        public void ConsumeChargeAndStartCooldown(int index)
        {
            SkillData skill = GetSkill(index);
            if (skill == null || charges[index] <= 0)
            {
                return;
            }

            charges[index]--;

            if (charges[index] == skill.maxCharges - 1)
            {
                cooldownTimers[index] = skill.cooldown;
            }
        }

        private bool IsValidSkillIndex(int index)
        {
            return equippedSkills != null
                && charges != null
                && cooldownTimers != null
                && index >= 0
                && index < equippedSkills.Length
                && index < charges.Length
                && index < cooldownTimers.Length
                && equippedSkills[index] != null;
        }
    }
}
