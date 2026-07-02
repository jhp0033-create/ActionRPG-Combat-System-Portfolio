using UnityEngine;
using TMPro;
using System.Collections;

namespace ActionRPG.UI
{
    /// <summary>
    /// 게임 내 어디서든 텍스트 로그(전투 피드백, 시스템 메시지 등)를 화면에 띄워주는 싱글톤 매니저입니다.
    /// 화면 하단의 대화창(Dialog) 틀을 재활용하여 UI 밸런스를 맞추는 용도로 사용됩니다.
    /// </summary>
    public class UI_SystemLogManager : MonoBehaviour
    {
        public static UI_SystemLogManager Instance { get; private set; }

        [Header("UI Components")]
        [Tooltip("시스템 로그가 표시될 TextMeshPro 컴포넌트")]
        public TextMeshProUGUI logText;
        
        [Tooltip("텍스트의 배경이 되는 패널 이미지 (선택 사항)")]
        public CanvasGroup panelCanvasGroup;

        [Header("Settings")]
        [Tooltip("로그가 화면에 머무는 시간(초)")]
        public float displayDuration = 3f;
        [Tooltip("로그가 서서히 사라지는(Fade Out) 데 걸리는 시간(초)")]
        public float fadeDuration = 1f;

        private Coroutine currentFadeCoroutine;

        private void Awake()
        {
            // 싱글톤 세팅
            if (Instance == null)
            {
                Instance = this;
                // 필요하다면 씬 전환 시에도 유지
                // DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 초기에는 텍스트를 비워둡니다.
            if (logText != null)
            {
                logText.text = "";
                logText.alpha = 0f;
            }
            
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// 시스템 로그를 출력합니다. 게임 내 어디서든 UI_SystemLogManager.Instance.ShowLog("메시지"); 형태로 호출하세요.
        /// </summary>
        /// <param name="message">출력할 메시지</param>
        public void ShowLog(string message)
        {
            if (logText == null)
            {
                Debug.LogWarning($"[SystemLog] UI Text가 연결되지 않았습니다. 메시지: {message}");
                return;
            }

            // 만약 기존에 진행 중인 페이드아웃 코루틴이 있다면 중지하고 텍스트를 즉각 교체합니다.
            if (currentFadeCoroutine != null)
            {
                StopCoroutine(currentFadeCoroutine);
            }

            logText.text = message;
            
            // 텍스트와 패널의 투명도를 즉시 100%로 복구
            logText.alpha = 1f;
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 1f;
            }

            // 대기 후 페이드아웃 시작
            currentFadeCoroutine = StartCoroutine(FadeOutRoutine());
        }

        private IEnumerator FadeOutRoutine()
        {
            // 지정된 시간만큼 텍스트를 온전히 표시하며 대기
            yield return new WaitForSeconds(displayDuration);

            float elapsedTime = 0f;
            float startAlpha = 1f;

            // fadeDuration 동안 서서히 투명도를 0으로 깎아냅니다.
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float newAlpha = Mathf.Lerp(startAlpha, 0f, elapsedTime / fadeDuration);
                
                logText.alpha = newAlpha;
                if (panelCanvasGroup != null)
                {
                    panelCanvasGroup.alpha = newAlpha;
                }

                yield return null;
            }

            // 페이드아웃이 완전히 끝나면 완전히 투명하게 처리하고 텍스트를 비웁니다.
            logText.alpha = 0f;
            logText.text = "";
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
            }

            currentFadeCoroutine = null;
        }
    }
}
