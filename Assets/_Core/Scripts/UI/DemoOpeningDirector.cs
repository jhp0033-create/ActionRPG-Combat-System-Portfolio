using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using DG.Tweening;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using ActionRPG.Managers;
using ActionRPG.Player;

namespace ActionRPG.UI
{
    /// <summary>
    /// 데모 오프닝의 암전, 지역명 표시, 인트로 VFX 흐름을 순차적으로 제어합니다.
    /// </summary>
    public class DemoOpeningDirector : MonoBehaviour
    {
        [Header("개발 및 편의성")]
        [Tooltip("체크 시 오프닝 연출을 무시하고 게임을 즉시 시작합니다. (에디터 프리뷰용)")]
        public bool skipOpening = false;

        [Header("마비노기 오프닝 연출 셋업")]
        [Tooltip("화면 전체를 덮을 까만색 캔버스 이미지")]
        public Image blackScreen;
        
        [Tooltip("중앙에 띄울 하얀색 텍스트 (예: 알비 던전)")]
        public TextMeshProUGUI locationText;

        [Tooltip("타이틀 밑에 살짝 올라올 서브 설명 텍스트")]
        public TextMeshProUGUI subTitleText;

        [Header("추가 장식 이미지 (Fill Amount)")]
        [Tooltip("제목 왼쪽에서 채워질 가로 이미지")]
        public Image leftImage;
        [Tooltip("제목 오른쪽에서 채워질 가로 이미지")]
        public Image rightImage;
        [Tooltip("밑에서 360도로 채워질 원형 이미지")]
        public Image bottomRadialImage;

        [Header("타이밍 및 연출 설정")]
        public float startDelay = 0.5f;     // 시작 전 암전 대기 시간
        public float textFadeDuration = 1f; // 글씨가 스르륵 나타나는 시간
        public float subTitleRiseOffset = -30f; // 서브 텍스트가 아래에서 위로 올라오는 거리
        public float holdDuration = 1.5f;   // 글씨 띄워두고 여운 주는 시간
        public float textFadeOutDuration = 0.8f; // 글씨가 먼저 스르륵 사라지는 시간
        public float screenFadeOutDuration = 1.5f; // 까만 화면이 밝아지며 게임 시작되는 시간

        [Header("오프닝 종료 이벤트")]
        [Tooltip("화면이 밝아진 직후 실행할 함수들을 연결하세요. (예: 퀘스트 UI 등장, 조작 활성화 등)")]
        public UnityEvent OnOpeningFinished;

        [Header("데모 인트로 시퀀스")]
        [Tooltip("오프닝 종료 후 불길한 기운 대화, 메테오 낙하, 충돌/안개/빛기둥 연출을 자동 재생합니다.")]
        public bool playDemoIntroSequence = true;
        [Tooltip("대사/메테오 연출이 모두 끝나고 자동이동 버튼 등 게임플레이 UI를 열 때 사용합니다.")]
        public UnityEvent OnGameplayIntroFinished;
        public float demoIntroStartDelay = 0.35f;
        [Header("Meteor Scale")]
        public float meteorStartScale = 0.15f;
        public float meteorEndScale = 2.2f;
        public Ease meteorScaleEase = Ease.OutQuad;
        [Tooltip("오프닝에 띄울 대사 ID (예: Dialogue_Omen)")]
        public string omenDialogueID = "Dialogue_Omen";
        
        public bool autoAdvanceDialogue = true;
        public float dialogueLineHoldDuration = 1.1f;

        [Header("메테오 / 충돌 연출")]
        [Tooltip("메테오가 떨어질 기준 지점입니다. 비어있으면 AreaSpawner 위치를 사용합니다.")]
        public Transform impactPoint;
        public GameObject meteorPrefab;
        public GameObject impactVfxPrefab;
        public Vector3 meteorSpawnOffset = new Vector3(-8f, 16f, 5f);
        [Tooltip("프리팹의 앞축이 Unity forward(+Z)가 아닐 때 보정합니다. 예: 모델이 아래(-Y)를 앞축으로 쓰면 X=90 등을 조정하세요.")]
        public Vector3 meteorVisualRotationOffset = Vector3.zero;
        [Tooltip("진행 방향(Z축)을 기준으로만 회전하여 건들거림 없이 드릴처럼 떨어집니다.")]
        public Vector3 meteorRotationPerSecond = new Vector3(0f, 0f, 1080f);
        public float meteorFallDuration = 0.25f;
        public float meteorImpactVfxLifetime = 4f;
        public float postImpactHoldDuration = 0.7f;
        public Ease meteorFallEase = Ease.InExpo;

