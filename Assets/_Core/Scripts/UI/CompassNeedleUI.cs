using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

namespace ActionRPG.UI
{
    /// <summary>
    /// 나침반 바늘의 회전, 흔들림, 셰이더 기반 블러 값을 제어합니다.
    /// </summary>
    public class CompassNeedleUI : MonoBehaviour
    {
        [Header("Init Animation (기승)")]
        [Tooltip("처음 켜질 때 바늘이 도는 시간")]
        public float initDuration = 2.0f;
        [Tooltip("처음 켜질 때 몇 바퀴 회전할 것인가?")]
        public int initSpins = 3;

        [Header("Idle Noise (전결 - 동적)")]
        [Tooltip("최초에 뒤로 살짝 당겨지는 각도 (텐션 모으기)")]
        public float startSwayAngle = -40f; 
        [Tooltip("뒤로 당겨지는 시간")]
        public float startSwayDuration = 0.4f; 
        [Tooltip("잔잔해진 후 살짝 왔다갔다 하는 기본 각도")]
        public float idleSwayAngle = 3f; 
        [Tooltip("잔잔해진 후 스윙 시간")]
        public float idleSwayDuration = 4f; 

        [Tooltip("가끔씩 확 튀는 노이즈의 최대 강도")]
        public float maxSpikeStrength = 8f;

        [Header("Shader Motion Blur (쉐이더 제어)")]
        [Tooltip("얼마나 빠르게 돌 때 블러가 최대로 켜질지 기준 속도")]
        public float maxBlurSpeed = 1000f;
        [Tooltip("쉐이더 그래프의 _BlurIntensity 최대치")]
        public float maxBlurIntensity = 5f;

        [Header("VFX (시각 효과)")]
        [Tooltip("3바퀴 돌 때만 뿜어낼 파티클 시스템 (트레일이나 빛가루 효과용)")]
        public ParticleSystem spinParticle;

        [Header("Test")]
        [Tooltip("에디터에서 체크를 껐다 켜면 기승전결 애니메이션이 다시 재생됩니다.")]
        public bool testTrigger = false;
        private bool prevTestTrigger;

        private RectTransform needleRect;
        private Sequence compassSequence;

        // 쉐이더 제어용
        private Image needleImage;
        private Material needleMaterial;
        private float lastRotationZ;
        private float currentBlurAmount = 0f; // 물리적 관성(Lerp) 적용을 위한 변수

        // GC(힙 할당) 최적화를 위한 코루틴 프리 타이머 변수들
        private bool isIdlePhase = false;
        private float noiseTimer = 0f;

        private void Awake()
        {
            needleRect = GetComponent<RectTransform>();
            needleImage = GetComponent<Image>();

            // UI 머티리얼을 복제해서 다른 나침반과 값이 섞이지 않게 분리
            if (needleImage != null && needleImage.material != null)
            {
                needleMaterial = new Material(needleImage.material);
                needleImage.material = needleMaterial;

                // 인스펙터 블러 강도와 셰이더 파라미터를 초기화 시점에 동기화합니다.
                needleMaterial.SetFloat("_MaxBlurIntensity", maxBlurIntensity);
            }
        }

        private void OnEnable()
        {
            if (needleRect == null) return;
            PlayCompassAnimation();
        }

        private void Update()
        {
            // 에디터 테스트용 트리거
            if (testTrigger != prevTestTrigger)
            {
                prevTestTrigger = testTrigger;
                PlayCompassAnimation();
            }

            // 반복 흔들림은 코루틴 대신 Update 타이머로 처리합니다.
            if (isIdlePhase && needleRect != null)
            {
                noiseTimer -= Time.deltaTime;
                if (noiseTimer <= 0f)
                {
                    TriggerDynamicNoise();
                    noiseTimer = Random.Range(0.1f, 1.0f);
                }
            }

            // 쉐이더 기반 다이나믹 모션 블러 제어
            ControlShaderMotionBlur();
        }

