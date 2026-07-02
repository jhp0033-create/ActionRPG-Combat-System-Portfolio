using UnityEngine;

namespace ActionRPG.Test
{
    /// <summary>
    /// 아주 단순하게 마우스를 계속 따라다니며 궤적을 남기는 예제 스크립트입니다.
    /// 빈 게임 오브젝트에 넣기만 하면 즉시 작동합니다.
    /// </summary>
    public class SimpleMouseTrail : MonoBehaviour
    {
        private TrailRenderer tr;
        [Tooltip("트레일이 그려질 기준 카메라 (비워두면 Main Camera를 자동 사용합니다)")]
        [SerializeField] private Camera targetCam;

        void Start()
        {
            // 1. 인스펙터에 할당된 카메라가 없으면 Main Camera 사용
            if (targetCam == null) targetCam = Camera.main;
            
            // 2. TrailRenderer 강제 생성 및 세팅
            tr = GetComponent<TrailRenderer>();
            if (tr == null)
            {
                tr = gameObject.AddComponent<TrailRenderer>();
                tr.time = 0.5f;             
                tr.startWidth = 0.5f;       
                tr.endWidth = 0.0f;         
                tr.minVertexDistance = 0.01f; // 더 촘촘하게 점을 찍도록 간격 축소 (반응성 향상)
                
                tr.material = new Material(Shader.Find("Sprites/Default")); 
                tr.startColor = Color.cyan; 
                tr.endColor = new Color(0, 1, 1, 0); 
                
                tr.sortingOrder = 32000;
            }

            // 3. UI 카메라가 그려주도록 레이어를 UI(5)로 강제 변경
            gameObject.layer = 5; 
        }

        // 카메라 등의 렌더링 세팅이 다 끝난 가장 마지막 시점에 호출 (UI 지연 현상 완화)
        void LateUpdate()
        {
            if (targetCam == null) return;

            // 마우스 스크린 좌표 가져오기
            Vector3 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            
            // UICamera(직교) 기준 카메라 앞 10단위
            mousePos.z = 10f; 
            
            // 좌표 변환 및 이동
            transform.position = targetCam.ScreenToWorldPoint(mousePos);
        }
    }
}
