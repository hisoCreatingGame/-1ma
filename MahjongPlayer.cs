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

    public bool IsRinshanChance { get { return _isRinshanChance; } }
    private bool _isRinshanChance = false;
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
    public bool IsFirstTurn { get{ return _isFirstTurn; } }

    private bool _isFirstTurn = true;
    
    public bool[] DiscardedFlags { get; private set; } = new bool[37];
    private bool _hasAutoDiscarded = false;

    public bool IsMenzen { get { return !_isOpenHand; } }
    private bool _isOpenHand = false;

    [Header("Settings")]
    private float tileWidth = 4.1f;  
    private float tileHeight = 5.4f;
    private float tsumoGap = 1.0f;
    private float meldGap = 2.0f;

    private MahjongTile _draggingTile;
    private float _lastSwapTime;
    private int _discardCount = 0;

    private MahjongTile _hoveredTile;
    private const float HoverYOffset = 1.0f;

    private bool _isDiscardProcessing = false;

    private HashSet<MahjongTile> _validRiichiDiscardTiles = new HashSet<MahjongTile>();

    // ★追加: リーチ時の待ち牌キャッシュ
    private List<int> _cachedRiichiWaits = null;

    private Dictionary<MahjongTile, List<int>> _cachedDiscardWaitMap = new Dictionary<MahjongTile, List<int>>();

    // ★追加: 捨て牌ごとの待ち牌リストをキャッシュする辞書
    // Key: 捨てる牌のID, Value: その牌を捨てた時の待ち牌(和了牌)リスト
    private Dictionary<int, List<int>> _cachedWaits = new Dictionary<int, List<int>>();
    
    // ★追加: 計算中のコルーチン保持用
    private Coroutine _calculationCoroutine;

    // ★追加: 新しくツモったばかりかを判定するフラグ
    private bool _isNewTsumo = false;

    // ★追加: 確定した役満を保持するリストと、1巡1回の制限フラグ
    public HashSet<string> GuaranteedYakuman { get; private set; } = new HashSet<string>();
    private bool _gimmickTriggeredThisTurn = false;
    public void Initialize(int seatIndex, bool isHuman, int initialScore)
    {
        Seat = seatIndex;
        IsHuman = isHuman;
        
    Debug.Log($"[Player.Initialize] Received initialScore: {initialScore}");

    Score = initialScore;

    // ★デバッグ推奨3: 代入後の Score を確認
    Debug.Log($"[Player.Initialize] Score set to: {Score}");

        _discardCount = 0;
        IsDoubleRiichi = false;
        IsIppatsuChance = false;
        _isFirstTurn = true;
        _cachedRiichiWaits = null; // 初期化
        _isDiscardProcessing = false;

        GuaranteedYakuman.Clear();
        
        float dist = 45f;
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

                    if (MahjongGameManager.Instance != null && MahjongGameManager.Instance.TilesRemainingInWall <= 0)
                    {
                        canAnkan = false;
                    }

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

    // ... (前略)
    // ★追加: ターンの開始時（ツモった時）にこれを呼んで、バックグラウンドで待ち計算を開始する
    public void RefreshWaitsAsync()
    {
        if (_calculationCoroutine != null) StopCoroutine(_calculationCoroutine);
        _calculationCoroutine = StartCoroutine(CalculateWaitsRoutine());
    }
    private IEnumerator CalculateWaitsRoutine()
    {
        _cachedWaits.Clear();

        // 1. 計算用の手牌カウント配列を作成
        int[] currentTiles = new int[37];
        foreach (var t in HandTiles) if (t) currentTiles[GetNormalizedTileId(t.TileId)]++;
        if (TsumoTile != null) currentTiles[GetNormalizedTileId(TsumoTile.TileId)]++;

        int meldCount = MeldTiles.Count / 4; 

        // 2. 捨てる候補の牌をリストアップ
        HashSet<int> uniqueHandTiles = new HashSet<int>();
        foreach(var t in HandTiles) if(t) uniqueHandTiles.Add(GetNormalizedTileId(t.TileId));
        if(TsumoTile != null) uniqueHandTiles.Add(GetNormalizedTileId(TsumoTile.TileId));

        // ★タイムスライス用のストップウォッチ開始
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        long frameBudgetMs = 3; // 1フレームあたりに使っていい時間（ミリ秒）。3ms程度なら60FPSを維持できます。

        foreach (int discardId in uniqueHandTiles)
        {
            if (discardId >= 34) continue; 

            // 仮想的に1枚捨てる
            if (currentTiles[discardId] > 0)
            {
                currentTiles[discardId]--;

                // A. テンパイしているか確認（シャンテン計算）
                int shanten = MahjongLogic.CalculateShanten(currentTiles, meldCount);

                // ★時間チェック: 処理時間が長引いていたら一旦休憩
                if (sw.ElapsedMilliseconds > frameBudgetMs)
                {
                    yield return null; // 次のフレームまで待機
                    sw.Restart();      // 時間計測リセット
                }

                if (shanten <= 0)
                {
                    // B. 待ち牌の計算 (GetEffectiveTilesの中身をここで展開して、細かく休憩できるようにする)
                    List<int> waits = new List<int>();

                    for (int i = 0; i < 34; i++)
                    {
                        // 1枚加えてシャンテン数が下がるか試す
                        currentTiles[i]++;
                        int nextShanten = MahjongLogic.CalculateShanten(currentTiles, meldCount);
                        currentTiles[i]--;

                        if (nextShanten < shanten)
                        {
                            waits.Add(i);
                        }

                        // ★重要: ループの途中でも、時間がかかりすぎていたら休憩
                        if (sw.ElapsedMilliseconds > frameBudgetMs)
                        {
                            yield return null;
                            sw.Restart();
                        }
                    }

                    if (waits.Count > 0)
                    {
                        _cachedWaits[discardId] = waits;
                    }
                }

                // 戻す
                currentTiles[discardId]++;
            }
        }
    }


    public void OnTileHoverExit(MahjongTile tile)
    {
        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
            // パネルを非表示にする（nullを渡して非表示にする前提）
            canvas.ShowUkeirePanel(null); 
        }

        if (_hoveredTile == tile)
        {
            _hoveredTile = null;
            _isHandDirty = true;
        }
    }

    public void OnTileHoverEnter(MahjongTile tile)
    {
        // 自分のターンかつ操作可能な時のみ
        if (!IsHuman) return;
        
        // リーチ中は変更できないので表示しない（必要なら外してください）
        if (IsRiichi) return; 
        
        _hoveredTile = tile;
        _isHandDirty = true; 

        if (tile.IsAnimating) return; 

        int id = GetNormalizedTileId(tile.TileId);

        // マネージャーと設定の確認
        if (MahjongGameManager.Instance != null && 
            MahjongGameManager.Instance.config.ShowUkeireAssist)
        {
             var canvas = FindAnyObjectByType<MahjongCanvas>();
             if (canvas != null)
             {
                 // ★修正: キャッシュ辞書(_cachedWaits)にIDが含まれているか確認
                 if (_cachedWaits.ContainsKey(id))
                 {
                     // 待ちデータがあれば表示
                     canvas.ShowUkeirePanel(_cachedWaits[id]);
                 }
                 else
                 {
                     // この牌を切ってもテンパイしない（キャッシュにない）場合はパネルを消す
                     canvas.ShowUkeirePanel(null);
                 }
             }
        }
    }
