using ActionRPG.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    public sealed class RewardItemView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private CanvasGroup canvasGroup;

        public CanvasGroup CanvasGroup
        {
            get
            {
                if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
                return canvasGroup;
            }
        }

        public RectTransform RectTransform => (RectTransform)transform;

        public void StopIntroParticles()
        {
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                if (particle == null) continue;
                particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particle.Clear(true);
            }
        }

        public void PlayIntroParticles()
        {
            ParticleSystem[] particles = GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                if (particle == null) continue;
                particle.Clear(true);
                particle.Play(true);
            }
        }

        public void Setup(RewardItemData rewardItem)
        {
            if (rewardItem == null) return;

            Setup(rewardItem.icon, rewardItem.rewardTitle, rewardItem.rewardDescription);
        }

        public void Setup(Sprite icon, string title, string description)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (titleText != null) titleText.text = title;
            if (descriptionText != null) descriptionText.text = description;
        }
    }
}
