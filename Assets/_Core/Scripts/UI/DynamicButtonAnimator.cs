using UnityEngine;
using DG.Tweening;
using ActionRPG.Managers;
namespace ActionRPG.UI
{
    /// <summary>
    /// 동적 버튼 밀어내기 연출 (Dynamic Button Push Animation)
    /// 3번째 버튼(동적 버튼)이 나타날 때 화면 밑에서 솟아오르며, 
    /// 기존에 존재하던 버튼들을 좌측으로 물리적으로 튕겨내듯 밀어내는 DOTween 시퀀스 제어기입니다.
    /// </summary>
    public class DynamicButtonAnimator : MonoBehaviour
    {
        [Header("UI 대상 할당")]
        [Tooltip("기존에 우측 정렬로 존재하던 고정 버튼들 (인덱스 0, 1)")]
        public RectTransform[] fixedButtons;
        [Tooltip("활성화 시 밑에서 솟구쳐 올라올 3번째 버튼")]
        public RectTransform dynamicButton;

        [Header("애니메이션 설정")]
        [Tooltip("고정 버튼들을 왼쪽으로 얼마나 밀어낼 것인가? (픽셀 단위, 음수로 설정)")]
        public float pushDistanceX = -120f;
        [Tooltip("동적 버튼이 등장하기 전 숨겨져 있을 Y축 오프셋 위치 (음수) - 파티클이 안 보일 때까지 깊숙이 내림")]
        public float hideOffsetY = -300f;
        [Tooltip("애니메이션 소요 시간")]
        public float animDuration = 0.5f;

        [Header("Editor Preview")]
        [Tooltip("에디터에서 체크박스를 끄고 켜면 애니메이션을 즉시 미리 볼 수 있습니다.")]
        public bool testShowButton = false;
        private bool prevTestState = false;

        // 원본 위치 캐싱용 배열
        private Vector2[] fixedOriginalPos;
        private Vector2 dynamicOriginalPos;

        private Sequence animSequence;
        private bool isInitialized = false;

        private void Awake()
        {
            testShowButton = false;
            prevTestState = false;

            InitializePositions();
        }

        private void Start()
        {
            // 에디터 프리뷰 모드일 경우 애니메이션 생략
            if (testShowButton) return;

            // 씬에 오프닝 디렉터가 있다면, 화면이 밝아지고 연출이 끝날 때까지 대기한 뒤 UI 진입 연출 실행
            var openingDirector = UnityEngine.Object.FindFirstObjectByType<DemoOpeningDirector>();
            if (openingDirector != null)
            {
                if (openingDirector.playDemoIntroSequence)
                {
                    if (openingDirector.isGameplayIntroFinished)
                    {
                        PlayInitialEnterAnimation();
                    }
                    else
                    {
                        openingDirector.OnGameplayIntroFinished.AddListener(() =>
                        {
                            PlayInitialEnterAnimation();
                        });
                    }
                }
                else if (openingDirector.isFinished)
                {
                    PlayInitialEnterAnimation();
                }
                else
                {
                    openingDirector.OnOpeningFinished.AddListener(() => 
                    {
                        PlayInitialEnterAnimation();
                    });
                }
            }
            else
            {
                PlayInitialEnterAnimation();
            }
        }

