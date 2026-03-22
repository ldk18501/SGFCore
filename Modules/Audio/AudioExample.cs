using UnityEngine;

namespace GameFramework.Core
{
    public class AudioExample : MonoBehaviour
    {
        public void OnClickUpgrade()
        {
            // isSingleton = true: 如果上一次的声音还没播完，直接打断重播，绝对不会叠加！
            // pitchRange = 0.1f: 每次声音的音调会在 0.9 ~ 1.1 之间随机微调，极大减轻玩家的听觉疲劳！
            GameApp.Audio.PlaySFX("UI_Upgrade", isSingleton: true, pitchRange: 0.1f);
        }
        
        public void TakeDamage()
        {
            // 传入坐标，开启 3D 模式，设置衰减距离为 5~30 米
            GameApp.Audio.PlaySFX(
                "Hit_Flesh", 
                is3D: true, 
                position: this.transform.position, 
                minDistance: 5f, 
                maxDistance: 30f
            );
        }
        
        private long _sawAudioId;

        public void StartSawing()
        {
            // loop = true 开启循环
            // 因为我们采用了同步返回 ID，即便此时 AudioClip 还在 Addressables 异步加载路上，你也能安全拿到 ID。
            _sawAudioId = GameApp.Audio.PlaySFX("Sawing_Loop", loop: true);
        }

        public void StopSawing()
        {
            // 玩家突然跑开或者树被砍倒，直接调用 Stop
            // 如果资源还没加载完，模块内部会标记 IsAborted，加载完后直接丢弃，不发生任何泄露！
            GameApp.Audio.StopAudio(_sawAudioId);
        }
    }
}