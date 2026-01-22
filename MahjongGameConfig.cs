using UnityEngine;

public enum DebugDeckMode
{
    Normal,         // 通常
    Chinitsu,       // 清一色（特定の一色のみ）
    JihaiOnly,      // 字牌のみ
    TanyaoOnly,     // タンヤオ牌（2～8）のみ
    YaochuOnly      // 么九牌（1,9,字牌）のみ
}

public enum TileSuit
{
    Manzu,
    Pinzu,
    Sozu
}

[CreateAssetMenu(fileName = "MahjongGameConfig", menuName = "Mahjong/GameConfig")]
public class MahjongGameConfig : ScriptableObject
{
    [Header("Debug Mode Settings")]
    [Tooltip("デバッグ用の山生成モード")]
    public DebugDeckMode DeckMode = DebugDeckMode.Normal;

    [Tooltip("清一色モードの時に使う色")]
    public TileSuit ChinitsuSuit = TileSuit.Manzu;

    [Header("Game Rules")]
    [Tooltip("王牌として残す枚数 (通常は14)")]
    public int DeadWallCount = 14;

    [Tooltip("手牌の枚数 (通常は13)")]
    public int HandTileCount = 13;

    [Header("Score Settings")]
    public int InitialScore = 25000;

    [Header("Wall Settings")]
    [Tooltip("山を表示するかどうか")]
    public bool ShowWall = true;

    [Tooltip("山の生成位置オフセット")]
    public Vector3 WallCenterPosition = new Vector3(0, 1.5f, 0);

    [Tooltip("山の一列あたりの長さ（2段積みなので、例えば17なら34枚）")]
    public int WallStackLength = 17;

    [Header("Deck Configuration (Normal Mode Only)")]
    public bool UseManzu = true;   
    public bool UsePinzu = true;   
    public bool UseSozu = true;    
    public bool UseHonors = true;  

    [Header("Red Dora Settings")]
    public bool UseRedDora = false; 
    public int RedManzuId = 34;
    public int RedPinzuId = 35;
    public int RedSozuId = 36;

    [Header("Tile Settings")]
    public float TileWidth = 4.1f;
    public float TileHeight = 5.4f;
    public float TileThickness = 3.0f;
}