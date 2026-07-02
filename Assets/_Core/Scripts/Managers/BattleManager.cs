using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ActionRPG.Enemy;
using ActionRPG.Managers;
using ActionRPG.UI;

namespace ActionRPG.Core
{
    /// <summary>
    /// 전투 데모의 진행 상태를 관리합니다.
    /// 적 AI는 사망 사실만 알리고, 처치 수/재소환/퀘스트 진행은 이 매니저가 책임집니다.
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        public static BattleManager Instance { get; private set; }

    
        [Header("Battle Flow")]
        [SerializeField] private bool startBattleOnStart = false;
        [Tooltip("퀘스트 완료를 위해 처치해야 할 적의 수")]
        [SerializeField] private int targetKillCount = 30;
        [SerializeField] private bool respawnUntilTargetReached = true;
        [SerializeField] private int maxConcurrentEnemies = 5;
        [SerializeField] private float replacementCheckInterval = 0.1f;
        [SerializeField] public AreaSpawner battleSpawner;

        [Header("Quest Text")]
        [SerializeField] private string killQuestTitle = "마물 소탕 작전";
        [SerializeField] private string killQuestFormat = "<color=#D4AA47>몰려드는 몬스터 처치</color> {0}/{1}";
        [SerializeField] private string completeQuestTitle = "소탕 작전 성공";
        [SerializeField] private string completeQuestDescription = "해당 구역의 위협적인 마물들을 모두 섬멸했습니다.";

        [Header("Quest Complete Presentation")]
        [Tooltip("퀘스트 완료 팝업 오브젝트입니다.")]
        [SerializeField] private QuestCompletePopupPresenter questCompletePopup;
        [Tooltip("퀘스트 완료 직후 자동 재생할 대사 ID입니다.")]
        [SerializeField] private string questCompleteDialogueID = "Dialogue_QuestClear";
        [SerializeField] private float questCompleteDialogueDelay = 5.0f;
        [SerializeField] private float questCompleteDialogueLineHoldDuration = 1.2f;
        [SerializeField] private float questCompletePopupDelay = 0.2f;

        [SerializeField] private DemoOpeningDirector openingDirector;

        private bool isBattleActive;
        private bool isBattleComplete;
        private int defeatedEnemyCount;
        private readonly Queue<string> pendingReplacementNames = new Queue<string>();
        private Coroutine replacementRoutine;
        public int TargetKillCount => Mathf.Max(0, targetKillCount);

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (battleSpawner == null)
                battleSpawner = FindFirstObjectByType<AreaSpawner>();

