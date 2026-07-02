using ActionRPG.Data;
using ActionRPG.Managers;
using DG.Tweening;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    [DisallowMultipleComponent]
    public sealed class QuestCompletePopupPresenter : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private CanvasGroup rootCanvasGroup;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool deactivateOnOutroComplete = false;

        [Header("Dim Overlay")]
        [Tooltip("화면 전체를 덮는 암전 전용 Image입니다.")]
        [SerializeField] private CanvasGroup dimCanvasGroup;
        [SerializeField] private Image dimImage;
        [SerializeField, Range(0f, 1f)] private float dimMaxAlpha = 1f;
        [SerializeField] private float dimDarkenDuration = 0.25f;
        [SerializeField] private float dimHoldDuration = 0.15f;
        [SerializeField] private float dimBrightenDuration = 0.35f;

        [Header("Reveal Camera")]
        [SerializeField] private Camera uiCamera;
        [SerializeField] private bool enableUiCameraPostProcessingOnReveal = true;

        [Header("Audio")]
        [SerializeField] private string questCompleteSoundID = "QuestClear";
        [SerializeField] private bool fadeOutBgmOnIntro = true;
        [SerializeField, Range(0f, 1f)] private float bgmFadeOutTargetRatio = 0f;
        [SerializeField] private float bgmFadeOutDuration = 0.7f;

        [Header("Center Stage")]
        [SerializeField] private RectTransform centerStage;
        [SerializeField] private CanvasGroup centerStageCanvasGroup;
        [SerializeField] private TextMeshProUGUI questCompleteText;
        [SerializeField] private GameObject centerVisualObject;
        [SerializeField] private float centerParticleLeadDelay = 0.18f;
        [SerializeField] private float centerContentFadeDuration = 0.3f;

        [Header("Center Text Squeeze")]
        [Tooltip("텍스트가 처음 나올 때 가로(X)로 줄어드는 목표 스케일입니다.")]
        [SerializeField, Range(0.1f, 1f)] private float textSqueezeScaleX = 0.89f;
        [SerializeField] private float textSqueezeDuration = 0.35f;

        [Header("Bottom Right Group")]
        [SerializeField] private RectTransform bottomRightGroup;
        [SerializeField] private Button actionButton;

        [Header("Exit")]
        [SerializeField] private bool quitApplicationOnActionButton = true;

        [Header("Content Timing (밝아지는 시점 기준)")]
        [SerializeField] private float centerHoldDuration = 0.3f;
        [SerializeField] private float centerMoveDuration = 0.65f;
        [SerializeField] private float bottomGroupDelay = 0.25f;
        [SerializeField] private float bottomGroupDuration = 0.55f;

        [Header("Outro Content Timing")]
        [SerializeField] private float contentExitDuration = 0.3f;

        [Header("Position Offsets")]
        [Tooltip("CenterStage가 등장 후 위로 이동할 거리입니다.")]
        [SerializeField] private float centerMoveUpDistance = 95f;
        [Tooltip("BottomRightGroup이 숨겨질 때 오프셋입니다.")]
        [SerializeField] private float bottomHiddenOffsetY = -180f;
        [SerializeField] private Vector3 centerStartScale = new Vector3(1.08f, 1.08f, 1f);
        [SerializeField] private Vector3 centerEndScale = new Vector3(0.88f, 0.88f, 1f);

        [Header("Quill Pen Motion")]
        [Tooltip("우측으로 서서히 이동하면서 각도가 까딱거리는 장식용 깃털펜입니다.")]
        [SerializeField] private RectTransform quillPenTransform;
        [SerializeField] private CanvasGroup quillPenCanvasGroup;
        [Tooltip("펜이 우측으로 이동하는 총 거리입니다.")]
        [SerializeField] private float quillTravelDistance = 60f;
        [Tooltip("펜이 까딱거리는 횟수입니다.")]
        [SerializeField] private int quillTiltCount = 3;
        [Tooltip("까딱임 1회에 걸리는 시간입니다.")]
        [SerializeField] private float quillSingleTiltDuration = 0.45f;
        [Tooltip("펜이 까딱거리는 각도.")]
        [SerializeField, Range(1f, 20f)] private float quillTiltAngle = 4f;
        [Tooltip("까딱임이 한 번 왕복(끄덕)하는 데 걸리는 시간입니다.")]
        [SerializeField] private float quillTiltDuration = 0.15f;
        [SerializeField] private float quillFadeInDuration = 0.12f;
        [SerializeField] private float quillFadeOutDuration = 0.2f;

        [Header("Reward Items")]
        [SerializeField] private RewardSetData rewardSetData;
        [SerializeField] private RectTransform rewardItemsContainer;
        [SerializeField] private RewardItemView rewardItemPrefab;
        [SerializeField] private Vector3 rewardItemStartScale = new Vector3(0.65f, 1.25f, 1f);
        [SerializeField] private float rewardItemParticleLeadDelay = 0.08f;
        [SerializeField] private float rewardItemDuration = 0.3f;
        [SerializeField] private float rewardItemStagger = 0.08f;

        private readonly List<RewardItemView> spawnedRewardItems = new List<RewardItemView>();

        [Header("Events")]
        public UnityEvent OnIntroFinished;
        public UnityEvent OnOutroFinished;
        public UnityEvent OnActionButtonClicked;

        private Sequence sequence;
        private Vector2 centerOriginalPosition;
        private Vector2 bottomOriginalPosition;
        private Vector2 quillOriginalPosition;
        private bool initialized;
        private bool quitRequested;

        // 오프셋 계산을 한 곳으로 모아서 인트로/아웃트로가 항상 같은 기준을 쓰도록 함
        private Vector2 CenterShownPosition => centerOriginalPosition;
        private Vector2 CenterRaisedPosition => centerOriginalPosition + Vector2.up * centerMoveUpDistance;
        private Vector2 BottomShownPosition => bottomOriginalPosition;
        private Vector2 BottomHiddenPosition => bottomOriginalPosition + Vector2.up * bottomHiddenOffsetY;

        private void Awake()
        {
            Initialize();
            WireButton();
        }

        private void OnEnable()
        {
            Initialize();
            if (playOnEnable) PlayIntro();
        }

        private void OnDisable()
        {
            KillSequence();
        }

        public void Show()
        {
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
                if (playOnEnable) return;
            }

            PlayIntro();
        }

        [ContextMenu("Play Intro")]
        public void PlayIntro()
        {
            Initialize();
            KillSequence();
            gameObject.SetActive(true);
            LockGameplayInputForPresentation();
            PopulateRewardsFromSet();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.alpha = 1f;
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            // 딤: 항상 완전 투명(0)에서 시작 -> 암전(1) -> 유지 -> 다시 밝아짐(0)
            if (dimCanvasGroup != null)
            {
                dimCanvasGroup.gameObject.SetActive(true);
                dimCanvasGroup.alpha = 0f;
            }

            // 콘텐츠는 암전 상태에서 미리 시작 위치로 세팅해둡니다
            PrepareCenterStageForIntro();

            if (questCompleteText != null)
            {
                Vector3 s = questCompleteText.transform.localScale;
                questCompleteText.transform.localScale = new Vector3(1f, s.y, s.z);
            }

            if (quillPenTransform != null)
            {
                quillPenTransform.gameObject.SetActive(true);
                quillPenTransform.anchoredPosition = quillOriginalPosition;
                quillPenTransform.localEulerAngles = Vector3.zero;
            }
            if (quillPenCanvasGroup != null)
            {
                quillPenCanvasGroup.alpha = 0f;
            }

            if (bottomRightGroup != null)
            {
                bottomRightGroup.anchoredPosition = BottomHiddenPosition;
                bottomRightGroup.gameObject.SetActive(true);
            }

            CanvasGroup bottomCanvasGroup = EnsureCanvasGroup(bottomRightGroup);
            if (bottomCanvasGroup != null) bottomCanvasGroup.alpha = 0f;

            sequence = DOTween.Sequence();
            FadeOutBgmForIntro();

            // 1) 암전: 0 -> dimMaxAlpha
            if (dimCanvasGroup != null)
            {
                sequence.Insert(0f, dimCanvasGroup.DOFade(dimMaxAlpha, dimDarkenDuration).SetEase(Ease.InOutQuad));
            }

            float brightenStart = dimDarkenDuration + dimHoldDuration;
            float centerContentStart = brightenStart + centerParticleLeadDelay;

            sequence.InsertCallback(brightenStart, EnableUiCameraPostProcessingForReveal);
            sequence.InsertCallback(brightenStart, HideGameplayForQuestCompleteReveal);
            sequence.InsertCallback(brightenStart, PlayQuestCompleteSound);
            sequence.InsertCallback(brightenStart, ShowCenterStageAndParticles);

            // 밝아진 뒤에도 딤을 일부 유지해 팝업에 시선을 모읍니다.
            float dimRestAlpha = Mathf.Clamp01(dimMaxAlpha * 0.45f);
            if (dimCanvasGroup != null)
            {
                sequence.Insert(brightenStart, dimCanvasGroup.DOFade(dimRestAlpha, dimBrightenDuration).SetEase(Ease.InOutQuad));
            }

            if (centerStageCanvasGroup != null)
            {
                sequence.Insert(centerContentStart,
                    centerStageCanvasGroup.DOFade(1f, centerContentFadeDuration).SetEase(Ease.OutQuad));
            }

            if (centerStage != null)
            {
                sequence.Insert(centerContentStart,
                    centerStage.DOScale(Vector3.one, centerContentFadeDuration).SetEase(Ease.OutBack, 1.08f));
                sequence.Insert(centerContentStart + centerHoldDuration,
                    centerStage.DOAnchorPos(CenterRaisedPosition, centerMoveDuration).SetEase(Ease.InOutSine));
                sequence.Insert(centerContentStart + centerHoldDuration,
                    centerStage.DOScale(centerEndScale, centerMoveDuration).SetEase(Ease.InOutSine));
            }

            if (questCompleteText != null)
            {
                // 텍스트만 가로로 살짝 줄어드는 스퀴즈 연출 (부모인 centerStage 스케일과는 별개로 동작)
                sequence.Insert(centerContentStart,
                    questCompleteText.transform.DOScaleX(textSqueezeScaleX, textSqueezeDuration).SetEase(Ease.OutQuad));
            }

            // 깃털펜: 텍스트 등장과 같은 타이밍에 페이드인 후 좌우로 왕복
            float quillStart = centerContentStart;
            float quillEnd = quillStart + (quillTiltCount * quillSingleTiltDuration);

            if (quillPenCanvasGroup != null)
            {
                sequence.Insert(quillStart, quillPenCanvasGroup.DOFade(1f, quillFadeInDuration).SetEase(Ease.OutQuad));
            }

            if (quillPenTransform != null)
            {
                quillPenTransform.localEulerAngles = Vector3.zero;

                float stepDistance = quillTravelDistance / Mathf.Max(1, quillTiltCount);

                // 까딱 1번 = 이동 1구간으로 묶어서, 매 까딱마다 이동도 같은 시간만큼 같이 진행되게 동기화
                for (int i = 0; i < quillTiltCount; i++)
                {
                    float segmentStart = quillStart + (quillSingleTiltDuration * i);
                    float targetX = quillOriginalPosition.x + stepDistance * (i + 1);

                    sequence.Insert(segmentStart,
                        quillPenTransform.DOAnchorPosX(targetX, quillSingleTiltDuration).SetEase(Ease.Linear));

                    sequence.Insert(segmentStart,
                        quillPenTransform.DOLocalRotate(new Vector3(0f, 0f, quillTiltAngle), quillSingleTiltDuration / 2f)
                            .SetEase(Ease.InOutSine)
                            .SetLoops(2, LoopType.Yoyo));
                }
            }

            if (quillPenCanvasGroup != null)
            {
                sequence.Insert(quillEnd, quillPenCanvasGroup.DOFade(0f, quillFadeOutDuration).SetEase(Ease.InQuad));
            }

            sequence.InsertCallback(quillEnd, () =>
            {
                if (quillPenTransform != null) quillPenTransform.localEulerAngles = Vector3.zero;
            });

            float centerMoveEnd = centerContentStart + centerHoldDuration + centerMoveDuration;
            float rewardsStart = centerMoveEnd;

            for (int i = 0; i < spawnedRewardItems.Count; i++)
            {
                RewardItemView item = spawnedRewardItems[i];
                float itemStart = rewardsStart + (rewardItemStagger * i);
                float itemContentStart = itemStart + rewardItemParticleLeadDelay;

                PrepareRewardItemForIntro(item);
                sequence.InsertCallback(itemStart, () => ShowRewardItemParticles(item));
                sequence.Insert(itemContentStart, item.CanvasGroup.DOFade(1f, rewardItemDuration).SetEase(Ease.OutQuad));
                sequence.Insert(itemContentStart, item.RectTransform.DOScale(Vector3.one, rewardItemDuration).SetEase(Ease.OutBack, 1.2f));
            }

            float rewardsEnd = rewardsStart + (rewardItemStagger * Mathf.Max(0, spawnedRewardItems.Count - 1)) + rewardItemParticleLeadDelay + rewardItemDuration;

            float bottomStartTime = rewardsEnd + bottomGroupDelay;

            if (bottomRightGroup != null)
            {
                sequence.Insert(bottomStartTime,
                    bottomRightGroup.DOAnchorPos(BottomShownPosition, bottomGroupDuration).SetEase(Ease.OutBack, 1.1f));
            }

            if (bottomCanvasGroup != null)
            {
                sequence.Insert(bottomStartTime, bottomCanvasGroup.DOFade(1f, bottomGroupDuration * 0.8f).SetEase(Ease.OutQuad));
            }

            sequence.OnComplete(() =>
            {
                if (rootCanvasGroup != null)
                {
                    rootCanvasGroup.interactable = true;
                    rootCanvasGroup.blocksRaycasts = true;
                }
                OnIntroFinished?.Invoke();
            });
        }

        [ContextMenu("Play Outro")]
        public void PlayOutro()
        {
            Initialize();
            KillSequence();

            if (rootCanvasGroup != null)
            {
                rootCanvasGroup.interactable = false;
                rootCanvasGroup.blocksRaycasts = false;
            }

            if (dimCanvasGroup != null) dimCanvasGroup.alpha = 0f;

            sequence = DOTween.Sequence();

            // 1) 콘텐츠 퇴장 + 암전 동시 진행 (암전이 콘텐츠 사라짐을 가려줌)
            if (dimCanvasGroup != null)
            {
                sequence.Insert(0f, dimCanvasGroup.DOFade(dimMaxAlpha, dimDarkenDuration).SetEase(Ease.InOutSine));
            }

            if (centerStage != null)
            {
                sequence.Insert(0f, centerStage.DOScale(centerEndScale * 0.92f, contentExitDuration).SetEase(Ease.InSine));
            }

            if (bottomRightGroup != null)
            {
                sequence.Insert(0f, bottomRightGroup.DOAnchorPos(BottomHiddenPosition, contentExitDuration).SetEase(Ease.InBack));
            }

            // 2) 완전 암전 상태에서 콘텐츠를 즉시 숨김 처리 (부자연스러운 잔상 방지)
            float brightenStart = dimDarkenDuration + dimHoldDuration;
            sequence.InsertCallback(brightenStart, () =>
            {
                if (centerStage != null) centerStage.gameObject.SetActive(false);
                if (bottomRightGroup != null) bottomRightGroup.gameObject.SetActive(false);
                if (quillPenTransform != null) quillPenTransform.gameObject.SetActive(false);
            });

            // 3) 다시 밝아짐: dimMaxAlpha -> 0 (원래 게임 화면 복귀)
            if (dimCanvasGroup != null)
            {
                sequence.Insert(brightenStart, dimCanvasGroup.DOFade(0f, dimBrightenDuration).SetEase(Ease.InOutSine));
            }

            sequence.OnComplete(() =>
            {
                OnOutroFinished?.Invoke();
                if (deactivateOnOutroComplete) gameObject.SetActive(false);
                if (quitRequested) QuitApplication();
            });
        }

        private void Initialize()
        {
            if (initialized) return;

            if (rootCanvasGroup == null)
            {
                rootCanvasGroup = GetComponent<CanvasGroup>();
                if (rootCanvasGroup == null) rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            if (dimImage != null && dimCanvasGroup == null)
            {
                dimCanvasGroup = dimImage.GetComponent<CanvasGroup>();
                if (dimCanvasGroup == null) dimCanvasGroup = dimImage.gameObject.AddComponent<CanvasGroup>();
            }

            if (dimImage != null)
            {
                Color c = dimImage.color;
                dimImage.color = new Color(c.r, c.g, c.b, 1f); // 알파는 CanvasGroup이 전담
            }

            if (centerStage != null && centerStageCanvasGroup == null)
            {
                centerStageCanvasGroup = centerStage.GetComponent<CanvasGroup>();
                if (centerStageCanvasGroup == null) centerStageCanvasGroup = centerStage.gameObject.AddComponent<CanvasGroup>();
            }

            if (centerStage != null) centerOriginalPosition = centerStage.anchoredPosition;
            if (bottomRightGroup != null) bottomOriginalPosition = bottomRightGroup.anchoredPosition;
            if (quillPenTransform != null) quillOriginalPosition = quillPenTransform.anchoredPosition;

            initialized = true;
        }

        private void WireButton()
        {
            if (actionButton == null) return;
            actionButton.onClick.RemoveListener(HandleActionButtonClicked);
            actionButton.onClick.AddListener(HandleActionButtonClicked);
        }

        private void HideGameplayForQuestCompleteReveal()
        {
            ActionRPG.Player.NetworkPlayerController player = FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
            if (player != null)
            {
                player.ForceExitCombatState();
            }

            UI_GamePlayOverlay overlay = FindFirstObjectByType<UI_GamePlayOverlay>();
            if (overlay != null)
            {
                overlay.HideForQuestCompletePresentation();
            }

            PlayerWorldBar playerWorldBar = FindFirstObjectByType<PlayerWorldBar>();
            if (playerWorldBar != null)
            {
                playerWorldBar.SetPlayerNameVisible(false);
            }
        }

        private void EnableUiCameraPostProcessingForReveal()
        {
            if (!enableUiCameraPostProcessingOnReveal) return;

            Camera targetCamera = ResolveUiCamera();
            if (targetCamera == null) return;

            Component cameraData = ResolveAdditionalCameraData(targetCamera);
            if (cameraData != null)
            {
                SetCameraPostProcessingEnabled(cameraData);
            }
        }

        private Component ResolveAdditionalCameraData(Camera targetCamera)
        {
            Component[] components = targetCamera.GetComponents<Component>();
            foreach (Component component in components)
            {
                if (component == null) continue;
                if (component.GetType().Name == "UniversalAdditionalCameraData")
                {
                    return component;
                }
            }

            return null;
        }

        private void SetCameraPostProcessingEnabled(Component cameraData)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            System.Type cameraDataType = cameraData.GetType();
            PropertyInfo property = cameraDataType.GetProperty("renderPostProcessing", flags);
            if (property != null && property.CanWrite)
            {
                property.SetValue(cameraData, true);
                return;
            }

            FieldInfo field = cameraDataType.GetField("m_RenderPostProcessing", flags);
            if (field != null)
            {
                field.SetValue(cameraData, true);
            }
        }

        private Camera ResolveUiCamera()
        {
            if (uiCamera != null) return uiCamera;

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Camera camera in cameras)
            {
                if (camera == null) continue;
                string cameraName = camera.gameObject.name;
                if (cameraName.Contains("UI") || cameraName.Contains("Ui") || cameraName.Contains("ui"))
                {
                    uiCamera = camera;
                    return uiCamera;
                }
            }

            return null;
        }

        private void HandleActionButtonClicked()
        {
            OnActionButtonClicked?.Invoke();
            if (quitApplicationOnActionButton)
            {
                QuitApplication();
                return;
            }

            PlayOutro();
        }

        private void LockGameplayInputForPresentation()
        {
            if (ActionRPG.Player.MobileTouchManager.Instance != null)
            {
                ActionRPG.Player.MobileTouchManager.Instance.SetInputBlocked(true);
            }

            ActionRPG.Player.NetworkPlayerController player = FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
            if (player != null)
            {
                player.isInputLocked = true;
            }

            ActionRPG.CameraSystem.CameraController cameraController =
                Camera.main != null ? Camera.main.GetComponent<ActionRPG.CameraSystem.CameraController>() : null;
            if (cameraController != null)
            {
                cameraController.isInputLocked = true;
            }
        }

        public void RequestQuitAfterOutro()
        {
            QuitApplication();
        }

        private void KillSequence()
        {
            if (sequence != null && sequence.IsActive()) sequence.Kill();
            sequence = null;
        }

        private void PlayQuestCompleteSound()
        {
            if (string.IsNullOrEmpty(questCompleteSoundID) || SoundManager.Instance == null) return;
            SoundManager.Instance.PlaySFXByKey(questCompleteSoundID);
        }

        private void FadeOutBgmForIntro()
        {
            if (!fadeOutBgmOnIntro || SoundManager.Instance == null) return;

            float duration = bgmFadeOutDuration > 0f ? bgmFadeOutDuration : dimDarkenDuration;
            SoundManager.Instance.FadeBGM(bgmFadeOutTargetRatio, duration);
        }

        private void PrepareCenterStageForIntro()
        {
            if (centerStage == null) return;

            centerStage.anchoredPosition = CenterShownPosition;
            centerStage.localScale = centerStartScale;
            centerStage.gameObject.SetActive(false);

            if (centerStageCanvasGroup != null)
            {
                centerStageCanvasGroup.alpha = 0f;
                centerStageCanvasGroup.interactable = false;
                centerStageCanvasGroup.blocksRaycasts = false;
            }

            if (centerVisualObject != null && centerVisualObject != centerStage.gameObject)
            {
                centerVisualObject.SetActive(false);
            }
        }

        private void ShowCenterStageAndParticles()
        {
            if (centerStage != null) centerStage.gameObject.SetActive(true);

            GameObject centerStageObject = centerStage != null ? centerStage.gameObject : null;
            if (centerVisualObject != null && centerVisualObject != centerStageObject)
            {
                centerVisualObject.SetActive(true);
            }

            PlayCenterParticles();
        }

        private void PlayCenterParticles()
        {
            GameObject particleRoot = centerVisualObject != null ? centerVisualObject : centerStage != null ? centerStage.gameObject : null;
            if (particleRoot == null) return;

            ParticleSystem[] particles = particleRoot.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in particles)
            {
                if (particle == null) continue;
                particle.Clear(true);
                particle.Play(true);
            }
        }

        private static CanvasGroup EnsureCanvasGroup(RectTransform target)
        {
            if (target == null) return null;
            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
            return canvasGroup;
        }

        public void PopulateRewards(IReadOnlyList<RewardItemData> rewards)
        {
            ClearRewards();
            if (rewardItemsContainer == null || rewardItemPrefab == null) return;
            if (rewards == null) return;

            foreach (var reward in rewards)
            {
                if (reward == null) continue;

                RewardItemView item = Instantiate(rewardItemPrefab, rewardItemsContainer);
                item.Setup(reward);
                PrepareRewardItemForIntro(item);
                item.StopIntroParticles();
                item.gameObject.SetActive(false);
                spawnedRewardItems.Add(item);
            }
        }

        public void PopulateRewards(IReadOnlyList<(Sprite icon, string title, string description)> rewards)
        {
            ClearRewards();
            if (rewardItemsContainer == null || rewardItemPrefab == null) return;
            if (rewards == null) return;

            foreach (var reward in rewards)
            {
                RewardItemView item = Instantiate(rewardItemPrefab, rewardItemsContainer);
                item.Setup(reward.icon, reward.title, reward.description);
                PrepareRewardItemForIntro(item);
                item.StopIntroParticles();
                item.gameObject.SetActive(false);
                spawnedRewardItems.Add(item);
            }
        }

        private void PopulateRewardsFromSet()
        {
            if (rewardSetData == null) return;
            PopulateRewards(rewardSetData.RewardItems);
        }

        private void PrepareRewardItemForIntro(RewardItemView item)
        {
            if (item == null) return;

            item.CanvasGroup.alpha = 0f;
            item.RectTransform.localScale = rewardItemStartScale;
        }

        private void ShowRewardItemParticles(RewardItemView item)
        {
            if (item == null) return;

            item.gameObject.SetActive(true);
            item.StopIntroParticles();
            item.PlayIntroParticles();
        }

        private void ClearRewards()
        {
            foreach (var item in spawnedRewardItems)
            {
                if (item != null) Destroy(item.gameObject);
            }
            spawnedRewardItems.Clear();
        }

        private void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

    }
}
