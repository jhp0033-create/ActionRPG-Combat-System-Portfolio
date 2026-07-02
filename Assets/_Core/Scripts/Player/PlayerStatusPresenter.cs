using ActionRPG.UI;
using ActionRPGBattleSystem.UI;
using UnityEngine;

namespace ActionRPG.Player
{
    /// <summary>
    /// Keeps player status UI lookup and presentation out of the main controller flow.
    /// </summary>
    public static class PlayerStatusPresenter
    {
        public static PlayerWorldBar ResolveWorldBar(PlayerWorldBar assignedBar)
        {
            if (assignedBar != null)
            {
                return assignedBar;
            }

            PlayerWorldBar bar = Object.FindFirstObjectByType<PlayerWorldBar>();
            if (bar != null)
            {
                return bar;
            }

            PlayerWorldBar[] allBars = Resources.FindObjectsOfTypeAll<PlayerWorldBar>();
            foreach (PlayerWorldBar candidate in allBars)
            {
                if (candidate.gameObject.scene.name != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        public static UI_PlayerProfileHUD ResolveProfileHud(UI_PlayerProfileHUD assignedHud)
        {
            return assignedHud != null
                ? assignedHud
                : Object.FindFirstObjectByType<UI_PlayerProfileHUD>();
        }

        public static void Initialize(
            PlayerWorldBar worldBar,
            UI_PlayerProfileHUD profileHud,
            float currentHealth,
            float maxHealth,
            float currentExp,
            float maxExp,
            int currentLevel)
        {
            SetHealth(worldBar, profileHud, currentHealth, maxHealth);
            SetExperience(profileHud, currentExp, maxExp);
            SetLevel(profileHud, currentLevel);
        }

        public static void SetHealth(
            PlayerWorldBar worldBar,
            UI_PlayerProfileHUD profileHud,
            float currentHealth,
            float maxHealth)
        {
            if (worldBar != null)
            {
                worldBar.gameObject.SetActive(true);
                worldBar.SetHealth(currentHealth, maxHealth);
            }

            if (profileHud != null)
            {
                profileHud.SetHealth(currentHealth, maxHealth);
            }
        }

        public static void SetExperience(UI_PlayerProfileHUD profileHud, float currentExp, float maxExp)
        {
            if (profileHud != null)
            {
                profileHud.SetExp(currentExp, maxExp);
            }
        }

        public static void SetLevel(UI_PlayerProfileHUD profileHud, int currentLevel)
        {
            if (profileHud != null)
            {
                profileHud.SetLevel(currentLevel);
            }
        }

        public static void ShowDamage(Transform owner, float damageAmount)
        {
            if (FloatingUIManager.Instance == null)
            {
                return;
            }

            FloatingUIManager.Instance.SpawnDamageText(
                owner,
                owner.position,
                Mathf.RoundToInt(damageAmount).ToString(),
                Color.red);
        }

        public static void ShowLevelUp(Transform owner)
        {
            if (FloatingUIManager.Instance == null)
            {
                return;
            }

            FloatingUIManager.Instance.SpawnDamageText(
                owner,
                owner.position + Vector3.up * 2.5f,
                "LEVEL UP!",
                Color.cyan);
        }
    }
}
