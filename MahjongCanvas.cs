using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using unityroom.Api;
using System.Collections.Generic;

public class MahjongCanvas : MonoBehaviour
{
    [Header("UI References")]
    public RectTransform buttonPanel;
    public Button riichiButton;
    public Button kanButton;
    public Button winButton;
    public Button ronButton;
    public Button skipButton;

    public Button sortButton;
    public Button debugDealButton;
    public TMP_Text statusText;

    public TMP_Text[] scoreTexts;

    public TMP_Text roundCountText;
    public TMP_Text remainingTilesText;

    [Header("Win Result UI")]
    public GameObject resultPanel;
    public TMP_Text resultText;    // 役名などの表示用

    // ★追加: 点数・ランク（倍満など）表示用のテキスト
    // (InspectorでResultPanel内に新しいTextMeshProを作成してアサインしてください)
    public TMP_Text winScoreText;

    public Button nextRoundButton;

    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TMP_Text finalScoreText;
    public TMP_Text totalCumulativeText;
    public Button retryButton;
    public Button sendRankingButton;
    public Button finishButton;

    private MahjongPlayer _localPlayer;

    public Transform doraContainer;
    public Transform uraDoraContainer;

    [Header("Tile Sprites")]
    public Sprite[] tileSprites;
    public Sprite tileBackSprite;

    private int _lastFinalScore = 0;

    [Header("Assist UI")]
    public GameObject ukeirePanel;      // 表示用の親パネル
    public Transform ukeireGrid;        // 牌画像を並べる親オブジェクト


    [Header("Modules")]
    public YakuVoiceManager voiceManager;
    [Header("Result Hand UI")]
    // ★追加: リザルトパネル内に手牌を並べるための親オブジェクト
    // (ResultPanelの中に空のGameObjectを作り、Horizontal Layout Groupを設定してここにアサインしてください)
    public Transform resultHandContainer;


    [Header("Unityroom (Rescue)")]
    [SerializeField] private GameObject unityroomApiClientPrefab; // ★追加: プレハブをここにもセットする

    [Header("Voice Settings")]
    public AudioSource sfxAudioSource; // インスペクターでAudioSourceをアサイン
    public List<AudioClip> riichiVoices;      // 「リーチ」
    public List<AudioClip> tsumoVoices;       // 「ツモ」
    public List<AudioClip> kanVoices;         // 「カン」
    public List<AudioClip> ronVoices;         // 「ロン」（ついでに追加しておくと便利です）

    // ★追加: 衝撃音（SE）用の変数
    [Header("SE Settings")]
    public AudioClip impactSE; // ここに「ドーン！」などの効果音をセット

    [Header("Round Start UI")]
    public GameObject roundStartPanel; // インスペクターでパネルを割り当て
    public TMP_Text roundNameText;     // "東1局" などを表示
    public TMP_Text honbaText;         // "0本場" などを表示

    [Header("Ankan Select UI")]
    // ★追加: 選択パネルの親オブジェクト (Canvas上にPanelを作成してアサインしてください)
    public GameObject ankanSelectPanel;
    // ★追加: ボタンを並べるグリッド (Panelの下にHorizontal Layout Groupを持つ空オブジェクトを作り、アサインしてください)
    public Transform ankanSelectGrid;
    // ★追加: キャンセルボタン（必要であればPanel内に配置してアサイン）
    public Button ankanCancelButton;
    // ★追加: 局開始アニメーションを再生するコルーチン
    private bool _isSelectingAnkan = false;

    [Header("Gimmick UI")]
    public GameObject gimmickPanel; // ★追加: ギミック表示用の親パネル
    public TMP_Text gimmickText;    // ★追加: 「大三元 確定！」などを出すテキスト

    [Header("Gimmick Voices")]
    public AudioClip[] daisangenGimmickVoices;
    public AudioClip[] chuurenGimmickVoices;
    public AudioClip[] daisushiGimmickVoices;
    public AudioClip[] allgreenGimmickVoices;
    public AudioClip[] tsuisoGimmickVoices;
    public AudioClip[] kokushimusouGimmickVoices;

