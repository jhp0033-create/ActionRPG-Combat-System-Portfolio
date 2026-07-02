using UnityEngine;

namespace ActionRPG.Data
{
    /// <summary>
    /// 사운드 클립과 재생 설정을 ScriptableObject 에셋으로 분리해 관리합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSoundData", menuName = "ActionRPG/Data/SoundData")]
    public class SoundData : ScriptableObject
    {
        [Tooltip("이 사운드를 호출할 때 사용할 고유 키값 (예: 'Footstep_Dirt')")]
        public string soundID;

        [Tooltip("재생할 오디오 클립들. 배열에 여러 개를 넣으면 매번 랜덤으로 하나가 재생됩니다.")]
        public AudioClip[] clips;

        [Tooltip("기본 재생 볼륨 보정치 (0 ~ 1)")]
        [Range(0f, 1f)] public float baseVolume = 1f;

        [Tooltip("기계음 느낌을 방지하기 위해 재생 시마다 피치를 얼마나 비틀지 조절합니다 (예: 0.1)")]
        [Range(0f, 0.3f)] public float pitchRandomness = 0.1f;

        [Tooltip("체크 시 여러 개가 동시에 재생되어도 사운드가 겹치면서 증폭(폭발음)되는 것을 허용합니다.")]
        public bool allowOverlap = false;
    }
}
