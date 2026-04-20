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

    [Tooltip("局中に積もれる最大回数。0以下なら無制限（山が尽きるまで）")]
    public int MaxTsumoCountPerRound = 0;

    [Header("Score Settings")]
    public int InitialScore = 0;

    [Header("Wall Settings")]
    [Tooltip("山を表示するかどうか")]
    public bool ShowWall = true;

    [Tooltip("山の生成位置オフセット")]
    public Vector3 WallCenterPosition = new Vector3(0, 1.5f, 0);

    [Tooltip("山の一列あたりの長さ（2段積みなので、例えば17なら34枚）")]
    public int WallStackLength = 17;

    [Header("Assist Settings")]
    [Tooltip("マウスオーバー時に受け入れ牌を表示するか")]
    public bool ShowUkeireAssist = true;

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
    [Header("Physics & Visual Effects")]
    [Tooltip("役満演出時の爆発力")]
    public float YakumanExplosionForce = 8000f;
    [Tooltip("役満演出時の爆発半径")]
    public float YakumanExplosionRadius = 30.0f;
    [Tooltip("役満演出時の上方向への持ち上げ力")]
    public float YakumanUpwardsModifier = 3.0f;
    [Tooltip("役満演出時の回転力")]
    public float YakumanTorque = 100f;

    [Tooltip("通常演出時の横方向のばらつき")]
    public float NormalImpactSpread = 5f;
    [Tooltip("通常演出時の上方向への力")]
    public float NormalUpForce = 15f;
    [Tooltip("通常演出時の奥方向への力")]
    public float NormalZForce = 5f;
    [Tooltip("通常演出時の回転力")]
    public float NormalTorque = 10f;

    [Header("Tsumo Tile Physics")]
    [Tooltip("ツモ牌の質量（通常時）")]
    public float TsumoMassNormal = 1.0f;
    [Tooltip("ツモ牌の質量（役満時）")]
    public float TsumoMassYakuman = 5.0f;
    [Tooltip("ツモ牌が落下を開始する高さ")]
    public float TsumoDropHeight = 125.0f;

    [Header("Hand Tile Physics")]
    [Tooltip("手牌の質量（吹き飛びやすさに関係）")]
    public float HandTileMass = 0.5f;
    [Tooltip("手牌の空気抵抗（小さいほど遠くまで飛ぶ）")]
    public float HandTileDamping = 0.05f;

    [Header("Yakuman Prob")]
    [Tooltip("大三元が出る確率%（0-100の範囲）")]
    public int DaisangenProb = 0;
    [Tooltip("九蓮宝燈が出る確率%（0-100）")]
    public int ChurenProb = 0;
    [Tooltip("国士無双が出る確率 KokushiProb %")]
    public int KokushiProb = 0;
    [Tooltip("大四喜が出る確率 DaiSuShiProb %")]
    public int DaiSuShiProb = 0;
    [Tooltip("緑一色が出る確率 Prob %")]
    public int AllGreenProb = 0;
    [Tooltip("字一色が出る確率 Prob %")]
    public int TsuISoProb = 0;

    [Header("River Setting")]
    [Tooltip("河の長さ")]
    public int RiverLength = 6;
}
