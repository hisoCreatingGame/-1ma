using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;

public class MahjongGameManager : MonoBehaviour
{
    // ★追加: 役満動画再生を待機するためのフラグ
    public bool isYakumanProductionTriggered { get; private set; } = false;

    // ★追加: UIのButtonからOnClickで呼び出すメソッド
    public void StartYakumanProduction()
    {
        isYakumanProductionTriggered = true;

        // WebGLではユーザー入力直後の再生要求が安定しやすい。
        if (videoController != null && !string.IsNullOrEmpty(_pendingYakumanVideoName))
        {
            videoController.PlayVideo();
        }
    }

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
    public int TilesRemainingInWall { get { return GetRemainingDrawableTileCount(); } }
    public int RawTilesRemainingInWall { get { return deck != null ? deck.Count : 0; } }

    private List<GameObject> visualWallObjects = new List<GameObject>();
    public List<MahjongPlayer> connectedPlayers = new List<MahjongPlayer>();
    
    private List<int> deadWall = new List<int>(); 
    private List<int> doraIndicatorIndices = new List<int>();
    private List<int> uraDoraIndicatorIndices = new List<int>();

    // --- ゲーム進行ステート変数 ---
    public int CurrentScore { get; private set; } 
    public int HonbaCount { get; private set; } = 0; 
    public int RoundCount { get; private set; } = 1; 
    private int currentRoundTsumoCount = 0;

    // ★追加: 役名と動画のペア設定用構造体
    [System.Serializable]
    public struct YakumanVideoMapping
    {
        public string yakuName; // 例: "国士無双"
        public UnityEngine.Video.VideoClip videoClip;
    }

    [Header("Yakuman Movies")]
    public List<YakumanVideoMapping> yakumanVideos; // インスペクターで設定
    // ★追加: 役満実績管理用のリスト
    // MahjongLogic.cs が返す役名と完全に一致させる必要があります
    private readonly string[] TargetYakumanList = new string[]
    {
        "国士無双", "国士無双13面待ち", 
        "四暗刻", "四暗刻単騎待ち", 
        "大三元", 
        "緑一色", 
        "字一色", 
        "小四喜", "大四喜", 
        "清老頭", 
        "四槓子", 
        "九蓮宝燈", "純正九蓮宝燈", 
        "天和", "真ん中強し"
    };

    [Header("--- Yakuman Video Clips (役満動画設定) ---")]
    public string clip_Kokushi = "Kokushimuso1";        // 国士無双
    public string clip_Kokushi13 = "Kokushimuso13";      // 国士無双13面待ち
    public string clip_Suuankou = "Suanko1";       // 四暗刻
    public string clip_SuuankouTanki = "Suankotanki1";  // 四暗刻単騎待ち
    public string clip_Daisangen = "Daisangen1";      // 大三元
    public string clip_Ryuuiisou = "Ryuiso1";      // 緑一色
    public string clip_Tsuuiisou = "Tuiso1";      // 字一色
    public string clip_Shousuushii = "Syosushi1";    // 小四喜
    public string clip_Daisuushii = "Daisushi1";     // 大四喜
    public string clip_Chinroutou = "Tinroto1";     // 清老頭
    public string clip_Suukantsu = "Sukantu1";      // 四槓子
    public string clip_Chuuren = "Tyurenpoto1";        // 九蓮宝燈
    public string clip_JunseiChuuren = "JunseiTyurenpoto1";  // 純正九蓮宝燈
    public string clip_Tenhou = "Tenho1";         // 天和
    public string clip_Chiihou = "1";        // 地和
    public string clip_CenterStrong = "Mannakatusyoshi1";   // 真ん中強し (オリジナル)

    private Dictionary<string, string> _yakuVideoMap;

    public WebGLVideoPlayerController videoController;
    [SerializeField] private string defaultYakumanVideo = "yakuman.mp4";
    private string _pendingYakumanVideoName;

    [Header("SE Keys")]
    [SerializeField] private string normalTsumoSeKey = SeKeys.GameNormalTsumoImpact;
    [SerializeField] private string yakumanExplosionSeKey = SeKeys.GameYakumanExplosion;
    [SerializeField] private string roundStartSeKey = SeKeys.GameRoundStartEastKyoku;
    [SerializeField] private string scoreSlotSeKey = SeKeys.GameScoreSlot;

    private void Start() 
    { 
        // ★追加: 辞書の初期化
        InitializeVideoMap();

        if (videoController == null)
        {
            videoController = WebGLVideoPlayerController.Instance ?? FindAnyObjectByType<WebGLVideoPlayerController>();
        }

        if (videoController != null)
        {
            string warmupVideoName = ResolveStartupWarmupVideoName();
            if (!string.IsNullOrEmpty(warmupVideoName))
            {
                // 起動直後の初回再生ラグを避けるため、無音・透明で裏再生しておく
                videoController.StartHiddenWarmupLoop(warmupVideoName);
            }
        }

        StartNewGame(); 
    }

