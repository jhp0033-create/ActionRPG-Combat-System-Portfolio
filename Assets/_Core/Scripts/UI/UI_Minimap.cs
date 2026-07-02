using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    /// <summary>
    /// 정지된 맵(스크린샷) 텍스처 위에서 플레이어의 3D 좌표를 2D UI 좌표로 변환하여 
    /// 플레이어 마커(아이콘)를 움직여주는 미니맵 컨트롤러입니다.
    /// </summary>
    public class UI_Minimap : MonoBehaviour
    {
        [Header("Target References")]
        [Tooltip("추적할 플레이어의 Transform을 연결하세요.")]
        public Transform playerTransform;
        
        [Header("UI Elements")]
        [Tooltip("미니맵 배경 이미지 자체의 RectTransform을 연결하세요.")]
        public RectTransform mapBackgroundRect;
        
        [Tooltip("미니맵 위에 표시될 플레이어 '위치' 마커(점, 초상화 등)를 연결하세요.")]
        public RectTransform playerIconRect;

        [Tooltip("(선택 사항) 플레이어가 바라보는 '방향'을 나타내는 화살표 UI가 따로 있다면 연결하세요.\n비워두면 위의 위치 마커 자체가 회전합니다.")]
        public RectTransform playerDirectionIconRect;

        [Header("Enemy Elements")]
        [Tooltip("적 위치에 표시될 빨간색 점(아이콘) 프리팹 (RectTransform 포함)")]
        public GameObject enemyIconPrefab;
        [Tooltip("적 아이콘들이 생성될 부모 컨테이너 (보통 Player Icon과 같은 뎁스)")]
        public RectTransform enemyContainer;

        [Header("World Mapping Data")]
        [Tooltip("에디터 탑뷰 캡처 시 카메라의 'Orthographic Size' 값을 입력하세요.")]
        public float cameraOrthographicSize = 100f;
        
        [Tooltip("캡처 당시 카메라가 위치했던 정중앙의 실제 3D 월드 좌표 (X, Z)")]
        public Vector2 worldCenterPoint = Vector2.zero;

        [Header("Calibration")]
        [Tooltip("맵 이미지가 미세하게 어긋날 경우, 여기서 픽셀 단위로 상하좌우 영점을 맞출 수 있습니다.")]
        public Vector2 mapPixelOffset = Vector2.zero;

        [Header("Direction Icon Calibration")]
        [Tooltip("화살표 이미지가 기본적으로 위쪽(↑)이 아닌 옆으로 누워 그려져 있다면 각도를 입력해 영점을 맞추세요. (예: 90, 180)")]
        public float directionRotationOffset = 0f;

        [Tooltip("시야 화살표를 캐릭터 중심에서 바라보는 방향(앞쪽)으로 살짝 띄워 밀어내고 싶다면 값을 올리세요.")]
        public float directionForwardOffset = 0f;

        [Header("Camera Reference")]
        [Tooltip("화면 보는 방향(우클릭 회전 등)에 맞춰 미니맵을 돌리려면 메인 카메라를 연결하세요. (비워두면 자동 탐색)")]
        public Transform cameraTransform;

        [Header("Minimap Rotation Settings")]
        [Tooltip("체크하면 카메라를 돌릴 때 미니맵 전체 배경이 함께 돕니다.\n체크 해제하면 미니맵은 고정(북쪽 고정)되고 내부 아이콘만 돕니다.")]
        public bool rotateMapWithCamera = false;

        // 적 추적용 UI 딕셔너리
        private System.Collections.Generic.Dictionary<GameObject, RectTransform> enemyIcons = new System.Collections.Generic.Dictionary<GameObject, RectTransform>();

        private void LateUpdate()
        {
            if (playerTransform == null || mapBackgroundRect == null || playerIconRect == null) return;
            
            // 카메라 자동 캐싱
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            // 1. [비율 자동 계산] 
            float worldSizeZ = cameraOrthographicSize * 2f;
            float imageAspectRatio = mapBackgroundRect.rect.width / mapBackgroundRect.rect.height;
            float worldSizeX = worldSizeZ * imageAspectRatio;

            // 2. 월드 내에서 플레이어의 현재 상대적 위치 비율 계산 (-0.5 ~ +0.5 사이의 값)
            float relX = (playerTransform.position.x - worldCenterPoint.x) / worldSizeX;
            float relZ = (playerTransform.position.z - worldCenterPoint.y) / worldSizeZ; // y 필드는 실제 월드의 Z축

            float pivotOffsetX = mapPixelOffset.x / (mapBackgroundRect.rect.width * mapBackgroundRect.localScale.x);
            float pivotOffsetY = mapPixelOffset.y / (mapBackgroundRect.rect.height * mapBackgroundRect.localScale.y);

            mapBackgroundRect.pivot = new Vector2((relX + 0.5f) - pivotOffsetX, (relZ + 0.5f) - pivotOffsetY);
            mapBackgroundRect.anchoredPosition = playerIconRect.anchoredPosition;

            // 4. [회전 로직] PlayerIcon은 캐릭터 방향, DirectionIcon은 카메라 시야 방향을 담당합니다.
            float camRotY = cameraTransform != null ? cameraTransform.eulerAngles.y : 0f;
            float playerRotY = playerTransform.eulerAngles.y;

            if (rotateMapWithCamera)
            {
                // [회전 맵 모드] 맵 자체가 카메라에 맞춰 돌아갑니다.
                mapBackgroundRect.localEulerAngles = new Vector3(0f, 0f, camRotY);
                
                // 캐릭터 아이콘(PlayerIcon)은 맵 회전량(camRotY)을 빼줘야 실제 캐릭터 방향을 가리킵니다.
                playerIconRect.localEulerAngles = new Vector3(0f, 0f, -(playerRotY - camRotY) + directionRotationOffset);

                // 카메라 시야 방향(DirectionIcon)은 맵이 돌면 항상 화면 위(Up)를 바라보게 됩니다. (camRotY - camRotY = 0)
                if (playerDirectionIconRect != null)
                {
                    playerDirectionIconRect.localEulerAngles = new Vector3(0f, 0f, directionRotationOffset);
                    ApplyForwardOffset(playerDirectionIconRect, directionRotationOffset);
                }
            }
            else
            {
                // [고정 맵 모드] 맵은 북쪽(0도)으로 완전히 고정됩니다.
                mapBackgroundRect.localEulerAngles = Vector3.zero;

                // 캐릭터 아이콘(PlayerIcon)은 캐릭터 각도(-playerRotY) 그대로 돕니다.
                playerIconRect.localEulerAngles = new Vector3(0f, 0f, -playerRotY + directionRotationOffset);

                // 시야 방향 부채꼴(DirectionIcon)은 오직 카메라 방향(-camRotY)으로만 돕니다.
                if (playerDirectionIconRect != null)
                {
                    float finalRotZ = -camRotY + directionRotationOffset;
                    playerDirectionIconRect.localEulerAngles = new Vector3(0f, 0f, finalRotZ);
                    ApplyForwardOffset(playerDirectionIconRect, finalRotZ);
                }
            }

            UpdateEnemyIcons(worldSizeX, worldSizeZ, camRotY);
        }

        private void UpdateEnemyIcons(float worldSizeX, float worldSizeZ, float camRotY)
        {
            if (enemyIconPrefab == null || enemyContainer == null) return;
            if (ActionRPG.Core.EnemyManager.Instance == null) return;

            var activeEnemies = ActionRPG.Core.EnemyManager.Instance.ActiveEnemies;

            // 1. 비활성화, 파괴, 또는 '사망한' 적의 아이콘 삭제 (딕셔너리 정리)
            System.Collections.Generic.List<GameObject> toRemove = new System.Collections.Generic.List<GameObject>();
            foreach (var kvp in enemyIcons)
            {
                var enemyCtrl = kvp.Key != null ? kvp.Key.GetComponent<ActionRPG.Enemy.EnemyController>() : null;
                bool isDead = enemyCtrl != null && enemyCtrl.IsDead;

                if (kvp.Key == null || !kvp.Key.activeInHierarchy || !activeEnemies.Contains(kvp.Key) || isDead)
                {
                    if (kvp.Value != null) Destroy(kvp.Value.gameObject);
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (var key in toRemove) enemyIcons.Remove(key);

            // 2. 현재 살아있는 적들의 위치 업데이트 및 새 아이콘 생성
            foreach (var enemy in activeEnemies)
            {
                if (enemy == null || !enemy.activeInHierarchy) continue;

                if (!enemyIcons.TryGetValue(enemy, out RectTransform iconRect))
                {
                    GameObject iconObj = Instantiate(enemyIconPrefab, enemyContainer);
                    iconRect = iconObj.GetComponent<RectTransform>();
                    enemyIcons.Add(enemy, iconRect);
                }

                // 플레이어 대비 적의 실제 월드 거리 오프셋
                Vector3 offset = enemy.transform.position - playerTransform.position;

                // 월드 비율을 UI 픽셀 비율로 변환
                float uiOffsetX = (offset.x / worldSizeX) * (mapBackgroundRect.rect.width * mapBackgroundRect.localScale.x);
                float uiOffsetY = (offset.z / worldSizeZ) * (mapBackgroundRect.rect.height * mapBackgroundRect.localScale.y);
                Vector2 enemyUIPos = new Vector2(uiOffsetX, uiOffsetY);

                // 맵이 회전 모드일 경우, 적들의 상대 위치도 카메라 반대 방향으로 회전시켜야 정확한 위치에 표시됨
                if (rotateMapWithCamera)
                {
                    float angleRad = -camRotY * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(angleRad);
                    float sin = Mathf.Sin(angleRad);
                    enemyUIPos = new Vector2(
                        enemyUIPos.x * cos - enemyUIPos.y * sin,
                        enemyUIPos.x * sin + enemyUIPos.y * cos
                    );
                }

                // 플레이어 아이콘을 원점(기준점)으로 삼아 UI 캔버스 상에 오프셋 적용
                iconRect.anchoredPosition = playerIconRect.anchoredPosition + enemyUIPos;
            }
        }

        private void ApplyForwardOffset(RectTransform targetRect, float finalRotZ)
        {
            Vector2 basePos = playerIconRect.anchoredPosition;
            if (directionForwardOffset != 0f)
            {
                float rad = finalRotZ * Mathf.Deg2Rad;
                Vector2 forwardDir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
                basePos += forwardDir * directionForwardOffset;
            }

            if (targetRect.parent == playerIconRect.parent)
                targetRect.anchoredPosition = basePos;
            else
                targetRect.position = playerIconRect.position + (Vector3)(targetRect.up * directionForwardOffset);
        }
    }
}
