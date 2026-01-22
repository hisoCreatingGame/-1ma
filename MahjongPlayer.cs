using UnityEngine;
using System.Collections; 
using System.Collections.Generic;
using System.Linq;

public class MahjongPlayer : MonoBehaviour
{
    public int Seat { get; set; }
    public int Score { get; set; }
    
    public List<MahjongTile> HandTiles { get; private set; } = new List<MahjongTile>();
    public MahjongTile TsumoTile { get; set; }
    public List<MahjongTile> MeldTiles { get; private set; } = new List<MahjongTile>();
    public List<int> AvailableAnkanTiles { get; private set; } = new List<int>();

    public bool IsRiichi { get; set; }
    public List<int> DiscardHistory { get; private set; } = new List<int>();
    
    public bool IsDoubleRiichi { get; set; }
    public bool IsIppatsuChance { get; set; }

    public List<int> CurrentWaitingTiles { get; private set; } = new List<int>();
    public bool IsFuriten { get; private set; } = false;

    public bool IsRiichiPending { get; private set; } = false;
    public bool IsAutoSortEnabled { get; set; } = false;
    public bool CanRon { get; private set; } = false;
    public int CurrentShanten { get; private set; } = 8;
    
    public bool IsHuman = false;

    public HashSet<string> ActiveSpecialTriggers { get; private set; } = new HashSet<string>();

    private bool _isHandDirty = true; 
    private bool _isWaitingForRiichiAnkan = false;
    private bool _isRinshanChance = false;
    private bool _isFirstTurn = true;
    
    public bool[] DiscardedFlags { get; private set; } = new bool[37];
    private bool _hasAutoDiscarded = false;

    [Header("Settings")]
    private float tileWidth = 4.1f;  
    private float tileHeight = 5.4f;
    private float tsumoGap = 1.0f;
    private float meldGap = 2.0f;

    private MahjongTile _draggingTile;
    private float _lastSwapTime;
    private int _discardCount = 0;

    public void Initialize(int seatIndex, bool isHuman)
    {
        Seat = seatIndex;
        IsHuman = isHuman;
        
        if (MahjongGameManager.Instance != null && MahjongGameManager.Instance.config != null)
        {
            Score = MahjongGameManager.Instance.config.InitialScore;
        }
        else
        {
            Score = 25000;
        }

        _discardCount = 0;
        IsDoubleRiichi = false;
        IsIppatsuChance = false;
        _isFirstTurn = true;
        
        float dist = 40f;
        if (seatIndex == 0)
        {
            transform.position = new Vector3(0, 0, -dist);
            transform.rotation = Quaternion.identity;
        }
        else if (seatIndex == 1)
        {
            transform.position = new Vector3(dist, 0, 0);
            transform.rotation = Quaternion.Euler(0, -90, 0);
        }
        else if (seatIndex == 2)
        {
            transform.position = new Vector3(0, 0, dist);
            transform.rotation = Quaternion.Euler(0, 180, 0);
        }
        else if (seatIndex == 3)
        {
            transform.position = new Vector3(-dist, 0, 0);
            transform.rotation = Quaternion.Euler(0, 90, 0);
        }
    }

    private void Update()
    {
        if (_isHandDirty)
        {
            if (IsHuman) 
            {
                if (IsAutoSortEnabled && _draggingTile == null) SortHandTiles();
                
                // ★追加: 手牌が変わったタイミングでトリガーを確認
                CheckCurrentTriggers();

                CalculateShantenAndAnkan();
                CheckFuriten();

                if (IsRiichi && TsumoTile != null && !_hasAutoDiscarded)
                {
                    bool canAnkan = false;
                    int tsumoId = TsumoTile.TileId;
                    int normalizedTsumo = GetNormalizedTileId(tsumoId);
                    if (AvailableAnkanTiles.Contains(normalizedTsumo)) canAnkan = true;

                    if (canAnkan)
                    {
                        _isWaitingForRiichiAnkan = true;
                    }
                    else
                    {
                        _isWaitingForRiichiAnkan = false;
                        StartCoroutine(AutoDiscardRoutine());
                    }
                }
            }
            _isHandDirty = false;
        }
        UpdateTilePositions();
    }

