using UnityEngine;

/// <summary>
/// 형태(외형) 모듈: 이 오브젝트가 화면에 어떤 모습으로 보일지(스프라이트/색상/크기)를 정의합니다.
/// Enemy, Map 등 시각적 형태가 필요한 모든 객체에 공통으로 부착 가능합니다.
/// 값이 바뀌면 자동으로 화면에 반영됩니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ShapeModule : MonoBehaviour
{
    [Header("외형 설정")]
    [SerializeField] private Sprite sprite;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private Vector2 visualSize = Vector2.one;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 0;

    private SpriteRenderer spriteRenderer;

    public Sprite Sprite => sprite;
    public Color Color => color;
    public Vector2 VisualSize => visualSize;

    private void OnEnable()
    {
        EnsureRenderer();
        Apply();
    }

    private void OnValidate()
    {
        EnsureRenderer();
        Apply();
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer != null) return;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            var visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
        }
    }

    public void Apply()
    {
        EnsureRenderer();
        if (spriteRenderer == null) return;

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.transform.localScale = new Vector3(visualSize.x, visualSize.y, 1f);
    }

    public void SetAppearance(Sprite newSprite, Color newColor, Vector2 newSize)
    {
        sprite = newSprite;
        color = newColor;
        visualSize = newSize;
        Apply();
    }
}
