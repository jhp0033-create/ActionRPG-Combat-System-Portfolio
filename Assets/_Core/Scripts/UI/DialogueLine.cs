using UnityEngine;

namespace ActionRPG.UI
{
    /// <summary>
    /// 대화 한 줄의 정보를 담는 데이터 클래스입니다.
    /// 인스펙터에서 쉽게 수정할 수 있도록 Serializable로 선언합니다.
    /// </summary>
    [System.Serializable]
    public class DialogueLine
    {
        [TextArea(3, 5)]
        public string dialogueText;
    }
}