/*
    public void OnTileHoverExit(MahjongTile tile)
    {
        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
            // パネルを非表示にするメソッド（MahjongCanvasに実装されている前提）
            // snippetには ShowUkeirePanel で active(false) にするロジックが見当たらないため
            // 空リストなどを渡して非表示にするか、別途Hideメソッドが必要
            canvas.ShowUkeirePanel(null); 
        }
        if (_hoveredTile == tile)
        {
            _hoveredTile = null;
            _isHandDirty = true;
        }
    }

    public void OnTileHoverEnter(MahjongTile tile)
    {
        // 自分のターンかつ操作可能な時のみ
                if (!IsHuman) return;
        if (IsRiichi) return; 
        
        _hoveredTile = tile;
        _isHandDirty = true; 

        if (tile.IsAnimating) return; 

        int id = GetNormalizedTileId(tile.TileId);
        // リーチ宣言待機中、かつアシストONの場合
        // if (IsRiichiPending &&
        //     MahjongGameManager.Instance != null && 
        //     MahjongGameManager.Instance.config.ShowUkeireAssist)
        if (MahjongGameManager.Instance != null && 
            MahjongGameManager.Instance.config.ShowUkeireAssist)
        {
             // ★修正: 計算せず、キャッシュから取得して表示
            if (_cachedWaits.ContainsKey(id))
             {
                 var waits = _cachedDiscardWaitMap[tile];
                 var canvas = FindAnyObjectByType<MahjongCanvas>();
                 if (canvas != null)
                 {
                     canvas.ShowUkeirePanel(_cachedWaits[id]);
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
*/

    public void RequestDiscard(MahjongTile tileObj)
    {
        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null && canvas.ankanSelectPanel != null && canvas.ankanSelectPanel.activeSelf)
        {
            Debug.Log("暗槓選択中のため、打牌をキャンセルしました");
            return;
        }
        if (_isDiscardProcessing) return;

        if (!IsRiichiPending)
        {
            _isDiscardProcessing = true;
        }

        _discardCount++;
        bool isDeclaringRiichi = false;

        if (IsRiichiPending)
        {
            if (!_validRiichiDiscardTiles.Contains(tileObj)) return;

            _isDiscardProcessing = true;

            IsRiichi = true;
            IsIppatsuChance = true; 
            if (_discardCount == 1 && MeldTiles.Count == 0) IsDoubleRiichi = true;
            
            isDeclaringRiichi = true; // 立直宣言フラグON

            // ★修正: リーチ確定（待ち計算）
            List<int> finalWaits = CalculateWaitsAfterDiscard(tileObj);
            _cachedRiichiWaits = finalWaits; 

            if (MahjongGameManager.Instance != null && 
                MahjongGameManager.Instance.config.ShowUkeireAssist)
            {
                var _canvas = FindAnyObjectByType<MahjongCanvas>();
                if (_canvas != null) _canvas.ShowUkeirePanel(finalWaits);
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
        
        // ★追加: 立直宣言時なら、ここで音声を流す
        if (isDeclaringRiichi)
        {
            var bgm = BgmController.GetOrFindInstance();
            if (bgm != null)
            {
                bgm.PlayRiichiBgmRandom();
            }

            var _canvas = FindAnyObjectByType<MahjongCanvas>();
            if (_canvas != null)
            {
                _canvas.PlayRiichiVoice();
            }
        }

        if (MahjongGameManager.Instance != null)
        {
            MahjongGameManager.Instance.OnDiscardRequested(this, tileObj, isDeclaringRiichi);
        }
    }

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
                ActiveSpecialTriggers.Add("真ん中強し");
            }
        }
    }

    public List<string> GetActiveTriggersForWin()
    {
        CheckCurrentTriggers();
        List<string> active = ActiveSpecialTriggers.ToList();

        foreach (var yaku in GuaranteedYakuman)
        {
            if (!active.Contains(yaku+"確定"))
            {
                active.Add(yaku+"確定");
            }
        }
        return active;
    }

    // MahjongPlayer.cs

    public void RequestTsumo()
    {
        // 手牌カウント配列の作成
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
        context.IsMenzen = this.IsMenzen; // 簡易判定
        context.RedDoraCount = GetRedDoraCount();
        if (TsumoTile != null) context.WinningTileId = GetNormalizedTileId(TsumoTile.TileId);
        
        context.IsRinshan = _isRinshanChance; // 嶺上開花
        context.SeatWind = Seat;
        context.RoundWind = 0;
        context.DoraTiles = new List<int>();
        context.SpecialYakuTriggers = GetActiveTriggersForWin();

        // ★追加: プレイヤー側での海底判定（念のため）
        if (MahjongGameManager.Instance != null)
        {
            // 山の残りが0なら海底
            if (MahjongGameManager.Instance.TilesRemainingInWall <= 0)
            {
                context.IsHaitei = true;
            }
        }

        // 計算
        ScoringResult result = MahjongLogic.CalculateScore(tileCounts, meldIds, context); 

        if (MahjongGameManager.Instance != null)
        {
            // Managerに結果を送る
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
        if (_isPhysicsMode) return;
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

        // --- 手牌の整列 ---
        for (int i = 0; i < HandTiles.Count; i++)
        {
            if(HandTiles[i] != null)
            {
                var rb = HandTiles[i].GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = true;
                    rb.useGravity = false;
                }

                float yPos = (HandTiles[i] == _hoveredTile) ? HoverYOffset : 0f;
                MoveTileToTarget(HandTiles[i], currentX, yPos, false, false);
            }
            currentX += tileWidth;
        }

        // --- ツモ牌の処理（修正箇所） ---
        if (TsumoTile != null)
        {
            var rb = TsumoTile.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // ツモ牌の目標位置を計算
            float targetLocalX = startX + handWidth + tsumoGap;
            float yPos = (TsumoTile == _hoveredTile) ? HoverYOffset : 0f;

            MoveTileToTarget(TsumoTile, targetLocalX, yPos, false, false);
        }

        // --- 副露牌の処理 ---
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
                        Vector3 localOffset = new Vector3(tilePos, 1.0f, 0);
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
            Vector3 localOffset = new Vector3(localX, 1.0f + localY, 0);
            ApplyTransform(tile.transform, localOffset, faceDown, isMeld);
        }
    }

    private void ApplyTransform(Transform t, Vector3 localOffset, bool faceDown, bool isMeld)
    {
        if (_isPhysicsMode) return; // ★物理モード中はスクリプトで位置を上書きしない
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
            if (IsHuman) tileRot = baseRot * Quaternion.Euler(-90, 180, 0);
            else tileRot = baseRot * Quaternion.Euler(90, 0, 0); 
        }

        float speed = 12f;
        t.position = Vector3.Lerp(t.position, targetPos, Time.deltaTime * speed);
        t.rotation = Quaternion.Slerp(t.rotation, tileRot, Time.deltaTime * speed);
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

    // MahjongPlayer.cs 1080行目付近の OnTileDragging を修正