        [Header("잔류 월드 연출")]
        [Tooltip("충돌 후 켜둘 안개, 빛기둥, 소환 원형 등 씬 오브젝트를 넣어주세요.")]
        public GameObject[] lingeringEffectObjects;
        public bool disableLingeringEffectsOnStart = true;

        [Header("하늘 어둠 연출")]
        [Tooltip("하늘 머티리얼만 어둡게 틴트합니다. 파티클/UI 가독성을 지키는 데 사용합니다.")]
        public bool darkenSkyOnImpact = true;
        public Color impactSkyTint = new Color(0.26f, 0.29f, 0.35f, 1f);
        public float skyFadeDuration = 0.8f;
        [Tooltip("체크하면 하늘뿐 아니라 환경광도 함께 낮춥니다. 전투 가독성이 떨어지면 끄세요.")]
        public bool darkenAmbientOnImpact = false;
        [Range(0f, 1f)]
        public float impactAmbientIntensityMultiplier = 0.65f;

        private Sequence openingSeq;
        private Coroutine demoIntroRoutine;
        private bool openingFinishedEventInvoked;
        private Material runtimeSkybox;
        private string skyTintProperty;
        private Color originalSkyTint;
        private float originalAmbientIntensity;
        private Color originalAmbientLight;
        private Color originalAmbientSkyColor;
        private Color originalAmbientEquatorColor;
        private Color originalAmbientGroundColor;
        private bool skyStateCaptured;
        private readonly System.Collections.Generic.Dictionary<GameObject, Vector3> lingeringOriginalScales = new();
        public bool isFinished { get; private set; } = false;
        public bool isGameplayIntroFinished { get; private set; } = false;

        private void Awake()
        {
            CaptureSkyState();

            if (disableLingeringEffectsOnStart && lingeringEffectObjects != null)
            {
                foreach (var effectObj in lingeringEffectObjects)
                {
                    if (effectObj != null) effectObj.SetActive(false);
                }
            }
        }

        private void Start()
        {
            if (skipOpening)
            {
                // UI들을 즉시 숨기고 바로 게임 시작 이벤트 발동
                FinishSequence();
            }
            else
            {
                LockPlayerInput(true);
                PlayOpeningSequence();
            }
        }

        private void LockPlayerInput(bool isLocked)
        {
            var player = FindFirstObjectByType<NetworkPlayerController>();
            if (player != null) player.isInputLocked = isLocked;

            var cam = Camera.main != null ? Camera.main.GetComponent<ActionRPG.CameraSystem.CameraController>() : null;
            if (cam != null) cam.isInputLocked = isLocked;
        }

        private void Update()
        {
            // 에디터 프리뷰 편의를 위해 New Input System 입력으로 오프닝을 넘길 수 있습니다.
            if (!isFinished)
            {
                bool mouseClick = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
                bool anyKey = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
                
                if (mouseClick || anyKey)
                {
                    SkipOpening();
                }
            }
        }

