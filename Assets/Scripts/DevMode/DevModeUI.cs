using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// "총합 테스트" 씬 전용 개발자 모드 UI.
/// - 좌하단: 지도(격자) 종류 전환 버튼 (튜토리얼 9×15 / 표준 12×20 / 하드 17×24).
/// - 우하단: 장애물·몬스터 배치 팔레트. 항목을 클릭하면 커서에 고스트가 붙고,
///   원하는 격자 칸을 클릭하면 그 자리에 배치된다.
/// - 우상단: 탄환 지급 패널. 프로젝트에 존재하는 모든 BulletItemDefinition을 나열하고,
///   버튼을 누르면 플레이어(인벤토리)가 그 탄환을 1발 획득한다. 버튼에는 현재 보유 수량이 함께 표시된다.
/// - 배치된 장애물/몬스터는 더블클릭으로 삭제할 수 있다(이 씬에서만 동작).
/// - 마우스 커서가 이 UI(버튼/패널) 위에 있는 동안에는 PlayerShooter의 조준/격발이 자동으로 멈춘다
///   (PlayerShooter.Update()의 EventSystem.IsPointerOverGameObject() 체크 참고).
/// </summary>
public class DevModeUI : MonoBehaviour
{
    [Header("씬 참조 (비워두면 자동으로 찾음)")]
    [SerializeField] private GridModule _gridModule;
    [SerializeField] private MapShapeModule _mapShape;
    [SerializeField] private MapGridVisualizer _gridVisualizer;
    [SerializeField] private Transform _player;
    [SerializeField] private Camera _cam;

    [Header("배치 가능한 장애물 프리팹 (비워두면 Assets/Prefabs/Obstacles 에서 자동 수집)")]
    [SerializeField] private List<GameObject> _obstaclePrefabs = new List<GameObject>();

    [Header("배치 가능한 몬스터 프리팹 (비워두면 Assets/Prefabs/Enemies 에서 자동 수집)")]
    [SerializeField] private List<GameObject> _enemyPrefabs = new List<GameObject>();

    [Header("지급 가능한 탄환 정의 (비워두면 프로젝트 전체에서 BulletItemDefinition 자동 수집)")]
    [SerializeField] private List<BulletItemDefinition> _bulletDefs = new List<BulletItemDefinition>();

    [Header("UI 배치")]
    [SerializeField] private Vector2 _margin = new Vector2(24f, 24f);
    [SerializeField] private float _panelWidth = 300f;

    // ───────────────────────── 지도 프리셋 ─────────────────────────

    [System.Serializable]
    private struct MapPreset
    {
        public string label;
        public int columns;
        public int rows;
        public MapPreset(string label, int columns, int rows)
        {
            this.label = label; this.columns = columns; this.rows = rows;
        }
    }

    private readonly MapPreset[] _presets = new MapPreset[]
    {
        new MapPreset("튜토리얼 (9×15)", 9, 15),
        new MapPreset("표준 (12×20)", 12, 20),
        new MapPreset("하드 (17×24)", 17, 24),
    };
    private int _presetIndex = -1;

    // ───────────────────────── 내부 상태 ─────────────────────────

    private Canvas _canvas;
    private Font _font;
    private Transform _placedRoot;

    private Text _mapButtonText;
    private RectTransform _bulletContent;
    private RectTransform _bulletPanelRt;
    private RectTransform _palettePanelRt;
    private RectTransform _sharedInventoryRt;
    private readonly Dictionary<RectTransform, float> _clampBaseHeights = new Dictionary<RectTransform, float>();
    private readonly Dictionary<BulletItemDefinition, Text> _bulletLabels = new Dictionary<BulletItemDefinition, Text>();

    private GameObject _selectedPrefab;
    private GameObject _ghost;
    private SpriteRenderer _ghostRenderer;

    private GameObject _lastClicked;
    private float _lastClickTime;
    private const float DoubleClickWindow = 0.35f;

    // ───────────────────────── 초기화 ─────────────────────────

