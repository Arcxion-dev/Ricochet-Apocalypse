using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>HUD "장착 파츠" 칩 한 개(프리팹). 파츠 이름을 표시한다. 아이콘 필드가 없어 이름 중심.</summary>
public class PartChipView : MonoBehaviour
{
    [SerializeField] private Image _dot;
    [SerializeField] private TMP_Text _name;

    public void Set(string partName)
    {
        if (_name != null) _name.text = partName;
    }
}