    // ★追加: 役名と動画クリップをマッピングするメソッド
    private void InitializeVideoMap()
    {
        _yakuVideoMap = new Dictionary<string, string>();

        // StreamingAssetsから読み込むため、拡張子（.mp4等）が必要です
        _yakuVideoMap["国士無双"] = clip_Kokushi + ".mp4";
        _yakuVideoMap["国士無双13面待ち"] = clip_Kokushi13 + ".mp4";
        _yakuVideoMap["四暗刻"] = clip_Suuankou + ".mp4";
        _yakuVideoMap["四暗刻単騎待ち"] = clip_SuuankouTanki + ".mp4";
        _yakuVideoMap["大三元"] = clip_Daisangen + ".mp4";
        _yakuVideoMap["緑一色"] = clip_Ryuuiisou + ".mp4";
        _yakuVideoMap["字一色"] = clip_Tsuuiisou + ".mp4";
        _yakuVideoMap["小四喜"] = clip_Shousuushii + ".mp4";
        _yakuVideoMap["大四喜"] = clip_Daisuushii + ".mp4";
        _yakuVideoMap["清老頭"] = clip_Chinroutou + ".mp4";
        _yakuVideoMap["四槓子"] = clip_Suukantsu + ".mp4";
        _yakuVideoMap["九蓮宝燈"] = clip_Chuuren + ".mp4";
        _yakuVideoMap["純正九蓮宝燈"] = clip_JunseiChuuren + ".mp4";
        _yakuVideoMap["天和"] = clip_Tenhou + ".mp4";
        _yakuVideoMap["地和"] = clip_Chiihou + ".mp4";
        _yakuVideoMap["真ん中強し"] = clip_CenterStrong + ".mp4";
    }

    private string ResolveYakumanVideoName(List<string> yakuList)
    {
        if (_yakuVideoMap != null && yakuList != null)
        {
            List<string> candidates = new List<string>();
            HashSet<string> unique = new HashSet<string>();

            foreach (var yaku in yakuList)
            {
                if (_yakuVideoMap.TryGetValue(yaku, out string mappedVideo) && !string.IsNullOrEmpty(mappedVideo))
                {
                    if (unique.Add(mappedVideo))
                    {
                        candidates.Add(mappedVideo);
                    }
                }
            }

            if (candidates.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, candidates.Count);
                return candidates[index];
            }
        }

        if (!string.IsNullOrEmpty(defaultYakumanVideo))
        {
            return defaultYakumanVideo;
        }

        if (_yakuVideoMap != null)
        {
            foreach (var mappedVideo in _yakuVideoMap.Values)
            {
                if (!string.IsNullOrEmpty(mappedVideo))
                {
                    return mappedVideo;
                }
            }
        }

