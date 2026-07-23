using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 칸(그리드) 모듈: 맵을 일정 크기의 칸으로 나누어 관리합니다.
/// 칸마다 별도의 GameObject를 만들지 않고 좌표 데이터로만 관리합니다.
/// </summary>
public class GridModule : MonoBehaviour
{
    [Header("그리드 설정")]
    [SerializeField] private int columns = 9;
    [SerializeField] private int rows = 12;
    [SerializeField] private float cellSize = 1f;
    [SerializeField] private Vector2 origin = new Vector2(-4.5f, -6f);

    public int Columns => columns;
    public int Rows => rows;
    public float CellSize => cellSize;
    public Vector2 Origin => origin;

    public Vector2 GridWorldSize => new Vector2(columns * cellSize, rows * cellSize);
    public Vector2 GridWorldCenter => origin + GridWorldSize * 0.5f;

    private readonly Dictionary<Vector2Int, GameObject> occupants = new Dictionary<Vector2Int, GameObject>();

    public Vector2Int WorldToCell(Vector2 worldPos)
    {
        int col = Mathf.FloorToInt((worldPos.x - origin.x) / cellSize);
        int row = Mathf.FloorToInt((worldPos.y - origin.y) / cellSize);
        return new Vector2Int(col, row);
    }

    public Vector2 CellToWorld(Vector2Int cell)
    {
        float x = origin.x + (cell.x + 0.5f) * cellSize;
        float y = origin.y + (cell.y + 0.5f) * cellSize;
        return new Vector2(x, y);
    }

    public bool IsInsideGrid(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < columns && cell.y >= 0 && cell.y < rows;
    }

    public bool IsCellOccupied(Vector2Int cell) => occupants.ContainsKey(cell);

    public bool TryOccupyCell(Vector2Int cell, GameObject occupant)
    {
        if (!IsInsideGrid(cell) || IsCellOccupied(cell)) return false;
        occupants[cell] = occupant;
        return true;
    }

    public void ReleaseCell(Vector2Int cell) => occupants.Remove(cell);

    public GameObject GetOccupant(Vector2Int cell)
    {
        occupants.TryGetValue(cell, out var occupant);
        return occupant;
    }

    public void ClearAllOccupants() => occupants.Clear();

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
        for (int c = 0; c <= columns; c++)
        {
            Vector3 from = new Vector3(origin.x + c * cellSize, origin.y, 0f);
            Vector3 to = new Vector3(origin.x + c * cellSize, origin.y + rows * cellSize, 0f);
            Gizmos.DrawLine(from, to);
        }
        for (int r = 0; r <= rows; r++)
        {
            Vector3 from = new Vector3(origin.x, origin.y + r * cellSize, 0f);
            Vector3 to = new Vector3(origin.x + columns * cellSize, origin.y + r * cellSize, 0f);
            Gizmos.DrawLine(from, to);
        }
    }
}
