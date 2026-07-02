using UnityEngine;

namespace ActionRPG.Data
{
    public enum SkillType { MeleeCombo, ChargeAttack, ShieldBash, DashAttack, AoE, ComboAttack }
    public enum SkillHitShape { Box, Sphere }

    [CreateAssetMenu(fileName = "NewSkillData", menuName = "ActionRPG/Data/SkillData")]
    public class SkillData : ScriptableObject
    {
        [Header("Skill Identity")]
        public string skillName = "New Skill";
        public SkillType skillType;
        public Sprite icon;
        
        [Header("Animation")]
        public string animationTriggerName = "Attack";
        public string chargeAnimationTriggerName = "";
        public string chargeAnimationStateName = "";
        public float animSpeedMultiplier = 1.0f;
        public float chargeAnimationStartNormalizedTime = 0.2f;

        [Header("Combat Metrics")]
        public float damageMultiplier = 1.0f;
        public float cooldown = 3.0f;
        public float range = 2.5f;
        public float aoeRadius = 0f;
        public int maxCharges = 1;

        [Header("Charge Attack Settings")]
        public float chargeTime = 1.0f;
        public float chargePostHitFreezeDuration = 0.6f;
        public float chargeGaugeForwardOffset = 1.5f;
        public float chargeHitForwardOffset = 1.5f;
        public SkillHitShape chargeHitShape = SkillHitShape.Box;
        public Vector3 chargeHitBoxHalfExtents = new Vector3(2.5f, 0.5f, 2.5f);
        public float chargeHitRadius = 2.5f;

        [Header("VFX Settings")]
        public GameObject vfxPrefab;
        public float vfxForwardOffset = 1.5f;
        public float vfxHeightOffset = 1.0f;
        public float vfxScaleMultiplier = 1.0f;

        [Header("Combo Attack VFX Angles")]
        public Vector3 comboVfxStep1EulerOffset = new Vector3(0f, 10f, 0f);
        public Vector3 comboVfxStep2EulerOffset = new Vector3(0f, -10f, -15f);
        public Vector3 comboVfxStep3EulerOffset = new Vector3(0f, 10f, 15f);

        [Header("Charge VFX Settings")]
        public GameObject chargeVfxPrefab;
        public GameObject chargeIndicatorPrefab;

        [Header("Audio Settings")]
        public string skillImpactSoundID = "";
        public string castSoundID = "";
    }
}
