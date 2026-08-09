using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스테이지 HUD 전용 스프라이트를 코드로 그려 PNG로 굽는 에디터 도구.
///
/// 유니티 기본 UI 스프라이트(모서리 둥근 흰 사각형)만으로는 완성된 화면이 나오지 않는데,
/// 외부 이미지 에셋을 들여올 수도 없어서 필요한 조각을 전부 여기서 절차적으로 그린다.
/// 렌더링은 SDF(부호 있는 거리 함수) 기반이라 슈퍼샘플링 없이도 가장자리가 깨끗하다.
///
/// 색은 대부분 흰색으로 굽고 런타임에 <see cref="UITheme"/> 색으로 틴트한다.
/// 금속 질감처럼 명암이 필요한 것만 RGB에 회색조 음영을 구워 넣는다(틴트하면 색 있는 금속이 된다).
///
/// 메뉴: Tools ▸ Ricochet ▸ Generate Stage HUD Art
/// </summary>
public static class StageHudArtGenerator
{
    private const string OutDir = "Assets/Sprites/UI/Generated";

    /// <summary>실린더 챔버 수. 플레이어가 고를 수 있는 탄환 종류 수와 맞춘다(PlayerShooter.MaxSelectableBullets).</summary>
    public const int ChamberCount = 5;

    [MenuItem("Tools/Ricochet/Generate Stage HUD Art")]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(OutDir);

        // ── 패널: 채움 / 테두리 / 글로우를 따로 구워 겹친다(틴트를 따로 먹이려고) ──
        Bake("Panel_Fill",    96, 96, PanelFill,    border: new Vector4(28, 28, 28, 28));
        Bake("Panel_Stroke",  96, 96, PanelStroke,  border: new Vector4(28, 28, 28, 28));
        Bake("Panel_Glow",   128,128, PanelGlow,    border: new Vector4(44, 44, 44, 44));

        // ── 게이지(알약 형태) ──
        Bake("Bar_Track", 64, 64, BarTrack, border: new Vector4(30, 30, 30, 30));
        Bake("Bar_Fill",  64, 64, BarFill,  border: new Vector4(30, 30, 30, 30));

        // ── 원형 버튼(설정/인벤/이동) ──
        Bake("Circle_Fill",   192, 192, CircleFill);
        Bake("Circle_Stroke", 192, 192, CircleStroke);
        Bake("Ring_Dashed",   256, 256, RingDashed);
        Bake("Glow_Radial",   256, 256, GlowRadial);

        // ── 리볼버 실린더 ──
        Bake("Cyl_Plate",   640, 640, CylPlate);     // 회전하는 실린더 뒷판(금속 질감 + 노치)
        Bake("Cyl_Main",    448, 448, CylMain);      // 중앙 큰 원(1번 슬롯)
        Bake("Cyl_Chamber", 192, 192, CylChamber);   // 위성 작은 원(대기 탄환)
        Bake("Cyl_Hammer",  128, 128, CylHammer);    // 장전 위치 표시(공이) 삼각형

        // ── 퀵슬롯(모서리 깎은 사각 프레임) ──
        Bake("Slot_Fill",   128, 128, SlotFill,   border: new Vector4(38, 38, 38, 38));
        Bake("Slot_Stroke", 128, 128, SlotStroke, border: new Vector4(38, 38, 38, 38));

        // ── 글리프 ──
        Bake("Chevron", 128, 128, Chevron);          // 좌우 이동 버튼(오른쪽은 X 스케일 -1)
        Bake("Bullet_Icon", 128, 128, BulletIcon);   // 탄환 아이콘 폴백
        Bake("Icon_Gear", 128, 128, IconGear);       // 설정
        Bake("Icon_Grid", 128, 128, IconGrid);       // 인벤토리
        Bake("Icon_Coin", 128, 128, IconCoin);       // 골드
        Bake("Grad_Down", 16, 256, GradDown);        // 상단 암막 그라데이션