public void OnTileDragging(MahjongTile tile, float mouseWorldX)
{
    if (!IsHuman) return;
    if (IsRiichi) return; 
    if (TsumoTile == tile) return; 
    if (tile != _draggingTile) return;
    if (Time.time < _lastSwapTime + 0.05f) return;

    // マウスの世界座標を、Playerオブジェクト（手牌の親）のローカル座標に変換
    Vector3 localMousePos = transform.InverseTransformPoint(new Vector3(mouseWorldX, transform.position.y, transform.position.z));

    // --- 基準位置の再計算（UpdateTilePositions のロジックと合わせる） ---
    float handWidth = HandTiles.Count * tileWidth;
    int meldSetCount = MeldTiles.Count / 4;
    float tsumoSlotSize = tsumoGap + tileWidth;
    float totalVisualWidth = handWidth;
    
    if (meldSetCount > 0)
    {
        totalVisualWidth += tsumoSlotSize + meldGap + (MeldTiles.Count * tileWidth);
    }

    // 全体の中央から手牌の開始位置を算出
    float startX = -totalVisualWidth / 2.0f + (tileWidth / 2.0f);

    // 現在のマウス位置が、手牌の何番目のスロットに近いかを算出
    int targetIndex = Mathf.RoundToInt((localMousePos.x - startX) / tileWidth);
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
        _isDiscardProcessing = false;
        //_isNewTsumo = true;

        _gimmickTriggeredThisTurn = false;

        if (IsHuman) CheckGimmickYakuman();
    }

    // ★追加: 役満確定ギミックの判定処理
    private void CheckGimmickYakuman()
    {
        if (_gimmickTriggeredThisTurn) return; // 1ターン（一巡）に1つまで
        var config = MahjongGameManager.Instance != null ? MahjongGameManager.Instance.config : null;

        if (_discardCount >= config.RiverLength) return;

        // 現在の手牌＋ツモ牌＋副露牌のカウントを取得
        int[] counts = new int[37];
        foreach (var t in HandTiles) if (t) counts[GetNormalizedTileId(t.TileId)]++;
        if (TsumoTile) counts[GetNormalizedTileId(TsumoTile.TileId)]++;
        foreach (var t in MeldTiles) if (t) counts[GetNormalizedTileId(t.TileId)]++;


        // 1. 大三元ギミック (三元牌が5枚以上 ＆ 3種類すべて揃っている -> 10%)
        if (!GuaranteedYakuman.Contains("大三元"))
        {
            int haku = counts[31];
            int hatsu = counts[32];
            int chun = counts[33];
            if ((haku * hatsu >= 1 || hatsu * chun >= 1 || chun * haku >= 1) && (haku + hatsu + chun) >= 3)
            {
                if (UnityEngine.Random.Range(0, 100) < config.DaisangenProb) // 10%の確率
                {
                    TriggerGimmick("大三元");
                    return; // 1度確定したらこのターンの判定は終了
                }
            }
        }

        // 2. 九蓮宝燈ギミック (同色の1・9牌が合計5枚以上 -> 5%)
        if (!GuaranteedYakuman.Contains("九蓮宝燈"))
        {
            bool hasChuuren = false;
            if (counts[0] + counts[8] >= 5) hasChuuren = true;       // 萬子 (1と9)
            else if (counts[9] + counts[17] >= 5) hasChuuren = true; // 筒子 (1と9)
            else if (counts[18] + counts[26] >= 5) hasChuuren = true;// 索子 (1と9)

            if (hasChuuren)
            {
                if (UnityEngine.Random.Range(0, 100) < config.ChurenProb) 
                {
                    TriggerGimmick("九蓮宝燈");
                    return; // 1度確定したらこのターンの判定は終了
                }
            }
        }
        if (!GuaranteedYakuman.Contains("国士無双"))
        {
            // 4. 国士無双ギミック (字牌と1・9牌が合計13種類以上 -> 2%)
            int uniqueTerminalsAndHonors = 0;
            for (int i = 0; i < 34; i++)
            {
                if (counts[i] > 0)
                {
                    if (i < 27) // 数牌
                    {
                        int mod = i % 9;
                        if (mod == 0 || mod == 8) uniqueTerminalsAndHonors++; // 1か9
                    }
                    else
                    {
                        uniqueTerminalsAndHonors++; // 字牌
                    }
                }
            }
            if (UnityEngine.Random.Range(0, 100) < config.KokushiProb && uniqueTerminalsAndHonors >= 3) // 2%の確率
            {
                TriggerGimmick("国士無双");
                return; // 1度確定したらこのターンの判定は終了
            }
        }
        if (!GuaranteedYakuman.Contains("大四喜"))
        {
            int counter = 0;
            for (int i = 27; i <= 30; i++)
            {
                counter += counts[i];
            }
            if (UnityEngine.Random.Range(0, 100) < config.DaiSuShiProb && 6 <= counter) // 10%の確率
            {
                TriggerGimmick("大四喜");
                return; // 1度確定したらこのターンの判定は終了
            }
        }
        if (!GuaranteedYakuman.Contains("緑一色"))
        {
            int[] greenTile = 
                { 19, 20, 21, 23, 25, 32 }; // 2索, 3索, 4索, 6索, 8索, 發
            int counter = 0;
            for (int i = 0; i < greenTile.Length; i++)
            {
                counter += counts[greenTile[i]];
            }
            if (UnityEngine.Random.Range(0, 100) < config.AllGreenProb && counter >= 5) // 10%の確率
            {
                TriggerGimmick("緑一色");
                return; // 1度確定したらこのターンの判定は終了
            }
        }
        if (!GuaranteedYakuman.Contains("字一色"))
        {
            int counter = 0;
            for (int i = 27; i < 34; i++)
            {
                counter += counts[i];
            }
            if (UnityEngine.Random.Range(0, 100) < config.TsuISoProb && counter >= 7) // 10%の確率
            {
                TriggerGimmick("字一色");
                return; // 1度確定したらこのターンの判定は終了
            }
        }
    }

    private void TriggerGimmick(string yakuName)
    {
        GuaranteedYakuman.Add(yakuName); // HashSetなので重複しても安全に1度だけ保持される
        _gimmickTriggeredThisTurn = true;

        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
            canvas.PlayGimmickAnnouncement(yakuName);
        }
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

    // MahjongPlayer.cs

    // ★修正: 引数(tileId)を追加し、デフォルト値を-1にする
    public void RequestAnkan(int tileId = -1)
    {
        if (IsHuman && AvailableAnkanTiles.Count > 0)
        {
            int targetTileId = -1;

            // 指定がある場合（選択画面から選ばれた場合）
            if (tileId != -1)
            {
                // 正当な候補かどうかチェック
                if (AvailableAnkanTiles.Contains(tileId))
                {
                    targetTileId = tileId;
                }
            }
            // 指定がない場合（候補が1つだけの場合など）
            else
            {
                targetTileId = AvailableAnkanTiles[0];
            }

            // 実行
            if (targetTileId != -1 && MahjongGameManager.Instance != null) 
            {
                MahjongGameManager.Instance.OnAnkanRequested(this, targetTileId);
            }
        }
    }

    public void SkipRiichiAnkan()
    {
        if (IsHuman && IsRiichi)
        {
            // 待ち状態を解除
            _isWaitingForRiichiAnkan = false;

            // 念のため自動廃棄フラグを立てて、Update内での重複処理を防ぐ
            _hasAutoDiscarded = true; 

            // ★修正: コルーチンを使わず、その場で即座にツモ牌を切る
            if (TsumoTile != null)
            {
                RequestDiscard(TsumoTile);
            }
        }
    }

    private bool _isPhysicsMode = false; // ★追加：物理モードフラグ
