using ActionRPG.Data;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AI;

namespace ActionRPG.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerChargedSkillService : MonoBehaviour
    {
        private NetworkPlayerController player;
        private PlayerNavigationService navigationService;
        private NavMeshAgent navAgent;
        private Animator animator;
        private Coroutine chargeRoutine;
        private GameObject currentIndicatorObj;
        private Vector3 lastIndicatorPosition;

        public bool IsCasting => chargeRoutine != null;

        public void Initialize(
            NetworkPlayerController playerController,
            PlayerNavigationService navigation,
            NavMeshAgent agent,
            Animator characterAnimator)
        {
            player = playerController;
            navigationService = navigation;
            navAgent = agent;
            animator = characterAnimator;
        }

        public void Cast(int skillIndex)
        {
            Cancel();
            chargeRoutine = StartCoroutine(ChargeRoutine(skillIndex));
        }

        public void Cancel()
        {
            if (chargeRoutine != null)
            {
                StopCoroutine(chargeRoutine);
                chargeRoutine = null;
            }

            ClearIndicator();
            player.ReleaseCurrentChargeVfx();
            player.SetChargePostHitFrozen(false);
            player.SetChargingAttack(false);
            player.EndChargeCameraLock();
            player.HideChargeHitAreaPreview();
        }

        private System.Collections.IEnumerator ChargeRoutine(int skillIndex)
        {
            SkillData skill = player.equippedSkills[skillIndex];
            if (skill == null)
            {
                chargeRoutine = null;
                yield break;
            }

            player.SetChargingAttack(true);
            player.MarkAttackStarted();

            yield return PlayChargeStartupAnimation(skill);
            TriggerChargeCameraZoom();
            SpawnChargeVfx(skill);
            ShowIndicator(skill);

            Vector3 originalScale = transform.localScale;
            Tween chargeTween = transform.DOShakeScale(skill.chargeTime, new Vector3(0.04f, 0.04f, 0.04f), 30, 90f);

            yield return new WaitForSeconds(skill.chargeTime);

            if (chargeTween != null)
            {
                chargeTween.Kill();
            }
            transform.localScale = originalScale;

            if (animator != null)
            {
                float speedScale = skill.animSpeedMultiplier > 0f ? skill.animSpeedMultiplier : 1.0f;
                animator.speed = speedScale;
            }

            player.ReleaseCurrentChargeVfx();
            EnsureDamageOrigin(skill);
            DespawnIndicator();

            player.activeSkillIndex = skillIndex;
            player.ExecuteSkillDamage();

            if (!player.IsChargeCameraLocked)
            {
                player.BeginChargeCameraLock(null, 0.3f);
            }

            float postHitFreeze = skill.chargePostHitFreezeDuration > 0f ? skill.chargePostHitFreezeDuration : 0.3f;
            player.SetChargePostHitFrozen(true);
            yield return new WaitForSeconds(postHitFreeze);

            if (animator != null)
            {
                animator.SetTrigger("ChargeEnd");
            }

            player.SetChargePostHitFrozen(false);
            player.HideChargeHitAreaPreview();
            player.ReleaseChargeAttackMovementLock();
            player.SetChargingAttack(false);
            chargeRoutine = null;
        }

        private System.Collections.IEnumerator PlayChargeStartupAnimation(SkillData skill)
        {
            if (animator == null)
            {
                yield break;
            }

            animator.speed = 1.0f;

            StopNavAgentForCharge();
            string triggerName = skill.animationTriggerName + "_Full";
            player.SetMovingAttack(false);
            animator.SetTrigger(triggerName);

            float waitTimer = 0f;
            while (!animator.IsInTransition(0) && waitTimer < 0.1f)
            {
                waitTimer += Time.deltaTime;
                yield return null;
            }

            while (animator.IsInTransition(0))
            {
                yield return null;
            }

            float freezePoint = skill.chargeAnimationStartNormalizedTime > 0f
                ? skill.chargeAnimationStartNormalizedTime
                : 0.2f;

            while (true)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

                if (!animator.IsInTransition(0) && stateInfo.normalizedTime >= freezePoint)
                {
                    animator.speed = 0f;
                    StopNavAgentForCharge();
                    break;
                }

                if (stateInfo.normalizedTime >= 1.0f)
                {
                    break;
                }

                yield return null;
            }
        }

        private void TriggerChargeCameraZoom()
        {
            if (Camera.main == null)
            {
                return;
            }

            var camController = Camera.main.GetComponent<ActionRPG.CameraSystem.CameraController>();
            if (camController != null)
            {
                camController.TriggerZoom(10f, 1.15f);
            }
        }

        private void SpawnChargeVfx(SkillData skill)
        {
            player.ReleaseCurrentChargeVfx();

            if (skill.chargeVfxPrefab == null)
            {
                return;
            }

            Transform parentBone = player.skillChargePoint != null ? player.skillChargePoint : transform;
            player.SetCurrentChargeVfx(PlayerSkillEffects.SpawnChargeVFX(skill.chargeVfxPrefab, parentBone));
        }

        private void ShowIndicator(SkillData skill)
        {
            player.ShowChargeHitAreaPreview(skill);
            ClearIndicator();

            if (skill.chargeIndicatorPrefab == null)
            {
                lastIndicatorPosition = transform.position + transform.forward * 1.5f;
                lastIndicatorPosition.y = 0.05f;
                return;
            }

            FaceChargeDirection();

            ChargeAttackHitArea hitPreview = player.EnsureChargeHitAreaPreview();
            hitPreview.SyncFromSkill(skill);

            float gaugeScale = hitPreview.GaugeDiameter;
            float gaugeForward = hitPreview.GaugeForwardOffset;

            Vector3 indicatorPos = transform.position + transform.forward * gaugeForward;
            indicatorPos.y = 0.05f;
            lastIndicatorPosition = indicatorPos;

            Quaternion indicatorRot = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * skill.chargeIndicatorPrefab.transform.rotation;
            currentIndicatorObj = SpawnIndicatorObject(skill, indicatorPos, indicatorRot);
            ConfigureIndicator(skill, currentIndicatorObj, gaugeScale, gaugeForward, transform);
        }

        private void FaceChargeDirection()
        {
            if (Camera.main != null)
            {
                Vector3 camForward = Camera.main.transform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.01f)
                {
                    transform.rotation = Quaternion.LookRotation(camForward.normalized);
                }
            }
            else if (player.currentTarget != null)
            {
                navigationService.FaceHorizontalPosition(player.currentTarget.position);
            }
        }

        private GameObject SpawnIndicatorObject(SkillData skill, Vector3 position, Quaternion rotation)
        {
            Transform canvasParent = player.GetEffectCanvasParent();
            if (ActionRPG.Managers.ObjectPoolManager.Instance != null)
            {
                return ActionRPG.Managers.ObjectPoolManager.Instance.Spawn(
                    skill.chargeIndicatorPrefab,
                    position,
                    rotation,
                    canvasParent);
            }

            return Instantiate(skill.chargeIndicatorPrefab, position, rotation, canvasParent);
        }

        private static void ConfigureIndicator(
            SkillData skill,
            GameObject indicatorObj,
            float gaugeScale,
            float gaugeForward,
            Transform caster)
        {
            if (indicatorObj == null)
            {
                return;
            }

            ActionRPG.UI.SkillIndicator indicator = indicatorObj.GetComponent<ActionRPG.UI.SkillIndicator>();
            if (indicator != null)
            {
                indicator.StartCharging(skill.chargeTime, gaugeScale);
                return;
            }

            var gauge = indicatorObj.GetComponent<ActionRPG.UI.UI_ChargeGauge>();
            if (gauge == null)
            {
                gauge = indicatorObj.GetComponentInChildren<ActionRPG.UI.UI_ChargeGauge>(true);
            }

            if (gauge == null)
            {
                return;
            }

            gauge.targetTransform = caster;
            gauge.casterTransform = caster;
            gauge.useSpacebarTrigger = false;
            gauge.syncRotationToCaster = true;
            gauge.forwardOffset = gaugeForward;

            if (indicatorObj.TryGetComponent<RectTransform>(out RectTransform rect))
            {
                rect.localScale = Vector3.one;
            }

            gauge.StartPreviewFromCode(skill.chargeTime, gaugeScale);
        }

        private void EnsureDamageOrigin(SkillData skill)
        {
            if (lastIndicatorPosition != Vector3.zero)
            {
                return;
            }

            float forwardDistance = skill.range > 0f ? skill.range : 3f;
            lastIndicatorPosition = transform.position + transform.forward * forwardDistance;
            lastIndicatorPosition.y = 0.05f;
        }

        private void DespawnIndicator()
        {
            GameObject indicatorToRemove = currentIndicatorObj;
            currentIndicatorObj = null;

            if (indicatorToRemove != null)
            {
                StartCoroutine(DespawnAfterDelay(indicatorToRemove, 0f));
            }
        }

        private void ClearIndicator()
        {
            if (currentIndicatorObj == null)
            {
                return;
            }

            if (ActionRPG.Managers.ObjectPoolManager.Instance != null)
            {
                ActionRPG.Managers.ObjectPoolManager.Instance.Despawn(currentIndicatorObj);
            }
            else
            {
                Destroy(currentIndicatorObj);
            }

            currentIndicatorObj = null;
        }

        private System.Collections.IEnumerator DespawnAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj == null)
            {
                yield break;
            }

            if (ActionRPG.Managers.ObjectPoolManager.Instance != null && obj.activeInHierarchy)
            {
                ActionRPG.Managers.ObjectPoolManager.Instance.Despawn(obj);
            }
            else if (obj != null)
            {
                Destroy(obj);
            }
        }

        private void StopNavAgentForCharge()
        {
            if (navAgent == null || !navAgent.enabled)
            {
                return;
            }

            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
        }
    }
}