        private void PlayOpeningSequence()
        {
            if (blackScreen == null || locationText == null)
            {

                FinishSequence();
                return;
            }

            // 1. 초기화: 화면은 까맣게, 텍스트는 안 보이게 셋팅
            blackScreen.gameObject.SetActive(true);
            blackScreen.color = new Color(0, 0, 0, 1f); // 알파 1
            
            locationText.gameObject.SetActive(true);
            locationText.color = new Color(locationText.color.r, locationText.color.g, locationText.color.b, 0f); // 알파 0

            if (subTitleText != null)
            {
                subTitleText.gameObject.SetActive(true);
                subTitleText.color = new Color(subTitleText.color.r, subTitleText.color.g, subTitleText.color.b, 0f);
            }

            // 추가된 이미지들 초기화 (보이되, FillAmount는 0으로)
            if (leftImage != null) { leftImage.gameObject.SetActive(true); leftImage.fillAmount = 0f; leftImage.color = new Color(leftImage.color.r, leftImage.color.g, leftImage.color.b, 1f); }
            if (rightImage != null) { rightImage.gameObject.SetActive(true); rightImage.fillAmount = 0f; rightImage.color = new Color(rightImage.color.r, rightImage.color.g, rightImage.color.b, 1f); }
            if (bottomRadialImage != null) { bottomRadialImage.gameObject.SetActive(true); bottomRadialImage.fillAmount = 0f; bottomRadialImage.color = new Color(bottomRadialImage.color.r, bottomRadialImage.color.g, bottomRadialImage.color.b, 1f); }

            // 2. DOTween 시퀀스 연출 시작
            openingSeq = DOTween.Sequence();

            // 단계 0: 처음에 메인 글씨가 스케일 업되며 스르륵 등장 (고급스러운 임팩트 폴리싱)
            openingSeq.AppendInterval(startDelay);
            if (locationText != null)
            {
                locationText.transform.localScale = Vector3.one * 0.85f; // 약간 작게 시작
                openingSeq.Append(locationText.DOFade(1f, textFadeDuration).SetEase(Ease.OutQuad));
                // OutBack 탄성을 이용해 띠용 하고 글씨가 자리를 잡음
                openingSeq.Join(locationText.transform.DOScale(1f, textFadeDuration).SetEase(Ease.OutBack));
            }
            
            // 단계 1: 메인 텍스트는 가만히 고정된 채, 양옆 이미지 Fill 0 -> 1 (서브 텍스트도 등장)
            float fillDuration = 1.5f;
            
            // 더미 트윈(빈 애니메이션)을 하나 넣어 1.5초를 소모하게 하고, 나머지 연출을 Join으로 묶습니다.
            openingSeq.Append(DOVirtual.Float(0, 1, fillDuration, (v) => { }));
            
            if (leftImage != null) openingSeq.Join(leftImage.DOFillAmount(1f, fillDuration).SetEase(Ease.InOutSine));
            if (rightImage != null) openingSeq.Join(rightImage.DOFillAmount(1f, fillDuration).SetEase(Ease.InOutSine));
            if (subTitleText != null) openingSeq.Join(subTitleText.DOFade(1f, fillDuration).SetEase(Ease.OutQuad));

            // 단계 2: 양옆 이미지가 50% 정도 찼을 때 (즉, fillDuration의 절반 지점), 밑에 원형 이미지(로고) Fill 시작
            if (bottomRadialImage != null)
            {
                float bottomFillDuration = 1.0f;
                float insertTime = openingSeq.Duration() - (fillDuration * 0.5f);
                
                // 기교를 빼고, 원래 있던 '스르륵 차오르며 한 바퀴 도는 첫 효과(FillAmount)'까지만 유지합니다.
                openingSeq.Insert(insertTime, bottomRadialImage.DOFillAmount(1f, bottomFillDuration).SetEase(Ease.InOutSine));
            }

            // 여운 주기 (모든 게 완성된 상태) - 화면에 떠 있는 동안 메인 텍스트가 아주 천천히 확대됨 (생동감 부여)
            if (locationText != null)
            {
                openingSeq.Append(locationText.transform.DOScale(1.03f, holdDuration).SetEase(Ease.Linear));
            }
            else
            {
                openingSeq.AppendInterval(holdDuration);
            }

            // 단계 4: 글씨와 장식 이미지들 먼저 투명하게 페이드 아웃
            openingSeq.Append(locationText.DOFade(0f, textFadeOutDuration).SetEase(Ease.InOutSine));
            if (subTitleText != null) openingSeq.Join(subTitleText.DOFade(0f, textFadeOutDuration).SetEase(Ease.InOutSine));
            if (leftImage != null) openingSeq.Join(leftImage.DOFade(0f, textFadeOutDuration).SetEase(Ease.InOutSine));
            if (rightImage != null) openingSeq.Join(rightImage.DOFade(0f, textFadeOutDuration).SetEase(Ease.InOutSine));
            if (bottomRadialImage != null) openingSeq.Join(bottomRadialImage.DOFade(0f, textFadeOutDuration).SetEase(Ease.InOutSine));

            // 글씨가 완전히 꺼진 뒤 잠시 어둠 유지
            openingSeq.AppendInterval(0.3f);

            // 단계 5: 까만 화면이 밝아지며 인게임 화면 등장
            openingSeq.Append(blackScreen.DOFade(0f, screenFadeOutDuration).SetEase(Ease.InOutSine));

            // 연출이 모두 끝나면 껍데기 UI들은 꺼주고, 다음 이벤트를 호출함
            openingSeq.OnComplete(() =>
            {
                FinishSequence();
            });
        }