// MahjongPlayer.cs

    public void ReleasePhysicalTiles()
    {
        _isPhysicsMode = true; 
        
        var config = (MahjongGameManager.Instance != null) ? MahjongGameManager.Instance.config : null;
        // Configがない場合のデフォルト値
        float mass = (config != null) ? config.HandTileMass : 0.5f;
        float damping = (config != null) ? config.HandTileDamping : 0.05f;
        foreach (var tile in HandTiles)
        {
            if (tile == null) continue;
            var rb = tile.GetComponent<Rigidbody>();
            var col = tile.GetComponent<Collider>();
            
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                col.isTrigger = false;

                // ★重要: 空気抵抗(Damping)を小さくして、爆風で遠くまで飛ぶようにする
#if UNITY_6000_0_OR_NEWER
                rb.linearDamping = damping;
                rb.angularDamping = damping;
#else
                rb.drag = damping;
                rb.angularDrag = damping;
#endif
                rb.mass = mass;

                // 衝突判定の精度を上げる
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // コライダーのサイズ調整（少し小さくしておくと、密集していても弾けやすい）
                if (col is BoxCollider box) box.size = new Vector3(0.95f, 0.95f, 0.95f);
            }
        }
    }

    private void EnableTilePhysics(MahjongTile tile)
    {
        var rb = tile.GetComponent<Rigidbody>();
        var col = tile.GetComponent<Collider>();
        
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            col.isTrigger = false;
            
            // 牌の重さを軽くして、吹っ飛びやすくする
            rb.mass = 0.5f; 
            // 空気抵抗を減らす
            rb.linearDamping = 0.1f; 
            rb.angularDamping = 0.1f;

            if (col is BoxCollider box) box.size = new Vector3(1.1f, 1.1f, 1.1f);

            // 衝撃前に少しだけ上に浮かせて「卓から浮かせる」と、衝撃が伝わりやすくなります
            rb.AddForce(Vector3.up * 2f, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * 0f, ForceMode.Impulse);
        }
    }

    public void PerformTsumogiri()
    {
        // 自分のターンかつ、操作可能な状態で、ツモ牌が存在する場合のみ実行
        if (IsHuman && !_isDiscardProcessing && TsumoTile != null && !IsRiichiPending)
        {
            RequestDiscard(TsumoTile);
        }
    }
}


