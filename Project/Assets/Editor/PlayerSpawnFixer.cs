using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 스테이지 씬에서 플레이어가 벽/장애물에 박혀(또는 아주 좁은 칸에 갇혀) 못 움직이는 스폰을 찾아
/// 바닥중앙에서 가장 가까운 "충분히 넓은 열린 칸"으로 Player와 PlayerSpawnMarker를 재배치한다.
///
/// 임포터 재생성(MapSlideImporter) 대신 기존 씬만 in-place로 고쳐 슬라임 벽 편집 등을 보존한다.
/// blocked 판정: 벽 타일맵 3종의 HasTile 또는 장애물/벽 콜라이더(layer 8) OverlapBox.
/// </summary>
public static class PlayerSpawnFixer
{
    const string StagesDir = "Assets/Scenes/Stages";
    const int MinRegion = 6; // 스폰이 속해야 하는 최소 열린영역 크기(이보다 작으면 "갇힘")

    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    [MenuItem("Tools/Ricochet/갇힌 플레이어 스폰 재배치")]
    public static void FixAll()
    {
        var guids = AssetDatabase.FindAssets("t:Scene", new[] { StagesDir });
        int checkedCount = 0, changed = 0;
        var report = new StringBuilder();

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".unity")) continue;
            if (!System.IO.Path.GetFileName(path).StartsWith("Stage")) continue;

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            checkedCount++;
            try
            {
                string result = FixScene(scene);
                if (result != null)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    changed++;
                    report.AppendLine($"{scene.name}: {result}");
                }
            }
            catch (Exception e) { report.AppendLine($"{scene.name}: 예외 {e.Message}"); }
        }

        Debug.LogWarning($"[PlayerSpawnFixer] 검사 {checkedCount}개, 재배치 {changed}개\n{report}");
    }

    /// <summary>스폰이 갇혔으면 재배치하고 설명 문자열을, 아니면 null을 반환.</summary>
    static string FixScene(Scene scene)
    {
        var grid = UnityEngine.Object.FindObjectOfType<GridModule>();
        if (grid == null) return null;
        int W = grid.Columns, H = grid.Rows;

        var walls = new List<Tilemap>();
        foreach (var root in scene.GetRootGameObjects())
            foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
                if (tm.name == "Tilemap_Wall_Normal" || tm.name == "Tilemap_Wall_Steel" || tm.name == "Tilemap_Wall_Slime")
                    walls.Add(tm);

        var player = GameObject.Find("Player");
        var marker = UnityEngine.Object.FindObjectOfType<PlayerSpawnMarker>();
        if (player == null && marker == null) return null;

        int mask = 1 << MapTileLayers.ObstacleLayer; // layer 8 = 벽·장애물 공용
        Physics2D.SyncTransforms();

        Func<int, int, bool> blocked = (x, y) =>
        {
            if (x < 0 || y < 0 || x >= W || y >= H) return true; // 그리드 밖은 막힘 취급
            var cell = new Vector3Int(x, y, 0);
            foreach (var tm in walls) if (tm.HasTile(cell)) return true;
            Vector2 c = grid.CellToWorld(new Vector2Int(x, y));
            return Physics2D.OverlapBox(c, Vector2.one * (grid.CellSize * 0.9f), 0f, mask) != null;
        };

        Vector2 curPos = marker != null ? (Vector2)marker.transform.position : (Vector2)player.transform.position;
        Vector2Int spawn = grid.WorldToCell(curPos);
        spawn.x = Mathf.Clamp(spawn.x, 0, W - 1);
        spawn.y = Mathf.Clamp(spawn.y, 0, H - 1);

        bool self = blocked(spawn.x, spawn.y);
        // 모바일은 좌/우 이동만 가능 → 좌우가 둘 다 막히면 위/아래만 뚫려 있어도 사실상 못 움직인다.
        bool horizBoxed = !self && blocked(spawn.x - 1, spawn.y) && blocked(spawn.x + 1, spawn.y);
        int region = self ? 0 : RegionSize(spawn, W, H, blocked, MinRegion + 1);
        bool trapped = self || horizBoxed || region < MinRegion;
        if (!trapped) return null;

        Vector2Int? target = FindOpenCell(new Vector2Int(W / 2, 1), W, H, blocked);
        if (target == null) return "재배치 실패(넓은 열린칸 없음)";

        Vector2 world = grid.CellToWorld(target.Value);
        if (player != null) player.transform.position = new Vector3(world.x, world.y, player.transform.position.z);
        if (marker != null) marker.transform.position = new Vector3(world.x, world.y, marker.transform.position.z);
        return $"스폰 {spawn}(self={self},horizBoxed={horizBoxed},region={region}) -> {target.Value}";
    }

    /// <summary>free 셀 4방향 BFS로 도달 가능한 열린영역 크기(cap에서 조기 종료).</summary>
    static int RegionSize(Vector2Int start, int W, int H, Func<int, int, bool> blocked, int cap)
    {
        var seen = new HashSet<Vector2Int> { start };
        var q = new Queue<Vector2Int>();
        q.Enqueue(start);
        int n = 0;
        while (q.Count > 0 && n < cap)
        {
            var p = q.Dequeue(); n++;
            foreach (var d in Dirs)
            {
                var np = p + d;
                if (seen.Contains(np) || blocked(np.x, np.y)) continue;
                seen.Add(np); q.Enqueue(np);
            }
        }
        return n;
    }

    /// <summary>from에서 바깥으로 BFS 확장하며, 막히지 않고 충분히 넓은 영역에 속한 가장 가까운 셀을 반환.</summary>
    static Vector2Int? FindOpenCell(Vector2Int from, int W, int H, Func<int, int, bool> blocked)
    {
        from.x = Mathf.Clamp(from.x, 0, W - 1);
        from.y = Mathf.Clamp(from.y, 0, H - 1);
        var seen = new HashSet<Vector2Int> { from };
        var q = new Queue<Vector2Int>();
        q.Enqueue(from);
        while (q.Count > 0)
        {
            var p = q.Dequeue();
            // 좌우 이동 가능(좌 또는 우가 열림) + 충분히 넓은 열린영역에 속한 칸만 스폰 후보로.
            if (!blocked(p.x, p.y)
                && (!blocked(p.x - 1, p.y) || !blocked(p.x + 1, p.y))
                && RegionSize(p, W, H, blocked, MinRegion) >= MinRegion) return p;
            foreach (var d in Dirs)
            {
                var np = p + d;
                if (np.x < 0 || np.y < 0 || np.x >= W || np.y >= H) continue;
                if (seen.Contains(np)) continue;
                seen.Add(np); q.Enqueue(np);
            }
        }
        return null;
    }
}
