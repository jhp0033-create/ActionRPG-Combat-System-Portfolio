using UnityEngine;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine.InputSystem;

namespace ActionRPG.UI
{
    public class UI_DialogueManager : MonoBehaviour
    {
        public static UI_DialogueManager Instance { get; private set; }

        [Header("UI References (Editor Setup)")]
        [Tooltip("화면 상단에 고정된 전체 대화창 부모 패널 (에디터 상단 Anchor/Pivot=1 권장)")]
        [SerializeField] private RectTransform dialoguePanelRoot;
        
        [Tooltip("실제 대사가 표시되는 텍스트 (UI_Text_Dialogue)")]
        [SerializeField] private TextMeshProUGUI dialogueText;
        

        [Header("Settings")]
        [Tooltip("타이핑 속도 (글자 당 대기 시간)")]
        public float typeSpeed = 0.05f;
        
        [Tooltip("대화창 등장/퇴장 시 Y축 목표 위치 (Panel Root의 Anchored Y 기준)")]
        public float visiblePosY = 0f;
        [Tooltip("대화창 숨김 시 Y축 위치 (패널 높이 이상으로 위로 올리기)")]
        public float hiddenPosY = 300f;

        [Header("World Tracking (Optional)")]
        [Tooltip("월드 타겟이 지정되면 고정 좌표가 아닌 타겟의 머리 위를 따라다닙니다.")]
        public Vector3 followOffset = new Vector3(0, 2.2f, 0);
        private Transform followTarget;
        private Camera mainCam;

        private Queue<DialogueLine> linesQueue = new Queue<DialogueLine>();
        private DialogueLine currentLine;
        
        private bool isTyping = false;
        private bool isDialogueActive = false;
        private bool autoAdvance = false;
        private float autoAdvanceDelay = 1.1f;

        // 대사 데이터를 ID 기준으로 조회합니다.
        private System.Collections.Generic.Dictionary<string, DialogueData> dialogueDatabase = new System.Collections.Generic.Dictionary<string, DialogueData>();

        private Coroutine typingCoroutine;
        private Coroutine autoAdvanceCoroutine;

        public bool IsDialogueActive => isDialogueActive;
        public event Action OnDialogueFinished;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LoadDialogueDatabase();
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 시작 시 크기를 0으로 해서 숨겨둡니다. (버블 스타일)
            if (dialoguePanelRoot != null)
            {
                dialoguePanelRoot.localScale = Vector3.zero;
                dialoguePanelRoot.gameObject.SetActive(false);
            }
        }

        private void LoadDialogueDatabase()
        {
            DialogueData[] allDialogues = Resources.LoadAll<DialogueData>("DialogueData");
            foreach (var d in allDialogues)
            {
                if (!string.IsNullOrEmpty(d.dialogueID))
                {
                    dialogueDatabase[d.dialogueID] = d;
                }
            }
        }

