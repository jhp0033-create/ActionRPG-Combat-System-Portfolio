using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ActionRPG.UI
{
    /// <summary>
    /// 개별 스킬 슬롯 프리팹에 부착되어 내부 UI 요소들을 연결해두는 컴포넌트입니다.
    /// UI_GamePlayOverlay가 이 프리팹을 복제하여 사용합니다.
    /// </summary>
    public class UI_SkillSlot : MonoBehaviour
    {
        [Header("UI Components")]
        public Button button;
        public Image buttonBgImage;
        public Image iconImage; 
        
        [Header("Cooldown & Stack")]
        public Image cooldownOverlay;
        // 큰 초 표시 (3,2,1)
        public TextMeshProUGUI cooldownSecondText;

        // 0.1초 단위 표시
        public TextMeshProUGUI cooldownDecimalText;

        // 점(.) 표시용 텍스트
        public TextMeshProUGUI cooldownDotText;

        [Tooltip("스택 구슬들을 묶어놓은 부모 컨테이너 오브젝트 (옵션)")]
        public GameObject stackContainer;
        
        [Tooltip("이미지(구슬 등)로 스택을 표시할 경우 순서대로 연결하세요 (옵션)")]
        public Image[] stackIcons;

        public TextMeshProUGUI skillNameText;
        [Header("Available Effect")]
        public ParticleSystem fireParticle;
        public Vector3 fireParticleOffset;
        private void Awake()
        {
            if (button == null)
            {
                Debug.LogError("[UI_SkillSlot] button 참조가 인스펙터에 할당되지 않았습니다.", this);
            }

            if (fireParticle != null)
            {
                // 슬롯별 파티클 오프셋을 초기 위치에 반영합니다.
                fireParticle.transform.localPosition = fireParticleOffset;

                fireParticle.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting
                );

                fireParticle.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Inspector에서 fireParticleOffset을 변경했을 때 에디터에서도 즉시 반영합니다.
        /// </summary>
        private void OnValidate()
        {
            if (fireParticle != null)
            {
                fireParticle.transform.localPosition = fireParticleOffset;
            }
        }
    }
}
