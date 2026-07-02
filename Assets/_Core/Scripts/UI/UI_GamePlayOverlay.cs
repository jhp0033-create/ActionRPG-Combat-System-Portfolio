using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ActionRPG.Player;
using ActionRPG.Data;

namespace ActionRPG.UI
{
    public class UI_GamePlayOverlay : MonoBehaviour
    {
        private NetworkPlayerController player;

        [Header("UI Panels (에디터 직접 연결)")]
        [SerializeField] private GameObject normalUIPanel;
        [SerializeField] private GameObject battleUIPanel;
        [SerializeField] private GameObject inventoryPanel;

        [Header("Enemy List UI (에디터 직접 연결)")]
        [Tooltip("화면 좌우 적 리스트 매니저 컴포넌트를 연결하세요.")]
        [SerializeField] private UI_EnemyListManager enemyListManager;

        [Header("Gold UI (에디터 직접 연결)")]
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private RectTransform goldPanelRect;

        [Header("Skill UI (에디터 직접 연결)")]
        [Tooltip("자동 사냥 버튼 (수동 모드일 때만 표시, 클릭 시 자동 사냥 켜짐)")]
        [SerializeField] private UnityEngine.UI.Button autoBattleButton;

        [Tooltip("씬에 직접 배치하신 6개의 UI_SkillSlot 프리팹 오브젝트들을 여기에 순서대로 넣으세요.")]
        [SerializeField] private UI_SkillSlot[] skillSlots = new UI_SkillSlot[6];

        // 쿨타임 완료 순간을 포착하기 위한 상태 추적 배열
        private bool[] wasOnCooldown = new bool[6];
        private int[] lastCooldownSeconds = new int[6];

        private bool isInventoryOpen = false;
        private int displayedGold = int.MinValue;
        private bool isPresentationLocked;

        public void Initialize(NetworkPlayerController playerController)
        {
            this.player = playerController;

            if (player != null)
            {
                player.OnGoldChanged -= RefreshGoldUI;
                player.OnGoldChanged += RefreshGoldUI;
                RefreshGoldUI(player.CurrentGold);

                player.OnCombatStateChanged -= HandleCombatStateChanged;
                player.OnCombatStateChanged += HandleCombatStateChanged;

                player.OnAutoModeChanged -= HandleAutoModeChanged;
                player.OnAutoModeChanged += HandleAutoModeChanged;
            }

            // 자동 사냥 버튼 클릭 이벤트 등록
            if (autoBattleButton != null)
            {
                autoBattleButton.onClick.RemoveAllListeners();
                autoBattleButton.onClick.AddListener(() => {
                    if (player != null && !player.isAutoMode)
                    {
                        player.ToggleAutoMode(); // 자동 사냥 켜기
                    }
                });
            }

            // 에디터에서 연결한 6개의 스킬 슬롯에 클릭 이벤트 연결
            if (skillSlots != null)
            {
                for (int i = 0; i < skillSlots.Length; i++)
                {
                    if (skillSlots[i] != null)
                    {
                        if (skillSlots[i].button != null)
                        {
                            int index = i; // 클로저용 복사본
                            
                            // 기존 이벤트가 겹치지 않게 초기화 후 등록
                            skillSlots[i].button.onClick.RemoveAllListeners();
                            skillSlots[i].button.onClick.AddListener(() => OnSkillSlotClicked(index));
                        }
                        else
                        {
                            Debug.LogError($"[UI_GamePlayOverlay] skillSlots[{i}]의 Button 참조가 비어 있습니다.", skillSlots[i]);
                        }
                    }
                }
            }

            SwitchCombatStateUI(false);
            
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }

            // UI_EnemyListManager 연동 초기화
            if (enemyListManager != null)
            {
                enemyListManager.Initialize(playerController);
                enemyListManager.SetPresentationHidden(isPresentationLocked);
            }
            else
            {
                Debug.LogError("[UI_GamePlayOverlay] enemyListManager가 인스펙터에 할당되지 않았습니다! (UI_EnemyListManager를 드래그 앤 드롭 하세요)");
            }
        }

        private void HandleCombatStateChanged(bool inCombat)
        {
            SwitchCombatStateUI(inCombat);
        }

