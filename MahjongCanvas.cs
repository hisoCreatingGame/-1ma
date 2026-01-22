using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement; // ★追加: シーン遷移に必要
using unityroom.Api; 

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

    [Header("Win Result UI")]
    public GameObject resultPanel; 
    public TMP_Text resultText;    
    public Button nextRoundButton; 

    [Header("Game Over UI")]
    public GameObject gameOverPanel; 
    public TMP_Text finalScoreText;  
    public TMP_Text totalCumulativeText; 
    public Button retryButton;       
    public Button sendRankingButton; 
    public Button finishButton; // ★追加: タイトルへ戻るボタン

    private MahjongPlayer _localPlayer;

    public Transform doraContainer;    
    public Transform uraDoraContainer; 
    public GameObject uraDoraLabel;    

    [Header("Tile Sprites")]
    public Sprite[] tileSprites; 
    public Sprite tileBackSprite;

    // スコア管理用キー
    private const string PREF_KEY_TOTAL_SCORE = "TotalAccumulatedScore";
    private int _lastFinalScore = 0;

    private void Start()
    {
        // アクションボタン
        if (riichiButton != null) riichiButton.onClick.AddListener(OnRiichiClicked);
        if (kanButton != null)    kanButton.onClick.AddListener(OnKanClicked);
        if (winButton != null)    winButton.onClick.AddListener(OnWinClicked);
        if (sortButton != null)   sortButton.onClick.AddListener(OnSortClicked);
        if (skipButton != null)   skipButton.onClick.AddListener(OnSkipClicked);
        if (debugDealButton != null)   debugDealButton.onClick.AddListener(OnDebugDealClicked);
        if (ronButton != null) ronButton.onClick.AddListener(OnRonClicked);

        // 結果画面ボタン
        if (nextRoundButton != null) nextRoundButton.onClick.AddListener(OnNextRoundClicked);

        // ゲームオーバー画面ボタン
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (sendRankingButton != null) sendRankingButton.onClick.AddListener(OnSendRankingClicked);
        // ★追加: Finishボタン（タイトルへ）
        if (finishButton != null) finishButton.onClick.AddListener(OnFinishClicked);

        // 初期非表示
        if (riichiButton != null) riichiButton.gameObject.SetActive(false);
        if (kanButton != null)    kanButton.gameObject.SetActive(false);
        if (winButton != null)    winButton.gameObject.SetActive(false);
        if (skipButton != null)   skipButton.gameObject.SetActive(false);
        if (ronButton != null) ronButton.gameObject.SetActive(false);

        if (resultPanel != null) resultPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    private void Update()
    {
        if (_localPlayer == null) FindLocalPlayer();

        if (_localPlayer != null)
        {
            UpdateActionButtons();
            UpdateStatusText();
            UpdateSortButton();
            UpdateScoreTexts();
        }
    }

    private void UpdateActionButtons()
    {
        // 自分のターンかどうか
        bool isMyTurn = (MahjongGameManager.Instance != null && MahjongGameManager.Instance.CurrentTurnSeat == _localPlayer.Seat);
        // ゲーム中のみボタンを表示
        bool isGameActive = MahjongGameManager.Instance.IsGameStarted;

        if (!isGameActive)
        {
            SetButtonActive(riichiButton, false);
            SetButtonActive(kanButton, false);
            SetButtonActive(winButton, false);
            SetButtonActive(ronButton, false);
            SetButtonActive(skipButton, false);
            return;
        }

        bool canRiichi = isMyTurn 
                         && !_localPlayer.IsRiichi 
                         && !_localPlayer.IsRiichiPending 
                         && (_localPlayer.CurrentShanten <= 0);

        bool canKan = isMyTurn && _localPlayer.AvailableAnkanTiles.Count > 0 && !_localPlayer.IsRiichiPending && !_localPlayer.IsRiichi;
        bool canWin = isMyTurn && (_localPlayer.CurrentShanten == -1);
        
        bool isRiichiAnkanChance = _localPlayer.IsRiichi && _localPlayer.AvailableAnkanTiles.Count > 0;
        bool canSkip = canRiichi || canKan || canWin || _localPlayer.IsRiichiPending || isRiichiAnkanChance;
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

    public void ShowWinResult(int seat, int score, string yakuStr, int[] doraInds, int[] uraInds)
    {
        if (resultText == null || resultPanel == null) return;

        string playerName = (seat == 0) ? "You" : $"Player {seat}";
        
        string titleColor = "<color=yellow>";
        string title = $"{titleColor}{playerName} WIN!</color>";

        string formattedYaku = yakuStr.Replace("/", "\n");
        int totalScore = MahjongGameManager.Instance.CurrentScore;
        
        resultText.text = $"{title}\n\n<size=80%>{formattedYaku}</size>\n\n" +
                          $"<size=120%><color=orange>+{score} Points</color></size>\n" +
                          $"<size=100%>Total: {totalScore}</size>";
        
        ClearContainer(doraContainer);
        ClearContainer(uraDoraContainer);

        if (doraInds != null)
        {
            foreach(int tileId in doraInds) CreateTileImage(tileId, doraContainer);
        }

        bool hasUra = false;
        if (uraInds != null)
        {
            foreach (int id in uraInds)
            {
                CreateTileImage (id, uraDoraContainer);
                hasUra |= (id != -1);
            }
        }

        uraDoraLabel.SetActive(hasUra);
        
        if (nextRoundButton != null) nextRoundButton.gameObject.SetActive(true);

        resultPanel.SetActive(true);
    }

    public void ShowGameOver(int finalScore)
    {
        if (gameOverPanel == null) return;

        _lastFinalScore = finalScore;

        int oldTotal = PlayerPrefs.GetInt(PREF_KEY_TOTAL_SCORE, 0);
        int newTotal = oldTotal + finalScore; 
        PlayerPrefs.SetInt(PREF_KEY_TOTAL_SCORE, newTotal);
        PlayerPrefs.Save();

        if (finalScoreText != null) 
            finalScoreText.text = $"Game Over\nFinal Score: <color=yellow>{finalScore}</color>";
        
        if (totalCumulativeText != null)
            totalCumulativeText.text = $"Lifetime Total Score: {newTotal}";

        gameOverPanel.SetActive(true);
        if (resultPanel != null) resultPanel.SetActive(false); 
    }
    
    private void CreateTileImage(int tileId, Transform container)
    {
        if (container == null) return;
        Sprite spriteToShow = null;

        if (tileId == -1) spriteToShow = tileBackSprite;
        else if (tileSprites != null && tileId >= 0 && tileId < tileSprites.Length) spriteToShow = tileSprites[tileId];

        if (spriteToShow == null) return;

        GameObject imgObj = new GameObject("TileImage");
        imgObj.transform.SetParent(container, false);

        Image img = imgObj.AddComponent<Image>();
        img.sprite = spriteToShow;
        img.preserveAspect = true;

        LayoutElement layout = imgObj.AddComponent<LayoutElement>();
        layout.preferredWidth = 40;  
        layout.preferredHeight = 56;
    }

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
            else if (shanten == 0) msg = "TEMPAI";
            else if (shanten == 1) msg = "1 SHANTEN";
            else msg = $"{shanten} SHANTEN";

            if (_localPlayer.IsRiichi) msg += " <color=yellow>[Riichi now]</color>";
            if (_localPlayer.IsRiichiPending) msg += " <color=orange>[Choosing tile to discard]</color>";
            if (_localPlayer.IsFuriten) msg += "<color=red>Furiten</color>";

            statusText.text = msg;
        }
    }

    private void UpdateSortButton()
    {
        if (sortButton != null && _localPlayer != null)
        {
            var txt = sortButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = _localPlayer.IsAutoSortEnabled ? "Sort: ON" : "Sort: OFF";
        }
    }

    private void FindLocalPlayer()
    {
        if (MahjongGameManager.Instance != null)
        {
            foreach (var player in MahjongGameManager.Instance.connectedPlayers)
            {
                if (player.IsHuman)
                {
                    _localPlayer = player;
                    break;
                }
            }
        }
    }

    private void OnRiichiClicked()
    {
        if (_localPlayer != null) _localPlayer.SetRiichiPending(true);
    }

    private void OnKanClicked()
    {
        if (_localPlayer != null) _localPlayer.RequestAnkan();
    }

    private void OnWinClicked()
    {
        if (_localPlayer != null) _localPlayer.RequestTsumo();
    }

    private void OnSortClicked()
    {
        if (_localPlayer != null) _localPlayer.ToggleAutoSort();
    }

    private void OnSkipClicked()
    {
        if (_localPlayer != null)
        {
            if (_localPlayer.IsRiichiPending)
            {
                _localPlayer.SetRiichiPending(false);
            }
            else if (_localPlayer.IsRiichi)
            {
                _localPlayer.SkipRiichiAnkan();
            }
        }
    }
    
    private void OnDebugDealClicked()
    {
        if (MahjongGameManager.Instance != null)
        {
            MahjongGameManager.Instance.RequestDebugRestart();
        }
    }
    
    private void OnRonClicked()
    {
        if (_localPlayer != null) _localPlayer.RequestRon();
    }

    private void OnNextRoundClicked()
    {
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

    private void OnSendRankingClicked()
    {
        int totalAccumulated = PlayerPrefs.GetInt(PREF_KEY_TOTAL_SCORE, 0);

        UnityroomApiClient.Instance.SendScore(1, _lastFinalScore, ScoreboardWriteMode.HighScoreDesc);
        UnityroomApiClient.Instance.SendScore(2, totalAccumulated, ScoreboardWriteMode.HighScoreDesc);
        
        Debug.Log($"Sent Scores -> Board 1: {_lastFinalScore}, Board 2: {totalAccumulated}");

        if (sendRankingButton != null) sendRankingButton.interactable = false;
    }

    // ★追加: FinishボタンでStartSceneへ
    private void OnFinishClicked()
    {
        // "StartScene" という名前のシーンをロードします
        SceneManager.LoadScene("StartScene");
    }
}