        /// <summary>
        /// 개발 편의를 위한 즉시 스킵 기능
        /// </summary>
        public void SkipOpening()
        {
            if (isFinished) return;

            // 진행 중인 애니메이션 강제 종료
            if (openingSeq != null && openingSeq.IsActive())
            {
                openingSeq.Kill();
            }

            FinishSequence();
        }

        private void FinishSequence()
        {
            if (isFinished) return;
            isFinished = true;

            if (blackScreen != null) blackScreen.gameObject.SetActive(false);
            if (locationText != null) locationText.gameObject.SetActive(false);
            if (subTitleText != null) subTitleText.gameObject.SetActive(false);
            if (leftImage != null) leftImage.gameObject.SetActive(false);
            if (rightImage != null) rightImage.gameObject.SetActive(false);
            if (bottomRadialImage != null) bottomRadialImage.gameObject.SetActive(false);
            
            if (playDemoIntroSequence)
            {
                if (demoIntroRoutine != null) StopCoroutine(demoIntroRoutine);
                demoIntroRoutine = StartCoroutine(PlayDemoIntroSequence());
            }
            else
            {
                InvokeOpeningFinishedOnce();
                FinishGameplayIntro();
            }
        }

        private IEnumerator PlayDemoIntroSequence()
        {
            yield return new WaitForSeconds(demoIntroStartDelay);

            // 1. 메테오가 먼저 떨어져 폭발 연출을 보여줍니다.
            yield return PlayMeteorImpactSequence();

            // 충돌 연출의 여운을 둔 뒤 대사를 시작합니다.
            yield return new WaitForSeconds(0.25f);

            // 2. 메테오 연출(폭발 및 여운)이 끝난 후 대사가 나옵니다.
            if (!string.IsNullOrEmpty(omenDialogueID) && UI_DialogueManager.Instance != null)
            {
                bool dialogueDone = false;
                void HandleDialogueFinished() => dialogueDone = true;

                UI_DialogueManager.Instance.OnDialogueFinished += HandleDialogueFinished;
                
                var player = FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
                Transform targetTransform = player != null ? player.transform : null;
                
                UI_DialogueManager.Instance.StartDialogueByKey(omenDialogueID, autoAdvanceDialogue, dialogueLineHoldDuration, targetTransform);

                while (!dialogueDone && UI_DialogueManager.Instance != null && UI_DialogueManager.Instance.IsDialogueActive)
                {
                    yield return null;
                }

                if (UI_DialogueManager.Instance != null)
                {
                    UI_DialogueManager.Instance.OnDialogueFinished -= HandleDialogueFinished;
                }
            }

            // 3. 완전히 끝나면 인풋 락 해제 및 게임 UI 표시
            FinishGameplayIntro();
        }

        private IEnumerator PlayMeteorImpactSequence()
        {
            Vector3 impactPos = ResolveImpactPosition();
            
            // 시네마틱(운석 낙하) 시작 전 캐릭터의 원래 위치/회전값 저장
            var player = FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
            Quaternion originalPlayerRot = player != null ? player.transform.rotation : Quaternion.identity;
            bool hasPlayer = player != null;

            // 1. 배경음악을 20%로 부드럽게 줄이고 메테오 낙하~폭발 통합 사운드 재생 (페이드 인 및 배속 재생)
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.FadeBGM(0.2f, 0.5f);
                // 3번째 인자(1.5f)가 재생 속도입니다. 숫자를 높이면 사운드가 더 빨리 재생됩니다!
                SoundManager.Instance.PlayCinematicSFX("MeteorSound", 1.0f, 2.5f); 
            }
            
            var cameraController = Camera.main != null ? Camera.main.GetComponent<ActionRPG.CameraSystem.CameraController>() : null;