        /// <summary>
        /// 맨 처음 등장할 때 픽스드 버튼들이 화면 우측 바깥에서 날아오며 투명에서 선명해지는 연출
        /// </summary>
        private void PlayInitialEnterAnimation()
        {
            if (!isInitialized) InitializePositions();

            // 애니메이션 시작 시점에 다이나믹 버튼이 다른 요인으로 인해 켜져있는 것을 방지
            if (dynamicButton != null)
            {
                dynamicButton.anchoredPosition = new Vector2(dynamicOriginalPos.x, dynamicOriginalPos.y + hideOffsetY);
                CanvasGroup dcg = dynamicButton.GetComponent<CanvasGroup>();
                if (dcg != null) dcg.alpha = 0f;
                dynamicButton.gameObject.SetActive(false);
            }
            if (extraPopupUI != null)
            {
                extraPopupUI.gameObject.SetActive(false);
            }

            if (animSequence != null && animSequence.IsActive()) animSequence.Kill();
            animSequence = DOTween.Sequence();

            if (fixedButtons != null)
            {
                for (int i = 0; i < fixedButtons.Length; i++)
                {
                    if (fixedButtons[i] != null)
                    {
                        // 연출 시작 시 활성화
                        fixedButtons[i].gameObject.SetActive(true);

                        // 최종 목표 위치 (3번 자리가 비어있으므로 땡겨진 상태)
                        float targetX = fixedOriginalPos[i].x - pushDistanceX;
                        
                        // 우측 바깥에서 목표 위치로 날아옴
                        animSequence.Insert(0f, fixedButtons[i].DOAnchorPosX(targetX, animDuration).SetEase(Ease.OutBack, 1.2f));

                        // 투명도 페이드 인 (0 -> 1)
                        CanvasGroup cg = fixedButtons[i].GetComponent<CanvasGroup>();
                        if (cg != null)
                        {
                            animSequence.Insert(0f, cg.DOFade(1f, animDuration).SetEase(Ease.InOutSine));
                        }
                    }
                }
            }

            // 고정 버튼이 다 들어오고 나서 살짝 뜸을 들인 뒤, 다이나믹 버튼(네비게이션 버튼) 솟아오르기 자동 실행
            animSequence.AppendInterval(0.2f);
            animSequence.AppendCallback(() => 
            {
                ShowDynamicButton();
            });
        }

        [Header("Auto Navigation UI Integration")]
        [Tooltip("다이나믹 버튼(자동이동)이 사라질 때 그 자리를 채우며 나타날 전용 '중지' 버튼")]
        public RectTransform stopNavButton;

        [Tooltip("다이나믹 버튼과 함께 똑같이 밑에서 위로 솟아오르고 숨겨질 추가 UI가 있다면 여기에 할당하세요.")]
        public RectTransform extraPopupUI;
        private Vector2 extraOriginalPos;

        [Header("Audio Settings")]
        [Tooltip("버튼을 누를 때 재생할 효과음의 Sound ID (예: UI_Click)")]
        public string clickSoundID = "UI_Click";

        private ActionRPG.Player.NetworkPlayerController playerCtrl;
        private Sequence navSwapSequence;

        public bool isUnlocked { get; private set; } = false;

        private void InitializePositions()
        {
            if (isInitialized) return;

            // 고정 버튼들의 최종 완성 위치(켜졌을 때)를 원래 위치로 기억
            if (fixedButtons != null && fixedButtons.Length > 0)
            {
                fixedOriginalPos = new Vector2[fixedButtons.Length];
                for (int i = 0; i < fixedButtons.Length; i++)
                {
                    if (fixedButtons[i] != null)
                    {
                        fixedOriginalPos[i] = fixedButtons[i].anchoredPosition;
                        
                        // 초기 상태: 아예 화면 우측 바깥(예: +400px)으로 밀어두고 투명하게 감춤
                        fixedButtons[i].anchoredPosition = new Vector2(fixedOriginalPos[i].x + 400f, fixedOriginalPos[i].y);
                        
                        // 투명도 제어를 위해 CanvasGroup이 없으면 자동 부착
                        CanvasGroup cg = fixedButtons[i].GetComponent<CanvasGroup>();
                        if (cg == null) cg = fixedButtons[i].gameObject.AddComponent<CanvasGroup>();
                        cg.alpha = 0f;

                        // 완전히 꺼둠
                        fixedButtons[i].gameObject.SetActive(false);
                    }
                }
            }

            // 동적 버튼의 최종 완성 위치를 기억
            if (dynamicButton != null)
            {
                dynamicOriginalPos = dynamicButton.anchoredPosition;
                // 초기 상태: 동적 버튼은 아래로 숨겨둔다
                dynamicButton.anchoredPosition = new Vector2(dynamicOriginalPos.x, dynamicOriginalPos.y + hideOffsetY);
                
                CanvasGroup cg = dynamicButton.GetComponent<CanvasGroup>();
                if (cg == null) cg = dynamicButton.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                dynamicButton.gameObject.SetActive(false);
            }

            // 추가 팝업 UI 위치 및 투명도 세팅
            if (extraPopupUI != null)
            {
                extraOriginalPos = extraPopupUI.anchoredPosition;
                extraPopupUI.anchoredPosition = new Vector2(extraOriginalPos.x, extraOriginalPos.y + hideOffsetY);
                
                CanvasGroup cg = extraPopupUI.GetComponent<CanvasGroup>();
                if (cg == null) cg = extraPopupUI.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;

                extraPopupUI.gameObject.SetActive(false);
            }

            // 중지 버튼도 투명도 제어를 위해 CanvasGroup 세팅
            if (stopNavButton != null)
            {
                CanvasGroup cg = stopNavButton.GetComponent<CanvasGroup>();
                if (cg == null) cg = stopNavButton.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                
                // 투명도만 0으로 만들고 끄지 않아서 투명한 채로 켜져있던 문제 해결
                stopNavButton.gameObject.SetActive(false);
            }

            isInitialized = true;
        }

