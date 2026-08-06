using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 기획 슬라이드(맵 임시파일 pptx)의 표(Table) 배치를 Stage 씬으로 옮기는 1회용 임포터.
/// 완성 스테이지(Stage7)를 템플릿으로 복제→base로 스트립→그리드 리사이즈→
/// 타일(바닥/벽 RuleTile)·프리팹(장애물/적) 재배치→NavMesh 재베이크→저장 순으로 만든다.
/// (맵 에디터 MapEditorWindow.DuplicateStageBaseOnly + PaintTile/PlaceAt 과 동일 파이프라인)
///
/// 좌표: 표는 행0=위. GridModule 원점=좌하단이라 gridY = rows-1-row 로 뒤집는다.
/// 셀 토큰: '.'/빈칸=바닥, 벽/철벽/슬라임=벽 타일, 풀/나무/바위/민간/모래/아지=장애물 프리팹,
///          일/화/얼/전/장/신/특=적 프리팹.
/// </summary>
public static class MapSlideImporter
{
    const string Template = "Assets/Scenes/Stage7.unity";
    const string OutDir = "Assets/Scenes/SlideMaps";
    const string MapRootName = "MapObjects";

    class MapDef { public string scene; public int cols; public int rows; public string[] top; }

    static readonly Dictionary<string, string> ObstaclePrefab = new Dictionary<string, string>
    {
        { "풀",   "Assets/Prefabs/Obstacles/WallPrefab_Bush.prefab" },
        { "나무", "Assets/Prefabs/Obstacles/WallPrefab_Tree.prefab" },
        { "바위", "Assets/Prefabs/Obstacles/WallPrefab_Rock.prefab" },
        { "민간", "Assets/Prefabs/Obstacles/WallPrefab_Civilian.prefab" },
        { "모래", "Assets/Prefabs/Obstacles/WallPrefab_Sandstorm.prefab" },
        { "아지", "Assets/Prefabs/Obstacles/WallPrefab_HeatHaze.prefab" },
    };

    static readonly Dictionary<string, string> EnemyPrefab = new Dictionary<string, string>
    {
        { "일", "Assets/Prefabs/Enemies/Enemy_Base.prefab" },
        { "화", "Assets/Prefabs/Enemies/Enemy_Fire.prefab" },
        { "얼", "Assets/Prefabs/Enemies/Enemy_Ice.prefab" },
        { "전", "Assets/Prefabs/Enemies/Enemy_Electric.prefab" },
        { "장", "Assets/Prefabs/Enemies/Enemy_Armored.prefab" },
        { "신", "Assets/Prefabs/Enemies/Enemy_Haste.prefab" },
        { "특", "Assets/Prefabs/Enemies/Enemy_Summon.prefab" },
    };

    static List<MapDef> Maps() => new List<MapDef>
    {
        new MapDef { scene = "Tutorial1", cols = 9, rows = 15, top = new[]
        {
            ".,.,.,.,.,.,.,.,.",
            ".,벽,벽,벽,벽,벽,벽,벽,.",
            ".,벽,벽,.,.,.,.,벽,.",
            ".,벽,벽,일,.,.,일,벽,.",
            ".,벽,벽,.,.,.,.,벽,.",
            ".,벽,벽,.,.,벽,벽,벽,.",
            ".,벽,벽,.,.,.,.,.,.",
            ".,벽,벽,.,일,.,.,.,.",
            ".,벽,벽,벽,벽,벽,.,.,.",
            ".,벽,벽,벽,벽,벽,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
        }},
        new MapDef { scene = "Tutorial2", cols = 9, rows = 15, top = new[]
        {
            ".,.,.,.,.,.,.,.,.",
            ".,벽,벽,벽,벽,벽,벽,벽,.",
            ".,벽,벽,벽,벽,.,.,벽,.",
            ".,벽,벽,벽,.,.,.,벽,.",
            ".,벽,벽,.,.,.,풀,벽,.",
            ".,.,.,.,일,.,.,벽,.",
            ".,.,.,.,풀,.,.,벽,.",
            ".,벽,.,.,.,.,.,벽,.",
            ".,벽,풀,.,.,.,벽,벽,.",
            ".,벽,.,.,.,.,벽,.,.",
            ".,벽,.,.,.,일,벽,.,.",
            ".,.,.,.,.,풀,벽,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
        }},
        new MapDef { scene = "Tutorial3", cols = 9, rows = 15, top = new[]
        {
            ".,.,.,.,.,.,.,.,.",
            ".,벽,벽,벽,벽,벽,벽,벽,.",
            ".,벽,벽,벽,벽,벽,벽,벽,.",
            ".,벽,벽,벽,벽,벽,벽,벽,.",
            ".,벽,벽,벽,벽,벽,벽,벽,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,화,.,얼,.,전,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.",
        }},
        new MapDef { scene = "Hard1", cols = 12, rows = 20, top = new[]
        {
            ".,.,.,.,.,.,.,.,.,.,.,.",
            ".,나무,나무,나무,나무,나무,나무,나무,나무,나무,나무,.",
            ".,.,신,.,나무,나무,.,.,.,.,.,.",
            ".,.,.,.,나무,나무,.,.,.,.,.,.",
            ".,.,.,.,나무,나무,.,.,.,일,벽,.",
            ".,.,.,.,나무,나무,.,.,.,.,벽,.",
            ".,.,화,.,.,.,.,.,.,.,벽,.",
            ".,.,.,.,.,.,.,.,.,.,벽,.",
            ".,.,.,나무,나무,나무,.,.,.,.,벽,.",
            ".,.,신,나무,나무,나무,.,일,.,.,벽,.",
            ".,모래,모래,나무,나무,나무,나무,나무,나무,나무,벽,.",
            ".,.,.,.,나무,나무,.,나무,나무,.,벽,.",
            ".,.,.,.,나무,.,.,.,.,.,벽,.",
            ".,.,.,.,나무,.,.,.,.,.,벽,.",
            ".,.,.,.,나무,.,.,.,.,.,벽,.",
            ".,.,.,.,.,.,.,.,.,.,벽,.",
            ".,.,.,.,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.,.,.,.",
            ".,.,.,.,.,.,.,.,.,.,.,.",
        }},
    };

