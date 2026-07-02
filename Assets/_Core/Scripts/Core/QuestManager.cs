using System;
using UnityEngine;

namespace ActionRPG.Core
{
    /// <summary>
    /// 퀘스트 데이터와 진행 상태를 관리하고 UI에 변경 이벤트를 전달합니다.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }

        public event Action<QuestData> OnQuestUpdated;

        private QuestData currentQuestData;
        private QuestData runtimeQuestData;
        private int currentProgress = 0;

        private System.Collections.Generic.Dictionary<string, QuestData> questDatabase = new System.Collections.Generic.Dictionary<string, QuestData>();


        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                LoadQuestDatabase();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LoadQuestDatabase()
        {
            QuestData[] allQuests = Resources.LoadAll<QuestData>("QuestData");
            foreach (var q in allQuests)
            {
                if (!string.IsNullOrEmpty(q.questID))
                {
                    questDatabase[q.questID] = q;
                }
            }
        }

        private void Start()
        {
        }

        /// <summary>
        /// 오프닝 연출 이후 초기 퀘스트를 시작합니다.
        /// </summary>
        public void StartInitialQuest()
        {
            UpdateQuestByKey("Quest_Omen");
        }

        /// <summary>
        /// 퀘스트 식별자를 기준으로 퀘스트를 갱신합니다.
        /// </summary>
        public void UpdateQuestByKey(string questID)
        {
            if (questDatabase.TryGetValue(questID, out QuestData qData))
            {
                UpdateQuest(qData);
            }
            else
            {
                Debug.LogError($"[QuestManager] '{questID}' 키에 해당하는 퀘스트 데이터를 찾을 수 없습니다! Resources/QuestData 폴더에 파일이 있는지, ID가 올바른지 확인해주세요.");
            }
        }

        /// <summary>
        /// 현재 퀘스트 데이터를 교체하고 진행 상태를 초기화합니다.
        /// </summary>
        /// <param name="newQuest">새로운 퀘스트 데이터 에셋</param>
        public void UpdateQuest(QuestData newQuest)
        {
            currentQuestData = newQuest;
            currentProgress = 0;

            OnQuestUpdated?.Invoke(currentQuestData);
        }

        /// <summary>
        /// 사냥형 퀘스트 진행도를 갱신합니다.
        /// </summary>
        public void ReportEnemyKilled(string enemyID)
        {
            if (currentQuestData == null) return;

            if (currentQuestData.questType == QuestType.Hunt && currentProgress < currentQuestData.targetCount)
            {
                bool isTargetMatched = string.IsNullOrEmpty(currentQuestData.targetEnemyID) || 
                                       enemyID.StartsWith(currentQuestData.targetEnemyID);

                if (isTargetMatched)
                {
                    currentProgress++;
                    
                    OnQuestUpdated?.Invoke(currentQuestData);
                    
                }
            }
        }

        /// <summary>
        /// 현재 퀘스트의 진행도 표시 문자열을 반환합니다.
        /// </summary>
        public string GetQuestProgressText()
        {
            if (currentQuestData == null) return string.Empty;

            if (currentQuestData.questType == QuestType.Hunt)
            {
                return $"\n\n<color=#D4AA47>진행도: {currentProgress} / {currentQuestData.targetCount}</color>";
            }

            return string.Empty;
        }

        public void UpdateQuestText(string title, string description)
        {
            if (runtimeQuestData == null)
                runtimeQuestData = ScriptableObject.CreateInstance<QuestData>();

            runtimeQuestData.questTitle = title;
            runtimeQuestData.questDescription = description;
            UpdateQuest(runtimeQuestData);
        }
    }
}
