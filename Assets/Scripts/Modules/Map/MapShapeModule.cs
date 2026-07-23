using UnityEngine;

/// <summary>
/// 지도 형태 모듈: 맵의 기본 배경 스프라이트를 담당합니다.
/// 같은 오브젝트에 GridModule이 있으면 배경 크기를 그리드 전체 크기에 맞춰 자동 조정합니다.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class MapShapeModule : MonoBehaviour
{
    [Header("배경 설정")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private bool fitToGrid = true;
    [SerializeField] private Vector2 manualSize = new Vector2(10f, 10f);
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -100;

    private SpriteRenderer spriteRenderer;
    private GridModule gridModule;

    private void OnEnable()
    {
        EnsureRenderer();
        gridModule = GetComponent<GridModule>();
        Apply();
    }

    private void OnValidate()
    {
        EnsureRenderer();
        if (gridModule == null) gridModule = GetComponent<GridModule>();
        Apply();
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer != null) return;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            var visual = new GameObject("Background");
            visual.transform.SetParent(transform, false);
            spriteRenderer = visual.AddComponent<SpriteRenderer>();
        }
    }

    public void Apply()
    {
        EnsureRenderer();
        if (spriteRenderer == null) return;

        spriteRenderer.sprite = backgroundSprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;

        Vector2 targetSize = (fitToGrid && gridModule != null) ? gridModule.GridWorldSize : manualSize;

        if (backgroundSprite != null)
        {
            Vector2 spriteWorldSize = backgroundSprite.bounds.size;
            float scaleX = spriteWorldSize.x > 0f ? targetSize.x / spriteWorldSize.x : 1f;
            float scaleY = spriteWorldSize.y > 0f ? targetSize.y / spriteWorldSize.y : 1f;
            spriteRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        if (fitToGrid && gridModule != null)
        {
            spriteRenderer.transform.position = gridModule.GridWorldCenter;
        }
    }

    public void SetBackground(Sprite sprite, Color newColor)
    {
        backgroundSprite = sprite;
        color = newColor;
        Apply();
    }
}
