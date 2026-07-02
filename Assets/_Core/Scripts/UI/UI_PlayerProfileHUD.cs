using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace ActionRPGBattleSystem.UI
{
    /// <summary>
    /// 플레이어의 프로필 영역(가로형 HP 바, 프로필 라디얼 EXP 바, 하단 EXP 슬라이더, 레벨 등)을
    /// 하나의 허브에서 통합 관리하는 클래스입니다.
    /// </summary>
    public class UI_PlayerProfileHUD : MonoBehaviour
    {
        [Header("프로필 HP 게이지")]
        [Tooltip("가로형 HP 게이지 (Filled Image)")]
        [SerializeField] private Image horizontalHpImage;
        [Tooltip("White Shadow Hp")]
        [SerializeField] private Image whiteShadowHpImage;

        [Header("프로필 EXP 게이지 (라디얼)")]
        [Tooltip("원형 EXP 게이지 (Filled Image - Radial360)")]
        [SerializeField] private Image radialExpImage;

        [Header("경험치 (EXP) 슬라이더 (하단)")]
        [Tooltip("하단 EXP 슬라이더 (Slider)")]
        [SerializeField] private Slider horizontalExpSlider;

        [Header("프로필 정보 텍스트")]
        [Tooltip("플레이어 레벨 표시 텍스트")]
        [SerializeField] private TextMeshProUGUI levelText;

        private int displayedLevel = int.MinValue;

        private void Awake()
        {
            if (horizontalHpImage == null) Debug.LogError($"[{gameObject.name}] UI_PlayerProfileHUD: horizontalHpImage(초록색 체력바)가 인스펙터에 할당되지 않았습니다!");
            if (whiteShadowHpImage == null) Debug.LogError($"[{gameObject.name}] UI_PlayerProfileHUD: whiteShadowHpImage(하얀색 잔상 체력바)가 인스펙터에 할당되지 않았습니다!");
            if (radialExpImage == null) Debug.LogError($"[{gameObject.name}] UI_PlayerProfileHUD: radialExpImage가 인스펙터에 할당되지 않았습니다!");

            // 게이지 이미지 타입 Filled 검증 및 보정 (할당된 경우에만)
            if (horizontalHpImage != null) ValidateImageFilled(horizontalHpImage, "HP", Image.FillMethod.Horizontal);
            if (whiteShadowHpImage != null) ValidateImageFilled(whiteShadowHpImage, "WhiteShadow HP", Image.FillMethod.Horizontal);
            if (radialExpImage != null) ValidateImageFilled(radialExpImage, "EXP Radial", Image.FillMethod.Radial360);
        }

        private void ValidateImageFilled(Image image, string gaugeName, Image.FillMethod defaultMethod)
        {
            if (image != null)
            {
                if (image.type != Image.Type.Filled || image.fillMethod != defaultMethod)
                {
                    image.type = Image.Type.Filled;
                    image.fillMethod = defaultMethod;
                    Debug.LogWarning($"[{gameObject.name}] {gaugeName} 이미지 세팅(Type 또는 FillMethod)이 올바르지 않아 자동으로 강제 변환했습니다.");
                }
            }
        }

        /// <summary>
        /// 플레이어 체력 변동 시 호출되어 가로형 HP UI를 갱신합니다.
        /// </summary>
        public void SetHealth(float current, float max)
        {
            if (max <= 0f) return;
            float newRatio = Mathf.Clamp01(current / max);

            if (horizontalHpImage != null)
            {
                horizontalHpImage.DOKill();
                horizontalHpImage.DOFillAmount(newRatio, 0.2f).SetEase(Ease.OutQuad);
            }

            if (whiteShadowHpImage != null)
            {
                whiteShadowHpImage.DOKill();
                if (newRatio < whiteShadowHpImage.fillAmount)
                {
                    whiteShadowHpImage.DOFillAmount(newRatio, 0.4f)
                        .SetDelay(0.4f)
                        .SetEase(Ease.OutQuad);
                }
                else
                {
                    // 회복 시에는 즉각 하얀색 바도 채워줌
                    whiteShadowHpImage.fillAmount = newRatio;
                }
            }
            else
            {
                Debug.LogError($"[{gameObject.name}] UI_PlayerProfileHUD: whiteShadowHpImage(하얀색 잔상 체력바)가 인스펙터에 할당되지 않아 체력바 딜레이 연출이 무시되었습니다!");
            }
        }

        /// <summary>
        /// 플레이어 경험치 변동 시 호출되어 EXP 슬라이더 및 라디얼 EXP 이미지를 갱신합니다.
        /// </summary>
        public void SetExp(float current, float max)
        {
            if (max <= 0f) return;
            float ratio = Mathf.Clamp01(current / max);

            // 1. 하단 경험치 슬라이더 갱신
            if (horizontalExpSlider != null)
            {
                horizontalExpSlider.DOKill();
                if (ratio < horizontalExpSlider.value)
                {
                    horizontalExpSlider.value = ratio;
                }
                else
                {
                    horizontalExpSlider.DOValue(ratio, 0.3f).SetEase(Ease.OutCubic);
                }
            }

            // 2. 프로필 라디얼 경험치 이미지 갱신
            if (radialExpImage != null)
            {
                radialExpImage.DOKill();
                if (ratio < radialExpImage.fillAmount)
                {
                    radialExpImage.fillAmount = ratio;
                }
                else
                {
                    radialExpImage.DOFillAmount(ratio, 0.3f).SetEase(Ease.OutCubic);
                }
            }
        }

        /// <summary>
        /// 플레이어 레벨 변동 시 호출되어 텍스트를 갱신합니다.
        /// </summary>
        public void SetLevel(int level)
        {
            if (levelText != null)
            {
                bool isInitialSet = displayedLevel == int.MinValue;
                bool isSameLevel = displayedLevel == level;

                levelText.text = $"{level}";
                displayedLevel = level;
                
                levelText.transform.DOKill();
                levelText.transform.localScale = Vector3.one;

                if (!isInitialSet && !isSameLevel)
                {
                    levelText.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0.2f), 0.3f, 5, 0.5f);
                }
            }
        }
    }
}
