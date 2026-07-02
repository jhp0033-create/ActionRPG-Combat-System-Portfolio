using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ActionRPG.Enemy;

namespace ActionRPG.UI
{
    /// <summary>
    /// Screen Space canvas에서 적 이름과 HP 바를 각각 추적하고,
    /// 피격 시 흰색 잔상/청크 연출을 보여주는 월드 UI 체력바입니다.
    /// </summary>
    public class WorldHealthBar : MonoBehaviour
    {
        [Header("UI References")]
        public Image healthFillImage;
        public Image whiteShadowImage;
        public TextMeshProUGUI nameText;
        public Image backgroundImage;

        [Header("Hit Presentation")]
        public float targetScaleMultiplier = 1.3f;
        public float showHpWhenDamagedDuration = 1.3f;

        [Header("UI Containers")]
        public bool useSeparateTracking = false;
        public RectTransform nameContainer;
        public RectTransform hpBarContainer;

        [Header("Tracking Settings - Name")]
        public Transform nameTrackedTarget;
        public float nameHeightOffset = 0.3f;

        [Header("Tracking Settings - HP Bar")]
        public Transform hpBarTrackedTarget;
        public float hpBarHeightOffset = 0.1f;
        public float bubbleAvoidanceOffset = 0.0f;

        [Header("Scale Compensation")]
        public float referenceDistance = 10f;
        public float minScale = 1f;
        public float maxScale = 1f;
        public float maxVisibleDistance = 25f;


        private Camera mainCam;
        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private CanvasGroup canvasGroup;
        private Transform trackedTarget;
        private CharacterController targetCharacterController;
        private Collider targetCollider;
        private Renderer[] targetRenderers;
        private Collider[] targetColliders;
        private EnemyController targetEnemyController;
        private float currentHealth;
        private float currentMaxHealth;
        private bool isInitialized;
        private bool isFirstHealthUpdate = true;
        private float hitPulseMultiplier = 1f;
        private Tween visibilityTween;
        private Tween hitScaleTween;
        private bool hasCachedGraphicOffsets;
        private Vector2 backgroundOffsetFromHp;
        private Vector2 whiteOffsetFromHp;

        private void Awake()
        {
            CacheComponents();
            AutoBindReferences();
        }

        private void OnDisable()
        {
            KillTweens();
        }

        private void CacheComponents()
        {
            mainCam = Camera.main;
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void AutoBindReferences()
        {
            if (healthFillImage == null)
                healthFillImage = FindImage("HP_Gage", "Gage", "Fill");

            if (whiteShadowImage == null)
                whiteShadowImage = FindImage("White", "Shadow");

            if (backgroundImage == null)
                backgroundImage = FindImage("HP_Background", "Background");

            if (nameText == null)
                nameText = GetComponentInChildren<TextMeshProUGUI>(true);

            if (nameContainer == null && nameText != null)
                nameContainer = nameText.GetComponent<RectTransform>();

            if (hpBarContainer == null && healthFillImage != null)
                hpBarContainer = healthFillImage.transform.parent as RectTransform;
        }

        private Image FindImage(params string[] keywords)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (string keyword in keywords)
            {
                foreach (Image image in images)
                {
                    if (image != null && image.name.Contains(keyword))
                        return image;
                }
            }

            return null;
        }

        public void Initialize(Transform target, string enemyName, float maxHealth)
        {
            CacheComponents();
            AutoBindReferences();

            trackedTarget = target;
            nameTrackedTarget = target;
            hpBarTrackedTarget = target;
            targetCharacterController = target != null ? target.GetComponentInChildren<CharacterController>() : null;
            targetCollider = target != null ? target.GetComponentInChildren<Collider>() : null;
            targetRenderers = target != null ? target.GetComponentsInChildren<Renderer>(true) : null;
            targetColliders = target != null ? target.GetComponentsInChildren<Collider>(true) : null;
            targetEnemyController = ResolveEnemyController(target);
            CacheGraphicOffsets();

            currentMaxHealth = Mathf.Max(1f, maxHealth);
            currentHealth = currentMaxHealth;
            isInitialized = true;
            isFirstHealthUpdate = false;
            hitPulseMultiplier = 1f;

            if (nameText != null)
                nameText.text = enemyName;

            if (healthFillImage != null)
            {
                healthFillImage.DOKill();
                healthFillImage.fillAmount = 1f;
                healthFillImage.enabled = true;
            }

            if (whiteShadowImage != null)
            {
                whiteShadowImage.DOKill();
                whiteShadowImage.fillAmount = 1f;
                whiteShadowImage.color = new Color(1f, 1f, 1f, 0.9f);
                whiteShadowImage.enabled = true;
            }

            RefreshTargetVisibility(0f);
            gameObject.SetActive(true);
        }

