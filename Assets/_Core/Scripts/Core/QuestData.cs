using UnityEngine;

namespace ActionRPG.Core
{
    public enum QuestType
    {
        None = 0,    // 단순 대화/이동 퀘스트 (목표 수치 없음)
        Hunt = 1,    // 몬스터 사냥
        Collect = 2  // 아이템 수집 등 (추후 확장용)
    }

    /// <summary>
    /// 퀘스트의 텍스트 데이터 및 달성 목표 로직을 저장하는 데이터 컨테이너입니다.
    /// 에디터의 Create 메뉴에서 마우스 우클릭으로 생성할 수 있습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewQuestData", menuName = "ActionRPG/Data/QuestData", order = 1)]
    public class QuestData : ScriptableObject
    {
        [Header("퀘스트 식별 코드 (예: Quest_Omen)")]
        public string questID;

        [Header("퀘스트 타이틀")]
        public string questTitle;

        [Header("퀘스트 목표/설명 (고정 텍스트 부분)")]
        [TextArea(1, 3)]
        public string questDescription;

        [Header("진행 로직 설정 (선택사항)")]
        public QuestType questType = QuestType.None;
        
        [Tooltip("사냥/수집 대상의 ID 혹은 이름 (예: Skeleton)")]
        public string targetEnemyID;
        
        [Tooltip("달성해야 할 목표 수치 (예: 100)")]
        public int targetCount;
    }
}
