using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 리볼버 실린더의 약실 한 칸. <see cref="RevolverCylinderUI"/>가 배열로 들고 갱신한다.
///
/// 실린더 뒷판(Cyl_Plate)과 함께 회전하기 때문에, 안에 든 탄환 아이콘/숫자는
/// 반대로 돌려서 항상 똑바로 서 있게 만든다(<see cref="SetCounterRotation"/>).
/// </summary>
public class CylinderChamberView : MonoBehaviour
{
    [SerializeField] private CanvasGroup _group;      // 화면 가장자리 페이드(약실 전체)
    [SerializeField] private Image _ring;      // 약실 테두리(금속)
    [SerializeField] private RectTransform _upright;  // 회전을 상쇄해 항상 세워두는 컨테이너
    [SerializeField] private Image _icon;      // 탄환 아이콘
    [SerializeField] private TMP_Text _count;  // 보유 수량

    /// <summary>
    /// 화면 오른쪽 벽에 가려질 위치로 돌아갈 때의 투명도(1=완전히 보임, 0=사라짐).
    /// 잘려나가는 대신 스르륵 사라지고 반대편에서 스르륵 나타나게 해 이질감을 없앤다.
    /// </summary>
    public float EdgeAlpha { get; private set; } = 1f;

    /// <summary>가장자리 페이드 값을 적용한다. <see cref="_upright"/>의 장전 페이드와는 중첩되어 곱해진다.</summary>
    public void SetEdgeFade(float alpha)
    {
        EdgeAlpha = Mathf.Clamp01(alpha);
        if (_group != null) _group.alpha = EdgeAlpha;
    }

    private RectTransform _rt;
    public RectTransform Rect => _rt != null ? _rt : (_rt = (RectTransform)transform);

    /// <summary>이 약실에 탄이 들어 있는지(빈 약실은 아이콘/숫자를 감춘다).</summary>
    public bool HasRound { get; private set; }

    /// <summary>약실 내용을 설정한다. <paramref name="definition"/>이 null이면 빈 약실.</summary>
    public void Set(BulletItemDefinition definition, int count, Sprite fallbackIcon)
    {
        HasRound = definition != null;

        if (_icon != null)
        {
            _icon.enabled = HasRound;
            if (HasRound)
            {
                _icon.sprite = definition.icon != null ? definition.icon : fallbackIcon;
                _icon.color = BulletTint(definition);
            }
        }

        if (_count != null)
        {
            _count.enabled = HasRound;
            if (HasRound) _count.text = count.ToString();
        }

        // 빈 약실은 테두리를 어둡게 죽여 "비었다"는 걸 한눈에 보이게 한다.
        if (_ring != null)
            _ring.color = HasRound ? Color.white : new Color(1f, 1f, 1f, 0.35f);
    }

    /// <summary>선택(장전 위치)된 약실인지에 따라 밝기를 조절한다.</summary>
    public void SetLoaded(bool loaded)
    {
        if (_upright == null) return;
        var g = UIAnim.GroupOf(_upright);
        // 장전된 탄은 가운데 큰 슬롯에 올라가 있으므로, 약실 쪽은 흐리게 남긴다.
        UIAnim.Stop(g);
        g.DOFade(loaded ? 0.3f : 1f, UIAnim.Fast).SetUpdate(true).SetTarget(g).SetLink(gameObject);
    }

    /// <summary>실린더가 <paramref name="rotorAngle"/>도 돌아갔을 때 내용물을 똑바로 세운다.</summary>
    public void SetCounterRotation(float rotorAngle)
    {
        if (_upright != null) _upright.localEulerAngles = new Vector3(0f, 0f, -rotorAngle);
    }

    /// <summary>탄환의 대표 색. 능력 라벨이 있으면 그 속성 색, 없으면 시안.</summary>
    public static Color BulletTint(BulletItemDefinition definition)
    {
        if (definition == null) return UITheme.Cyan;
        if (definition.isBasic) return UITheme.TextHi;

        var labels = definition.GetAbilityLabels();
        return labels != null && labels.Count > 0 ? UITheme.AbilityColor(labels[0]) : UITheme.Cyan;
    }
}
