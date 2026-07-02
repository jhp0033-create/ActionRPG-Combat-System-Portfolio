using UnityEngine;

namespace ActionRPG.Player
{
    public partial class NetworkPlayerController
    {
        /// <summary>
        /// 플레이어가 피격당할 때 외부(적 공격, 함정 등)에서 호출합니다.
        /// PlayerWorldBar의 체력바도 함께 업데이트합니다.
        /// </summary>
        public void TakeDamage(float damageAmount, Vector3 hitPoint, Vector3 hitNormal, bool isCritical = false)
        {
            SetCombatActivity();
            if (currentHealth <= 0f) return;

            currentHealth = Mathf.Max(0f, currentHealth - damageAmount);

            PlayerStatusPresenter.SetHealth(playerWorldBar, profileHUD, currentHealth, maxHealth);

            TriggerHitFlash();

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private void Die()
        {
            currentHealth = 0;

            if (animator != null)
            {
                animator.SetTrigger("Die");
                animator.SetFloat("Speed", 0f);
            }

            controller.enabled = false;
            navAgent.enabled = false;
            isAutoMode = false;
        }

        private void TriggerHitFlash()
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }

            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private System.Collections.IEnumerator HitFlashRoutine()
        {
            yield return PlayerHitFlashEffect.Play(childRenderers, propBlock, () => currentHealth > 0f);
            hitFlashCoroutine = null;
        }

        /// <summary>
        /// 골드를 획득하고 UI 갱신 이벤트를 통지합니다.
        /// </summary>
        public void AddGold(int amount)
        {
            if (!progressionState.AddGold(amount)) return;

            OnGoldChanged?.Invoke(progressionState.Gold);
        }

        /// <summary>
        /// 경험치를 획득하고 필요 시 레벨업을 처리합니다.
        /// </summary>
        public void AddExp(float amount)
        {
            if (amount <= 0f) return;

            int levelUps = progressionState.AddExp(amount);
            for (int i = 0; i < levelUps; i++)
            {
                if (ActionRPG.Managers.SoundManager.Instance != null)
                {
                    ActionRPG.Managers.SoundManager.Instance.PlaySpawnSFX();
                }
            }

            if (levelUps > 0)
            {
                PlayerStatusPresenter.SetLevel(profileHUD, progressionState.Level);
            }

            PlayerStatusPresenter.SetExperience(profileHUD, progressionState.Exp, progressionState.RequiredExp);
        }

        // Called by animation events to open/close the active weapon damage window.
        public void EnableWeaponHitbox()
        {
            if (weaponManager != null) weaponManager.EnableCurrentWeaponHitbox();

            if (!isSkillComboActive && ActionRPG.Managers.SoundManager.Instance != null)
            {
                ActionRPG.Managers.SoundManager.Instance.PlaySwingSFX();
            }

            if (!isSkillComboActive)
            {
                if (Time.time - lastVfxSpawnTime < 0.15f)
                {
                    return;
                }

                lastVfxSpawnTime = Time.time;

                UnityEngine.Object prefabObj = null;

                if (ActionRPG.Data.VFXDatabase.Instance != null)
                {
                    prefabObj = ActionRPG.Data.VFXDatabase.Instance.defaultSlashVFXPrefab;
                }

                if (prefabObj is GameObject goPrefab)
                {
                    Vector3 spawnPos = transform.position + Vector3.up * 1.0f;

                    Vector3 eulerOffset = comboStep1Angle;
                    if (comboStep == 2) eulerOffset = comboStep2Angle;
                    else if (comboStep == 3) eulerOffset = comboStep3Angle;

                    Quaternion spawnRot = transform.rotation * Quaternion.Euler(eulerOffset);

                    ActionRPG.Managers.CombatEffects.SpawnPooledVFX(goPrefab, spawnPos, spawnRot, 1.5f, null);
                }
            }
        }

        public void DisableWeaponHitbox()
        {
            if (weaponManager != null) weaponManager.DisableCurrentWeaponHitbox();
        }
    }
}
