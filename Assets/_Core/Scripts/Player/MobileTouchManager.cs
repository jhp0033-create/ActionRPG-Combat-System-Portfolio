using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using ActionRPG.Managers;
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace ActionRPG.Player
{
    /// <summary>
    /// 모바일 환경을 위한 듀얼 터치 매니저입니다.
    /// 화면 좌측은 이동 입력, 우측은 카메라 회전 입력으로 분리합니다.
    /// </summary>
    public class MobileTouchManager : MonoBehaviour
    {
        public static MobileTouchManager Instance { get; private set; }

        [Header("Touch Outputs")]
        public Vector2 MoveInput { get; private set; }
        public Vector2 CameraDelta { get; private set; }

        public Vector2 LeftTouchStartPos { get; private set; }
        public float LeftTouchStartTime { get; private set; }
        public bool IsLeftTouching { get; private set; }
        public bool IsLeftJoystickActive { get; private set; }
        public bool IsInputBlocked => inputBlocked;

        [Header("Touch Input")]
        [SerializeField] private float joystickActivationDelay = 0.4f;
        public float JoystickActivationDelay => joystickActivationDelay;

        [Header("Touch VFX")]
        public GameObject touchVFXPrefab;
        public GameObject touchTrailPrefab;
        public RectTransform targetVFXContainer;
        [SerializeField] private float burstDespawnDelay = 1.5f;
        [SerializeField] private float trailReleaseDelay = 0.5f;
        [SerializeField] private float screenVFXCameraDistance = 2f;
        [SerializeField] private int touchVFXSortingOrder = 32000;

        private int leftFingerId = -1;
        private int rightFingerId = -1;

        private readonly Dictionary<int, GameObject> activeTouchVFX = new Dictionary<int, GameObject>();
        private readonly Dictionary<Renderer, int> originalRendererSortingOrders = new Dictionary<Renderer, int>();
        private GameObject activeMouseVFX;
        private Transform touchVFXWorldRoot;
        private Camera touchVFXCamera;
        private int touchVFXLayer;
        private bool inputBlocked;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }

        private void Update()
        {
            MoveInput = Vector2.zero;
            CameraDelta = Vector2.zero;
            if (inputBlocked)
            {
                ClearTouchState(releaseTrails: true);
                return;
            }

            // 오프닝 연출 중에는 이동/카메라 터치 입력을 차단합니다.
            var openingDirector = UnityEngine.Object.FindFirstObjectByType<ActionRPG.UI.DemoOpeningDirector>();
            if (openingDirector != null)
            {
                bool isPlayingOpening = false;
                if (openingDirector.playDemoIntroSequence && !openingDirector.isGameplayIntroFinished) isPlayingOpening = true;
                if (!openingDirector.playDemoIntroSequence && !openingDirector.isFinished) isPlayingOpening = true;

                if (isPlayingOpening)
                {
                    ClearTouchState(releaseTrails: true);
                    return;
                }
            }

            float halfScreenWidth = Screen.width / 2f;

            if (ETouch.activeTouches.Count == 0)
            {
                UpdateMouseEmulation(halfScreenWidth);
                return;
            }

            UpdateMobileTouches(halfScreenWidth);
        }

        public void SetInputBlocked(bool blocked)
        {
            inputBlocked = blocked;
            if (inputBlocked)
            {
                ClearTouchState(releaseTrails: true);
            }
        }

        private void ClearTouchState(bool releaseTrails)
        {
            MoveInput = Vector2.zero;
            CameraDelta = Vector2.zero;
            leftFingerId = -1;
            rightFingerId = -1;
            IsLeftTouching = false;
            IsLeftJoystickActive = false;

            if (releaseTrails)
            {
                ReleaseTouchTrail(activeMouseVFX);
                activeMouseVFX = null;

                foreach (GameObject trailObj in activeTouchVFX.Values)
                {
                    ReleaseTouchTrail(trailObj);
                }
                activeTouchVFX.Clear();
            }
        }

        private void UpdateMouseEmulation(float halfScreenWidth)
        {
            if (UnityEngine.InputSystem.Mouse.current == null) return;

            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool isPressed = mouse.leftButton.isPressed;
            bool wasPressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            bool wasReleasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
            Vector2 mousePos = mouse.position.ReadValue();

            if (wasPressedThisFrame)
            {
                SpawnTouchBurstVFX(mousePos);
                activeMouseVFX = SpawnTouchTrailVFX(mousePos);

                if (mousePos.x < halfScreenWidth)
                {
                    LeftTouchStartPos = mousePos;
                    LeftTouchStartTime = Time.time;
                    IsLeftTouching = true;
                    IsLeftJoystickActive = false;
                }
            }

            if (isPressed)
            {
                if (activeMouseVFX != null)
                {
                    UpdateTouchVFXPosition(activeMouseVFX, mousePos);
                }

                if (IsLeftTouching)
                {
                    Vector2 delta = mousePos - LeftTouchStartPos;
                    bool wasJustActivated = !IsLeftJoystickActive && (Time.time - LeftTouchStartTime >= joystickActivationDelay);
                    IsLeftJoystickActive = Time.time - LeftTouchStartTime >= joystickActivationDelay;

                    if (wasJustActivated && activeMouseVFX != null)
                    {
                        ReleaseTouchTrail(activeMouseVFX);
                        activeMouseVFX = null;
                    }

                    if (IsLeftJoystickActive)
                    {
                        MoveInput = Vector2.ClampMagnitude(delta / 150f, 1f);
                    }
                }
                else if (mousePos.x >= halfScreenWidth)
                {
                    CameraDelta = mouse.delta.ReadValue();
                }
            }

            if (wasReleasedThisFrame)
            {
                ReleaseTouchTrail(activeMouseVFX);
                activeMouseVFX = null;
                IsLeftTouching = false;
                IsLeftJoystickActive = false;
                MoveInput = Vector2.zero;
                CameraDelta = Vector2.zero;
            }
            else if (!isPressed && IsLeftTouching)
            {
                IsLeftTouching = false;
                IsLeftJoystickActive = false;
                MoveInput = Vector2.zero;
            }
        }

        private void UpdateMobileTouches(float halfScreenWidth)
        {
            foreach (var touch in ETouch.activeTouches)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    SpawnTouchBurstVFX(touch.screenPosition);
                    GameObject trail = SpawnTouchTrailVFX(touch.screenPosition);
                    if (trail != null) activeTouchVFX[touch.finger.index] = trail;

                    if (touch.screenPosition.x < halfScreenWidth && leftFingerId == -1)
                    {
                        leftFingerId = touch.finger.index;
                        LeftTouchStartPos = touch.screenPosition;
                        LeftTouchStartTime = Time.time;
                        IsLeftTouching = true;
                        IsLeftJoystickActive = false;
                    }
                    else if (touch.screenPosition.x >= halfScreenWidth && rightFingerId == -1)
                    {
                        rightFingerId = touch.finger.index;
                    }
                }

                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    if (activeTouchVFX.TryGetValue(touch.finger.index, out GameObject trailObj))
                    {
                        UpdateTouchVFXPosition(trailObj, touch.screenPosition);
                    }
                }
                else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                {
                    if (activeTouchVFX.TryGetValue(touch.finger.index, out GameObject trailObj))
                    {
                        ReleaseTouchTrail(trailObj);
                        activeTouchVFX.Remove(touch.finger.index);
                    }
                }

                if (touch.finger.index == leftFingerId)
                {
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved || touch.phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                    {
                        Vector2 delta = touch.screenPosition - LeftTouchStartPos;
                        bool wasJustActivated = !IsLeftJoystickActive && (Time.time - LeftTouchStartTime >= joystickActivationDelay);
                        IsLeftJoystickActive = Time.time - LeftTouchStartTime >= joystickActivationDelay;

                        if (wasJustActivated)
                        {
                            if (activeTouchVFX.TryGetValue(touch.finger.index, out GameObject trailObj))
                            {
                                ReleaseTouchTrail(trailObj);
                                activeTouchVFX.Remove(touch.finger.index);
                            }
                        }

                        if (IsLeftJoystickActive)
                        {
                            MoveInput = Vector2.ClampMagnitude(delta / 150f, 1f);
                        }
                    }
                    else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        leftFingerId = -1;
                        MoveInput = Vector2.zero;
                        IsLeftTouching = false;
                        IsLeftJoystickActive = false;
                    }
                }

                if (touch.finger.index == rightFingerId)
                {
                    if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                    {
                        CameraDelta = touch.delta;
                    }
                    else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
                    {
                        rightFingerId = -1;
                        CameraDelta = Vector2.zero;
                    }
                }
            }
        }

        private Transform ResolveTouchVFXParent()
        {
            if (targetVFXContainer != null) return targetVFXContainer;

            Canvas canvas = FindFirstObjectByType<Canvas>();
            return canvas != null ? canvas.transform : null;
        }

        private Camera ResolveTouchVFXCamera(Transform parentTransform)
        {
            Canvas canvas = parentTransform != null ? parentTransform.GetComponentInParent<Canvas>() : null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay && canvas.worldCamera != null)
            {
                touchVFXCamera = canvas.worldCamera;
            }
            else
            {
                touchVFXCamera = Camera.main;
            }

            return touchVFXCamera;
        }

        private void SpawnTouchBurstVFX(Vector2 screenPos)
        {
            if (touchVFXPrefab == null) return;

            Transform parentTransform = ResolveTouchVFXParent();
            Camera vfxCamera = ResolveTouchVFXCamera(parentTransform);
            if (parentTransform == null && vfxCamera == null) return;

            GameObject vfxObj = SpawnTouchVFXObject(touchVFXPrefab, parentTransform, vfxCamera, screenPos);
            if (vfxObj == null) return;

            UpdateTouchVFXPosition(vfxObj, screenPos, parentTransform as RectTransform, vfxCamera);
            PlayParticleSystems(vfxObj);
            StartCoroutine(DespawnAfterDelay(vfxObj, burstDespawnDelay));
        }

        private GameObject SpawnTouchTrailVFX(Vector2 screenPos)
        {
            if (touchTrailPrefab == null) return null;

            Transform parentTransform = ResolveTouchVFXParent();
            Camera vfxCamera = ResolveTouchVFXCamera(parentTransform);
            if (parentTransform == null && vfxCamera == null) return null;

            GameObject vfxObj = SpawnTouchVFXObject(touchTrailPrefab, parentTransform, vfxCamera, screenPos);
            if (vfxObj == null) return null;

            UpdateTouchVFXPosition(vfxObj, screenPos, parentTransform as RectTransform, vfxCamera);
            PrepareTrailRenderers(vfxObj, true);
            PlayParticleSystems(vfxObj);
            return vfxObj;
        }

        private GameObject SpawnTouchVFXObject(GameObject prefab, Transform parentTransform, Camera vfxCamera, Vector2 screenPos)
        {
            Transform spawnParent = parentTransform is RectTransform
                ? parentTransform
                : ResolveTouchVFXWorldRoot(vfxCamera, parentTransform);

            Vector3 spawnPosition = Vector3.zero;
            Quaternion spawnRotation = prefab.transform.rotation;
            ResolveTouchVFXPose(spawnParent, vfxCamera, screenPos, ref spawnPosition, ref spawnRotation);

            GameObject vfxObj = null;
            if (ObjectPoolManager.Instance != null)
            {
                vfxObj = ObjectPoolManager.Instance.Spawn(prefab, spawnPosition, spawnRotation, spawnParent);
            }

            if (vfxObj == null)
            {
                vfxObj = Instantiate(prefab, spawnPosition, spawnRotation, spawnParent);
            }

            if (vfxObj == null) return null;

            vfxObj.transform.SetAsLastSibling();
            SetLayerRecursively(vfxObj, spawnParent.gameObject.layer);
            ApplyCanvasRendererSorting(vfxObj, spawnParent);
            return vfxObj;
        }

        private void ResolveTouchVFXPose(Transform spawnParent, Camera vfxCamera, Vector2 screenPos, ref Vector3 position, ref Quaternion rotation)
        {
            Canvas canvas = spawnParent != null ? spawnParent.GetComponentInParent<Canvas>() : null;
            RectTransform planeRect = spawnParent as RectTransform;

            if (canvas != null && planeRect != null)
            {
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(planeRect, screenPos, uiCamera, out Vector3 worldPoint))
                {
                    position = worldPoint;
                    rotation = planeRect.rotation;
                    return;
                }
            }

            if (vfxCamera != null)
            {
                float distance = Mathf.Clamp(screenVFXCameraDistance, vfxCamera.nearClipPlane + 0.01f, vfxCamera.farClipPlane - 0.01f);
                position = vfxCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distance));
                rotation = vfxCamera.transform.rotation;
            }
        }

        private Transform ResolveTouchVFXWorldRoot(Camera vfxCamera, Transform fallbackParent)
        {
            if (vfxCamera == null) return fallbackParent;

            if (touchVFXWorldRoot == null)
            {
                GameObject root = new GameObject("TouchVFX_WorldRoot");
                touchVFXWorldRoot = root.transform;
                touchVFXWorldRoot.SetParent(vfxCamera.transform, false);
                touchVFXWorldRoot.localPosition = Vector3.zero;
                touchVFXWorldRoot.localRotation = Quaternion.identity;
                touchVFXWorldRoot.localScale = Vector3.one;
            }
            else if (touchVFXWorldRoot.parent != vfxCamera.transform)
            {
                touchVFXWorldRoot.SetParent(vfxCamera.transform, false);
            }

            touchVFXLayer = ResolveVisibleLayer(vfxCamera, fallbackParent);
            touchVFXWorldRoot.gameObject.layer = touchVFXLayer;
            return touchVFXWorldRoot;
        }

        private static int ResolveVisibleLayer(Camera vfxCamera, Transform fallbackParent)
        {
            if (fallbackParent != null)
            {
                int parentLayer = fallbackParent.gameObject.layer;
                if ((vfxCamera.cullingMask & (1 << parentLayer)) != 0)
                {
                    return parentLayer;
                }
            }

            int defaultLayer = LayerMask.NameToLayer("Default");
            if (defaultLayer >= 0 && (vfxCamera.cullingMask & (1 << defaultLayer)) != 0)
            {
                return defaultLayer;
            }

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0 && (vfxCamera.cullingMask & (1 << uiLayer)) != 0)
            {
                return uiLayer;
            }

            return fallbackParent != null ? fallbackParent.gameObject.layer : 0;
        }

        private void UpdateTouchVFXPosition(GameObject vfxObj, Vector2 screenPos, RectTransform parentRect = null, Camera vfxCamera = null)
        {
            if (vfxObj == null) return;
            vfxCamera ??= touchVFXCamera != null ? touchVFXCamera : ResolveTouchVFXCamera(parentRect);

            Canvas canvas = vfxObj.GetComponentInParent<Canvas>();
            RectTransform planeRect = parentRect != null ? parentRect : vfxObj.transform.parent as RectTransform;
            if (canvas != null && planeRect != null)
            {
                Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(planeRect, screenPos, uiCamera, out Vector3 worldPoint))
                {
                    vfxObj.transform.position = worldPoint;
                    vfxObj.transform.rotation = planeRect.rotation;
                }
                return;
            }

            if (vfxCamera != null)
            {
                float distance = Mathf.Clamp(screenVFXCameraDistance, vfxCamera.nearClipPlane + 0.01f, vfxCamera.farClipPlane - 0.01f);
                Vector3 worldPos = vfxCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distance));
                vfxObj.transform.position = worldPos;
                vfxObj.transform.rotation = vfxCamera.transform.rotation;
            }
        }

        private void ReleaseTouchTrail(GameObject trailObj)
        {
            if (trailObj == null) return;

            PrepareTrailRenderers(trailObj, false);
            StopParticleSystems(trailObj);
            StartCoroutine(DespawnAfterDelay(trailObj, trailReleaseDelay));
        }

        private static void PrepareTrailRenderers(GameObject obj, bool emitting)
        {
            foreach (TrailRenderer trail in obj.GetComponentsInChildren<TrailRenderer>(true))
            {
                trail.Clear();
                trail.emitting = emitting;
            }
        }

        private static void PlayParticleSystems(GameObject obj)
        {
            foreach (ParticleSystem particle in obj.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Clear(true);
                particle.Play(true);
            }
        }

        private static void StopParticleSystems(GameObject obj)
        {
            foreach (ParticleSystem particle in obj.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void ApplyCanvasRendererSorting(GameObject obj, Transform spawnParent)
        {
            Canvas canvas = spawnParent != null ? spawnParent.GetComponentInParent<Canvas>() : null;
            if (canvas == null) return;

            foreach (Renderer renderer in obj.GetComponentsInChildren<Renderer>(true))
            {
                if (!originalRendererSortingOrders.TryGetValue(renderer, out int originalOrder))
                {
                    originalOrder = renderer.sortingOrder;
                    originalRendererSortingOrders[renderer] = originalOrder;
                }

                renderer.sortingLayerID = canvas.sortingLayerID;
                renderer.sortingOrder = Mathf.Clamp(touchVFXSortingOrder + originalOrder, short.MinValue, short.MaxValue);
            }
        }

        private System.Collections.IEnumerator DespawnAfterDelay(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (obj == null) yield break;

            if (ObjectPoolManager.Instance != null && obj.activeInHierarchy)
            {
                ObjectPoolManager.Instance.Despawn(obj);
            }
            else if (ObjectPoolManager.Instance == null)
            {
                Destroy(obj);
            }
        }
    }
}
