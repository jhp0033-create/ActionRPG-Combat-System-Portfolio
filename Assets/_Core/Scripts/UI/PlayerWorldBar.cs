using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ActionRPG.Player;
using ActionRPG.Managers;
namespace ActionRPG.UI
{
    /// <summary>
    /// 플레이어 캐릭터 머리 위에 닉네임과 체력바를 표시합니다.
    /// WorldHealthBar(적 체력바)와 동일한 WorldToScreenPoint 추적 방식을 사용합니다.
    /// - WorldUICanvas(Screen Space - Camera)의 자식으로 배치해야 합니다.
    /// - NetworkPlayerController가 있는 "Player" 태그 오브젝트를 자동으로 찾아 추적합니다.
    /// </summary>
    public class PlayerWorldBar : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("체력 게이지 이미지 (Image Type: Filled, Fill Method: Horizontal)")]
        public Image healthFillImage;

        [Tooltip("피격 시 뒤에서 깎이는 하얀색 쉐도우 바 이미지 (수동 할당)")]
        public Image whiteShadowImage;

        [Tooltip("플레이어 닉네임 텍스트 (TMPro)")]
        public TextMeshProUGUI nameText;

        [Tooltip("체력바 배경 이미지")]
        public Image backgroundImage;

        [Header("Player Settings")]
        [Tooltip("플레이어 닉네임")]
        public string playerName = "테스트";

        [Tooltip("플레이어 최대 체력")]
        public float maxHealth = 500f;

        [Header("UI Containers (독립 트래킹용)")]
        [Tooltip("체력바와 닉네임을 분리하여 개별 트래킹할지 여부")]
        public bool useSeparateTracking = true;

        [Tooltip("닉네임 UI 영역의 RectTransform")]
        public RectTransform nameContainer;

        [Tooltip("체력바 UI 영역의 RectTransform (개별 트래킹용)")]
        public RectTransform hpBarContainer;

        [Header("Tracking Settings - Nickname (머리)")]
        [Tooltip("닉네임 추적 대상 (비어 있으면 자동으로 자식 오브젝트 중 Head를 탐색합니다)")]
        public Transform nameTrackedTarget;
        [Tooltip("닉네임 높이 오프셋")]
        public float nameHeightOffset = 0.3f; // 머리 뼈 기준이므로 0.3 정도의 여유분만 둠

        [Header("Tracking Settings - HP Bar (발)")]
        [Tooltip("체력바 추적 대상 (비어 있으면 플레이어 루트를 사용합니다)")]
        public Transform hpBarTrackedTarget;
        [Tooltip("체력바 높이 오프셋 (발쪽에 두려면 0.15 정도)")]
        public float hpBarHeightOffset = 0.15f;

        [Header("Scale Compensation (거리 비례 크기 조절)")]
        [Tooltip("크기가 1.0이 되는 기준 거리")]
        public float referenceDistance = 10f;
        [Tooltip("최소 크기 방어선")]
        public float minScale = 0.6f;
        [Tooltip("최대 크기 방어선 (가까울 때 너무 커지는 현상 방지)")]
        public float maxScale = 2.0f;

        // 내부 참조
        private Camera mainCam;
        private RectTransform rectTransform;
        private Canvas rootCanvas;
        private CanvasGroup canvasGroup;
        private Transform trackedTarget; // 기본 폴백용 루트 타겟
        private CharacterController targetCharController;
        private Collider targetCollider;

        private float currentHealth;

        [Header("Combo UI")]
        [Tooltip("콤보 UI 전체를 감싸는 컨테이너의 CanvasGroup (페이드 제어용)")]
        public CanvasGroup comboContainerGroup;
        [Tooltip("콤보 숫자가 표시될 텍스트 (이펙트 적용 대상)")]
        public TextMeshProUGUI comboNumberText;
        [Tooltip("고정 글자 'COMBO' 텍스트 (색상 깜빡임 연출 공유)")]
        public TextMeshProUGUI comboLabelText;
        
        private Color initialNumberColor = Color.white;
        private Color initialLabelColor = Color.white;
        
        private Tween comboColorTweenNum;
        private Tween comboColorTweenLabel;
        private Tween comboScaleTween;

        private void Awake()
        {
            mainCam = Camera.main;
            rectTransform = GetComponent<RectTransform>();
            rootCanvas = GetComponentInParent<Canvas>();

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (healthFillImage == null) Debug.LogError("[PlayerWorldBar] healthFillImage가 인스펙터에 할당되지 않았습니다! (초록색 게이지)");
            if (whiteShadowImage == null) Debug.LogError("[PlayerWorldBar] whiteShadowImage가 인스펙터에 할당되지 않았습니다! (피격 시 깎이는 하얀색 잔상)");
            if (backgroundImage == null) Debug.LogError("[PlayerWorldBar] backgroundImage가 인스펙터에 할당되지 않았습니다! (검은색 배경)");

            currentHealth = maxHealth;

            // 콤보 UI는 프리팹 구조가 유지되면 하위 오브젝트 이름으로 자동 연결합니다.
            if (comboContainerGroup == null)
            {
                Transform container = FindDeepChild(transform, "ComboContainer");
                if (container != null)
                {
                    comboContainerGroup = container.GetComponent<CanvasGroup>();
                    if (comboContainerGroup == null)
                        comboContainerGroup = container.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (comboNumberText == null)
            {
                Transform numberText = FindDeepChild(transform, "ComboNumberText");
                if (numberText != null)
                {
                    comboNumberText = numberText.GetComponent<TextMeshProUGUI>();
                }
            }
            
            if (comboLabelText == null)
            {
                Transform labelText = FindDeepChild(transform, "ComboLabelText");
                if (labelText != null)
                {
                    comboLabelText = labelText.GetComponent<TextMeshProUGUI>();
                }
            }

            if (comboNumberText != null)
            {
                initialNumberColor = comboNumberText.color;
            }
            if (comboLabelText != null)
            {
                initialLabelColor = comboLabelText.color;
            }

            // 닉네임과 체력바의 추적 기준을 분리하기 위해 컨테이너를 준비합니다.
            InitializeContainers();
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

        private void InitializeContainers()
        {
            if (!useSeparateTracking) return;

            if (nameContainer == null && nameText != null)
            {
                GameObject newNameContainer = new GameObject("NameContainer", typeof(RectTransform));
                newNameContainer.transform.SetParent(transform, false);
                nameContainer = newNameContainer.GetComponent<RectTransform>();

                RectTransform textRect = nameText.GetComponent<RectTransform>();
                nameContainer.anchorMin = textRect.anchorMin;
                nameContainer.anchorMax = textRect.anchorMax;
                nameContainer.pivot = textRect.pivot;
                nameContainer.anchoredPosition = textRect.anchoredPosition;
                nameContainer.sizeDelta = textRect.sizeDelta;

                textRect.SetParent(nameContainer, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }

            if (hpBarContainer == null && healthFillImage != null)
            {
                GameObject newHpContainer = new GameObject("HPBarContainer", typeof(RectTransform));
                newHpContainer.transform.SetParent(transform, false);
                hpBarContainer = newHpContainer.GetComponent<RectTransform>();

                RectTransform fillRect = healthFillImage.GetComponent<RectTransform>();
                hpBarContainer.anchorMin = fillRect.anchorMin;
                hpBarContainer.anchorMax = fillRect.anchorMax;
                hpBarContainer.pivot = fillRect.pivot;
                hpBarContainer.anchoredPosition = fillRect.anchoredPosition;
                hpBarContainer.sizeDelta = fillRect.sizeDelta;

                fillRect.SetParent(hpBarContainer, false);
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;

                if (backgroundImage != null)
                {
                    RectTransform bgRect = backgroundImage.GetComponent<RectTransform>();
                    bgRect.SetParent(hpBarContainer, false);
                    bgRect.SetAsFirstSibling(); // 배경이 가장 뒤로
                    bgRect.anchorMin = Vector2.zero;
                    bgRect.anchorMax = Vector2.one;
                    bgRect.offsetMin = Vector2.zero;
                    bgRect.offsetMax = Vector2.zero;
                }

                // 잔상 바도 동일 컨테이너 기준으로 배치해 체력바와 정렬을 맞춥니다.
                if (whiteShadowImage != null)
                {
                    RectTransform shadowRect = whiteShadowImage.GetComponent<RectTransform>();
                    shadowRect.SetParent(hpBarContainer, false);
                    
                    // healthFillImage 바로 뒤에 렌더링되도록 순서 조정
                    shadowRect.SetSiblingIndex(fillRect.GetSiblingIndex());
                    
                    shadowRect.anchorMin = Vector2.zero;
                    shadowRect.anchorMax = Vector2.one;
                    shadowRect.offsetMin = Vector2.zero;
                    shadowRect.offsetMax = Vector2.zero;
                }
            }
        }

        private void Start()
        {
            // 1순위: NetworkPlayerController 컴포넌트로 플레이어 탐색 (가장 정확함)
            NetworkPlayerController playerCtrl = FindFirstObjectByType<NetworkPlayerController>();
            GameObject player = playerCtrl != null ? playerCtrl.gameObject : null;

            // 보조 탐색: "Player" 태그 오브젝트
            if (player == null)
            {
                player = GameObject.FindGameObjectWithTag("Player");
            }

            if (player != null)
            {
                trackedTarget = player.transform;

                targetCharController = player.GetComponent<CharacterController>();
                if (targetCharController == null)
                    targetCharController = player.GetComponentInChildren<CharacterController>();

                targetCollider = player.GetComponent<Collider>();
                if (targetCollider == null)
                    targetCollider = player.GetComponentInChildren<Collider>();

                // [독립 트래킹 세팅] 머리 뼈(Head Bone) 자동 검색
                if (useSeparateTracking)
                {
                    if (nameTrackedTarget == null)
                    {
                        nameTrackedTarget = FindDeepChild(player.transform, "Head");
                        if (nameTrackedTarget == null)
                            nameTrackedTarget = FindDeepChild(player.transform, "head");

                        if (nameTrackedTarget == null)
                            nameTrackedTarget = player.transform; // 폴백: 루트 사용
                    }

                    if (hpBarTrackedTarget == null)
                    {
                        hpBarTrackedTarget = player.transform; // 발밑(루트) 기준
                    }
                }
            }
            else
            {
                Debug.LogError("[PlayerWorldBar] 플레이어 캐릭터(NetworkPlayerController 또는 'Player' 태그)를 찾을 수 없습니다!");
            }

            // 닉네임 및 체력 초기화
            if (nameText != null)
                nameText.text = playerName;

            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = 1f;
            }

            // 콤보 초기화
            if (comboContainerGroup != null)
            {
                comboContainerGroup.alpha = 0f;
            }
            if (comboNumberText != null)
            {
                initialNumberColor = comboNumberText.color;
            }
            if (comboLabelText != null)
            {
                initialLabelColor = comboLabelText.color;
            }

            // 콤보 매니저 이벤트 구독
            if (ComboManager.Instance != null)
            {
                ComboManager.Instance.OnComboChanged += HandleComboChanged;
                ComboManager.Instance.OnComboReset += HandleComboReset;

            }
            else
            {
                Debug.LogError("[PlayerWorldBar] ComboManager.Instance가 널(Null)입니다! 씬에 ComboManager 매니저 오브젝트가 존재하는지 확인해주세요.");
            }

            if (comboContainerGroup == null || comboNumberText == null)
            {
                Debug.LogError($"[PlayerWorldBar] 콤보 UI 연결 실패! ComboContainerGroup 존재 여부: {comboContainerGroup != null}, ComboNumberText 존재 여부: {comboNumberText != null}. 자식 오브젝트 이름이 정확히 'ComboContainer'와 'ComboNumberText'인지 확인하거나 인스펙터에 수동으로 넣어주세요.");
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
            if (comboContainerGroup == null || comboNumberText == null)
            {

                return;
            }

            // 컨테이너 표시 (페이드인)
            if (comboContainerGroup.alpha < 1f)
            {
                comboContainerGroup.DOKill();
                comboContainerGroup.DOFade(1f, 0.2f);
            }

            // 숫자 텍스트 갱신
            comboNumberText.text = combo.ToString();

            // 기존 진행 중인 연출 캔슬
            if (comboScaleTween != null) comboScaleTween.Kill(true);
            if (comboColorTweenNum != null) comboColorTweenNum.Kill(true);
            if (comboColorTweenLabel != null) comboColorTweenLabel.Kill(true);

            // 1. 스케일 연출 (회전/흔들림 없이 자연스럽게 크기만 부드럽게 팽창 수축)
            // 억지스러운 느낌을 줄이기 위해 시작 스케일을 1.0f로 유지하고 바로 1.3배 펌핑 후 돌아옴
            comboNumberText.transform.localScale = Vector3.one;
            comboScaleTween = comboNumberText.transform.DOScale(1.35f, 0.1f)
                .SetEase(Ease.OutCubic)
                .OnComplete(() => {
                    comboScaleTween = comboNumberText.transform.DOScale(1f, 0.25f).SetEase(Ease.OutQuad);
                });

            // 2. 색상 화이트 플래시 (숫자)
            comboNumberText.color = Color.white; // 매우 밝게 보이고 싶다면 이 색상 위에 유니티의 PostProcessing Bloom이 걸려있어야 합니다.
            comboColorTweenNum = comboNumberText.DOColor(initialNumberColor, 0.35f).SetEase(Ease.InQuad);

            // 3. 색상 화이트 플래시 (COMBO 글자 텍스트)
            if (comboLabelText != null)
            {
                comboLabelText.color = Color.white;
                comboColorTweenLabel = comboLabelText.DOColor(initialLabelColor, 0.35f).SetEase(Ease.InQuad);
            }
        }
        private void HandleComboReset()
        {
            if (comboContainerGroup != null)
            {
                comboContainerGroup.DOKill();
                // 콤보 리셋 시 스르륵 페이드 아웃
                comboContainerGroup.DOFade(0f, 0.5f);
            }
        }

        public void Initialize(Transform playerTarget)
        {
            trackedTarget = playerTarget;
            
            targetCharController = trackedTarget.GetComponentInChildren<CharacterController>();
            if (targetCharController == null)
                targetCollider = trackedTarget.GetComponentInChildren<Collider>();

            if (useSeparateTracking)
            {
                if (nameTrackedTarget == null)
                {
                    nameTrackedTarget = FindDeepChild(trackedTarget, "Head");
                    if (nameTrackedTarget == null)
                        nameTrackedTarget = FindDeepChild(trackedTarget, "head");
                        
                    if (nameTrackedTarget == null)
                        nameTrackedTarget = trackedTarget;
                }

                if (hpBarTrackedTarget == null)
                {
                    hpBarTrackedTarget = trackedTarget;
                }
            }
        }

        private Transform FindDeepChild(Transform parent, string keyword)
        {
            foreach (Transform child in parent)
            {
                if (child.name.Contains(keyword)) return child;
                Transform result = FindDeepChild(child, keyword);
                if (result != null) return result;
            }
            return null;
        }

        private void LateUpdate()
        {
            if (trackedTarget == null || mainCam == null) return;

            float distance = Vector3.Distance(mainCam.transform.position, trackedTarget.position);
            
            if (rootCanvas == null) return;
            Camera uiCamera = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : (rootCanvas.worldCamera != null ? rootCanvas.worldCamera : mainCam);
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && uiCamera == null) return;

            float baseScale = referenceDistance / distance;
            baseScale = Mathf.Clamp(baseScale, minScale, maxScale);

            Vector3 targetScale = new Vector3(baseScale, baseScale, baseScale);

            if (useSeparateTracking && nameContainer != null && hpBarContainer != null)
            {
                Transform nameTarget = nameTrackedTarget != null ? nameTrackedTarget : trackedTarget;
                Transform hpTarget = hpBarTrackedTarget != null ? hpBarTrackedTarget : trackedTarget;

                float visualHeight = 2.2f;
                float bottomOffset = 0f;

                if (targetCharController != null && targetCharController.enabled)
                {
                    visualHeight = targetCharController.center.y + (targetCharController.height * 0.5f);
                    bottomOffset = Mathf.Min(0f, targetCharController.center.y - (targetCharController.height * 0.5f));
                }
                else if (targetCollider != null && targetCollider.enabled)
                {
                    if (targetCollider is CapsuleCollider capsule)
                    {
                        visualHeight = capsule.center.y + (capsule.height * 0.5f);
                        bottomOffset = Mathf.Min(0f, capsule.center.y - (capsule.height * 0.5f));
                    }
                    else if (targetCollider is BoxCollider box)
                    {
                        visualHeight = box.center.y + (box.size.y * 0.5f);
                        bottomOffset = Mathf.Min(0f, box.center.y - (box.size.y * 0.5f));
                    }
                    else
                    {
                        visualHeight = targetCollider.bounds.max.y - trackedTarget.position.y;
                        bottomOffset = Mathf.Min(0f, targetCollider.bounds.min.y - trackedTarget.position.y);
                    }
                }

                Vector3 nameWorldPos = (nameTarget == trackedTarget) ? 
                                       trackedTarget.position + Vector3.up * (visualHeight + nameHeightOffset) : 
                                       nameTarget.position + Vector3.up * nameHeightOffset;
                
                Vector3 hpWorldPos = (hpTarget == trackedTarget) ? 
                                     trackedTarget.position + Vector3.up * (bottomOffset + hpBarHeightOffset) : 
                                     hpTarget.position + Vector3.up * hpBarHeightOffset;

                Vector3 nameScreenPos = mainCam.WorldToScreenPoint(nameWorldPos);
                Vector3 hpScreenPos = mainCam.WorldToScreenPoint(hpWorldPos);

                if (nameScreenPos.z > 0f)
                {
                    Vector2 nameLocalPos;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, nameScreenPos, uiCamera, out nameLocalPos);
                    nameContainer.anchoredPosition = nameLocalPos;
                    nameContainer.localScale = targetScale;
                }

                if (hpScreenPos.z > 0f)
                {
                    Vector2 hpLocalPos;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, hpScreenPos, uiCamera, out hpLocalPos);
                    hpBarContainer.anchoredPosition = hpLocalPos;
                    hpBarContainer.localScale = targetScale;
                }
            }
            else
            {
                float visualHeight = 2.2f;
                if (targetCharController != null && targetCharController.enabled)
                    visualHeight = targetCharController.center.y + (targetCharController.height * 0.5f);
                else if (targetCollider != null && targetCollider.enabled)
                {
                    if (targetCollider is CapsuleCollider capsule)
                        visualHeight = capsule.center.y + (capsule.height * 0.5f);
                    else if (targetCollider is BoxCollider box)
                        visualHeight = box.center.y + (box.size.y * 0.5f);
                    else
                        visualHeight = targetCollider.bounds.max.y - trackedTarget.position.y;
                }

                Vector3 worldPos = trackedTarget.position + Vector3.up * (visualHeight + nameHeightOffset);
                Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

                if (screenPos.z > 0f)
                {
                    Vector2 localPos;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        rootCanvas.GetComponent<RectTransform>(), screenPos, uiCamera, out localPos);
                    rectTransform.anchoredPosition = localPos;
                    
                    rectTransform.localScale = Vector3.Lerp(
                        rectTransform.localScale,
                        targetScale,
                        Time.deltaTime * 10f);
                }
            }
        }

        private bool isFirstHealthUpdate = true;

        public void TakeDamage(float damageAmount)
        {
            float oldRatio = maxHealth > 0 ? currentHealth / maxHealth : 0f;
            currentHealth = Mathf.Max(0f, currentHealth - damageAmount);
            float newRatio = maxHealth > 0 ? currentHealth / maxHealth : 0f;

            UpdateHealthUI(oldRatio, newRatio);
        }

        public void SetHealth(float current, float max)
        {
            float oldRatio = maxHealth > 0 ? currentHealth / maxHealth : 0f;
            maxHealth = max;
            currentHealth = Mathf.Clamp(current, 0f, max);
            float newRatio = maxHealth > 0 ? currentHealth / maxHealth : 0f;

            UpdateHealthUI(oldRatio, newRatio);
        }

        private void UpdateHealthUI(float oldRatio, float newRatio)
        {
            if (healthFillImage == null || maxHealth <= 0f) return;

            healthFillImage.DOKill();
            if (whiteShadowImage != null)
            {
                whiteShadowImage.DOKill();
            }

            // [오프닝 연출 개선] 첫 등장 시 촌스럽게 차오르는 애니메이션을 스킵하고 멋진 팝업 효과 적용
            if (isFirstHealthUpdate)
            {
                isFirstHealthUpdate = false;
                healthFillImage.fillAmount = newRatio;
                if (whiteShadowImage != null) whiteShadowImage.fillAmount = newRatio;

                if (hpBarContainer != null)
                {
                    hpBarContainer.localScale = Vector3.one * 0.3f;
                    hpBarContainer.DOScale(Vector3.one, 0.6f).SetEase(Ease.OutElastic, 1.2f, 0.8f);
                }
                return;
            }

            healthFillImage.DOFillAmount(newRatio, 0.15f).SetEase(Ease.OutCubic);

            if (newRatio < oldRatio)
            {
                if (whiteShadowImage != null)
                {
                    whiteShadowImage.DOFillAmount(newRatio, 0.4f)
                        .SetDelay(0.4f)
                        .SetEase(Ease.OutQuad);
                }

                CreateDamageChunk(oldRatio, newRatio);
            }
            else if (newRatio > oldRatio && whiteShadowImage != null)
            {
                whiteShadowImage.fillAmount = newRatio;
            }
        }

        private void CreateDamageChunk(float oldRatio, float newRatio)
        {
            if (healthFillImage == null || oldRatio <= newRatio) return;

            Image chunkImg = null;
            if (ActionRPG.UI.FloatingUIManager.Instance != null)
            {
                chunkImg = ActionRPG.UI.FloatingUIManager.Instance.GetDamageChunk(healthFillImage.transform);
            }
            else
            {
                GameObject chunkObj = new GameObject("DamageChunk", typeof(RectTransform), typeof(Image));
                chunkObj.transform.SetParent(healthFillImage.transform, false);
                chunkImg = chunkObj.GetComponent<Image>();
            }

            // 오브젝트 풀에서 꺼내올 때 비활성화 상태일 수 있으므로 강제 활성화
            chunkImg.gameObject.SetActive(true);
            chunkImg.transform.SetAsLastSibling();

            chunkImg.sprite = null; 
            chunkImg.color = new Color(1f, 1f, 1f, 0.8f); 
            chunkImg.type = Image.Type.Simple;

            RectTransform chunkRect = chunkImg.rectTransform;

            chunkRect.anchorMin = new Vector2(newRatio, 0.15f);
            chunkRect.anchorMax = new Vector2(oldRatio, 0.85f);
            chunkRect.offsetMin = Vector2.zero;
            chunkRect.offsetMax = Vector2.zero;
            
            chunkRect.pivot = new Vector2(0.5f, 0.5f);
            chunkRect.localScale = Vector3.one;

            chunkRect.DOKill();
            chunkImg.DOKill();

            Sequence seq = DOTween.Sequence();
            
            // 팝아웃 폭발 효과 (0.15초 기반) - 스케일 대폭 증가
            seq.Append(chunkRect.DOScale(new Vector3(1.1f, 1.8f, 1.0f), 0.1f).SetEase(Ease.OutBack));
            seq.AppendInterval(0.1f);
            seq.Append(chunkRect.DOScale(new Vector3(1.0f, 0.1f, 1.0f), 0.15f).SetEase(Ease.InCubic));
            seq.Join(chunkImg.DOFade(0f, 0.15f).SetEase(Ease.InCubic));
            
            seq.OnComplete(() => {
                if (chunkImg != null && ActionRPG.UI.FloatingUIManager.Instance != null)
                {
                    ActionRPG.UI.FloatingUIManager.Instance.ReturnDamageChunk(chunkImg);
                }
                else if (chunkImg != null)
                {
                    Destroy(chunkImg.gameObject);
                }
            });
        }

        /// <summary>
        /// 닉네임을 런타임에 변경합니다.
        /// </summary>
        public void SetPlayerName(string nickname)
        {
            playerName = nickname;
            if (nameText != null) nameText.text = nickname;
        }

        public void SetPlayerNameVisible(bool visible)
        {
            if (nameContainer != null)
            {
                nameContainer.gameObject.SetActive(visible);
                return;
            }

            if (nameText != null)
            {
                nameText.gameObject.SetActive(visible);
            }
        }
    }
}