        private void Update()
        {
            if (!isDialogueActive) return;

            bool tapped = false;

            // PC 좌클릭 체크
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                tapped = true;
            }
            // 모바일 터치 체크 (Input System)
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                tapped = true;
            }

            if (tapped)
            {
                OnUserTap();
            }
        }

        /// <summary>
        /// 대사 식별 코드(dialogueID)를 통해 대화를 시작합니다. (사운드 매니저와 동일 구조)
        /// </summary>
        public void StartDialogueByKey(string dialogueID, bool autoAdvanceLines = false, float lineHoldDuration = 1.1f, Transform target = null)
        {
            if (dialogueDatabase.TryGetValue(dialogueID, out DialogueData dData))
            {
                StartDialogue(dData.lines, autoAdvanceLines, lineHoldDuration, target);
            }
            else
            {
                Debug.LogError($"[UI_DialogueManager] '{dialogueID}' 키에 해당하는 대사 데이터를 찾을 수 없습니다! Resources/DialogueData 폴더에 파일이 있는지, ID가 올바른지 확인해주세요.");
            }
        }

        /// <summary>
        /// 외부에서 이 함수를 호출하여 대화를 시작합니다.
        /// 예: UI_DialogueManager.Instance.StartDialogue(myDialogueLines);
        /// </summary>
        public void StartDialogue(DialogueLine[] lines)
        {
            StartDialogue(lines, false, autoAdvanceDelay);
        }

        /// <summary>
        /// 영상 녹화용 연출처럼 입력 없이 흘러가야 하는 대화에 사용합니다.
        /// target이 지정되면 화면 고정이 아닌 해당 타겟(캐릭터)를 따라다니는 플로팅 UI로 작동합니다.
        /// </summary>
        public void StartDialogue(DialogueLine[] lines, bool autoAdvanceLines, float lineHoldDuration, Transform target = null)
        {
            if (lines == null || lines.Length == 0) return;

            followTarget = target;
            if (mainCam == null) mainCam = Camera.main;

            StopAutoAdvanceRoutine();
            linesQueue.Clear();
            foreach (var line in lines)
            {
                linesQueue.Enqueue(line);
            }

            autoAdvance = autoAdvanceLines;
            autoAdvanceDelay = Mathf.Max(0.1f, lineHoldDuration);
            isDialogueActive = true;

            // 대화창 등장 연출
            if (dialoguePanelRoot != null)
            {
                // 대화창이 켜질 때 이전 대사가 노출되지 않도록 텍스트를 먼저 초기화합니다.
                if (dialogueText != null) dialogueText.text = "";

                dialoguePanelRoot.gameObject.SetActive(true);
                dialoguePanelRoot.DOKill();
                
                dialoguePanelRoot.localScale = Vector3.zero;
                
                Sequence appearSeq = DOTween.Sequence();
                appearSeq.Join(dialoguePanelRoot.DOScale(1f, 0.4f).SetEase(Ease.OutBack));
                
                // 타겟 추적(플로팅 UI) 모드가 아닐 때만 Y축 이동 애니메이션 적용
                if (followTarget == null)
                {
                    dialoguePanelRoot.anchoredPosition = new Vector2(dialoguePanelRoot.anchoredPosition.x, visiblePosY - 50f);
                    appearSeq.Join(dialoguePanelRoot.DOAnchorPosY(visiblePosY, 0.4f).SetEase(Ease.OutCubic));
                }
                
                appearSeq.OnComplete(DisplayNextLine);
            }
            else
            {
                DisplayNextLine();
            }
        }

        private void DisplayNextLine()
        {
            if (linesQueue.Count == 0)
            {
                EndDialogue();
                return;
            }

            currentLine = linesQueue.Dequeue();
            
            // 텍스트 즉시 출력 (타이핑 효과 제거)
            if (dialogueText != null) dialogueText.text = currentLine.dialogueText;
            
            // 대사가 바뀔 때마다 말풍선 강조 효과를 재생합니다.
            if (dialoguePanelRoot != null)
            {
                dialoguePanelRoot.DOKill(true); // 기존 연출 강제 종료
                dialoguePanelRoot.DOPunchScale(new Vector3(0.1f, 0.1f, 0f), 0.3f, 5, 1f);
            }

            // 대화 넘어갈 때 사운드 재생 (SoundManager 표준 방식)
            if (ActionRPG.Managers.SoundManager.Instance != null)
            {
                ActionRPG.Managers.SoundManager.Instance.PlayDialogueAdvanceSFX();
            }

            // 자동으로 다음 대사로 넘어가도록 예약
            ScheduleAutoAdvance();
        }

        private void OnUserTap()
        {
            // 자동 진행 대화에서는 입력 스킵을 사용하지 않습니다.
        }

        private void EndDialogue()
        {
            isDialogueActive = false;
            autoAdvance = false;
            StopAutoAdvanceRoutine();
          
            // 화면 밖으로 살짝 더 올라가면서 작아지며 사라지는 퇴장 연출 (버블 스타일)
            if (dialoguePanelRoot != null)
            {
                dialoguePanelRoot.DOKill();
                Sequence disappearSeq = DOTween.Sequence();
                disappearSeq.Join(dialoguePanelRoot.DOScale(0f, 0.3f).SetEase(Ease.InBack));
                
                if (followTarget == null)
                {
                    disappearSeq.Join(dialoguePanelRoot.DOAnchorPosY(visiblePosY + 50f, 0.3f).SetEase(Ease.InCubic));
                }
                
                disappearSeq.OnComplete(() =>
                {
                    dialoguePanelRoot.gameObject.SetActive(false);
                    followTarget = null;
                    OnDialogueFinished?.Invoke();
                });
            }
            else
            {
                OnDialogueFinished?.Invoke();
            }
        }

        private void ScheduleAutoAdvance()
        {
            if (!autoAdvance || !isDialogueActive) return;

            StopAutoAdvanceRoutine();
            autoAdvanceCoroutine = StartCoroutine(AutoAdvanceRoutine());
        }

        private IEnumerator AutoAdvanceRoutine()
        {
            yield return new WaitForSeconds(autoAdvanceDelay);
            autoAdvanceCoroutine = null;

            if (isDialogueActive && !isTyping)
            {
                DisplayNextLine();
            }
        }

        private void StopAutoAdvanceRoutine()
        {
            if (autoAdvanceCoroutine != null)
            {
                StopCoroutine(autoAdvanceCoroutine);
                autoAdvanceCoroutine = null;
            }
        }

        private void LateUpdate()
        {
            // 플로팅 UI(타겟 추적) 모드일 경우 매 프레임 위치 업데이트
            if (isDialogueActive && followTarget != null && dialoguePanelRoot != null)
            {
                if (mainCam == null) mainCam = Camera.main;
                if (mainCam != null)
                {
                    Vector3 screenPos = mainCam.WorldToScreenPoint(followTarget.position + followOffset);
                    
                    // 타겟이 카메라 뒤에 있으면 무시
                    if (screenPos.z <= 0) return;

                    Canvas rootCanvas = dialoguePanelRoot.GetComponentInParent<Canvas>();
                    if (rootCanvas != null)
                    {
                        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
                        if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && uiCamera == null)
                            uiCamera = mainCam;

                        RectTransform canvasRect = rootCanvas.transform as RectTransform;
                        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(canvasRect, screenPos, uiCamera, out Vector3 worldPoint))
                        {
                            dialoguePanelRoot.position = worldPoint;
                        }
                    }
                }
            }
        }
    }
}
