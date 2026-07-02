using UnityEngine;
using System.Collections.Generic;
using ActionRPG.Core;
using ActionRPG.Managers;

namespace ActionRPG.Player
{
    /// <summary>
    /// 플레이어의 무기(검) 오브젝트에 부착되어 물리적 충돌(Hitbox)을 감지하고 데미지를 중재하는 클래스입니다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class WeaponCollider : MonoBehaviour
    {
        [Header("Frontal Hitbox Settings")]
        public float attackRadius = 2.5f;
        public float attackForwardOffset = 1.5f;
        public float attackDamage = 10f;
        public float attackHeightOffset = 1f;

        [Header("Editor Preview")]
        [SerializeField] private bool showHitboxGizmo = true;
        [SerializeField] private Color inactiveGizmoColor = new Color(1f, 0.85f, 0.1f, 0.25f);
        [SerializeField] private Color activeGizmoColor = new Color(1f, 0.15f, 0.05f, 0.45f);

        private Collider weaponCollider;

        private HashSet<Collider> alreadyHitEnemies = new HashSet<Collider>();
        private bool isHitboxActive = false;
        private Transform playerRoot;

        private void Awake()
        {
            weaponCollider = GetComponent<Collider>();
            if (weaponCollider != null) weaponCollider.isTrigger = true; 
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            playerRoot = transform.root;
            DisableHitbox();
        }

        // --- 애니메이션 콜백(PlayerAttackBehaviour)에서 호출할 퍼블릭 메서드 ---

        /// <summary>
        /// 새로운 공격이 시작될 때 (검을 내려치기 직전) 호출되어 타격 판정을 켭니다.
        /// </summary>
        public void EnableHitbox()
        {
            alreadyHitEnemies.Clear(); // 새 공격이므로 피격 목록 초기화
            isHitboxActive = true;
        }

        /// <summary>
        /// 공격이 끝나거나 준비 동작일 때 호출되어 타격 판정을 끕니다.
        /// </summary>
        public void DisableHitbox()
        {
            isHitboxActive = false;
        }

        // --- 전방 스피어 판정 (무기 콜라이더 대신 플레이어 기준 전방 히트박스) ---
        private void Update()
        {
            if (!isHitboxActive || playerRoot == null) return;

            // 플레이어 전방으로 공격 범위를 생성합니다.
            Vector3 center = GetHitboxCenter();
            Collider[] hits = Physics.OverlapSphere(center, attackRadius);

            foreach (var hit in hits)
            {
                // 이미 맞은 적 패스
                if (alreadyHitEnemies.Contains(hit)) continue;

                // 자기 자신(플레이어)은 때리지 않음
                if (hit.transform.root == playerRoot) continue;

                IDamageable target = hit.GetComponent<IDamageable>();
                if (target == null) target = hit.GetComponentInParent<IDamageable>();
                
                if (target != null)
                {
                    Vector3 hitPoint = hit.ClosestPoint(playerRoot.position);
                    Vector3 hitNormal = (hit.transform.position - playerRoot.position).normalized;

                    // 무기 매니저에서 치명타 확률/배율 가져오기
                    float baseDamage = attackDamage;
                    float critChance = 0.2f;
                    float critMult = 1.5f;
                    var weaponManager = playerRoot.GetComponentInChildren<WeaponManager>();
                    if (weaponManager != null)
                    {
                        baseDamage = weaponManager.GetCurrentWeaponDamage();
                        critChance = weaponManager.GetCurrentWeaponCriticalChance();
                        critMult = weaponManager.GetCurrentWeaponCriticalMultiplier();
                    }

                    var playerController = playerRoot.GetComponent<NetworkPlayerController>();
                    if (playerController != null)
                    {
                        if (playerController.activeSkillIndex >= 0 && 
                            playerController.activeSkillIndex < playerController.equippedSkills.Length && 
                            playerController.equippedSkills[playerController.activeSkillIndex] != null &&
                            playerController.equippedSkills[playerController.activeSkillIndex].skillType == ActionRPG.Data.SkillType.ComboAttack)
                        {
                            float multiplier = playerController.equippedSkills[playerController.activeSkillIndex].damageMultiplier;
                            baseDamage *= multiplier > 0f ? multiplier : 1f;
                        }
                    }

                    bool isCritical = Random.value <= critChance;
                    float finalDamage = isCritical ? baseDamage * critMult : baseDamage;

                    target.TakeDamage(finalDamage, hitPoint, hitNormal, isCritical);
                    CombatEffects.ShakeCamera(0.16f, isCritical ? 0.5f : 0.35f);

                    alreadyHitEnemies.Add(hit);
                }
            }
        }

        private Vector3 GetHitboxCenter()
        {
            Transform root = playerRoot != null ? playerRoot : transform.root;
            return root.position + root.forward * attackForwardOffset + Vector3.up * attackHeightOffset;
        }

        private void OnDrawGizmos()
        {
            if (!showHitboxGizmo) return;

            Transform root = playerRoot != null ? playerRoot : transform.root;
            if (root == null) return;

            Vector3 center = root.position + root.forward * attackForwardOffset + Vector3.up * attackHeightOffset;
            Gizmos.color = isHitboxActive ? activeGizmoColor : inactiveGizmoColor;
            Gizmos.DrawSphere(center, attackRadius);
            Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.95f);
            Gizmos.DrawWireSphere(center, attackRadius);

            Gizmos.color = new Color(1f, 1f, 1f, 0.75f);
            Gizmos.DrawLine(root.position + Vector3.up * attackHeightOffset, center);
        }
    }
}