        return null;
    }

    private string ResolveStartupWarmupVideoName()
    {
        if (_yakuVideoMap != null)
        {
            foreach (var mappedVideo in _yakuVideoMap.Values)
            {
                if (!string.IsNullOrEmpty(mappedVideo))
                {
                    return mappedVideo;
                }
            }
        }

        if (!string.IsNullOrEmpty(defaultYakumanVideo))
        {
            return defaultYakumanVideo;
        }

        return null;
    }

    private IEnumerator WaitYakumanVideoFinishOrTimeout(float timeoutSeconds)
    {
        if (videoController == null)
        {
            yield break;
        }

        float elapsed = 0f;
        while (!videoController.IsVideoFinished && elapsed < timeoutSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!videoController.IsVideoFinished)
        {
            Debug.LogWarning("[Yakuman] Video wait timed out. Continue without blocking.");
        }
    }

    // ... (その他のメソッド) ...

    // TsumoImpactRoutine を以下のように修正（辞書を使って動画を取得）

    private void Awake()
    {
        // シングルトンパターンの確立
        if (Instance == null)
        {
            Instance = this;
            // シーン遷移（リロード）してもこのオブジェクトを破壊しないようにする
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            // もしすでにInstanceが存在する場合（シーンリロード時など）、
            // 重複して作られた自分自身を即座に破棄する
            if (Instance != this)
            {
                Destroy(gameObject);
                // return; // これ以上の処理を行わせない
            }
        }

        // 既存の初期化処理
        tilePrefabs = config.DeckMode == DebugDeckMode.Normal ? tilePrefabs : tilePrefabs; // (例)
    }
    //  private void Awake()
    //  {
    //      if (Instance == null) Instance = this;
    //      else if (Instance != this) Destroy(gameObject);

    //      connectedPlayers = new List<MahjongPlayer>();
    //      deck = new List<int>();
    //      visualWallObjects = new List<GameObject>();
    //  }

    
    public void StartNewGame()
    {
        CurrentScore = (config != null) ? config.InitialScore : 25000;
        HonbaCount = 0;
        RoundCount = 1;
        StartRound();
    }
    // ... (前略)

    private void StartRound()
    {
        StopAllCoroutines();
        IsGameStarted = false;
        _pendingYakumanVideoName = null;
        currentRoundTsumoCount = 0;

        var bgm = BgmController.GetOrFindInstance();
        if (bgm != null)
        {
            bgm.PlayGameStartBgm();
        }
        
        // --- プレイヤー生成やデッキ作成の既存処理 (そのまま) ---
        for (int i = connectedPlayers.Count - 1; i >= 0; i--)
        {
            if (connectedPlayers[i] == null || connectedPlayers[i].gameObject == null)
            {
                connectedPlayers.RemoveAt(i);
            }
            else
            {
                connectedPlayers[i].DespawnAllTiles();
                Destroy(connectedPlayers[i].gameObject);
                connectedPlayers.RemoveAt(i);
            }
        }
        connectedPlayers.Clear();

        Debug.Log($"[StartRound] Spawning new player. CurrentScore to pass: {CurrentScore}");
        GameObject pObj = Instantiate(playerPrefab);

        pObj.name = "Player_Me";
        MahjongPlayer mp = pObj.GetComponent<MahjongPlayer>();
        
        mp.Initialize(0, true, CurrentScore);
        connectedPlayers.Add(mp);

        CreateDeck();

        deadWall.Clear();
        doraIndicatorIndices.Clear();
        uraDoraIndicatorIndices.Clear();

        int deadCount = config.DeadWallCount;
        if (deck.Count < deadCount + config.HandTileCount)
        {
            deadCount = Mathf.Max(0, deck.Count - config.HandTileCount - 4); 
        }

        if (deck.Count >= deadCount)
        {
            for (int i = 0; i < deadCount; i++)
            {
                int val = deck[deck.Count - 1];
                deck.RemoveAt(deck.Count - 1);
                deadWall.Add(val);
            }
        }

        if (deadWall.Count > 4) doraIndicatorIndices.Add(4);
        
        SpawnVisualWall();

        if (gameTable != null) gameTable.ClearTable();

        LastDiscardSeat = -1;
        LastDiscardTileId = -1;

        // ★変更: ここでいきなり配牌せず、シーケンスコルーチンを開始する
        StartCoroutine(RoundStartSequence(mp));
    }

    public void ProceedToNextRound()
    {
        HonbaCount++;
        RoundCount++;
        Debug.Log($"Proceeding to Round {RoundCount}, Honba {HonbaCount}");
        StartRound();
    }
    // MahjongGameManager.cs

    // 引数に isRon = false をデフォルトで設定
    private ScoringResult RecalculateForPayment(MahjongPlayer player, bool isRon = false)
    {
        int[] tileCounts = new int[37];
        foreach (var t in player.HandTiles) if(t) tileCounts[t.TileId]++;
        
        // ツモ時はツモ牌を加算、ロン時は当たり牌を加算
        if (!isRon && player.TsumoTile != null) 
        {
            tileCounts[player.TsumoTile.TileId]++;
        }
        else if (isRon && LastDiscardTileId != -1)
        {
            tileCounts[LastDiscardTileId]++;
        }

        List<int> meldIds = new List<int>();
        foreach (var t in player.MeldTiles) if(t) meldIds.Add(t.TileId);

        ScoringContext context = new ScoringContext();
        
        context.IsTsumo = !isRon;   
        context.IsDealer = (player.Seat == 0); 
        // 暗槓以外の副露がない場合を門前とする（簡易判定：MeldTilesが空なら門前）
        context.IsMenzen = player.IsMenzen; 
        
        context.SeatWind = player.Seat;     
        context.IsFirstTurn = player.IsFirstTurn; 
        
        context.IsRiichi = player.IsRiichi;
        context.IsDoubleRiichi = player.IsDoubleRiichi;
        context.IsIppatsu = player.IsIppatsuChance; 
        context.IsRinshan = player.IsRinshanChance; // 嶺上開花

        // ==========================================================
        // ★追加: 海底摸月・河底撈魚の判定ロジック
        // ==========================================================
        // 現在のデッキ（山）の残り枚数を確認
        int remaining = TilesRemainingInWall;

        // ★デバッグログ: ここでコンソールを確認してください
        Debug.Log($"<color=cyan>【海底判定】残り枚数: {remaining}, ツモあがり?: {context.IsTsumo}</color>");

        // 0枚以下なら強制的にフラグを立てる
        if (remaining <= 0)
        {
            if (context.IsTsumo)
            {
                context.IsHaitei = true;
                Debug.Log("<color=green>判定: 海底摸月 (Haitei) フラグON</color>");
            }
            else
            {
                context.IsHoutei = true;
                Debug.Log("<color=green>判定: 河底撈魚 (Houtei) フラグON</color>");
            }
        }

        // 上がり牌の設定
        int winningId = -1;
        if (isRon) winningId = LastDiscardTileId;
        else if (player.TsumoTile != null) winningId = player.TsumoTile.TileId;

        if (winningId != -1)
        {
            int normalId = winningId;
            if (winningId == 34) normalId = 4;
            else if (winningId == 35) normalId = 13;
            else if (winningId == 36) normalId = 22;
            context.WinningTileId = normalId;
        }

        context.DoraTiles = GetDoraList();
        if (player.IsRiichi) context.UraDoraTiles = GetUraDoraList();
        context.RedDoraCount = player.GetRedDoraCount();
        if (isRon && LastDiscardTileId >= 34 && LastDiscardTileId <= 36) context.RedDoraCount++;

        if (player.ActiveSpecialTriggers != null)
        {
            context.SpecialYakuTriggers = player.GetActiveTriggersForWin();
        }
        
        return MahjongLogic.CalculateScore(tileCounts, meldIds, context);
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

        if (HasReachedTsumoLimit())
        {
            int limit = (config != null) ? config.MaxTsumoCountPerRound : 0;
            Debug.Log($"流局（ツモ上限到達） {currentRoundTsumoCount}/{limit}");
            OnRyuukyoku();
            yield break;
        }
        
        if (deck.Count <= 0) 
        { 
            Debug.Log("流局（山なし）");
            OnRyuukyoku();
            yield break; 
        }
        
        var player = connectedPlayers[0];
        bool drawSucceeded = SpawnAndGiveTsumo(player);
        if (drawSucceeded)
        {
            currentRoundTsumoCount++;
        }
    }

    private bool HasReachedTsumoLimit()
    {
        if (config == null)
        {
            return false;
        }

        int limit = config.MaxTsumoCountPerRound;
        return limit > 0 && currentRoundTsumoCount >= limit;
    }

    private int GetRemainingDrawableTileCount()
    {
        int rawRemaining = (deck != null) ? deck.Count : 0;
        if (config == null)
        {
            return rawRemaining;
        }

        int limit = config.MaxTsumoCountPerRound;
        if (limit <= 0)
        {
            return rawRemaining;
        }

        int limitRemaining = Mathf.Max(0, limit - currentRoundTsumoCount);
        return Mathf.Min(rawRemaining, limitRemaining);
    }

    private void OnRyuukyoku()
    {
        IsGameStarted = false;
        var se = SeController.GetOrFindInstance();
        if (se != null)
        {
            se.Play(scoreSlotSeKey);
        }

        if (connectedPlayers.Count > 0)
        {
            CurrentScore = connectedPlayers[0].Score;
        }
        Debug.Log("Game Over: 流局");
        
        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
            // Canvas側にゲームオーバー表示を依頼（スコアを渡す）
            // MahjongCanvas側でこのメソッドを実装している必要があります
            //canvas.ShowRyuukyoku(true, CurrentScore);
            canvas.ShowGameOver(CurrentScore);
            // ※MahjongCanvasのコードに合わせて適宜コメントアウトを解除してください
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

    // MahjongGameManager.cs

public void OnTsumoRequested(MahjongPlayer winner, int score, string[] yakuList)
{
        IsGameStarted = false;
        // 演出コルーチンを開始
        StartCoroutine(TsumoImpactRoutine(winner));
}

private IEnumerator CameraShake(float duration, float magnitude)
{
    Vector3 originalPos = Camera.main.transform.localPosition;
    float elapsed = 0.0f;

    while (elapsed < duration)
    {
        float x = Random.Range(-1f, 1f) * magnitude;
        float y = Random.Range(-1f, 1f) * magnitude;

        Camera.main.transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
        elapsed += Time.deltaTime;
        yield return null;
    }

    Camera.main.transform.localPosition = originalPos;
}
    // MahjongGameManager.cs に追加・修正

[Header("VFX Prefabs")]
public GameObject yakumanExplosionPrefab; // インスペクターで爆発と煙のパーティクルをセット

    // MahjongGameManager.cs 内の該当メソッドを修正

    // MahjongGameManager.cs 内の TsumoImpactRoutine を上書きしてください

    // ★追加: ロンあがり時の処理を実装
    public void OnRonRequested(MahjongPlayer winner)
    {
        IsGameStarted = false;
        StartCoroutine(RonImpactRoutine(winner));
    }

    // ★追加: ロン用の演出コルーチン
    private IEnumerator RonImpactRoutine(MahjongPlayer winner)
    {
        // 1. スコア計算 (isRon = true)
        ScoringResult result = RecalculateForPayment(winner, true);
        bool isYakuman = result.Han >= 13;

        // 2. 演出 (ツモと似たような処理、あるいはカメラをロン牌に向けるなど)
        // ここでは簡易的に少し待ってから結果表示
        yield return new WaitForSeconds(1.0f);

        // ロン牌への衝撃演出などを入れたい場合はここに記述
        // 例: activeDiscardsの最後の牌を取得してエフェクト再生など

        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
             canvas.PlayImpactSE();
        }

        yield return new WaitForSeconds(1.5f);

        int[] omoteIds = GetDoraIds(false);
        int[] uraIds = (winner.IsRiichi || winner.IsDoubleRiichi) ? GetDoraIds(true) : null;
        string yakuStr = string.Join(" / ", result.YakuList);

        winner.Score += result.TotalScore;
        CurrentScore = winner.Score;

        CheckAndUnlockYakuman(result.YakuList);

        // 結果表示 (ロンなので seat=0 ではなく放銃者の席順を入れるのが通例ですが、一人麻雀なのでCPU(他家)からの出あがりとして扱います)
        // LastDiscardSeat が放銃者のID
        AnnounceWin(winner, LastDiscardSeat, result.TotalScore, yakuStr, omoteIds, uraIds, result.Han, result.Fu, result.ScoreName);
    }
    // MahjongGameManager.cs

    // MahjongGameManager.cs

    //private IEnumerator TsumoImpactRoutine(MahjongPlayer winner)
    //{
    //    // 1. スコア計算
    //    ScoringResult result = RecalculateForPayment(winner);
    //    bool isYakuman = result.Han >= 13;

    //    // 2. 物理挙動解放
    //    winner.ReleasePhysicalTiles();
    //    
    //    // 3. 溜め
    //    yield return new WaitForSeconds(isYakuman ? 1.8f : 1.2f);

    //    // 4. ツモ牌叩きつけ
    //    if (winner.TsumoTile != null)
    //    {
    //        GameObject tsumo = winner.TsumoTile.gameObject;
    //        float impactDuration = 0.15f; 
    //        
    //        // 物理設定 (Configの値を使用)
    //        ConfigureTsumoPhysics(tsumo, isYakuman, impactDuration);

    //        // 落下待ち
    //        yield return new WaitForSeconds(impactDuration);

    //        // ====================================================
    //        // 卓に激突！
    //        // ====================================================
    //        var canvas = FindAnyObjectByType<MahjongCanvas>();
    //        if (canvas != null) canvas.PlayImpactSE();
    //        
    //        Vector3 impactPos = new Vector3(0, 0, -50);

    //        // 演出後の「物理挙動が落ち着くまでの待ち時間」
    //        float settleTime = 2.5f; 

    //        if (isYakuman)
    //        {
    //            VideoClip clipToPlay = null;
    //            
    //            // --- 役満爆発エフェクト生成 ---
    //            if (yakumanExplosionPrefab != null)
    //            {
    //                GameObject effectObj = Instantiate(yakumanExplosionPrefab, impactPos, Quaternion.identity);
    //                
    //                var effectScript = effectObj.GetComponent<YakumanExplosionEffect>();
    //                if (effectScript != null && _yakuVideoMap != null)
    //                {
    //                    foreach (var yaku in result.YakuList)
    //                    {
    //                        if (_yakuVideoMap.ContainsKey(yaku) && _yakuVideoMap[yaku] != null)
    //                        {
    //                            clipToPlay = _yakuVideoMap[yaku];
    //                            break;
    //                        }
    //                    }
    //                    effectScript.SetupVideo(clipToPlay);
    //                }
    //                
    //                // 時間停止しないので、強制再生(Play)やFreerun設定は不要
    //                // プレハブ側のスクリプト(YakumanExplosionEffect)に再生を任せます
    //            }

    //            // --- 牌を吹き飛ばす物理力適用 (Config使用) ---
    //            Vector3 physicalExplosionPos = tsumo.transform.position; 
    //            physicalExplosionPos.y = 0;

    //            // Configから値を取得（定義されていない場合のデフォルト値も考慮）
    //            float force = (config != null) ? config.YakumanExplosionForce : 8000f;
    //            float radius = (config != null) ? config.YakumanExplosionRadius : 30.0f;
    //            float upMod = (config != null) ? config.YakumanUpwardsModifier : 3.0f;
    //            float torque = (config != null) ? config.YakumanTorque : 100f;

    //            foreach (var tile in winner.HandTiles)
    //            {
    //                if (tile == null) continue;
    //                var rb = tile.GetComponent<Rigidbody>();
    //                if (rb != null)
    //                {
    //                    rb.AddExplosionForce(force, physicalExplosionPos, radius, upMod, ForceMode.Impulse);
    //                    rb.AddTorque(Random.onUnitSphere * torque, ForceMode.Impulse);
    //                }
    //            }
    //            var tsumoRb = tsumo.GetComponent<Rigidbody>();
    //            if (tsumoRb != null)
    //            {
    //                tsumoRb.AddExplosionForce(force, physicalExplosionPos, radius, upMod, ForceMode.Impulse);
    //                tsumoRb.AddTorque(Random.onUnitSphere * torque, ForceMode.Impulse);
    //            }

    //            StartCoroutine(CameraShake(1.2f, 2.0f));

    //            // ==========================================================
    //            // 動画の長さ分待機 (時間停止なし)
    //            // ==========================================================
    //            if (clipToPlay != null)
    //            {
    //                float videoDuration = (float)clipToPlay.length;
    //                // 動画の長さ + 少しの余韻 を通常の時間で待つ
    //                yield return new WaitForSeconds(videoDuration + 1.0f);
    //            }
    //            else
    //            {
    //                // 動画がない場合は通常の余韻
    //                yield return new WaitForSeconds(3.0f);
    //            }
    //        }
    //        else
    //        {
    //            // --- 通常演出 (Config使用) ---
    //            float spread = (config != null) ? config.NormalImpactSpread : 5f;
    //            float upForce = (config != null) ? config.NormalUpForce : 15f;
    //            float zForce = (config != null) ? config.NormalZForce : 5f;
    //            float normTorque = (config != null) ? config.NormalTorque : 10f;

    //            foreach (var tile in winner.HandTiles)
    //            {
    //                if (tile == null) continue;
    //                var rb = tile.GetComponent<Rigidbody>();
    //                if (rb != null)
    //                {
    //                    rb.AddForce(new Vector3(Random.Range(-spread, spread), upForce, zForce), ForceMode.Impulse);
    //                    rb.AddTorque(Random.onUnitSphere * normTorque, ForceMode.Impulse);
    //                }
    //            }
    //            StartCoroutine(CameraShake(0.3f, 0.5f));
    //            
    //            // 通常時の待機
    //            yield return new WaitForSeconds(settleTime);
    //        }
    //    }
    //    else
    //    {
    //        // ツモ牌がない場合（ロンなど）のフォールバック
    //        yield return new WaitForSeconds(2.5f);
    //    }

    //    // --- 結果表示 ---
    //    int[] omoteIds = GetDoraIds(false);
    //    int[] uraIds = (winner.IsRiichi || winner.IsDoubleRiichi) ? GetDoraIds(true) : null;
    //    
    //    string yakuStr = string.Join(" / ", result.YakuList);
    //    
    //    winner.Score += result.TotalScore; 
    //    CurrentScore = winner.Score;       
    //    
    //    CheckAndUnlockYakuman(result.YakuList);

    //    AnnounceWin(winner, 0, result.TotalScore, yakuStr, omoteIds, uraIds, result.Han, result.Fu, result.ScoreName);
    //}

    private IEnumerator TsumoImpactRoutine(MahjongPlayer winner)
    {
        // 1. スコア計算
        ScoringResult result = RecalculateForPayment(winner, false);
        bool isYakuman = result.Han >= 13;
        string yakumanVideoName = null;
        bool useSharedYakumanVideo = false;

        // 役満動画はできるだけ早くPrepareを開始して、初回再生ラグを隠す
        if (isYakuman)
        {
            yakumanVideoName = ResolveYakumanVideoName(result.YakuList);
            _pendingYakumanVideoName = yakumanVideoName;
            useSharedYakumanVideo = videoController != null && !string.IsNullOrEmpty(yakumanVideoName);
            if (useSharedYakumanVideo)
            {
                videoController.SetupVideo(yakumanVideoName);
            }
        }

        // 2. 物理挙動解放
        winner.ReleasePhysicalTiles();

        // 3. 溜め
        yield return new WaitForSeconds(isYakuman ? 1.8f : 1.2f);

        // 4. ツモ牌叩きつけ
        if (winner.TsumoTile != null)
        {
            GameObject tsumo = winner.TsumoTile.gameObject;
            float impactDuration = 0.15f;

            ConfigureTsumoPhysics(tsumo, isYakuman, impactDuration);

            // 落下にかかる時間分だけ待つ
            yield return new WaitForSeconds(impactDuration);

            var canvas = FindAnyObjectByType<MahjongCanvas>();
            if (canvas != null)
            {
                canvas.PlayImpactSE(); // 衝撃音再生
            }
            var se = SeController.GetOrFindInstance();
            if (se != null)
            {
                if (isYakuman) se.Play(yakumanExplosionSeKey);
                else se.Play(normalTsumoSeKey);
            }
            Vector3 impactPos = tsumo.transform.position;
            impactPos.y = 0.2f;

            if (isYakuman)
            {
                GameObject effectObj = null;
                YakumanExplosionEffect effectScript = null;

                if (yakumanExplosionPrefab != null)
                {
                    effectObj = Instantiate(yakumanExplosionPrefab, impactPos, Quaternion.identity);
                    effectScript = effectObj.GetComponent<YakumanExplosionEffect>();
                }

                // 牌を吹き飛ばす物理力適用
                Vector3 physicalExplosionPos = impactPos;
                physicalExplosionPos.y = 0f;

                foreach (var tile in winner.HandTiles)
                {
                    if (tile == null) continue;
                    var rb = tile.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.AddExplosionForce(8000f, physicalExplosionPos, 30.0f, 3.0f, ForceMode.Impulse);
                        rb.AddTorque(Random.onUnitSphere * 100f, ForceMode.Impulse);
                    }
                }

                var tsumoRb = tsumo.GetComponent<Rigidbody>();
                if (tsumoRb != null)
                {
                    tsumoRb.AddExplosionForce(8000f, physicalExplosionPos, 30.0f, 3.0f, ForceMode.Impulse);
                    tsumoRb.AddTorque(Random.onUnitSphere * 100f, ForceMode.Impulse);
                }

                StartCoroutine(CameraShake(1.2f, 2.0f));

                // 爆発の余韻を見せるために2秒ほど待機
                yield return new WaitForSeconds(2.0f);

                isYakumanProductionTriggered = false;
                if (canvas != null)
                {
                    canvas.ShowYakumanConfirmUI();
                }

                // ボタン押下待ち（OnClick -> StartYakumanProduction）
                yield return new WaitUntil(() => isYakumanProductionTriggered);

                if (useSharedYakumanVideo)
                {
                    // 念のため再要求（未準備時は内部でキューされる）
                    videoController.PlayVideo();
                    yield return StartCoroutine(WaitYakumanVideoFinishOrTimeout(90f));
                    if (effectObj != null)
                    {
                        Destroy(effectObj, 3.5f);
                    }
                }
                else if (effectScript != null && !string.IsNullOrEmpty(yakumanVideoName))
                {
                    // フォールバック: 既存の爆発オブジェクト上で再生
                    effectScript.SetupVideo(yakumanVideoName);

                    float fallbackWait = 60f;
                    float elapsed = 0f;
                    while (!effectScript.IsVideoFinished && elapsed < fallbackWait)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        yield return null;
                    }

                    if (!effectScript.IsVideoFinished)
                    {
                        Debug.LogWarning("[Yakuman] Fallback video wait timed out. Continue.");
                        Destroy(effectObj);
                    }
                }
                else if (effectObj != null)
                {
                    // 動画なしの場合、爆発演出だけ少し残してから破棄
                    Destroy(effectObj, 3.5f);
                }

                _pendingYakumanVideoName = null;
            }
            else
            {
                // --- 通常演出 ---
                foreach (var tile in winner.HandTiles)
                {
                    if (tile == null) continue;
                    var rb = tile.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.AddForce(new Vector3(Random.Range(-5f, 5f), 15f, 5f), ForceMode.Impulse);
                        rb.AddTorque(Random.onUnitSphere * 10f, ForceMode.Impulse);
                    }
                }
                StartCoroutine(CameraShake(0.3f, 0.5f));
                yield return new WaitForSeconds(2.5f);
            }
        }
        else
        {
            yield return new WaitForSeconds(2.5f);
        }

        _pendingYakumanVideoName = null;

        // ====================================================
        // ④ 結果表示 (AnnounceWin)
        // ====================================================
        int[] omoteIds = GetDoraIds(false);
        int[] uraIds = (winner.IsRiichi || winner.IsDoubleRiichi) ? GetDoraIds(true) : null;

        string yakuStr = string.Join(" / ", result.YakuList);

        winner.Score += result.TotalScore;
        CurrentScore = winner.Score;

        CheckAndUnlockYakuman(result.YakuList);

        AnnounceWin(winner, 0, result.TotalScore, string.Join(" / ", result.YakuList), omoteIds, uraIds, result.Han, result.Fu, result.ScoreName);
    }
    // ... (前略)

    // TsumoImpactRoutine から呼ばれる物理設定メソッド
    private void ConfigureTsumoPhysics(GameObject tsumo, bool isYakuman, float duration)
    {
        var rb = tsumo.GetComponent<Rigidbody>();
        var col = tsumo.GetComponent<Collider>();
        
        // 物理挙動の有効化
        rb.isKinematic = false;
        rb.useGravity = true; 
        col.isTrigger = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        
        // 質量設定（役満なら重くして破壊力を増す）
        rb.mass = isYakuman ? 5.0f : 1.0f;

        // 1. 現在の位置（着地地点）を保存
        Vector3 landPos = tsumo.transform.position;

        // 2. 開始位置を「真上」に設定
        float startHeight = 125.0f; // かなり高い位置から
        Vector3 startPos = landPos;
        startPos.y = startHeight; // X, Zは変えずにYだけ高くする（＝真上）
        
        tsumo.transform.position = startPos;
        
        // ★追加: 光る軌跡を有効化
        var tileScript = tsumo.GetComponent<MahjongTile>();
        if (tileScript != null)
        {
            tileScript.EnableTrailEffect(true);
        }

        // 3. 初速の計算（等加速度直線運動の公式より逆算）
        // 変位 y = v0*t + 1/2*g*t^2
        // v0 = (y - 1/2*g*t^2) / t
        // ここで y は変位（landPos.y - startPos.y）、g は重力加速度(通常マイナス)
        
        float displacementY = landPos.y - startPos.y;
        float gravityY = Physics.gravity.y;
        float time = duration;

        float initialVelocityY = (displacementY - 0.5f * gravityY * time * time) / time;

        // X, Z方向の移動はない（真上からなので0）が、念のため計算式に入れるなら
        // v0_x = (landPos.x - startPos.x) / time; -> 0
        // v0_z = (landPos.z - startPos.z) / time; -> 0
        
        Vector3 initialVelocity = new Vector3(0, initialVelocityY, 0);

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = initialVelocity;
#else
        rb.velocity = initialVelocity;
#endif

        // 回転を加える（真下に落ちるが、牌自体は回転して迫力を出す）
        rb.AddTorque(Random.onUnitSphere * 50f, ForceMode.Impulse);
    }


