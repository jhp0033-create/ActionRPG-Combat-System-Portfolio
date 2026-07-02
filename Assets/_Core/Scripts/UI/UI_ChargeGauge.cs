using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    /// <summary>
    /// 장판 셰이더(보로노이/단색 등)의 연출을 인게임 스킬 사용 없이
    /// 화면에 항상 띄워놓고 반복적으로 확인하기 위한 프리뷰 스크립트입니다.
    /// 루트 오브젝트에 스크립트를 달고, 하위에 배경/게이지 2개의 이미지를 연결해 프리팹으로 만드세요.
    /// </summary>
    public class UI_ChargeGauge : MonoBehaviour
    {
        [Header("Child Elements (프리팹 자식 오브젝트)")]
        [Tooltip("배경/가이드라인 역할을 하는 꽉 찬 장판 이미지 (미입력 시 무시)")]
        public Image baseImage;
        
        [Tooltip("시간에 따라 차오르는 게이지 이미지 (미입력 시 무시)")]
        public Image gaugeImage;

        [Header("Preview Settings")]
        [Tooltip("게이지가 0에서 100%까지 차오르는 데 걸리는 시간(초)")]
        public float chargeTime = 2f;
        
        [Tooltip("100% 도달 후 0으로 리셋되기 전 대기 시간")]
        public float delayBeforeReset = 0.5f;

        [Tooltip("위치를 따라다닐 대상 (비워두면 'Player' 태그를 자동 검색합니다)")]
        public Transform targetTransform;
        
        [Tooltip("방향(회전)의 기준이 될 시전자 (비워두면 targetTransform의 방향을 따릅니다)")]
        public Transform casterTransform;

        [Tooltip("바닥에 묻히지 않도록 약간 띄워주는 Y축 오프셋")]
        public Vector3 offset = new Vector3(0, 0.05f, 0);

        [Tooltip("시전자(casterTransform) 기준 앞쪽으로 얼마나 이동할지 (0이면 발밑, 양수면 앞으로)")]
        public float forwardOffset = 0f;

        [Tooltip("시전자 Yaw에 맞춰 게이지/셰이더 방향을 회전")]
        public bool syncRotationToCaster = true;

        [Header("Shader Parameters (Runtime Preview)")]
        [Tooltip("피자 조각 게이지의 크기 (0 ~ 1)")]
        [Range(0f, 1f)]
        public float testRadialFill = 1.0f;
        
        [Tooltip("피자 조각이 시작되는 각도 오프셋 (-360 ~ 360)")]
        [Range(-360f, 360f)]
        public float testAngleOffset = 0f;

        [Header("Animation Settings")]
        [Tooltip("스페이스바를 눌러 프리뷰를 실행할지 여부")]
        public bool useSpacebarTrigger = true;

        [Tooltip("게이지가 차오르는 속도 곡선 (오른쪽으로 갈수록 가파르면 끝에 확 차오름)")]
        public AnimationCurve fillCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        private Material baseMaterial;
        private Material gaugeMaterial;
        private float timer = 0f;
        private bool isPlaying = false;
        private Quaternion authoredLocalRotation;

        void Awake()
        {
            // 프리팹 원본이 가진 고유 회전(바닥에 눕히는 X, 장판 방향 Z 등)을 그대로 저장합니다.
            authoredLocalRotation = transform.localRotation;
            // 머티리얼 인스턴스화는 Awake에서 1회만 수행합니다.
            // 머티리얼이 이미 인스턴스화된 경우 중복 생성을 막습니다.
            if (baseImage != null && baseImage.material != null && baseImage.material.name != "Default UI Material" && baseMaterial == null)
            {
                baseMaterial = new Material(baseImage.material);
                baseImage.material = baseMaterial;
                baseImage.fillAmount = 1f;
                if (baseMaterial.HasProperty("_CenterFill")) baseMaterial.SetFloat("_CenterFill", 1.0f);
            }

            if (gaugeImage != null && gaugeImage.material != null && gaugeImage.material.name != "Default UI Material" && gaugeMaterial == null)
            {
                gaugeMaterial = new Material(gaugeImage.material);
                gaugeImage.material = gaugeMaterial;
                gaugeImage.fillAmount = 1f;
                if (gaugeMaterial.HasProperty("_CenterFill"))
                    gaugeMaterial.SetFloat("_CenterFill", fillCurve.Evaluate(0f));
            }
        }

        void OnEnable()
        {
            // 풀에서 꺼낼 때마다 상태를 초기화합니다.
            // targetTransform 자동 탐색
            if (targetTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) targetTransform = player.transform;
            }

            // 게이지를 0으로 초기화 (이전 재생 잔상 제거)
            isPlaying = false;
            timer = 0f;
            if (gaugeMaterial != null && gaugeMaterial.HasProperty("_CenterFill"))
                gaugeMaterial.SetFloat("_CenterFill", fillCurve.Evaluate(0f));

            // 트리거 모드: 자식 이미지 숨김 (StartPreviewFromCode에서 다시 켜줌)
            if (useSpacebarTrigger)
            {
                if (baseImage != null) baseImage.gameObject.SetActive(false);
                if (gaugeImage != null) gaugeImage.gameObject.SetActive(false);
            }
            else
            {
                isPlaying = true;
            }
        }

        /// <summary>
        /// 인게임 플레이어(NetworkPlayerController)가 스킬을 시전할 때 외부에서 장판을 구동시키는 함수입니다.
        /// </summary>
        public void StartPreviewFromCode(float duration, float scale)
        {
            chargeTime = duration;
            transform.localScale = Vector3.one * scale; // 스킬의 범위(aoeRadius/range)에 맞춰 크기 조절
            
            isPlaying = true;
            timer = 0f;

            // 오브젝트 활성화 직전 게이지를 0으로 초기화해 이전 재생 잔상을 제거합니다.
            if (gaugeMaterial != null && gaugeMaterial.HasProperty("_CenterFill"))
            {
                gaugeMaterial.SetFloat("_CenterFill", fillCurve.Evaluate(0f));
            }
            
            if (baseImage != null) baseImage.gameObject.SetActive(true);
            if (gaugeImage != null) gaugeImage.gameObject.SetActive(true);
        }

        void Update()
        {
            // 트리거 입력 감지 (스페이스바)는 이제 별도 처리 없이 Update에 둡니다.
            if (useSpacebarTrigger && UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    StartPreviewFromCode(chargeTime, transform.localScale.x);
                }
            }

            // 타겟 잃었을 때 자동 복구
            if (targetTransform == null)
            {
                var playerCtrl = FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
                if (playerCtrl != null) targetTransform = playerCtrl.transform;
            }

            // 게이지 차징이 완료(100%)되었는지 확인 (delayBeforeReset 대기 시간 동안 화면에 남아있음)
            bool isFinished = isPlaying && (timer >= chargeTime);

            // 1. 플레이어(또는 타겟) 위치로 이동 (차징 중에만 따라가고, 끝나면 제자리 바닥에 고정)
            float finalAngle = testAngleOffset;
            if (targetTransform != null && !isFinished)
            {
                Transform dirRef = casterTransform != null ? casterTransform : targetTransform;

                Vector3 forwardDir = dirRef.forward;
                forwardDir.y = 0f;
                if (forwardDir.sqrMagnitude > 0.0001f) forwardDir.Normalize();
                else forwardDir = dirRef.forward;

                Vector3 worldPos = targetTransform.position + offset + forwardDir * forwardOffset;
                float rotationY = dirRef.eulerAngles.y;

                if (syncRotationToCaster)
                {
                    // 시전자 Yaw만 월드 방향으로 얹고, 프리팹에서 잡아둔 X/Z 회전은 quaternion 그대로 보존합니다.
                    Quaternion worldRot = Quaternion.Euler(0f, rotationY, 0f) * authoredLocalRotation;

                    if (transform is RectTransform rect)
                    {
                        rect.SetPositionAndRotation(worldPos, worldRot);
                    }
                    else
                    {
                        transform.SetPositionAndRotation(worldPos, worldRot);
                    }
                }
                else
                {
                    transform.position = worldPos;
                    transform.rotation = authoredLocalRotation;
                    finalAngle = testAngleOffset;
                }
            }

            // --- Base Image 업데이트 (고정 배경) ---
            if (baseMaterial != null && baseImage != null && baseImage.gameObject.activeSelf)
            {
                if (baseMaterial.HasProperty("_RadialFill")) baseMaterial.SetFloat("_RadialFill", testRadialFill);
                if (baseMaterial.HasProperty("_AngleOffset")) baseMaterial.SetFloat("_AngleOffset", finalAngle);
                baseImage.SetMaterialDirty();
            }

            // --- Gauge Image 업데이트 (애니메이션) ---
            if (gaugeMaterial != null && gaugeImage != null && gaugeImage.gameObject.activeSelf)
            {
                if (gaugeMaterial.HasProperty("_RadialFill")) gaugeMaterial.SetFloat("_RadialFill", testRadialFill);
                if (gaugeMaterial.HasProperty("_AngleOffset")) gaugeMaterial.SetFloat("_AngleOffset", finalAngle);

                if (isPlaying)
                {
                    timer += Time.deltaTime;
                    float normalizedTime = Mathf.Clamp01(timer / chargeTime);
                    
                    // 곡선을 이용해 게이지 차오르는 속도(형태) 조절
                    float fillAmount = fillCurve.Evaluate(normalizedTime);

                    if (gaugeMaterial.HasProperty("_CenterFill"))
                    {
                        gaugeMaterial.SetFloat("_CenterFill", fillAmount);
                    }

                    // 단발성 재생 종료 처리
                    if (useSpacebarTrigger && timer >= chargeTime + delayBeforeReset)
                    {
                        isPlaying = false;
                        if (baseImage != null) baseImage.gameObject.SetActive(false);
                        if (gaugeImage != null) gaugeImage.gameObject.SetActive(false);
                    }
                    // 트리거 없이 무한 루프 모드일 경우
                    else if (!useSpacebarTrigger && timer >= chargeTime + delayBeforeReset)
                    {
                        timer = 0f; // 반복
                    }
                }

                gaugeImage.SetMaterialDirty();
            }
        }
    }
}


