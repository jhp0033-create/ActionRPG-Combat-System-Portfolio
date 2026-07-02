using UnityEngine;
using System.Collections.Generic;

namespace ActionRPG.Player
{
    /// <summary>
    /// 무기 데이터를 정의하는 구조체입니다.
    /// 무기마다 모양과 손잡이 위치가 다르기 때문에, 각 무기 전용 오프셋(위치/회전)을 세팅할 수 있습니다.
    /// </summary>
    [System.Serializable]
    public class WeaponData
    {
        public string weaponName;

        public GameObject weaponPrefab;
        
        [Header("Grip Offsets")]
        public Vector3 positionOffset;
        public Vector3 rotationOffset; // 인스펙터에서 편하게 보기 위한 Euler Angles
        
        [Header("Stats")]
        public float attackPower = 10f;
        public float criticalChance = 0.2f; // 치명타 확률 (0~1)
        public float criticalDamageMultiplier = 1.5f; // 치명타 피해 배율
    }

    /// <summary>
    /// 캐릭터의 오른손/왼손 소켓을 관리하고, 코드를 통해 동적으로 무기를 장착(스왑)하는 매니저입니다.
    /// </summary>
    public class WeaponManager : MonoBehaviour
    {
        private const float DefaultAttackPower = 10f;

        [Tooltip("캐릭터의 오른손 뼈대(Bone) Transform을 여기에 드래그하세요.")]
        public Transform rightHandSocket;

        [Tooltip("게임 내에서 사용할 무기들의 데이터 리스트")]
        public List<WeaponData> availableWeapons = new List<WeaponData>();

        // 현재 장착된 무기의 게임 오브젝트 인스턴스
        private GameObject currentWeaponInstance;
        private WeaponData currentWeaponData;
        private WeaponCollider currentWeaponCollider;

        private void Start()
        {
            // 1. 씬에 이미 손 소켓에 무기가 달려있을 경우 자동 인식
            if (currentWeaponInstance == null && rightHandSocket != null && rightHandSocket.childCount > 0)
            {
                currentWeaponInstance = rightHandSocket.GetChild(0).gameObject;
                currentWeaponData = GetFirstAvailableWeaponData();
                currentWeaponCollider = ResolveWeaponCollider(currentWeaponInstance);

            }
            // 2. 무기가 없고 리스트에 무기가 있다면 첫 번째 무기 자동 장착
            else if (currentWeaponInstance == null && availableWeapons.Count > 0)
            {
                EquipWeapon(0);
            }
        }

        /// <summary>
        /// 인덱스 번호로 무기를 장착합니다. (UI 버튼 등에서 호출 가능)
        /// </summary>
        public void EquipWeapon(int index)
        {
            if (rightHandSocket == null)
            {
                return;
            }

            if (index < 0 || index >= availableWeapons.Count)
            {
                return;
            }

            // 1. 기존에 들고 있던 무기가 있다면 파괴 (스왑 로직)
            if (currentWeaponInstance != null)
            {
                Destroy(currentWeaponInstance);
            }

            // 2. 새 무기 데이터 가져오기
            currentWeaponData = availableWeapons[index];
            if (currentWeaponData.weaponPrefab == null) return;

            // 3. 무기 생성 및 소켓(오른손)의 자식으로 등록
            currentWeaponInstance = Instantiate(currentWeaponData.weaponPrefab, rightHandSocket);

            // 4. 무기마다 다른 손잡이 오프셋(위치, 각도) 적용
            currentWeaponInstance.transform.localPosition = currentWeaponData.positionOffset;
            currentWeaponInstance.transform.localRotation = Quaternion.Euler(currentWeaponData.rotationOffset);
            
            // 크기는 원본 프리팹 유지
            currentWeaponInstance.transform.localScale = Vector3.one;

            // 무기 프리팹의 실제 콜라이더가 자식에 있을 수 있으므로 하위 구조까지 확인합니다.
            currentWeaponCollider = ResolveWeaponCollider(currentWeaponInstance);

        }

        public Transform GetCurrentWeaponTransform()
        {
            if (currentWeaponInstance != null) return currentWeaponInstance.transform;
            return rightHandSocket;
        }

        // 데미지 계산기(CombatResolver)에서 현재 무기 스탯을 가져올 때 사용합니다.
        public float GetCurrentWeaponDamage()
        {
            if (currentWeaponData != null && currentWeaponData.attackPower > 0f)
            {
                return currentWeaponData.attackPower;
            }

            WeaponData defaultData = GetFirstAvailableWeaponData();
            if (defaultData != null && defaultData.attackPower > 0f)
            {
                return defaultData.attackPower;
            }

            return DefaultAttackPower;
        }

        public float GetCurrentWeaponCriticalChance()
        {
            if (currentWeaponData != null) return currentWeaponData.criticalChance;
            WeaponData defaultData = GetFirstAvailableWeaponData();
            if (defaultData != null) return defaultData.criticalChance;
            return 0.2f;
        }

        public float GetCurrentWeaponCriticalMultiplier()
        {
            if (currentWeaponData != null) return currentWeaponData.criticalDamageMultiplier;
            WeaponData defaultData = GetFirstAvailableWeaponData();
            if (defaultData != null) return defaultData.criticalDamageMultiplier;
            return 1.5f;
        }

        // --- 물리 타격 판정 제어 브릿지 ---

        public void EnableCurrentWeaponHitbox()
        {
            if (currentWeaponCollider != null) currentWeaponCollider.EnableHitbox();
        }

        public void DisableCurrentWeaponHitbox()
        {
            if (currentWeaponCollider != null) currentWeaponCollider.DisableHitbox();
        }

        private WeaponData GetFirstAvailableWeaponData()
        {
            for (int i = 0; i < availableWeapons.Count; i++)
            {
                if (availableWeapons[i] != null)
                {
                    return availableWeapons[i];
                }
            }

            return null;
        }

        private WeaponCollider ResolveWeaponCollider(GameObject weaponRoot)
        {
            if (weaponRoot == null) return null;

            WeaponCollider existingWeaponCollider = weaponRoot.GetComponentInChildren<WeaponCollider>(true);
            if (existingWeaponCollider != null)
            {
                return existingWeaponCollider;
            }

            Collider existingCollider = weaponRoot.GetComponentInChildren<Collider>(true);
            if (existingCollider != null)
            {
                return existingCollider.gameObject.AddComponent<WeaponCollider>();
            }

            return weaponRoot.AddComponent<WeaponCollider>();
        }
    }
}
