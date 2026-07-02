using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace ActionRPG.UI
{
    /// <summary>
    /// 기 모으기(Charge) 스킬 사용 시 바닥에 깔려 채워지는 장판(Indicator)을 제어하는 스크립트입니다.
    /// World Space Canvas 내부에 Image(Filled) 컴포넌트를 가진 UI 요소에 부착하여 사용합니다.
    /// </summary>
    public class SkillIndicator : MonoBehaviour
    {
        [Tooltip("차오르는 효과를 줄 원형 UI Image (Image Type = Filled, Radial 360 설정 필수)")]
        public Image fillImage;
        
        [Tooltip("배경이나 테두리 등을 서서히 나타나게 할 때 사용할 캔버스 그룹 (선택사항)")]
        public CanvasGroup canvasGroup;

        // 초기화 및 차징 코루틴 시작
        public void StartCharging(float chargeTime, float targetScale)
        {
            // World Space Canvas에서 UI 기본 크기가 실제 월드 크기로 과대 반영되지 않도록 자동 보정합니다.
            // UI Image의 기본 크기(Width 100 등)를 그대로 월드 공간에 두면 100미터짜리 거대한 이미지가 됩니다.
            // 따라서 targetScale(미터)을 달성하기 위해, 기존 Width 값만큼 스케일을 나누어 보정해 줍니다.
            RectTransform rect = GetComponent<RectTransform>();
            if (rect != null)
            {
                float baseWidth = Mathf.Max(rect.rect.width, 1f);
                transform.localScale = Vector3.one * (targetScale / baseWidth);
            }
            else
            {
                // UI가 아닐 경우 기본 스케일링
                transform.localScale = Vector3.one * targetScale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                // 처음에 부드럽게 Fade-in
                canvasGroup.DOFade(1f, 0.2f).SetEase(Ease.OutQuad);
            }

            if (fillImage != null)
            {
                // 커스텀 셰이더 조작을 위해 머티리얼 인스턴스화 (동시에 여러 장판이 깔릴 때 충돌 방지)
                if (fillImage.material != null && fillImage.material.name != "Default UI Material")
                {
                    fillImage.material = new Material(fillImage.material);
                }

                // 커스텀 셰이더는 외곽 각도와 중앙 충전량을 독립적으로 제어합니다.
                // _RadialFill(피자 조각 각도)은 인스펙터에서 설정한 고정값(미리 설정)으로 두고,
                // _CenterFill(중앙에서 퍼지는 게이지)만 시간에 따라 차오르게 합니다.
                bool hasCustomFill = false;
                if (fillImage.material != null)
                {
                    if (fillImage.material.HasProperty("_CenterFill"))
                    {
                        fillImage.material.SetFloat("_CenterFill", 0f);
                        fillImage.material.DOFloat(1f, "_CenterFill", chargeTime).SetEase(Ease.Linear);
                        hasCustomFill = true;
                    }
                }

                if (hasCustomFill)
                {
                    // 커스텀 셰이더가 마스크를 담당하므로 Image Mesh는 항상 100%로 유지합니다.
                    fillImage.fillAmount = 1f; 
                    
                    // 시간 타이머를 수동으로 달아서 OnChargeComplete 실행
                    DOVirtual.DelayedCall(chargeTime, OnChargeComplete);
                }
                else
                {
                    // 커스텀 셰이더가 없으면 기본 Image FillAmount로 게이지를 표시합니다.
                    fillImage.fillAmount = 0f;
                    fillImage.DOFillAmount(1f, chargeTime).SetEase(Ease.Linear).OnComplete(OnChargeComplete);
                }
            }

            // [옵션] 장판 자체의 물리적 회전은 제거 (셰이더에서 제어하거나 필요시 추가)
            // transform.DORotate(new Vector3(0, 360f, 0), chargeTime, RotateMode.WorldAxisAdd).SetEase(Ease.Linear);
        }

        private void OnChargeComplete()
        {
            // 100% 꽉 찼을 때 (폭발 직전 붉은색으로 강렬하게 번쩍임)
            if (fillImage != null)
            {
                if (fillImage.material != null && (fillImage.material.HasProperty("_RadialFill") || fillImage.material.HasProperty("_CenterFill")))
                {
                    // 피자 모양(_RadialFill)은 건드리지 않고, 중앙 확장(_CenterFill)만 100%로 보장합니다.
                    if (fillImage.material.HasProperty("_CenterFill")) fillImage.material.SetFloat("_CenterFill", 1f);
                    
                    if (fillImage.material.HasProperty("_GlowColor"))
                    {
                        Color origGlow = fillImage.material.GetColor("_GlowColor");
                        // HDR 붉은색으로 강하게 번쩍
                        fillImage.material.DOColor(Color.red * 2f, "_GlowColor", 0.1f).SetLoops(2, LoopType.Yoyo).OnComplete(() => {
                            if(fillImage != null && fillImage.material != null) fillImage.material.SetColor("_GlowColor", origGlow);
                        });
                    }
                }
                else
                {
                    fillImage.fillAmount = 1f;
                    // 일반 이미지는 컬러로 번쩍임
                    fillImage.DOColor(Color.red, 0.1f).SetLoops(2, LoopType.Yoyo);
                }
            }

            // 폭발 타격 판정 시간(약 0.15초)을 벌어준 뒤 자연스럽게 소멸
            DOVirtual.DelayedCall(0.15f, () => FadeOutAndDestroy());
        }

        private void FadeOutAndDestroy()
        {
            float fadeTime = 0.3f;

            // 크기는 살짝 더 커지면서
            transform.DOScale(transform.localScale * 1.2f, fadeTime).SetEase(Ease.OutQuad);
            
            // 투명도는 점점 흐려짐
            if (fillImage != null)
            {
                fillImage.DOFade(0f, fadeTime).SetEase(Ease.OutQuad);
            }
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, fadeTime).SetEase(Ease.OutQuad);
            }

            // 트윈 완료 후 오브젝트 파괴
            Destroy(gameObject, fadeTime + 0.1f);
        }

        private void OnDestroy()
        {
            // 오브젝트 파괴 시 진행 중이던 모든 DOTween 애니메이션 안전하게 강제 종료
            transform.DOKill();
            if (fillImage != null) fillImage.DOKill();
            if (canvasGroup != null) canvasGroup.DOKill();
        }
    }
}
