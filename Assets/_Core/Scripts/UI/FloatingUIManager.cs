using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace ActionRPG.UI
{
    /// <summary>
    /// 기존의 DamageTextManager, AlertBubbleManager, WorldHealthBarManager를 통합한
    /// 월드 공간 UI (데미지 텍스트, 알림 말풍선, 몬스터 체력바 등) 통합 매니저입니다.
    /// 모든 UI 요소의 오브젝트 풀링을 한 곳에서 중앙 집중식으로 관리합니다.
    /// </summary>
    public class FloatingUIManager : MonoBehaviour
    {
        public static FloatingUIManager Instance { get; private set; }

        [Header("Canvas & Container References")]
        [Tooltip("오브젝트들이 배치될 기본 캔버스 (World Space 또는 Screen Space)")]
        public RectTransform worldUICanvas;
        
        [Tooltip("데미지 텍스트가 담길 컨테이너 (없으면 Canvas에 바로 배치)")]
        public Transform damageTextContainer;
        
        [Tooltip("알림 말풍선이 담길 컨테이너")]
        public RectTransform alertBubbleContainer;
        
        [Tooltip("몬스터 HP 바가 담길 컨테이너")]
        public RectTransform hpBarContainer;

        [Header("Damage Text Pooling")]
        public GameObject damageTextPrefab;
        public int damageTextPoolSize = 30;
        public float damageTextHeightOffset = 0.4f;
        private Queue<DamageText> damageTextPool = new Queue<DamageText>();
        private Dictionary<Transform, List<DamageText>> activeDamageTexts = new Dictionary<Transform, List<DamageText>>();

        [Header("Damage Text Color Settings")]
        [Tooltip("기본 데미지 텍스트 색상 (일반 공격)")]
        public Color defaultDamageColor = Color.white;
        [Tooltip("퍼스트어택 데미지 색상")]
        public Color firstAttackColor = new Color(0.47f, 0.83f, 0.98f);
        [Tooltip("치명타 데미지 색상")]
        public Color criticalColor = new Color(1.0f, 0.70f, 0.35f);
        [Tooltip("피니시 데미지 색상 (마무리 공격)")]
        public Color finishColor = new Color(1.0f, 0.45f, 0.45f);

        [Header("Alert Bubble Pooling")]
        public GameObject alertBubblePrefab;
        public int alertBubblePoolSize = 10;
        public float alertBumpHeight = 0.5f;
        private Queue<AlertBubble> alertBubblePool = new Queue<AlertBubble>();
        private Dictionary<Transform, AlertBubble> activeBubbles = new Dictionary<Transform, AlertBubble>();

        [Header("World Health Bar Pooling")]
        public GameObject healthBarPrefab;
        public int healthBarPoolSize = 20;
        private Queue<WorldHealthBar> healthBarPool = new Queue<WorldHealthBar>();

        [Header("Damage Chunk Pooling")]
        public int damageChunkPoolSize = 20;
        private Queue<Image> damageChunkPool = new Queue<Image>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializePools();
        }

        private void InitializePools()
        {
            // 1. Damage Texts
            if (damageTextPrefab != null)
            {
                Transform parent = damageTextContainer != null ? damageTextContainer : (worldUICanvas != null ? worldUICanvas : transform);
                for (int i = 0; i < damageTextPoolSize; i++)
                {
                    DamageText dt = Instantiate(damageTextPrefab, parent).GetComponent<DamageText>();
                    if (dt != null)
                    {
                        dt.gameObject.SetActive(false);
                        damageTextPool.Enqueue(dt);
                    }
                }
            }

            // 2. Alert Bubbles
            if (alertBubblePrefab != null)
            {
                Transform parent = alertBubbleContainer != null ? alertBubbleContainer : (worldUICanvas != null ? worldUICanvas : transform);
                for (int i = 0; i < alertBubblePoolSize; i++)
                {
                    AlertBubble ab = Instantiate(alertBubblePrefab, parent).GetComponent<AlertBubble>();
                    if (ab != null)
                    {
                        ab.gameObject.SetActive(false);
                        alertBubblePool.Enqueue(ab);
                    }
                }
            }

            // 3. Health Bars
            if (healthBarPrefab != null)
            {
                Transform parent = hpBarContainer != null ? hpBarContainer : (worldUICanvas != null ? worldUICanvas : transform);
                for (int i = 0; i < healthBarPoolSize; i++)
                {
                    WorldHealthBar hb = Instantiate(healthBarPrefab, parent).GetComponent<WorldHealthBar>();
                    if (hb != null)
                    {
                        hb.gameObject.SetActive(false);
                        healthBarPool.Enqueue(hb);
                    }
                }
            }

            // 4. Damage Chunks (데미지를 입었을 때 체력바에서 깎여나가는 연출 조각)
            Transform chunkParent = hpBarContainer != null ? hpBarContainer : (worldUICanvas != null ? worldUICanvas : transform);
            for (int i = 0; i < damageChunkPoolSize; i++)
            {
                GameObject chunkObj = new GameObject("DamageChunk_Pool", typeof(RectTransform), typeof(Image));
                chunkObj.transform.SetParent(chunkParent, false);
                chunkObj.SetActive(false);
                damageChunkPool.Enqueue(chunkObj.GetComponent<Image>());
            }
        }

        #region Damage Chunk API
        
        /// <summary>
        /// 풀에서 데미지 조각(Image)을 꺼내어 타겟 컨테이너의 자식으로 배치합니다.
        /// </summary>
        public Image GetDamageChunk(Transform parent)
        {
            Image chunk = null;
            int currentPoolSize = damageChunkPool.Count;

            for (int i = 0; i < currentPoolSize; i++)
            {
                var candidate = damageChunkPool.Dequeue();
                damageChunkPool.Enqueue(candidate);

                if (candidate != null && !candidate.gameObject.activeInHierarchy)
                {
                    chunk = candidate;
                    break;
                }
            }

            if (chunk == null)
            {
                // 풀이 부족하면 새로 생성하여 풀에 추가
                Transform fallbackParent = hpBarContainer != null ? hpBarContainer : (worldUICanvas != null ? worldUICanvas : transform);
                GameObject chunkObj = new GameObject("DamageChunk_Pool", typeof(RectTransform), typeof(Image));
                chunkObj.transform.SetParent(fallbackParent, false);
                chunk = chunkObj.GetComponent<Image>();
                damageChunkPool.Enqueue(chunk);
            }

            chunk.transform.SetParent(parent, false);
            chunk.gameObject.SetActive(true);
            return chunk;
        }

        /// <summary>
        /// 사용이 끝난 데미지 조각을 풀로 되돌려 보냅니다.
        /// </summary>
        public void ReturnDamageChunk(Image chunk)
        {
            if (chunk != null)
            {
                chunk.gameObject.SetActive(false);
                Transform parent = hpBarContainer != null ? hpBarContainer : (worldUICanvas != null ? worldUICanvas : transform);
                chunk.transform.SetParent(parent, false);
            }
        }

        #endregion

        #region Damage Text API

        public void SpawnDamageText(Transform target, Vector3 position, float damageAmount, string statusText = "")
        {
            Color textColor = defaultDamageColor;
            
            if (statusText.Contains("피니시"))
            {
                textColor = finishColor;
            }
            else if (statusText == "퍼스트어택" || statusText == "퍼스트 어택")
            {
                textColor = firstAttackColor;
            }
            else if (statusText == "치명타" || statusText.Contains("치명"))
            {
                textColor = criticalColor;
            }

            SpawnDamageText(target, position, Mathf.RoundToInt(damageAmount).ToString(), textColor, statusText);
        }

        public void SpawnDamageText(Transform target, Vector3 position, string textContent, Color textColor, string statusText = "")
        {
            if (damageTextPrefab == null) return;

            DamageText textToSpawn = null;
            int currentPoolSize = damageTextPool.Count;

            for (int i = 0; i < currentPoolSize; i++)
            {
                var candidate = damageTextPool.Dequeue();
                damageTextPool.Enqueue(candidate);

                if (!candidate.gameObject.activeInHierarchy)
                {
                    textToSpawn = candidate;
                    break;
                }
            }

            if (textToSpawn == null)
            {
                Transform parent = damageTextContainer != null ? damageTextContainer : (worldUICanvas != null ? worldUICanvas : transform);
                textToSpawn = Instantiate(damageTextPrefab, parent).GetComponent<DamageText>();
                if (textToSpawn != null) damageTextPool.Enqueue(textToSpawn);
            }

            if (textToSpawn != null)
            {
                textToSpawn.gameObject.SetActive(true);
                textToSpawn.heightOffset = this.damageTextHeightOffset;
                textToSpawn.Initialize(target, position, textContent, textColor, statusText);

                // 동일한 타겟에 띄워진 데미지 텍스트들을 위로 밀어 올리기 (Stacking/Bumping)
                if (target != null)
                {
                    if (!activeDamageTexts.ContainsKey(target))
                    {
                        activeDamageTexts[target] = new List<DamageText>();
                    }

                    float bumpAmount = textToSpawn.isLargeText ? textToSpawn.largeBumpHeight : textToSpawn.normalBumpHeight;

                    List<DamageText> list = activeDamageTexts[target];
                    for (int i = list.Count - 1; i >= 0; i--)
                    {
                        if (list[i] == null || !list[i].gameObject.activeInHierarchy)
                        {
                            list.RemoveAt(i);
                        }
                        else
                        {
                            list[i].BumpUp(bumpAmount);
                        }
                    }
                    
                    list.Add(textToSpawn);
                }
            }
        }

        #endregion

        #region Alert Bubble API

        private Dictionary<Transform, float> alertBumps = new Dictionary<Transform, float>();

        public float GetAlertBumpHeight(Transform target)
        {
            if (target == null) return 0f;
            return alertBumps.TryGetValue(target, out float val) ? val : 0f;
        }

        public AlertBubble ShowAlert(Transform target)
        {
            if (target == null || alertBubblePrefab == null) return null;

            if (activeBubbles.TryGetValue(target, out AlertBubble existingBubble))
            {
                if (existingBubble.gameObject.activeInHierarchy) return existingBubble;
                else activeBubbles.Remove(target);
            }

            AlertBubble bubble = null;
            int currentPoolSize = alertBubblePool.Count;

            for (int i = 0; i < currentPoolSize; i++)
            {
                var candidate = alertBubblePool.Dequeue();
                alertBubblePool.Enqueue(candidate);

                if (!candidate.gameObject.activeInHierarchy)
                {
                    bubble = candidate;
                    break;
                }
            }

            if (bubble == null)
            {
                Transform parent = alertBubbleContainer != null ? alertBubbleContainer : (worldUICanvas != null ? worldUICanvas : transform);
                bubble = Instantiate(alertBubblePrefab, parent).GetComponent<AlertBubble>();
                if (bubble != null) alertBubblePool.Enqueue(bubble);
            }

            if (bubble != null)
            {
                activeBubbles[target] = bubble;
                bubble.gameObject.SetActive(true); 
                bubble.ShowAlert(target);

                DG.Tweening.DOTween.Kill("AlertBump_" + target.GetInstanceID());
                if (!alertBumps.ContainsKey(target)) alertBumps[target] = 0f;
                DG.Tweening.DOTween.To(() => alertBumps[target], x => alertBumps[target] = x, alertBumpHeight, 0.4f)
                    .SetId("AlertBump_" + target.GetInstanceID()).SetEase(DG.Tweening.Ease.OutBack);
            }
            return bubble;
        }

        public void ReturnBubble(AlertBubble bubble, Transform target)
        {
            if (bubble == null) return;
            if (target != null && activeBubbles.ContainsKey(target))
            {
                activeBubbles.Remove(target);

                DG.Tweening.DOTween.Kill("AlertBump_" + target.GetInstanceID());
                DG.Tweening.DOTween.To(() => alertBumps.TryGetValue(target, out float v) ? v : 0f, x => alertBumps[target] = x, 0f, 0.2f)
                    .SetId("AlertBump_" + target.GetInstanceID()).SetEase(DG.Tweening.Ease.InCubic);
            }
            bubble.gameObject.SetActive(false);
        }

        public bool IsBubbleActive(Transform target)
        {
            return target != null && activeBubbles.ContainsKey(target) && activeBubbles[target] != null && activeBubbles[target].gameObject.activeInHierarchy;
        }

        private void LateUpdate()
        {
            if (activeBubbles.Count <= 1) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;
            Vector3 camPos = mainCam.transform.position;

            var sortedBubbles = new List<KeyValuePair<Transform, AlertBubble>>(activeBubbles);
            sortedBubbles.Sort((a, b) =>
            {
                float distA = Vector3.SqrMagnitude(a.Key.position - camPos);
                float distB = Vector3.SqrMagnitude(b.Key.position - camPos);
                return distB.CompareTo(distA); // 카메라와 더 가까운 것을 맨 위로 렌더링하도록 정렬
            });

            for (int i = 0; i < sortedBubbles.Count; i++)
            {
                if (sortedBubbles[i].Value != null)
                {
                    sortedBubbles[i].Value.transform.SetSiblingIndex(i);
                }
            }
        }

        #endregion

        #region World Health Bar API

        public WorldHealthBar GetHealthBar(Transform target, string enemyName, float maxHealth)
        {
            if (healthBarPrefab == null) return null;

            WorldHealthBar bar = null;
            int currentPoolSize = healthBarPool.Count;

            for (int i = 0; i < currentPoolSize; i++)
            {
                var candidate = healthBarPool.Dequeue();
                healthBarPool.Enqueue(candidate);

                if (!candidate.gameObject.activeInHierarchy)
                {
                    bar = candidate;
                    break;
                }
            }

            if (bar == null)
            {
                Transform parent = hpBarContainer != null ? hpBarContainer : (worldUICanvas != null ? worldUICanvas : transform);
                bar = Instantiate(healthBarPrefab, parent).GetComponent<WorldHealthBar>();
                if (bar != null) healthBarPool.Enqueue(bar);
            }

            if (bar != null)
            {
                bar.gameObject.SetActive(true);
                bar.Initialize(target, enemyName, maxHealth);
            }
            return bar;
        }

        public void ReturnHealthBar(WorldHealthBar bar)
        {
            if (bar == null) return;
            bar.gameObject.SetActive(false);
        }

        #endregion
    }
}
