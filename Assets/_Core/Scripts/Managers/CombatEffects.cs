using ActionRPG.CameraSystem;
using UnityEngine;

namespace ActionRPG.Managers
{
    /// <summary>
    /// Combat scripts use this helper to trigger shared effects such as pooled VFX and camera shake.
    /// Keeping this here prevents controllers from repeating pooling and AutoDespawn wiring.
    /// </summary>
    public static class CombatEffects
    {
        public static GameObject SpawnPooledVFX(
            GameObject prefab,
            Vector3 position,
            Quaternion rotation,
            float lifeTime,
            Transform parent = null)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance;
            if (ObjectPoolManager.Instance != null)
            {
                instance = ObjectPoolManager.Instance.Spawn(prefab, position, rotation, parent);
                EnsureAutoDespawn(instance, lifeTime);
            }
            else
            {
                instance = Object.Instantiate(prefab, position, rotation, parent);
                Object.Destroy(instance, lifeTime);
            }

            return instance;
        }

        public static void EnsureAutoDespawn(GameObject target, float lifeTime)
        {
            if (target == null || lifeTime <= 0f)
            {
                return;
            }

            AutoDespawn autoDespawn = target.GetComponent<AutoDespawn>();
            if (autoDespawn == null)
            {
                autoDespawn = target.AddComponent<AutoDespawn>();
            }

            autoDespawn.Restart(lifeTime);
        }

        public static void ShakeCamera(float duration, float magnitude)
        {
            CameraController cameraController = GetMainCameraController();
            if (cameraController != null)
            {
                cameraController.TriggerShake(duration, magnitude);
            }
        }

        public static void ShakeAndZoomCamera(float shakeDuration, float shakeMagnitude, float zoomAmount, float zoomDuration)
        {
            CameraController cameraController = GetMainCameraController();
            if (cameraController == null)
            {
                return;
            }

            cameraController.TriggerShake(shakeDuration, shakeMagnitude);
            cameraController.TriggerZoom(zoomAmount, zoomDuration);
        }

        private static CameraController GetMainCameraController()
        {
            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.GetComponent<CameraController>() : null;
        }
    }
}
