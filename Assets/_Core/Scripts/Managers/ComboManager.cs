using UnityEngine;
using System;

namespace ActionRPG.Managers
{
    /// <summary>
    /// 플레이어의 공격 콤보 수치를 관리하는 싱글턴 매니저입니다.
    /// 일정 시간 공격이 없으면 콤보가 초기화됩니다.
    /// </summary>
    public class ComboManager : MonoBehaviour
    {
        public static ComboManager Instance { get; private set; }

        public int CurrentCombo { get; private set; } = 0;
        
        [Tooltip("마지막 공격 이후 콤보가 유지되는 시간 (초)")]
        public float comboMaintainTime = 3.0f;
        
        private float lastAttackTime = -999f;
        
        // 콤보 변경 시 UI 등에 알리기 위한 이벤트
        public event Action<int> OnComboChanged;
        public event Action OnComboReset;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 플레이어의 전투 상태 변경 이벤트를 구독합니다.
            // (씬 로드 시퀀스에 따라 플레이어 객체가 늦게 찾아질 수 있으므로 Start에서 찾습니다)
            var playerCtrl = UnityEngine.Object.FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.OnCombatStateChanged += HandleCombatStateChanged;
            }
        }

        private void OnDestroy()
        {
            var playerCtrl = UnityEngine.Object.FindFirstObjectByType<ActionRPG.Player.NetworkPlayerController>();
            if (playerCtrl != null)
            {
                playerCtrl.OnCombatStateChanged -= HandleCombatStateChanged;
            }
        }

        private void HandleCombatStateChanged(bool isInCombat)
        {
            // 전투가 종료되어 일반 상태로 돌아갈 때 콤보를 초기화합니다.
            if (!isInCombat)
            {
                ResetCombo();
            }
        }

        /// <summary>
        /// 적을 타격했을 때 호출하여 콤보를 1 증가시킵니다.
        /// </summary>
        public void AddCombo()
        {
            CurrentCombo++;
            lastAttackTime = Time.time;
            
            OnComboChanged?.Invoke(CurrentCombo);
            
        }

        /// <summary>
        /// 콤보를 강제로 0으로 초기화합니다.
        /// </summary>
        public void ResetCombo()
        {
            if (CurrentCombo == 0) return;
            
            CurrentCombo = 0;
            OnComboReset?.Invoke();
            
        }
    }
}
