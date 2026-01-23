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
    public bool IsFirstTurn { get{ return _isFirstTurn; } }

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

    private MahjongTile _hoveredTile;
    private const float HoverYOffset = 0.5f; 

    private HashSet<MahjongTile> _validRiichiDiscardTiles = new HashSet<MahjongTile>();

    // ★追加: リーチ時の待ち牌キャッシュ
    private List<int> _cachedRiichiWaits = null;

    private Dictionary<MahjongTile, List<int>> _cachedDiscardWaitMap = new Dictionary<MahjongTile, List<int>>();
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
            Score = 0;
        }

        _discardCount = 0;
        IsDoubleRiichi = false;
        IsIppatsuChance = false;
        _isFirstTurn = true;
        _cachedRiichiWaits = null; // 初期化
        
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

    public void RequestDiscard(MahjongTile tileObj)
    {
        _discardCount++;
        bool isDeclaringRiichi = false; 

        if (IsRiichiPending)
        {
            if (!_validRiichiDiscardTiles.Contains(tileObj)) return;

            IsRiichi = true;
            IsIppatsuChance = true; 
            if (_discardCount == 1 && MeldTiles.Count == 0) IsDoubleRiichi = true;
            isDeclaringRiichi = true; 

            // ★修正: リーチ確定時、待ちを計算してキャッシュに保存し、UI表示を行う
            // 毎回計算するのではなく、ここで確定させる
            List<int> finalWaits = CalculateWaitsAfterDiscard(tileObj);
            _cachedRiichiWaits = finalWaits; // キャッシュ保存

            // UI表示 (アシストONなら)
            if (MahjongGameManager.Instance != null && 
                MahjongGameManager.Instance.config.ShowUkeireAssist)
            {
                var canvas = FindAnyObjectByType<MahjongCanvas>();
                if (canvas != null) canvas.ShowUkeirePanel(finalWaits);
            }
        }
        else if (IsRiichi)
        {
            IsIppatsuChance = false;
        }

        ResetTileVisuals();

        _isFirstTurn = false;
        _isRinshanChance = false;
        IsRiichiPending = false;
        
        if (MahjongGameManager.Instance != null)
        {
            MahjongGameManager.Instance.OnDiscardRequested(this, tileObj, isDeclaringRiichi);
        }
    }

    // ★追加: 特定の牌を切った後の待ち牌リストを計算して返すヘルパー
    private List<int> CalculateWaitsAfterDiscard(MahjongTile discardTile)
    {
        // 手牌(14枚)から対象を抜いて、残り13枚での待ちを計算
        int[] counts = new int[34];
        foreach (var t in HandTiles) if(t && t != discardTile) counts[GetNormalizedTileId(t.TileId)]++;
        if (TsumoTile && TsumoTile != discardTile) counts[GetNormalizedTileId(TsumoTile.TileId)]++;
        
        // 赤ドラなどは正規化済みIDに加算されている前提
        
        int meldCount = MeldTiles.Count / 4;
        return MahjongLogic.GetEffectiveTiles(counts, meldCount);
    }

    public void SetRiichiPending(bool pending)
    {
        if (IsHuman && !IsRiichi) 
        {
            IsRiichiPending = pending;

            if (pending)
            {
                // ★ここで「どの牌を切れるか」＋「その時の待ちは何か」を一括計算してキャッシュする
                PrecalculateRiichiCandidatesAndWait();
                
                // ビジュアル更新（暗くする処理）
                UpdateRiichiSelectionVisuals();
            }
            else
            {
                // キャンセル時
                ResetTileVisuals();
                var canvas = FindAnyObjectByType<MahjongCanvas>();
                if (canvas != null) canvas.HideUkeirePanel();
                
                // キャッシュクリア
                _cachedDiscardWaitMap.Clear();
            }
        }
    }

    private void PrecalculateRiichiCandidatesAndWait()
    {
        _validRiichiDiscardTiles.Clear();
        _cachedDiscardWaitMap.Clear();

        // 1. 現在の手牌構成（14枚）のカウント配列を作成
        int[] totalCounts = new int[34];
        foreach (var t in HandTiles) 
        {
            if(t != null) totalCounts[GetNormalizedTileId(t.TileId)]++;
        }
        if (TsumoTile != null) 
        {
            totalCounts[GetNormalizedTileId(TsumoTile.TileId)]++;
        }
        
        int meldCount = MeldTiles.Count / 4;

        // 2. 手牌の各牌について「これを切ったらどうなるか？」をシミュレーション
        foreach (var tile in HandTiles)
        {
            CalculateAndCacheWait(tile, totalCounts, meldCount);
        }

        // 3. ツモ牌についても同様に
        if (TsumoTile != null)
        {
            CalculateAndCacheWait(TsumoTile, totalCounts, meldCount);
        }
    }

    // ★追加ヘルパー: 指定した牌を切った場合の待ちを計算し、有効ならキャッシュに登録
    private void CalculateAndCacheWait(MahjongTile discardCandidate, int[] totalCounts, int meldCount)
    {
        int tid = GetNormalizedTileId(discardCandidate.TileId);
        
        // カウント配列から一時的に減らす（打牌シミュレーション）
        if (tid < 34 && totalCounts[tid] > 0)
        {
            totalCounts[tid]--;

            // この状態で有効牌（待ち）があるか計算
            // GetEffectiveTiles はシャンテン数が下がる牌を返すが、
            // テンパイ時(シャンテン0)にこれを呼ぶと「上がり牌(-1になる牌)」が返ってくる仕様を利用
            
            // まずテンパイしているか(シャンテン0以下か)を確認
            int shanten = MahjongLogic.CalculateShanten(totalCounts, meldCount);
            
            if (shanten <= 0)
            {
                // テンパイ（または既に上がっている）なら、待ち牌を取得
                List<int> waits = MahjongLogic.GetEffectiveTiles(totalCounts, meldCount);
                
                if (waits != null && waits.Count > 0)
                {
                    // 有効なリーチ打牌として登録
                    _validRiichiDiscardTiles.Add(discardCandidate);
                    
                    // ★待ちリストをキャッシュに保存
                    _cachedDiscardWaitMap[discardCandidate] = waits;
                }
            }

            totalCounts[tid]++; // 配列を元に戻す
        }
    }


    private bool CheckDiscardForTenpai(MahjongTile tile, int[] counts, int meldCount)
    {
        int tid = GetNormalizedTileId(tile.TileId);
        if(tid < 34 && counts[tid] > 0)
        {
            counts[tid]--; 
            int shanten = MahjongLogic.CalculateShanten(counts, meldCount);
            counts[tid]++; 
            return (shanten <= 0); 
        }
        return false;
    }

    public void OnTileHoverEnter(MahjongTile tile)
    {
        if (!IsHuman) return;
        if (IsRiichi) return; 
        
        _hoveredTile = tile;
        _isHandDirty = true; 

        // リーチ宣言待機中、かつアシストONの場合
        if (IsRiichiPending &&
            MahjongGameManager.Instance != null && 
            MahjongGameManager.Instance.config.ShowUkeireAssist)
        {
             // ★修正: 計算せず、キャッシュから取得して表示
             if (_cachedDiscardWaitMap.ContainsKey(tile))
             {
                 var waits = _cachedDiscardWaitMap[tile];
                 var canvas = FindAnyObjectByType<MahjongCanvas>();
                 if (canvas != null)
                 {
                     canvas.ShowUkeirePanel(waits);
                 }
             }
             else
             {
                 // 有効な打牌ではない（テンパイしない）場合はパネルを消す
                 var canvas = FindAnyObjectByType<MahjongCanvas>();
                 if (canvas != null) canvas.HideUkeirePanel();
             }
        }
    }
    private void UpdateRiichiSelectionVisuals()
    {
        foreach(var tile in HandTiles)
        {
            if(tile) tile.SetDarkened(!_validRiichiDiscardTiles.Contains(tile));
        }
        if(TsumoTile) TsumoTile.SetDarkened(!_validRiichiDiscardTiles.Contains(TsumoTile));
    }

    private void ResetTileVisuals()
    {
        foreach(var tile in HandTiles) if(tile) tile.SetDarkened(false);
        if(TsumoTile) TsumoTile.SetDarkened(false);
    }

    public void UpdateRiichiWaitDisplay()
    {
        // 廃止（RequestDiscard内で直接処理するため）
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
            
            _isFirstTurn = false;
            if (IsRiichi) IsIppatsuChance = false; 
            _isRinshanChance = true;
            
            _isHandDirty = true;
        }
    }

    public void DespawnAllTiles()
    {
        foreach (var tile in HandTiles)
        {
            if (tile != null && tile.gameObject != null) Destroy(tile.gameObject);
        }
        HandTiles.Clear();

        foreach (var tile in MeldTiles)
        {
            if (tile != null && tile.gameObject != null) Destroy(tile.gameObject);
        }
        MeldTiles.Clear();

        if (TsumoTile != null && TsumoTile.gameObject != null)
        {
            Destroy(TsumoTile.gameObject);
            TsumoTile = null;
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

        if (IsRiichi && AvailableAnkanTiles.Count > 0)
        {
            // ★修正: リーチ中の暗槓チェックで、キャッシュがあればそれを利用して計算をスキップ
            List<int> waitsBefore;

            if (_cachedRiichiWaits != null)
            {
                waitsBefore = _cachedRiichiWaits;
            }
            else
            {
                // 万が一キャッシュがない場合は計算（通常通らない）
                int[] hand13Counts = new int[34];
                foreach (var tile in HandTiles)
                {
                    if(tile)
                    {
                        int tid = GetNormalizedTileId(tile.TileId);
                        if (tid < 34) hand13Counts[tid]++;
                    }
                }
                waitsBefore = MahjongLogic.GetWinningTiles(hand13Counts, meldCount);
            }

            List<int> toRemove = new List<int>();

            foreach(int kanId in AvailableAnkanTiles)
            {
                int normalId = GetNormalizedTileId(kanId);
                
                int[] hand10Counts = (int[])tileCounts.Clone();
                if(normalId < 34) hand10Counts[normalId] -= 4;

                List<int> waitsAfter = MahjongLogic.GetWinningTiles(hand10Counts, meldCount + 1);

                bool isWaitChanged = false;
                
                if (waitsBefore.Count != waitsAfter.Count)
                {
                    isWaitChanged = true;
                }
                else
                {
                    if (waitsBefore.Except(waitsAfter).Any()) isWaitChanged = true;
                }

                if (isWaitChanged)
                {
                    toRemove.Add(kanId);
                }
            }

            foreach(int rm in toRemove) AvailableAnkanTiles.Remove(rm);
        }
    }
    private void CheckCurrentTriggers()
    {
        ActiveSpecialTriggers.Clear();

        if (HandTiles == null || HandTiles.Count == 0) return;

        if (HandTiles.Count > 0 && HandTiles[0] != null)
        {
            if (HandTiles[(HandTiles.Count - 1) >> 1].TileId == 33)
            {
                ActiveSpecialTriggers.Add("CENTER_CHUN");
            }
        }
    }

    public List<string> GetActiveTriggersForWin()
    {
        CheckCurrentTriggers();
        return ActiveSpecialTriggers.ToList();
    }

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
        context.IsMenzen = true; 
        context.RedDoraCount = GetRedDoraCount();
        if (TsumoTile != null) context.WinningTileId = GetNormalizedTileId(TsumoTile.TileId);
        context.IsRinshan = _isRinshanChance;
        context.SeatWind = Seat;
        context.RoundWind = 0;
        context.DoraTiles = new List<int>();
        context.SpecialYakuTriggers = GetActiveTriggersForWin();

        ScoringResult result = MahjongLogic.CalculateScore(tileCounts, meldIds, context); 

        if (MahjongGameManager.Instance != null)
        {
            MahjongGameManager.Instance.OnTsumoRequested(this, result.TotalScore, result.YakuList.ToArray());
        }
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

        // ★修正: リーチ中かつキャッシュがあればそれを使う（重い計算をスキップ）
        if (IsRiichi && _cachedRiichiWaits != null)
        {
            CurrentWaitingTiles = new List<int>(_cachedRiichiWaits);
        }
        else
        {
            // 通常の計算
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
            }
        }

        // 共通のフリテンチェック処理
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

    public void OnTileHoverExit(MahjongTile tile)
    {
        if (_hoveredTile == tile)
        {
            _hoveredTile = null;
            _isHandDirty = true;

            var canvas = FindAnyObjectByType<MahjongCanvas>();
            if (canvas != null) canvas.HideUkeirePanel();
        }
    }

    private void CalculateAndShowUkeire(MahjongTile discardCandidate)
    {
        // UI表示用のヘルパー
        List<int> effectiveTiles = CalculateWaitsAfterDiscard(discardCandidate);

        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
            canvas.ShowUkeirePanel(effectiveTiles);
        }
    }

    private void UpdateTilePositions()
    {
        if (HandTiles.Count == 0 && TsumoTile == null && MeldTiles.Count == 0) return;

        float handWidth = 0f;
        if (HandTiles.Count > 0) handWidth += HandTiles.Count * tileWidth;

        float meldsWidth = 0f;
        int meldSetCount = MeldTiles.Count / 4;
        if (meldSetCount > 0)
        {
            meldsWidth += MeldTiles.Count * tileWidth;
        }

        float totalVisualWidth = handWidth;
        float tsumoSlotSize = tsumoGap + tileWidth;

        if (meldSetCount > 0)
        {
            totalVisualWidth += tsumoSlotSize + meldGap + meldsWidth;
        }

        float startX = -totalVisualWidth / 2.0f + (tileWidth / 2.0f);
        float currentX = startX;

        for (int i = 0; i < HandTiles.Count; i++)
        {
            if(HandTiles[i] != null)
            {
                var rb = HandTiles[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                float yPos = (HandTiles[i] == _hoveredTile) ? HoverYOffset : 0f;
                MoveTileToTarget(HandTiles[i], currentX, yPos, false, false);
            }
            currentX += tileWidth;
        }

        if (TsumoTile != null)
        {
            var rb = TsumoTile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            float yPos = (TsumoTile == _hoveredTile) ? HoverYOffset : 0f;
            MoveTileToTarget(TsumoTile, startX + handWidth + tsumoGap, yPos, false, false);
        }

        if (meldSetCount > 0)
        {
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
                        
                        var rb = tileObj.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.isKinematic = true;
                            rb.useGravity = false;
                        }

                        bool isFaceDown = (i == 0 || i == 3);
                        float tilePos = setBaseX + (i * tileWidth);
                        Vector3 localOffset = new Vector3(tilePos, tileHeight / 2, 0);
                        ApplyTransform(tileObj.transform, localOffset, isFaceDown, true);
                    }
                }
            }
        }
    }

    private void MoveTileToTarget(MahjongTile tile, float localX, float localY, bool faceDown, bool isMeld)
    {
        if (tile != null)
        {
            Vector3 localOffset = new Vector3(localX, tileHeight / 2 + localY, 0);
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
}