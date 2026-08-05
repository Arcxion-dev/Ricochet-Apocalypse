using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스테이지 클리어 시 뜨는 결과/보상 창. 이제 <b>프리팹 기반</b>이다
/// (Figma 목업 → <c>Resources/UI/StageClearScreen</c> 프리팹).
///
/// - 씬 무관 싱글턴: <see cref="Bootstrap"/>이 첫 씬 로드 후 프리팹을 인스턴스화한다(별도 배치 불필요).
/// - <see cref="Show"/>가 결과/드랍을 받아 텍스트를 채우고, 드랍 개수만큼 <see cref="DropCardView"/>를 생성한다.
/// - [확인] 버튼 → <see cref="Show"/>에 넘긴 onConfirm 콜백(보통 상점 이동).
///
/// 필드 참조는 프리팹에서 바인딩되어 있다(코드로 캔버스를 만들지 않는다).
/// </summary>
public class StageClearUI : MonoBehaviour
{
    public static StageClearUI Instance { get; private set; }

    [Header("루트")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private CanvasGroup _group;
    [SerializeField] private RectTransform _window;   // 스케일 등장 대상

    [Header("텍스트")]
    [SerializeField] private TMP_Text _title;
    [SerializeField] private GameObject _perfectPill;
    [SerializeField] private TMP_Text _killsValue;
    [SerializeField] private TMP_Text _comboValue;
    [SerializeField] private TMP_Text _shotsValue;
    [SerializeField] private TMP_Text _goldValue;

    [Header("드랍")]
    [SerializeField] private Transform _dropsContainer;
    [SerializeField] private GameObject _dropsEmptyLabel;
    [SerializeField] private DropCardView _dropCardPrefab;

    [Header("버튼")]
    [SerializeField] private Button _confirmButton;

    private Action _onConfirm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("UI/StageClearScreen");
        if (prefab == null)
        {
            Debug.LogError("[StageClearUI] Resources/UI/StageClearScreen 프리팹을 찾을 수 없습니다.");
            return;
        }
        Instantiate(prefab); // 프리팹 루트의 StageClearUI가 Awake에서 Instance를 잡는다.
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureEventSystem();
        if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirmClicked);
        if (_group == null && _canvas != null) _group = UIAnim.GroupOf(_canvas);
        if (_canvas != null) _canvas.enabled = false; // 평소 숨김.
    }

    /// <summary>클리어 결과와 드랍 목록을 표시하고, [확인] 시 onConfirm을 호출한다.</summary>
    public void Show(StageResult result, IReadOnlyList<DropResult> drops, Action onConfirm)
    {
        _onConfirm = onConfirm;
        Time.timeScale = 1f; // 오버레이가 프리즈 뒤에 가려지지 않도록.

        if (_title != null) _title.text = result.IsClear ? "STAGE CLEAR" : "STAGE FAILED";
        if (_perfectPill != null) _perfectPill.SetActive(result.IsPerfect);

        PopulateDrops(drops);

        if (_canvas != null)
        {
            _canvas.enabled = true;
            _canvas.transform.SetAsLastSibling();
        }
        PlayEntrance(result);
    }

    /// <summary>
    /// 창이 뜨는 순서: 창 등장 → 타이틀 펀치 → 통계 타일 하나씩 → 골드 카운트업 → 드랍 카드 하나씩.
    /// 결과를 "읽어 나가는" 리듬을 만들어 준다.
    /// </summary>
    private void PlayEntrance(StageResult result)
    {
        UIAnim.ShowPopup(_group, _window, UIAnim.Slow, 0.88f);

        if (_title != null)
        {
            _title.rectTransform.localScale = Vector3.one;
            UIAnim.Punch(_title.rectTransform, 0.22f, 0.5f);
        }

        // 통계 타일: 값은 0에서 굴러오고, 타일 자체는 순서대로 튀어나온다.
        TileIn(_killsValue, result.TotalKills, "", "", 0.12f);
        TileIn(_comboValue, result.Combo, "×", "", 0.20f);
        TileIn(_shotsValue, result.ShotsFired, "", "", 0.28f);

        if (_goldValue != null)
        {
            _goldValue.text = "+ 0 G";
            var count = UIAnim.CountTo(_goldValue, 0, result.Reward, "+ ", " G", "N0", 0.7f);
            if (count != null) count.SetDelay(0.38f).OnComplete(() => UIAnim.Punch(_goldValue.rectTransform, 0.2f, 0.35f));
        }

        if (_perfectPill != null && _perfectPill.activeSelf)
            UIAnim.PopIn(_perfectPill.transform as RectTransform, 0.5f);

        // 드랍 카드는 마지막에, 확실히 눈에 띄게.
        if (_dropsContainer != null)
        {
            for (int i = 0; i < _dropsContainer.childCount; i++)
                UIAnim.PopIn(_dropsContainer.GetChild(i) as RectTransform, 0.6f + i * 0.09f);
        }
    }

    /// <summary>통계 타일 하나: 타일이 튀어나오면서 값이 0부터 굴러간다.</summary>
    private void TileIn(TMP_Text value, int target, string prefix, string suffix, float delay)
    {
        if (value == null) return;
        var tile = value.transform.parent as RectTransform;
        if (tile != null) UIAnim.PopIn(tile, delay, UIAnim.Fast);

        value.text = prefix + "0" + suffix;
        var count = UIAnim.CountTo(value, 0, target, prefix, suffix, "0", 0.5f);
        if (count != null) count.SetDelay(delay + 0.08f);
    }

    private void PopulateDrops(IReadOnlyList<DropResult> drops)
    {
        if (_dropsContainer == null) return;

        for (int i = _dropsContainer.childCount - 1; i >= 0; i--)
            Destroy(_dropsContainer.GetChild(i).gameObject);

        int count = 0;
        if (drops != null && _dropCardPrefab != null)
        {
            foreach (var d in drops)
            {
                if (d.Item == null) continue;
                var card = Instantiate(_dropCardPrefab, _dropsContainer);
                card.Set(d.Item, d.Quantity);
                count++;
            }
        }
        if (_dropsEmptyLabel != null) _dropsEmptyLabel.SetActive(count == 0);
    }

    private void OnConfirmClicked()
    {
        var cb = _onConfirm;
        _onConfirm = null;

        // 창이 닫히는 걸 보고 나서 다음 씬으로 넘어간다.
        if (_confirmButton != null) _confirmButton.interactable = false;
        UIAnim.HidePopup(_group, _window, UIAnim.Normal, () =>
        {
            if (_canvas != null) _canvas.enabled = false;
            if (_confirmButton != null) _confirmButton.interactable = true;
            cb?.Invoke();
        });
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(esGO);
    }
}
