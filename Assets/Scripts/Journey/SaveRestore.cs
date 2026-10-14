using UnityEngine;
using System;

namespace Hiking.Journey
{
    // 把 SaveData 写回运行中的地图和会话。
    //
    // 完整的读档流程是：先用随机种子建一张地图（GameFlowController 负责）
    // → 用存档整批覆盖数据（这里）→ 刷新表现 → 离线推进 → 按退出状态表恢复交互。
    // 这一步只做「覆盖数据 + 刷新表现」，不碰流程编排。
    public static class SaveRestore
    {
        public static bool Apply(GameFlowController flow, SaveData data)
        {
            if (flow == null || flow.Map == null || data == null) return false;

            var stations = flow.Map.stations;
            if (stations == null || data.stations == null || data.stations.Length != stations.Length)
            {
                Debug.LogWarning("存档的站数与当前地图不一致，已放弃恢复。");
                return false;
            }

            // 先清植物标记再换地块。顺序反了，旧标记就再也找不到，屏幕上会留下幽灵植物。
            for (int i = 0; i < stations.Length; i++) stations[i].ClearPlantMarkers(flow.Map);

            for (int i = 0; i < stations.Length; i++)
            {
                var saved = data.stations[i];
                if (saved == null || saved.tiles == null || saved.tiles.Length != stations[i].tileCount)
                {
                    Debug.LogWarning("存档第 " + (i + 1) + " 站的地块数对不上，已放弃恢复。");
                    return false;
                }

                var tiles = new TileState[saved.tiles.Length];
                for (int t = 0; t < tiles.Length; t++) tiles[t] = TileState.Restore(saved.tiles[t]);
                stations[i].RestoreTiles(tiles);
            }

            flow.Map.RestoreSimulationStep(data.map.simulationStep);
            flow.Map.Weather.Restore(data.weather);
            flow.Session.Restore(data.session);

            // 表现层跟着数据走：水分标签、水面颜色与植物标记
            float elapsedSeconds = (float)Math.Max(0, (DateTime.UtcNow - SaveSystem.SavedAtUtc(data)).TotalSeconds);
            for (int i = 0; i < stations.Length; i++)
            {
                for (int t = 0; t < stations[i].tileCount; t++) stations[i].RefreshTile(t, false);
                stations[i].RestorePondVisuals(flow.Map, i, data.stations[i], elapsedSeconds);
                stations[i].SyncPlantMarkers(flow.Map);
                stations[i].RefreshThemeConversion();
            }
            return true;
        }
    }
}
