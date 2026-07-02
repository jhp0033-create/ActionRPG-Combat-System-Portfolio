using UnityEngine;
using DG.Tweening;

namespace ActionRPG.UI
{
    /// <summary>
    /// 적발견 느낌표(!), 퀘스트 물음표(?) 등 머리 위에 통통 튀어오르는 팝업 UI 스크립트입니다.
    /// WorldHealthBar처럼 UI Canvas 안에 생성되어 적의 3D 월드 좌표를 추적(Tracking)합니다.
    /// </summary>
    public class AlertBubble : MonoBehaviour
    {
        [Header("Tracking Settings")]
        [Tooltip("팝업이 머리 위로 얼마나 높이 뜰지 오프셋")]
        public float heightOffset = 2.5f;
        
        [Header("Animation Settings")]
        public float popDuration = 0.4f;
        public float hideDuration = 0.2f;
        [Tooltip("팝업이 완전히 등장한 후 스스로 사라지기까지 대기하는 시간 (0이면 자동 숨김 안 함)")]
        public float autoHideDuration = 1.5f;
        public Ease popEase = Ease.OutBack;
        
        [Header("Floating Settings")]
        public bool enableFloating = true;
        public float floatDistance = 20f; // UI 픽셀 단위 둥둥거리기
        public float floatDuration = 1f;

        private Transform trackedTarget;
        private Camera mainCam;
        private RectTransform rectTransform;
        private Canvas rootCanvas;
        
        private Vector3 originalScale;
        private Vector2 originalAnchoredPos;
        private Tween floatTween;
        private CanvasGroup canvasGroup;
        private bool isTracking = false;

        private void Awake()
        {
            mainCam = Camera.main;
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();
            
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 에디터에서 설정한 프리팹의 원본 스케일을 그대로 보존합니다.
            originalScale = transform.localScale;
            
            // 처음엔 숨김
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 느낌표를 특정 대상을 추적하며 띄웁니다.
        /// </summary>
        /// <param name="target">추적할 대상 (주로 몬스터의 Head 본이나 Transform)</param>
        public void ShowAlert(Transform target)
        {
            if (target == null) return;
            

            trackedTarget = target;
            isTracking = true;
            gameObject.SetActive(true);
            
            // 트윈 초기화
            transform.DOKill();
            floatTween?.Kill();
            
            transform.localScale = Vector3.zero;

            // 1. 크기가 0에서 원본 크기로 통통 튀며(OutBack) 등장
            transform.DOScale(originalScale, popDuration).SetEase(popEase).OnComplete(() =>
            {
                // 2. 등장이 끝나면 위아래로 둥둥 떠다니는 로컬 애니메이션 시작
                if (enableFloating && rectTransform != null)
                {
                    // 로컬 Y축을 기준으로 약간씩 오르락내리락
                    floatTween = rectTransform.DOBlendableLocalMoveBy(new Vector3(0, floatDistance, 0), floatDuration)
                        .SetLoops(-1, LoopType.Yoyo)
                        .SetEase(Ease.InOutSine);
                }

                // 3. 지정된 시간이 지나면 자동으로 사라지도록 예약
                if (autoHideDuration > 0)
                {
                    DOVirtual.DelayedCall(autoHideDuration, () =>
                    {
                        if (isTracking) HideAlert();
                    }, false).SetId(this); // id를 this로 묶어서 나중에 일괄 취소 가능하게 함
                }
            });
        }

        /// <summary>
        /// 느낌표를 숨깁니다.
        /// </summary>
        public void HideAlert()
        {
            isTracking = false;
            DOTween.Kill(this); // DelayedCall 취소
            transform.DOKill();
            floatTween?.Kill();
            
            // 크기가 다시 0으로 쪼그라들면서(InBack) 사라진 후 비활성화 및 풀 반납
            transform.DOScale(Vector3.zero, hideDuration).SetEase(Ease.InBack).OnComplete(() =>
            {
                gameObject.SetActive(false);
                if (FloatingUIManager.Instance != null)
                {
                    FloatingUIManager.Instance.ReturnBubble(this, trackedTarget);
                }
            });
        }

        private void LateUpdate()
        {
            if (!isTracking || trackedTarget == null || mainCam == null || rootCanvas == null) return;

            // 3D 월드 좌표 -> 2D 스크린 좌표로 변환
            Vector3 worldPos = trackedTarget.position + Vector3.up * heightOffset;
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

            // 카메라 뒤에 있거나 화면 밖(좌우상하)으로 나가면 투명하게 숨김 처리
            bool isOffScreen = screenPos.z < 0f || 
                               screenPos.x < 0f || screenPos.x > Screen.width || 
                               screenPos.y < 0f || screenPos.y > Screen.height;
            
            if (canvasGroup != null)
            {
                // 부드럽게 끄고 켤 수도 있지만, 느낌표 특성상 즉각적으로 끄고 켬
                canvasGroup.alpha = isOffScreen ? 0f : 1f;
            }

            if (isOffScreen) return; // 화면 밖이면 위치 업데이트 스킵 (어차피 안 보임)

            Camera uiCamera = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : (rootCanvas.worldCamera != null ? rootCanvas.worldCamera : mainCam);

            // WorldHealthBar/DamageText와 같은 방식으로 Canvas 로컬 좌표를 사용합니다.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rootCanvas.transform as RectTransform,
                    screenPos,
                    uiCamera,
                    out Vector2 localPoint))
            {
                rectTransform.anchoredPosition = localPoint;
            }
        }

        private void OnDisable()
        {
            transform.DOKill();
            floatTween?.Kill();
            isTracking = false;
        }
        
        private void OnDestroy()
        {
            transform.DOKill();
            floatTween?.Kill();
        }
    }
}