        private void ControlShaderMotionBlur()
        {
            if (needleRect == null || needleMaterial == null) return;

            // 프레임당 회전 각도 기반으로 블러 강도를 계산합니다.
            float currentRotZ = needleRect.eulerAngles.z;
            float angularVelocity = Mathf.Abs(Mathf.DeltaAngle(lastRotationZ, currentRotZ)) / Time.deltaTime;

            // 빠른 회전 구간에서 블러가 더 크게 반응하도록 비선형 곡선을 사용합니다.
            float normalizedSpeed = Mathf.Clamp01(angularVelocity / maxBlurSpeed);
            float targetBlur = Mathf.Pow(normalizedSpeed, 1.5f) * maxBlurIntensity;

            // 저속 회전 구간에는 데드존을 적용해 미세한 번짐을 줄입니다.
            if (targetBlur < 0.15f) targetBlur = 0f;

            // 블러 변화는 Lerp로 보간해 급격한 튐을 줄입니다.
            currentBlurAmount = Mathf.Lerp(currentBlurAmount, targetBlur, Time.deltaTime * 12f);

            // 셰이더에 최종 블러 값을 전달합니다.
            needleMaterial.SetFloat("_BlurIntensity", currentBlurAmount);

            lastRotationZ = currentRotZ;
        }

        private void PlayCompassAnimation()
        {
            if (needleRect == null) return;

            // 시퀀스 초기화
            if (compassSequence != null && compassSequence.IsActive())
            {
                compassSequence.Kill();
            }

            // 시작 각도 리셋 (텐션 모으기 전의 기본 위치, 또는 0도에서 시작)
            needleRect.localRotation = Quaternion.identity;

            compassSequence = DOTween.Sequence();
            isIdlePhase = false;

            // [1단계: 기] 뒤로 쭈우우욱~ 넓고 천천히 당기면서 텐션을 끈적하게 모음
            compassSequence.Append(needleRect.DOLocalRotate(new Vector3(0, 0, startSwayAngle), startSwayDuration)
                .SetEase(Ease.InOutQuad));

            // [2단계: 승] 폭발적으로 3바퀴 스핀
            // 스핀 시작 시점에 파티클 재생
            compassSequence.AppendCallback(() => 
            {
                if (spinParticle != null) spinParticle.Play();
            });

            compassSequence.Append(
                needleRect.DOLocalRotate(new Vector3(0, 0, -360f * initSpins), initDuration, RotateMode.FastBeyond360)
                .SetRelative(true)
                .SetEase(Ease.OutElastic, 1.2f, 0.6f) 
            );

            // [2단계: 전결] 자리를 잡은 후 Idle Phase 진입
            compassSequence.OnComplete(() =>
            {
                // 회전이 멈추더라도 파티클(트레일 등)이 루핑되도록 강제 정지 로직 주석 처리
                // if (spinParticle != null) spinParticle.Stop();

                // 기본 베이스 스윙 (무한 반복)
                needleRect.localRotation = Quaternion.Euler(0, 0, -idleSwayAngle);
                needleRect.DOLocalRotate(new Vector3(0, 0, idleSwayAngle), idleSwayDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);

                // 코루틴 없이 Update 타이머를 통한 동적 노이즈 가동 시작
                isIdlePhase = true;
                noiseTimer = Random.Range(0.1f, 0.5f); // 첫 노이즈는 빠르게 시작
            });
        }

        /// <summary>
        /// 코루틴 대신 Update에서 호출되어 가비지 컬렉터(GC) 부하를 전혀 주지 않는 노이즈 트리거
        /// </summary>
        private void TriggerDynamicNoise()
        {
            // 기존 펀치 로테이션이 있다면 덮어쓰기 위해 Kill (베이스 스윙은 유지됨)
            needleRect.DOComplete(); // 진행중인 펀치만 빠르게 완료

            // 20% 확률로 크게 튀고, 80% 확률로 살짝 떨림
            float currentStrength = Random.value > 0.8f ? maxSpikeStrength : maxSpikeStrength * 0.3f;
            float duration = Random.Range(0.1f, 0.3f); // 템포 상승 (빠르게 튐)

            needleRect.DOPunchRotation(new Vector3(0, 0, currentStrength), duration, vibrato: 10, elasticity: 0.5f);
        }

        private void OnDisable()
        {
            isIdlePhase = false;
            if (spinParticle != null) spinParticle.Stop();
            if (needleRect != null) DOTween.Kill(needleRect);
            if (compassSequence != null) compassSequence.Kill();
        }
    }
}