        private void LateUpdate()
        {
            if (!isInitialized || trackedTarget == null)
                return;

            if (mainCam == null)
                mainCam = Camera.main;
            if (mainCam == null || rectTransform == null)
                return;

            if (rootCanvas == null)
                rootCanvas = GetComponentInParent<Canvas>();
            if (rootCanvas == null)
                return;

            if (!CanShowForTarget())
            {
                SetGraphicEnabled(false);
                SetVisible(false, 0f);
                return;
            }

            float distance = Vector3.Distance(mainCam.transform.position, trackedTarget.position);
            bool shouldShow = distance <= maxVisibleDistance && IsInFrontOfCamera(trackedTarget.position);
            SetGraphicEnabled(shouldShow);
            if (!shouldShow)
                return;

            Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && uiCamera == null)
                uiCamera = mainCam;

            float scaleValue = referenceDistance / Mathf.Max(distance, 0.1f);
            scaleValue = Mathf.Clamp(scaleValue, minScale, maxScale);
            Vector3 targetScale = Vector3.one * (scaleValue * hitPulseMultiplier);

            if (useSeparateTracking && nameContainer != null && hpBarContainer != null)
            {
                UpdateContainerPosition(nameContainer, GetNameWorldPosition(), targetScale, uiCamera);
                UpdateContainerPosition(hpBarContainer, GetHpWorldPosition(), targetScale, uiCamera);

                SyncHpGraphicTransform(backgroundImage, backgroundOffsetFromHp);
                SyncHpGraphicTransform(whiteShadowImage, whiteOffsetFromHp);
            }
            else
            {
                UpdateContainerPosition(rectTransform, GetHpWorldPosition(), targetScale, uiCamera);
            }
        }

        private bool IsInFrontOfCamera(Vector3 worldPosition)
        {
            return mainCam.WorldToScreenPoint(worldPosition).z > 0f;
        }

        public void RefreshTargetVisibility(float duration)
        {
            bool visible = CanShowForTarget();
            SetGraphicEnabled(visible);
            SetVisible(visible, duration);
        }

        private bool CanShowForTarget()
        {
            if (!isInitialized || trackedTarget == null)
                return false;

            return targetEnemyController == null || !targetEnemyController.IsSpawning;
        }

        private EnemyController ResolveEnemyController(Transform target)
        {
            if (target == null)
                return null;

            EnemyController controller = target.GetComponent<EnemyController>();
            if (controller != null)
                return controller;

            controller = target.GetComponentInParent<EnemyController>();
            if (controller != null)
                return controller;

            return target.GetComponentInChildren<EnemyController>();
        }

