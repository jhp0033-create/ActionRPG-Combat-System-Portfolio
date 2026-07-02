using UnityEngine;

namespace ActionRPG.Managers
{
    /// <summary>
    /// ObjectPoolManager에서 런타임에 동적으로 생성된 오브젝트에 부착되는 꼬리표입니다.
    /// 이 오브젝트가 어느 프리팹(풀) 출신인지 InstanceID를 기억해 두었다가,
    /// Despawn 시 빠르게 원래 큐(Queue)로 돌아갈 수 있도록 돕습니다.
    /// 개발자가 직접 인스펙터에서 붙일 필요 없이 매니저가 알아서 붙여줍니다.
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        [HideInInspector]
        public int prefabId;
    }
}
