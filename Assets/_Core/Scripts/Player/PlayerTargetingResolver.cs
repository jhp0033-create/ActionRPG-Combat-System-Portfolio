using System.Collections.Generic;
using ActionRPG.Enemy;
using UnityEngine;

namespace ActionRPG.Player
{
    /// <summary>
    /// Resolves player target selection without depending on NetworkPlayerController state.
    /// This keeps targeting rules testable and keeps the player controller focused on orchestration.
    /// </summary>
    public static class PlayerTargetingResolver
    {
        public readonly struct TargetResult
        {
            public readonly Transform Target;
            public readonly EnemyController Controller;

            public TargetResult(Transform target, EnemyController controller)
            {
                Target = target;
                Controller = controller;
            }

            public bool HasTarget => Target != null && Controller != null;
        }

        private struct ViewportCandidate
        {
            public Transform Transform;
            public EnemyController Controller;
            public float ViewportX;

            public ViewportCandidate(Transform transform, EnemyController controller, float viewportX)
            {
                Transform = transform;
                Controller = controller;
                ViewportX = viewportX;
            }
        }

        public static TargetResult FindClosest(IEnumerable<GameObject> activeEnemies, Vector3 origin, float radius)
        {
            float closestDistance = Mathf.Infinity;
            Transform bestTarget = null;
            EnemyController bestController = null;

            foreach (GameObject enemyObject in activeEnemies)
            {
                if (!TryResolveEnemy(enemyObject, out EnemyController enemy))
                {
                    continue;
                }

                float distanceToEnemy = Vector3.Distance(origin, enemy.transform.position);
                if (distanceToEnemy <= radius && distanceToEnemy < closestDistance)
                {
                    closestDistance = distanceToEnemy;
                    bestTarget = enemy.transform;
                    bestController = enemy;
                }
            }

            return new TargetResult(bestTarget, bestController);
        }

        public static TargetResult FindHorizontalNeighbor(
            IEnumerable<GameObject> activeEnemies,
            Vector3 origin,
            float radius,
            Transform currentTarget,
            bool toLeft,
            Camera camera)
        {
            if (camera == null)
            {
                return default;
            }

            List<ViewportCandidate> candidates = BuildViewportCandidates(activeEnemies, origin, radius, camera, onlyVisible: true);
            if (candidates.Count == 0)
            {
                candidates = BuildViewportCandidates(activeEnemies, origin, radius, camera, onlyVisible: false);
            }

            if (candidates.Count == 0)
            {
                return default;
            }

            candidates.Sort((a, b) => a.ViewportX.CompareTo(b.ViewportX));

            float currentX = 0.5f;
            bool hasCurrentTarget = false;
            if (currentTarget != null && currentTarget.gameObject.activeInHierarchy)
            {
                Vector3 currentViewportPos = camera.WorldToViewportPoint(currentTarget.position);
                if (currentViewportPos.z > 0f)
                {
                    currentX = currentViewportPos.x;
                    hasCurrentTarget = true;
                }
            }

            ViewportCandidate? selected = !hasCurrentTarget
                ? FindNearestToCenter(candidates)
                : FindNeighbor(candidates, currentTarget, currentX, toLeft);

            return selected.HasValue
                ? new TargetResult(selected.Value.Transform, selected.Value.Controller)
                : default;
        }

        private static List<ViewportCandidate> BuildViewportCandidates(
            IEnumerable<GameObject> activeEnemies,
            Vector3 origin,
            float radius,
            Camera camera,
            bool onlyVisible)
        {
            List<ViewportCandidate> candidates = new List<ViewportCandidate>();
            foreach (GameObject enemyObject in activeEnemies)
            {
                if (!TryResolveEnemy(enemyObject, out EnemyController enemy))
                {
                    continue;
                }

                if (!onlyVisible && Vector3.Distance(origin, enemy.transform.position) > radius)
                {
                    continue;
                }

                Vector3 viewportPos = camera.WorldToViewportPoint(enemy.transform.position);
                bool isVisible = viewportPos.z > 0f &&
                    viewportPos.x >= 0f && viewportPos.x <= 1f &&
                    viewportPos.y >= 0f && viewportPos.y <= 1f;

                if (onlyVisible && !isVisible)
                {
                    continue;
                }

                candidates.Add(new ViewportCandidate(enemy.transform, enemy, viewportPos.x));
            }

            return candidates;
        }

        private static ViewportCandidate? FindNearestToCenter(List<ViewportCandidate> candidates)
        {
            float minDiff = Mathf.Infinity;
            ViewportCandidate? best = null;

            foreach (ViewportCandidate candidate in candidates)
            {
                float diff = Mathf.Abs(candidate.ViewportX - 0.5f);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    best = candidate;
                }
            }

            return best;
        }

        private static ViewportCandidate? FindNeighbor(
            List<ViewportCandidate> candidates,
            Transform currentTarget,
            float currentX,
            bool toLeft)
        {
            const float Epsilon = 0.01f;
            ViewportCandidate? best = null;
            float bestX = toLeft ? -1f : 2f;

            foreach (ViewportCandidate candidate in candidates)
            {
                if (candidate.Transform == currentTarget)
                {
                    continue;
                }

                bool isCandidate = toLeft
                    ? candidate.ViewportX < currentX - Epsilon && candidate.ViewportX > bestX
                    : candidate.ViewportX > currentX + Epsilon && candidate.ViewportX < bestX;

                if (isCandidate)
                {
                    bestX = candidate.ViewportX;
                    best = candidate;
                }
            }

            return best ?? FindWrappedNeighbor(candidates, currentTarget, toLeft);
        }

        private static ViewportCandidate? FindWrappedNeighbor(
            List<ViewportCandidate> candidates,
            Transform currentTarget,
            bool toLeft)
        {
            ViewportCandidate? best = null;
            float bestX = toLeft ? -1f : 2f;

            foreach (ViewportCandidate candidate in candidates)
            {
                if (candidate.Transform == currentTarget)
                {
                    continue;
                }

                bool isBetter = toLeft
                    ? candidate.ViewportX > bestX
                    : candidate.ViewportX < bestX;

                if (isBetter)
                {
                    bestX = candidate.ViewportX;
                    best = candidate;
                }
            }

            return best;
        }

        private static bool TryResolveEnemy(GameObject enemyObject, out EnemyController enemy)
        {
            enemy = null;
            if (enemyObject == null || !enemyObject.activeInHierarchy)
            {
                return false;
            }

            enemy = enemyObject.GetComponent<EnemyController>();
            return enemy != null && !enemy.IsDead && !enemy.IsSpawning;
        }
    }
}
