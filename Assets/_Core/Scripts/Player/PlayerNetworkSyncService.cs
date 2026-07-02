using System.Collections.Generic;
using ActionRPG.Network;
using UnityEngine;

namespace ActionRPG.Player
{
    /// <summary>
    /// 로컬 이동 스냅샷을 목업 서버로 전송하고, 응답 스냅샷을 보간 버퍼로 소비합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerNetworkSyncService : MonoBehaviour
    {
        [Header("Send")]
        [SerializeField] private int actorId = -1;
        [SerializeField, Range(2f, 30f)] private float sendRate = 10f;

        [Header("Interpolation")]
        [SerializeField, Range(0.02f, 0.4f)] private float interpolationBackTime = 0.12f;
        [SerializeField, Range(1f, 30f)] private float correctionLerpSpeed = 14f;
        [SerializeField, Range(2, 64)] private int maxBufferedSnapshots = 32;

        private readonly List<MovementSnapshot> snapshotBuffer = new List<MovementSnapshot>();
        private Transform sourceTransform;
        private float nextSendTime;
        private float lastLocalSampleTime;
        private Vector3 lastLocalPosition;
        private int sequence;

        public Vector3 InterpolatedPosition { get; private set; }
        public Quaternion InterpolatedRotation { get; private set; } = Quaternion.identity;
        public float ServerPositionError { get; private set; }
        public bool HasServerSnapshot => snapshotBuffer.Count > 0;

        public void Initialize(Transform source)
        {
            sourceTransform = source;
            if (actorId == -1 && sourceTransform != null)
            {
                actorId = sourceTransform.GetInstanceID();
            }

            Vector3 startPosition = sourceTransform != null ? sourceTransform.position : transform.position;
            Quaternion startRotation = sourceTransform != null ? sourceTransform.rotation : transform.rotation;

            InterpolatedPosition = startPosition;
            InterpolatedRotation = startRotation;
            lastLocalPosition = startPosition;
            lastLocalSampleTime = Time.time;
        }

        public void Tick(Vector3 currentPosition, Quaternion currentRotation)
        {
            float now = Time.time;
            float deltaTime = Mathf.Max(0.0001f, now - lastLocalSampleTime);
            Vector3 velocity = (currentPosition - lastLocalPosition) / deltaTime;

            SendMovementSnapshotIfNeeded(now, currentPosition, currentRotation, velocity);
            UpdateInterpolatedSnapshot(now);

            lastLocalPosition = currentPosition;
            lastLocalSampleTime = now;
            ServerPositionError = Vector3.Distance(currentPosition, InterpolatedPosition);
        }

        private void SendMovementSnapshotIfNeeded(float now, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (MockNetworkManager.Instance == null)
            {
                InterpolatedPosition = position;
                InterpolatedRotation = rotation;
                return;
            }

            if (now < nextSendTime)
            {
                return;
            }

            float interval = 1f / Mathf.Max(1f, sendRate);
            nextSendTime = now + interval;
            sequence++;

            MockNetworkManager.Instance.SendMovementSnapshot(
                actorId,
                position,
                rotation,
                velocity,
                sequence,
                AddServerSnapshot);
        }

        private void AddServerSnapshot(MovementSnapshot snapshot)
        {
            if (snapshot.actorId != actorId)
            {
                return;
            }

            int insertIndex = snapshotBuffer.Count;
            for (int i = 0; i < snapshotBuffer.Count; i++)
            {
                if (snapshot.serverTime < snapshotBuffer[i].serverTime)
                {
                    insertIndex = i;
                    break;
                }
            }

            snapshotBuffer.Insert(insertIndex, snapshot);

            while (snapshotBuffer.Count > maxBufferedSnapshots)
            {
                snapshotBuffer.RemoveAt(0);
            }
        }

        private void UpdateInterpolatedSnapshot(float now)
        {
            if (snapshotBuffer.Count == 0)
            {
                return;
            }

            float renderTime = now - interpolationBackTime;

            while (snapshotBuffer.Count >= 2 && snapshotBuffer[1].serverTime <= renderTime)
            {
                snapshotBuffer.RemoveAt(0);
            }

            MovementSnapshot from = snapshotBuffer[0];
            MovementSnapshot to = snapshotBuffer.Count >= 2 ? snapshotBuffer[1] : snapshotBuffer[0];

            float duration = Mathf.Max(0.0001f, to.serverTime - from.serverTime);
            float t = snapshotBuffer.Count >= 2 ? Mathf.Clamp01((renderTime - from.serverTime) / duration) : 1f;

            Vector3 targetPosition = Vector3.Lerp(from.position, to.position, t);
            Quaternion targetRotation = Quaternion.Slerp(from.rotation, to.rotation, t);
            float follow = 1f - Mathf.Exp(-correctionLerpSpeed * Time.deltaTime);

            InterpolatedPosition = Vector3.Lerp(InterpolatedPosition, targetPosition, follow);
            InterpolatedRotation = Quaternion.Slerp(InterpolatedRotation, targetRotation, follow);
        }
    }
}
