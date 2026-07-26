using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 그리드 맵 에디터. Scene 뷰에서 클릭으로 맵 구성 오브젝트(장애물/적/플레이어 스폰지점)를
/// 활성 씬의 <see cref="GridModule"/> 칸에 스냅해 배치·삭제한다. 배치물은 씬의 "MapObjects"
/// 루트 아래 실제 GameObject로 생성되어 씬 저장으로 영구화된다(별도 데이터 포맷 없음).
///
/// 사용: 상단 메뉴 Tools/Ricochet/맵 에디터 → 창에서 브러시 선택 → Scene 뷰 좌클릭 배치.
/// 좌드래그로 연속 배치, Erase 모드(또는 배치 중 Shift+클릭)로 삭제, Ctrl+Z로 취소.
/// </summary>
public class MapEditorWindow : EditorWindow
{
    private const string MapRootName = "MapObjects";
    private static readonly string[] PaletteFolders = { "Assets/Prefabs/Obstacles", "Assets/Prefabs/Enemies" };

    private enum Mode { Paint, Erase }

    private class PaletteEntry
    {
        public GameObject prefab;
        public string category;
        public int rank;    // 정렬용: 장애물0 / 적1 / 특수2
        public bool isSpawn;
    }

    private bool _active = true;
    private Mode _mode = Mode.Paint;
    private bool _snap = true;
    private bool _overwrite;

    private readonly List<PaletteEntry> _palette = new List<PaletteEntry>();
    private GameObject _brush;
    private Vector2 _scroll;

    [MenuItem("Tools/Ricochet/맵 에디터")]
    private static void Open()
    {
        var w = GetWindow<MapEditorWindow>("맵 에디터");
        w.minSize = new Vector2(300, 380);
    }

    private void OnEnable()
    {
        ReloadPalette();
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    // ───────────────────────── 팔레트 ─────────────────────────

    private void ReloadPalette()
    {
        _palette.Clear();

        var folders = new List<string>();
        foreach (var f in PaletteFolders)
            if (AssetDatabase.IsValidFolder(f)) folders.Add(f);

        if (folders.Count > 0)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", folders.ToArray()))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                bool isEnemy = path.Contains("/Enemies/");
                _palette.Add(new PaletteEntry
                {
                    prefab = prefab,
                    category = isEnemy ? "적" : "장애물",
                    rank = isEnemy ? 1 : 0,
                });
            }
        }

        var spawn = FindSpawnPrefab();
        if (spawn != null)
            _palette.Add(new PaletteEntry { prefab = spawn, category = "특수", rank = 2, isSpawn = true });

        _palette.Sort((a, b) => a.rank != b.rank ? a.rank.CompareTo(b.rank)
                                                 : string.CompareOrdinal(a.prefab.name, b.prefab.name));

