using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 맵 에디터의 Tilemap 레이어 구조를 만들고 찾는 에디터 전용 헬퍼.
/// GridModule에 정렬된 Unity Grid 아래에 바닥/벽(일반·강철·슬라임) 타일맵 레이어를 구성한다.
///
/// 벽 레이어에는 <see cref="TilemapCollider2D"/> + <see cref="CompositeCollider2D"/> +
/// <see cref="ObstacleTypeMarker"/>(+슬라임엔 <see cref="ElasticWallMarker"/>)를 같은 GameObject에 부착해,
/// BulletController의 리코셰 판정(충돌 콜라이더에서 IBulletObstacleInfoProvider 조회)과
/// NavMesh(CollectSources2d의 2D 콜라이더 수집)가 그대로 동작하도록 한다.
/// </summary>
public static class MapTileLayers
{
    public const string GridName = "TileMap";
    public const int ObstacleLayer = 8; // 총알 wallLayerMask 대상 레이어

    public struct LayerDef
    {
        public string layerName;
        public string ruleTilePath;
        public bool isWall;
        public BulletTargetType targetType;
        public bool elastic;
        public int sortingOrder;
        public string label;
    }

    public static readonly LayerDef Floor = new LayerDef
    {
        layerName = "Tilemap_Floor", label = "바닥",
        ruleTilePath = "Assets/Resources/타일팔레트/던전바닥/Floor_RuleTile.asset",
        isWall = false, sortingOrder = -10,
    };
    public static readonly LayerDef WallNormal = new LayerDef
    {
        layerName = "Tilemap_Wall_Normal", label = "일반벽",
        ruleTilePath = "Assets/Resources/타일팔레트/실제벽/Normal_Wall_RuleTile.asset",
        isWall = true, targetType = BulletTargetType.Wall, elastic = false, sortingOrder = 0,
    };
    public static readonly LayerDef WallSteel = new LayerDef
    {
        layerName = "Tilemap_Wall_Steel", label = "강철벽",
        ruleTilePath = "Assets/Resources/타일팔레트/실제벽/Steel_Wall_RuleTile.asset",
        isWall = true, targetType = BulletTargetType.ArmoredWall, elastic = false, sortingOrder = 0,
    };
    public static readonly LayerDef WallSlime = new LayerDef
    {
        layerName = "Tilemap_Wall_Slime", label = "슬라임벽",
        ruleTilePath = "Assets/Resources/타일팔레트/실제벽/Slime_Wall_RuleTile.asset",
        isWall = true, targetType = BulletTargetType.Wall, elastic = true, sortingOrder = 0,
    };

    public static IReadOnlyList<LayerDef> All => new[] { Floor, WallNormal, WallSteel, WallSlime };

    // ───────────────────────── Grid ─────────────────────────

    /// <summary>씬에서 기존 Grid를 찾거나 새로 만든다(새로 만들 때만 GridModule에 정렬).</summary>
    public static Grid GetOrCreateGrid(GridModule module)
    {
        var scene = module.gameObject.scene;
        foreach (var root in scene.GetRootGameObjects())
        {
            var g = root.GetComponentInChildren<Grid>(true);
            if (g != null) return g;
        }
        var go = new GameObject(GridName);
        EditorSceneManager.MoveGameObjectToScene(go, scene);
        Undo.RegisterCreatedObjectUndo(go, "TileMap 그리드 생성");
        var grid = go.AddComponent<Grid>();
        AlignGrid(grid, module);
        return grid;
    }

    public static void AlignGrid(Grid grid, GridModule module)
    {
        Undo.RecordObject(grid, "그리드 정렬");
        Undo.RecordObject(grid.transform, "그리드 정렬");
        grid.cellSize = new Vector3(module.CellSize, module.CellSize, 0f);
        grid.transform.position = new Vector3(module.Origin.x, module.Origin.y, 0f);
    }

