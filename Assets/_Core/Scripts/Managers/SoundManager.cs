using UnityEngine;
using System.Collections.Generic;
using ActionRPG.Data;
using DG.Tweening;

namespace ActionRPG.Managers
{
    /// <summary>
    /// 게임 전체의 배경음악(BGM)과 효과음(SFX)을 통합 관리하는 싱글턴 매니저입니다.
    /// 씬이 전환되어도 파괴되지 않으며(DontDestroyOnLoad), SFX의 다중 재생(PlayOneShot)을 지원합니다.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Audio Sources")]
        [Tooltip("배경음악을 재생할 전용 오디오 소스")]
        [SerializeField] private AudioSource bgmSource;
        [Tooltip("효과음을 재생할 전용 오디오 소스 (여러 개 겹치기용)")]
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("시네마틱/환경음을 재생할 전용 오디오 소스 (독립 페이드용)")]
        [SerializeField] private AudioSource cinematicSource;

        [Header("Global Volume Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.5f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        private Dictionary<string, SoundData> soundDatabase = new Dictionary<string, SoundData>();
        
        private Dictionary<string, int> lastPlayedIndices = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                
                if (bgmSource == null) 
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                    bgmSource.loop = true;
                    bgmSource.playOnAwake = false;
                }
                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                    sfxSource.loop = false;
                    sfxSource.playOnAwake = false;
                }
                
                ApplyVolume();
                LoadSoundDatabase();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 볼륨 변경 사항을 AudioSource에 즉시 적용합니다.
        /// </summary>
        public void ApplyVolume()
        {
            EnsureSfxSource();
            if (bgmSource != null) bgmSource.volume = bgmVolume * masterVolume;
            if (sfxSource != null) sfxSource.volume = sfxVolume * masterVolume;
        }

        private void EnsureSfxSource()
        {
            if (sfxSource != null) return;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        /// <summary>
        /// 배경음악(BGM)을 재생합니다. 이미 같은 곡이 재생 중이면 무시합니다.
        /// </summary>
        public void PlayBGM(AudioClip clip)
        {
            if (clip == null) return;
            
            if (bgmSource.isPlaying && bgmSource.clip == clip) return;

            bgmSource.clip = clip;
            bgmSource.Play();
        }

        /// <summary>
        /// 현재 재생 중인 배경음악(BGM)을 멈춥니다.
        /// </summary>
        public void StopBGM()
        {
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.Stop();
            }
        }

        /// <summary>
        /// BGM 볼륨을 부드럽게 줄이거나 키웁니다.
        /// targetVolumeRatio: 0.0 ~ 1.0 (예: 0.2면 기본 BGM 볼륨의 20%로 줄어듦)
        /// </summary>
        public void FadeBGM(float targetVolumeRatio, float duration)
        {
            if (bgmSource == null) return;
            
            float targetVolume = bgmVolume * masterVolume * Mathf.Clamp01(targetVolumeRatio);
            bgmSource.DOKill();
            bgmSource.DOFade(targetVolume, duration).SetEase(Ease.InOutQuad);
        }

        private void EnsureCinematicSource()
        {
            if (cinematicSource != null) return;
            cinematicSource = gameObject.AddComponent<AudioSource>();
            cinematicSource.loop = false;
            cinematicSource.playOnAwake = false;
        }

        /// <summary>
        /// 시네마틱 효과음을 페이드 인하며 재생합니다.
        /// playbackSpeed (pitch)를 조절하면 사운드 재생 속도가 빨라지거나 느려집니다.
        /// </summary>
        public void PlayCinematicSFX(string soundID, float fadeInDuration = 0.5f, float playbackSpeed = 1f)
        {
            if (string.IsNullOrEmpty(soundID)) return;
            EnsureCinematicSource();

            if (soundDatabase.Count == 0) LoadSoundDatabase();

            if (soundDatabase.TryGetValue(soundID, out SoundData data))
            {
                if (data.clips == null || data.clips.Length == 0) return;
                AudioClip selectedClip = data.clips[Random.Range(0, data.clips.Length)];
                
                cinematicSource.clip = selectedClip;
                cinematicSource.volume = 0f;
                cinematicSource.pitch = playbackSpeed;
                cinematicSource.Play();
                
                float targetVolume = sfxVolume * masterVolume * data.baseVolume;
                cinematicSource.DOKill();
                cinematicSource.DOFade(targetVolume, fadeInDuration).SetEase(Ease.OutQuad);
            }
        }

        /// <summary>
        /// 시네마틱 효과음을 페이드 아웃하며 종료합니다.
        /// </summary>
        public void FadeOutCinematicSFX(float fadeOutDuration = 0.5f)
        {
            if (cinematicSource == null || !cinematicSource.isPlaying) return;
            
            cinematicSource.DOKill();
            cinematicSource.DOFade(0f, fadeOutDuration).SetEase(Ease.InQuad).OnComplete(() => cinematicSource.Stop());
        }

        /// <summary>
        /// 효과음(SFX)을 재생합니다.
        /// </summary>
        public void PlaySFX(AudioClip clip, float pitchRandomness = 0f)
        {
            EnsureSfxSource();
            if (clip == null || sfxSource == null) return;

            if (pitchRandomness > 0f)
            {
                sfxSource.pitch = 1f + Random.Range(-pitchRandomness, pitchRandomness);
            }
            else
            {
                sfxSource.pitch = 1f;
            }

            sfxSource.PlayOneShot(clip, 1f);
        }

        /// <summary>
        /// 여러 클립 중 하나를 선택하고 피치 변화를 적용해 재생합니다.
        /// </summary>
        public void PlayRandomSFX(AudioClip[] clips, float pitchRandomness = 0.1f)
        {
            EnsureSfxSource();
            if (clips == null || clips.Length == 0 || sfxSource == null) return;
            
            AudioClip randomClip = clips[Random.Range(0, clips.Length)];
            
            if (pitchRandomness > 0f)
            {
                sfxSource.pitch = 1f + Random.Range(-pitchRandomness, pitchRandomness);
            }
            else
            {
                sfxSource.pitch = 1f;
            }

            sfxSource.PlayOneShot(randomClip, 1f);
        }

        /// <summary>
        /// Resources/SoundData 폴더의 SoundData 에셋을 캐싱합니다.
        /// </summary>
        private void LoadSoundDatabase()
        {
            soundDatabase.Clear();
            lastPlayedIndices.Clear();
            
            SoundData[] loadedData = Resources.LoadAll<SoundData>("SoundData");
            
            foreach (var data in loadedData)
            {
                if (string.IsNullOrEmpty(data.soundID)) continue;
                
                if (!soundDatabase.ContainsKey(data.soundID))
                {
                    soundDatabase.Add(data.soundID, data);
                    lastPlayedIndices.Add(data.soundID, -1);
                }
            }
        }

        /// <summary>
        /// soundID로 사운드 데이터를 찾아 재생합니다.
        /// </summary>
        
        private Dictionary<string, float> lastPlayedTimes = new Dictionary<string, float>();

        public void PlaySFXByKey(string soundID)
        {
            if (string.IsNullOrEmpty(soundID)) return;

            EnsureSfxSource();

            if (soundDatabase.Count == 0)
            {
                LoadSoundDatabase();
            }

            if (soundDatabase.TryGetValue(soundID, out SoundData data))
            {
                if (!data.allowOverlap)
                {
                    if (lastPlayedTimes.TryGetValue(soundID, out float lastTime))
                    {
                        if (Time.time - lastTime < 0.05f) return;
                    }
                    lastPlayedTimes[soundID] = Time.time;
                }

                if (data.clips == null || data.clips.Length == 0)
                {
                    Debug.LogError($"[SoundManager] '{soundID}' 사운드 데이터에 할당된 오디오 클립(AudioClip)이 하나도 없습니다!");
                    return;
                }
                if (sfxSource == null)
                {
                    Debug.LogError("[SoundManager] sfxSource 컴포넌트가 존재하지 않습니다!");
                    return;
                }

                int selectedIndex = 0;
                int clipCount = data.clips.Length;

                if (clipCount > 1)
                {
                    // 직전에 재생된 인덱스 가져오기
                    int lastIndex = lastPlayedIndices.ContainsKey(soundID) ? lastPlayedIndices[soundID] : -1;
                    
                    // 직전 인덱스와 겹치지 않을 때까지 반복 추출 (Non-Repeating Random)
                    do
                    {
                        selectedIndex = Random.Range(0, clipCount);
                    } while (selectedIndex == lastIndex);

                    // 새로운 인덱스 기록
                    lastPlayedIndices[soundID] = selectedIndex;
                }

                AudioClip selectedClip = data.clips[selectedIndex];

                // 피치 조절
                if (data.pitchRandomness > 0f)
                {
                    sfxSource.pitch = 1f + Random.Range(-data.pitchRandomness, data.pitchRandomness);
                }
                else
                {
                    sfxSource.pitch = 1f;
                }

                // 볼륨 보정치 적용 (AudioSource.volume에 이미 sfxVolume과 masterVolume이 적용되어 있으므로 에셋 고유 볼륨만 전달)
                float finalVolume = data.baseVolume;
                sfxSource.PlayOneShot(selectedClip, finalVolume);
            }
            else
            {
                Debug.LogError($"[SoundManager] '{soundID}' 키에 해당하는 사운드 데이터(SoundData)를 찾을 수 없습니다! Resources/SoundData 폴더에 파일이 있는지, 또는 SoundID가 정확한지 확인해주세요.");
            }
         
        }
        public void PlaySwingSFX()
        {
            PlaySFXByKey("Swing");
        }

        public void PlayMonsterSwingSFX()
        {
            if (soundDatabase.Count == 0) LoadSoundDatabase();
            if (soundDatabase.ContainsKey("MonsterSwing"))
            {
                PlaySFXByKey("MonsterSwing");
            }
            else
            {
              
                PlaySFXByKey("Swing"); 
            }
        }



        public void PlaySpawnSFX()
        {
            PlaySFXByKey("Spawn");
        }

        public void PlayDialogueAdvanceSFX()
        {
            PlaySFXByKey("DialogueAdvance");
        }
    }
}
