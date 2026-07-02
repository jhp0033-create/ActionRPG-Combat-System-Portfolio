using UnityEngine;

namespace ActionRPG.Utils
{
    /// <summary>
    /// 런타임 캐릭터 Animator 참조를 보관하는 보조 컴포넌트입니다.
    /// 예전 에디터 미리보기 로직은 저장/도메인 리로드 때 Animator를 강제 갱신해 에디터를 무겁게 만들 수 있어 비활성화했습니다.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class EditorAnimatorPreview : MonoBehaviour
    {
        private Animator animator;

        private void OnEnable()
        {
            animator = GetComponent<Animator>();
        }

    }
}