    private void Awake()
    {
#if UNITY_EDITOR
        AutoDiscoverAssets();
#endif
        if (_gridModule == null) _gridModule = FindObjectOfType<GridModule>();
        if (_mapShape == null && _gridModule != null) _mapShape = _gridModule.GetComponent<MapShapeModule>();
        if (_gridVisualizer == null && _gridModule != null) _gridVisualizer = _gridModule.GetComponent<MapGridVisualizer>();
        if (_cam == null) _cam = Camera.main;
        if (_player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }
        if (_placedRoot == null)
        {
            var go = new GameObject("Placed_DevMode");
            go.transform.SetParent(transform, false);
            _placedRoot = go.transform;
        }

        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildUI();
    }

    private void Start()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.Inventory.Changed += RefreshBulletLabels;
        RefreshBulletLabels();
        StartCoroutine(RelocateSharedBulletHud());
    }

    /// <summary>공용 탄환 선택 HUD(BulletInventoryUI, 좌하단)가 지도와 겹치지 않도록
    /// 이 씬에서만 우하단으로 옮긴다. 공용 스크립트 자체는 건드리지 않는다(다른 스테이지 씬에는 영향 없음).</summary>
    private System.Collections.IEnumerator RelocateSharedBulletHud()
    {
        for (int i = 0; i < 30; i++)
        {
            var hudGO = GameObject.Find("BulletHudCanvas");
            if (hudGO != null)
            {
                RectTransform panelRt = null;
                foreach (RectTransform child in hudGO.transform)
                {
                    panelRt = child;
                    break;
                }
                if (panelRt != null)
                {
                    panelRt.anchorMin = new Vector2(1f, 0f);
                    panelRt.anchorMax = new Vector2(1f, 0f);
                    panelRt.pivot = new Vector2(1f, 0f);
                    panelRt.anchoredPosition = new Vector2(-_margin.x, _margin.y);
                }
                yield break;
            }
            yield return null;
        }
    }


    private void OnDestroy()
    {
        PlayerShooter.ExternalInputBlocked = false;
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.Inventory.Changed -= RefreshBulletLabels;
    }


#if UNITY_EDITOR
    /// <summary>인스펙터에 프리팹/탄환 목록을 수동으로 채우지 않아도 되도록, 에디터에서만 자동 수집한다.</summary>
    private void AutoDiscoverAssets()
    {
        if (_obstaclePrefabs == null || _obstaclePrefabs.Count == 0)
        {
            _obstaclePrefabs = new List<GameObject>();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Obstacles" });
            foreach (var g in guids)
            {
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (go != null) _obstaclePrefabs.Add(go);
            }
        }

        if (_enemyPrefabs == null || _enemyPrefabs.Count == 0)
        {
            _enemyPrefabs = new List<GameObject>();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Enemies" });
            foreach (var g in guids)
            {
                var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (go != null) _enemyPrefabs.Add(go);
            }
        }

        if (_bulletDefs == null || _bulletDefs.Count == 0)
        {
            _bulletDefs = new List<BulletItemDefinition>();
            var guids = UnityEditor.AssetDatabase.FindAssets("t:BulletItemDefinition");
            foreach (var g in guids)
            {
                var def = UnityEditor.AssetDatabase.LoadAssetAtPath<BulletItemDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (def != null) _bulletDefs.Add(def);
            }
        }
    }