    private void CheckCurrentTriggers()
    {
        // セットをクリアして再評価
        ActiveSpecialTriggers.Clear();

        if (HandTiles == null || HandTiles.Count == 0) return;

        // --- トリガー1: 「中」が一番左（インデックス0）にある ---
        if (HandTiles.Count > 0 && HandTiles[0] != null)
        {
            // 33 = 中
            if (HandTiles[(HandTiles.Count - 1) >> 1].TileId == 33)
            {
                ActiveSpecialTriggers.Add("CENTER_CHUN");
                // Debug.Log("Trigger Active: 左端が中です");
            }
        }

    }

public List<string> GetActiveTriggersForWin()
    {
        // 念のため最新の状態をチェック
        CheckCurrentTriggers();
        return ActiveSpecialTriggers.ToList();
    }

    // ... (その他のメソッドは変更なし、RequestTsumoのみ修正して掲載) ...

    public void RequestTsumo()
    {
        int[] tileCounts = new int[37];
        foreach (var tile in HandTiles) if(tile) 
        {
            int tid = GetNormalizedTileId(tile.TileId);
            if(tid < 34) tileCounts[tid]++;
        }
        if (TsumoTile != null) 
        {
            int tid = GetNormalizedTileId(TsumoTile.TileId);
            if(tid < 34) tileCounts[tid]++;
        }

        List<int> meldIds = new List<int>();
        foreach(var tile in MeldTiles) if(tile) meldIds.Add(GetNormalizedTileId(tile.TileId));

        ScoringContext context = new ScoringContext();
        context.IsFirstTurn = _isFirstTurn;
        context.IsTsumo = true;
        context.IsRiichi = IsRiichi;
        context.IsDealer = (Seat == 0);
        
        // ★修正: 暗槓しか存在しないモードなので、常にメンゼン扱いにする
        context.IsMenzen = true; 
        
        context.RedDoraCount = GetRedDoraCount();

        if (TsumoTile != null) context.WinningTileId = GetNormalizedTileId(TsumoTile.TileId);
        
        context.IsRinshan = _isRinshanChance;
        context.SeatWind = Seat;
        context.RoundWind = 0;
        context.DoraTiles = new List<int>();

        // ★修正: ここで現在のアクティブなトリガーをコンテキストに渡す
        context.SpecialYakuTriggers = GetActiveTriggersForWin();

        ScoringResult result = MahjongLogic.CalculateScore(tileCounts, meldIds, context); 

        if (MahjongGameManager.Instance != null)
        {
            MahjongGameManager.Instance.OnTsumoRequested(this, result.TotalScore, result.YakuList.ToArray());
        }
    }
    private void CalculateShantenAndAnkan()
    {
        int[] tileCounts = new int[37];
        foreach (var tile in HandTiles)
        {
            if(tile == null) continue;
            int tid = GetNormalizedTileId(tile.TileId);
            if (tid >= 0 && tid < 34) tileCounts[tid]++;
        }
        if (TsumoTile != null)
        {
            int tid = GetNormalizedTileId(TsumoTile.TileId);
            if (tid >= 0 && tid < 34) tileCounts[tid]++;
        }

        int meldCount = MeldTiles.Count / 4;
        CurrentShanten = MahjongLogic.CalculateShanten(tileCounts, meldCount);

        int[] rawCounts = new int[37];
        foreach (var tile in HandTiles) if(tile) rawCounts[tile.TileId]++;
        if (TsumoTile) rawCounts[TsumoTile.TileId]++;
        
        AvailableAnkanTiles = new List<int>();
        for(int i=0; i<37; i++) if(rawCounts[i] == 4) AvailableAnkanTiles.Add(i);
        CheckRedAnkan(rawCounts, 4, 34);  // Manzu
        CheckRedAnkan(rawCounts, 13, 35); // Pinzu
        CheckRedAnkan(rawCounts, 22, 36); // Sozu
    }

    private void CheckRedAnkan(int[] counts, int normalId, int redId)
    {
        if (counts[redId] == 1 && counts[normalId] == 3) AvailableAnkanTiles.Add(normalId);
    }

    private int GetNormalizedTileId(int id)
    {
        if (id == 34) return 4;
        if (id == 35) return 13;
        if (id == 36) return 22;
        return id;
    }

