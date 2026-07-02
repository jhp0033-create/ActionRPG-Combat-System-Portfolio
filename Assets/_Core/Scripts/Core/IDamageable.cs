using UnityEngine;

namespace ActionRPG.Core
{
    /// <summary>
    /// 무기(Hitbox)에 맞을 수 있는 모든 오브젝트(플레이어, 몬스터, 파괴 가능한 물체 등)가 상속받아야 하는 공용 인터페이스입니다.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 데미지를 입었을 때 호출됩니다.
        /// </summary>
        /// <param name="damageAmount">입은 피해량</param>
        /// <param name="hitPoint">타격 지점 (파티클 재생용)</param>
        /// <param name="hitNormal">타격 표면 방향 (파티클 재생용)</param>
        void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false);
    }
}
