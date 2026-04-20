using UnityEngine;
using System.Collections.Generic;

public class MahjongTable : MonoBehaviour
{
    [Header("捨て牌の基準点")]
    public Transform riverPointFront; 
    public Transform riverPointBack; // 対面（CPUなど）用
    public Transform riverPointRight; // 追加: 右側
    public Transform riverPointLeft; // 追加: 左側

    [Header("牌プレハブ")]
    [SerializeField] private GameObject[] tilePrefabs;

    private List<GameObject> activeDiscards = new List<GameObject>();

    // 定数
    private const float TileWidth = 4.1f; 
    private const float TileHeight = 5.3f; 
    private const float Gap = 0.0f;

    private Dictionary<int, int> discardCounts = new Dictionary<int, int>();
    private Dictionary<int, float> seatCursorX = new Dictionary<int, float>();

    public void DiscardTile(int seat, int tileId, bool isRiichi)
    {
        if (tileId < 0 || tileId >= tilePrefabs.Length) return;
        GameObject prefab = tilePrefabs[tileId];
        if (prefab == null) return;

        // オフラインなので、Seat 0 が常に自分
        Transform targetRiver = null;
        
        // 4人打ち対応の相対位置計算 (Seat 0視点)
        if (seat == 0) targetRiver = riverPointFront;
        else if (seat == 1) targetRiver = riverPointRight; // 下家
        else if (seat == 2) targetRiver = riverPointBack; // 対面
        else if (seat == 3) targetRiver = riverPointLeft; // 上家

        // 指定がなければFront/Backの簡易2人打ちロジックに倒す
        if (targetRiver == null)
        {
            if (seat == 0) targetRiver = riverPointFront;
            else targetRiver = riverPointBack;
        }

        if (targetRiver == null) return;

        // --- カウント管理 ---
        if (!discardCounts.ContainsKey(seat)) discardCounts[seat] = 0;
        int currentCount = discardCounts[seat];
        
        // --- 行と列 ---
        int row = currentCount / MahjongGameManager.Instance.config.RiverLength;
        int col = currentCount % MahjongGameManager.Instance.config.RiverLength;

        // 行が変わったらカーソルリセット
        if (!seatCursorX.ContainsKey(seat) || col == 0)
        {
            seatCursorX[seat] = 0f;
        }

        // --- 寸法決定 ---
        float currentVisualWidth = isRiichi ? TileHeight : TileWidth;
        float currentVisualHeight = isRiichi ? TileWidth : TileHeight;

        // --- 座標計算 ---
        float xCenterPos = -TileWidth*MahjongGameManager.Instance.config.RiverLength / 2 + seatCursorX[seat] + (currentVisualWidth / 2.0f);
        seatCursorX[seat] += currentVisualWidth + Gap; 

        // Z座標: 行の基準位置
        float baseZ = -row * TileHeight - 10.0f;
        float zOffset = (TileHeight - currentVisualHeight) / 2.0f;
        
        Vector3 localPos = new Vector3(xCenterPos, 1.7f, baseZ + zOffset);

        // --- 生成と適用 ---
        GameObject discardObj = Instantiate(prefab, targetRiver);
        discardObj.transform.localPosition = localPos;
        
        Quaternion baseRotation = Quaternion.Euler(-90f, 0, 180); 
        discardObj.transform.localRotation = baseRotation;

        if (isRiichi)
        {
            discardObj.transform.Rotate(0, 0, 90f, Space.Self);
        }

        var rb = discardObj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
        // マージャン牌コンポーネントがついていたら、物理挙動を切るなど初期化
        if (discardObj.TryGetComponent<MahjongTile>(out var tile))
        {
             // 視覚用オブジェクトなので特に何もしなくて良いが、TileIdはセットしておくと良いかも
             tile.Initialize(tileId, -1);
        }

        activeDiscards.Add(discardObj);
        discardCounts[seat]++;
    }

    public void ClearTable()
    {
        foreach (var obj in activeDiscards)
        {
            if (obj != null) Destroy(obj);
        }
        activeDiscards.Clear();
        discardCounts.Clear();
        seatCursorX.Clear();
    }
}