    private void CheckFuriten()
    {
        IsFuriten = false;
        CurrentWaitingTiles.Clear();

        int[] tileCounts = new int[34];
        foreach (var tile in HandTiles)
        {
            if(tile == null) continue;
            int tid = GetNormalizedTileId(tile.TileId);
            if (tid >= 0 && tid < 34) tileCounts[tid]++;
        }

        int meldCount = MeldTiles.Count / 4;
        int shanten13 = MahjongLogic.CalculateShanten(tileCounts, meldCount);

        if (shanten13 == 0)
        {
            CurrentWaitingTiles = MahjongLogic.GetWinningTiles(tileCounts, meldCount);
            foreach (var waitingTile in CurrentWaitingTiles)
            {
                if (waitingTile >= 0 && waitingTile < 37)
                {
                    if (CheckIfDiscarded(waitingTile))
                    {
                        IsFuriten = true;
                        break;
                    }
                }
            }
        }
    }

    private bool CheckIfDiscarded(int waitingTileNormalId)
    {
        if (DiscardedFlags[waitingTileNormalId]) return true;
        if (waitingTileNormalId == 4 && DiscardedFlags[34]) return true;
        if (waitingTileNormalId == 13 && DiscardedFlags[35]) return true;
        if (waitingTileNormalId == 22 && DiscardedFlags[36]) return true;
        return false;
    }

    public void RegisterDiscard(int tileId)
    {
        if (tileId >= 0 && tileId < 37) DiscardedFlags[tileId] = true;
    }

    public void ClearDiscardFlags()
    {
        for (int i = 0; i < 37; i++) DiscardedFlags[i] = false;
    }

    public void ChangeScore(int delta) { Score += delta; }
    public void AddToDiscardHistory(int tileId) { DiscardHistory.Add(tileId); }
    public void ClearDiscardHistory() { DiscardHistory.Clear(); }

    // ★修正: フーロ（暗槓）時の位置ズレ防止とTsumoSlotの確保
    private void UpdateTilePositions()
    {
        // 手牌の幅
        float handWidth = 0f;
        if (HandTiles.Count > 0) handWidth += HandTiles.Count * tileWidth;

        // フーロ牌の幅
        float meldsWidth = 0f;
        int meldSetCount = MeldTiles.Count / 4;
        if (meldSetCount > 0)
        {
            meldsWidth += MeldTiles.Count * tileWidth;
        }

        // 全体の幅計算
        // 暗槓などフーロがある場合は、「手牌」＋「ツモ牌用スロット」＋「フーロ牌」の構成にする
        // これにより、ツモ牌の有無に関わらずフーロ牌の位置が固定される
        float totalVisualWidth = handWidth;
        float tsumoSlotSize = tsumoGap + tileWidth;

        if (meldSetCount > 0)
        {
            // フーロがあるなら、ツモ牌が入るための隙間とフーロ用隙間を常に確保する
            totalVisualWidth += tsumoSlotSize + meldGap + meldsWidth;
        }

        float startX = -totalVisualWidth / 2.0f + (tileWidth / 2.0f);
        float currentX = startX;

        // 1. 手牌配置
        for (int i = 0; i < HandTiles.Count; i++)
        {
            if(HandTiles[i] != null)
                MoveTileToTarget(HandTiles[i], currentX, false, false);
            currentX += tileWidth;
        }

        // 2. ツモ牌配置
        // ツモ牌がある場合は、手牌の右隣（gap分空けて）に配置
        // ただし currentX は手牌の直後を指しているので、gapを加算する
        if (TsumoTile != null)
        {
            MoveTileToTarget(TsumoTile, startX + handWidth + tsumoGap, false, false);
        }

        // 3. フーロ牌配置
        if (meldSetCount > 0)
        {
            // フーロ牌の開始位置は、「手牌の幅」+「ツモ用スロット(常に確保)」+「フーロ用ギャップ」の後ろ
            float meldStartX = startX + handWidth + tsumoSlotSize + meldGap;
            
            for (int m = 0; m < meldSetCount; m++)
            {
                int visualIndex = (meldSetCount - 1) - m;
                float setBaseX = meldStartX + (visualIndex * 4 * tileWidth);

                for (int i = 0; i < 4; i++)
                {
                    int tileIndex = (m * 4) + i; 
                    if (tileIndex < MeldTiles.Count && MeldTiles[tileIndex] != null)
                    {
                        var tileObj = MeldTiles[tileIndex];
                        bool isFaceDown = (i == 0 || i == 3);
                        float tilePos = setBaseX + (i * tileWidth);
                        Vector3 localOffset = new Vector3(tilePos, tileHeight / 2, 0);
                        ApplyTransform(tileObj.transform, localOffset, isFaceDown, true);
                    }
                }
            }
        }
    }

