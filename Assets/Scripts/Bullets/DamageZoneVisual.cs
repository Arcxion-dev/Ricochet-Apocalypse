using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 장판(<see cref="DamageZone"/>)의 비주얼을 파티클 시스템으로 구성하는 컴포넌트.
///
/// 예전에는 Circle 스프라이트 프리팹을 하나 깔아두는 방식이었는데, 프리팹 스케일과
/// 실제 판정 반경(<see cref="DamageZone"/>의 CircleCollider2D radius)이 전혀 연동되지 않아
/// "보이는 크기 != 실제 맞는 크기" 문제가 있었다. 여기서는 두 파티클 시스템을 런타임에 만들어
/// 반경 값 하나로 둘 다 크기를 맞춘다.
///
/// - Fill  : 원판 내부 전체에서 피어오르는 안개/불꽃. 장판이 "여기부터 여기까지"임을 채워서 보여준다.
///           방출량을 넓이(r²)에 비례시켜 반경이 바뀌어도 밀도가 일정하다.
/// - Rim   : 원의 테두리에서만 태어나는 작고 밝은 입자. 판정 경계선을 정확히 그린다.
///           방출량을 둘레(r)에 비례시켜 반경이 바뀌어도 선 굵기 느낌이 일정하다.
///
/// 프리팹 제작이 필요 없도록 머티리얼/텍스처까지 코드에서 생성해 공유한다.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class DamageZoneVisual : MonoBehaviour
{
    /// <summary>배경 바닥(sortingOrder -100)보다는 위, 벽/적(0)보다는 아래에 깔리는 값.</summary>
    private const int FillSortingOrder = -2;

    /// <summary>경계선은 채움보다 한 단계 위에 그려 테두리가 묻히지 않게 한다.</summary>
    private const int RimSortingOrder = -1;

    /// <summary>
    /// 원판을 몇 겹으로 덮을지(오버드로 배수). 방출량을 넓이에서 역산하는 데 쓴다.
    /// 가산 혼합이라 값이 크면 금방 하얗게 뜨므로 2겹 안팎이 적당하다.
    /// </summary>
    private const float FillCoverage = 2.2f;

    /// <summary>테두리 입자 간격을 입자 크기의 몇 배로 둘지. 1보다 작아야 선이 끊기지 않는다.</summary>
    private const float RimSpacingRatio = 0.6f;

    private const float FillLifetime = 1.1f;
    private const float RimLifetime = 0.55f;

    private static Material _sharedMaterial;
    private static Texture2D _sharedTexture;

    private ParticleSystem _fill;
    private ParticleSystem _rim;

    /// <summary>정지 후 남은 입자가 다 사라질 때까지 기다려야 하는 시간.</summary>
    private float _tailTime;

    /// <summary>
    /// 지정한 반경/속성에 맞는 장판 비주얼 오브젝트를 만들어 <paramref name="parent"/>의 자식으로 붙인다.
    /// </summary>
    /// <param name="parent">장판 본체(위치 기준).</param>
    /// <param name="radius">장판 판정 반경. 비주얼이 정확히 이 반경에 맞춰진다.</param>
    /// <param name="attribute">색/움직임 스타일을 고르는 공격 속성(화상/냉기).</param>
    public static DamageZoneVisual Create(Transform parent, float radius, BulletAttackAttribute attribute)
    {
        var go = new GameObject("ZoneVisual");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale = Vector3.one;

        var visual = go.AddComponent<DamageZoneVisual>();
        visual.Build(radius, attribute);
        return visual;
    }

    private void Build(float radius, BulletAttackAttribute attribute)
    {
        radius = Mathf.Max(0.05f, radius);
        Style style = Style.For(attribute);

        _fill = GetComponent<ParticleSystem>();
        ConfigureFill(_fill, radius, style);

        var rimGo = new GameObject("Rim");
        rimGo.transform.SetParent(transform, false);
        _rim = rimGo.AddComponent<ParticleSystem>();
        ConfigureRim(_rim, radius, style);

        _tailTime = Mathf.Max(FillLifetime, RimLifetime) + 0.15f;

        _fill.Play(true);
    }

    /// <summary>
    /// 장판 판정이 끝났을 때 호출한다. 본체와 분리해 방출만 멈추고, 남은 입자가 자연스럽게
    /// 사라진 뒤 스스로 파괴된다(장판이 순간 삭제되며 이펙트가 뚝 끊기는 것을 막는다).
    /// </summary>
    public void Release()
    {
        // 씬 언로드 중이면 새 루트 오브젝트를 만들지 않고 그대로 함께 정리되게 둔다.
        if (!gameObject.scene.isLoaded) return;

        transform.SetParent(null, true);

        if (_fill != null) _fill.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        if (_rim != null) _rim.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        Destroy(gameObject, _tailTime);
    }

    // ───────────────────────── 파티클 구성 ─────────────────────────

    /// <summary>원판 내부를 채우는 안개/불꽃. 반경 안쪽 아무 데서나 태어나 스타일대로 흐른다.</summary>
    private static void ConfigureFill(ParticleSystem ps, float radius, Style style)
    {
        // 입자 하나의 평균 크기와 원판 넓이로부터 "몇 개가 동시에 살아 있어야 하는지"를 역산한다.
        // 이렇게 하면 반경이 2.5든 5든 보이는 밀도가 똑같이 유지된다.
        float minSize = radius * 0.12f;
        float maxSize = radius * 0.26f;
        float averageParticleArea = Mathf.PI * Mathf.Pow((minSize + maxSize) * 0.25f, 2f);
        float zoneArea = Mathf.PI * radius * radius;
        float aliveTarget = zoneArea * FillCoverage / Mathf.Max(0.0001f, averageParticleArea);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = FillLifetime;
        main.startSpeed = style.driftSpeed;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(style.coreColor, style.edgeColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World; // 장판은 제자리에 고정된다
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.CeilToInt(aliveTarget * 1.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = aliveTarget / FillLifetime;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 1f; // 원판 내부 전체
        shape.arc = 360f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;

        // x/y/z 선형 속도 커브는 반드시 셋 다 같은 모드여야 한다
        // (하나만 TwoConstants로 두면 "Particle Velocity curves must all be in the same mode" 런타임 에러).
        // 서리는 riseSpeed가 음수라 min/max 순서도 보정한다.
        float riseA = style.riseSpeed * 0.5f;
        float riseB = style.riseSpeed;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f); // 좌우로 살짝 흔들려 균일해 보이지 않게
        velocity.y = new ParticleSystem.MinMaxCurve(Mathf.Min(riseA, riseB), Mathf.Max(riseA, riseB));
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        velocity.orbitalZ = style.swirlSpeed; // 살짝 도는 소용돌이(orbital X/Y는 기본 Constant 0 - 모드 일치)

        ApplyFade(ps, fadeIn: 0.25f);
        ApplySizeCurve(ps, style.growOverLifetime);
        ConfigureRenderer(ps, FillSortingOrder);
    }

    /// <summary>판정 경계선을 그리는 테두리 입자. 어디까지가 장판인지 한눈에 보이게 하는 핵심.</summary>
    private static void ConfigureRim(ParticleSystem ps, float radius, Style style)
    {
        // 경계선 굵기는 반경을 그대로 따라가면 안 된다(반경 5에서 0.65 굵기면 선이 아니라 띠가 된다).
        // 반경에 약하게만 비례시키고 상한/하한을 두어 어느 크기에서도 "선"으로 읽히게 한다.
        float rimSize = Mathf.Clamp(radius * 0.06f, 0.08f, 0.24f);

        // 둘레를 입자 간격으로 나누면 선이 끊기지 않을 만큼의 동시 생존 개수가 나온다.
        float circumference = 2f * Mathf.PI * radius;
        float aliveTarget = circumference / Mathf.Max(0.0001f, rimSize * RimSpacingRatio);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = RimLifetime;
        main.startSpeed = 0f; // 테두리에 머물러야 경계선이 흐려지지 않는다
        main.startSize = new ParticleSystem.MinMaxCurve(rimSize * 0.7f, rimSize * 1.3f);
        main.startColor = new ParticleSystem.MinMaxGradient(style.rimColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = Mathf.CeilToInt(aliveTarget * 1.5f);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = aliveTarget / RimLifetime;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0f; // 테두리에서만 방출
        shape.arc = 360f;
        // Random으로 뿌리면 뭉치고 비는 구간이 생겨 경계선이 울퉁불퉁해진다.
        // Loop는 방출 지점이 원을 따라 일정하게 돌아 균일한 링이 된다.
        shape.arcMode = ParticleSystemShapeMultiModeValue.Loop;

        // 테두리를 따라 천천히 흘러 살아있는 링처럼 보이게 한다.
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.orbitalZ = style.swirlSpeed * 2f;

        ApplyFade(ps, fadeIn: 0.15f);
        ApplySizeCurve(ps, growOverLifetime: false);
        ConfigureRenderer(ps, RimSortingOrder);
    }

    /// <summary>
    /// 태어날 때 부드럽게 나타나고 사라질 때 서서히 없어지도록 알파 곡선을 건다.
    /// 색은 흰색으로 두어야 한다 - colorOverLifetime은 startColor에 곱해지므로 여기서 색을 또 넣으면
    /// 색이 두 번 곱해져 어두워진다.
    /// </summary>
    private static void ApplyFade(ParticleSystem ps, float fadeIn)
    {
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, fadeIn),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(0f, 1f),
            });

        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private static void ApplySizeCurve(ParticleSystem ps, bool growOverLifetime)
    {
        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;

        AnimationCurve curve = growOverLifetime
            ? AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.15f) // 불꽃: 피어오르며 커진다
            : AnimationCurve.EaseInOut(0f, 1.1f, 1f, 0.35f); // 서리/테두리: 반짝 후 잦아든다

        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, curve);
    }

    private static void ConfigureRenderer(ParticleSystem ps, int sortingOrder)
    {
        var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        psRenderer.alignment = ParticleSystemRenderSpace.View;
        psRenderer.sharedMaterial = GetSharedMaterial();
        psRenderer.sortingOrder = sortingOrder;
        psRenderer.shadowCastingMode = ShadowCastingMode.Off;
        psRenderer.receiveShadows = false;
    }

    // ───────────────────────── 머티리얼 / 텍스처 ─────────────────────────

    /// <summary>
    /// 모든 장판이 공유하는 파티클 머티리얼. 겹칠수록 밝아지도록 가산 혼합을 시도하고,
    /// 해당 프로퍼티가 없는 셰이더(Sprites/Default 등)로 폴백되면 기본 알파 블렌딩을 쓴다.
    /// </summary>
    private static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Sprites/Default")
                        ?? Shader.Find("Particles/Standard Unlit");

        _sharedMaterial = new Material(shader)
        {
            name = "DamageZoneParticle (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = GetSoftDotTexture(),
        };

        TryConfigureAdditive(_sharedMaterial);
        return _sharedMaterial;
    }

    /// <summary>URP Particles/Unlit의 Transparent + Additive 설정. 프로퍼티가 없으면 조용히 건너뛴다.</summary>
    private static void TryConfigureAdditive(Material material)
    {
        if (!material.HasProperty("_SrcBlend") || !material.HasProperty("_DstBlend")) return;

        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f); // Transparent
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 2f);     // Additive
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);

        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    /// <summary>가운데가 밝고 가장자리로 갈수록 투명해지는 원형 텍스처를 코드로 만든다.</summary>
    private static Texture2D GetSoftDotTexture()
    {
        if (_sharedTexture != null) return _sharedTexture;

        const int size = 64;
        const float center = (size - 1) * 0.5f;

        _sharedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "DamageZoneDot (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
        };

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                // 1 - d² 를 한 번 더 제곱해 중심부는 꽉 차고 경계는 부드럽게 사라지는 감쇠.
                float falloff = Mathf.Clamp01(1f - distance);
                float alpha = falloff * falloff;

                var value = (byte)Mathf.RoundToInt(alpha * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, value);
            }
        }

        _sharedTexture.SetPixels32(pixels);
        _sharedTexture.Apply(false, false);
        return _sharedTexture;
    }

    // ───────────────────────── 속성별 스타일 ─────────────────────────

    /// <summary>속성(화상/냉기)에 따른 색과 움직임 프리셋.</summary>
    private readonly struct Style
    {
        public readonly Color coreColor;
        public readonly Color edgeColor;
        public readonly Color rimColor;

        /// <summary>태어날 때 원 바깥으로 퍼지는 초기 속도.</summary>
        public readonly float driftSpeed;

        /// <summary>수명 동안 위로 떠오르는 속도(불은 크게, 서리는 작게 - 서리는 가라앉는다).</summary>
        public readonly float riseSpeed;

        /// <summary>장판 중심을 기준으로 도는 속도(도/초).</summary>
        public readonly float swirlSpeed;

        /// <summary>수명 동안 커지는지(불꽃) 작아지는지(서리).</summary>
        public readonly bool growOverLifetime;

        private Style(Color coreColor, Color edgeColor, Color rimColor,
            float driftSpeed, float riseSpeed, float swirlSpeed, bool growOverLifetime)
        {
            this.coreColor = coreColor;
            this.edgeColor = edgeColor;
            this.rimColor = rimColor;
            this.driftSpeed = driftSpeed;
            this.riseSpeed = riseSpeed;
            this.swirlSpeed = swirlSpeed;
            this.growOverLifetime = growOverLifetime;
        }

        public static Style For(BulletAttackAttribute attribute)
        {
            switch (attribute)
            {
                case BulletAttackAttribute.Burn:
                    // 화염지대: 주황~노랑이 위로 피어오르며 커진다.
                    return new Style(
                        coreColor: new Color(1f, 0.45f, 0.08f, 0.55f),
                        edgeColor: new Color(1f, 0.78f, 0.20f, 0.35f),
                        rimColor: new Color(1f, 0.62f, 0.15f, 0.9f),
                        driftSpeed: 0.15f,
                        riseSpeed: 0.9f,
                        swirlSpeed: 12f,
                        growOverLifetime: true);

                case BulletAttackAttribute.Frost:
                    // 냉기지대: 하늘색 서리가 낮게 깔리며 잦아든다.
                    return new Style(
                        coreColor: new Color(0.35f, 0.75f, 1f, 0.5f),
                        edgeColor: new Color(0.78f, 0.95f, 1f, 0.3f),
                        rimColor: new Color(0.6f, 0.9f, 1f, 0.9f),
                        driftSpeed: 0.1f,
                        riseSpeed: -0.25f,
                        swirlSpeed: -8f,
                        growOverLifetime: false);

                default:
                    // 속성 없는 장판(신규 효과 추가 시)도 최소한 경계는 보이도록 중립 회백색.
                    return new Style(
                        coreColor: new Color(0.85f, 0.85f, 0.9f, 0.4f),
                        edgeColor: new Color(1f, 1f, 1f, 0.25f),
                        rimColor: new Color(1f, 1f, 1f, 0.8f),
                        driftSpeed: 0.12f,
                        riseSpeed: 0.35f,
                        swirlSpeed: 10f,
                        growOverLifetime: false);
            }
        }
    }
}
