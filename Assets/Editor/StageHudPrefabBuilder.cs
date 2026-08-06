using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 스테이지 HUD 프리팹(<c>Assets/Prefabs/UI/StageHud.prefab</c>)을 코드로 조립하는 에디터 도구.
///
/// 레이아웃은 세로 1080x1920 기준이고 레퍼런스 배치를 그대로 따른다.
/// <code>
///   ┌──────────────────────────────┐
///   │ [체력]                   (설정) │
///   │ [골드]                   (인벤) │
///   │            콤보!               │
///   │                                │
///   │                       ◎ 실린더  │
///   │ (◀)(▶)            [ ][ ][ ]   │
///   └──────────────────────────────┘
/// </code>
///
/// 손으로 만들지 않고 스크립트로 굽는 이유: 위젯 수가 많고 참조 바인딩이 촘촘해서,
/// 배치를 고칠 때마다 다시 굽는 편이 정확하다. 구운 뒤에는 평범한 프리팹이라
/// 인스펙터에서 자유롭게 손봐도 된다(단, 다시 구우면 덮어써진다).
///
/// 메뉴: Tools ▸ Ricochet ▸ Build Stage HUD Prefab (+ Place In Stage Scenes)
/// </summary>
public static class StageHudPrefabBuilder
{
    private const string ArtDir = "Assets/Sprites/UI/Generated/";
    private const string PrefabPath = "Assets/Prefabs/UI/StageHud.prefab";

    // ── 화면 기준 치수 ──
    private const float RefW = 1080f, RefH = 1920f;
    private const float Margin = 40f;

    // ── 실린더 치수 ──
    // 뒷판 스프라이트(Cyl_Plate)에 구운 구멍 위치와 반드시 맞아야 한다.
    // 텍스처 640px 기준 R=314, 궤도=0.60R, 구멍반지름=0.215R → 화면 반지름에 대한 비율로 환산.
    private const float CylSize = 452f;
    private const float OrbitRatio = 0.58875f;   // 188.4 / 320
    private const float HoleRatio = 0.21097f;    //  67.5 / 320

    /// <summary>
    /// 화면 오른쪽 끝에서 실린더 <b>중심</b>까지의 거리.
    ///
    /// 예전에는 60이라 뒷판과 약실이 화면 밖으로 잘려 나갔는데, 반원으로 의도한 것처럼 보이지 않고
    /// 그냥 화면을 넘친 것처럼 읽혔다. 지금은 <b>아무것도 잘리지 않는</b> 값으로 잡고,
    /// 오른쪽으로 돌아가는 약실은 잘리는 대신 알파로 사라진다(<see cref="RevolverCylinderUI"/>).
    ///
    /// 150이면 가운데 큰 슬롯(반지름 95)이 오른쪽 끝에서 55px 여유를 두고 완전히 들어오고,
    /// 약실(반지름 48)은 중심 기준 +102px 지점에서 알파 0이 되어 경계에 닿기 전에 사라진다.
    /// </summary>
    private const float CylEdgeInset = 150f;

    /// <summary>약실 반지름(px). 페이드 경계를 계산하는 데 쓴다.</summary>
    private static float ChamberRadius => ChamberSize * 0.5f;

    private static float OrbitRadius => CylSize * 0.5f * OrbitRatio;
    private static float ChamberSize => CylSize * HoleRatio;   // 지름

    [MenuItem("Tools/Ricochet/Build Stage HUD Prefab")]
    public static void Build()
    {
        var root = Compose();
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log($"[StageHudPrefabBuilder] 프리팹 생성 완료 → {PrefabPath}");
    }

    [MenuItem("Tools/Ricochet/Build Stage HUD Prefab + Place In Stage Scenes")]
    public static void BuildAndPlace()
    {
        Build();
        PlaceInScenes();
    }

    /// <summary>Stage1~5 씬에 HUD 프리팹 인스턴스를 하나씩 넣는다(이미 있으면 교체).</summary>
    public static void PlaceInScenes()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[StageHudPrefabBuilder] 프리팹을 찾지 못했습니다."); return; }