    private void MoveTileToTarget(MahjongTile tile, float localX, bool faceDown, bool isMeld)
    {
        if (tile != null)
        {
            Vector3 localOffset = new Vector3(localX, tileHeight / 2, 0);
            ApplyTransform(tile.transform, localOffset, faceDown, isMeld);
        }
    }

    private void ApplyTransform(Transform t, Vector3 localOffset, bool faceDown, bool isMeld)
    {
        Vector3 targetPos = transform.position + (transform.rotation * localOffset);
        Quaternion baseRot = transform.rotation; 
        Quaternion tileRot;

        if (isMeld)
        {
            if (faceDown) tileRot = baseRot * Quaternion.Euler(90, 0, 0);
            else tileRot = baseRot * Quaternion.Euler(-90, 0, 180);
        }
        else
        {
            if (IsHuman) tileRot = baseRot * Quaternion.Euler(0, 180, 0);
            else tileRot = baseRot * Quaternion.Euler(90, 0, 0); 
        }

        t.position = Vector3.Lerp(t.position, targetPos, Time.deltaTime * 25f);
        t.rotation = Quaternion.Slerp(t.rotation, tileRot, Time.deltaTime * 25f);
    }

    public void ToggleAutoSort()
    {
        IsAutoSortEnabled = !IsAutoSortEnabled;
        SortHandTiles();
        _isHandDirty = true;
    }

    private void SortHandTiles()
    {
        HandTiles.Sort((a, b) => 
        {
            if (a == null || b == null) return 0;
            int weightA = GetSortWeight(a.TileId);
            int weightB = GetSortWeight(b.TileId);
            if (weightA != weightB) return weightA.CompareTo(weightB);
            return a.TileId.CompareTo(b.TileId);
        });
    }

    private int GetSortWeight(int id)
    {
        if (id == 34) return 4;
        if (id == 35) return 13;
        if (id == 36) return 22;
        return id;
    }

    public void OnTileDragging(MahjongTile tile, float mouseWorldX)
    {
        if (!IsHuman) return;
        if (IsRiichi) return; 
        if (TsumoTile == tile) return; 
        if (tile != _draggingTile) return;
        if (Time.time < _lastSwapTime + 0.05f) return;

        Vector3 worldMousePos = new Vector3(mouseWorldX, transform.position.y, transform.position.z);
        Vector3 localMousePos = transform.InverseTransformPoint(worldMousePos);

        float startX = -((HandTiles.Count - 1) * tileWidth) / 2;
        int targetIndex = Mathf.FloorToInt((localMousePos.x - startX) / tileWidth + 0.5f);
        targetIndex = Mathf.Clamp(targetIndex, 0, HandTiles.Count - 1);

        int currentIndex = HandTiles.IndexOf(tile);
        if (currentIndex != -1 && targetIndex != currentIndex)
        {
            MahjongTile movingTile = HandTiles[currentIndex];
            HandTiles.RemoveAt(currentIndex);
            HandTiles.Insert(targetIndex, movingTile);
            _lastSwapTime = Time.time;
            _isHandDirty = true; 
        }
    }

    public void StartDraggingTile(MahjongTile tile) { if(IsHuman) { _draggingTile = tile; _isHandDirty = true; } }
    public void StopDraggingTile(MahjongTile tile) { if (_draggingTile == tile) _draggingTile = null; }

    public void SetRiichiPending(bool pending)
    {
        if (IsHuman && !IsRiichi) IsRiichiPending = pending;
    }

    public void OnAnyMeldOccurred()
    {
        IsIppatsuChance = false;
        _isFirstTurn = false;
    }


    public void CheckRonAvailability()
    {
        CanRon = false;
        var mgr = MahjongGameManager.Instance;
        if (mgr == null) return;
        
        if (mgr.LastDiscardSeat == -1 || mgr.LastDiscardSeat == Seat) return;
        if (IsFuriten) return;

        int discardTile = mgr.LastDiscardTileId;
        int discardNormalId = GetNormalizedTileId(discardTile);

        if (CurrentWaitingTiles.Contains(discardNormalId)) CanRon = true;
    }

    public void RequestRon()
    {
        if (CanRon)
        {
            if (MahjongGameManager.Instance != null) MahjongGameManager.Instance.OnRonRequested(this);
            CanRon = false;
        }
    }