    private AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }


    // ★追加: ギミック演出の開始
    public void PlayGimmickAnnouncement(string yakuName)
    {
        StartCoroutine(GimmickAnnouncementRoutine(yakuName));
    }

    // ★追加: 音声の長さに合わせてテキストを表示するコルーチン
    private IEnumerator GimmickAnnouncementRoutine(string yakuName)
    {
        AudioClip voice = null;
        string displayText = "";

        if (yakuName == "大三元")
        {
            voice = GetRandomClip(daisangenGimmickVoices);
            displayText = "大三元確定";
        }
        else if (yakuName == "九蓮宝燈")
        {
            voice = GetRandomClip(chuurenGimmickVoices);
            displayText = "九蓮宝燈確定";
        }
        else if (yakuName == "大四喜")
        {
            voice = GetRandomClip(daisushiGimmickVoices);
            displayText = "大四喜確定";
        }
        else if (yakuName == "緑一色")
        {
            voice = GetRandomClip(allgreenGimmickVoices);
            displayText = "緑一色確定";
        }
        else if (yakuName == "字一色")
        {
            voice = GetRandomClip(tsuisoGimmickVoices);
            displayText = "字一色確定";
        }
        else if (yakuName == "国士無双")
        {
            voice = GetRandomClip(kokushimusouGimmickVoices);
            displayText = "国士無双確定";
        }


        // 音声再生
        if (sfxAudioSource != null && voice != null)
        {
            sfxAudioSource.PlayOneShot(voice);
        }

        // テキストとパネルの表示
        if (gimmickPanel != null && gimmickText != null)
        {
            gimmickText.text = displayText;
            gimmickPanel.SetActive(true);

            // 音声の長さ分（音声がなければ最低2秒）表示して待機
            float waitTime = (voice != null) ? voice.length : 2.0f;
            yield return new WaitForSeconds(waitTime);

            // 非表示に戻す
            gimmickPanel.SetActive(false);
        }
    }
    public IEnumerator PlayRoundStartAnimation(int roundCount, int honbaCount)
    {
        if (roundStartPanel != null)
        {
            // 1. テキストの更新
            if (roundNameText != null)
            {
                // ラウンド数から "東○局" "南○局" を計算 (4人打ち想定: 1-4=東, 5-8=南)
                //string wind = (roundCount <= 4) ? "東" : "南";
                string wind = "東";
                // int number = ((roundCount - 1) % 4) + 1;
                int number = 1;
                roundNameText.text = $"{wind} {number} 局";
                roundCountText.text = $"{wind} {number} 局";
            }

            if (honbaText != null)
            {
                honbaText.text = $"{honbaCount} 本場";
                roundCountText.text += $"\n{honbaCount} 本場";
            }

            // 2. パネル表示
            roundStartPanel.SetActive(true);

            // 3. 待機 (例: 2秒間表示)
            yield return new WaitForSeconds(2.0f);

            // 4. パネル非表示
            roundStartPanel.SetActive(false);
        }
        else
        {
            // パネルがない場合は即終了
            yield return null;
        }
    }

    // ... (中略) ...

    // ★追加: 衝撃音を再生するメソッド
    public void PlayImpactSE()
    {
        if (sfxAudioSource != null)
        {
            PlayRandomVoice(tsumoVoices);
        }
    }

    private void Start()
    {
        if (FakeCursor.Instance != null)
        {
            FakeCursor.Instance.SetVisible(true);
            FakeCursor.Instance.SetScale(1.0f);
        }
        var client = FindAnyObjectByType<unityroom.Api.UnityroomApiClient>();

        if (client == null)
        {
            if (unityroomApiClientPrefab != null)
            {
                Instantiate(unityroomApiClientPrefab);
                Debug.Log("<color=green>MahjongCanvas: UnityroomApiClientを緊急生成しました</color>");
            }
            else
            {
                Debug.LogWarning("MahjongCanvas: UnityroomApiClientのプレハブがセットされていません");
            }
        }
        if (riichiButton != null) riichiButton.onClick.AddListener(OnRiichiClicked);
        if (kanButton != null) kanButton.onClick.AddListener(OnKanClicked);
        if (winButton != null) winButton.onClick.AddListener(OnWinClicked);
        if (sortButton != null) sortButton.onClick.AddListener(OnSortClicked);
        if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
        if (debugDealButton != null) debugDealButton.onClick.AddListener(OnDebugDealClicked);
        if (ronButton != null) ronButton.onClick.AddListener(OnRonClicked);

        if (nextRoundButton != null) nextRoundButton.onClick.AddListener(OnNextRoundClicked);

        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (sendRankingButton != null) sendRankingButton.onClick.AddListener(OnSendRankingClicked);
        if (finishButton != null) finishButton.onClick.AddListener(OnFinishClicked);

        if (riichiButton != null) riichiButton.gameObject.SetActive(false);
        if (kanButton != null) kanButton.gameObject.SetActive(false);
        if (winButton != null) winButton.gameObject.SetActive(false);
        if (skipButton != null) skipButton.gameObject.SetActive(false);
        if (ronButton != null) ronButton.gameObject.SetActive(false);

        if (resultPanel != null) resultPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        if (ankanSelectPanel != null) ankanSelectGrid.gameObject.SetActive(false);
        if (ankanCancelButton != null)
        {
            ankanCancelButton.onClick.RemoveAllListeners();
            ankanCancelButton.onClick.AddListener(OnAnkanCancelClicked);
        }
    }

    private void Update()
    {
        if (_localPlayer == null) FindLocalPlayer();

        if (_localPlayer != null)
        {
            if (ankanSelectPanel != null && !ankanSelectPanel.activeSelf && _isSelectingAnkan)
            {
                _isSelectingAnkan = false;
            }
            UpdateActionButtons();
            UpdateStatusText();
            UpdateSortButton();
            UpdateScoreTexts();
        }
        UpdateRemainingTilesText();
    }
    private void UpdateRemainingTilesText()
    {
        if (remainingTilesText != null && MahjongGameManager.Instance != null)
        {
            int count = MahjongGameManager.Instance.TilesRemainingInWall;
            remainingTilesText.text = $"残り: {count}";

            // 残り枚数が少ない時に色を変える演出（お好みで）
            if (count == 0) remainingTilesText.color = Color.red;
            else if (count <= 10) remainingTilesText.color = Color.yellow;
            else remainingTilesText.color = Color.white;
        }
    }

    private void UpdateActionButtons()
    {
        if (_isSelectingAnkan)
        {
            HideAllActionButtons();
            return;
        }
        bool isMyTurn = (MahjongGameManager.Instance != null && MahjongGameManager.Instance.CurrentTurnSeat == _localPlayer.Seat);
        bool isGameActive = MahjongGameManager.Instance.IsGameStarted;

        if (!isGameActive)
        {
            HideAllActionButtons();
            return;
        }
        // ★追加: 海底（残り山0枚）かどうか判定
        bool isHaitei = false;
        if (MahjongGameManager.Instance != null)
        {
            isHaitei = (MahjongGameManager.Instance.TilesRemainingInWall == 0);
        }

        // ★修正: 海底では立直不可
        bool canRiichi = isMyTurn
                         && !_localPlayer.IsRiichi
                         && !_localPlayer.IsRiichiPending
                         && (_localPlayer.CurrentShanten <= 0)
                         && !isHaitei; // ← 条件追加

        // ★修正: 海底ではカン不可（嶺上牌が引けないため）
        bool canKan = isMyTurn
                      && _localPlayer.AvailableAnkanTiles.Count > 0
                      && !_localPlayer.IsRiichiPending
                      && !_localPlayer.IsRiichi
                      && !isHaitei; // ← 条件追加
        bool canWin = isMyTurn && (_localPlayer.CurrentShanten == -1);

        bool isRiichiAnkanChance = _localPlayer.IsRiichi && _localPlayer.AvailableAnkanTiles.Count > 0;

        // ★修正: _localPlayer.IsRiichiPending を削除
        // これにより、立直宣言中（牌選択中）にスキップボタンが出なくなります
        bool canSkip = canRiichi || canKan || canWin || isRiichiAnkanChance || _localPlayer.IsRiichiPending;

        bool canRon = !isMyTurn && _localPlayer.CanRon;

        if (_localPlayer.IsRiichi)
        {
            canKan = (_localPlayer.AvailableAnkanTiles.Count > 0);
        }

        SetButtonActive(riichiButton, canRiichi);
        SetButtonActive(kanButton, canKan);
        SetButtonActive(winButton, canWin);
        SetButtonActive(ronButton, canRon);
        SetButtonActive(skipButton, canSkip);

        if (canRon)
        {
            SetButtonActive(skipButton, true);
        }
    }
    public void ShowRyuukyoku(bool isTenpai, int currentScore)
    {
        if (resultText == null || resultPanel == null) return;

        string title = isTenpai ? "<color=green>RYUUKYOKU (Tenpai)</color>" : "<color=red>RYUUKYOKU (No-ten)</color>";
        string info = isTenpai ? "Safe! You can proceed to next round." : "Game Over...";

        resultText.text = $"{title}\n\n{info}\n\n" +
                          $"<size=100%>Total Score: {currentScore}</size>";

        ClearContainer(doraContainer);
        ClearContainer(uraDoraContainer);
        // if (uraDoraLabel != null) uraDoraLabel.SetActive(false);

        if (nextRoundButton != null) nextRoundButton.gameObject.SetActive(isTenpai);

        resultPanel.SetActive(true);
    }

    public void ShowGameOver(int finalScore)
    {
        if (gameOverPanel == null) return;

        _lastFinalScore = finalScore;

        int highScore = PlayerPrefs.GetInt("HighScore", 0);
        bool isNewRecord = false;

        if (finalScore > highScore)
        {
            highScore = finalScore;
            PlayerPrefs.SetInt("HighScore", highScore);
            PlayerPrefs.Save();
            isNewRecord = true;
        }

        // UI表示の更新
        if (finalScoreText != null)
        {
            string msg = $"<size=200%>流局</size>\n\n最終スコア\n<size=20%> </size>\n<size=120%><color=yellow>{finalScore}</color></size>";

            if (isNewRecord)
            {
                msg += "\n<size=20%> </size>\n<size=80%><color=red>New High Score!</color></size>";
            }
            else
            {
                msg += $"\n<size=20%> </size>\n<size=70%>(Best: {highScore})</size>";
            }

            finalScoreText.text = msg;
        }

        if (totalCumulativeText != null && MahjongGameManager.Instance != null)
        {
            int yakumanCount = MahjongGameManager.Instance.GetUnlockedYakumanCount();
            // 役満実績数の表示
            totalCumulativeText.text = $"役満実績解除数\n<size=20%> </size>\n<size=150%><color=orange>{yakumanCount} / 15</color></size>";
        }

        gameOverPanel.SetActive(true);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    // ★重要変更: ShowWinResult
    public void ShowWinResult(MahjongPlayer winner, int seat, int score, string yakuStr, int[] doraInds, int[] uraInds, int han, int fu, string scoreName)
    {
        if (resultText == null || resultPanel == null) return;

        // 1. ヘッダー情報の表示
        string header = "";

        // 2. 役リストの処理
        List<string> yakuList = new List<string>(yakuStr.Split(new string[] { " / " }, System.StringSplitOptions.RemoveEmptyEntries));

        // 3. スコア詳細のテキスト作成（まだ表示しない）
        int totalScore = 0;
        if (MahjongGameManager.Instance != null) totalScore = MahjongGameManager.Instance.CurrentScore;

        string scoreDetail = "";
        if (han >= 100) // 役満以上
        {
            // 役満の場合は点数のみ、あるいは役満名称のみなどシンプルに
            //scoreDetail = $"<size=120%>{scoreName}</size>"; 
            scoreDetail = $"<size=120%>{scoreName}</size>";
        }
        else
        {
            // 通常手
            string rank = string.IsNullOrEmpty(scoreName) ? "" : $"{scoreName}";
            // scoreDetail = $"<size=100%>{fu}符 {han}飜  <color=yellow>{rank}</color></size>";
            // 文字サイズ調整などは適宜行ってください
            scoreDetail = $"<size=100%>{fu}符 {han}飜  <color=yellow>{rank}</color></size>";
        }

        string footer = $"{scoreDetail}\n" +
                        $"<size=140%><color=orange>+{score}</color></size>\n" +
                        $"<size=80%>Total: {totalScore}</size>";

        // 4. ドラ表示 (既存のロジック)
        ClearContainer(doraContainer);
        if (doraInds != null)
        {
            foreach (int id in doraInds) CreateTileImage(id, doraContainer);
        }

        ClearContainer(uraDoraContainer);
        if (uraInds != null && uraInds.Length > 0)
        {
            if (uraDoraContainer != null)
            {
                uraDoraContainer.gameObject.SetActive(true);
                foreach (int id in uraInds) CreateTileImage(id, uraDoraContainer);
            }
        }
        else
        {
            if (uraDoraContainer != null) uraDoraContainer.gameObject.SetActive(false);
        }

        // 5. 手牌表示
        ShowResultHand(winner);

        // 6. UI初期化（点数は隠しておく）
        resultPanel.SetActive(true);
        if (nextRoundButton != null) nextRoundButton.gameObject.SetActive(true);

        // ★修正: メインテキストにはヘッダーと役だけを表示
        // (yakuStrを表示するか、VoiceManager側で1行ずつ出すかはお好みですが、ここでは初期状態として全部出さずにヘッダーだけ、あるいはVoiceManagerに任せる形にします)
        // 今回のVoiceManagerの仕様上、yakuListを渡すと1行ずつ追加していくスタイルなので、最初はヘッダーのみにしておきます。
        resultText.text = "";

        // ★追加: 点数テキストを初期化して非表示に
        if (winScoreText != null)
        {
            winScoreText.text = footer;   // テキストはセットしておく
            winScoreText.gameObject.SetActive(false); // まだ見せない
        }

        // 7. 音声再生と演出の開始
        if (voiceManager != null)
        {
            // ★変更: 引数に「役読み上げが終わった後のアクション」を追加
            voiceManager.StartAnnouncement(
                yakuList,
                resultText,
                header,
                footer, // VoiceManager内ではfooterは最終的にresultTextに追加される仕様だが、今回は分離したので空文字などを渡すか、あるいはVoiceManager側を改造する
                scoreName,
                () =>
                {
                    // --- コールバック: 役読み上げ完了後に実行される ---

                    // A. 点数テキストを表示
                    if (winScoreText != null)
                    {
                        winScoreText.gameObject.SetActive(true);

                        // ここで「バン！」というSEを鳴らす
                        if (sfxAudioSource != null && impactSE != null)
                        {
                            sfxAudioSource.PlayOneShot(impactSE);
                        }
                    }
                }
            );
        }
        else
        {
            // VoiceManagerがない場合は即座に全部表示
            string formattedYaku = yakuStr.Replace(" / ", "\n");
            resultText.text = $"<size=80%>{formattedYaku}</size>";
            if (winScoreText != null) winScoreText.gameObject.SetActive(true);
        }
    }

    // ★追加: リザルト用手牌表示メソッド
    // MahjongCanvas.cs
    // MahjongCanvas.cs

    // ★修正: ツモ牌を右側に配置するように変更
    private void ShowResultHand(MahjongPlayer winner)
    {
        if (resultHandContainer == null || winner == null) return;

        // 1. 前回の表示をクリア
        ClearContainer(resultHandContainer);

        // 2. 手牌リストの作成（ツモ牌は別途表示するため除外）
        List<int> handIds = new List<int>();
        foreach (var tile in winner.HandTiles)
        {
            if (tile != null && tile != winner.TsumoTile)
            {
                handIds.Add(tile.TileId);
            }
        }

        // 3. 理牌（ソート）
        handIds.Sort((a, b) =>
        {
            int wA = GetSortWeight(a);
            int wB = GetSortWeight(b);
            if (wA != wB) return wA.CompareTo(wB);
            return a.CompareTo(b);
        });

        // 4. 手牌の表示（まずは通常の牌を並べる）
        foreach (int id in handIds)
        {
            CreateTileImage(id, resultHandContainer, false);
        }

        // 5. ツモ牌（あがり牌）を右側に表示
        if (winner.TsumoTile != null)
        {
            // 手牌とツモ牌の間のスペーサー
            GameObject tsumoSpacer = new GameObject("Spacer_Tsumo");
            tsumoSpacer.transform.SetParent(resultHandContainer, false);
            LayoutElement le = tsumoSpacer.AddComponent<LayoutElement>();
            le.preferredWidth = 20f; // 手牌とあがり牌の間隔

            // ツモ牌を表示
            CreateTileImage(winner.TsumoTile.TileId, resultHandContainer, false);
        }

        // 6. 副露牌（暗槓など）の表示
        if (winner.MeldTiles.Count > 0)
        {
            // ツモ牌（または手牌）と副露の間のスペーサー
            GameObject meldSpacer = new GameObject("Spacer_Meld");
            meldSpacer.transform.SetParent(resultHandContainer, false);
            LayoutElement le = meldSpacer.AddComponent<LayoutElement>();
            le.preferredWidth = 20f; // あがり牌と鳴き牌の間隔

            // 暗槓表示（端を裏返す処理は維持）
            for (int i = 0; i < winner.MeldTiles.Count; i++)
            {
                if (winner.MeldTiles[i] == null) continue;
                int tileId = winner.MeldTiles[i].TileId;

                bool isFaceDown = (i % 4 == 0) || (i % 4 == 3);

                CreateTileImage(tileId, resultHandContainer, isFaceDown);
            }
        }
    }

    // ソート用の重み付け（赤ドラを正しい位置に挟む）
    private int GetSortWeight(int id)
    {
        if (id == 34) return 4;  // 赤マンズ5 -> マンズ5扱い
        if (id == 35) return 13; // 赤ピンズ5 -> ピンズ5扱い
        if (id == 36) return 22; // 赤ソーズ5 -> ソーズ5扱い
        return id;
    }
    // MahjongCanvas.cs

    // 第3引数に isFaceDown を追加
    private void CreateTileImage(int tileId, Transform container, bool isFaceDown = false)
    {
        if (container == null) return;

        Sprite spriteToShow = null;

        // isFaceDown が true なら、強制的に「裏面の画像」を使う
        if (isFaceDown)
        {
            spriteToShow = tileBackSprite;
        }
        // それ以外で有効なIDなら「表面の画像」を使う
        else if (tileSprites != null && tileId >= 0 && tileId < tileSprites.Length)
        {
            spriteToShow = tileSprites[tileId];
        }
        // IDが-1の場合も裏面
        else if (tileId == -1)
        {
            spriteToShow = tileBackSprite;
        }

        if (spriteToShow == null) return;

        GameObject imgObj = new GameObject("TileImage");
        imgObj.transform.SetParent(container, false);

        Image img = imgObj.AddComponent<Image>();
        img.sprite = spriteToShow;
        img.preserveAspect = true;

        // --- レイアウト調整（間隔修正用） ---
        LayoutElement layout = imgObj.AddComponent<LayoutElement>();
        layout.preferredWidth = 10;  // 幅を少し狭める
        layout.preferredHeight = 20; // 高さ
    }
    //   private void CreateTileImage(int tileId, Transform container, bool isFaceDown = false)
    //   {
    //       if (container == null) return;
    //       Sprite spriteToShow = null;

    //       // isFaceDown が true の場合は強制的に裏面を表示
    //       if (isFaceDown || tileId == -1) 
    //       {
    //           spriteToShow = tileBackSprite;
    //       }
    //       else if (tileSprites != null && tileId >= 0 && tileId < tileSprites.Length) 
    //       {
    //           spriteToShow = tileSprites[tileId];
    //       }

    //       if (spriteToShow == null) return;

    //       GameObject imgObj = new GameObject("TileImage");
    //       imgObj.transform.SetParent(container, false);

    //       Image img = imgObj.AddComponent<Image>();
    //       img.sprite = spriteToShow;
    //       img.preserveAspect = true;

    //       LayoutElement layout = imgObj.AddComponent<LayoutElement>();
    //       //layout.preferredWidth = 32;  // 前回の回答に基づき調整
    //       //layout.preferredHeight = 48;
    //   }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }

    private void UpdateScoreTexts()
    {
        if (scoreTexts == null) return;
        var players = MahjongGameManager.Instance.connectedPlayers;

        foreach (var player in players)
        {
            int seat = player.Seat;
            if (seat >= 0 && seat < scoreTexts.Length && scoreTexts[seat] != null)
            {
                string prefix = (player.IsHuman) ? "<color=yellow>You</color>" : $"P{seat}";
                scoreTexts[seat].text = $"{prefix}: {player.Score}";
            }
        }
    }

    private void SetButtonActive(Button btn, bool isActive)
    {
        if (btn != null && btn.gameObject.activeSelf != isActive)
        {
            btn.gameObject.SetActive(isActive);
        }
    }

    private void UpdateStatusText()
    {
        if (statusText != null && _localPlayer != null)
        {
            int shanten = _localPlayer.CurrentShanten;
            string msg = "";
            if (shanten <= -1) msg = "<color=red>Hola</color>";
            else if (shanten == 0) msg = "聴牌";
            else if (shanten == 1) msg = "1向聴";
            else msg = $"{shanten} 向聴";

            if (_localPlayer.IsRiichi) msg += " <color=yellow>[立直中]</color>";
            if (_localPlayer.IsRiichiPending) msg += " <color=orange>[Choosing tile to discard]</color>";
            if (_localPlayer.IsFuriten) msg += "<color=red>振聴</color>";

            statusText.text = msg;
        }
    }

    private void UpdateSortButton()
    {
        if (sortButton != null && _localPlayer != null)
        {
            var txt = sortButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = _localPlayer.IsAutoSortEnabled ? "自動理牌ON" : "自動理牌OFF";
        }
    }

    private void FindLocalPlayer()
    {
        // Managerが存在し、かつリストが初期化されているか確認
        if (MahjongGameManager.Instance != null && MahjongGameManager.Instance.connectedPlayers != null)
        {
            foreach (var player in MahjongGameManager.Instance.connectedPlayers)
            {
                // プレイヤー情報がnullでないか確認してからアクセス
                if (player != null && player.IsHuman)
                {
                    _localPlayer = player;
                    break;
                }
            }
        }
    }

    private void OnRiichiClicked()
    {
        // 1. ボタンを即座に消す
        HideAllActionButtons();

        // ★修正: ここでの音声再生を削除
        // PlayRandomVoice(riichiVoices); 

        // 2. ロジック実行
        if (_localPlayer != null) _localPlayer.SetRiichiPending(true);
    }

    // ★追加: 外部（MahjongPlayer）から立直音声を再生するためのパブリックメソッド
    public void PlayRiichiVoice()
    {
        PlayRandomVoice(riichiVoices);
    }

    private void OnKanClicked()
    {
        if (_localPlayer == null) return;

        var candidates = _localPlayer.AvailableAnkanTiles;
        if (candidates.Count == 0) return;

        _isSelectingAnkan = true;

        // 候補が1つの場合は即座に実行（既存の挙動）
        if (candidates.Count == 1)
        {
            HideAllActionButtons();
            PlayRandomVoice(kanVoices);
            _localPlayer.RequestAnkan(candidates[0]);
        }
        else
        {
            // ★追加: 候補が複数の場合は選択パネルを表示
            // アクションボタンは隠すが、選択パネルを出す
            HideAllActionButtons();
            ShowAnkanSelection(candidates);
        }
    }
    // MahjongCanvas.cs 内
    // MahjongCanvas.cs 内

    // 選択パネルを表示し、4枚並びのボタンを生成する

    // MahjongCanvas.cs 内
    // MahjongCanvas.cs 720行目付近

    private void ShowAnkanSelection(List<int> candidates)
    {
        if (ankanSelectPanel == null || ankanSelectGrid == null) return;

        ankanSelectPanel.SetActive(true);
        ankanSelectGrid.gameObject.SetActive(true);

        // --- 親グリッド(ankanSelectGrid)のレイアウト設定を強制リセット ---
        HorizontalLayoutGroup parentLayout = ankanSelectGrid.GetComponent<HorizontalLayoutGroup>();
        if (parentLayout == null) parentLayout = ankanSelectGrid.gameObject.AddComponent<HorizontalLayoutGroup>();

        parentLayout.spacing = 40;
        parentLayout.childAlignment = TextAnchor.MiddleCenter;
        parentLayout.childControlWidth = true;  // 子(ボタン)の幅を自動調整
        parentLayout.childControlHeight = true; // 子(ボタン)の高さを自動調整
        parentLayout.childForceExpandWidth = false;
        parentLayout.childForceExpandHeight = false;

        // 古いボタンの削除
        // ★修正: キャンセルボタン(ankanCancelButton)が既にGrid内にある場合、
        // それを削除してしまわないようにガードする
        foreach (Transform child in ankanSelectGrid)
        {
            if (ankanCancelButton != null && child == ankanCancelButton.transform)
            {
                continue; // キャンセルボタンは消さない
            }
            Destroy(child.gameObject);
        }

        // 牌1枚の基本サイズ
        float tileW = 56f;
        float tileH = 78f;
        float totalW = tileW * 4;

        foreach (int id in candidates)
        {
            // 1. 親ボタンの作成 (RectTransform, Button, Image を同時付与)
            GameObject btnObj = new GameObject($"AnkanBtn_{id}", typeof(RectTransform), typeof(Button), typeof(Image));
            btnObj.transform.SetParent(ankanSelectGrid, false);

            // ボタンの当たり判定サイズを決定する重要なコンポーネント
            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = totalW;
            le.preferredHeight = tileH;
            le.minWidth = totalW; // 最小サイズを保証

            // 背景設定（これが当たり判定の「面」になります）
            Image bg = btnObj.GetComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);
            bg.raycastTarget = true;

            // ボタン内部の牌並べ用レイアウト
            HorizontalLayoutGroup hlg = btnObj.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 0;
            hlg.childControlWidth = true;  // 中の画像サイズを制御
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // 2. 牌画像（4枚）の作成
            for (int i = 0; i < 4; i++)
            {
                GameObject tileImgObj = new GameObject($"TileImg_{i}", typeof(RectTransform), typeof(Image));
                tileImgObj.transform.SetParent(btnObj.transform, false);

                Image img = tileImgObj.GetComponent<Image>();
                if (id >= 0 && id < tileSprites.Length) img.sprite = tileSprites[id];
                img.preserveAspect = true;
                img.raycastTarget = false; // ★画像自体はクリックを吸わない
            }

            // 3. クリックイベント設定
            Button btn = btnObj.GetComponent<Button>();
            int targetId = id;
            btn.onClick.AddListener(() => OnAnkanCandidateSelected(targetId));
        }

        if (ankanCancelButton != null)
        {
            // Gridの最後尾に配置
            ankanCancelButton.transform.SetParent(ankanSelectGrid, false);
            ankanCancelButton.transform.SetAsLastSibling();
            ankanCancelButton.gameObject.SetActive(true);

            // ★追加：自動レイアウトによってサイズが壊されないように保護する
            LayoutElement cancelLe = ankanCancelButton.GetComponent<LayoutElement>();
            if (cancelLe == null) cancelLe = ankanCancelButton.gameObject.AddComponent<LayoutElement>();

            // インスペクターで設定している元のサイズ（例えば幅160x高さ60など）をここで維持させる
            // RectTransformの現在のサイズを優先的に使用する設定
            RectTransform cancelRect = ankanCancelButton.GetComponent<RectTransform>();
            cancelLe.preferredWidth = cancelRect.sizeDelta.x;
            cancelLe.preferredHeight = cancelRect.sizeDelta.y;
            cancelLe.minWidth = cancelRect.sizeDelta.x;
            cancelLe.minHeight = cancelRect.sizeDelta.y;
        }

        // ★重要: レイアウトの再計算を強制
        LayoutRebuilder.ForceRebuildLayoutImmediate(ankanSelectGrid as RectTransform);
    }
    private void OnAnkanCandidateSelected(int tileId)
    {
        _isSelectingAnkan = false;
        // パネルを閉じる
        if (ankanSelectPanel != null) ankanSelectPanel.SetActive(false);

        // 音声再生して実行
        PlayRandomVoice(kanVoices);
        if (_localPlayer != null)
        {
            _localPlayer.RequestAnkan(tileId);
        }
        // アクションボタンはRequestAnkan後の処理で適切に更新されるはずなので、ここでは再表示しない
    }

    // ★追加: 戻る（Skip）ボタンが押されたとき
    private void OnAnkanCancelClicked()
    {
        _isSelectingAnkan = false;
        // 1. 選択パネルを閉じる
        if (ankanSelectPanel != null) ankanSelectPanel.SetActive(false);

        // 2. 元のアクションボタン（Kan, Skip等）を復活させる
        UpdateActionButtons();
    }

    private void OnWinClicked() // ツモ
    {
        HideAllActionButtons();
        //PlayRandomVoice(tsumoVoices);
        if (_localPlayer != null) _localPlayer.RequestTsumo();
    }

    private void OnRonClicked() // ロン
    {
        HideAllActionButtons();
        PlayRandomVoice(ronVoices);
        if (_localPlayer != null) _localPlayer.RequestRon();
    }

    private void OnSkipClicked()
    {
        HideAllActionButtons();
        // スキップ時は音声なし、またはスキップ音があればここで再生
        if (_localPlayer != null)
        {
            if (_localPlayer.IsRiichiPending) _localPlayer.SetRiichiPending(false);
            else if (_localPlayer.IsRiichi) _localPlayer.SkipRiichiAnkan();
        }
    }

    // ★追加: ボタン一括非表示ヘルパー（コード重複を減らすため）
    private void HideAllActionButtons()
    {
        if (riichiButton != null) riichiButton.gameObject.SetActive(false);
        if (kanButton != null) kanButton.gameObject.SetActive(false);
        if (winButton != null) winButton.gameObject.SetActive(false);
        if (ronButton != null) ronButton.gameObject.SetActive(false);
        if (skipButton != null) skipButton.gameObject.SetActive(false);
    }

    // ★変更: リストからランダムに選んで再生するヘルパー
    private void PlayRandomVoice(List<AudioClip> clips)
    {
        if (sfxAudioSource != null && clips != null && clips.Count > 0)
        {
            // リストの中からランダムなインデックスを取得
            int index = Random.Range(0, clips.Count);
            AudioClip clip = clips[index];

            if (clip != null)
            {
                sfxAudioSource.PlayOneShot(clip);
            }
        }
    }


    private void OnSortClicked()
    {
        if (_localPlayer != null) _localPlayer.ToggleAutoSort();
    }


    private void OnDebugDealClicked()
    {
        if (MahjongGameManager.Instance != null)
        {
            MahjongGameManager.Instance.RequestDebugRestart();
        }
    }

    private void OnNextRoundClicked()
    {
        if (nextRoundButton != null) nextRoundButton.gameObject.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
        if (MahjongGameManager.Instance != null)
        {
            MahjongGameManager.Instance.ProceedToNextRound();
        }
    }

    private void OnRetryClicked()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (MahjongGameManager.Instance != null)
        {
            MahjongGameManager.Instance.StartNewGame();
        }
    }

    // MahjongCanvas.cs
    // MahjongCanvas.cs

    private void OnSendRankingClicked()
    {
        // 1. まずシーン内を探す
        var client = FindAnyObjectByType<unityroom.Api.UnityroomApiClient>();

        // 2. なければ生成し、そのコンポーネントを取得して client 変数に入れる
        if (client == null)
        {
            if (unityroomApiClientPrefab != null)
            {
                GameObject obj = Instantiate(unityroomApiClientPrefab);
                client = obj.GetComponent<unityroom.Api.UnityroomApiClient>();
                Debug.Log("<color=green>送信直前に UnityroomApiClient を緊急生成しました</color>");
            }
            else
            {
                Debug.LogError("【設定ミス】UnityroomApiClientのプレハブが設定されていません！");
                return;
            }
        }

        // --- ここからが修正ポイント ---
        // UnityroomApiClient.Instance ではなく、
        // 今ここで確保した client 変数を直接使って送信します。

        int yakumanCount = 0;
        if (MahjongGameManager.Instance != null)
        {
            yakumanCount = MahjongGameManager.Instance.GetUnlockedYakumanCount();
        }

        // ★修正: client.SendScore(...) に変更
        if (client != null)
        {
            client.SendScore(1, _lastFinalScore, ScoreboardWriteMode.Always);
            client.SendScore(2, yakumanCount, ScoreboardWriteMode.Always);
            Debug.Log($"Sent Scores -> Board 1: {_lastFinalScore}, Board 2 (Yakuman): {yakumanCount}");
        }

        if (sendRankingButton != null) sendRankingButton.interactable = false;
    }
    //    private void OnSendRankingClicked()
    //    {
    //        // ★修正: いきなり .Instance を呼ばず、まずは存在確認する
    //        // (Unity 2023以降は FindAnyObjectByType, それ以前は FindObjectOfType)
    //        var client = FindAnyObjectByType<unityroom.Api.UnityroomApiClient>();
    //
    //        // 存在しない場合の救済措置
    //        if (client == null)
    //        {
    //            if (unityroomApiClientPrefab != null)
    //            {
    //                Instantiate(unityroomApiClientPrefab);
    //                Debug.Log("<color=green>送信直前に UnityroomApiClient を緊急生成しました</color>");
    //            }
    //            else
    //            {
    //                Debug.LogError("【設定ミス】MahjongCanvas の Inspector に UnityroomApiClient のプレハブが設定されていません！ランキング送信を中止します。");
    //                return; // ここで処理を中断しないと次の行でエラーになる
    //            }
    //        }
    //
    //        // --- ここから既存の処理 ---
    //        int yakumanCount = 0;
    //        if (MahjongGameManager.Instance != null)
    //        {
    //            yakumanCount = MahjongGameManager.Instance.GetUnlockedYakumanCount();
    //        }
    //
    //        // 安全に Instance を呼ぶ
    //        UnityroomApiClient.Instance.SendScore(1, _lastFinalScore, ScoreboardWriteMode.HighScoreDesc);
    //        UnityroomApiClient.Instance.SendScore(2, yakumanCount, ScoreboardWriteMode.HighScoreDesc); 
    //        
    //        Debug.Log($"Sent Scores -> Board 1: {_lastFinalScore}, Board 2 (Yakuman): {yakumanCount}");
    //
    //        if (sendRankingButton != null) sendRankingButton.interactable = false;
    //    }

    private void OnFinishClicked()
    {
        Time.timeScale = 1f;

        Debug.Log("OnFinishClicked : Go to StartScene");

        SceneManager.LoadScene("StartScene");
    }

    // ★修正: プレハブを使わずコードで画像生成
    public void ShowUkeirePanel(List<int> effectiveTiles)
    {
        if (ukeirePanel == null || ukeireGrid == null)
        {
            Debug.LogWarning("UkeirePanel or Grid is not assigned in Inspector.");
            return;
        }

        // 1. 前回の表示内容をクリア
        foreach (Transform child in ukeireGrid)
        {
            Destroy(child.gameObject);
        }

        // 2. 有効牌がない場合は非表示にして終了
        if (effectiveTiles == null || effectiveTiles.Count == 0)
        {
            ukeirePanel.SetActive(false);
            return;
        }

        // 3. 牌の画像をコードで生成して並べる
        foreach (int tileId in effectiveTiles)
        {
            // 新しいゲームオブジェクトを作成
            GameObject tileObj = new GameObject($"Tile_{tileId}", typeof(RectTransform), typeof(Image));

            // Gridの子要素にする
            tileObj.transform.SetParent(ukeireGrid, false);

            // Imageコンポーネントを取得してスプライトを設定
            Image img = tileObj.GetComponent<Image>();
            if (tileId >= 0 && tileId < tileSprites.Length)
            {
                img.sprite = tileSprites[tileId];
            }
            else
            {
                img.color = Color.white; // スプライトがない場合は白四角
            }
        }

        // 4. パネルを表示
        ukeirePanel.SetActive(true);
    }

    public void HideUkeirePanel()
    {
        if (ukeirePanel != null) ukeirePanel.SetActive(false);
    }

    [Header("Yakuman UI")]
    [SerializeField] private GameObject yakumanConfirmPanel; // 役満確認用のパネル（ボタンを含む親）

    [Header("Yakuman UI")]
    [SerializeField] private GameObject yakumanRoot;       // 親オブジェクト（空のオブジェクト）
    [SerializeField] private GameObject yakumanPanelA;     // 上のパネル（クリックで消える用）
    [SerializeField] private GameObject yakumanPanelB;     // 下のパネル（ボタンがある用）

    // ゲーム開始時やリザルトを閉じる時に呼ぶリセット処理
    public void ResetYakumanUI()
    {
        if (yakumanRoot != null) yakumanRoot.SetActive(false);
        if (yakumanPanelA != null) yakumanPanelA.SetActive(true); // Aは初期表示
        if (yakumanPanelB != null) yakumanPanelB.SetActive(false); // Bは非表示
    }

    // GameManagerから呼ばれる：演出開始
    public void ShowYakumanConfirmUI()
    {
        if (yakumanRoot != null)
        {
            yakumanRoot.SetActive(true);
            yakumanPanelA.SetActive(true);
            yakumanPanelB.SetActive(false);
        }
    }

    // パネルAをクリックした時に呼ぶ（インスペクターからEventTriggerなどで設定）
    public void OnPanelAClicked()
    {
        yakumanPanelA.SetActive(false);
        yakumanPanelB.SetActive(true);
    }

    // パネルBの中のButtonを押した時に呼ぶ
    public void OnYakumanConfirmButtonClicked()
    {
        yakumanRoot.SetActive(false); // 全て閉じる

        // GameManagerのフラグを立てて、演出（動画）を再開させる
        MahjongGameManager.Instance.StartYakumanProduction();
    }
    [SerializeField] private GameObject blackPanel; // 演出用の黒いパネル

    public void OnShowBlackPanel()
    {
        StartCoroutine(WaitBlackPanel(1.5f)); // 1.5秒表示してから消す例
    }

    IEnumerator WaitBlackPanel(float duration)
    {
        if (blackPanel != null) blackPanel.SetActive(true);
        yield return new WaitForSeconds(duration);
        if (blackPanel != null) blackPanel.SetActive(false);
    }
}
