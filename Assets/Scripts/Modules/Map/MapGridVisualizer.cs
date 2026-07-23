using UnityEngine;

/// <summary>
/// 맵 위에 격자(그리드) 선을 그려서 플레이어 화면(게임 뷰)에도 보이게 합니다.
/// GridModule의 설정(칸 수, 칸 크기, 원점)을 기반으로 LineRenderer 선을 생성합니다.
/// OnDrawGizmos와 달리 실제 Game 화면에서도 렌더링됩니다.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(GridModule))]
public class MapGridVisualizer : MonoBehaviour
{
    [Header("표시 설정")]
    [SerializeField] private Color lineColor = new Color(1f, 1f, 1f, 0.25f);
    [SerializeField] private float lineWidth = 0.03f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -50;

    private GridModule gridModule;
    private Transform linesParent;
    private static Material s_lineMaterial;

private void OnEnable()
    {
        gridModule = GetComponent<GridModule>();
        QueueRebuild();
    }

private void OnValidate()
    {
        if (gridModule == null) gridModule = GetComponent<GridModule>();
        QueueRebuild();
    }

private bool rebuildQueued;

    /// <summary>
    /// OnEnable/OnValidate처럼 Awake·CheckConsistency 단계에서 호출될 수 있는 지점에서는
    /// 바로 오브젝트를 생성하지 않고, 에디터에서는 delayCall로 안전한 시점까지 미룹니다.
    /// (그렇지 않으면 "SendMessage cannot be called during Awake..." 경고가 발생합니다)
    /// </summary>
    private void QueueRebuild()
    {
        if (rebuildQueued) return;
        rebuildQueued = true;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                rebuildQueued = false;
                if (this != null) Rebuild();
            };
            return;
        }
#endif
        rebuildQueued = false;
        Rebuild();
    }


    /// <summary>
    /// 현재 GridModule 설정에 맞춰 격자 선을 다시 생성합니다.
    /// 칸 수/크기/원점이 바뀐 뒤 호출하면 화면에도 즉시 반영됩니다.
    /// </summary>
    public void Rebuild()
    {
        if (gridModule == null) return;

        if (linesParent != null)
        {
            DestroyLinesParent();
        }

        var parentObj = new GameObject("GridLines");
        linesParent = parentObj.transform;
        linesParent.SetParent(transform, false);
        linesParent.gameObject.hideFlags = HideFlags.DontSave;

        int columns = gridModule.Columns;
        int rows = gridModule.Rows;
        float cellSize = gridModule.CellSize;
        Vector2 origin = gridModule.Origin;

        for (int c = 0; c <= columns; c++)
        {
            Vector3 from = new Vector3(origin.x + c * cellSize, origin.y, 0f);
            Vector3 to = new Vector3(origin.x + c * cellSize, origin.y + rows * cellSize, 0f);
            CreateLine("Col_" + c, from, to);
        }

        for (int r = 0; r <= rows; r++)
        {
            Vector3 from = new Vector3(origin.x, origin.y + r * cellSize, 0f);
            Vector3 to = new Vector3(origin.x + columns * cellSize, origin.y + r * cellSize, 0f);
            CreateLine("Row_" + r, from, to);
        }
    }

    private void CreateLine(string lineName, Vector3 from, Vector3 to)
    {
        var lineObj = new GameObject(lineName);
        lineObj.hideFlags = HideFlags.DontSave;
        lineObj.transform.SetParent(linesParent, false);

        var lr = lineObj.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = GetLineMaterial();
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;
        lr.numCapVertices = 0;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        lr.allowOcclusionWhenDynamic = false;
    }

    private void DestroyLinesParent()
    {
        var obj = linesParent.gameObject;
        linesParent = null;
        if (obj == null) return;

        if (Application.isPlaying)
        {
            Destroy(obj);
        }
        else
        {
            DestroyImmediate(obj);
        }
    }

    private void OnDisable()
    {
        if (linesParent != null)
        {
            DestroyLinesParent();
        }
    }

    private static Material GetLineMaterial()
    {
        if (s_lineMaterial == null)
        {
            var shader = Shader.Find("Sprites/Default");
            s_lineMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        }
        return s_lineMaterial;
    }
}
