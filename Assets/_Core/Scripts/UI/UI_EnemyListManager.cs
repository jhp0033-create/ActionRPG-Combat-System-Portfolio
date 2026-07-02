using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections.Generic;
using ActionRPG.Player;
using ActionRPG.Enemy;
using ActionRPG.Core;

namespace ActionRPG.UI
{
    /// <summary>
    /// 화면 기준 좌우에 전환 가능한 적 목록을 표시하고 타겟 변경 입력을 중계합니다.
    /// </summary>
    public class UI_EnemyListManager : MonoBehaviour
    {
        private NetworkPlayerController player;
        private Camera cam;

        [Header("UI Background Frames (인스펙터 배경 프레임 지정)")]
        [Tooltip("왼쪽에 위치할 적 리스트 백그라운드 RectTransform")]
        [SerializeField] private RectTransform enemyListLeft;
        [Tooltip("오른쪽에 위치할 적 리스트 백그라운드 RectTransform")]
        [SerializeField] private RectTransform enemyListRight;

        [Header("UI List Containers (에디터 직접 연결)")]
        [Tooltip("왼쪽 텍스트들이 동적으로 생성될 실제 컨테이너 패널 RectTransform")]
        [SerializeField] private RectTransform leftContainer;
        [Tooltip("오른쪽 텍스트들이 동적으로 생성될 실제 컨테이너 패널 RectTransform")]
        [SerializeField] private RectTransform rightContainer;

        [Header("UI Item Prefab (텍스트 프리팹 지정 필수)")]
        [Tooltip("적 이름 텍스트 UI로 사용될 프리팹 오브젝트 (TextMeshProUGUI 컴포넌트 포함 필수)")]
        [SerializeField] private GameObject enemyListItemPrefab;

        // 적 캐릭터와 UI 아이템 간의 매핑 딕셔너리
        private Dictionary<EnemyController, UI_EnemyListItem> activeItems = new Dictionary<EnemyController, UI_EnemyListItem>();
        private bool presentationHidden;

        public void Initialize(NetworkPlayerController playerController)
        {
            this.player = playerController;
            this.cam = Camera.main;

            if (enemyListLeft == null || enemyListRight == null || leftContainer == null || rightContainer == null)
            {
                Debug.LogError("[UI_EnemyListManager] enemyListLeft/enemyListRight/leftContainer/rightContainer를 인스펙터에 모두 연결해야 합니다.", this);
                enabled = false;
                return;
            }

            if (enemyListItemPrefab == null)
            {
                Debug.LogError("[UI_EnemyListManager] enemyListItemPrefab이 인스펙터에 할당되지 않았습니다.", this);
                enabled = false;
                return;
            }

            if (leftContainer != null)
            {
                List<GameObject> leftTrash = new List<GameObject>();
                foreach (Transform child in leftContainer)
                {
                    leftTrash.Add(child.gameObject);
                }
                foreach (var trash in leftTrash)
                {
                    trash.transform.SetParent(null);
                    Destroy(trash);
                }
            }

            if (rightContainer != null)
            {
                List<GameObject> rightTrash = new List<GameObject>();
                foreach (Transform child in rightContainer)
                {
                    rightTrash.Add(child.gameObject);
                }
                foreach (var trash in rightTrash)
                {
                    trash.transform.SetParent(null);
                    Destroy(trash);
                }
            }

        }

