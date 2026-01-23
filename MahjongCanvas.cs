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
    public Button finishButton; 

    private MahjongPlayer _localPlayer;

    public Transform doraContainer;    
    public Transform uraDoraContainer; 
    public GameObject uraDoraLabel;    

    [Header("Tile Sprites")]
    public Sprite[] tileSprites; 
    public Sprite tileBackSprite;

    private int _lastFinalScore = 0;
    
    [Header("Assist UI")]
    public GameObject ukeirePanel;      // 表示用の親パネル
    public Transform ukeireGrid;        // 牌画像を並べる親オブジェクト

    private void Start()
    {
        if (riichiButton != null) riichiButton.onClick.AddListener(OnRiichiClicked);
        if (kanButton != null)    kanButton.onClick.AddListener(OnKanClicked);
        if (winButton != null)    winButton.onClick.AddListener(OnWinClicked);
        if (sortButton != null)   sortButton.onClick.AddListener(OnSortClicked);
        if (skipButton != null)   skipButton.onClick.AddListener(OnSkipClicked);
        if (debugDealButton != null)   debugDealButton.onClick.AddListener(OnDebugDealClicked);
        if (ronButton != null) ronButton.onClick.AddListener(OnRonClicked);

        if (nextRoundButton != null) nextRoundButton.onClick.AddListener(OnNextRoundClicked);

        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (sendRankingButton != null) sendRankingButton.onClick.AddListener(OnSendRankingClicked);
        if (finishButton != null) finishButton.onClick.AddListener(OnFinishClicked);

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
        bool isMyTurn = (MahjongGameManager.Instance != null && MahjongGameManager.Instance.CurrentTurnSeat == _localPlayer.Seat);
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

    public void ShowRyuukyoku(bool isTenpai, int currentScore)
    {
        if (resultText == null || resultPanel == null) return;

        string title = isTenpai ? "<color=green>RYUUKYOKU (Tenpai)</color>" : "<color=red>RYUUKYOKU (No-ten)</color>";
        string info = isTenpai ? "Safe! You can proceed to next round." : "Game Over...";

        resultText.text = $"{title}\n\n{info}\n\n" +
                          $"<size=100%>Total Score: {currentScore}</size>";

        ClearContainer(doraContainer);
        ClearContainer(uraDoraContainer);
        if (uraDoraLabel != null) uraDoraLabel.SetActive(false);

        if (nextRoundButton != null) nextRoundButton.gameObject.SetActive(isTenpai);

        resultPanel.SetActive(true);
    }

    public void ShowGameOver(int finalScore)
    {
        if (gameOverPanel == null) return;

        _lastFinalScore = finalScore;

        if (finalScoreText != null) 
            finalScoreText.text = $"Game Over\nFinal Score: <color=yellow>{finalScore}</color>";
        
        if (totalCumulativeText != null && MahjongGameManager.Instance != null)
        {
            int yakumanCount = MahjongGameManager.Instance.GetUnlockedYakumanCount();
            totalCumulativeText.text = $"Yakuman Collection: <color=orange>{yakumanCount} / 15</color>";
        }

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
        int yakumanCount = 0;
        if (MahjongGameManager.Instance != null)
        {
            yakumanCount = MahjongGameManager.Instance.GetUnlockedYakumanCount();
        }

        UnityroomApiClient.Instance.SendScore(1, _lastFinalScore, ScoreboardWriteMode.HighScoreDesc);
        UnityroomApiClient.Instance.SendScore(2, yakumanCount, ScoreboardWriteMode.HighScoreDesc); 
        
        Debug.Log($"Sent Scores -> Board 1: {_lastFinalScore}, Board 2 (Yakuman): {yakumanCount}");

        if (sendRankingButton != null) sendRankingButton.interactable = false;
    }

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
}