        private void HandleAutoModeChanged(bool isAuto)
        {
            if (autoBattleButton != null)
            {
                autoBattleButton.transform.DOKill(); // 기존 진행 중인 트윈 취소

                // 배틀 UI가 켜지기 전(일반 UI 상태 등)이라면 애니메이션 없이 즉시 숨김/표시 처리
                if (battleUIPanel != null && !battleUIPanel.activeInHierarchy)
                {
                    autoBattleButton.gameObject.SetActive(!isAuto);
                    autoBattleButton.transform.localScale = isAuto ? Vector3.zero : Vector3.one;
                    return;
                }

                if (isAuto)
                {
                    // 꺼질 때 (자동 사냥 시작): 또잉(InBack) 하면서 0으로 줄어들고 꺼짐
                    autoBattleButton.transform.DOScale(Vector3.zero, 0.3f)
                        .SetEase(Ease.InBack)
                        .OnComplete(() => autoBattleButton.gameObject.SetActive(false));
                }
                else
                {
                    // 켜질 때 (수동 조작으로 풀림): 0에서부터 또잉(OutBack) 하면서 커짐
                    autoBattleButton.gameObject.SetActive(true);
                    autoBattleButton.transform.localScale = Vector3.zero;
                    autoBattleButton.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutBack);
                }
            }
        }

        private void SwitchCombatStateUI(bool inCombat)
        {
            if (isPresentationLocked)
            {
                ForceHideGameplayPanels();
                return;
            }

            // CanvasGroup 페이드 효과를 걷어내고, 단순 SetActive로 명시적 제어
            if (normalUIPanel != null) normalUIPanel.SetActive(!inCombat);
            if (battleUIPanel != null) battleUIPanel.SetActive(inCombat);
            if (enemyListManager != null) enemyListManager.SetPresentationHidden(false);
        }

        public void HideForQuestCompletePresentation()
        {
            isPresentationLocked = true;
            ForceHideGameplayPanels();
            if (enemyListManager != null) enemyListManager.SetPresentationHidden(true);
        }

        private void ForceHideGameplayPanels()
        {
            if (normalUIPanel != null) normalUIPanel.SetActive(false);
            if (battleUIPanel != null) battleUIPanel.SetActive(false);
            if (inventoryPanel != null) inventoryPanel.SetActive(false);
            if (autoBattleButton != null)
            {
                autoBattleButton.transform.DOKill();
                autoBattleButton.gameObject.SetActive(false);
                autoBattleButton.transform.localScale = Vector3.zero;
            }
        }

        public void ToggleInventory()
        {
            isInventoryOpen = !isInventoryOpen;
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(isInventoryOpen);
                if (isInventoryOpen)
                {
                    inventoryPanel.transform.localScale = Vector3.zero;
                    inventoryPanel.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack);
                }
            }
        }

        private void RefreshGoldUI(int newGold)
        {
            if (goldText == null) return;

            bool isInitialSet = displayedGold == int.MinValue;
            bool isSameGold = displayedGold == newGold;

            goldText.text = $"{newGold:#,##0}";
            displayedGold = newGold;

            if (goldPanelRect != null)
            {
                goldPanelRect.DOKill();
                goldPanelRect.localScale = Vector3.one;

                if (!isInitialSet && !isSameGold)
                {
                    goldPanelRect.DOPunchScale(new Vector3(0.15f, 0.15f, 0.15f), 0.2f, 5, 0.5f);
                }
            }
        }

        // 스킬 버튼의 OnClick 이벤트는 이제 프리팹 생성 시 코드로 자동 연결됩니다.
        public void OnSkillSlotClicked(int index)
        {
            if (player != null)
            {
                player.UseSkill(index);
                if (skillSlots[index] != null && skillSlots[index].button != null)
                {
                    skillSlots[index].button.transform.DOPunchScale(new Vector3(-0.1f, -0.1f, -0.1f), 0.15f);
                }
            }
        }

        private void Update()
        {
            if (player == null) return;
            if (isPresentationLocked)
            {
                ForceHideGameplayPanels();
                return;
            }

            for (int i = 0; i < skillSlots.Length; i++)
            {
                UI_SkillSlot slot = skillSlots[i];
                if (slot == null) continue;
                
                SkillData skill = i < player.equippedSkills.Length ? player.equippedSkills[i] : null;
                
                // --- 1. 스킬 데이터가 없는 빈 슬롯 처리 ---
                if (skill == null)
                {
                    if (slot.iconImage != null) slot.iconImage.gameObject.SetActive(false);
                    if (slot.skillNameText != null) 
                    {
                        slot.skillNameText.gameObject.SetActive(true);
                        slot.skillNameText.text = ""; // 스킬 이름 비워주기
                    }
                    if (slot.cooldownOverlay != null) slot.cooldownOverlay.fillAmount = 0f;
                    if (slot.cooldownSecondText != null)
                    {
                        slot.cooldownSecondText.gameObject.SetActive(false);
                    }

                    if (slot.cooldownDecimalText != null)
                    {
                        slot.cooldownDecimalText.gameObject.SetActive(false);
                    }

                    // 빈 슬롯은 불꽃 이펙트 절대 OFF
                    if (slot.fireParticle != null)
                    {
                        slot.fireParticle.Stop(
                            true,
                            ParticleSystemStopBehavior.StopEmitting
                        );

                        slot.fireParticle.gameObject.SetActive(false);
                    }

                    // 빈 슬롯이므로 스택 컨테이너를 숨김 (CanvasGroup 활용)
                    if (slot.stackContainer != null)
                    {
                        slot.stackContainer.SetActive(true); // GameObject 자체는 항상 켜둠
                        var cg = slot.stackContainer.GetComponent<CanvasGroup>();
                        if (cg != null) cg.alpha = 0f;
                        else slot.stackContainer.SetActive(false);
                    }

                    if (slot.stackIcons != null)
                    {
                        foreach (var icon in slot.stackIcons)
                        {
                            if (icon != null) icon.gameObject.SetActive(false);
                        }
                    }
                    continue;
                }

                // --- 2. 스킬 데이터가 있는 정상 슬롯 처리 ---
                float remaining = player.SkillCooldownTimers[i];
                float total = skill.cooldown;
                int currentCharges = player.SkillCharges[i];
                int maxCharges = skill.maxCharges;
                
                if (slot.iconImage == null || slot.buttonBgImage == null) continue;

                if (skill.icon != null)
                {
                    slot.iconImage.gameObject.SetActive(true);
                    slot.iconImage.sprite = skill.icon;
                    if (slot.cooldownOverlay != null) slot.cooldownOverlay.sprite = skill.icon; // 쿨타임 오버레이도 똑같은 스프라이트로 맞춤
                }
                else
                {
                    slot.iconImage.gameObject.SetActive(false);
                }

                // 사용자가 텍스트를 할당했다면 아이콘 유무와 무관하게 항상 스킬 이름 표시
                if (slot.skillNameText != null)
                {
                    slot.skillNameText.gameObject.SetActive(true);
                    slot.skillNameText.text = skill.skillName;
                }

                bool isStackSkill = maxCharges > 1;

                if (isStackSkill)
                {
                    if (slot.stackContainer != null)
                    {
                        slot.stackContainer.SetActive(true); // GameObject 자체는 항상 켜둠
                        var cg = slot.stackContainer.GetComponent<CanvasGroup>();
                        if (cg != null) cg.alpha = 1f;
                    }

                    // 구슬(이미지) 형태의 스택 UI 표시 로직
                    if (slot.stackIcons != null && slot.stackIcons.Length > 0)
                    {
                        for (int j = 0; j < slot.stackIcons.Length; j++)
                        {
                            if (slot.stackIcons[j] == null) continue;
                            
                            // 스택 제한을 넘는 잉여 구슬은 아예 오브젝트를 끔
                            if (j >= maxCharges)
                            {
                                slot.stackIcons[j].gameObject.SetActive(false);
                                continue;
                            }
                            
                            // 사용자가 직접 '색 있는 구슬'과 '색 없는 구슬'을 겹쳐두었으므로, 알파 대신 SetActive로 켜고 끕니다.
                            slot.stackIcons[j].gameObject.SetActive(j < currentCharges);
                        }
                    }
                }
                else
                {
                    if (slot.stackContainer != null)
                    {
                        slot.stackContainer.SetActive(true);
                        var cg = slot.stackContainer.GetComponent<CanvasGroup>();
                        if (cg != null) cg.alpha = 0f;
                        else slot.stackContainer.SetActive(false);
                    }

                    if (slot.stackIcons != null)
                    {
                        for (int j = 0; j < slot.stackIcons.Length; j++)
                        {
                            if (slot.stackIcons[j] != null) slot.stackIcons[j].gameObject.SetActive(false);
                        }
                    }
                }

                if (currentCharges <= 0)
                {
                    if (slot.cooldownOverlay != null)
                        slot.cooldownOverlay.fillAmount = remaining / total;

                    int second = Mathf.CeilToInt(remaining);

                    // 큰 초
                    if (slot.cooldownSecondText != null)
                    {
                        slot.cooldownSecondText.gameObject.SetActive(true);
                        slot.cooldownSecondText.text = second.ToString();

                        // 초 변경 순간 DOTween
                        if (lastCooldownSeconds[i] != second)
                        {
                            lastCooldownSeconds[i] = second;

                            slot.cooldownSecondText.transform.DOKill();

                            slot.cooldownSecondText.transform.localScale =
                                Vector3.one;

                            slot.cooldownSecondText.transform
                                .DOPunchScale(
                                    Vector3.one * 0.25f,
                                    0.2f,
                                    5,
                                    0.5f
                                );
                        }
                    }

                    // 0.1초
                    if (slot.cooldownDecimalText != null)
                    {
                        slot.cooldownDecimalText.gameObject.SetActive(true);
                        slot.cooldownDecimalText.text = ((int)(remaining * 10) % 10).ToString();
                    }
                    if (slot.cooldownDotText != null)
                    {
                        slot.cooldownDotText.gameObject.SetActive(true);
                    }

                    // 쿨다운 중에는 불꽃 OFF
                    if (slot.fireParticle != null)
                    {
                        slot.fireParticle.Stop(
                            true,
                            ParticleSystemStopBehavior.StopEmitting
                        );

                        slot.fireParticle.gameObject.SetActive(false);
                    }

                    wasOnCooldown[i] = true;
                }
                else
                {
                    if (slot.cooldownOverlay != null) slot.cooldownOverlay.fillAmount = 0f;
                    if (slot.cooldownSecondText != null)
                        slot.cooldownSecondText.gameObject.SetActive(false);

                    if (slot.cooldownDecimalText != null)
                        slot.cooldownDecimalText.gameObject.SetActive(false);
                        
                    if (slot.cooldownDotText != null)
                        slot.cooldownDotText.gameObject.SetActive(false);

                    // 사용 가능 상태 → 불꽃 ON
                    if (slot.fireParticle != null)
                    {
                        if (!slot.fireParticle.gameObject.activeSelf)
                        {
                            slot.fireParticle.gameObject.SetActive(true);
                            slot.fireParticle.Play();
                        }
                    }

                    if (wasOnCooldown[i])
                    {
                        wasOnCooldown[i] = false;
                        if (slot.iconImage != null)
                        {
                            slot.iconImage.transform.DOKill();
                            slot.iconImage.transform.localScale = Vector3.one;
                            slot.iconImage.transform.DOPunchScale(new Vector3(0.05f, 0.05f, 0.0f), 0.15f);
                        }
                    }
                }
            }

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard.digit1Key.wasPressedThisFrame && skillSlots.Length > 0 && skillSlots[0] != null && skillSlots[0].button != null) skillSlots[0].button.transform.DOPunchScale(new Vector3(-0.1f, -0.1f, -0.1f), 0.15f);
                if (keyboard.digit2Key.wasPressedThisFrame && skillSlots.Length > 1 && skillSlots[1] != null && skillSlots[1].button != null) skillSlots[1].button.transform.DOPunchScale(new Vector3(-0.1f, -0.1f, -0.1f), 0.15f);
                if (keyboard.digit3Key.wasPressedThisFrame && skillSlots.Length > 2 && skillSlots[2] != null && skillSlots[2].button != null) skillSlots[2].button.transform.DOPunchScale(new Vector3(-0.1f, -0.1f, -0.1f), 0.15f);

                if (keyboard.iKey.wasPressedThisFrame)
                {
                    ToggleInventory();
                }
            }
        }

        private void OnDestroy()
        {
            if (player != null)
            {
                player.OnGoldChanged -= RefreshGoldUI;
                player.OnCombatStateChanged -= HandleCombatStateChanged;
                player.OnAutoModeChanged -= HandleAutoModeChanged;
            }
        }
    }
}