    // ───────────────────────── Layer ─────────────────────────

    public static Tilemap FindLayer(GridModule module, string layerName)
    {
        var scene = module.gameObject.scene;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var tm in root.GetComponentsInChildren<Tilemap>(true))
                if (tm.name == layerName) return tm;
        return null;
    }

    /// <summary>레이어 타일맵을 찾거나(없으면) 규격대로 생성해 반환한다.</summary>
    public static Tilemap EnsureLayer(GridModule module, LayerDef def)
    {
        var existing = FindLayer(module, def.layerName);
        if (existing != null) return existing;

        var grid = GetOrCreateGrid(module);

        var go = new GameObject(def.layerName);
        go.transform.SetParent(grid.transform, false);
        Undo.RegisterCreatedObjectUndo(go, "타일맵 레이어 생성");

        var tm = go.AddComponent<Tilemap>();
        var tr = go.AddComponent<TilemapRenderer>();
        tr.sortingOrder = def.sortingOrder;

        if (def.isWall)
        {
            go.layer = ObstacleLayer;
            // 정적 콜라이더: 셀 단위 TilemapCollider2D를 그대로 사용한다.
            // (CompositeCollider2D는 저장 시 지오메트리가 빈 채로 베이크되어 로드 후 자동 재생성되지
            //  않는 문제가 있어 사용하지 않는다. 셀 단위 박스만으로도 총알 리코셰/NavMesh 소스로 충분.)
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            go.AddComponent<TilemapCollider2D>();

            var marker = go.AddComponent<ObstacleTypeMarker>();
            var so = new SerializedObject(marker);
            var p = so.FindProperty("targetType");
            if (p != null) { p.enumValueIndex = (int)def.targetType; so.ApplyModifiedPropertiesWithoutUndo(); }

            if (def.elastic) go.AddComponent<ElasticWallMarker>();

            // NavMeshPlus가 이 콜라이더를 '통행 불가' 구멍으로 카브하도록 표시.
            // (없으면 벽 콜라이더가 '걷기 가능' 지오메트리로 수집되어 적이 벽을 통과함)
            SetNotWalkable(go);
        }
        return tm;
    }

    /// <summary>NavMeshPlus의 NavMeshModifier를 리플렉션으로 붙이고 Area='Not Walkable'로 설정(어셈블리 하드 참조 회피).</summary>
    public static void SetNotWalkable(GameObject go)
    {
        var t = FindNavMeshModifierType();
        if (t == null) { Debug.LogWarning("[MapTileLayers] NavMeshModifier 타입을 찾지 못해 벽 카브 표시를 건너뜁니다."); return; }
        var comp = go.GetComponent(t) ?? go.AddComponent(t);
        var so = new SerializedObject(comp);
        var oa = so.FindProperty("m_OverrideArea"); if (oa != null) oa.boolValue = true;
        var ar = so.FindProperty("m_Area"); if (ar != null) ar.intValue = 1; // 1 = Not Walkable
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Type FindNavMeshModifierType()
    {
        var t = Type.GetType("NavMeshPlus.Components.NavMeshModifier, NavMeshPlus");
        if (t != null) return t;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { t = asm.GetTypes().FirstOrDefault(x => x.Name == "NavMeshModifier"); }
            catch { t = null; }
            if (t != null) return t;
        }
        return null;
    }

    /// <summary>Grid 정렬 + 모든 표준 레이어를 보장한다(에디터 버튼용).</summary>
    public static void EnsureAllAligned(GridModule module)
    {
        var grid = GetOrCreateGrid(module);
        AlignGrid(grid, module);
        foreach (var def in All)
        {
            var tm = EnsureLayer(module, def);
            // 이미 존재하던 벽 레이어도 NavMesh 카브 표시를 보수(관통 방지).
            if (def.isWall && tm != null) SetNotWalkable(tm.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(module.gameObject.scene);
    }
}
