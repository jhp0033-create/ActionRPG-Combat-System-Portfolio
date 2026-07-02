using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace ActionRPG.Network
{
    public struct MovementSnapshot
    {
        public int actorId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public float serverTime;
        public int sequence;

        public MovementSnapshot(int actorId, Vector3 position, Quaternion rotation, Vector3 velocity, float serverTime, int sequence)
        {
            this.actorId = actorId;
            this.position = position;
            this.rotation = rotation;
            this.velocity = velocity;
            this.serverTime = serverTime;
            this.sequence = sequence;
        }
    }

    /// <summary>
    /// 전투 타격과 이동 스냅샷에 지연, 승인, 서버 응답 흐름을 적용하는 목업 네트워크 매니저입니다.
    /// </summary>
    public class MockNetworkManager : MonoBehaviour
    {
        public static MockNetworkManager Instance { get; private set; }

        [Header("Network Settings")]
        [Tooltip("클라이언트와 서버 간의 왕복 통신 지연 시간(초). 예: 0.15 = 150ms")]
        [Range(0f, 1f)]
        public float simulatedPingDelay = 0.15f;

        [Tooltip("서버에서의 가짜 타격 무시 확률 (롤백 테스트용)")]
        [Range(0f, 1f)]
        public float dropPacketRate = 0.0f; 

        [Header("Movement Sync")]
        [Tooltip("이동 스냅샷 응답에 추가할 가변 지연입니다.")]
        [Range(0f, 0.2f)]
        public float movementJitter = 0.02f;

        [Tooltip("서버 스냅샷 간 위치 차이가 이 값보다 크면 순간이동성 보정으로 간주합니다.")]
        public float movementSnapDistance = 8f;

        private readonly Dictionary<int, MovementSnapshot> serverMovementStates = new Dictionary<int, MovementSnapshot>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 타격 요청을 지연 처리한 뒤 승인 결과를 콜백으로 반환합니다.
        /// </summary>
        /// <param name="target">타격 대상</param>
        /// <param name="damage">요청 데미지</param>
        /// <param name="onServerConfirmed">승인 결과 콜백</param>
        public void SendHitRequest(ActionRPG.Core.IDamageable target, float damage, Vector3 hitPoint, Vector3 pushNormal, bool isCritical, Action<bool> onServerConfirmed)
        {
            StartCoroutine(ProcessHitRequestRoutine(target, damage, hitPoint, pushNormal, isCritical, onServerConfirmed));
        }

        /// <summary>
        /// 클라이언트 이동 스냅샷을 서버 시간 기준 스냅샷으로 변환해 반환합니다.
        /// </summary>
        public void SendMovementSnapshot(int actorId, Vector3 position, Quaternion rotation, Vector3 velocity, int sequence, Action<MovementSnapshot> onServerSnapshot)
        {
            StartCoroutine(ProcessMovementSnapshotRoutine(actorId, position, rotation, velocity, sequence, onServerSnapshot));
        }

        private IEnumerator ProcessHitRequestRoutine(ActionRPG.Core.IDamageable target, float damage, Vector3 hitPoint, Vector3 pushNormal, bool isCritical, Action<bool> onServerConfirmed)
        {
            yield return new WaitForSeconds(simulatedPingDelay / 2f);

            bool isHitConfirmed = true;

            if (UnityEngine.Random.value < dropPacketRate)
            {
                isHitConfirmed = false;
            }
            else
            {
                if (target != null)
                {
                    target.TakeDamage(damage, hitPoint, pushNormal, isCritical);
                }
            }

            yield return new WaitForSeconds(simulatedPingDelay / 2f);
            
            onServerConfirmed?.Invoke(isHitConfirmed);
        }

        private IEnumerator ProcessMovementSnapshotRoutine(
            int actorId,
            Vector3 position,
            Quaternion rotation,
            Vector3 velocity,
            int sequence,
            Action<MovementSnapshot> onServerSnapshot)
        {
            yield return new WaitForSeconds(GetOneWayDelayWithJitter());

            MovementSnapshot acceptedSnapshot = CreateAuthoritativeMovementSnapshot(actorId, position, rotation, velocity, sequence);
            serverMovementStates[actorId] = acceptedSnapshot;

            yield return new WaitForSeconds(GetOneWayDelayWithJitter());

            onServerSnapshot?.Invoke(acceptedSnapshot);
        }

        private MovementSnapshot CreateAuthoritativeMovementSnapshot(
            int actorId,
            Vector3 position,
            Quaternion rotation,
            Vector3 velocity,
            int sequence)
        {
            if (serverMovementStates.TryGetValue(actorId, out MovementSnapshot previous))
            {
                float distance = Vector3.Distance(previous.position, position);
                if (distance > movementSnapDistance)
                {
                    velocity = Vector3.zero;
                }
            }

            return new MovementSnapshot(actorId, position, rotation, velocity, Time.time, sequence);
        }

        private float GetOneWayDelayWithJitter()
        {
            float jitter = movementJitter > 0f ? UnityEngine.Random.Range(-movementJitter, movementJitter) : 0f;
            return Mathf.Max(0f, simulatedPingDelay * 0.5f + jitter);
        }
    }
}
