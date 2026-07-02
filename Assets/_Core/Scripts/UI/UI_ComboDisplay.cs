using UnityEngine;
using TMPro;
using DG.Tweening;
using ActionRPG.Managers;

namespace ActionRPG.UI
{
    /// <summary>
    /// ComboManager의 이벤트를 구독하여 화면에 콤보 숫자를 화려하게 표시합니다.
    /// PlayerWorldBar 하위나 ScreenSpace 캔버스 어디든 부착하여 사용할 수 있습니다.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class UI_ComboDisplay : MonoBehaviour
    {
        private TextMeshProUGUI comboText;
        private CanvasGroup canvasGroup;
        
        [Header("Animation Settings")]
        public float punchScaleAmount = 0.5f;
        public float punchDuration = 0.2f;
        public float fadeOutDuration = 0.5f;

        private void Awake()
        {
            comboText = GetComponent<TextMeshProUGUI>();
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            
            // 시작 시에는 숨김
            canvasGroup.alpha = 0f;
            comboText.text = "";
        }

        private void Start()
        {
            if (ComboManager.Instance != null)
            {
                ComboManager.Instance.OnComboChanged += HandleComboChanged;
                ComboManager.Instance.OnComboReset += HandleComboReset;
            }
            else
            {

            }
        }

        private void OnDestroy()
        {
            if (ComboManager.Instance != null)
            {
                ComboManager.Instance.OnComboChanged -= HandleComboChanged;
                ComboManager.Instance.OnComboReset -= HandleComboReset;
            }
        }

        private void HandleComboChanged(int combo)
        {
            // 원본 레퍼런스 스타일 텍스트
            comboText.text = $"{combo} 콤보";
            
            // 페이드 아웃 중이었다면 강제 취소하고 즉시 보이게 함
            DOTween.Kill(canvasGroup);
            canvasGroup.alpha = 1f;
            
            // 기존 펀치 애니메이션이 있다면 취소하고 새로 시작 (찰진 타격감)
            transform.DOKill(true);
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * punchScaleAmount, punchDuration, 5, 0.5f);
        }

        private void HandleComboReset()
        {
            // 스르륵 사라지는 연출
            canvasGroup.DOFade(0f, fadeOutDuration).OnComplete(() => 
            {
                comboText.text = "";
            });
        }
    }
}
