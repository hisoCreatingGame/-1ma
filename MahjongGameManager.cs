using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class MahjongGameManager : MonoBehaviour
{
    public static MahjongGameManager Instance { get; private set; }
    
    public int CurrentTurnSeat { get { return 0; } } 
    public bool IsGameStarted { get; set; }
    public int LastDiscardSeat { get; set; } = -1;
    public int LastDiscardTileId { get; set; } = -1;
    
    public MahjongTable gameTable;
    [Header("Configuration")] public MahjongGameConfig config;
    [Header("Prefabs")] public GameObject[] tilePrefabs; 
    public GameObject playerPrefab;

    private List<int> deck = new List<int>();
    public int TilesRemainingInWall { get { return deck != null ? deck.Count : 0; } }

    private List<GameObject> visualWallObjects = new List<GameObject>();
    public List<MahjongPlayer> connectedPlayers = new List<MahjongPlayer>();
    
    private List<int> deadWall = new List<int>(); 
    private List<int> doraIndicatorIndices = new List<int>();
    private List<int> uraDoraIndicatorIndices = new List<int>();

    // --- ゲーム進行ステート変数 ---
    public int CurrentScore { get; private set; } // 現在の持ち点
    public int HonbaCount { get; private set; } = 0; // 本場（連荘回数）
    public int RoundCount { get; private set; } = 1; // 局数

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        connectedPlayers = new List<MahjongPlayer>();
        deck = new List<int>();
        visualWallObjects = new List<GameObject>();
    }

    private void Start() 
    { 
        // ゲーム起動時は新規ゲームとして開始
        StartNewGame(); 
    }
    
    /// <summary>
    /// 全く新しいゲームを開始する（スコア初期化）
    /// </summary>
    public void StartNewGame()
    {
        CurrentScore = (config != null) ? config.InitialScore : 25000;
        HonbaCount = 0;
        RoundCount = 1;
        StartRound();
    }

    /// <summary>
    /// 次の局（または現在の局の再開）を開始する
    /// </summary>
    private void StartRound()
    {
        IsGameStarted = false;
        
        // ★修正: 既存プレイヤーとその手牌を完全に削除
        foreach(var p in connectedPlayers) 
        {
            if(p != null) 
            {
                p.DespawnAllTiles(); // ★重要: プレイヤーが持っている牌を削除
                Destroy(p.gameObject);
            }
        }
        connectedPlayers.Clear();

        // プレイヤー生成
        GameObject pObj = Instantiate(playerPrefab);
        pObj.name = "Player_Me";
        MahjongPlayer mp = pObj.GetComponent<MahjongPlayer>();
        
        mp.Initialize(0, true);
        
        // 現在の持ち点をプレイヤーに適用（引継ぎ）
        mp.Score = CurrentScore;
        
        connectedPlayers.Add(mp);

        CreateDeck();

        deadWall.Clear();
        doraIndicatorIndices.Clear();
        uraDoraIndicatorIndices.Clear();

        int deadCount = config.DeadWallCount;
        if (deck.Count >= deadCount)
        {
            for (int i = 0; i < deadCount; i++)
            {
                int val = deck[deck.Count - 1];
                deck.RemoveAt(deck.Count - 1);
                deadWall.Add(val);
            }
        }

        doraIndicatorIndices.Add(4);
        SpawnVisualWall();

        if (gameTable != null) gameTable.ClearTable();

        int handCount = config.HandTileCount;
        for (int i = 0; i < handCount; i++) SpawnAndGiveTileToHand(mp);

        LastDiscardSeat = -1;
        LastDiscardTileId = -1;
        IsGameStarted = true;
        
        StartCoroutine(NextDrawRoutine());
    }

    /// <summary>
    /// UIから「Next Round」が押されたときに呼ばれる
    /// </summary>
    public void ProceedToNextRound()
    {
        // 親（自分）が上がったので連荘（本場加算）
        HonbaCount++;
        RoundCount++;
        
        Debug.Log($"Proceeding to Round {RoundCount}, Honba {HonbaCount}");
        
        StartRound();
    }

    public void OnDiscardRequested(MahjongPlayer player, MahjongTile tileObj, bool isRiichi)
    {
        if (player.Seat != 0) return;
        if (isRiichi) player.IsRiichi = true;

        int discardedTileTypeId = tileObj.TileId;
        bool removed = false;
        
        if (player.TsumoTile == tileObj)
        {
            player.ClearTsumoTile();
            removed = true;
        }
        else
        {
            if (player.RemoveTileFromHand(tileObj))
            {
                if (player.TsumoTile != null)
                {
                    player.AddTileToHand(player.TsumoTile);
                    player.ClearTsumoTile();
                }
                removed = true;
            }
        }

        if (removed)
        {
            player.RegisterDiscard(discardedTileTypeId);
            Destroy(tileObj.gameObject);
            if (gameTable != null) gameTable.DiscardTile(0, discardedTileTypeId, isRiichi);
            StartCoroutine(NextDrawRoutine());
        }
    }

    private IEnumerator NextDrawRoutine()
    {
        yield return new WaitForSeconds(0.4f);
        
        // 山がなくなったら流局（ゲームオーバー）処理へ
        if (deck.Count <= 0) 
        { 
            Debug.Log("流局（山なし）");
            OnRyuukyoku();
            yield break; 
        }
        
        var player = connectedPlayers[0];
        SpawnAndGiveTsumo(player);
    }

    /// <summary>
    /// 流局時の処理
    /// </summary>
    private void OnRyuukyoku()
    {
        IsGameStarted = false;
        
        // 現在のスコアを更新
        if (connectedPlayers.Count > 0)
        {
            CurrentScore = connectedPlayers[0].Score;
        }

        Debug.Log("Game Over: Ryuukyoku");
        
        // Canvasに通知してゲームオーバー画面を表示
        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
            canvas.ShowGameOver(CurrentScore);
        }
    }
    
    public void OnAnkanRequested(MahjongPlayer player, int tileType)
    {
        player.PerformAnkan(tileType);
        if (doraIndicatorIndices.Count > 0)
        {
            int lastIndex = doraIndicatorIndices[doraIndicatorIndices.Count - 1];
            int nextIndex = lastIndex + 2;
            if (nextIndex < deadWall.Count - 1) 
            {
                doraIndicatorIndices.Add(nextIndex);
                SpawnVisualWall();
            }
        }
        StartCoroutine(NextDrawRoutine());
    }

    public void OnTsumoRequested(MahjongPlayer winner, int score, string[] yakuList)
    {
        if (winner.IsRiichi)
        {
            RevealUraDoraIndices();
            SpawnVisualWall();
        }

        var result = RecalculateForPayment(winner);
        
        int[] omoteIds = GetDoraIds(false);
        int[] uraIds = GetDoraIds(true);
        string yakuStr = string.Join(" / ", result.YakuList);
        
        // スコアを加算して管理変数更新
        winner.Score += result.TotalScore; 
        CurrentScore = winner.Score;       

        AnnounceWin(0, result.TotalScore, yakuStr, omoteIds, uraIds);
    }

    public void OnRonRequested(MahjongPlayer winner) { }

    private int[] GetDoraIds(bool isUra)
    {
        int[] ids = new int[] { -1, -1, -1, -1, -1 };
        int[] targetIndices = { 4, 6, 8, 10, 12 };
        for (int i = 0; i < 5; i++)
        {
            int checkIdx = targetIndices[i];
            if (doraIndicatorIndices.Contains(checkIdx) && checkIdx < deadWall.Count)
            {
                if(!isUra) ids[i] = deadWall[checkIdx];
                else {
                    int uraIdx = checkIdx + 1;
                    if (uraIdx < deadWall.Count) ids[i] = deadWall[uraIdx];
                }
            }
        }
        return ids;
    }

    private void AnnounceWin(int seat, int score, string yakuStr, int[] doraIndicators, int[] uraDoraIndicators)
    {
        Debug.Log($"Win! Score:{score}");
        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
            canvas.ShowWinResult(seat, score, yakuStr, doraIndicators, uraDoraIndicators);
        }
    }

    private ScoringResult RecalculateForPayment(MahjongPlayer player)
    {
        int[] tileCounts = new int[37];
        foreach (var t in player.HandTiles) if(t) tileCounts[t.TileId]++;
        if (player.TsumoTile != null) tileCounts[player.TsumoTile.TileId]++;

        List<int> meldIds = new List<int>();
        foreach (var t in player.MeldTiles) if(t) meldIds.Add(t.TileId);

        ScoringContext context = new ScoringContext();
        
        context.IsTsumo = true;   
        context.IsDealer = true;  
        context.IsMenzen = true; 
        
        context.SeatWind = 0;     

        context.IsRiichi = player.IsRiichi;
        context.IsDoubleRiichi = player.IsDoubleRiichi;
        
        context.DoraTiles = GetDoraList();
        if (player.IsRiichi) context.UraDoraTiles = GetUraDoraList();
        context.RedDoraCount = player.GetRedDoraCount();

        if (player.ActiveSpecialTriggers != null)
        {
            context.SpecialYakuTriggers = new List<string>(player.ActiveSpecialTriggers);
        }
        
        return MahjongLogic.CalculateScore(tileCounts, meldIds, context);
    }
    
    private List<int> GetDoraList()
    {
        List<int> list = new List<int>();
        foreach (int idx in doraIndicatorIndices)
            if (idx < deadWall.Count) list.Add(GetNextTile(deadWall[idx]));
        return list;
    }
    private List<int> GetUraDoraList()
    {
        List<int> list = new List<int>();
        foreach (int idx in doraIndicatorIndices) {
            int uraIdx = idx + 1;
            if (uraIdx < deadWall.Count) list.Add(GetNextTile(deadWall[uraIdx]));
        }
        return list;
    }
    private int GetNextTile(int indicatorId)
    {
        if (indicatorId >= 0 && indicatorId <= 8) return (indicatorId == 8) ? 0 : indicatorId + 1;
        if (indicatorId >= 9 && indicatorId <= 17) return (indicatorId == 17) ? 9 : indicatorId + 1;
        if (indicatorId >= 18 && indicatorId <= 26) return (indicatorId == 26) ? 18 : indicatorId + 1;
        if (indicatorId >= 27 && indicatorId <= 30) return (indicatorId == 30) ? 27 : indicatorId + 1;
        if (indicatorId >= 31 && indicatorId <= 33) return (indicatorId == 33) ? 31 : indicatorId + 1;
        return indicatorId;
    }

    private void SpawnAndGiveTsumo(MahjongPlayer player)
    {
        if (deck.Count <= 0) { return; } 

        int tileType = DrawTileFromDeck();
        if (tileType == -1) { Debug.LogError("【エラー】デッキID取得失敗"); return; }

        MahjongTile tileObj = SpawnTileObject(tileType, player);
        if (tileObj != null) player.SetTsumoTile(tileObj);
        else Debug.LogError("【エラー】牌生成失敗");
    }
    
    private void SpawnAndGiveTileToHand(MahjongPlayer player)
    {
        int tileType = DrawTileFromDeck();
        if (tileType == -1) return;
        MahjongTile tileObj = SpawnTileObject(tileType, player);
        if (tileObj != null) player.AddTileToHand(tileObj);
    }

    private void CreateDeck()
    {
        deck.Clear();
        for (int i = 0; i < 34; i++)
        {
            bool isTarget = false;
            if (i >= 0 && i <= 8) isTarget = config.UseManzu;
            else if (i >= 9 && i <= 17) isTarget = config.UsePinzu;
            else if (i >= 18 && i <= 26) isTarget = config.UseSozu;
            else if (i >= 27 && i <= 33) isTarget = config.UseHonors;

            if (!isTarget) continue;

            for (int count = 0; count < 4; count++)
            {
                int tileToAdd = i;
                if (config.UseRedDora && count == 0)
                {
                    if (i == 4) tileToAdd = config.RedManzuId;
                    else if (i == 13) tileToAdd = config.RedPinzuId;
                    else if (i == 22) tileToAdd = config.RedSozuId;
                }
                deck.Add(tileToAdd);
            }
        }
        var r = new System.Random();
        int n = deck.Count;
        while (n > 1)
        {
            n--;
            int k = r.Next(n + 1);
            int v = deck[k];
            deck[k] = deck[n];
            deck[n] = v;
        }
    }

    private int DrawTileFromDeck()
    {
        if (deck.Count == 0) return -1;
        int t = deck[0];
        deck.RemoveAt(0);
        return t;
    }

    private MahjongTile SpawnTileObject(int tileType, MahjongPlayer player)
    {
        if (tilePrefabs != null && tileType < tilePrefabs.Length)
        {
            GameObject prefab = tilePrefabs[tileType];
            if (prefab != null)
            {
                Vector3 rightOffset = player.transform.right * 35.0f;
                Vector3 upOffset = Vector3.up * 5.0f;
                GameObject tileObj = Instantiate(prefab, player.transform.position + rightOffset + upOffset, Quaternion.identity);
                MahjongTile tileScript = tileObj.GetComponent<MahjongTile>();
                if (tileScript != null) tileScript.Initialize(tileType, 0); 
                return tileScript;
            }
        }
        return null;
    }

    private void SpawnVisualWall()
    {
        foreach (var obj in visualWallObjects) if (obj != null) Destroy(obj);
        visualWallObjects.Clear();
        if (!config.ShowWall) return;

        Vector3 centerPos = config.WallCenterPosition;
        float tileWidth = config.TileWidth;
        float tileHeight = config.TileHeight;
        float totalWidth = 5 * tileWidth;
        float startX = -totalWidth / 2.0f + (tileWidth / 2.0f);

        for (int i = 4; i < deadWall.Count; i++)
        {
            int tileId = deadWall[i];
            int col = (i - 4) / 2;
            bool isUpper = (i % 2 == 0); 
            float xPos = startX + (col * tileWidth);
            float yPos = (isUpper ? 1.5f : 0.5f) * config.TileThickness;
            float zPos = 0f;
            if (isUpper && uraDoraIndicatorIndices.Contains(i + 1)) {
                yPos = 0f;
                zPos = -tileHeight * 1.2f;
            }
            Vector3 pos = centerPos + new Vector3(xPos, yPos, zPos);
            Quaternion rot = (doraIndicatorIndices.Contains(i) || uraDoraIndicatorIndices.Contains(i)) 
                ? Quaternion.Euler(-90f, 0f, 0f) : Quaternion.Euler(90f, 0f, 0f);

            if (tileId >= 0 && tileId < tilePrefabs.Length) {
                GameObject prefab = tilePrefabs[tileId];
                if (prefab != null) {
                    GameObject obj = Instantiate(prefab, pos, rot);
                    if (obj.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
                    if (obj.TryGetComponent<MahjongTile>(out var tileScript)) tileScript.Initialize(tileId, -1);
                    visualWallObjects.Add(obj);
                }
            }
        }
    }
    
    // デバッグ用: リスタートはNewGame扱いにする
    public void RequestDebugRestart() { StartNewGame(); }

    private void RevealUraDoraIndices()
    {
        uraDoraIndicatorIndices.Clear();
        foreach (int idx in doraIndicatorIndices) {
            int uraIdx = idx + 1;
            if (uraIdx < deadWall.Count) uraDoraIndicatorIndices.Add(uraIdx);
        }
    }
}