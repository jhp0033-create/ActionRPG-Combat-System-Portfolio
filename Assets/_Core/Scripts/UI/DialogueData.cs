using UnityEngine;

namespace ActionRPG.UI
{
    [CreateAssetMenu(fileName = "NewDialogueData", menuName = "ActionRPG/Data/DialogueData", order = 2)]
    public class DialogueData : ScriptableObject
    {
        [Header("대사 식별 코드 (예: Dialogue_Omen)")]
        public string dialogueID;

        public DialogueLine[] lines;
    }
}