        private void Update()
        {
            // 인스펙터 프리뷰용
            if (testShowButton != prevTestState)
            {
                prevTestState = testShowButton;
                if (testShowButton)
                    ShowDynamicButton();
                else
                    HideDynamicButton();
            }
        }

        /// <summary>
        /// 동적 버튼이 나타나면서 기존 버튼들을 밀어내는 연출 (해금 기점)
        /// </summary>
        public void ShowDynamicButton()
        {
            isUnlocked = true;

            if (!isInitialized) InitializePositions();
            
            // 시퀀스 초기화
            if (animSequence != null && animSequence.IsActive()) animSequence.Kill();
            animSequence = DOTween.Sequence();

            if (dynamicButton != null)
            {
                dynamicButton.gameObject.SetActive(true);
                
                CanvasGroup cg = dynamicButton.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 0f; animSequence.Insert(0f, cg.DOFade(1f, animDuration).SetEase(Ease.InOutSine)); }

                // 동적 버튼은 투명에서 선명해지며 밑에서 위로 원래 위치를 향해 솟구침 (OutBack 탄성)
                animSequence.Insert(0f, dynamicButton.DOAnchorPosY(dynamicOriginalPos.y, animDuration).SetEase(Ease.OutBack, 1.2f));
            }

            if (extraPopupUI != null)
            {
                extraPopupUI.gameObject.SetActive(true);
                CanvasGroup cg = extraPopupUI.GetComponent<CanvasGroup>();
                if (cg != null) { cg.alpha = 0f; animSequence.Insert(0f, cg.DOFade(1f, animDuration).SetEase(Ease.InOutSine)); }

                animSequence.Insert(0f, extraPopupUI.DOAnchorPosY(extraOriginalPos.y, animDuration).SetEase(Ease.OutBack, 1.2f));
            }

            // 고정 버튼들은 옆(좌측)으로 밀려남 (최종 3개 배치 상태인 원래 위치로 복귀)
            if (fixedButtons != null)
            {
                for (int i = 0; i < fixedButtons.Length; i++)
                {
                    if (fixedButtons[i] != null)
                    {
                        // 에디터에 배치된 원래 위치(왼쪽)로 밀려남
                        animSequence.Insert(0f, fixedButtons[i].DOAnchorPosX(fixedOriginalPos[i].x, animDuration).SetEase(Ease.OutBack, 1.2f));
                    }
                }
            }
        }