    [MenuItem("Tools/Ricochet/슬라이드→SlideMaps 생성")]
    public static void BuildAll()
    {
        if (!AssetDatabase.IsValidFolder(OutDir))
            AssetDatabase.CreateFolder("Assets/Scenes", "SlideMaps");
        int ok = 0;
        foreach (var m in Maps())
        {
            try { BuildOne(m); ok++; }
            catch (Exception e) { Debug.LogError($"[MapSlideImporter] {m.scene} 실패: {e}"); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[MapSlideImporter] 완료: {ok}/4 스테이지 생성.");
    }

    static void BuildOne(MapDef m)
    {
        string dst = OutDir + "/" + m.scene + ".unity";
        if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
        if (!AssetDatabase.CopyAsset(Template, dst))
            throw new Exception($"템플릿 복제 실패: {Template} → {dst}");
        AssetDatabase.Refresh();

        var scene = EditorSceneManager.OpenScene(dst, OpenSceneMode.Single);
        StripToBase(scene);

        var grid = UnityEngine.Object.FindObjectOfType<GridModule>();
        if (grid == null) throw new Exception("GridModule 없음");
        float cell = grid.CellSize;
        Vector2 origin = new Vector2(-m.cols * cell * 0.5f, -m.rows * cell * 0.5f);
        grid.Configure(m.cols, m.rows, cell, origin);
        EditorUtility.SetDirty(grid);
        var vis = grid.GetComponent<MapGridVisualizer>(); if (vis != null) vis.Rebuild();
        MapTileLayers.EnsureAllAligned(grid);

        var floorTm = MapTileLayers.EnsureLayer(grid, MapTileLayers.Floor);
        var floorTile = AssetDatabase.LoadAssetAtPath<TileBase>(MapTileLayers.Floor.ruleTilePath);
        var wnTm = MapTileLayers.EnsureLayer(grid, MapTileLayers.WallNormal);
        var wnTile = AssetDatabase.LoadAssetAtPath<TileBase>(MapTileLayers.WallNormal.ruleTilePath);
        var wsTm = MapTileLayers.EnsureLayer(grid, MapTileLayers.WallSteel);
        var wsTile = AssetDatabase.LoadAssetAtPath<TileBase>(MapTileLayers.WallSteel.ruleTilePath);
        var slTm = MapTileLayers.EnsureLayer(grid, MapTileLayers.WallSlime);
        var slTile = AssetDatabase.LoadAssetAtPath<TileBase>(MapTileLayers.WallSlime.ruleTilePath);

        // 바닥 전면 채움
        if (floorTile != null)
            for (int x = 0; x < m.cols; x++)
                for (int y = 0; y < m.rows; y++)
                    floorTm.SetTile(new Vector3Int(x, y, 0), floorTile);

        var root = GetOrCreateRoot(scene);
        int walls = 0, obst = 0, enemies = 0, unknown = 0;

        for (int r = 0; r < m.rows; r++)
        {
            var cells = m.top[r].Split(',');
            for (int c = 0; c < m.cols && c < cells.Length; c++)
            {
                string tok = cells[c].Trim();
                if (tok == "" || tok == ".") continue;
                int x = c;
                int y = m.rows - 1 - r;
                var cp = new Vector3Int(x, y, 0);

                if (tok == "벽") { wnTm.SetTile(cp, wnTile); walls++; }
                else if (tok == "철벽") { wsTm.SetTile(cp, wsTile); walls++; }
                else if (tok == "슬라임" || tok == "슬라임벽") { slTm.SetTile(cp, slTile); walls++; }
                else if (ObstaclePrefab.TryGetValue(tok, out var opath)) { PlacePrefab(grid, root, opath, x, y); obst++; }
                else if (EnemyPrefab.TryGetValue(tok, out var epath)) { PlacePrefab(grid, root, epath, x, y); enemies++; }
                else { Debug.LogWarning($"[MapSlideImporter] {m.scene} ({x},{y}) 알 수 없는 토큰 '{tok}'"); unknown++; }
            }
        }

        FrameCamera(grid, m, cell);
        RepositionPlayer(grid, m);
        Rebake();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AddSceneToBuildSettings(dst);
        Debug.Log($"[MapSlideImporter] {m.scene} ({m.cols}×{m.rows}) — 벽 {walls}, 장애물 {obst}, 적 {enemies}, 미상 {unknown}");
    }

    static void PlacePrefab(GridModule grid, Transform root, string path, int x, int y)
    {
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (pf == null) { Debug.LogWarning($"[MapSlideImporter] 프리팹 없음: {path}"); return; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf);
        inst.transform.SetParent(root, true);
        Vector2 w = grid.CellToWorld(new Vector2Int(x, y));
        inst.transform.position = new Vector3(w.x, w.y, 0f);
    }

    static void StripToBase(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == MapRootName) UnityEngine.Object.DestroyImmediate(go);

        foreach (var mk in UnityEngine.Object.FindObjectsOfType<ObstacleTypeMarker>())
            if (mk) UnityEngine.Object.DestroyImmediate(mk.gameObject);
        foreach (var e in UnityEngine.Object.FindObjectsOfType<EnemyController>())
            if (e) UnityEngine.Object.DestroyImmediate(e.gameObject);
        // PlayerSpawnMarker/Player 는 유지(스폰 배선 보존) — RepositionPlayer 에서 재배치.

        var grid = UnityEngine.Object.FindObjectOfType<GridModule>();
        if (grid != null)
            foreach (var def in MapTileLayers.All)
            {
                var tm = MapTileLayers.FindLayer(grid, def.layerName);
                if (tm != null) tm.ClearAllTiles();
            }
    }

    static Transform GetOrCreateRoot(Scene scene)
    {
        foreach (var go in scene.GetRootGameObjects())
            if (go.name == MapRootName) return go.transform;
        var root = new GameObject(MapRootName);
        EditorSceneManager.MoveGameObjectToScene(root, scene);
        return root.transform;
    }

    static void FrameCamera(GridModule grid, MapDef m, float cell)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 ctr = grid.GridWorldCenter;
        cam.transform.position = new Vector3(ctr.x, ctr.y, cam.transform.position.z);
        if (cam.orthographic)
        {
            float aspect = cam.aspect > 0.01f ? cam.aspect : 16f / 9f;
            float sizeH = m.rows * cell * 0.5f;
            float sizeW = (m.cols * cell * 0.5f) / aspect;
            cam.orthographicSize = Mathf.Max(sizeH, sizeW) + 0.5f;
        }
        EditorUtility.SetDirty(cam);
    }

    static void RepositionPlayer(GridModule grid, MapDef m)
    {
        Vector2 spot = grid.CellToWorld(new Vector2Int(m.cols / 2, 1));
        var player = GameObject.Find("Player");
        if (player != null)
            player.transform.position = new Vector3(spot.x, spot.y, player.transform.position.z);
        foreach (var sm in UnityEngine.Object.FindObjectsOfType<PlayerSpawnMarker>())
            sm.transform.position = new Vector3(spot.x, spot.y, sm.transform.position.z);
    }

    static void Rebake()
    {
        foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (mb.GetType().Name != "NavMeshSurface") continue;
            var mi = mb.GetType().GetMethod("BuildNavMesh", BindingFlags.Public | BindingFlags.Instance);
            if (mi == null) continue;
            Physics2D.SyncTransforms();
            mi.Invoke(mb, null);
            return;
        }
    }

    static void AddSceneToBuildSettings(string path)
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in list) if (s.path == path) return;
        list.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