    public void RequestDiscard(MahjongTile tileObj)
    {
        _discardCount++;
        bool isDeclaringRiichi = false; 

        if (IsRiichiPending)
        {
            IsRiichi = true;
            IsIppatsuChance = true; 
            if (_discardCount == 1 && MeldTiles.Count == 0) IsDoubleRiichi = true;
            isDeclaringRiichi = true; 
        }

        _isFirstTurn = false;
        _isRinshanChance = false;
        
        if (IsRiichi) IsIppatsuChance = false; 

        IsRiichiPending = false;
        
        if (MahjongGameManager.Instance != null)
        {
            MahjongGameManager.Instance.OnDiscardRequested(this, tileObj, isDeclaringRiichi);
        }
    }

    public void AddTileToHand(MahjongTile tile) { HandTiles.Add(tile); _isHandDirty = true; }
    
    public void SetTsumoTile(MahjongTile tile) 
    { 
        TsumoTile = tile; 
        _isHandDirty = true;
        _hasAutoDiscarded = false; 
    }

    public bool RemoveTileFromHand(MahjongTile tile) { bool r = HandTiles.Remove(tile); if(r) _isHandDirty = true; return r; }
    
    public void ClearTsumoTile() 
    { 
        TsumoTile = null; 
        _isHandDirty = true; 
    }

    public void DespawnAllTiles()
    {
        foreach (var tile in HandTiles) if (tile != null) Destroy(tile.gameObject);
        HandTiles.Clear();
        if (TsumoTile != null) { Destroy(TsumoTile.gameObject); TsumoTile = null; }
        foreach (var tile in MeldTiles) if (tile != null) Destroy(tile.gameObject);
        MeldTiles.Clear();
    }

    public int GetRedDoraCount()
    {
        int count = 0;
        foreach (var tile in HandTiles) if (tile && IsRedDora(tile.TileId)) count++;
        if (TsumoTile && IsRedDora(TsumoTile.TileId)) count++;
        foreach (var tile in MeldTiles) if (tile && IsRedDora(tile.TileId)) count++;
        return count;
    }

    private bool IsRedDora(int tileId) { return (tileId >= 34 && tileId <= 36); }

    private IEnumerator AutoDiscardRoutine()
    {
        _hasAutoDiscarded = true; 
        yield return new WaitForSeconds(1.0f);
        if (TsumoTile != null && CurrentShanten > -1) RequestDiscard(TsumoTile);
    }

    public void RequestAnkan()
    {
        if (IsHuman && AvailableAnkanTiles.Count > 0)
        {
            int targetTileId = AvailableAnkanTiles[0];
            if (MahjongGameManager.Instance != null) MahjongGameManager.Instance.OnAnkanRequested(this, targetTileId);
        }
    }

    public void SkipRiichiAnkan()
    {
        if (IsHuman && IsRiichi && _isWaitingForRiichiAnkan)
        {
            _isWaitingForRiichiAnkan = false;
            StartCoroutine(AutoDiscardRoutine());
        }
    }

    public void PerformAnkan(int tileType)
    {
        List<MahjongTile> tilesToMove = new List<MahjongTile>();
        bool tsumoUsed = false;
        List<MahjongTile> tempHand = new List<MahjongTile>(HandTiles);
        
        for (int i = tempHand.Count - 1; i >= 0; i--)
        {
            if (tilesToMove.Count >= 4) break;
            int currentId = tempHand[i].TileId;
            int normalizedId = GetNormalizedTileId(currentId);
            if (normalizedId == tileType)
            {
                tilesToMove.Add(tempHand[i]);
                tempHand.RemoveAt(i);
            }
        }

        if (tilesToMove.Count < 4 && TsumoTile != null)
        {
            int normalizedId = GetNormalizedTileId(TsumoTile.TileId);
            if (normalizedId == tileType)
            {
                tilesToMove.Add(TsumoTile);
                TsumoTile = null;
                tsumoUsed = true;
            }
        }

        if (tilesToMove.Count == 4)
        {
            HandTiles.Clear();
            HandTiles.AddRange(tempHand);
            foreach (var tile in tilesToMove) MeldTiles.Add(tile);
            if (!tsumoUsed && TsumoTile != null)
            {
                HandTiles.Add(TsumoTile);
                TsumoTile = null;
            }
            _isHandDirty = true;
        }
    }
}