        private void Update()
        {
            if (presentationHidden) return;
            if (player == null || cam == null || EnemyManager.Instance == null) return;

            var activeEnemies = EnemyManager.Instance.ActiveEnemies;

            // 1. 플레이어 반경 내의 유효한 적 목록 스캔 (화면 밖의 적도 포함하여 상대적 좌우 위치 표시)
            HashSet<EnemyController> currentVisibleEnemies = new HashSet<EnemyController>();

            foreach (var enemyObj in activeEnemies)
            {
                if (enemyObj == null || !enemyObj.activeInHierarchy) continue;

                var enemyCtrl = enemyObj.GetComponent<EnemyController>();
                if (enemyCtrl == null || enemyCtrl.IsDead || enemyCtrl.IsSpawning) continue;

                // 타겟 반경 내의 적만 리스트 대상으로 선정
                float dist = Vector3.Distance(player.transform.position, enemyObj.transform.position);
                if (dist <= player.autoTargetRadius)
                {
                    currentVisibleEnemies.Add(enemyCtrl);
                }
            }

            // [타겟 제외] 현재 선택된 타겟은 리스트에 표시하지 않음 (타겟은 별도 UI에서 표시)
            if (player.currentTarget != null)
            {
                var targetCtrl = player.currentTarget.GetComponent<EnemyController>();
                if (targetCtrl != null) currentVisibleEnemies.Remove(targetCtrl);
            }

            // [선택지 조건] 타겟 제외 후 전환 가능한 적이 1마리 이상 있을 때만 리스트 표시
            bool shouldShowList = currentVisibleEnemies.Count >= 1;

            if (enemyListLeft != null && enemyListLeft.gameObject.activeSelf != shouldShowList)
                enemyListLeft.gameObject.SetActive(shouldShowList);
            if (enemyListRight != null && enemyListRight.gameObject.activeSelf != shouldShowList)
                enemyListRight.gameObject.SetActive(shouldShowList);

            if (!shouldShowList)
            {
                ClearItemsImmediately();
                return;
            }

            // 2. 화면 밖으로 벗어났거나 제거된 적의 UI 정리 (shouldShowList 상태에서만 개별 삭제 연출)
            List<EnemyController> toRemove = new List<EnemyController>();
            foreach (var kvp in activeItems)
            {
                var enemy = kvp.Key;
                if (enemy == null || enemy.IsDead || !currentVisibleEnemies.Contains(enemy))
                {
                    toRemove.Add(enemy);
                }
            }

            foreach (var enemy in toRemove)
            {
                RemoveListItem(enemy);
            }

            // 3. 기준 방향 설정 (포커스된 적 기준)
            // 플레이어가 타겟을 바라보는 방향을 기준으로 좌우(Right 벡터)를 새롭게 도출합니다.
            Vector3 referenceRight = cam.transform.right;
            Vector3 referenceOrigin = cam.transform.position;

            if (player.currentTarget != null)
            {
                Vector3 toTarget = player.currentTarget.position - player.transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.001f)
                {
                    Vector3 forward = toTarget.normalized;
                    referenceRight = Vector3.Cross(Vector3.up, forward).normalized;
                    referenceOrigin = player.transform.position;
                }
            }

            // 신규 스폰 및 side 업데이트
            foreach (var enemyCtrl in currentVisibleEnemies)
            {
                // 포커스된 적 기준의 Right 벡터와 내적하여 좌/우를 정확히 판별
                Vector3 toEnemy = enemyCtrl.transform.position - referenceOrigin;
                bool isLeft = Vector3.Dot(toEnemy, referenceRight) < 0f;

                if (!activeItems.ContainsKey(enemyCtrl))
                {
                    CreateListItem(enemyCtrl, isLeft);
                }
                else
                {
                    UpdateItemSide(enemyCtrl, isLeft);
                }
            }

            // 4. 이름 갱신 (선택 상태는 리스트에서 제외되었으므로 이름만 업데이트)
            foreach (var kvp in activeItems)
            {
                var enemy = kvp.Key;
                var item = kvp.Value;

                item.SetName(enemy.enemyName); // 이름 매 프레임 갱신 (한글화 등 중간 변경 대응)
            }

            // 5. 좌/우 각각의 리스트에서 정렬 + 최상단 폰트 크기 40 적용
            SortAndArrangeList(true);  // Left Panel 정렬
            SortAndArrangeList(false); // Right Panel 정렬
        }

