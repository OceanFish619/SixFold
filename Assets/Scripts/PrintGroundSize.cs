using UnityEngine;
using UnityEngine.Tilemaps;

public class PrintGroundSize : MonoBehaviour
{
    void Start()
    {
        var tm = GetComponent<Tilemap>();
        var b = tm.cellBounds;              // 已刷 tile 的边界
        Debug.Log($"Ground bounds size: {b.size.x} x {b.size.y} tiles, min: {b.min}, max: {b.max}");
    }
}