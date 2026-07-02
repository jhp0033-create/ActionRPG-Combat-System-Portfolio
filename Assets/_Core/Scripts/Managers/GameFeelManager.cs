using UnityEngine;
using System.Collections;

namespace ActionRPG.Managers
{
    /// <summary>
    /// 게임 전반적인 조작감(Game Feel / Juice)을 담당하는 매니저입니다.
    /// 타격 시 화면이 미세하게 멈추는 역경직(Hit Stop) 등 게임의 손맛을 좌우하는 기능들을 제공합니다.
    /// </summary>
    public class GameFeelManager : MonoBehaviour
    {
        public static GameFeelManager Instance { get; private set; }

        private Coroutine hitStopCoroutine;
        private bool isHitStopping;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad는 필요하다면 추후 추가 (현재는 Scene 기반으로 작동하도록 설정)
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 적을 타격했을 때 아주 짧은 시간 동안 게임 속도를 극단적으로 낮춰 
        /// 진짜 살을 베는 듯한 끈적하고 묵직한 손맛(역경직)을 줍니다.
        /// </summary>
        /// <param name="duration">멈춰있는 시간 (보통 0.05 ~ 0.1초)</param>
        public void TriggerHitStop(float duration = 0.05f)
        {
            if (isHitStopping) return; // 이미 역경직 중이면 무시 (다단히트 시 버벅임 방지)

            if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            isHitStopping = true;
            
            // 게임 속도를 거의 정지에 가깝게(5%) 늦춥니다.
            // 완전히 0으로 만들면 물리 연산이나 UI 애니메이션이 고장날 수 있으므로 0.05로 둡니다.
            Time.timeScale = 0.05f; 
            
            // Time.timeScale이 늦춰졌으므로, 기다리는 시간은 현실 시간(Realtime) 기준으로 세야 합니다.
            yield return new WaitForSecondsRealtime(duration);
            
            // 원래 속도(100%)로 복구
            Time.timeScale = 1f;
            isHitStopping = false;
        }
    }
}
