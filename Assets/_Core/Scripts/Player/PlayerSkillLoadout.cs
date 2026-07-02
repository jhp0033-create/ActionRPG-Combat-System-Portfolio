using ActionRPG.Data;
using UnityEngine;

namespace ActionRPG.Player
{
    /// <summary>
    /// Owns runtime skill slot initialization and charge recovery rules.
    /// </summary>
    public static class PlayerSkillLoadout
    {
        public const int SlotCount = 6;

        private static readonly string[] DefaultSkillResourcePaths =
        {
            "SkillData/Skill_001",
            "SkillData/Skill_002",
            "SkillData/Skill_003"
        };

        public static void EnsureSlotCount(ref SkillData[] equippedSkills)
        {
            if (equippedSkills == null)
            {
                equippedSkills = new SkillData[SlotCount];
                return;
            }

            if (equippedSkills.Length != SlotCount)
            {
                System.Array.Resize(ref equippedSkills, SlotCount);
            }
        }

        public static void EnsureDefaultSkills(SkillData[] equippedSkills)
        {
            if (equippedSkills == null)
            {
                return;
            }

            for (int i = 0; i < DefaultSkillResourcePaths.Length && i < equippedSkills.Length; i++)
            {
                if (equippedSkills[i] != null)
                {
                    continue;
                }

                SkillData skill = Resources.Load<SkillData>(DefaultSkillResourcePaths[i]);
                if (skill != null)
                {
                    equippedSkills[i] = skill;
                }
                else
                {
                    Debug.LogWarning($"[PlayerSkillLoadout] 기본 스킬 데이터를 찾지 못했습니다: {DefaultSkillResourcePaths[i]}");
                }
            }
        }

        public static void FillCharges(SkillData[] equippedSkills, int[] skillCharges)
        {
            if (equippedSkills == null || skillCharges == null)
            {
                return;
            }

            int count = Mathf.Min(equippedSkills.Length, skillCharges.Length);
            for (int i = 0; i < count; i++)
            {
                skillCharges[i] = equippedSkills[i] != null ? equippedSkills[i].maxCharges : 0;
            }
        }

        public static void TickCooldowns(
            SkillData[] equippedSkills,
            int[] skillCharges,
            float[] cooldownTimers,
            float deltaTime)
        {
            if (equippedSkills == null || skillCharges == null || cooldownTimers == null)
            {
                return;
            }

            int count = Mathf.Min(equippedSkills.Length, skillCharges.Length, cooldownTimers.Length);
            for (int i = 0; i < count; i++)
            {
                SkillData skill = equippedSkills[i];
                if (skill == null || skillCharges[i] >= skill.maxCharges)
                {
                    continue;
                }

                cooldownTimers[i] -= deltaTime;
                if (cooldownTimers[i] > 0f)
                {
                    continue;
                }

                skillCharges[i]++;
                cooldownTimers[i] = skillCharges[i] < skill.maxCharges ? skill.cooldown : 0f;
            }
        }
    }
}
