using UnityEngine;
using DG.Tweening;

namespace ActionRPG.UI
{
    /// <summary>
    /// 모바일 터치 매니저의 입력값을 받아, 화면의 하얀 동그라미(Handle)를 
    /// 백그라운드 원형 영역을 벗어나지 않게 시각적으로 움직여주는 UI 스크립트입니다.
    /// </summary>
    public class VirtualJoystickUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("큰 원형 백그라운드 렉트 트랜스폼")]
        public RectTransform backgroundRect;
        [Tooltip("작은 하얀 동그라미 렉트 트랜스폼")]
        public RectTransform handleRect;
        [Tooltip("핸들의 이동 방향을 가리킬 화살표 렉트 트랜스폼")]
        public RectTransform arrowRect;

        [Header("Settings")]
        [Tooltip("하얀 동그라미가 뻗어나갈 수 있는 최대 거리 (백그라운드 반지름)")]
        public float maxRadius = 150f;
        [Tooltip("화살표 이미지의 원본 각도를 보정합니다. (현재 180도 뒤집힘 세팅)")]
        public float arrowAngleOffset = 90f;

        private Vector2 originalHandlePos;
        private Vector3 originalScale = Vector3.one;

        private CanvasGroup canvasGroup;

        private void Awake()
        {
            if (handleRect != null)
            {
                originalHandlePos = handleRect.anchoredPosition;
            }

            // 초기 프레임에 노출되지 않도록 캔버스 그룹을 먼저 숨깁니다.
            if (backgroundRect != null)
            {
                originalScale = backgroundRect.localScale;

                canvasGroup = backgroundRect.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = backgroundRect.gameObject.AddComponent<CanvasGroup>();
                
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void SetJoystickVisible(bool isVisible)
        {
            if (canvasGroup == null) return;
            targetAlpha = isVisible ? 1f : 0f;
            
            // 상호작용은 즉시 활성화/비활성화
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;

            if (backgroundRect != null)
            {
                backgroundRect.DOKill();
                if (isVisible)
                {
                    // 커지면서 나타나는 팝업 연출 (원래 스케일의 60%에서 100%로)
                    backgroundRect.localScale = originalScale * 0.6f;
                    backgroundRect.DOScale(originalScale, 0.3f).SetEase(Ease.OutBack);
                }
                else
                {
                    // 작아지면서 사라지는 연출 (원래 스케일의 80%로)
                    backgroundRect.DOScale(originalScale * 0.8f, 0.2f).SetEase(Ease.InQuad);
                }
            }
        }

        private float targetAlpha = 0f;
        public float fadeSpeed = 3f; // 1초 / 0.5초 = 2f

        private void Update()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
            }

            if (Player.MobileTouchManager.Instance == null) return;

            bool isTouching = Player.MobileTouchManager.Instance.IsLeftTouching;
            Vector2 touchStartPos = Player.MobileTouchManager.Instance.LeftTouchStartPos;

            // 터치 중일 때만 조이스틱 UI 활성화
            if (isTouching)
            {
                // 왼쪽 영역을 일정 시간 이상 누르면 드래그 여부와 관계없이 조이스틱을 표시합니다.
                if (Time.time - Player.MobileTouchManager.Instance.LeftTouchStartTime >= 0.15f)
                {
                    // 아직 타겟 알파가 0(숨김 상태)일 때 위치를 잡고 보이게 만듭니다.
                    if (canvasGroup != null && targetAlpha == 0f)
                    {
                        // 스크린 터치 좌표를 캔버스 로컬 좌표계로 변환합니다.
                        RectTransform parentRect = backgroundRect.parent.GetComponent<RectTransform>();
                        
                        // Screen Space - Camera 캔버스에서는 UI 카메라를 함께 전달합니다.
                        Canvas parentCanvas = backgroundRect.GetComponentInParent<Canvas>();
                        Camera uiCamera = (parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? parentCanvas.worldCamera : null;

                        Vector2 localPos;
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, touchStartPos, uiCamera, out localPos))
                        {
                            backgroundRect.anchoredPosition = localPos;
                        }
                        SetJoystickVisible(true);
                    }
                }

                // 현재 진짜 화면 터치 좌표 구하기
                Vector2 currentTouchPos = touchStartPos;
                if (UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0)
                {
                    foreach (var t in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
                    {
                        if (t.screenPosition.x < Screen.width / 2f)
                        {
                            currentTouchPos = t.screenPosition;
                            break;
                        }
                    }
                }
                else if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed)
                {
                    currentTouchPos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                }

                Canvas bgCanvas = backgroundRect.GetComponentInParent<Canvas>();
                Camera bgCamera = (bgCanvas != null && bgCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? bgCanvas.worldCamera : null;

                Vector2 backgroundLocalPos;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(backgroundRect, currentTouchPos, bgCamera, out backgroundLocalPos))
                {
                    // 배경 스케일링 중에도 화면상 최대 거리가 유지되도록 제한 반경을 보정합니다.
                    float currentScaleRatio = backgroundRect.localScale.x / originalScale.x;
                    if (currentScaleRatio <= 0.01f) currentScaleRatio = 0.01f;
                    float dynamicMaxRadius = maxRadius / currentScaleRatio;
                    
                    handleRect.anchoredPosition = originalHandlePos + Vector2.ClampMagnitude(backgroundLocalPos, dynamicMaxRadius);

                    // 2. 화살표(방향 지시기) 회전 로직
                    if (arrowRect != null)
                    {
                        if (backgroundLocalPos.sqrMagnitude > 0)
                        {
                            arrowRect.gameObject.SetActive(true);
                            float angle = Mathf.Atan2(backgroundLocalPos.y, backgroundLocalPos.x) * Mathf.Rad2Deg;
                            arrowRect.localRotation = Quaternion.Euler(0, 0, angle + arrowAngleOffset);
                        }
                        else
                        {
                            arrowRect.gameObject.SetActive(false);
                        }
                    }
                }
                else
                {
                    // 터치 중인데 움직임이 없을 때만 리셋
                    handleRect.anchoredPosition = originalHandlePos;
                    if (arrowRect != null) arrowRect.gameObject.SetActive(false);
                }
            }
            else
            {
                // 입력이 끝나면 조이스틱을 페이드아웃합니다.
                // 핸들과 화살표 방향은 사라지는 동안(알파값이 0이 될 때까지) 유지합니다.
                if (targetAlpha > 0f)
                {
                    SetJoystickVisible(false);
                }
            }
        }
    }
}