// 不足していた物理設定用メソッド
private void ConfigureTsumoPhysics(GameObject tsumo, bool isYakuman)
{
    var rb = tsumo.GetComponent<Rigidbody>();
    var col = tsumo.GetComponent<Collider>();
    
    rb.isKinematic = false;
    rb.useGravity = true;
    col.isTrigger = false;

    // 高速移動ですり抜けないための設定
    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    
    // 役満の時はツモ牌を「隕石」のように重くする
    rb.mass = isYakuman ? 10f : 5f;

    // 開始位置を上空に設定
    tsumo.transform.position += Vector3.up * 18f;
    
    // 真下へ加速
    rb.linearVelocity = Vector3.zero;
    float impactPower = isYakuman ? 500f : 120f;
    rb.AddForce(Vector3.down * impactPower, ForceMode.Impulse);
    rb.AddTorque(Random.onUnitSphere * 50f, ForceMode.Impulse);
}
private void ExecuteYakumanExplosion(Vector3 pos, MahjongPlayer winner)
{
    // 1. パーティクル生成（火と煙）
    if (yakumanExplosionPrefab != null)
    {
        Instantiate(yakumanExplosionPrefab, pos, Quaternion.identity);
    }

    // 2. 全ての牌を爆風で吹き飛ばす
    foreach (var tile in winner.HandTiles)
    {
        if (tile == null) continue;
        var rb = tile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // 爆風で外側かつ上方向へ強く飛ばす
            rb.AddExplosionForce(5000f, pos, 30f, 10.0f);
            rb.AddTorque(Random.onUnitSphere * 100f, ForceMode.Impulse);
        }
    }
}