        /// <summary>
        /// 동적 버튼이 다시 숨고 기존 버튼들이 원래 자리로 되돌아가는 연출
        /// </summary>
        public void HideDynamicButton()
        {
            if (!isInitialized) InitializePositions();

            // 시퀀스 초기화
            if (animSequence != null && animSequence.IsActive()) animSequence.Kill();
            animSequence = DOTween.Sequence();

            // 고정 버튼들은 우측으로 당겨와서 3번의 빈자리를 채움 (InBack으로 텐션을 주며 빨려들어가듯)
            if (fixedButtons != null)
            {
                for (int i = 0; i < fixedButtons.Length; i++)
                {
                    if (fixedButtons[i] != null)
                    {
                        float closedX = fixedOriginalPos[i].x - pushDistanceX;
                        animSequence.Insert(0f, fixedButtons[i].DOAnchorPosX(closedX, animDuration * 0.8f).SetEase(Ease.InBack));
                    }
                }
            }

            if (dynamicButton != null)
            {
                CanvasGroup cg = dynamicButton.GetComponent<CanvasGroup>();
                if (cg != null) animSequence.Insert(0f, cg.DOFade(0f, animDuration * 0.8f).SetEase(Ease.OutQuad));

                // 동적 버튼은 투명해지며 아래로 다시 꺼짐
                animSequence.Insert(0f, dynamicButton.DOAnchorPosY(dynamicOriginalPos.y + hideOffsetY, animDuration * 0.8f).SetEase(Ease.InBack))
                    .OnComplete(() =>
                    {
                        dynamicButton.gameObject.SetActive(false);
                    });
            }

            if (extraPopupUI != null)
            {
                CanvasGroup cg = extraPopupUI.GetComponent<CanvasGroup>();
                if (cg != null) animSequence.Insert(0f, cg.DOFade(0f, animDuration * 0.8f).SetEase(Ease.OutQuad));

                animSequence.Insert(0f, extraPopupUI.DOAnchorPosY(extraOriginalPos.y + hideOffsetY, animDuration * 0.8f).SetEase(Ease.InBack))
                    .OnComplete(() => extraPopupUI.gameObject.SetActive(false));
            }
        }

        /// <summary>
        /// 다이나믹 버튼(자동이동)과 중지 버튼 둘 다의 OnClick 이벤트에 이 함수를 똑같이 연결해 주시면 됩니다.
        /// (Toggle 방식이므로 어느 쪽을 누르든 자동으로 상태가 반전됩니다)
        /// </summary>
        public void OnDynamicButtonClicked()
        {
            // 터치 효과음 재생 (Data-Driven 방식)
            if (!string.IsNullOrEmpty(clickSoundID) && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySFXByKey(clickSoundID);
            }

            if (playerCtrl == null)
            {
                playerCtrl = UnityEngine.Object.FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
            }

            if (playerCtrl != null)
            {
                playerCtrl.ToggleSpawnAreaNavigation();
                // 이벤트 구독에 의해 OnQuestNavigationChanged가 불리면서 자동으로 교체 연출이 실행됩니다.
            }
        }

