namespace ActionRPG.Player
{
    public sealed class PlayerProgressionState
    {
        private const float RequiredExpGrowth = 1.2f;

        public int Gold { get; private set; }
        public int Level { get; private set; } = 1;
        public float Exp { get; private set; }
        public float RequiredExp { get; private set; } = 100f;

        public bool AddGold(int amount)
        {
            if (amount <= 0)
            {
                return false;
            }

            Gold += amount;
            return true;
        }

        public int AddExp(float amount)
        {
            if (amount <= 0f)
            {
                return 0;
            }

            Exp += amount;
            int levelUps = 0;

            while (Exp >= RequiredExp)
            {
                Exp -= RequiredExp;
                Level++;
                levelUps++;
                RequiredExp = UnityEngine.Mathf.Round(RequiredExp * RequiredExpGrowth);
            }

            return levelUps;
        }
    }
}