private void ExecuteNormalImpact(MahjongPlayer winner)
{
    foreach (var tile in winner.HandTiles)
    {
        if (tile == null) continue;
        var rb = tile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(new Vector3(Random.Range(-5f, 5f), 25f, 15f), ForceMode.Impulse);
        }
    }
}
    // --- ★追加: 役満実績保存ロジック ---
    private void CheckAndUnlockYakuman(List<string> yakuList)
    {
        foreach (var yaku in yakuList)
        {
            // 定義された役満リストに含まれているか確認
            if (TargetYakumanList.Contains(yaku))
            {
                string key = "Yakuman_" + yaku;
                // まだ解除されていない場合のみ保存（初回解除）
                if (PlayerPrefs.GetInt(key, 0) == 0)
                {
                    PlayerPrefs.SetInt(key, 1);
                    Debug.Log($"<color=cyan>【実績解除】New Yakuman Unlocked: {yaku}</color>");
                }
            }
        }
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 解除済みの役満の個数を返す
    /// </summary>
    public int GetUnlockedYakumanCount()
    {
        int count = 0;
        foreach (var yakumanName in TargetYakumanList)
        {
            string key = "Yakuman_" + yakumanName;
            if (PlayerPrefs.GetInt(key, 0) == 1)
            {
                count++;
            }
        }
        return count;
    }
    // ------------------------------------

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

    private void AnnounceWin(MahjongPlayer winner, int seat, int score, string yakuStr, int[] doraIndicators, int[] uraDoraIndicators,int han, int fu, string scoreName)
    {
        Debug.Log($"Win! Score:{score}");
        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
            canvas.ShowWinResult(winner, seat, score, yakuStr, doraIndicators, uraDoraIndicators, han, fu, scoreName);
        }
    }

    // RecalculateForPayment メソッド全体を以下のように修正してください

