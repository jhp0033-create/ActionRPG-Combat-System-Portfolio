using UnityEngine;
using ActionRPG.Data;

namespace ActionRPG.Player
{
    /// <summary>
    /// 차지어택 장판 크기·오프셋·판정 범위를 Scene 뷰에서 맞추기 위한 미리보기입니다.
    /// BoxCollider 크기 = 장판 지름/전후 폭, forwardOffset = 정면 이동 거리.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ChargeAttackHitArea : MonoBehaviour
    {
        public enum ColliderShape
        {
            Box,
            Sphere
        }

        [Header("에디터 튜닝")]
        [Tooltip("켜면 아래 인스펙터 값으로 장판/판정을 고정합니다 (SkillData 무시)")]
        public bool useInspectorValues = false;

        [Tooltip("Scene 뷰에 미리보기 Gizmo 표시")]
        public bool showEditorGizmo = true;

        [Header("위치 · 크기 (장판과 동일하게 맞추세요)")]
        [Tooltip("시전자 정면으로 밀어낼 거리 (m)")]
        public float forwardOffset = 0;

        [Tooltip("게이지/인디케이터를 시전자 정면으로 밀어낼 거리 (m)")]
        public float gaugeForwardOffset = 0;

        [Tooltip("바닥 장판 높이 (m)")]
        public float groundHeightOffset = 0.05f;

        [Tooltip("Overlap 판정 중심 높이 (m)")]
        public float hitHeightOffset = 1f;

        public ColliderShape shape = ColliderShape.Box;

        [Tooltip("Box: X=좌우 반경, Y=높이 반경, Z=전후 반경. 장판 지름 ≈ X 또는 Z의 2배")]
        public Vector3 boxHalfExtents = new Vector3(2.5f, 0.5f, 2.5f);

        [Tooltip("Sphere 모드 반경")]
        public float sphereRadius = 2.5f;

        [Header("References")]
        public BoxCollider boxCollider;
        public SphereCollider sphereCollider;

        private Transform caster;
        private bool isActive;

        public float GaugeDiameter => Mathf.Max(boxHalfExtents.x, boxHalfExtents.z) * 2f;
        public float GaugeForwardOffset => gaugeForwardOffset;

        public static void GetHitShapeFromSkill(SkillData skill, out float forward, out float radius, out float height)
        {
            forward = Mathf.Abs(skill.chargeHitForwardOffset) > 0.001f
                ? skill.chargeHitForwardOffset
                : (skill.range > 0f ? skill.range * 0.5f : 1.5f);
            radius = skill.aoeRadius > 0f ? skill.aoeRadius : 2.5f;
            height = 1f;
        }

        public static float GetGaugeForwardOffsetFromSkill(SkillData skill)
        {
            if (skill == null) return 0f;
            return Mathf.Abs(skill.chargeGaugeForwardOffset) > 0.001f
                ? skill.chargeGaugeForwardOffset
                : (skill.range > 0f ? skill.range * 0.5f : 1.5f);
        }

        private Vector3 initialEulerAngles;

        private void Awake()
        {
            initialEulerAngles = transform.eulerAngles;
            EnsureColliders();
        }

        private void EnsureColliders()
        {
            if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
            if (sphereCollider == null) sphereCollider = GetComponent<SphereCollider>();

            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();
            }

            if (sphereCollider == null)
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
            }

