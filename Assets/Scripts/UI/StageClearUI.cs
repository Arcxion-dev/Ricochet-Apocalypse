using System;
using System.Collections.Generic;
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

    /// <summary>
    /// 클리어 결과 창이 떠 있는지. 이 동안은 보상이 이미 지급됐고 진행만 남은 상태라,
    /// 다른 UI(일시정지 등)가 끼어들지 않도록 양보 판단에 쓴다.
    /// </summary>
    public bool IsShowing => _canvas != null && _canvas.enabled;

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
        if (_canvas != null) _canvas.enabled = false; // 평소 숨김.
    }

    /// <summary>클리어 결과와 드랍 목록을 표시하고, [확인] 시 onConfirm을 호출한다.</summary>
    public void Show(StageResult result, IReadOnlyList<DropResult> drops, Action onConfirm)
    {
        _onConfirm = onConfirm;
        Time.timeScale = 1f; // 오버레이가 프리즈 뒤에 가려지지 않도록.

        if (_title != null) _title.text = result.IsClear ? "STAGE CLEAR" : "STAGE FAILED";
        if (_perfectPill != null) _perfectPill.SetActive(result.IsPerfect);
        if (_killsValue != null) _killsValue.text = result.TotalKills.ToString();
        if (_comboValue != null) _comboValue.text = "×" + result.Combo;
        if (_shotsValue != null) _shotsValue.text = result.ShotsFired.ToString();
        if (_goldValue != null) _goldValue.text = "+ " + result.Reward + " G";

        PopulateDrops(drops);

        if (_canvas != null)
        {
            _canvas.enabled = true;
            _canvas.transform.SetAsLastSibling();
        }
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
        if (_canvas != null) _canvas.enabled = false;
        var cb = _onConfirm;
        _onConfirm = null;
        cb?.Invoke();
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