private ScoringResult RecalculateForPayment(MahjongPlayer player)
{
    int[] tileCounts = new int[37];
    foreach (var t in player.HandTiles) if(t) tileCounts[t.TileId]++;
    if (player.TsumoTile != null) tileCounts[player.TsumoTile.TileId]++;

    List<int> meldIds = new List<int>();
    foreach (var t in player.MeldTiles) if(t) meldIds.Add(t.TileId);

    ScoringContext context = new ScoringContext();
    
    context.IsTsumo = true;   
    context.IsDealer = (player.Seat == 0); // 修正: プレイヤーの席順で親判定
    context.IsMenzen = true; 
    context.SeatWind = player.Seat;     

    context.IsFirstTurn = player.IsFirstTurn; 
    
    context.IsRiichi = player.IsRiichi;
    context.IsDoubleRiichi = player.IsDoubleRiichi;
    context.IsIppatsu = player.IsIppatsuChance; 

    // ★重要修正: WinningTileId（上がり牌）をセットする
    // 赤ドラ(34,35,36)の場合、通常のID(4,13,22)に変換してセットする必要があります
    if (player.TsumoTile != null)
    {
        int rawId = player.TsumoTile.TileId;
        int normalId = rawId;
        if (rawId == 34) normalId = 4;
        else if (rawId == 35) normalId = 13;
        else if (rawId == 36) normalId = 22;
        
        context.WinningTileId = normalId;
    }

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

    private bool SpawnAndGiveTsumo(MahjongPlayer player)
    {
        if (deck.Count <= 0) { Debug.Log("【エラー】デッキID取得失敗"); return false; } 

        int tileType = DrawTileFromDeck();
        if (tileType == -1) { Debug.LogError("【エラー】デッキID取得失敗"); return false; }

        MahjongTile tileObj = SpawnTileObject(tileType, player);
        if (tileObj != null) player.SetTsumoTile(tileObj);
        else
        {
            Debug.LogError("【エラー】牌生成失敗");
            return false;
        }

        if (player.IsHuman)
        {
            player.RefreshWaitsAsync();
        }

        return true;
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

        // 0-8: Manzu, 9-17: Pinzu, 18-26: Sozu, 27-33: Honors
        for (int i = 0; i < 34; i++)
        {
            bool isTarget = false;

            switch (config.DeckMode)
            {
                case DebugDeckMode.Normal:
                    if (i >= 0 && i <= 8) isTarget = config.UseManzu;
                    else if (i >= 9 && i <= 17) isTarget = config.UsePinzu;
                    else if (i >= 18 && i <= 26) isTarget = config.UseSozu;
                    else if (i >= 27 && i <= 33) isTarget = config.UseHonors;
                    break;

                case DebugDeckMode.Chinitsu:
                    // 指定された色のみ
                    if (config.ChinitsuSuit == TileSuit.Manzu && i >= 0 && i <= 8) isTarget = true;
                    else if (config.ChinitsuSuit == TileSuit.Pinzu && i >= 9 && i <= 17) isTarget = true;
                    else if (config.ChinitsuSuit == TileSuit.Sozu && i >= 18 && i <= 26) isTarget = true;
                    break;

                case DebugDeckMode.JihaiOnly:
                    // 字牌(27-33)のみ
                    if (i >= 27 && i <= 33) isTarget = true;
                    break;

                case DebugDeckMode.TanyaoOnly:
                    // 2-8の数牌のみ (字牌NG、1,9 NG)
                    if (i >= 27) isTarget = false; // 字牌
                    else
                    {
                        int num = i % 9; // 0=1, 8=9
                        if (num != 0 && num != 8) isTarget = true;
                    }
                    break;

                case DebugDeckMode.YaochuOnly:
                    // 1,9,字牌のみ
                    if (i >= 27) isTarget = true; // 字牌
                    else
                    {
                        int num = i % 9;
                        if (num == 0 || num == 8) isTarget = true;
                    }
                    break;
            }

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
        
        // シャッフル
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

        Debug.Log($"[Deck Created] Mode: {config.DeckMode}, Tiles: {deck.Count}");
    }

    private int DrawTileFromDeck()
    {
        if (deck.Count == 0) return -1;
        int t = deck[0];
        deck.RemoveAt(0);
        return t;
    }
    // ... (前略)

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
                
                // ★修正: 生成した牌をPlayerの子オブジェクトにする
                // これにより、StartRound()で player.gameObject がDestroyされたときに道連れで消えるようになります
                tileObj.transform.SetParent(player.transform);

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
                ? Quaternion.Euler(-90f, 0f, 180f) : Quaternion.Euler(90f, 0f, 0f);

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
    
    public void RequestDebugRestart() { StartNewGame(); }

    private void RevealUraDoraIndices()
    {
        uraDoraIndicatorIndices.Clear();
        foreach (int idx in doraIndicatorIndices) {
            int uraIdx = idx + 1;
            if (uraIdx < deadWall.Count) uraDoraIndicatorIndices.Add(uraIdx);
        }
    }
    private IEnumerator RoundStartSequence(MahjongPlayer player)
    {
        var se = SeController.GetOrFindInstance();
        if (se != null)
        {
            se.Play(roundStartSeKey);
        }

        // 1. Canvasのアニメーションを再生して待機
        var canvas = FindAnyObjectByType<MahjongCanvas>();
        if (canvas != null)
        {
            // RoundCount, HonbaCount を渡して表示
            yield return StartCoroutine(canvas.PlayRoundStartAnimation(RoundCount, HonbaCount));
        }
        else
        {
            // Canvasが見つからない場合は少しだけ待つ
            yield return new WaitForSeconds(0.5f);
        }

        // 2. 配牌（元々StartRoundにあったループ処理）
        int handCount = config.HandTileCount;
        for (int i = 0; i < handCount; i++) 
        {
            SpawnAndGiveTileToHand(player);
            // 少しウェイトを入れると「配っている感」が出ます（お好みで）
            yield return new WaitForSeconds(0.05f); 
        }

        // 3. 理牌（ソート）を一度行う
        // player.SortHandTiles(); // 必要なら追加

        // 4. ゲーム開始フラグON
        IsGameStarted = true;
        
        // 5. 最初のツモへ
        StartCoroutine(NextDrawRoutine());
    }

}