            boxCollider.isTrigger = true;
            sphereCollider.isTrigger = true;
            boxCollider.enabled = false;
            sphereCollider.enabled = false;
            gameObject.SetActive(false);
        }

        public void SyncFromSkill(SkillData skill)
        {
            if (useInspectorValues || skill == null) return;

            GetHitShapeFromSkill(skill, out float fwd, out float radius, out _);
            if (skill.chargeHitRadius > 0f) radius = skill.chargeHitRadius;

            forwardOffset = fwd;
            gaugeForwardOffset = GetGaugeForwardOffsetFromSkill(skill);
            shape = skill.chargeHitShape == SkillHitShape.Sphere ? ColliderShape.Sphere : ColliderShape.Box;
            boxHalfExtents = ResolveChargeHitBoxHalfExtents(skill, radius);
            sphereRadius = radius;
        }

        private static Vector3 ResolveChargeHitBoxHalfExtents(SkillData skill, float fallbackRadius)
        {
            Vector3 halfExtents = skill.chargeHitBoxHalfExtents;
            if (halfExtents.x <= 0f || halfExtents.y <= 0f || halfExtents.z <= 0f)
            {
                return new Vector3(fallbackRadius, 0.5f, fallbackRadius);
            }

            return halfExtents;
        }

        public void Show(Transform casterTransform, SkillData skill)
        {
            if (casterTransform == null || skill == null) return;

            EnsureColliders();
            SyncFromSkill(skill);

            caster = casterTransform;
            isActive = true;
            gameObject.SetActive(true);
            UpdateActiveCollider();
            ApplyPose();
        }

        public void Hide()
        {
            isActive = false;
            caster = null;
            if (boxCollider != null) boxCollider.enabled = false;
            if (sphereCollider != null) sphereCollider.enabled = false;
            gameObject.SetActive(false);
        }

        public int OverlapHitTargets(Transform casterTransform, Collider[] buffer)
        {
            ResolveHitQuery(casterTransform, out Vector3 center, out Quaternion orientation, out Vector3 halfExtents, out float radius);

            if (shape == ColliderShape.Box)
            {
                return Physics.OverlapBoxNonAlloc(center, halfExtents, buffer, orientation, ~0, QueryTriggerInteraction.Collide);
            }

            return Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Collide);
        }

        public void ResolveHitQuery(
            Transform casterTransform,
            out Vector3 center,
            out Quaternion orientation,
            out Vector3 halfExtents,
            out float radius)
        {
            Vector3 forward = casterTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = casterTransform.forward;
            forward.Normalize();

            Quaternion lookRot = Quaternion.LookRotation(forward, Vector3.up);
            orientation = Quaternion.Euler(initialEulerAngles.x, lookRot.eulerAngles.y, initialEulerAngles.z);
            center = casterTransform.position + forward * forwardOffset + Vector3.up * hitHeightOffset;
            halfExtents = boxHalfExtents;
            radius = sphereRadius;
        }

        private void LateUpdate()
        {
            if (!isActive || caster == null) return;
            ApplyPose();
        }

        private void UpdateActiveCollider()
        {
            bool useBox = shape == ColliderShape.Box;
            if (boxCollider != null)
            {
                boxCollider.enabled = isActive && useBox;
                boxCollider.size = boxHalfExtents * 2f;
                boxCollider.center = Vector3.up * boxHalfExtents.y;
            }

            if (sphereCollider != null)
            {
                sphereCollider.enabled = isActive && !useBox;
                sphereCollider.radius = sphereRadius;
                sphereCollider.center = Vector3.zero;
            }
        }

        private void ApplyPose()
        {
            if (caster == null) return;

            Vector3 forward = caster.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = caster.forward;
            forward.Normalize();

            transform.position = caster.position + forward * forwardOffset + Vector3.up * groundHeightOffset;
            Quaternion lookRot = Quaternion.LookRotation(forward, Vector3.up);
            transform.rotation = Quaternion.Euler(initialEulerAngles.x, lookRot.eulerAngles.y, initialEulerAngles.z);
            UpdateActiveCollider();
        }

        private void OnDrawGizmos()
        {
            if (!showEditorGizmo) return;

            Transform refTransform = caster != null ? caster : transform.parent;
            if (refTransform == null) return;

            Vector3 forward = refTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();

            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 groundCenter = refTransform.position + forward * forwardOffset + Vector3.up * groundHeightOffset;
            Vector3 hitCenter = refTransform.position + forward * forwardOffset + Vector3.up * hitHeightOffset;

            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.25f);
            if (shape == ColliderShape.Box)
            {
                Matrix4x4 prev = Gizmos.matrix;
                Gizmos.matrix = Matrix4x4.TRS(groundCenter, rot, Vector3.one);
                Gizmos.DrawCube(Vector3.up * boxHalfExtents.y, boxHalfExtents * 2f);
                Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
                Gizmos.DrawWireCube(Vector3.up * boxHalfExtents.y, boxHalfExtents * 2f);
                Gizmos.matrix = prev;
            }
            else
            {
                Gizmos.DrawSphere(hitCenter, sphereRadius);
                Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
                Gizmos.DrawWireSphere(hitCenter, sphereRadius);
            }
        }
    }
}