            GameObject meteor = null;
            if (meteorPrefab != null)
            {
                meteor = SpawnPooledVfx(meteorPrefab, impactPos + meteorSpawnOffset, Quaternion.identity, meteorFallDuration + 0.2f);
                Vector3 fallDirection = (impactPos - meteor.transform.position).normalized;
                if (fallDirection.sqrMagnitude > 0.0001f)
                {
                    // 방향을 먼저 정확히 타겟으로 잡고 고정시킵니다.
                    meteor.transform.rotation = Quaternion.LookRotation(fallDirection, Vector3.up);
                    // 시각적 오프셋이 있다면 로컬로 회전시켜 보정합니다.
                    if (meteorVisualRotationOffset != Vector3.zero)
                    {
                        meteor.transform.Rotate(meteorVisualRotationOffset, Space.Self);
                    }
                }

                // 2. 플레이어와 카메라가 떨어지는 운석(운석 낙하 지점)을 플레이어 시점에서 바라보도록 방향 강제 정렬
                if (hasPlayer)
                {
                    Vector3 lookDir = (impactPos - player.transform.position).normalized;
                    lookDir.y = 0f;
                    if (lookDir != Vector3.zero)
                    {
                        // 부드러운 감속 중이라도 강제로 운석을 바라보도록 설정
                        player.transform.rotation = Quaternion.LookRotation(lookDir);
                    }
                }

                if (cameraController != null)
                {
                    // 카메라 자체의 기본 궤도(Yaw/Pitch)는 건드리지 않고 원본 설정대로 둡니다.
                    // 캐릭터가 운석을 향해 돌았으므로, 카메라는 자연스럽게 캐릭터 등 뒤에 위치하며 운석을 바라봅니다.
                    cameraController.SetCinematicFocus(meteor.transform, 0.6f);
                }

                meteor.transform.localScale = Vector3.one * meteorStartScale;

                Sequence meteorSeq = DOTween.Sequence();

                meteorSeq.Join(
                    meteor.transform.DOMove(impactPos, meteorFallDuration)
                        .SetEase(meteorFallEase)
                );

                meteorSeq.Join(
                    meteor.transform.DOScale(meteorEndScale, meteorFallDuration)
                        .SetEase(Ease.InExpo)
                );

                yield return meteorSeq.WaitForCompletion();

                if (meteorPrefab != null && ObjectPoolManager.Instance != null)
                    ObjectPoolManager.Instance.Despawn(meteor);
                else
                    Destroy(meteor);
            }
            else
            {
                yield return new WaitForSeconds(meteorFallDuration);
            }

            // 3. 메테오 충돌 직후 카메라 포커스 해제 및 사운드 폭발
            if (cameraController != null)
            {
                cameraController.ClearCinematicFocus();
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.FadeBGM(1.0f, 2.0f); // 배경음 100%로 서서히 복구
            }

            SpawnImpactVfx(impactPos);
            EnableLingeringEffects();
            PlayImpactCameraEffects();
            FadeInImpactSky();

            yield return new WaitForSeconds(postImpactHoldDuration);

            // 4. 시네마틱 연출 종료 후, 캐릭터를 시네마틱 시작 직전의 최초 각도로 부드럽게 원상 복구합니다.
            if (hasPlayer && player != null)
            {
                player.transform.DORotateQuaternion(originalPlayerRot, 0.7f).SetEase(Ease.InOutSine);
            }