#endif

    // ───────────────────────── 매 프레임 입력 처리 ─────────────────────────

    private void Update()
    {
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // 장애물/몬스터를 커서에 들고 있는 동안(고스트 활성 상태)에는 플레이어 조준·격발 입력이
        // 함께 발동하지 않도록 PlayerShooter에 차단 신호를 보낸다. 선택이 없으면 즉시 해제.
        PlayerShooter.ExternalInputBlocked = _selectedPrefab != null;

        if (_selectedPrefab != null && _ghost != null)
            _ghost.SetActive(!overUI && _gridModule != null);

        if (overUI) return;
        if (_gridModule == null || _cam == null) return;

        Vector2 world = _cam.ScreenToWorldPoint(Input.mousePosition);

        if (_selectedPrefab != null && _ghost != null && _ghost.activeSelf)
        {
            var cell = _gridModule.WorldToCell(world);
            bool inside = _gridModule.IsInsideGrid(cell);
            bool valid = inside && !_gridModule.IsCellOccupied(cell);
            _ghost.transform.position = inside ? (Vector3)_gridModule.CellToWorld(cell) : (Vector3)world;
            _ghostRenderer.color = valid ? new Color(0.4f, 1f, 0.5f, 0.6f) : new Color(1f, 0.3f, 0.3f, 0.6f);
        }

        if (Input.GetMouseButtonDown(1))
        {
            CancelSelection();
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            var hit = Physics2D.OverlapPoint(world);
            if (hit != null && IsDeletable(hit.gameObject))
            {
                HandlePotentialDoubleClick(hit.gameObject);
                return;
            }

            if (_selectedPrefab != null) TryPlace(world);
        }
    }



    /// <summary>다른 UI 스크립트(인벤토리 등)가 자기 Update()에서 크기를 다시 설정해버리는 경우를 피하려고
    /// 모든 Update()가 끝난 뒤 실행되는 LateUpdate에서 화면 밖으로 잘리는 패널을 매 프레임 축소 보정한다.</summary>
    private void LateUpdate()
    {
        ClampPanelToScreen(_bulletPanelRt, topAnchored: true);
        ClampPanelToScreen(_palettePanelRt, topAnchored: false);

        if (_sharedInventoryRt == null)
        {
            var invGO = GameObject.Find("InventoryCanvas");
            if (invGO != null && invGO.transform.childCount > 0)
                _sharedInventoryRt = invGO.transform.GetChild(0) as RectTransform;
        }
        ClampPanelToScreen(_sharedInventoryRt, topAnchored: true);
    }


    /// <summary>패널이 화면 위/아래로 잘려나가면 자기 자신의 고정된 모서리(피벗)를 기준으로 살짝 축소해
    /// 항상 화면 안에 들어오도록 한다. topAnchored=true면 위쪽 모서리를 고정하고 아래쪽 잘림을 방지하고,
    /// false면 아래쪽 모서리를 고정하고 위쪽 잘림을 방지한다.</summary>
    private void ClampPanelToScreen(RectTransform rt, bool topAnchored, float margin = 4f, float minScale = 0.4f)
    {
        if (rt == null) return;

        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float bottomY = corners[0].y;
        float topY = corners[1].y;

        // 캐시하지 않고 매 프레임 새로 측정한다. 첫 프레임에는 레이아웃(ContentSizeFitter 등)이
        // 아직 자리잡기 전이라 크기가 부정확할 수 있는데, 캐시하면 그 잘못된 값에 영원히 갇히기 때문이다.
        float baseHeight = (topY - bottomY) / Mathf.Max(0.0001f, rt.localScale.y);
        if (baseHeight < 1f) return; // 아직 레이아웃 전(0 또는 매우 작은 값)이면 이번 프레임은 건너뛴다.

        float available = topAnchored ? (topY - margin) : (Screen.height - margin - bottomY);
        float targetScale = available >= baseHeight ? 1f : Mathf.Max(minScale, available / baseHeight);

        if (!Mathf.Approximately(rt.localScale.y, targetScale))
            rt.localScale = new Vector3(targetScale, targetScale, 1f);
    }




    private static bool IsDeletable(GameObject go)
    {
        if (go == null) return false;
        if (go.GetComponent<ObstacleTypeMarker>() != null) return true;
        if (go.CompareTag("Enemy")) return true;
        return false;
    }

    private void HandlePotentialDoubleClick(GameObject go)
    {
        float t = Time.unscaledTime;
        if (_lastClicked == go && (t - _lastClickTime) <= DoubleClickWindow)
        {
            DeleteObject(go);
            _lastClicked = null;
        }
        else
        {
            _lastClicked = go;
            _lastClickTime = t;
        }
    }

    private void DeleteObject(GameObject go)
    {
        if (_gridModule != null)
        {
            var cell = _gridModule.WorldToCell(go.transform.position);
            _gridModule.ReleaseCell(cell);
        }
        Destroy(go);
    }

    private void TryPlace(Vector2 world)
    {
        var cell = _gridModule.WorldToCell(world);
        if (!_gridModule.IsInsideGrid(cell)) return;
        if (_gridModule.IsCellOccupied(cell)) return;

        Vector2 pos = _gridModule.CellToWorld(cell);
        var inst = Instantiate(_selectedPrefab, pos, Quaternion.identity, _placedRoot);
        inst.name = _selectedPrefab.name;
        _gridModule.TryOccupyCell(cell, inst);
    }

    private void SelectPrefab(GameObject prefab)
    {
        _selectedPrefab = prefab;
        EnsureGhost();
        var sr = prefab.GetComponentInChildren<SpriteRenderer>();
        _ghostRenderer.sprite = sr != null ? sr.sprite : null;
        _ghostRenderer.color = new Color(1f, 1f, 1f, 0.55f);
    }

    private void CancelSelection()
    {
        _selectedPrefab = null;
        PlayerShooter.ExternalInputBlocked = false;
        if (_ghost != null) _ghost.SetActive(false);
    }


    private void EnsureGhost()
    {
        if (_ghost != null) return;
        _ghost = new GameObject("DevGhostPreview");
        _ghostRenderer = _ghost.AddComponent<SpriteRenderer>();
        _ghostRenderer.sortingOrder = 500;
        _ghost.SetActive(false);
    }

    // ───────────────────────── 지도 전환 ─────────────────────────

    private void CycleMap()
    {
        _presetIndex = (_presetIndex + 1) % _presets.Length;
        ApplyPreset(_presets[_presetIndex]);
    }

    private void ApplyPreset(MapPreset preset)
    {
        CancelSelection();
        ClearAllPlaceables();

        if (_gridModule != null)
        {
            Vector2 origin = new Vector2(-preset.columns / 2f, -preset.rows / 2f);
            _gridModule.Configure(preset.columns, preset.rows, 1f, origin);
        }

        _gridVisualizer?.Rebuild();
        _mapShape?.Apply();
        RepositionPlayerAndCamera(preset);

        if (_mapButtonText != null)
            _mapButtonText.text = $"지도 변경\n▶ {preset.label}";
    }

    private void ClearAllPlaceables()
    {
        var markers = FindObjectsOfType<ObstacleTypeMarker>(true);
        foreach (var m in markers)
        {
            if (m != null) Destroy(m.gameObject);
        }

        var enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var e in enemies)
        {
            if (e != null) Destroy(e);
        }
    }

    private void RepositionPlayerAndCamera(MapPreset preset)
    {
        if (_gridModule == null) return;

        if (_player != null)
        {
            var spawnCell = new Vector2Int(preset.columns / 2, 1);
            _player.position = _gridModule.CellToWorld(spawnCell);
        }

        if (_cam != null)
        {
            Vector2 center = _gridModule.GridWorldCenter;
            _cam.transform.position = new Vector3(center.x, center.y, _cam.transform.position.z);

            if (_cam.orthographic)
            {
                // 화면 가로세로 비율(aspect)에 맞춰 격자 전체가 반드시 보이도록 계산한다.
                // orthographicSize는 화면 "세로 절반"의 월드 유닛 크기이므로,
                // 세로 기준 필요 크기 = rows/2, 가로 기준 필요 크기 = columns/(2*aspect) 이고
                // 둘 중 더 큰 쪽을 써야 양쪽 다 잘리지 않는다. 약간의 여백(8%)을 더한다.
                float aspect = _cam.aspect > 0.01f ? _cam.aspect : 16f / 9f;
                float sizeForHeight = preset.rows / 2f;
                float sizeForWidth = preset.columns / (2f * aspect);
                float size = Mathf.Max(sizeForHeight, sizeForWidth) * 1.08f;
                _cam.orthographicSize = Mathf.Max(size, 2f);
            }
        }
    }


    private void UpdateMapButtonLabel()
    {
        if (_mapButtonText == null || _gridModule == null) return;
        _mapButtonText.text = $"지도 변경\n(현재 {_gridModule.Columns}×{_gridModule.Rows})";
    }

    // ───────────────────────── 탄환 지급 패널 ─────────────────────────

    private string BulletLabel(BulletItemDefinition def)
    {
        int count = InventoryManager.Instance != null ? InventoryManager.Instance.Inventory.GetQuantity(def) : 0;
        return $"{def.ResolvedName} / {count}개 보유중";
    }

    private void GrantBullet(BulletItemDefinition def)
    {
        if (InventoryManager.Instance == null || def == null) return;
        InventoryManager.Instance.Add(def, 1);
    }

    private void RefreshBulletLabels()
    {
        foreach (var kv in _bulletLabels)
        {
            if (kv.Key == null || kv.Value == null) continue;
            kv.Value.text = BulletLabel(kv.Key);
        }
    }

    // ───────────────────────── 한글 표시 이름 ─────────────────────────

    private static string ObstacleKoreanName(BulletTargetType t)
    {
        switch (t)
        {
            case BulletTargetType.Wall: return "기본 벽";
            case BulletTargetType.ArmoredWall: return "강화 벽 (관통 필요)";
            case BulletTargetType.Bush: return "수풀";
            case BulletTargetType.Tree: return "나무 (파괴 가능)";
            case BulletTargetType.Rock: return "바위 (폭발로만 파괴)";
            case BulletTargetType.Civilian: return "민간인 (피격 시 실패)";
            case BulletTargetType.Sandstorm: return "모래바람";
            case BulletTargetType.ElectricPanel: return "전기 패널";
            case BulletTargetType.HeatHaze: return "아지랑이";
            default: return t.ToString();
        }
    }

    private static string EnemyKoreanName(string prefabName)
    {
        switch (prefabName)
        {
            case "Enemy_Base": return "적 - 기본형";
            case "Enemy_Fire": return "적 - 화염형";
            case "Enemy_Electric": return "적 - 전기형";
            case "Enemy_Ice": return "적 - 냉기형";
            case "Enemy_Armored": return "적 - 장갑형";
            case "Enemy_Haste": return "적 - 신속형";
            default: return prefabName;
        }
    }

    // ───────────────────────── UI 구성 (코드 생성, BulletInventoryUI와 동일한 방식) ─────────────────────────

    private void BuildUI()
    {
        var canvasGO = new GameObject("DevModeCanvas");
        canvasGO.transform.SetParent(transform, false);

        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 998;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        BuildMapPanel(canvasGO.transform);
        BuildPalettePanel(canvasGO.transform);
        BuildBulletPanel(canvasGO.transform);

        UpdateMapButtonLabel();
    }

    private void BuildMapPanel(Transform canvasRoot)
    {
        var panel = CreateVerticalPanel(canvasRoot, "MapPanel");
        panel.anchorMin = new Vector2(1f, 0.5f);
        panel.anchorMax = new Vector2(1f, 0.5f);
        panel.pivot = new Vector2(1f, 0.5f);
        panel.anchoredPosition = new Vector2(-_margin.x, 0f);

        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        AddText(panel, "지도(격자) 종류", 15, new Color(0.6f, 1f, 0.8f), FontStyle.Bold);

        var btn = AddButton(panel, "지도 변경", CycleMap, out _mapButtonText);
        var le = btn.GetComponent<LayoutElement>();
        if (le != null) le.preferredHeight = 44f;
    }



    private void BuildPalettePanel(Transform canvasRoot)
    {
        var panel = CreateVerticalPanel(canvasRoot, "PalettePanel");
        panel.anchorMin = new Vector2(0f, 0f);
        panel.anchorMax = new Vector2(0f, 0f);
        panel.pivot = new Vector2(0f, 0f);
        panel.anchoredPosition = new Vector2(_margin.x, _margin.y);
        _palettePanelRt = panel;

        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        AddText(panel, "설치할 오브젝트 (클릭 후 격자 클릭)", 15, new Color(1f, 0.8f, 0.4f), FontStyle.Bold);

        var grid = CreateGridPanel(panel, "Content", 3, new Vector2(150f, 30f), new Vector2(6f, 6f));

        foreach (var prefab in _obstaclePrefabs)
        {
            if (prefab == null) continue;
            var captured = prefab;
            var marker = prefab.GetComponent<ObstacleTypeMarker>();
            string label = marker != null ? ObstacleKoreanName(marker.TargetType) : prefab.name;
            AddGridButton(grid, label, () => SelectPrefab(captured), new Color(0.16f, 0.16f, 0.2f, 0.95f));
        }

        foreach (var prefab in _enemyPrefabs)
        {
            if (prefab == null) continue;
            var captured = prefab;
            string label = EnemyKoreanName(prefab.name);
            AddGridButton(grid, label, () => SelectPrefab(captured), new Color(0.32f, 0.14f, 0.14f, 0.95f));
        }

        AddText(panel, "우클릭: 선택 취소 / 배치물 더블클릭: 삭제", 10, new Color(0.6f, 0.6f, 0.6f));
    }




    private void BuildBulletPanel(Transform canvasRoot)
    {
        var panel = CreateVerticalPanel(canvasRoot, "BulletPanel");
        panel.anchorMin = new Vector2(0f, 1f);
        panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        // 좌상단 무기 파츠 패널(WeaponPartsCanvas, 세로 약 78) 바로 아래로 배치해 왼쪽 빈 공간을 사용한다.
        panel.anchoredPosition = new Vector2(_margin.x, -(_margin.y + 78f + 16f));
        _bulletPanelRt = panel;

        var bg = panel.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        AddText(panel, "탄환 지급 (클릭 시 1발 추가)", 15, new Color(0.6f, 0.85f, 1f), FontStyle.Bold);

        // 세로로 긴 목록 대신 격자로 배치해 화면이 낮고 넓은 비율에서도 공간을 적게 차지하게 한다.
        var grid = CreateGridPanel(panel, "Content", 2, new Vector2(220f, 30f), new Vector2(6f, 6f));

        foreach (var def in _bulletDefs)
        {
            if (def == null) continue;
            var captured = def;
            var txt = AddGridButton(grid, BulletLabel(def), () => GrantBullet(captured), new Color(0.14f, 0.2f, 0.3f, 0.95f));
            _bulletLabels[def] = txt;
        }
    }





    // ───────────────────────── 위젯 생성 헬퍼 (BulletInventoryUI와 동일 패턴) ─────────────────────────

    private RectTransform CreateVerticalPanel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(12, 12, 10, 12);
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return go.GetComponent<RectTransform>();
    }

    private RectTransform CreateGridPanel(Transform parent, string name, int columns, Vector2 cellSize, Vector2 spacing)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var grid = go.AddComponent<GridLayoutGroup>();
        grid.cellSize = cellSize;
        grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.padding = new RectOffset(0, 0, 0, 0);

        var fitter = go.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return go.GetComponent<RectTransform>();
    }

    private Text AddGridButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, Color bgColor)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = bgColor;

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(Mathf.Min(1f, bgColor.r + 0.18f), Mathf.Min(1f, bgColor.g + 0.18f), Mathf.Min(1f, bgColor.b + 0.18f), 1f);
        colors.pressedColor = new Color(0.55f, 0.5f, 0.15f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var rt = (RectTransform)txtGO.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(4f, 1f);
        rt.offsetMax = new Vector2(-4f, -1f);

        var text = txtGO.AddComponent<Text>();
        text.font = _font;
        text.text = label;
        text.fontSize = 11;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        return text;
    }



    private Text AddText(Transform parent, string message, int fontSize, Color color, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var text = go.AddComponent<Text>();
        text.font = _font;
        text.text = message;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = _panelWidth;
        le.preferredHeight = fontSize + 8f;

        return text;
    }

    private Button AddButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, out Text textComp)
    {
        var go = new GameObject("Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.16f, 0.16f, 0.2f, 0.95f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.3f, 0.3f, 0.36f, 1f);
        colors.pressedColor = new Color(0.55f, 0.5f, 0.15f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(onClick);

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = _panelWidth - 24f;
        le.preferredHeight = 28f;

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var rt = (RectTransform)txtGO.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(8f, 2f);
        rt.offsetMax = new Vector2(-8f, -2f);

        var text = txtGO.AddComponent<Text>();
        text.font = _font;
        text.text = label;
        text.fontSize = 13;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;

        textComp = text;
        return btn;
    }
}
