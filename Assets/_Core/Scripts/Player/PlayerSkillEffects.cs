using ActionRPG.Data;
using ActionRPG.Managers;
using UnityEngine;

namespace ActionRPG.Player
{
    /// <summary>
    /// Centralizes skill presentation details so NetworkPlayerController can focus on state flow.
    /// </summary>
    public static class PlayerSkillEffects
    {
        public static void PlayCastAudio(SkillData skill)
        {
            if (skill == null || SoundManager.Instance == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(skill.castSoundID))
            {
                if (skill.skillType == SkillType.ChargeAttack && skill.castSoundID == skill.skillImpactSoundID)
                {
                    return;
                }

                SoundManager.Instance.PlaySFXByKey(skill.castSoundID);
            }

            if (skill.skillType != SkillType.ShieldBash && skill.skillType != SkillType.ChargeAttack)
            {
                SoundManager.Instance.PlaySwingSFX();
            }
        }

        public static void PlayImpactAudio(SkillData skill, bool hitAny)
        {
            if (skill == null || SoundManager.Instance == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(skill.skillImpactSoundID))
            {
                SoundManager.Instance.PlaySFXByKey(skill.skillImpactSoundID);
            }
        }

        public static GameObject SpawnCastVFX(
            SkillData skill,
            Transform caster,
            Transform currentTarget,
            Transform canvasParent,
            Quaternion? rotationOffset = null)
        {
            if (skill == null || caster == null || skill.vfxPrefab == null || skill.skillType == SkillType.ChargeAttack)
            {
                return null;
            }

            Vector3 spawnPos = GetForwardSkillPosition(skill, caster, currentTarget);
            Transform parentTransform = skill.skillType == SkillType.ShieldBash ? canvasParent : null;
            Quaternion spawnRot = caster.rotation * skill.vfxPrefab.transform.rotation;
            if (rotationOffset.HasValue)
            {
                spawnRot = spawnRot * rotationOffset.Value;
            }
            float vfxScale = skill.vfxScaleMultiplier > 0f ? skill.vfxScaleMultiplier : 1f;

            GameObject vfx = CombatEffects.SpawnPooledVFX(skill.vfxPrefab, spawnPos, spawnRot, 3f, parentTransform);
            ApplySkillVFXScale(vfx, skill.vfxPrefab, parentTransform, vfxScale);
            return vfx;
        }

        public static GameObject SpawnChargeVFX(GameObject prefab, Transform parentBone)
        {
            if (prefab == null || parentBone == null)
            {
                return null;
            }

            GameObject vfx = ObjectPoolManager.Instance != null
                ? ObjectPoolManager.Instance.Spawn(prefab, parentBone.position, parentBone.rotation, parentBone)
                : Object.Instantiate(prefab, parentBone.position, parentBone.rotation, parentBone);

            AttachToBone(vfx, parentBone);
            return vfx;
        }

        public static GameObject SpawnImpactVFX(
            SkillData skill,
            Transform caster,
            Transform currentTarget,
            out float lifeTime,
            Quaternion? rotationOffset = null)
        {
            lifeTime = 3f;
            if (skill == null || caster == null || skill.vfxPrefab == null)
            {
                return null;
            }

            Vector3 spawnPos = GetForwardSkillPosition(skill, caster, currentTarget);
            float vfxScale = skill.vfxScaleMultiplier > 0f ? skill.vfxScaleMultiplier : 1f;
            if (skill.aoeRadius > 0f)
            {
                vfxScale *= skill.aoeRadius / 2.5f;
            }

            Quaternion rotation = caster.rotation * skill.vfxPrefab.transform.rotation;
            if (rotationOffset.HasValue)
            {
                rotation = rotation * rotationOffset.Value;
            }
            GameObject vfx = CombatEffects.SpawnPooledVFX(skill.vfxPrefab, spawnPos, rotation, lifeTime);
            if (vfx != null)
            {
                vfx.transform.localScale = skill.vfxPrefab.transform.localScale * vfxScale;
            }

            return vfx;
        }

        public static void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Despawn(instance);
            }
            else
            {
                Object.Destroy(instance);
            }
        }

        public static void AttachToBone(GameObject vfxInstance, Transform parentBone)
        {
            if (vfxInstance == null || parentBone == null)
            {
                return;
            }

            vfxInstance.transform.SetParent(parentBone, false);
            vfxInstance.transform.localPosition = Vector3.zero;
            vfxInstance.transform.localRotation = Quaternion.identity;
        }

        private static Vector3 GetForwardSkillPosition(SkillData skill, Transform caster, Transform currentTarget)
        {
            Vector3 forward = caster.forward;
            if (currentTarget != null)
            {
                Vector3 toTarget = currentTarget.position - caster.position;
                toTarget.y = 0f;
                if (toTarget != Vector3.zero)
                {
                    forward = toTarget.normalized;
                }
            }

            return caster.position + forward * skill.vfxForwardOffset + Vector3.up * skill.vfxHeightOffset;
        }

        private static void ApplySkillVFXScale(GameObject vfx, GameObject prefab, Transform parentTransform, float vfxScale)
        {
            if (vfx == null)
            {
                return;
            }

            if (parentTransform != null)
            {
                RectTransform rect = vfx.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.localPosition = Vector3.zero;
                    rect.localScale = prefab != null ? prefab.transform.localScale : Vector3.one;
                }
            }
            else
            {
                vfx.transform.localScale = (prefab != null ? prefab.transform.localScale : Vector3.one) * vfxScale;
            }
        }
    }
}