            // 5. 시네마틱 여운 사운드 페이드 아웃
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.FadeOutCinematicSFX(2.0f); // 2초간 서서히 페이드 아웃
            }
        }

        private Vector3 ResolveImpactPosition()
        {
            if (impactPoint != null) return impactPoint.position;

            AreaSpawner spawner = FindFirstObjectByType<AreaSpawner>();
            if (spawner != null) return spawner.transform.position;

            NetworkPlayerController player = FindFirstObjectByType<NetworkPlayerController>();
            if (player != null) return player.transform.position + player.transform.forward * 8f;

            return Vector3.zero;
        }

        private void SpawnImpactVfx(Vector3 impactPos)
        {
            if (impactVfxPrefab != null)
            {
                SpawnPooledVfx(impactVfxPrefab, impactPos, Quaternion.identity, meteorImpactVfxLifetime);
            }
        }

        private GameObject SpawnPooledVfx(GameObject prefab, Vector3 position, Quaternion rotation, float lifeTime)
        {
            return CombatEffects.SpawnPooledVFX(prefab, position, rotation, lifeTime);
        }

        private void EnableLingeringEffects()
        {
            if (lingeringEffectObjects == null) return;

            foreach (var effectObj in lingeringEffectObjects)
            {
                if (effectObj == null) continue;
                if (!lingeringOriginalScales.ContainsKey(effectObj))
                {
                    lingeringOriginalScales.Add(effectObj, effectObj.transform.localScale);
                }

                effectObj.transform.DOKill();
                effectObj.transform.localScale = lingeringOriginalScales[effectObj];
                effectObj.SetActive(true);
            }
        }

        private void CaptureSkyState()
        {
            if (skyStateCaptured) return;

            skyStateCaptured = true;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            originalAmbientLight = RenderSettings.ambientLight;
            originalAmbientSkyColor = RenderSettings.ambientSkyColor;
            originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
            originalAmbientGroundColor = RenderSettings.ambientGroundColor;

            Material sourceSkybox = RenderSettings.skybox;
            if (sourceSkybox == null) return;

            runtimeSkybox = new Material(sourceSkybox);
            runtimeSkybox.name = sourceSkybox.name + " (Runtime)";
            RenderSettings.skybox = runtimeSkybox;

            skyTintProperty = ResolveSkyTintProperty(runtimeSkybox);
            if (!string.IsNullOrEmpty(skyTintProperty))
            {
                originalSkyTint = runtimeSkybox.GetColor(skyTintProperty);
            }
        }

        private string ResolveSkyTintProperty(Material skyboxMaterial)
        {
            if (skyboxMaterial == null) return null;
            if (skyboxMaterial.HasProperty("_Tint")) return "_Tint";
            if (skyboxMaterial.HasProperty("_SkyTint")) return "_SkyTint";
            if (skyboxMaterial.HasProperty("_Color")) return "_Color";
            return null;
        }

        private void FadeInImpactSky()
        {
            if (!darkenSkyOnImpact) return;

            CaptureSkyState();

            if (runtimeSkybox != null && !string.IsNullOrEmpty(skyTintProperty))
            {
                DOTween.To(() => runtimeSkybox.GetColor(skyTintProperty),
                    value =>
                    {
                        runtimeSkybox.SetColor(skyTintProperty, value);
                        DynamicGI.UpdateEnvironment();
                    },
                    impactSkyTint,
                    skyFadeDuration)
                    .SetEase(Ease.InOutSine);
            }

            if (darkenAmbientOnImpact)
            {
                DOTween.To(() => RenderSettings.ambientIntensity,
                    value =>
                    {
                        RenderSettings.ambientIntensity = value;
                        DynamicGI.UpdateEnvironment();
                    },
                    originalAmbientIntensity * impactAmbientIntensityMultiplier,
                    skyFadeDuration)
                    .SetEase(Ease.InOutSine);

                DOTween.To(() => RenderSettings.ambientLight,
                    value => RenderSettings.ambientLight = value,
                    originalAmbientLight * impactAmbientIntensityMultiplier,
                    skyFadeDuration)
                    .SetEase(Ease.InOutSine);

                DOTween.To(() => RenderSettings.ambientSkyColor,
                    value => RenderSettings.ambientSkyColor = value,
                    originalAmbientSkyColor * impactAmbientIntensityMultiplier,
                    skyFadeDuration)
                    .SetEase(Ease.InOutSine);

                DOTween.To(() => RenderSettings.ambientEquatorColor,
                    value => RenderSettings.ambientEquatorColor = value,
                    originalAmbientEquatorColor * impactAmbientIntensityMultiplier,
                    skyFadeDuration)
                    .SetEase(Ease.InOutSine);

                DOTween.To(() => RenderSettings.ambientGroundColor,
                    value => RenderSettings.ambientGroundColor = value,
                    originalAmbientGroundColor * impactAmbientIntensityMultiplier,
                    skyFadeDuration)
                    .SetEase(Ease.InOutSine);
            }
        }

        private void PlayImpactCameraEffects()
        {
            CombatEffects.ShakeAndZoomCamera(0.55f, 0.85f, 16f, 0.25f);
        }

        private void FinishGameplayIntro()
        {
            if (isGameplayIntroFinished) return;

            isGameplayIntroFinished = true;
            InvokeOpeningFinishedOnce();
            LockPlayerInput(false);
            OnGameplayIntroFinished?.Invoke();
        }

        private void InvokeOpeningFinishedOnce()
        {
            if (openingFinishedEventInvoked) return;

            openingFinishedEventInvoked = true;
            OnOpeningFinished?.Invoke();
        }
    }
}