        for (int i = 1; i <= 5; i++)
        {
            string scenePath = $"Assets/Scenes/Stage{i}.unity";
            if (!File.Exists(scenePath)) continue;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 이전 인스턴스를 걷어내고(중복 HUD 방지) 새로 넣는다.
            foreach (var existing in Object.FindObjectsOfType<StageHud>(true))
                Object.DestroyImmediate(existing.gameObject);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.name = "StageHud";

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[StageHudPrefabBuilder] {scenePath} 에 HUD 배치 완료");
        }
    }

    // ═══════════════════════════ 조립 ═══════════════════════════

    private static GameObject Compose()
    {
        var rootGO = new GameObject("StageHud",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(StageHud));

        var canvas = rootGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = rootGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefW, RefH);
        scaler.matchWidthOrHeight = 0f;   // 폭 기준(세로 화면 고정)

        var root = (RectTransform)rootGO.transform;

        // Canvas 직속에는 배경(암막)만 둔다 — 나머지는 전부 SafeArea 아래로.
        var topScrim = Img("Scrim_Top", root, S("Grad_Down"), new Color(0f, 0f, 0f, 0.5f));
        Place(topScrim.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 520f));
        topScrim.rectTransform.anchorMin = new Vector2(0f, 1f);
        topScrim.rectTransform.anchorMax = new Vector2(1f, 1f);

        var bottomScrim = Img("Scrim_Bottom", root, S("Grad_Down"), new Color(0f, 0f, 0f, 0.55f));
        Place(bottomScrim.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 560f));
        bottomScrim.rectTransform.anchorMin = new Vector2(0f, 0f);
        bottomScrim.rectTransform.anchorMax = new Vector2(1f, 0f);
        bottomScrim.rectTransform.localScale = new Vector3(1f, -1f, 1f);   // 그라데이션 뒤집기

        var safe = Rect("SafeArea", root);
        Stretch(safe);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        var hud = rootGO.GetComponent<StageHud>();
        var so = new SerializedObject(hud);
        so.FindProperty("_canvas").objectReferenceValue = canvas;
        so.FindProperty("_group").objectReferenceValue = rootGO.GetComponent<CanvasGroup>();

        BuildHealth(safe, so);
        BuildGold(safe, so);
        BuildCombo(safe, so);
        BuildTopRightButtons(safe, so);
        BuildMovePad(safe, so);
        BuildQuickBar(safe, so);
        BuildCylinder(safe, so);
        BuildHint(safe, so);

        so.ApplyModifiedPropertiesWithoutUndo();
        return rootGO;
    }

    // ───────────────────────── 좌상단 체력 ─────────────────────────

    private static void BuildHealth(RectTransform parent, SerializedObject so)
    {
        var panel = Rect("HPPanel", parent);
        Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -Margin), new Vector2(560f, 118f));
        Decorate(panel, UITheme.Cyan);

        var label = Text("Label", panel, "체력", 22f, UITheme.TextMid, UITheme.Medium, TextAlignmentOptions.Left);
        Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -16f), new Vector2(200f, 30f));

        var value = Text("Value", panel, "100 / 100", 27f, UITheme.TextHi, UITheme.Bold, TextAlignmentOptions.Right);
        Place(value.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-26f, -13f), new Vector2(280f, 34f));

        var track = Img("Track", panel, S("Bar_Track"), UITheme.Hex("#04080E").A(0.95f), sliced: true);
        Place(track.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(26f, 22f), new Vector2(508f, 28f));

        // 게이지는 anchorMax.x만 움직여 채운다. 그래서 여백은 안쪽 컨테이너가 대신 만든다
        // (채움 이미지 자체에 offset을 주면 애니메이션이 매 프레임 0으로 되돌려버린다).
        var inner = Rect("Inner", track.rectTransform);
        Stretch(inner, 3f, 3f, 3f, 3f);

        var fill = Img("Fill", inner, S("Bar_Fill"), UITheme.Cyan, sliced: true);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        so.FindProperty("_hpPanel").objectReferenceValue = panel;
        so.FindProperty("_hpFill").objectReferenceValue = fill.rectTransform;
        so.FindProperty("_hpFillImage").objectReferenceValue = fill;
        so.FindProperty("_hpValue").objectReferenceValue = value;
    }

    // ───────────────────────── 좌상단 골드 / 진행 ─────────────────────────

    private static void BuildGold(RectTransform parent, SerializedObject so)
    {
        var panel = Rect("GoldPanel", parent);
        Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin, -174f), new Vector2(330f, 84f));
        Decorate(panel, UITheme.Gold);

        var coin = Img("Coin", panel, S("Icon_Coin"), UITheme.Gold);
        Place(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(46f, 46f));

        var value = Text("Value", panel, "0", 32f, UITheme.GoldText, UITheme.Bold, TextAlignmentOptions.Left);
        Place(value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(82f, 0f), new Vector2(230f, 46f));

        var stage = Text("StageInfo", parent, "STAGE 1   ·   적 0", 21f, UITheme.TextLo, UITheme.Medium, TextAlignmentOptions.Left);
        Place(stage.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Margin + 6f, -272f), new Vector2(460f, 30f));

        so.FindProperty("_goldPanel").objectReferenceValue = panel;
        so.FindProperty("_goldValue").objectReferenceValue = value;
        so.FindProperty("_stageInfo").objectReferenceValue = stage;
    }

    // ───────────────────────── 상단 중앙 콤보(배경 없음) ─────────────────────────

    private static void BuildCombo(RectTransform parent, SerializedObject so)
    {
        var comboRoot = Rect("Combo", parent);
        // 좌상단 체력/골드 블록 아래로 내려 겹치지 않게 한다.
        Place(comboRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(640f, 210f));
        var group = comboRoot.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var value = Text("Value", comboRoot, "0", 130f, UITheme.Cyan, UITheme.Bold, TextAlignmentOptions.Center);
        Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(640f, 150f));
        // 배경 패널이 없으므로 외곽선으로 가독성을 확보한다(밝은 맵 위에서도 읽히게).
        value.outlineWidth = 0.22f;
        value.outlineColor = new Color32(4, 8, 14, 235);

        var label = Text("Label", comboRoot, "C O M B O", 34f, UITheme.TextMid, UITheme.Bold, TextAlignmentOptions.Center);
        Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -138f), new Vector2(640f, 44f));
        label.outlineWidth = 0.2f;
        label.outlineColor = new Color32(4, 8, 14, 220);

        so.FindProperty("_comboValue").objectReferenceValue = value;
        so.FindProperty("_comboLabel").objectReferenceValue = label;
        so.FindProperty("_comboGroup").objectReferenceValue = group;
    }

    // ───────────────────────── 우상단 아이콘 버튼 ─────────────────────────

    private static void BuildTopRightButtons(RectTransform parent, SerializedObject so)
    {
        var settings = CircleButton(parent, "SettingsButton", S("Icon_Gear"), UITheme.Cyan,
            new Vector2(-Margin, -Margin), 116f, 56f);
        var inventory = CircleButton(parent, "InventoryButton", S("Icon_Grid"), UITheme.Cyan,
            new Vector2(-Margin, -172f), 116f, 52f);

        so.FindProperty("_settingsButton").objectReferenceValue = settings;
        so.FindProperty("_inventoryButton").objectReferenceValue = inventory;
    }

    private static Button CircleButton(RectTransform parent, string name, Sprite icon, Color accent,
                                       Vector2 pos, float size, float iconSize)
    {
        var rt = Rect(name, parent);
        Place(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), pos, new Vector2(size, size));

        var glow = Img("Glow", rt, S("Glow_Radial"), accent.A(0.22f));
        Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 1.7f, size * 1.7f));

        var fill = Img("Fill", rt, S("Circle_Fill"), UITheme.PanelBg.A(0.92f));
        Stretch(fill.rectTransform);
        fill.raycastTarget = true;   // 버튼이 눌림을 받으려면 레이캐스트 대상이 하나는 있어야 한다.

        var stroke = Img("Stroke", rt, S("Circle_Stroke"), accent.A(0.55f));
        Stretch(stroke.rectTransform);

        var glyph = Img("Icon", rt, icon, UITheme.TextHi);
        Place(glyph.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(iconSize, iconSize));

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        rt.gameObject.AddComponent<UIButtonMotion>();
        return button;
    }

    // ───────────────────────── 좌하단 이동 버튼 ─────────────────────────

    private static void BuildMovePad(RectTransform parent, SerializedObject so)
    {
        var pad = Rect("MovePad", parent);
        Place(pad, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Margin + 8f, 72f), new Vector2(368f, 160f));

        var left = MoveButton(pad, "MoveLeft", new Vector2(0f, 0f), flip: false);
        var right = MoveButton(pad, "MoveRight", new Vector2(208f, 0f), flip: true);

        so.FindProperty("_moveLeft").objectReferenceValue = left;
        so.FindProperty("_moveRight").objectReferenceValue = right;
    }

    private static HoldButton MoveButton(RectTransform parent, string name, Vector2 pos, bool flip)
    {
        const float size = 160f;
        var rt = Rect(name, parent);
        Place(rt, new Vector2(0f, 0f), new Vector2(0f, 0f), pos, new Vector2(size, size));

        var fill = Img("Fill", rt, S("Circle_Fill"), UITheme.PanelBg.A(0.86f));
        Stretch(fill.rectTransform);
        fill.raycastTarget = true;

        var stroke = Img("Stroke", rt, S("Circle_Stroke"), UITheme.Cyan.A(0.45f));
        Stretch(stroke.rectTransform);

        var chevron = Img("Chevron", rt, S("Chevron"), UITheme.TextHi);
        Place(chevron.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
        // 오른쪽 버튼은 같은 꺾쇠를 좌우로 뒤집어 쓴다.
        if (flip) chevron.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

        var hold = rt.gameObject.AddComponent<HoldButton>();
        var hso = new SerializedObject(hold);
        hso.FindProperty("_highlight").objectReferenceValue = fill;
        hso.FindProperty("_scaleTarget").objectReferenceValue = rt;
        hso.ApplyModifiedPropertiesWithoutUndo();
        return hold;
    }

    // ───────────────────────── 우하단 퀵슬롯 ─────────────────────────

    private static void BuildQuickBar(RectTransform parent, SerializedObject so)
    {
        const float slot = 148f, gap = 18f;
        const int count = 3;
        float width = count * slot + (count - 1) * gap;

        var bar = Rect("QuickBar", parent);
        Place(bar, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Margin, 72f), new Vector2(width, slot));

        var views = new List<QuickSlotView>();
        for (int i = 0; i < count; i++)
            views.Add(QuickSlot(bar, i, new Vector2(i * (slot + gap), 0f), slot));

        var prop = so.FindProperty("_quickSlots");
        prop.arraySize = views.Count;
        for (int i = 0; i < views.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = views[i];

        so.FindProperty("_fallbackItemIcon").objectReferenceValue = S("Bullet_Icon");
    }

    private static QuickSlotView QuickSlot(RectTransform parent, int index, Vector2 pos, float size)
    {
        var rt = Rect($"Slot_{index}", parent);
        Place(rt, new Vector2(0f, 0f), new Vector2(0f, 0f), pos, new Vector2(size, size));

        var fill = Img("Fill", rt, S("Slot_Fill"), UITheme.PanelRaised.A(0.92f), sliced: true);
        Stretch(fill.rectTransform);
        fill.raycastTarget = true;

        var stroke = Img("Stroke", rt, S("Slot_Stroke"), UITheme.Stroke, sliced: true);
        Stretch(stroke.rectTransform);

        var icon = Img("Icon", rt, S("Bullet_Icon"), UITheme.TextHi);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(78f, 78f));

        var count = Text("Count", rt, "0", 24f, UITheme.TextHi, UITheme.Bold, TextAlignmentOptions.BottomRight);
        Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 8f), new Vector2(70f, 30f));

        var hotkey = Text("Hotkey", rt, (index + 1).ToString(), 20f, UITheme.TextLo, UITheme.Medium, TextAlignmentOptions.TopLeft);
        Place(hotkey.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(44f, 28f));

        var group = rt.gameObject.AddComponent<CanvasGroup>();
        var view = rt.gameObject.AddComponent<QuickSlotView>();
        var vso = new SerializedObject(view);
        vso.FindProperty("_frame").objectReferenceValue = stroke;
        vso.FindProperty("_icon").objectReferenceValue = icon;
        vso.FindProperty("_count").objectReferenceValue = count;
        vso.FindProperty("_hotkey").objectReferenceValue = hotkey;
        vso.FindProperty("_group").objectReferenceValue = group;
        vso.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ───────────────────────── 우하단 리볼버 실린더 ─────────────────────────

    private static void BuildCylinder(RectTransform parent, SerializedObject so)
    {
        // 화면 중앙 높이의 오른쪽 벽에 반쯤 걸쳐 붙인다.
        var root = Rect("Cylinder", parent);
        Place(root, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-CylEdgeInset, 0f), new Vector2(CylSize, CylSize));
        root.gameObject.AddComponent<RevolverCylinderUI>();

        // 드래그를 받아야 하므로 루트 전체가 레이캐스트 대상이어야 한다.
        // 눈에 보이지 않는 판을 깔되(알파 0) raycastTarget은 켜둔다.
        var catcher = Img("Catcher", root, S("Circle_Fill"), new Color(1f, 1f, 1f, 0f));
        Stretch(catcher.rectTransform);
        catcher.raycastTarget = true;

        // 글로우 반지름은 화면 오른쪽 끝을 넘지 않게 잡는다(넘으면 경계에서 딱 잘린 자국이 보인다).
        // 지름 = 여백(150)의 2배보다 살짝 큰 정도면 알파가 거의 0인 부분만 걸친다.
        var glow = Img("Glow", root, S("Glow_Radial"), UITheme.Cyan.A(0.34f));
        Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 320f));

        // 파선 링은 뺐다. 뒷판을 감추고 나니 실린더보다 큰 원은 화면 밖으로 잘려서
        // "넘쳤다"는 인상만 남기고, 회전 가능하다는 신호는 약실 자체가 이미 주고 있다.

        var rotor = Rect("Rotor", root);
        Stretch(rotor);

        // 금속 뒷판은 감춘다. 오른쪽 벽에 붙이면 원판이 잘려서 반원 의도가 아니라
        // 화면을 벗어난 것처럼 보였다. 약실과 가운데 슬롯만 남기면 벽에 붙은 다이얼로 읽힌다.
        // (스프라이트/오브젝트는 남겨둬서 나중에 다시 켜기만 하면 되도록 했다.)
        var plate = Img("Plate", rotor, S("Cyl_Plate"), new Color(0.80f, 0.88f, 0.95f, 0f));
        Stretch(plate.rectTransform);
        plate.enabled = false;

        var chambers = new List<CylinderChamberView>();
        for (int i = 0; i < StageHudArtGenerator.ChamberCount; i++)
            chambers.Add(Chamber(rotor, i));

        // 장전 위치(12시)를 가리키는 공이 표시.
        // 뒷판 테두리(반지름 221)와 12시 약실 윗변(181) 사이에 놓아, 파선 링(250)과 겹치지 않게 한다.
        var hammer = Img("Hammer", root, S("Cyl_Hammer"), UITheme.Gold);
        Place(hammer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(46f, 46f));

        // ── 가운데 큰 슬롯 ──
        var mainSlot = Rect("MainSlot", root);
        Place(mainSlot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190f, 190f));

        var mainRing = Img("Ring", mainSlot, S("Cyl_Main"), Color.white);
        Stretch(mainRing.rectTransform);

        var iconRect = Rect("IconRect", mainSlot);
        Place(iconRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(88f, 88f));
        iconRect.gameObject.AddComponent<CanvasGroup>();

        var mainIcon = Img("Icon", iconRect, S("Bullet_Icon"), UITheme.Cyan);
        Stretch(mainIcon.rectTransform);

        var mainCount = Text("Count", mainSlot, "×0", 26f, UITheme.TextHi, UITheme.Bold, TextAlignmentOptions.Center);
        Place(mainCount.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -56f), new Vector2(140f, 34f));

        var emptyLabel = Text("Empty", mainSlot, "탄환\n없음", 24f, UITheme.TextLo, UITheme.Medium, TextAlignmentOptions.Center);
        Place(emptyLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(160f, 90f));

        // 탄환 이름은 실린더 위쪽 빈 공간에 띄운다(뒷판을 가리지 않게).
        // 실린더 중심이 화면 밖으로 치우쳐 있어 가운데 정렬하면 이름도 잘리므로,
        // 오른쪽 정렬해 화면 끝에서 16px 안쪽에 맞춘다.
        var mainName = Text("Name", root, "", 27f, UITheme.TextHi, UITheme.Bold, TextAlignmentOptions.Right);
        Place(mainName.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 0f),
              new Vector2(CylEdgeInset - 16f, 14f), new Vector2(440f, 42f));
        mainName.outlineWidth = 0.2f;
        mainName.outlineColor = new Color32(4, 8, 14, 220);

        // ── 참조 바인딩 ──
        var cylinder = root.GetComponent<RevolverCylinderUI>();
        var cso = new SerializedObject(cylinder);
        cso.FindProperty("_root").objectReferenceValue = root;
        cso.FindProperty("_rotor").objectReferenceValue = rotor;
        cso.FindProperty("_glow").objectReferenceValue = glow;
        cso.FindProperty("_dashRing").objectReferenceValue = null;
        cso.FindProperty("_mainSlot").objectReferenceValue = mainSlot;
        cso.FindProperty("_mainRing").objectReferenceValue = mainRing;
        cso.FindProperty("_mainIconRect").objectReferenceValue = iconRect;
        cso.FindProperty("_mainIcon").objectReferenceValue = mainIcon;
        cso.FindProperty("_mainCount").objectReferenceValue = mainCount;
        cso.FindProperty("_mainName").objectReferenceValue = mainName;
        cso.FindProperty("_emptyLabel").objectReferenceValue = emptyLabel.gameObject;
        cso.FindProperty("_fallbackBulletIcon").objectReferenceValue = S("Bullet_Icon");
        cso.FindProperty("_orbitRadius").floatValue = OrbitRadius;
        // 약실이 화면 오른쪽 끝에 닿기 직전(중심 기준 +거리)에서 알파 0이 되도록 맞춘다.
        cso.FindProperty("_edgeVisibleX").floatValue = CylEdgeInset - ChamberRadius;
        cso.FindProperty("_edgeFadeWidth").floatValue = 70f;

        var chamberProp = cso.FindProperty("_chambers");
        chamberProp.arraySize = chambers.Count;
        for (int i = 0; i < chambers.Count; i++)
            chamberProp.GetArrayElementAtIndex(i).objectReferenceValue = chambers[i];

        cso.ApplyModifiedPropertiesWithoutUndo();

        so.FindProperty("_cylinder").objectReferenceValue = cylinder;
    }

    private static CylinderChamberView Chamber(RectTransform rotor, int index)
    {
        float size = ChamberSize;
        var rt = Rect($"Chamber_{index}", rotor);
        // 실제 좌표는 RevolverCylinderUI.LayoutChambers가 런타임에 다시 잡지만,
        // 에디터에서도 제자리에 보이도록 같은 규칙으로 미리 놓아둔다.
        float a = (90f + index * (360f / StageHudArtGenerator.ChamberCount)) * Mathf.Deg2Rad;
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
              new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * OrbitRadius, new Vector2(size, size));

        // 약실 전체(테두리 + 내용물)를 한 번에 흐리게 하는 그룹. 장전 표시용 Upright 그룹과
        // 중첩되어 알파가 곱해지므로, 가장자리 페이드와 장전 페이드가 서로 덮어쓰지 않는다.
        var group = rt.gameObject.AddComponent<CanvasGroup>();

        var ring = Img("Ring", rt, S("Cyl_Chamber"), Color.white);
        Stretch(ring.rectTransform);

        var upright = Rect("Upright", rt);
        Stretch(upright);
        upright.gameObject.AddComponent<CanvasGroup>();

        var icon = Img("Icon", upright, S("Bullet_Icon"), UITheme.Cyan);
        Place(icon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 8f), new Vector2(44f, 44f));

        var count = Text("Count", upright, "0", 18f, UITheme.TextHi, UITheme.Bold, TextAlignmentOptions.Center);
        Place(count.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(70f, 22f));

        var view = rt.gameObject.AddComponent<CylinderChamberView>();
        var vso = new SerializedObject(view);
        vso.FindProperty("_group").objectReferenceValue = group;
        vso.FindProperty("_ring").objectReferenceValue = ring;
        vso.FindProperty("_upright").objectReferenceValue = upright;
        vso.FindProperty("_icon").objectReferenceValue = icon;
        vso.FindProperty("_count").objectReferenceValue = count;
        vso.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // ───────────────────────── 모드 안내 ─────────────────────────

    private static void BuildHint(RectTransform parent, SerializedObject so)
    {
        var panel = Rect("HintPanel", parent);
        // 실린더(우측)와 겹치지 않도록 좌측, 이동 버튼 위에 놓는다.
        Place(panel, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Margin + 8f, 260f), new Vector2(520f, 76f));
        Decorate(panel, UITheme.Gold);

        var text = Text("Text", panel, "", 22f, UITheme.GoldText, UITheme.Medium, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 16f, 0f, 16f, 0f);

        panel.gameObject.SetActive(false);

        so.FindProperty("_hintPanel").objectReferenceValue = panel.gameObject;
        so.FindProperty("_hintText").objectReferenceValue = text;
    }

    // ═══════════════════════════ 위젯 헬퍼 ═══════════════════════════

    /// <summary>패널에 글로우 + 반투명 배경 + 네온 테두리 세 겹을 깔아준다.</summary>
    private static void Decorate(RectTransform panel, Color accent)
    {
        var glow = Img("Glow", panel, S("Panel_Glow"), accent.A(0.18f), sliced: true);
        Stretch(glow.rectTransform, -26f, -26f, -26f, -26f);

        var fill = Img("Fill", panel, S("Panel_Fill"), UITheme.PanelBg.A(0.90f), sliced: true);
        Stretch(fill.rectTransform);

        var stroke = Img("Stroke", panel, S("Panel_Stroke"), accent.A(0.5f), sliced: true);
        Stretch(stroke.rectTransform);
    }

    private static Sprite S(string name)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");
        if (sprite == null) Debug.LogError($"[StageHudPrefabBuilder] 스프라이트를 찾지 못했습니다: {name}. 먼저 'Generate Stage HUD Art'를 실행하세요.");
        return sprite;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Image Img(string name, Transform parent, Sprite sprite, Color color, bool sliced = false)
    {
        var rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        img.raycastTarget = false;   // 필요한 것만 개별적으로 켠다.
        return img;
    }

    private static TMP_Text Text(string name, Transform parent, string content, float size,
                                 Color color, TMP_FontAsset font, TextAlignmentOptions align)
    {
        var rt = Rect(name, parent);
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.color = color;
        if (font != null) tmp.font = font;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }

    private static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }
}