        /// <summary>
        /// 자동 이동 상태에 따라 다이나믹 버튼과 중지 버튼이 DOTween으로 자리를 스왑(교대)합니다.
        /// </summary>
        private void UpdateButtonSwap(bool isNavigating)
        {
           
            if (!isInitialized) InitializePositions();
            
            if (navSwapSequence != null && navSwapSequence.IsActive()) navSwapSequence.Kill();
            navSwapSequence = DOTween.Sequence();

            float hideDur = animDuration * 0.6f;
            float showDelay = hideDur;

            if (isNavigating)
            {
                // 1. [자동이동 시작] 다이나믹 버튼이 페이드아웃 되면서 먼저 밑으로 사라짐
                if (dynamicButton != null && dynamicButton.gameObject.activeSelf)
                {
                    CanvasGroup cg = dynamicButton.GetComponent<CanvasGroup>();
                    if (cg != null) navSwapSequence.Insert(0f, cg.DOFade(0f, hideDur).SetEase(Ease.OutQuad));
                    
                    navSwapSequence.Insert(0f, dynamicButton.DOAnchorPosY(dynamicOriginalPos.y + hideOffsetY, hideDur).SetEase(Ease.InBack))
                        .OnComplete(() => dynamicButton.gameObject.SetActive(false));
                }

                // 2. [자동이동 시작] 다이나믹 버튼이 빠지고 '좀 있다가' 중지 버튼이 투명에서 선명해지며 솟아오름
                if (stopNavButton != null)
                {
                    stopNavButton.gameObject.SetActive(true);
                    stopNavButton.SetAsLastSibling();
                    stopNavButton.anchoredPosition = new Vector2(dynamicOriginalPos.x, dynamicOriginalPos.y + hideOffsetY);

                    CanvasGroup cg = stopNavButton.GetComponent<CanvasGroup>();
                    if (cg != null) 
                    {
                        cg.alpha = 0f;
                        navSwapSequence.Insert(showDelay, cg.DOFade(1f, animDuration).SetEase(Ease.InOutSine));
                    }
                    
                    navSwapSequence.Insert(showDelay, stopNavButton.DOAnchorPosY(dynamicOriginalPos.y, animDuration).SetEase(Ease.OutBack, 1.2f));
                }
            }
            else
            {
                // 1. [자동이동 취소] 중지 버튼이 페이드아웃 되면서 먼저 밑으로 사라짐
                if (stopNavButton != null && stopNavButton.gameObject.activeSelf)
                {
                    CanvasGroup cg = stopNavButton.GetComponent<CanvasGroup>();
                    if (cg != null) navSwapSequence.Insert(0f, cg.DOFade(0f, hideDur).SetEase(Ease.OutQuad));
                    
                    navSwapSequence.Insert(0f, stopNavButton.DOAnchorPosY(dynamicOriginalPos.y + hideOffsetY, hideDur).SetEase(Ease.InBack))
                        .OnComplete(() => stopNavButton.gameObject.SetActive(false));
                }

                // 2. [자동이동 취소] 시간차를 두고 다이나믹 버튼이 투명에서 선명해지며 다시 솟아오름
                if (dynamicButton != null// && (playerCtrl == null || !playerCtrl.isAutoMode)
                                         && isUnlocked)
                {
                    CanvasGroup cg = dynamicButton.GetComponent<CanvasGroup>();
                    bool isHidden = !dynamicButton.gameObject.activeSelf || (cg != null && cg.alpha < 0.9f);

                    // 이미 화면에 완전히 떠있는 상태가 아니라면(DOTween Kill로 인해 꼬인 상태 포함) 다시 튀어나오는 연출을 실행합니다.
                    if (isHidden)
                    {
                        dynamicButton.gameObject.SetActive(true);
                        dynamicButton.anchoredPosition = new Vector2(dynamicOriginalPos.x, dynamicOriginalPos.y + hideOffsetY);

                        if (cg != null) 
                        {
                            cg.alpha = 0f;
                            navSwapSequence.Insert(showDelay, cg.DOFade(1f, animDuration).SetEase(Ease.InOutSine));
                        }
                        
                        navSwapSequence.Insert(showDelay, dynamicButton.DOAnchorPosY(dynamicOriginalPos.y, animDuration).SetEase(Ease.OutBack, 1.2f));
                    }
                }
            }
        }

        /// <summary>
        /// 소환(전투) 구역에 도착하거나 자동 전투가 시작되었을 때 네비게이션 버튼들을 완전히 치워줍니다.
        /// </summary>
        private void UpdateAutoModeVisibility(bool isAutoMode)
        {
            if (!isInitialized) return;

            if (navSwapSequence != null && navSwapSequence.IsActive()) navSwapSequence.Kill();
            if (isAutoMode && animSequence != null && animSequence.IsActive()) animSequence.Kill();
            navSwapSequence = DOTween.Sequence();
            
            float hideDur = animDuration * 0.6f;

            if (isAutoMode)
            {
                // 전투 돌입: 화면에 떠있는 다이나믹/중지 버튼 모두 투명해지며 바닥으로 쏙 숨깁니다.
                AppendBattleEntryNavigationExit(navSwapSequence, hideDur);
            }
            else
            {
                // 전투 해제: 현재 자동 이동 중이 아니며, 조이스틱 등으로 UI가 "해금"된 상태에서만 다이나믹 버튼이 페이드인하며 다시 띄워집니다.
                if (playerCtrl != null && !playerCtrl.isQuestNavigating && isUnlocked)
                {
                    if (dynamicButton != null && !dynamicButton.gameObject.activeSelf)
                    {
                        dynamicButton.gameObject.SetActive(true);
                        dynamicButton.anchoredPosition = new Vector2(dynamicOriginalPos.x, dynamicOriginalPos.y + hideOffsetY);
                        
                        CanvasGroup cg = dynamicButton.GetComponent<CanvasGroup>();
                        if (cg != null) navSwapSequence.Insert(0f, cg.DOFade(1f, animDuration).SetEase(Ease.InOutSine));
                        navSwapSequence.Insert(0f, dynamicButton.DOAnchorPosY(dynamicOriginalPos.y, animDuration).SetEase(Ease.OutBack, 1.2f));
                    }
                }
            }
        }

