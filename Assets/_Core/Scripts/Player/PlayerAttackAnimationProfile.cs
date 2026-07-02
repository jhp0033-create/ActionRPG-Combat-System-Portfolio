namespace ActionRPG.Player
{
    public static class PlayerAttackAnimationProfile
    {
        private static readonly float[] BasicComboSpeeds = { 2.5f, 2.8f, 3.5f };
        private static readonly float[] SkillComboSpeeds = { 2.9f, 3.5f, 4.5f };

        public static float GetBasicComboSpeed(int comboStep)
        {
            int index = UnityEngine.Mathf.Clamp(comboStep - 1, 0, BasicComboSpeeds.Length - 1);
            return BasicComboSpeeds[index];
        }

        public static float GetSkillComboSpeed(int attackIndex, float speedScale)
        {
            int index = UnityEngine.Mathf.Clamp(attackIndex, 0, SkillComboSpeeds.Length - 1);
            float safeScale = speedScale > 0f ? speedScale : 1f;
            return SkillComboSpeeds[index] * safeScale;
        }
    }
}
