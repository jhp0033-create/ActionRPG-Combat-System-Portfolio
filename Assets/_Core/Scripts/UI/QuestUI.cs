using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ActionRPG.Core;
using System.Collections;

namespace ActionRPG.UI
{
    /// <summary>
    /// QuestManager의 이벤트를 수신해 퀘스트 텍스트, 레이아웃, 등장 연출을 갱신합니다.
    /// </summary>
    public class QuestUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("퀘스트 타이틀 (예: 알비 던전)")]
        public TextMeshProUGUI questTitleText;
        
        [Tooltip("퀘스트 내용 (예: 가이드 이동하기)")]
        public TextMeshProUGUI questDescText;
        
        [Tooltip("Content Size Fitter가 달린 최상위 퀘스트 배경 RectTransform (강제 갱신용)")]
        public RectTransform questBackgroundRect;

        [Header("DOTween Animation Settings")]
        public CanvasGroup canvasGroup;
        public float animDuration = 0.6f;
        public Vector2 startOffset = new Vector2(100f, 0f); // 등장 전 우측으로 100픽셀 밀려있음
        private Vector2 originalPosition;
        private Coroutine layoutRefreshRoutine;

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
                
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 초기 상태: 투명하고 우측에 위치하며 크기가 작음
            originalPosition = questBackgroundRect.anchoredPosition;
            canvasGroup.alpha = 0f;
            questBackgroundRect.anchoredPosition = originalPosition + startOffset;
            questBackgroundRect.localScale = new Vector3(0.5f, 0.5f, 1f); // 0.5배 크기로 시작
        }

        private void Start()
        {
            // 1. 매니저가 존재한다면 퀘스트 갱신 이벤트를 구독(Subscribe)합니다.
            if (Core.QuestManager.Instance != null)
            {
                Core.QuestManager.Instance.OnQuestUpdated += RefreshQuestUI;
            }
            else
            {
                Debug.LogWarning("[QuestUI] 씬에 QuestManager가 존재하지 않아 이벤트를 구독할 수 없습니다.");
            }
        }

        private void OnDestroy()
        {
            // 2. 오브젝트가 파괴될 때는 메모리 누수 방지를 위해 이벤트 구독을 해제합니다.
            if (Core.QuestManager.Instance != null)
            {
                Core.QuestManager.Instance.OnQuestUpdated -= RefreshQuestUI;
            }
        }

        /// <summary>
        /// QuestManager에서 이벤트를 발송할 때마다 자동으로 실행되는 갱신 함수입니다.
        /// </summary>
        private void RefreshQuestUI(QuestData newQuestData)
        {
            if (newQuestData == null || questBackgroundRect == null) return;

            // 1. 텍스트 교체 (고정 텍스트 + 동적 진행도 텍스트 합체)
            if (questTitleText != null) questTitleText.text = newQuestData.questTitle;
            if (questDescText != null)
            {
                string progressText = Core.QuestManager.Instance.GetQuestProgressText();
                questDescText.text = newQuestData.questDescription + progressText;
            }

            RefreshLayoutNow();
            if (layoutRefreshRoutine != null)
                StopCoroutine(layoutRefreshRoutine);
            layoutRefreshRoutine = StartCoroutine(RefreshLayoutNextFrame());
            
            // 3. DOTween 애니메이션 연출
            if (canvasGroup.alpha <= 0.1f)
            {
                // 완전 숨김 상태에서 등장: 페이드 인 + 슬라이드 인 + 크기 커짐
                canvasGroup.DOFade(1f, animDuration).SetEase(Ease.OutQuad);
                questBackgroundRect.DOAnchorPos(originalPosition, animDuration).SetEase(Ease.OutBack);
                questBackgroundRect.DOScale(1f, animDuration).SetEase(Ease.OutBack);
            }
            else
            {
                // 이미 화면에 떠있는 상태에서 갱신(사냥 진행도 오름 등): 통통 튀는 피드백만 제공
                questBackgroundRect.DOKill(true); // 기존 애니메이션 리셋
                questBackgroundRect.localScale = new Vector3(1.1f, 1.1f, 1f); // 살짝 커졌다가
                questBackgroundRect.DOScale(1f, 0.3f).SetEase(Ease.OutBack); // 1배로 돌아옴
            }
        }

        private void RefreshLayoutNow()
        {
            ResizeTextToPreferredWidth(questTitleText);
            ResizeTextToPreferredWidth(questDescText);

            Canvas.ForceUpdateCanvases();
            ForceRebuildLayoutChain(questBackgroundRect);
            Canvas.ForceUpdateCanvases();
        }

        private void ResizeTextToPreferredWidth(TextMeshProUGUI text)
        {
            if (text == null) return;

            text.ForceMeshUpdate();
            RectTransform textRect = text.rectTransform;
            Vector2 preferred = text.GetPreferredValues(text.text);
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Ceil(preferred.x));
            textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(preferred.y));
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);
        }

        private IEnumerator RefreshLayoutNextFrame()
        {
            yield return null;
            RefreshLayoutNow();
            layoutRefreshRoutine = null;
        }

        private void ForceRebuildLayoutChain(RectTransform startRect)
        {
            RectTransform current = startRect;
            while (current != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(current);
                current = current.parent as RectTransform;
            }
        }
    }
}
