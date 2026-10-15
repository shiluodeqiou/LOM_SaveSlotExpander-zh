using UnityEngine;

namespace LOM.SaveSlotExpander
{
    /// <summary>
    /// 常驻场景，只负责把元数据缓存的落盘动作错开。
    ///
    /// 缓存每处理一个存档位就会更新一次，每次都写文件的话，
    /// 300 个存档位就要写 300 次。这里合并成延迟写入。
    /// </summary>
    internal class ModRuntime : MonoBehaviour
    {
        private const float FlushIntervalSeconds = 3f;

        private float _nextFlush;

        private void Update()
        {
            if (!SaveMetaCache.IsDirty) return;
            if (Time.unscaledTime < _nextFlush) return;

            _nextFlush = Time.unscaledTime + FlushIntervalSeconds;
            SaveMetaCache.Flush();
        }

        private void OnApplicationQuit() => SaveMetaCache.Flush();

        private void OnDestroy() => SaveMetaCache.Flush();
    }
}