        if (_brush == null && _palette.Count > 0) _brush = _palette[0].prefab;
    }

    private static GameObject FindSpawnPrefab()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go != null && go.GetComponent<PlayerSpawnMarker>() != null) return go;
        }
        return null;
    }

    private PaletteEntry CurrentEntry()
    {
        foreach (var e in _palette) if (e.prefab == _brush) return e;
        return null;
    }

    // ───────────────────────── 창 GUI ─────────────────────────

    private void OnGUI()
    {
        EditorGUILayout.Space();
        _active = EditorGUILayout.ToggleLeft("배치 활성 (Scene 뷰에서 클릭)", _active);
        _mode = (Mode)GUILayout.Toolbar((int)_mode, new[] { "배치(Paint)", "삭제(Erase)" });
        _snap = EditorGUILayout.ToggleLeft("그리드 스냅", _snap);
        _overwrite = EditorGUILayout.ToggleLeft("같은 칸 덮어쓰기", _overwrite);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("팔레트 새로고침")) ReloadPalette();
            if (GUILayout.Button("NavMesh 재베이크")) RebakeNavMesh();
        }

        var grid = FindGrid();
        EditorGUILayout.HelpBox(
            grid != null
                ? $"그리드 {grid.Columns}×{grid.Rows}, cell {grid.CellSize} · 좌클릭 배치 / Shift+클릭 또는 Erase로 삭제"
                : "활성 씬에 GridModule이 없어 배치할 수 없습니다. 맵 씬을 열어주세요.",
            grid != null ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.LabelField("팔레트", EditorStyles.boldLabel);
        if (_palette.Count == 0)
            EditorGUILayout.HelpBox("팔레트가 비었습니다. Assets/Prefabs/Obstacles·Enemies 확인 후 '팔레트 새로고침'.", MessageType.Info);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        string lastCat = null;
        foreach (var e in _palette)
        {
            if (e.category != lastCat) { EditorGUILayout.LabelField(e.category, EditorStyles.miniBoldLabel); lastCat = e.category; }

            bool sel = _brush == e.prefab;
            var tex = AssetPreview.GetAssetPreview(e.prefab);
            var content = new GUIContent("  " + e.prefab.name, tex);
            var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fixedHeight = 34 };

            var bg = GUI.backgroundColor;
            if (sel) GUI.backgroundColor = new Color(0.4f, 0.8f, 1f);
            if (GUILayout.Button(content, style)) _brush = e.prefab;
            GUI.backgroundColor = bg;
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.LabelField("현재 브러시", _brush != null ? _brush.name : "(없음)");
    }

    // ───────────────────────── Scene 뷰 상호작용 ─────────────────────────

    private void OnSceneGUI(SceneView sv)
    {
        if (!_active || _brush == null) return;
        var grid = FindGrid();
        if (grid == null) return;

        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);

        if (!MouseToWorld(e, out Vector3 world)) return;
        Vector2Int cell = grid.WorldToCell(world);
        bool inside = grid.IsInsideGrid(cell);

        if (inside)
        {
            Vector2 c = grid.CellToWorld(cell);
            float h = grid.CellSize * 0.5f;
            var rect = new[]
            {
                new Vector3(c.x - h, c.y - h), new Vector3(c.x + h, c.y - h),
                new Vector3(c.x + h, c.y + h), new Vector3(c.x - h, c.y + h),
            };
            Color col = _mode == Mode.Erase ? new Color(1f, 0.35f, 0.35f) : new Color(0.3f, 1f, 0.55f);
            Handles.DrawSolidRectangleWithOutline(rect, new Color(col.r, col.g, col.b, 0.15f), col);
        }

        switch (e.GetTypeForControl(id))
        {
            case EventType.Layout:
                HandleUtility.AddDefaultControl(id);
                break;

            case EventType.MouseDown:
            case EventType.MouseDrag:
                if (e.button == 0 && !e.alt && inside)
                {
                    bool erase = _mode == Mode.Erase || e.shift;
                    if (erase) EraseAt(grid, cell);
                    else PlaceAt(grid, cell, world);
                    e.Use();
                }
                break;

            case EventType.MouseMove:
                sv.Repaint();
                break;
        }
    }

    private static bool MouseToWorld(Event e, out Vector3 world)
    {
        world = default;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        if (Mathf.Abs(ray.direction.z) < 1e-6f) return false;
        float t = -ray.origin.z / ray.direction.z; // z=0 평면 교점
        if (t < 0f) return false;
        world = ray.origin + ray.direction * t;
        return true;
    }

    // ───────────────────────── 배치 / 삭제 ─────────────────────────

    private void PlaceAt(GridModule grid, Vector2Int cell, Vector3 world)
    {
        var root = GetOrCreateRoot(grid);
        var entry = CurrentEntry();
        bool isSpawn = entry != null && entry.isSpawn;

        var existing = FindOccupant(grid, root, cell);
        if (existing != null)
        {
            if (!_overwrite && !isSpawn) return; // 드래그 중복 방지
            Undo.DestroyObjectImmediate(existing);
        }
        if (isSpawn) RemoveExistingSpawns();

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(_brush);
        if (inst == null) return;
        Undo.RegisterCreatedObjectUndo(inst, "맵 오브젝트 배치");
        inst.transform.SetParent(root, true);

        Vector2 pos = _snap ? grid.CellToWorld(cell) : (Vector2)world;
        inst.transform.position = new Vector3(pos.x, pos.y, 0f);

        if (isSpawn) WireSpawn(inst.transform);

        EditorSceneManager.MarkSceneDirty(inst.scene);
        Selection.activeGameObject = inst;
    }

    private void EraseAt(GridModule grid, Vector2Int cell)
    {
        var root = FindRoot(grid);
        if (root == null) return;
        var occ = FindOccupant(grid, root, cell);
        if (occ != null)
        {
            var scene = occ.scene;
            Undo.DestroyObjectImmediate(occ);
            EditorSceneManager.MarkSceneDirty(scene);
        }
    }

    private static GameObject FindOccupant(GridModule grid, Transform root, Vector2Int cell)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            var ch = root.GetChild(i);
            if (grid.WorldToCell(ch.position) == cell) return ch.gameObject;
        }
        return null;
    }

    private static Transform FindRoot(GridModule grid)
    {
        foreach (var go in grid.gameObject.scene.GetRootGameObjects())
            if (go.name == MapRootName) return go.transform;
        return null;
    }

    private static Transform GetOrCreateRoot(GridModule grid)
    {
        var existing = FindRoot(grid);
        if (existing != null) return existing;

        var root = new GameObject(MapRootName);
        EditorSceneManager.MoveGameObjectToScene(root, grid.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(root, "MapObjects 루트 생성");
        return root.transform;
    }

    private static void RemoveExistingSpawns()
    {
        foreach (var m in Object.FindObjectsOfType<PlayerSpawnMarker>())
            Undo.DestroyObjectImmediate(m.gameObject);
    }

    private static void WireSpawn(Transform marker)
    {
        var ppm = Object.FindObjectOfType<PlayerPositionModule>();
        if (ppm == null) return;
        Undo.RecordObject(ppm, "스폰 지점 연결");
        ppm.SetSpawnPoint(marker);
        EditorUtility.SetDirty(ppm);
    }

    // ───────────────────────── NavMesh / 그리드 ─────────────────────────

    private static GridModule FindGrid() => Object.FindObjectOfType<GridModule>();

    /// <summary>씬의 NavMeshSurface(NavMeshPlus)를 리플렉션으로 찾아 에디터 타임 재베이크한다.</summary>
    private static void RebakeNavMesh()
    {
        foreach (var mb in Object.FindObjectsOfType<MonoBehaviour>())
        {
            if (mb.GetType().Name != "NavMeshSurface") continue;
            var m = mb.GetType().GetMethod("BuildNavMesh", BindingFlags.Public | BindingFlags.Instance);
            if (m == null) continue;
            Physics2D.SyncTransforms();
            m.Invoke(mb, null);
            Debug.Log("[MapEditor] NavMesh 재베이크 완료");
            return;
        }
        Debug.LogWarning("[MapEditor] 씬에서 NavMeshSurface를 찾지 못했습니다.");
    }
}
