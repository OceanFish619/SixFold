using UnityEngine;
using UnityEngine.Tilemaps;

public class PrintGroundSize : MonoBehaviour
{
    [SerializeField] bool logOnStart;

    void Start()
    {
        if (!logOnStart) return;

        var tm = GetComponent<Tilemap>();
        if (tm == null) return;

        var b = tm.cellBounds;
        Debug.Log($"Ground bounds size: {b.size.x} x {b.size.y} tiles, min: {b.min}, max: {b.max}");
    }
}