        AssetDatabase.Refresh();
        Debug.Log($"[StageHudArtGenerator] 스프라이트 생성 완료 → {OutDir}");
    }

    // ═══════════════════════════ 도형 정의 ═══════════════════════════
    //
    // 좌표계: 텍스처 중심이 (0,0), 단위는 픽셀, +y가 위.
    // 각 함수는 "그 픽셀에서 도형 경계까지의 부호 있는 거리"를 돌려준다(음수=내부).

    private static void PanelFill(Ctx c)
    {
        // 살짝 위가 밝은 세로 그라데이션 — 평평한 단색보다 패널이 떠 보인다.
        float half = c.Half.x - 3f;
        c.Draw(p => SdRoundBox(p, new Vector2(half, c.Half.y - 3f), 20f),
               p => new Color(1f, 1f, 1f, Mathf.Lerp(0.86f, 1f, Norm01(p.y, -c.Half.y, c.Half.y))));
    }

    private static void PanelStroke(Ctx c)
    {
        var b = new Vector2(c.Half.x - 3f, c.Half.y - 3f);
        // 테두리 본체.
        c.Draw(p => Mathf.Abs(SdRoundBox(p, b, 20f)) - 1.6f, _ => Color.white);
        // 위쪽 절반만 한 번 더 밝게 — 빛이 위에서 오는 느낌.
        c.Draw(p => Mathf.Abs(SdRoundBox(p, b, 20f)) - 1.0f,
               p => new Color(1f, 1f, 1f, p.y > 0f ? 0.75f : 0f));
    }

    private static void PanelGlow(Ctx c)
    {
        // 패널 뒤에 깔아 네온이 번지는 느낌을 내는 소프트 글로우.
        var b = new Vector2(c.Half.x - 30f, c.Half.y - 30f);
        c.Draw(p =>
        {
            float d = SdRoundBox(p, b, 22f);
            return d - 26f;                                  // 넓게 퍼진 영역만 칠하고
        }, p =>
        {
            float d = Mathf.Max(0f, SdRoundBox(p, b, 22f));
            float a = Mathf.Pow(1f - Mathf.Clamp01(d / 26f), 2.2f);  // 바깥으로 갈수록 감쇠
            return new Color(1f, 1f, 1f, a * 0.85f);
        });
    }

    private static void BarTrack(Ctx c)
    {
        float r = c.Half.y - 2f;
        c.Draw(p => SdRoundBox(p, new Vector2(c.Half.x - 2f, r), r), _ => Color.white);
    }

    private static void BarFill(Ctx c)
    {
        float r = c.Half.y - 4f;
        // 위로 갈수록 밝아지는 하이라이트 — 유리/에너지 게이지 느낌.
        // 구간을 나누면 경계에 이음매가 보이므로 smoothstep 한 번으로 부드럽게 잇는다.
        c.Draw(p => SdRoundBox(p, new Vector2(c.Half.x - 4f, r), r),
               p =>
               {
                   float t = Norm01(p.y, -r, r);
                   return new Color(1f, 1f, 1f, Mathf.Lerp(0.70f, 1f, Mathf.SmoothStep(0f, 1f, t)));
               });
    }

    private static void CircleFill(Ctx c)
    {
        float r = c.Half.x - 4f;
        c.Draw(p => p.magnitude - r,
               p => new Color(1f, 1f, 1f, Mathf.Lerp(0.82f, 1f, Norm01(p.y, -r, r))));
    }

    private static void CircleStroke(Ctx c)
    {
        float r = c.Half.x - 4f;
        c.Draw(p => Mathf.Abs(p.magnitude - r) - 2.4f, _ => Color.white);
        c.Draw(p => Mathf.Abs(p.magnitude - r) - 1.4f,
               p => new Color(1f, 1f, 1f, p.y > 0f ? 0.8f : 0f));
    }

    private static void RingDashed(Ctx c)
    {
        // 실린더 바깥을 감싸는 파선 링(조작 가능하다는 신호).
        float r = c.Half.x - 8f;
        const int dashes = 24;
        c.Draw(p =>
        {
            float ring = Mathf.Abs(p.magnitude - r) - 2f;
            float ang = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            float seg = Mathf.Repeat(ang, 360f / dashes) / (360f / dashes);
            // 각 구간의 앞 55%만 남긴다.
            float gap = seg < 0.55f ? -1f : 1f;
            return Mathf.Max(ring, gap);
        }, _ => Color.white);
    }

    private static void GlowRadial(Ctx c)
    {
        float r = c.Half.x;
        c.Draw(p => p.magnitude - r,
               p => new Color(1f, 1f, 1f, Mathf.Pow(1f - Mathf.Clamp01(p.magnitude / r), 2.4f)));
    }

    // ─────────────────────── 리볼버 실린더 ───────────────────────

    private static void CylPlate(Ctx c)
    {
        float R = c.Half.x - 6f;

        // 1) 몸통: 중심이 밝고 가장자리로 갈수록 어두워지는 금속 원판.
        c.Draw(p => p.magnitude - R, p =>
        {
            float t = Mathf.Clamp01(p.magnitude / R);
            float v = Mathf.Lerp(0.42f, 0.16f, t * t);
            v += Brushed(p) * 0.05f;                                   // 방사형 헤어라인
            v += Mathf.Clamp01(Norm01(p.y, -R, R) - 0.5f) * 0.10f;     // 위쪽 광원
            return new Color(v, v, v, 0.97f);
        });

        // 2) 바깥 베벨 링: 위가 밝고 아래가 어두운 테두리.
        c.Draw(p => Mathf.Abs(p.magnitude - (R - 7f)) - 5f, p =>
        {
            float lit = Norm01(p.y, -R, R);
            float v = Mathf.Lerp(0.20f, 0.92f, lit);
            return new Color(v, v, v, 0.95f);
        });

        // 3) 널링(미끄럼 방지 톱니): 실린더가 "돌리는 물건"임을 알려준다.
        const int knurls = 60;
        for (int i = 0; i < knurls; i++)
        {
            float a = i * Mathf.PI * 2f / knurls;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 center = dir * (R - 2.5f);
            float deg = a * Mathf.Rad2Deg;
            c.Draw(p => SdRoundBox(Rot(p - center, -deg), new Vector2(5f, 2.6f), 1.2f),
                   _ => new Color(0.78f, 0.82f, 0.86f, 0.5f));
        }

        // 4) 챔버 구멍: 실제로 탄이 들어가는 자리. 안쪽은 깊게 어둡고 위쪽에 림 하이라이트.
        float orbit = R * 0.60f;
        float hole = R * 0.215f;
        for (int i = 0; i < ChamberCount; i++)
        {
            float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / ChamberCount; // 12시부터 시계 반대 방향
            Vector2 ctr = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * orbit;

            c.Draw(p => (p - ctr).magnitude - hole, p =>
            {
                float t = Mathf.Clamp01((p - ctr).magnitude / hole);
                float v = Mathf.Lerp(0.02f, 0.10f, t * t);
                return new Color(v, v, v, 1f);
            });
            c.Draw(p => Mathf.Abs((p - ctr).magnitude - hole) - 1.8f, p =>
            {
                float lit = Norm01((p - ctr).y, -hole, hole);
                float v = Mathf.Lerp(0.12f, 0.80f, lit);
                return new Color(v, v, v, 0.9f);
            });
        }

        // 5) 중앙 액슬 구멍(가운데 큰 원이 얹힐 자리를 비운다).
        float axle = R * 0.30f;
        c.Draw(p => p.magnitude - axle, p =>
        {
            float v = Mathf.Lerp(0.06f, 0.14f, Mathf.Clamp01(p.magnitude / axle));
            return new Color(v, v, v, 1f);
        });
    }

    private static void CylMain(Ctx c)
    {
        float R = c.Half.x - 4f;

        // 바깥 두꺼운 금속 링(베벨).
        c.Draw(p => p.magnitude - R, p =>
        {
            float lit = Norm01(p.y, -R, R);
            float v = Mathf.Lerp(0.18f, 0.74f, lit) + Brushed(p) * 0.05f;
            return new Color(v, v, v, 1f);
        });
        // 링 안쪽 그림자 홈.
        c.Draw(p => Mathf.Abs(p.magnitude - R * 0.80f) - R * 0.045f,
               _ => new Color(0.04f, 0.05f, 0.06f, 0.85f));

        // 총구 쪽 깊은 챔버(탄환 아이콘이 얹히는 곳).
        float inner = R * 0.72f;
        c.Draw(p => p.magnitude - inner, p =>
        {
            float t = Mathf.Clamp01(p.magnitude / inner);
            float v = Mathf.Lerp(0.05f, 0.13f, t * t * t);
            return new Color(v, v, v, 1f);
        });
        // 챔버 림 하이라이트(위가 밝게).
        c.Draw(p => Mathf.Abs(p.magnitude - inner) - 2.2f, p =>
        {
            float lit = Norm01(p.y, -inner, inner);
            return new Color(Mathf.Lerp(0.10f, 0.95f, lit), Mathf.Lerp(0.10f, 0.95f, lit), Mathf.Lerp(0.12f, 1f, lit), 0.95f);
        });
        // 강선(rifling) 홈 — 안쪽 벽에 얕게.
        const int grooves = 16;
        for (int i = 0; i < grooves; i++)
        {
            float a = i * Mathf.PI * 2f / grooves;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector2 ctr = dir * (inner * 0.88f);
            c.Draw(p => SdRoundBox(Rot(p - ctr, -a * Mathf.Rad2Deg), new Vector2(inner * 0.08f, 1.6f), 1f),
                   _ => new Color(0.5f, 0.56f, 0.6f, 0.22f));
        }
    }

    private static void CylChamber(Ctx c)
    {
        float R = c.Half.x - 3f;
        c.Draw(p => p.magnitude - R, p =>
        {
            float lit = Norm01(p.y, -R, R);
            float v = Mathf.Lerp(0.16f, 0.62f, lit);
            return new Color(v, v, v, 1f);
        });
        float inner = R * 0.70f;
        c.Draw(p => p.magnitude - inner, p =>
        {
            float t = Mathf.Clamp01(p.magnitude / inner);
            return new Color(Mathf.Lerp(0.05f, 0.12f, t * t), Mathf.Lerp(0.05f, 0.12f, t * t), Mathf.Lerp(0.06f, 0.14f, t * t), 1f);
        });
        c.Draw(p => Mathf.Abs(p.magnitude - inner) - 1.6f, p =>
        {
            float lit = Norm01(p.y, -inner, inner);
            float v = Mathf.Lerp(0.10f, 0.88f, lit);
            return new Color(v, v, v, 0.9f);
        });
    }

    private static void CylHammer(Ctx c)
    {
        // 장전 위치(12시)를 가리키는 아래쪽 방향 삼각형 + 짧은 축.
        // 축은 삼각형 밑변(y = -6)까지 내려와 붙어야 한 덩어리로 보인다.
        c.Draw(p => SdTriangle(p + new Vector2(0f, 6f), c.Half.x * 0.52f, -c.Half.y * 0.60f), _ => Color.white);
        c.Draw(p => SdRoundBox(p - new Vector2(0f, c.Half.y * 0.20f), new Vector2(c.Half.x * 0.13f, c.Half.y * 0.32f), 3f),
               _ => Color.white);
    }

    // ─────────────────────── 퀵슬롯 / 글리프 ───────────────────────

    /// <summary>모서리를 45°로 깎은 사각형(테크 느낌). 9-slice가 되도록 네 귀퉁이가 대칭이다.</summary>
    private static float SdChamferBox(Vector2 p, Vector2 b, float cut)
    {
        float box = SdRoundBox(p, b, 3f);
        // |x|+|y| 로 마름모를 만들어 귀퉁이를 잘라낸다.
        float diamond = Mathf.Abs(p.x) + Mathf.Abs(p.y) - (b.x + b.y - cut);
        return Mathf.Max(box, diamond);
    }

    private static void SlotFill(Ctx c)
    {
        var b = new Vector2(c.Half.x - 4f, c.Half.y - 4f);
        c.Draw(p => SdChamferBox(p, b, 26f),
               p => new Color(1f, 1f, 1f, Mathf.Lerp(0.82f, 1f, Norm01(p.y, -b.y, b.y))));
    }

    private static void SlotStroke(Ctx c)
    {
        var b = new Vector2(c.Half.x - 4f, c.Half.y - 4f);
        c.Draw(p => Mathf.Abs(SdChamferBox(p, b, 26f)) - 1.8f, _ => Color.white);
    }

    private static void Chevron(Ctx c)
    {
        // 두꺼운 '<' 꺾쇠 두 획. 오른쪽 버튼은 스케일 X=-1로 뒤집어 쓴다.
        float len = c.Half.x * 0.72f;
        float w = 8.5f;
        c.Draw(p =>
        {
            float a = SdSegment(p, new Vector2(len * 0.55f, len), new Vector2(-len * 0.35f, 0f)) - w;
            float bq = SdSegment(p, new Vector2(-len * 0.35f, 0f), new Vector2(len * 0.55f, -len)) - w;
            return Mathf.Min(a, bq);
        }, _ => Color.white);
    }

    private static void BulletIcon(Ctx c)
    {
        // 탄피(아래 원통) + 오자이브 탄두(위 뾰족한 아치).
        float hw = c.Half.x * 0.30f;
        float baseTop = -c.Half.y * 0.02f;

        // 탄피
        c.Draw(p => SdRoundBox(p - new Vector2(0f, -c.Half.y * 0.44f), new Vector2(hw, c.Half.y * 0.42f), 5f),
               p => new Color(1f, 1f, 1f, Mathf.Lerp(0.72f, 1f, Norm01(p.x, -hw, hw))));
        // 림(탄피 바닥 테두리)
        c.Draw(p => SdRoundBox(p - new Vector2(0f, -c.Half.y * 0.80f), new Vector2(hw * 1.18f, 4.5f), 2f),
               _ => new Color(1f, 1f, 1f, 0.95f));
        // 탄두: 큰 원 두 개의 교집합 = 오자이브
        float Rg = c.Half.y * 0.95f;
        c.Draw(p =>
        {
            Vector2 q = p - new Vector2(0f, baseTop);
            if (q.y < 0f) return 1f;
            float d = Mathf.Max((q - new Vector2(-Rg + hw, -Rg * 0.34f)).magnitude - Rg,
                                (q - new Vector2(Rg - hw, -Rg * 0.34f)).magnitude - Rg);
            return Mathf.Max(d, -q.y);
        }, p => new Color(1f, 1f, 1f, Mathf.Lerp(0.80f, 1f, Norm01(p.x, -hw, hw))));
    }

    private static void IconGear(Ctx c)
    {
        float R = c.Half.x * 0.56f;
        const int teeth = 8;

        // 톱니: 본체 둘레에 모서리 둥근 사각형을 방사형으로 붙인다.
        for (int i = 0; i < teeth; i++)
        {
            float deg = i * 360f / teeth;
            Vector2 ctr = Rot(new Vector2(0f, R * 1.06f), -deg);
            c.Draw(p => SdRoundBox(Rot(p - ctr, deg), new Vector2(R * 0.26f, R * 0.30f), R * 0.09f),
                   _ => Color.white);
        }
        c.Draw(p => p.magnitude - R, _ => Color.white);
        // 가운데 축 구멍을 뚫는다(뒤에 깔린 것이 비쳐 보이도록 알파 0으로 덮어쓰지 않고 지운다).
        c.Erase(p => p.magnitude - R * 0.36f);
    }

    private static void IconGrid(Ctx c)
    {
        // 3x3 격자 — 작은 크기에서도 "보관함"으로 바로 읽힌다.
        float cell = c.Half.x * 0.30f;
        float gap = c.Half.x * 0.10f;
        float pitch = cell + gap;
        for (int gy = -1; gy <= 1; gy++)
        for (int gx = -1; gx <= 1; gx++)
        {
            Vector2 ctr = new Vector2(gx * pitch, gy * pitch);
            c.Draw(p => SdRoundBox(p - ctr, new Vector2(cell * 0.5f, cell * 0.5f), cell * 0.18f),
                   _ => Color.white);
        }
    }

    private static void IconCoin(Ctx c)
    {
        float R = c.Half.x * 0.72f;
        c.Draw(p => p.magnitude - R, p => new Color(1f, 1f, 1f, Mathf.Lerp(0.78f, 1f, Norm01(p.y, -R, R))));
        // 안쪽 테두리 홈 + 마름모 각인 — 밋밋한 동그라미가 아니라 주화로 보이게.
        // (십자 각인은 회복 아이템처럼 읽혀서 마름모로 바꿨다.)
        c.Draw(p => Mathf.Abs(p.magnitude - R * 0.72f) - 2.2f, _ => new Color(0.35f, 0.28f, 0.10f, 0.55f));
        c.Draw(p => SdRoundBox(Rot(p, 45f), new Vector2(R * 0.26f, R * 0.26f), R * 0.07f),
               _ => new Color(0.35f, 0.28f, 0.10f, 0.75f));
    }

    private static void GradDown(Ctx c)
    {
        c.Draw(_ => -1f, p => new Color(1f, 1f, 1f, Mathf.Pow(Norm01(p.y, -c.Half.y, c.Half.y), 1.6f)));
    }

    // ═══════════════════════════ SDF / 유틸 ═══════════════════════════

    private static float SdRoundBox(Vector2 p, Vector2 b, float r)
    {
        Vector2 q = new Vector2(Mathf.Abs(p.x) - b.x + r, Mathf.Abs(p.y) - b.y + r);
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
    }

    private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 pa = p - a, ba = b - a;
        float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
        return (pa - ba * h).magnitude;
    }

    /// <summary>밑변 폭 2*halfWidth, 꼭짓점이 (0, height)인 이등변 삼각형(height가 음수면 아래를 향한다).</summary>
    private static float SdTriangle(Vector2 p, float halfWidth, float height)
    {
        if (height < 0f) { p.y = -p.y; height = -height; }
        float side = Mathf.Abs(p.x) * height + p.y * halfWidth - halfWidth * height;
        side /= Mathf.Sqrt(height * height + halfWidth * halfWidth);
        return Mathf.Max(side, -p.y);
    }

    private static Vector2 Rot(Vector2 p, float deg)
    {
        float a = deg * Mathf.Deg2Rad, s = Mathf.Sin(a), co = Mathf.Cos(a);
        return new Vector2(p.x * co - p.y * s, p.x * s + p.y * co);
    }

    /// <summary>각도에 따라 흔들리는 값. 금속의 방사형 헤어라인(브러시드) 질감을 흉내낸다.</summary>
    private static float Brushed(Vector2 p)
    {
        float a = Mathf.Atan2(p.y, p.x);
        return Mathf.Sin(a * 37f) * 0.5f + Mathf.Sin(a * 91f + 1.7f) * 0.3f + Mathf.Sin(a * 173f + 4.1f) * 0.2f;
    }

    private static float Norm01(float v, float min, float max) => Mathf.Clamp01((v - min) / (max - min));

    // ═══════════════════════════ 래스터라이저 ═══════════════════════════

    /// <summary>한 장의 그리기 대상. <see cref="Draw"/>를 여러 번 불러 레이어를 위에 쌓는다.</summary>
    private class Ctx
    {
        public readonly int W, H;
        public readonly Vector2 Half;
        private readonly Color[] _buf;

        public Ctx(int w, int h)
        {
            W = w; H = h;
            Half = new Vector2(w * 0.5f, h * 0.5f);
            _buf = new Color[w * h];
            for (int i = 0; i < _buf.Length; i++) _buf[i] = new Color(0f, 0f, 0f, 0f);
        }

        /// <summary>
        /// <paramref name="sdf"/>가 음수인 영역을 <paramref name="tint"/> 색으로 알파 합성한다.
        /// 경계 1px 구간에서 커버리지를 선형 보간해 안티에일리어싱을 얻는다.
        /// </summary>
        public void Draw(Func<Vector2, float> sdf, Func<Vector2, Color> tint)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - Half.x, y + 0.5f - Half.y);
                    float cov = Mathf.Clamp01(0.5f - sdf(p));
                    if (cov <= 0f) continue;

                    Color src = tint(p);
                    float sa = src.a * cov;
                    if (sa <= 0f) continue;

                    int i = y * W + x;
                    Color dst = _buf[i];
                    float outA = sa + dst.a * (1f - sa);
                    if (outA <= 0f) { _buf[i] = new Color(0f, 0f, 0f, 0f); continue; }

                    // 스트레이트 알파 over 합성.
                    _buf[i] = new Color(
                        (src.r * sa + dst.r * dst.a * (1f - sa)) / outA,
                        (src.g * sa + dst.g * dst.a * (1f - sa)) / outA,
                        (src.b * sa + dst.b * dst.a * (1f - sa)) / outA,
                        outA);
                }
            }
        }

        /// <summary><paramref name="sdf"/>가 음수인 영역의 알파를 깎아낸다(구멍 뚫기).</summary>
        public void Erase(Func<Vector2, float> sdf)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f - Half.x, y + 0.5f - Half.y);
                    float cov = Mathf.Clamp01(0.5f - sdf(p));
                    if (cov <= 0f) continue;

                    int i = y * W + x;
                    Color d = _buf[i];
                    _buf[i] = new Color(d.r, d.g, d.b, d.a * (1f - cov));
                }
            }
        }

        public Color[] Pixels => _buf;
    }

    private static void Bake(string name, int w, int h, Action<Ctx> draw, Vector4 border = default)
    {
        var ctx = new Ctx(w, h);
        draw(ctx);

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
        tex.SetPixels(ctx.Pixels);
        tex.Apply();

        string path = $"{OutDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) return;

        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spriteBorder = border;
        imp.spritePixelsPerUnit = 100f;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.filterMode = FilterMode.Bilinear;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.npotScale = TextureImporterNPOTScale.None;

        var settings = imp.GetDefaultPlatformTextureSettings();
        settings.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SetPlatformTextureSettings(settings);

        imp.SaveAndReimport();
    }
}