            if (startBattleOnStart)
                StartBattle();
        }

        public void StartBattle()
        {
            if (isBattleActive) return;

            isBattleActive = true;
            isBattleComplete = false;
            defeatedEnemyCount = 0;
            pendingReplacementNames.Clear();
            UpdateKillQuest();
        }

        public void EndBattle(bool isPlayerWon)
        {
            isBattleActive = false;
            isBattleComplete = isPlayerWon;
            pendingReplacementNames.Clear();

            if (replacementRoutine != null)
            {
                StopCoroutine(replacementRoutine);
                replacementRoutine = null;
            }

            if (isPlayerWon)
            {
                if (QuestManager.Instance != null)
                {
                    QuestManager.Instance.UpdateQuestText(completeQuestTitle, completeQuestDescription);
                }

                StartCoroutine(PlayQuestCompletePresentation());
            }
        }

        private IEnumerator PlayQuestCompletePresentation()
        {
            if (questCompleteDialogueDelay > 0f)
            {
                yield return new WaitForSeconds(questCompleteDialogueDelay);
            }

            if (!string.IsNullOrEmpty(questCompleteDialogueID) && ActionRPG.UI.UI_DialogueManager.Instance != null)
            {
                bool dialogueDone = false;
                void HandleDialogueFinished() => dialogueDone = true;

                ActionRPG.UI.UI_DialogueManager.Instance.OnDialogueFinished += HandleDialogueFinished;

                Transform playerTarget = null;
                ActionRPG.Player.NetworkPlayerController player = FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
                if (player != null)
                {
                    playerTarget = player.transform;
                }

                ActionRPG.UI.UI_DialogueManager.Instance.StartDialogueByKey(
                    questCompleteDialogueID,
                    autoAdvanceLines: true,
                    lineHoldDuration: questCompleteDialogueLineHoldDuration,
                    target: playerTarget);

                while (!dialogueDone &&
                       ActionRPG.UI.UI_DialogueManager.Instance != null &&
                       ActionRPG.UI.UI_DialogueManager.Instance.IsDialogueActive)
                {
                    yield return null;
                }

                if (ActionRPG.UI.UI_DialogueManager.Instance != null)
                {
                    ActionRPG.UI.UI_DialogueManager.Instance.OnDialogueFinished -= HandleDialogueFinished;
                }
            }

            if (questCompletePopupDelay > 0f)
            {
                yield return new WaitForSeconds(questCompletePopupDelay);
            }

            if (ActionRPG.UI.UI_SystemLogManager.Instance != null)
            {
                ActionRPG.UI.UI_SystemLogManager.Instance.ShowLog($"<size=150%><color=#FFD700>QUEST CLEAR</color></size>\n{completeQuestTitle}");
            }

            if (questCompletePopup != null)
            {
                questCompletePopup.Show();
            }
        }

        public void NotifyEnemyKilled(EnemyController enemy, Vector3 spawnOrigin, string inheritedName)
        {
            if (!isBattleActive || isBattleComplete || enemy == null) return;

            defeatedEnemyCount = Mathf.Clamp(defeatedEnemyCount + 1, 0, targetKillCount);
            UpdateKillQuest();

            if (defeatedEnemyCount >= targetKillCount)
            {
                EndBattle(true);
                return;
            }

            if (respawnUntilTargetReached)
            {
                QueueReplacementEnemy(inheritedName);
            }
        }

        private void QueueReplacementEnemy(string inheritedName)
        {
            if (!CanReserveReplacementEnemy())
            {
                return;
            }

            pendingReplacementNames.Enqueue(inheritedName);

            if (replacementRoutine == null)
            {
                replacementRoutine = StartCoroutine(SpawnReplacementWhenSlotAvailable());
            }
        }

        private IEnumerator SpawnReplacementWhenSlotAvailable()
        {
            while (pendingReplacementNames.Count > 0 && isBattleActive && !isBattleComplete)
            {
                if (!CanSpawnReplacementEnemyNow())
                {
                    pendingReplacementNames.Clear();
                    break;
                }

                if (GetActiveEnemyCount() >= maxConcurrentEnemies)
                {
                    yield return new WaitForSeconds(replacementCheckInterval);
                    continue;
                }

                string inheritedName = pendingReplacementNames.Dequeue();
                if (!SpawnReplacementEnemy(inheritedName))
                {
                    pendingReplacementNames.Enqueue(inheritedName);
                    yield return new WaitForSeconds(replacementCheckInterval);
                    continue;
                }

                yield return null;
            }

            replacementRoutine = null;
        }

        private int GetActiveEnemyCount()
        {
            return EnemyManager.Instance != null
                ? EnemyManager.Instance.ActiveEnemies.Count
                : 0;
        }

        private int GetLivingActiveEnemyCount()
        {
            if (EnemyManager.Instance == null)
            {
                return 0;
            }

            int count = 0;
            foreach (GameObject enemyObject in EnemyManager.Instance.ActiveEnemies)
            {
                if (enemyObject == null || !enemyObject.activeInHierarchy)
                {
                    continue;
                }

                EnemyController enemy = enemyObject.GetComponent<EnemyController>();
                if (enemy == null)
                {
                    enemy = enemyObject.GetComponentInChildren<EnemyController>();
                }

                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private bool CanReserveReplacementEnemy()
        {
            return defeatedEnemyCount + GetLivingActiveEnemyCount() + pendingReplacementNames.Count < TargetKillCount;
        }

        private bool CanSpawnReplacementEnemyNow()
        {
            return defeatedEnemyCount + GetLivingActiveEnemyCount() < TargetKillCount;
        }

        private bool SpawnReplacementEnemy(string inheritedName)
        {
            if (battleSpawner == null)
                battleSpawner = FindFirstObjectByType<AreaSpawner>();

            if (battleSpawner == null)
            {
                Debug.LogWarning("[BattleManager] AreaSpawner를 찾지 못해 다음 적을 생성할 수 없습니다.");
                return false;
            }

            return battleSpawner.SpawnRandomReplacement(inheritedName);
        }

        private void UpdateKillQuest()
        {
            if (QuestManager.Instance == null) return;

            string description = string.Format(killQuestFormat, defeatedEnemyCount, targetKillCount);
            QuestManager.Instance.UpdateQuestText(killQuestTitle, description);
        }

        public void TriggerHitStop(float duration = 0.1f, float timeScale = 0.1f)
        {
            if (!isBattleActive) return;
            StartCoroutine(HitStopCoroutine(duration, timeScale));
        }

        private IEnumerator HitStopCoroutine(float duration, float timeScale)
        {
            float originalTimeScale = Time.timeScale;
            Time.timeScale = timeScale;
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = originalTimeScale;
        }

        public void TriggerCameraShake(float intensity, float duration)
        {
            if (!isBattleActive) return;
        }
    }
}