        public void PlayBattleEntryNavigationExit()
        {
            if (!isInitialized) InitializePositions();

            if (animSequence != null && animSequence.IsActive()) animSequence.Kill();
            if (navSwapSequence != null && navSwapSequence.IsActive()) navSwapSequence.Kill();
            navSwapSequence = DOTween.Sequence();

            AppendBattleEntryNavigationExit(navSwapSequence, animDuration * 0.6f);
        }

        private void AppendBattleEntryNavigationExit(Sequence sequence, float hideDur)
        {
            if (sequence == null) return;

            if (fixedButtons != null && fixedOriginalPos != null)
            {
                for (int i = 0; i < fixedButtons.Length; i++)
                {
                    if (fixedButtons[i] == null || i >= fixedOriginalPos.Length) continue;

                    RectTransform fixedButton = fixedButtons[i];
                    fixedButton.gameObject.SetActive(true);
                    float exitX = fixedOriginalPos[i].x + 400f;
                    Tweener exitTween = fixedButton.DOAnchorPosX(exitX, animDuration * 0.8f).SetEase(Ease.InBack);
                    exitTween.OnComplete(() => fixedButton.gameObject.SetActive(false));
                    sequence.Insert(0f, exitTween);

                    CanvasGroup cg = fixedButton.GetComponent<CanvasGroup>();
                    if (cg != null) sequence.Insert(0f, cg.DOFade(0f, hideDur).SetEase(Ease.OutQuad));
                }
            }

            HideNavigationButton(sequence, dynamicButton, dynamicOriginalPos, hideDur);
            HideNavigationButton(sequence, stopNavButton, dynamicOriginalPos, hideDur);
        }

        private void HideNavigationButton(Sequence sequence, RectTransform button, Vector2 originalPosition, float hideDur)
        {
            if (sequence == null || button == null || !button.gameObject.activeSelf) return;

            CanvasGroup cg = button.GetComponent<CanvasGroup>();
            if (cg != null) sequence.Insert(0f, cg.DOFade(0f, hideDur).SetEase(Ease.OutQuad));

            Tweener exitTween = button.DOAnchorPosY(originalPosition.y + hideOffsetY, hideDur).SetEase(Ease.InBack);
            exitTween.OnComplete(() => button.gameObject.SetActive(false));
            sequence.Insert(0f, exitTween);
        }

        private void OnEnable()
        {
            // 플레이어 이벤트를 구독하여, 이동/취소 및 전투 진입 여부에 따라 알아서 버튼이 제어되도록 합니다.
            if (playerCtrl == null) playerCtrl = UnityEngine.Object.FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.OnQuestNavigationChanged += UpdateButtonSwap;
                playerCtrl.OnAutoModeChanged += UpdateAutoModeVisibility;
            }
        }

        private void OnDisable()
        {
            if (animSequence != null) animSequence.Kill();
            if (navSwapSequence != null) navSwapSequence.Kill();
            
            if (playerCtrl != null)
            {
                playerCtrl.OnQuestNavigationChanged -= UpdateButtonSwap;
                playerCtrl.OnAutoModeChanged -= UpdateAutoModeVisibility;
            }
        }
    }
}