        /// <summary>
        /// 사용자가 지정한 텍스트 UI 프리팹을 복제하여 아이템을 생성하고 컴포넌트를 연결합니다.
        /// </summary>
        private void CreateListItem(EnemyController enemy, bool isLeft)
        {
            RectTransform parentPanel = isLeft ? leftContainer : rightContainer;

            // 1. 프리팹 복제 생성
            GameObject itemObj = Instantiate(enemyListItemPrefab, parentPanel);
            itemObj.name = $"EnemyItem_{enemy.enemyName}";
            
            RectTransform itemRect = itemObj.GetComponent<RectTransform>();
            if (itemRect == null)
            {
                Debug.LogError("[UI_EnemyListManager] enemyListItemPrefab에는 RectTransform이 필요합니다.", itemObj);
                Destroy(itemObj);
                return;
            }

            // 앵커/피벗: 좌측은 컨테이너 좌측 기준(0,1), 우측도 컨테이너 좌측 기준(0,1)
            // → 두 사이드 모두 아이템 왼쪽 끝이 pivot이므로 텍스트가 오른쪽으로 자라 흔들림 없음
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(0f, 1f);
            itemRect.pivot     = new Vector2(0f, 1f);
            itemRect.anchoredPosition = Vector2.zero;

            // 2. 텍스트 컴포넌트 수집
            var nameTexts = itemObj.GetComponentsInChildren<TextMeshProUGUI>(true);

            // 3. 버튼 컴포넌트 검색 및 클릭 리스너 연결
            Button button = itemObj.GetComponent<Button>();

            if (button == null)
            {
                Debug.LogError("[UI_EnemyListManager] enemyListItemPrefab 루트에 Button 컴포넌트가 필요합니다.", itemObj);
                Destroy(itemObj);
                return;
            }

            // 4. 데이터 래퍼 컴포넌트 부착 및 초기화
            UI_EnemyListItem itemWrapper = itemObj.GetComponent<UI_EnemyListItem>();
            if (itemWrapper == null) itemWrapper = itemObj.AddComponent<UI_EnemyListItem>();
            
            itemWrapper.Initialize(itemRect, nameTexts, isLeft, enemy.enemyName);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (player != null && !enemy.IsDead)
                {
                    player.SetTarget(enemy.transform);
                    PlayClickFeedback(itemWrapper);
                }
            });

            // 초기 등장 트윈 연출 (0 크기에서 또잉 커짐) — id="spawn"으로 위치 트윈과 충돌 방지
            itemRect.localScale = Vector3.zero;
            DOTween.Kill(GetScaleTweenId(itemWrapper), false);
            itemRect.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutBack).SetId(GetScaleTweenId(itemWrapper));

            activeItems.Add(enemy, itemWrapper);
        }

        /// <summary>
        /// 적이 화면을 가로질러도 최초 스폰된 컨테이너를 고정 유지합니다.
        /// 피벗/앵커/부모를 변경하지 않아 에디터 세팅이 그대로 보존됩니다.
        /// </summary>
        private void UpdateItemSide(EnemyController enemy, bool isLeft)
        {
            if (activeItems.TryGetValue(enemy, out var item))
            {
                RectTransform targetParent = isLeft ? leftContainer : rightContainer;
                
                // 만약 현재 위치한 컨테이너와 타겟 컨테이너가 다르다면 (즉, 좌/우가 변경되었다면)
                if (item.RectTrans.parent != targetParent)
                {
                    DOTween.Kill(GetPositionTweenId(item), false);
                    item.lastTargetPos = new Vector2(float.NaN, float.NaN);
                    // 부모를 즉각 변경 (false: 로컬 스케일/회전 유지)
                    item.RectTrans.SetParent(targetParent, false);
                    // 텍스트 정렬 상태(좌측 정렬/우측 정렬) 업데이트
                    item.SetSide(isLeft);
                    
                    // 참고: 이후 Update() 하단의 SortAndArrangeList가 호출되면서
                    // 새로운 컨테이너 내에서의 Y좌표를 계산하여 자연스럽게 트위닝(이동)됩니다.
                }
            }
        }

        /// <summary>
        /// 화면 밖으로 나갔거나 사망한 몬스터 UI를 DOTween 스케일 축소 후 삭제합니다.
        /// </summary>
        private void RemoveListItem(EnemyController enemy)
        {
            if (activeItems.TryGetValue(enemy, out var item))
            {
                activeItems.Remove(enemy);
                DOTween.Kill(GetPositionTweenId(item), false);
                DOTween.Kill(GetScaleTweenId(item), false);
                item.RectTrans.DOScale(Vector3.zero, 0.2f)
                    .SetEase(Ease.InBack)
                    .SetId(GetScaleTweenId(item))
                    .OnComplete(() =>
                    {
                        Destroy(item.gameObject);
                    });
            }
        }

        /// <summary>
        /// 선택된 적을 맨 위에 고정하고, 나머지는 world Y 기준으로 정렬합니다.
        /// </summary>
        private void SortAndArrangeList(bool isLeft)
        {
            RectTransform targetPanel = isLeft ? leftContainer : rightContainer;

            // (worldY) — worldY 높은 순 (타겟은 이미 리스트에서 제외됨)
            List<(UI_EnemyListItem item, float worldY)> itemsOnSide =
                new List<(UI_EnemyListItem, float)>();

            foreach (var kvp in activeItems)
            {
                var enemy = kvp.Key;
                var item = kvp.Value;

                if (item.transform.parent == targetPanel)
                {
                    itemsOnSide.Add((item, enemy.transform.position.y));
                }
            }

            if (itemsOnSide.Count == 0) return;

            itemsOnSide.Sort((a, b) => 
            {
                // 소수점 미세 오차 무시
                if (Mathf.Abs(b.worldY - a.worldY) < 0.05f)
                {
                    return a.item.GetInstanceID().CompareTo(b.item.GetInstanceID());
                }
                return b.worldY.CompareTo(a.worldY);
            });

            float startY = -10f;       // 상단 여백
            float itemSpacing = 8f;    // 아이템 간격

            float currentY = startY;

            for (int i = 0; i < itemsOnSide.Count; i++)
            {
                var item = itemsOnSide[i].item;

                // [UI 시각적 정렬] 0번 인덱스가 폰트 사이즈가 40으로 가장 크며, 리스트의 가장 맨 위(Top)에 위치해야 합니다.
                // SiblingIndex가 0이어야 Vertical Layout 등에서 최상단으로 인식됩니다.
                item.transform.SetSiblingIndex(i);

                // 목표 Y 좌표
                float targetY = currentY;
                float targetX = 10f; // 좌/우 모두 pivot=(0,1) → 아이템 왼쪽 끝 기준으로 10px 우측 여백

                Vector2 targetPos = new Vector2(targetX, targetY);

                // 폰트 크기 세팅 (Top = 40, 나머지 = 30)
                item.SetTopItem(i == 0);

                // 다음 아이템을 위한 Y 위치 누적 (Top 아이템은 55f 공간, 나머지는 40f 공간 차지)
                float expectedHeight = (i == 0) ? 55f : 40f;
                currentY -= (expectedHeight + itemSpacing);

                bool targetUnchanged =
                    !float.IsNaN(item.lastTargetPos.x) &&
                    Mathf.Approximately(item.lastTargetPos.x, targetPos.x) &&
                    Mathf.Approximately(item.lastTargetPos.y, targetPos.y);
                bool isCloseToTarget = Vector2.SqrMagnitude(item.RectTrans.anchoredPosition - targetPos) < 0.25f;
                bool isMovingToTarget = DOTween.IsTweening(GetPositionTweenId(item));

                // 목표가 같더라도 외부 연출로 위치 트윈이 끊기면 현재 위치 기준으로 다시 보정합니다.
                if (targetUnchanged && (isCloseToTarget || isMovingToTarget))
                {
                    continue;
                }

                item.lastTargetPos = targetPos;
                // "pos" id가 붙은 위치 트윈만 종료, spawn(scale) 트윈은 건드리지 않음
                DOTween.Kill(GetPositionTweenId(item), false);
                item.RectTrans.DOAnchorPos(targetPos, 0.2f).SetEase(Ease.OutQuad).SetId(GetPositionTweenId(item));
            }
        }

        private void PlayClickFeedback(UI_EnemyListItem item)
        {
            if (item == null || item.RectTrans == null) return;

            DOTween.Kill(GetScaleTweenId(item), false);
            item.RectTrans.localScale = Vector3.one;
            item.RectTrans.DOPunchScale(new Vector3(-0.06f, -0.06f, 0f), 0.15f)
                .SetId(GetScaleTweenId(item));
        }

        private string GetPositionTweenId(UI_EnemyListItem item)
        {
            return "enemy_list_pos_" + item.GetInstanceID();
        }

        private string GetScaleTweenId(UI_EnemyListItem item)
        {
            return "enemy_list_scale_" + item.GetInstanceID();
        }

        public void SetPresentationHidden(bool hidden)
        {
            presentationHidden = hidden;

            if (!hidden) return;

            if (enemyListLeft != null) enemyListLeft.gameObject.SetActive(false);
            if (enemyListRight != null) enemyListRight.gameObject.SetActive(false);
            ClearItemsImmediately();
        }

        private void ClearItemsImmediately()
        {
            foreach (var item in activeItems.Values)
            {
                if (item == null) continue;

                DOTween.Kill(GetPositionTweenId(item), false);
                DOTween.Kill(GetScaleTweenId(item), false);
                Destroy(item.gameObject);
            }
            activeItems.Clear();
        }

        private void OnDestroy()
        {
            foreach (var item in activeItems.Values)
            {
                if (item != null && item.RectTrans != null)
                {
                    DOTween.Kill(GetPositionTweenId(item), false);
                    DOTween.Kill(GetScaleTweenId(item), false);
                }
            }
            activeItems.Clear();
        }
    }

    /// <summary>
    /// UI_EnemyListManager에서 사용하는 개별 아이템의 UI 요소 캐싱 및 텍스트 폰트 조절 헬퍼 클래스입니다.
    /// </summary>
    public class UI_EnemyListItem : MonoBehaviour
    {
        public RectTransform RectTrans { get; private set; }
        private TextMeshProUGUI[] nameTexts;
        private string displayName = string.Empty; // 실제 표시할 적 이름
        private bool isSelected = false;
        private bool isTop = false;     // 해당 사이드 리스트에서 최상단 여부
        private bool isLeft = true;

        // 마지막으로 설정된 anchoredPosition 목표값 캐시 (불필요한 트윈 재시작 방지)
        internal Vector2 lastTargetPos = new Vector2(float.NaN, float.NaN);

        // 원본 alignment 값을 보관하여 복원할 수 있게 처리
        private Dictionary<TextMeshProUGUI, TextAlignmentOptions> originalAlignments = new Dictionary<TextMeshProUGUI, TextAlignmentOptions>();

        public void Initialize(RectTransform rect, TextMeshProUGUI[] texts, bool isLeft, string enemyName)
        {
            RectTrans = rect;
            nameTexts = texts;
            this.isLeft = isLeft;
            this.displayName = enemyName;

            originalAlignments.Clear();
            if (nameTexts != null)
            {
                foreach (var txt in nameTexts)
                {
                    if (txt != null)
                    {
                        originalAlignments[txt] = txt.alignment;
                    }
                }
            }

            // isLeft가 확정된 직후 alignment 및 이름 즉시 적용
            UpdateTextStyles();
        }

        /// <summary>
        /// 적 이름을 갱신합니다. 한글화 등 중간에 바뀔 수 있으므로 매 프레임 호출될 수 있습니다.
        /// </summary>
        public void SetName(string name)
        {
            if (displayName == name) return;
            displayName = name;
            if (nameTexts == null) return;
            foreach (var txt in nameTexts)
            {
                if (txt != null) txt.text = displayName;
            }
        }

        public void SetSide(bool isLeft)
        {
            if (this.isLeft != isLeft)
            {
                this.isLeft = isLeft;
                UpdateTextStyles();
            }
        }

        public void SetSelected(bool select)
        {
            if (isSelected == select) return;
            isSelected = select;
            // 선택 상태는 정렬 우선순위(맨 위)로 표현됨 — 폰트 크기는 SetTopItem이 담당
        }

        /// <summary>
        /// 해당 사이드 리스트에서 0번(최상단)이면 true, 나머지는 false.
        /// 최상단: fontSize 40 / Bold, 나머지: fontSize 30 / Normal.
        /// </summary>
        public void SetTopItem(bool top)
        {
            if (isTop == top) return;
            isTop = top;
            UpdateTextStyles();
        }

        private void UpdateTextStyles()
        {
            if (nameTexts == null) return;

            foreach (var txt in nameTexts)
            {
                if (txt == null) continue;

                // 이름 항상 최신값으로 대입
                txt.text = displayName;
                // AutoSizing이 켜져있으면 fontSize 설정이 무시되므로 강제 해제
                txt.enableAutoSizing = false;

                // [정렬 조건] 우측 방향 컨테이너에서만 텍스트 좌측정렬(MidlineLeft), 좌측은 원래 정렬 유지
                if (!isLeft)
                {
                    txt.alignment = TextAlignmentOptions.MidlineLeft;
                }
                else
                {
                    if (originalAlignments.TryGetValue(txt, out var origAlign))
                        txt.alignment = origAlign;
                    else
                        txt.alignment = TextAlignmentOptions.MidlineRight;
                }

                // 최상단(isTop): fontSize 40, Bold, 불투명
                if (isTop)
                {
                    txt.fontSize = 40f;
                    txt.fontStyle = FontStyles.Bold;
                    Color color = txt.color;
                    color.a = 1f;
                    txt.color = color;
                }
                // 나머지: fontSize 30, Normal, 반투명
                else
                {
                    txt.fontSize = 30f;
                    txt.fontStyle = FontStyles.Normal;
                    Color color = txt.color;
                    color.a = 0.75f;
                    txt.color = color;
                }
            }
        }
    }
}
