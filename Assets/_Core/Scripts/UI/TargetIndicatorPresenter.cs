using UnityEngine;
using ActionRPG.Player;
using ActionRPG.Data;

namespace ActionRPG.UI
{
    /// <summary>
    /// [Presentation Layer]
    /// NetworkPlayerController의 타겟 변경 이벤트를 구독하여
    /// VFXDatabase.Instance에 지정된 타겟 표시 원형 파티클의 활성화 및 부모 지정을 관리합니다.
    /// 플레이어의 로직(이동/전투)과 시각 연출(VFX)을 철저히 분리(SoC)합니다.
    /// </summary>
    [RequireComponent(typeof(NetworkPlayerController))]
    public class TargetIndicatorPresenter : MonoBehaviour
    {
        private NetworkPlayerController playerController;
        private GameObject indicatorInstance;
        private Transform lastTarget;

        private void Awake()
        {
            playerController = GetComponent<NetworkPlayerController>();
        }

        private void Start()
        {
            // [방어적 구현 / SO Singleton 활용]
            if (VFXDatabase.Instance == null)
            {

                return;
            }

            // 원형 표시기 인스턴스를 1개 사전 생성하여 대기 (Object Pooling 최적화)
            if (VFXDatabase.Instance.targetIndicatorPrefab != null)
            {
                indicatorInstance = Instantiate(VFXDatabase.Instance.targetIndicatorPrefab);
                indicatorInstance.SetActive(false);
            }

            // 이벤트 구독
            playerController.OnTargetChanged += HandleTargetChanged;
            
            // 초기 타겟이 이미 설정되어 있는 경우를 위해 1회 수동 처리
            if (playerController.currentTarget != null)
            {
                HandleTargetChanged(null, playerController.currentTarget);
            }
        }

        private void OnDestroy()
        {
            if (playerController != null)
            {
                playerController.OnTargetChanged -= HandleTargetChanged;
            }

            if (indicatorInstance != null)
            {
                Destroy(indicatorInstance);
            }
        }

        private void HandleTargetChanged(Transform previousTarget, Transform newTarget)
        {
            if (indicatorInstance == null) return;

            if (newTarget != null && newTarget.gameObject.activeInHierarchy)
            {
                if (newTarget != lastTarget)
                {
                    // 부모 재지정 및 로컬 정렬
                    indicatorInstance.transform.SetParent(newTarget, false);
                    indicatorInstance.transform.localPosition = new Vector3(0f, VFXDatabase.Instance.indicatorHeightOffset, 0f);
                    indicatorInstance.transform.localRotation = Quaternion.identity;
                    
                    // 활성화
                    indicatorInstance.SetActive(true);

                    // 파티클 재재생 연출
                    ParticleSystem[] ps = indicatorInstance.GetComponentsInChildren<ParticleSystem>();
                    foreach (var p in ps)
                    {
                        p.Play();
                    }

                    lastTarget = newTarget;
                }
            }
            else
            {
                // 타겟 상실 시 비활성화 및 부모 해제
                if (indicatorInstance.activeSelf)
                {
                    indicatorInstance.SetActive(false);
                    indicatorInstance.transform.SetParent(null);
                    lastTarget = null;
                }
            }
        }
    }
}
