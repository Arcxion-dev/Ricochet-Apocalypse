using UnityEngine;
using TMPro;

/// <summary>
/// 프로젝트 UI의 단일 디자인 소스(다크 포스트아포칼립스 + 네온).
/// 색/폰트/간격을 여기 한 곳에서 관리한다. 프리팹 빌더와 런타임 UI 컨트롤러가 공유한다.
///
/// - 팔레트: Figma 목업(fileKey y3y1vay9lMHQC2JLnH9g2q)에서 확정한 값과 동일.
/// - 폰트: Noto Sans KR SDF(Dynamic) — 한글/영문 공용. 헤딩은 Bold, 본문은 Regular/Medium.
///   (SDF 에셋은 Resources/Font/ImportFont/ 에 위치)
/// </summary>
public static class UITheme
{
    // ───────────────────────── 색상 ─────────────────────────
    public static readonly Color Bg0        = Hex("#0A0E16"); // 최심부 배경
    public static readonly Color PanelBg     = Hex("#0B121C"); // 패널 배경(반투명 사용 권장)
    public static readonly Color PanelRaised = Hex("#101A26"); // 살짝 뜬 카드/행
    public static readonly Color Stroke       = Hex("#22384B"); // 패널 테두리
    public static readonly Color StrokeDim    = Hex("#233448"); // 카드 테두리(약)

    public static readonly Color Cyan      = Hex("#35F0FF"); // 주 강조(네온 시안)
    public static readonly Color CyanDeep  = Hex("#24E0FF"); // 시안 글로우
    public static readonly Color Magenta   = Hex("#FF4FB8"); // 보조 강조(콤보/복합)
    public static readonly Color MagentaDeep= Hex("#FF3DAE");
    public static readonly Color Gold      = Hex("#FFC24B"); // 골드/재화
    public static readonly Color GoldText  = Hex("#FFD873");

    public static readonly Color TextHi  = Hex("#EAF6FB"); // 본문 밝은
    public static readonly Color TextMid = Hex("#8FA9BC"); // 보조
    public static readonly Color TextLo  = Hex("#5C7488"); // 캡션/라벨
    public static readonly Color Danger  = Hex("#FF4D5E");
    public static readonly Color Success = Hex("#46E39B");

    // 원소/탄환 속성 색
    public static readonly Color Fire      = Hex("#FF6A3D");
    public static readonly Color Ice       = Hex("#6FE9FF");
    public static readonly Color Electric  = Hex("#FFD84B");
    public static readonly Color Explosive = Hex("#FF9F45");
    public static readonly Color Arcane    = Hex("#B07BFF"); // 파츠/희귀
    public static readonly Color Poison    = Hex("#46E39B");

    // ───────────────────────── 폰트 크기 ─────────────────────────
    public const float TitleLogo = 150f;
    public const float H1 = 60f;
    public const float H2 = 34f;
    public const float H3 = 24f;
    public const float Body = 18f;
    public const float Small = 15f;
    public const float Caption = 13f;

    // ───────────────────────── 간격(8px 그리드) ─────────────────────────
    public const float S1 = 8f, S2 = 16f, S3 = 24f, S4 = 32f, S5 = 48f;
    public const float RefWidth = 1920f, RefHeight = 1080f;

    // ───────────────────────── 폰트 에셋 ─────────────────────────
    static TMP_FontAsset _bold, _medium, _regular;
    public static TMP_FontAsset Bold    => _bold    ??= Load("NotoSansKR-Bold SDF");
    public static TMP_FontAsset Medium  => _medium  ??= Load("NotoSansKR-Medium SDF");
    public static TMP_FontAsset Regular => _regular ??= Load("NotoSansKR-Regular SDF");

    static TMP_FontAsset Load(string name)
    {
        var fa = Resources.Load<TMP_FontAsset>("Font/ImportFont/" + name);
        if (fa == null) Debug.LogWarning($"[UITheme] 폰트 에셋을 찾지 못함: {name}");
        return fa;
    }

    // ───────────────────────── 헬퍼 ─────────────────────────
    public static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    /// <summary>알파를 바꾼 색 사본.</summary>
    public static Color A(this Color c, float a) => new Color(c.r, c.g, c.b, a);
}
