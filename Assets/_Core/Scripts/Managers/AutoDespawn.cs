using UnityEngine;

namespace ActionRPG.Managers
{
    /// <summary>
    /// 파티클 이펙트나 데미지 이펙트, 일정 시간 뒤 사라져야 하는 아이템에 붙이는 헬퍼 컴포넌트입니다.
    /// 생성(OnEnable) 후 지정된 시간이 지나면 ObjectPoolManager를 통해 자동으로 회수(Despawn)됩니다.
    /// Destroy 대신 Despawn을 호출하기 때문에 렉 방지(GC 감소)에 필수적입니다.
    /// </summary>
    public class AutoDespawn : MonoBehaviour
    {
        [Tooltip("오브젝트가 풀로 돌아가기까지 대기할 시간 (초)")]
        public float lifeTime = 2.0f;

        private void OnEnable()
        {
            // 활성화될 때마다 타이머 시작
            Restart(lifeTime);
        }

        private void OnDisable()
        {
            // 비활성화 시 예약된 타이머 취소 (안전장치)
            CancelInvoke(nameof(DespawnNow));
        }

        public void Restart(float newLifeTime)
        {
            lifeTime = newLifeTime;
            CancelInvoke(nameof(DespawnNow));
            Invoke(nameof(DespawnNow), lifeTime);
        }

        private void DespawnNow()
        {
            // 이 컴포넌트가 꺼져있지 않다면 (이미 회수된 상태가 아니라면)
            if (gameObject.activeInHierarchy && ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Despawn(gameObject);
            }
            else if (gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
            }
        }
    }
}
