using TMPro;
using UnityEngine;
using DG.Tweening;

namespace ActionRPG.UI
{
    /// <summary>
    /// 데미지 숫자와 상태 문구를 분리해서 보여주는 월드/스크린 UI 데미지 팝업입니다.
    /// FloatingUIManager의 풀링 대상이며, 기존 DamageTextManager API도 유지합니다.
    /// </summary>
    public class DamageText : MonoBehaviour
    {
        public TextMeshProUGUI textMesh;
        public TextMeshProUGUI statusTextMesh;

        [Header("Animation Settings")]
        public float popHeight = 120f;
        public float floatHeight = 30f;
        public float popStartScale = 5f;
        public float popDuration = 0.5f;
        public float hangDuration = 0.5f;
        public float fadeDuration = 0.5f;
        public Ease popMoveEase = Ease.OutExpo;
        public Ease popFadeEase = Ease.Linear;
        public Ease popScaleEase = Ease.OutBack;
        public Ease hideFadeEase = Ease.InCubic;

        [Header("Tracking")]
        public float bubbleAvoidanceOffset = 0.5f;
        public float heightOffset = 2f;

        [Header("Stacking")]
        public bool isLargeText = false;
        public float normalBumpHeight = 40f;
        public float largeBumpHeight = 80f;

        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private CanvasGroup canvasGroup;
        private Camera mainCam;
        private Transform trackedTarget;
        private Vector3 worldPosition;
        private Vector2 bumpOffset;
        private Vector2 animationOffset;
        private Vector3 localOffset;
        private Sequence activeSequence;

        private void Awake()
        {
            CacheComponents();
        }

        private void OnDisable()
        {
            activeSequence?.Kill();
        }

        private void LateUpdate()
        {
            if (!gameObject.activeInHierarchy)
                return;

            if (trackedTarget != null)
            {
                float alertBump = FloatingUIManager.Instance != null ? FloatingUIManager.Instance.GetAlertBumpHeight(trackedTarget) : 0f;
                worldPosition = trackedTarget.position + localOffset + Vector3.up * (bubbleAvoidanceOffset + alertBump);
            }

            UpdateScreenPosition();
        }

        private void CacheComponents()
        {
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
            mainCam = Camera.main;

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (textMesh == null)
                textMesh = transform.Find("Text")?.GetComponent<TextMeshProUGUI>() ?? GetComponentInChildren<TextMeshProUGUI>(true);

            if (statusTextMesh == null)
                statusTextMesh = transform.Find("Info")?.GetComponent<TextMeshProUGUI>();
        }

        public void Initialize(Vector3 spawnPosition, float damageAmount)
        {
            Initialize(null, spawnPosition, Mathf.RoundToInt(damageAmount).ToString(), Color.white, string.Empty);
        }

        public void Initialize(Transform target, Vector3 spawnPosition, string textContent, Color textColor, string statusText = "")
        {
            CacheComponents();

            trackedTarget = target;
            if (target != null)
            {
                // spawnPosition(정수리) + heightOffset(인스펙터에서 설정한 추가 높이)를 로컬 오프셋으로 저장
                localOffset = (spawnPosition - target.position) + Vector3.up * heightOffset;
                worldPosition = target.position + localOffset + Vector3.up * bubbleAvoidanceOffset;
            }
            else
            {
                worldPosition = spawnPosition + Vector3.up * heightOffset;
            }
            bumpOffset = Vector2.zero;
            animationOffset = Vector2.zero;
            isLargeText = !string.IsNullOrEmpty(statusText) && (statusText.Contains("피니시") || statusText.Contains("치명"));

            if (textMesh != null)
            {
                textMesh.text = textContent;
                textMesh.color = Color.white;
                textMesh.alpha = 1f;
                textMesh.transform.localScale = Vector3.one;
            }

            if (statusTextMesh != null)
            {
                bool hasStatus = !string.IsNullOrEmpty(statusText);
                statusTextMesh.gameObject.SetActive(hasStatus);
                statusTextMesh.text = statusText;
                statusTextMesh.color = textColor;
                statusTextMesh.alpha = hasStatus ? 1f : 0f;
                statusTextMesh.transform.localScale = Vector3.one;
            }

            UpdateScreenPosition();
            PlayPopup();
        }

        public void BumpUp(float amount)
        {
            bumpOffset += Vector2.up * amount;
        }

        private void UpdateScreenPosition()
        {
            if (rectTransform == null)
                return;

            if (mainCam == null)
                mainCam = Camera.main;

            if (rootCanvas == null)
                rootCanvas = GetComponentInParent<Canvas>();

            if (mainCam == null || rootCanvas == null)
                return;

            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPosition);
            if (screenPos.z <= 0f)
                return;

            Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && uiCamera == null)
                uiCamera = mainCam;

            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out Vector2 localPoint))
            {
                // 말풍선 알림과 겹치지 않도록 데미지 텍스트 위치를 보정합니다.
                float alertBump = 0f;
                if (trackedTarget != null && FloatingUIManager.Instance != null && FloatingUIManager.Instance.IsBubbleActive(trackedTarget))
                {
                    float bumpHeight = FloatingUIManager.Instance.GetAlertBumpHeight(trackedTarget);
                    
                    // 월드 높이 보정값을 스크린 UI 좌표계에 맞춰 변환합니다.
                    alertBump = bumpHeight * 150f; 
                }

                rectTransform.anchoredPosition = localPoint + bumpOffset + animationOffset + (Vector2.up * alertBump);
            }
        }

        private void PlayPopup()
        {
            activeSequence?.Kill();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.DOKill();
            }

            rectTransform.localScale = Vector3.one * Mathf.Max(0.1f, popStartScale);

            animationOffset = Vector2.zero;
            Vector2 popPos = Vector2.up * popHeight;
            Vector2 endPos = popPos + Vector2.up * floatHeight;

            activeSequence = DOTween.Sequence();
            activeSequence.Append(DOTween.To(() => animationOffset, value => animationOffset = value, popPos, popDuration).SetEase(popMoveEase));
            activeSequence.Join(rectTransform.DOScale(Vector3.one, popDuration).SetEase(popScaleEase));
            activeSequence.AppendInterval(hangDuration);
            activeSequence.Append(DOTween.To(() => animationOffset, value => animationOffset = value, endPos, fadeDuration).SetEase(popFadeEase));

            if (canvasGroup != null)
                activeSequence.Join(canvasGroup.DOFade(0f, fadeDuration).SetEase(hideFadeEase));

            if (textMesh != null)
                activeSequence.Join(textMesh.DOFade(0f, fadeDuration).SetEase(hideFadeEase));

            if (statusTextMesh != null && statusTextMesh.gameObject.activeSelf)
                activeSequence.Join(statusTextMesh.DOFade(0f, fadeDuration).SetEase(hideFadeEase));

            activeSequence.OnComplete(() => gameObject.SetActive(false));
        }
    }
}