        private void UpdateContainerPosition(RectTransform container, Vector3 worldPosition, Vector3 targetScale, Camera uiCamera)
        {
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPosition);
            if (screenPos.z <= 0f)
                return;

            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out Vector2 localPoint))
            {
                container.anchoredPosition = localPoint;
            }

            container.localScale = Vector3.Lerp(container.localScale, targetScale, Time.deltaTime * 12f);
        }

        private void CacheGraphicOffsets()
        {
            if (hpBarContainer == null)
                return;

            if (backgroundImage != null && backgroundImage.rectTransform != hpBarContainer)
                backgroundOffsetFromHp = backgroundImage.rectTransform.anchoredPosition - hpBarContainer.anchoredPosition;

            if (whiteShadowImage != null && whiteShadowImage.rectTransform != hpBarContainer)
                whiteOffsetFromHp = whiteShadowImage.rectTransform.anchoredPosition - hpBarContainer.anchoredPosition;

            hasCachedGraphicOffsets = true;
        }

        private void SyncHpGraphicTransform(Image image, Vector2 offsetFromHp)
        {
            if (image == null || hpBarContainer == null)
                return;

            RectTransform imageRect = image.rectTransform;
            if (imageRect == hpBarContainer)
                return;

            if (!hasCachedGraphicOffsets)
                CacheGraphicOffsets();

            imageRect.anchoredPosition = hpBarContainer.anchoredPosition + offsetFromHp;
            imageRect.localScale = hpBarContainer.localScale;
        }

        private Vector3 GetNameWorldPosition()
        {
            Transform target = nameTrackedTarget != null ? nameTrackedTarget : trackedTarget;
            float alertBump = 0f;
            if (FloatingUIManager.Instance != null && target != null)
                alertBump = FloatingUIManager.Instance.GetAlertBumpHeight(target);

            float bubbleOffset = alertBump > 0f ? bubbleAvoidanceOffset + alertBump : 0f;
            return target.position + Vector3.up * (GetVisualHeight() + nameHeightOffset + bubbleOffset);
        }

        private Vector3 GetHpWorldPosition()
        {
            Transform target = hpBarTrackedTarget != null ? hpBarTrackedTarget : trackedTarget;
            float alertBump = 0f;
            if (FloatingUIManager.Instance != null && target != null)
                alertBump = FloatingUIManager.Instance.GetAlertBumpHeight(target);

            float bubbleOffset = alertBump > 0f ? bubbleAvoidanceOffset + alertBump : 0f;
            return target.position + Vector3.up * (GetVisualHeight() + hpBarHeightOffset + bubbleOffset);
        }

        private float GetVisualHeight()
        {
            if (targetCharacterController != null && targetCharacterController.enabled)
                return targetCharacterController.center.y + targetCharacterController.height * 0.5f;

            if (TryGetRenderableHeight(out float renderableHeight))
                return renderableHeight;

            if (TryGetColliderHeight(out float colliderHeight))
                return colliderHeight;

            if (targetCollider != null && targetCollider.enabled)
                return Mathf.Max(0.5f, targetCollider.bounds.max.y - trackedTarget.position.y);

            return 2.2f;
        }

        private bool TryGetRenderableHeight(out float height)
        {
            height = 0f;
            if (targetRenderers == null || trackedTarget == null)
                return false;

            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Renderer renderer in targetRenderers)
            {
                if (renderer == null || !renderer.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds)
                return false;

            height = Mathf.Max(0.5f, bounds.max.y - trackedTarget.position.y);
            return true;
        }

        private bool TryGetColliderHeight(out float height)
        {
            height = 0f;
            if (targetColliders == null || trackedTarget == null)
                return false;

            bool hasBounds = false;
            Bounds bounds = default;
            foreach (Collider collider in targetColliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            if (!hasBounds)
                return false;

            height = Mathf.Max(0.5f, bounds.max.y - trackedTarget.position.y);
            return true;
        }

        public void UpdateHealth(float current, float max)
        {
            if (healthFillImage == null || max <= 0f)
                return;

            float oldRatio = currentMaxHealth > 0f ? currentHealth / currentMaxHealth : 1f;
            currentMaxHealth = Mathf.Max(1f, max);
            currentHealth = Mathf.Clamp(current, 0f, currentMaxHealth);
            float newRatio = Mathf.Clamp01(currentHealth / currentMaxHealth);

            if (!CanShowForTarget())
            {
                SetGraphicEnabled(false);
                SetVisible(false, 0f);
                return;
            }

            UpdateHealthUI(oldRatio, newRatio);
        }

        private void UpdateHealthUI(float oldRatio, float newRatio)
        {
            healthFillImage.DOKill();
            if (whiteShadowImage != null)
                whiteShadowImage.DOKill();

            SetVisible(true, 0.08f);

            if (isFirstHealthUpdate)
            {
                isFirstHealthUpdate = false;
                healthFillImage.fillAmount = newRatio;
                if (whiteShadowImage != null)
                    whiteShadowImage.fillAmount = newRatio;
                return;
            }

            healthFillImage.DOFillAmount(newRatio, 0.14f).SetEase(Ease.OutCubic);

            if (newRatio < oldRatio)
            {
                PlayHitPulse();

                if (whiteShadowImage != null)
                {
                    whiteShadowImage.fillAmount = oldRatio;
                    whiteShadowImage.DOFillAmount(newRatio, 0.42f)
                        .SetDelay(0.28f)
                        .SetEase(Ease.OutQuad);
                }

                CreateDamageChunk(oldRatio, newRatio);
            }
            else if (newRatio > oldRatio && whiteShadowImage != null)
            {
                whiteShadowImage.fillAmount = newRatio;
            }

            visibilityTween?.Kill();
            visibilityTween = DOVirtual.DelayedCall(showHpWhenDamagedDuration, () => SetVisible(true, 0.2f));
        }


        private void PlayHitPulse()
        {
            hitScaleTween?.Kill();
            hitPulseMultiplier = 1f;
            hitScaleTween = DOTween.Sequence()
                .Append(DOTween.To(() => hitPulseMultiplier, value => hitPulseMultiplier = value, targetScaleMultiplier, 0.08f).SetEase(Ease.OutCubic))
                .Append(DOTween.To(() => hitPulseMultiplier, value => hitPulseMultiplier = value, 1f, 0.18f).SetEase(Ease.OutBack));
        }

        private void CreateDamageChunk(float oldRatio, float newRatio)
        {
            if (healthFillImage == null || oldRatio <= newRatio)
                return;

            Image chunkImg = null;
            if (FloatingUIManager.Instance != null)
                chunkImg = FloatingUIManager.Instance.GetDamageChunk(healthFillImage.transform);

            if (chunkImg == null)
            {
                GameObject chunkObj = new GameObject("DamageChunk", typeof(RectTransform), typeof(Image));
                chunkObj.transform.SetParent(healthFillImage.transform, false);
                chunkImg = chunkObj.GetComponent<Image>();
            }

            chunkImg.gameObject.SetActive(true);
            chunkImg.transform.SetAsLastSibling();
            chunkImg.sprite = null;
            chunkImg.color = new Color(1f, 1f, 1f, 0.85f);
            chunkImg.type = Image.Type.Simple;

            RectTransform chunkRect = chunkImg.rectTransform;
            chunkRect.anchorMin = new Vector2(newRatio, 0.08f);
            chunkRect.anchorMax = new Vector2(oldRatio, 0.92f);
            chunkRect.offsetMin = Vector2.zero;
            chunkRect.offsetMax = Vector2.zero;
            chunkRect.pivot = new Vector2(0.5f, 0.5f);
            chunkRect.localScale = Vector3.one;
            chunkRect.DOKill();
            chunkImg.DOKill();

            Sequence sequence = DOTween.Sequence();
            sequence.Append(chunkRect.DOScale(new Vector3(1.05f, 1.8f, 1f), 0.08f).SetEase(Ease.OutBack));
            sequence.AppendInterval(0.08f);
            sequence.Append(chunkRect.DOScale(new Vector3(1f, 0.1f, 1f), 0.16f).SetEase(Ease.InCubic));
            sequence.Join(chunkImg.DOFade(0f, 0.16f).SetEase(Ease.InCubic));
            sequence.OnComplete(() =>
            {
                if (chunkImg == null)
                    return;

                if (FloatingUIManager.Instance != null)
                    FloatingUIManager.Instance.ReturnDamageChunk(chunkImg);
                else
                    Destroy(chunkImg.gameObject);
            });
        }

        private void SetVisible(bool visible, float duration)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.DOKill();
            if (duration <= 0f)
                canvasGroup.alpha = visible ? 1f : 0f;
            else
                canvasGroup.DOFade(visible ? 1f : 0f, duration);
        }

        private void SetGraphicEnabled(bool enabled)
        {
            if (healthFillImage != null) healthFillImage.enabled = enabled;
            if (whiteShadowImage != null) whiteShadowImage.enabled = enabled;
            if (backgroundImage != null) backgroundImage.enabled = enabled;
            if (nameText != null) nameText.enabled = enabled;
        }

        private void KillTweens()
        {
            visibilityTween?.Kill();
            hitScaleTween?.Kill();
            if (healthFillImage != null) healthFillImage.DOKill();
            if (whiteShadowImage != null) whiteShadowImage.DOKill();
            if (canvasGroup != null) canvasGroup.DOKill();
        }
    }
}
