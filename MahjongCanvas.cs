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
    public TMP_Text resultText;    // �𖼂Ȃǂ̕\���p

    // ���ǉ�: �_���E�����N�i�{���Ȃǁj�\���p�̃e�L�X�g
    // (Inspector��ResultPanel���ɐV����TextMeshPro���쐬���ăA�T�C�����Ă�������)
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
    public GameObject ukeirePanel;      // �\���p�̐e�p�l��
    public Transform ukeireGrid;        // �v�摜����ׂ�e�I�u�W�F�N�g


    [Header("Modules")]
    public YakuVoiceManager voiceManager;
    [Header("Result Hand UI")]
    // ���ǉ�: ���U���g�p�l�����Ɏ�v����ׂ邽�߂̐e�I�u�W�F�N�g
    // (ResultPanel�̒��ɋ��GameObject�����AHorizontal Layout Group��ݒ肵�Ă����ɃA�T�C�����Ă�������)
    public Transform resultHandContainer;


    [Header("Unityroom (Rescue)")]
    [SerializeField] private GameObject unityroomApiClientPrefab; // ���ǉ�: �v���n�u�������ɂ��Z�b�g����

    [Header("Voice Settings")]
    public AudioSource sfxAudioSource; // �C���X�y�N�^�[��AudioSource���A�T�C��
    public List<AudioClip> riichiVoices;      // �u���[�`�v
    public List<AudioClip> tsumoVoices;       // �u�c���v
    public List<AudioClip> kanVoices;         // �u�J���v
    public List<AudioClip> ronVoices;         // �u�����v�i���łɒǉ����Ă����ƕ֗��ł��j

    // ���ǉ�: �Ռ����iSE�j�p�̕ϐ�
    [Header("SE Settings")]
    public AudioClip impactSE; // �����Ɂu�h�[���I�v�Ȃǂ̌��ʉ����Z�b�g

    [Header("Round Start UI")]
    public GameObject roundStartPanel; // �C���X�y�N�^�[�Ńp�l�������蓖��
    public TMP_Text roundNameText;     // "��1��" �Ȃǂ�\��
    public TMP_Text honbaText;         // "0�{��" �Ȃǂ�\��

    [Header("Ankan Select UI")]
    // ���ǉ�: �I���p�l���̐e�I�u�W�F�N�g (Canvas���Panel���쐬���ăA�T�C�����Ă�������)
    public GameObject ankanSelectPanel;
    // ���ǉ�: �{�^������ׂ�O���b�h (Panel�̉���Horizontal Layout Group������I�u�W�F�N�g�����A�A�T�C�����Ă�������)
    public Transform ankanSelectGrid;
    // ���ǉ�: �L�����Z���{�^���i�K�v�ł����Panel���ɔz�u���ăA�T�C���j
    public Button ankanCancelButton;
    // ���ǉ�: �ǊJ�n�A�j���[�V�������Đ�����R���[�`��
    private bool _isSelectingAnkan = false;

    [Header("Gimmick UI")]
    public GameObject gimmickPanel; // ���ǉ�: �M�~�b�N�\���p�̐e�p�l��
    public TMP_Text gimmickText;    // ���ǉ�: �u��O�� �m��I�v�Ȃǂ��o���e�L�X�g

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


    // ���ǉ�: �M�~�b�N���o�̊J�n
    public void PlayGimmickAnnouncement(string yakuName)
    {
        StartCoroutine(GimmickAnnouncementRoutine(yakuName));
    }

    // ���ǉ�: �����̒����ɍ��킹�ăe�L�X�g��\������R���[�`��
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

        if (sfxAudioSource != null && voice != null)
        {
            sfxAudioSource.PlayOneShot(voice);
        }

        if (gimmickPanel != null && gimmickText != null)
        {
            gimmickText.text = displayText;
            gimmickPanel.SetActive(true);
            float waitTime = (voice != null) ? voice.length : 2.0f;
            yield return new WaitForSeconds(waitTime);
            gimmickPanel.SetActive(false);
        }
        else
        {
            yield return null;
        }
    }
    public IEnumerator PlayRoundStartAnimation(int roundCount, int honbaCount)
    {
        if (roundStartPanel != null)
        {
            if (roundNameText != null)
            {
                string wind = "東";
                int number = 1;
                roundNameText.text = $"{wind}{number}局";
                if (roundCountText != null) roundCountText.text = $"{wind}{number}局";
            }

            if (honbaText != null)
            {
                honbaText.text = $"{honbaCount}本場";
                if (roundCountText != null) roundCountText.text += $"\n{honbaCount}本場";
            }

            roundStartPanel.SetActive(true);
            yield return new WaitForSeconds(2.0f);
            roundStartPanel.SetActive(false);
        }
        else
        {
            yield return null;
        }
    }

    // ��3������ isFaceDown ��ǉ�
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
                Debug.Log("<color=green>MahjongCanvas: UnityroomApiClient���ً}�������܂���</color>");
            }
            else
            {
                Debug.LogWarning("MahjongCanvas: UnityroomApiClient�̃v���n�u���Z�b�g����Ă��܂���");
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
            UpdateScoreTexts();
            UpdateStatusText();
            UpdateSortButton();
            UpdateActionButtons();

            if (remainingTilesText != null && MahjongGameManager.Instance != null)
            {
                remainingTilesText.text = $"残り牌: {MahjongGameManager.Instance.TilesRemainingInWall}";
            }
        }
    }

    private void UpdateActionButtons()
    {
        if (_localPlayer == null)
        {
            HideAllActionButtons();
            return;
        }

        bool canRiichi = !_localPlayer.IsRiichi && !_localPlayer.IsRiichiPending && _localPlayer.TsumoTile != null && _localPlayer.CurrentShanten <= 0;
        bool canKan = _localPlayer.AvailableAnkanTiles != null && _localPlayer.AvailableAnkanTiles.Count > 0;
        bool canWin = _localPlayer.TsumoTile != null && _localPlayer.CurrentShanten <= -1;
        bool canRon = _localPlayer.CanRon;
        bool canSkip = _localPlayer.IsRiichiPending || canRon || (_localPlayer.IsRiichi && canKan);

        if (_isSelectingAnkan)
        {
            SetButtonActive(riichiButton, false);
            SetButtonActive(kanButton, false);
            SetButtonActive(winButton, false);
            SetButtonActive(ronButton, false);
            SetButtonActive(skipButton, false);
            return;
        }

        SetButtonActive(riichiButton, canRiichi);
        SetButtonActive(kanButton, canKan);
        SetButtonActive(winButton, canWin);
        SetButtonActive(ronButton, canRon);
        SetButtonActive(skipButton, canSkip);
        if (canRon) SetButtonActive(skipButton, true);
    }

    public void ShowRyuukyoku(bool isTenpai, int currentScore)
    {
        if (resultText == null || resultPanel == null) return;

        string title = isTenpai ? "<color=green>流局（テンパイ）</color>" : "<color=red>流局（ノーテン）</color>";
        string info = isTenpai ? "次局へ進めます。" : "ゲームオーバー...";

        resultText.text = $"{title}\n\n{info}\n\n<size=100%>総得点: {currentScore}</size>";
        ClearContainer(doraContainer);
        ClearContainer(uraDoraContainer);
        if (nextRoundButton != null) nextRoundButton.gameObject.SetActive(isTenpai);
        resultPanel.SetActive(true);
    }

    public void ShowGameOver(int finalScore)
    {
        if (gameOverPanel == null) return;

        var bgm = BgmController.GetOrFindInstance();
        if (bgm != null)
        {
            bgm.PlayEndKyokuBgm();
        }

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

        if (finalScoreText != null)
        {
            string msg = $"ゲームオーバー\n最終スコア: <color=yellow>{finalScore}</color>";

            if (isNewRecord)
            {
                msg += "\n<size=80%><color=red>ハイスコア更新！</color></size>";
            }
            else
            {
                msg += $"\n<size=70%>(ベスト: {highScore})</size>";
            }

            finalScoreText.text = msg;
        }

        if (totalCumulativeText != null && MahjongGameManager.Instance != null)
        {
            int yakumanCount = MahjongGameManager.Instance.GetUnlockedYakumanCount();
            totalCumulativeText.text = $"役満コレクション: <color=orange>{yakumanCount} / 15</color>";
        }

        gameOverPanel.SetActive(true);
        if (resultPanel != null) resultPanel.SetActive(false);
    }

    public void ShowWinResult(MahjongPlayer winner, int seat, int score, string yakuStr, int[] doraInds, int[] uraInds, int han, int fu, string scoreName)
    {
        if (resultText == null || resultPanel == null) return;

        string winnerLabel = (winner != null && winner.IsHuman) ? "あなた" : $"プレイヤー{seat}";
        string scoreNameLine = string.IsNullOrEmpty(scoreName) ? "" : $"<size=120%>{scoreName}</size>\n";

        string hanFuLine = "";
        if (han > 0 && fu > 0) hanFuLine = $"{han}翻 {fu}符\n";
        else if (han > 0) hanFuLine = $"{han}翻\n";

        resultText.text = $"<color=yellow>{winnerLabel}の和了！</color>\n{scoreNameLine}{hanFuLine}獲得: +{score}\n\n{yakuStr}";

        if (winScoreText != null)
        {
            int totalScore = (MahjongGameManager.Instance != null) ? MahjongGameManager.Instance.CurrentScore : 0;
            winScoreText.text = $"合計スコア: <color=yellow>{totalScore}</color>";
        }

        ClearContainer(doraContainer);
        if (doraInds != null)
        {
            foreach (int id in doraInds) CreateTileImage(id, doraContainer);
        }

        ClearContainer(uraDoraContainer);
        if (uraInds != null)
        {
            foreach (int id in uraInds) CreateTileImage(id, uraDoraContainer);
        }

        if (nextRoundButton != null) nextRoundButton.gameObject.SetActive(true);
        resultPanel.SetActive(true);
    }

    private void CreateTileImage(int tileId, Transform container, bool isFaceDown = false)
    {
        if (container == null) return;

        Sprite spriteToShow = null;

        // isFaceDown �� true �Ȃ�A�����I�Ɂu���ʂ̉摜�v���g��
        if (isFaceDown)
        {
            spriteToShow = tileBackSprite;
        }
        // ����ȊO�ŗL����ID�Ȃ�u�\�ʂ̉摜�v���g��
        else if (tileSprites != null && tileId >= 0 && tileId < tileSprites.Length)
        {
            spriteToShow = tileSprites[tileId];
        }
        // ID��-1�̏ꍇ������
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

        // --- ���C�A�E�g�����i�Ԋu�C���p�j ---
        LayoutElement layout = imgObj.AddComponent<LayoutElement>();
        layout.preferredWidth = 10;  // �����������߂�
        layout.preferredHeight = 20; // ����
    }
    //   private void CreateTileImage(int tileId, Transform container, bool isFaceDown = false)
    //   {
    //       if (container == null) return;
    //       Sprite spriteToShow = null;

    //       // isFaceDown �� true �̏ꍇ�͋����I�ɗ��ʂ�\��
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
    //       //layout.preferredWidth = 32;  // �O��̉񓚂Ɋ�Â�����
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
                string prefix = (player.IsHuman) ? "<color=yellow>あなた</color>" : $"プレイヤー{seat}";
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
            if (shanten <= -1) msg = "<color=red>アガリ</color>";
            else if (shanten == 0) msg = "聴牌";
            else if (shanten == 1) msg = "1向聴";
            else msg = $"{shanten}向聴";

            if (_localPlayer.IsRiichi) msg += " <color=yellow>[立直中]</color>";
            if (_localPlayer.IsRiichiPending) msg += " <color=orange>[捨て牌を選択]</color>";
            if (_localPlayer.IsFuriten) msg += " <color=red>[振聴]</color>";

            statusText.text = msg;
        }
    }

    private void UpdateSortButton()
    {
        if (sortButton != null && _localPlayer != null)
        {
            var txt = sortButton.GetComponentInChildren<TMP_Text>();
            if (txt != null) txt.text = _localPlayer.IsAutoSortEnabled ? "�������vON" : "�������vOFF";
        }
    }

    private void FindLocalPlayer()
    {
        // Manager�����݂��A�����X�g������������Ă��邩�m�F
        if (MahjongGameManager.Instance != null && MahjongGameManager.Instance.connectedPlayers != null)
        {
            foreach (var player in MahjongGameManager.Instance.connectedPlayers)
            {
                // �v���C���[���null�łȂ����m�F���Ă���A�N�Z�X
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
        // 1. �{�^���𑦍��ɏ���
        HideAllActionButtons();

        // ���C��: �����ł̉����Đ����폜
        // PlayRandomVoice(riichiVoices); 

        // 2. ���W�b�N���s
        if (_localPlayer != null) _localPlayer.SetRiichiPending(true);
    }

    // ���ǉ�: �O���iMahjongPlayer�j���痧���������Đ����邽�߂̃p�u���b�N���\�b�h
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

        // ��₪1�̏ꍇ�͑����Ɏ��s�i�����̋����j
        if (candidates.Count == 1)
        {
            HideAllActionButtons();
            PlayRandomVoice(kanVoices);
            _localPlayer.RequestAnkan(candidates[0]);
        }
        else
        {
            // ���ǉ�: ��₪�����̏ꍇ�͑I���p�l����\��
            // �A�N�V�����{�^���͉B�����A�I���p�l�����o��
            HideAllActionButtons();
            ShowAnkanSelection(candidates);
        }
    }
    // MahjongCanvas.cs ��
    // MahjongCanvas.cs ��

    // �I���p�l����\�����A4�����т̃{�^���𐶐�����

    // MahjongCanvas.cs ��
    // MahjongCanvas.cs 720�s�ڕt��

    private void ShowAnkanSelection(List<int> candidates)
    {
        if (ankanSelectPanel == null || ankanSelectGrid == null) return;

        ankanSelectPanel.SetActive(true);
        ankanSelectGrid.gameObject.SetActive(true);

        // --- �e�O���b�h(ankanSelectGrid)�̃��C�A�E�g�ݒ���������Z�b�g ---
        HorizontalLayoutGroup parentLayout = ankanSelectGrid.GetComponent<HorizontalLayoutGroup>();
        if (parentLayout == null) parentLayout = ankanSelectGrid.gameObject.AddComponent<HorizontalLayoutGroup>();

        parentLayout.spacing = 40;
        parentLayout.childAlignment = TextAnchor.MiddleCenter;
        parentLayout.childControlWidth = true;  // �q(�{�^��)�̕�����������
        parentLayout.childControlHeight = true; // �q(�{�^��)�̍�������������
        parentLayout.childForceExpandWidth = false;
        parentLayout.childForceExpandHeight = false;

        // �Â��{�^���̍폜
        // ���C��: �L�����Z���{�^��(ankanCancelButton)������Grid���ɂ���ꍇ�A
        // ������폜���Ă��܂�Ȃ��悤�ɃK�[�h����
        foreach (Transform child in ankanSelectGrid)
        {
            if (ankanCancelButton != null && child == ankanCancelButton.transform)
            {
                continue; // �L�����Z���{�^���͏����Ȃ�
            }
            Destroy(child.gameObject);
        }

        // �v1���̊�{�T�C�Y
        float tileW = 56f;
        float tileH = 78f;
        float totalW = tileW * 4;

        foreach (int id in candidates)
        {
            // 1. �e�{�^���̍쐬 (RectTransform, Button, Image �𓯎��t�^)
            GameObject btnObj = new GameObject($"AnkanBtn_{id}", typeof(RectTransform), typeof(Button), typeof(Image));
            btnObj.transform.SetParent(ankanSelectGrid, false);

            // �{�^���̓����蔻��T�C�Y�����肷��d�v�ȃR���|�[�l���g
            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.preferredWidth = totalW;
            le.preferredHeight = tileH;
            le.minWidth = totalW; // �ŏ��T�C�Y��ۏ�

            // �w�i�ݒ�i���ꂪ�����蔻��́u�ʁv�ɂȂ�܂��j
            Image bg = btnObj.GetComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.6f);
            bg.raycastTarget = true;

            // �{�^�������̔v���חp���C�A�E�g
            HorizontalLayoutGroup hlg = btnObj.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.spacing = 0;
            hlg.childControlWidth = true;  // ���̉摜�T�C�Y�𐧌�
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // 2. �v�摜�i4���j�̍쐬
            for (int i = 0; i < 4; i++)
            {
                GameObject tileImgObj = new GameObject($"TileImg_{i}", typeof(RectTransform), typeof(Image));
                tileImgObj.transform.SetParent(btnObj.transform, false);

                Image img = tileImgObj.GetComponent<Image>();
                if (id >= 0 && id < tileSprites.Length) img.sprite = tileSprites[id];
                img.preserveAspect = true;
                img.raycastTarget = false; // ���摜���̂̓N���b�N���z��Ȃ�
            }

            // 3. �N���b�N�C�x���g�ݒ�
            Button btn = btnObj.GetComponent<Button>();
            int targetId = id;
            btn.onClick.AddListener(() => OnAnkanCandidateSelected(targetId));
        }

        if (ankanCancelButton != null)
        {
            // Grid�̍Ō���ɔz�u
            ankanCancelButton.transform.SetParent(ankanSelectGrid, false);
            ankanCancelButton.transform.SetAsLastSibling();
            ankanCancelButton.gameObject.SetActive(true);

            // ���ǉ��F�������C�A�E�g�ɂ���ăT�C�Y���󂳂�Ȃ��悤�ɕی삷��
            LayoutElement cancelLe = ankanCancelButton.GetComponent<LayoutElement>();
            if (cancelLe == null) cancelLe = ankanCancelButton.gameObject.AddComponent<LayoutElement>();

            // �C���X�y�N�^�[�Őݒ肵�Ă��錳�̃T�C�Y�i�Ⴆ�Ε�160x����60�Ȃǁj�������ňێ�������
            // RectTransform�̌��݂̃T�C�Y��D��I�Ɏg�p����ݒ�
            RectTransform cancelRect = ankanCancelButton.GetComponent<RectTransform>();
            cancelLe.preferredWidth = cancelRect.sizeDelta.x;
            cancelLe.preferredHeight = cancelRect.sizeDelta.y;
            cancelLe.minWidth = cancelRect.sizeDelta.x;
            cancelLe.minHeight = cancelRect.sizeDelta.y;
        }

        // ���d�v: ���C�A�E�g�̍Čv�Z������
        LayoutRebuilder.ForceRebuildLayoutImmediate(ankanSelectGrid as RectTransform);
    }
    private void OnAnkanCandidateSelected(int tileId)
    {
        _isSelectingAnkan = false;
        // �p�l�������
        if (ankanSelectPanel != null) ankanSelectPanel.SetActive(false);

        // �����Đ����Ď��s
        PlayRandomVoice(kanVoices);
        if (_localPlayer != null)
        {
            _localPlayer.RequestAnkan(tileId);
        }
        // �A�N�V�����{�^����RequestAnkan��̏����œK�؂ɍX�V�����͂��Ȃ̂ŁA�����ł͍ĕ\�����Ȃ�
    }

    // ���ǉ�: �߂�iSkip�j�{�^���������ꂽ�Ƃ�
    private void OnAnkanCancelClicked()
    {
        _isSelectingAnkan = false;
        // 1. �I���p�l�������
        if (ankanSelectPanel != null) ankanSelectPanel.SetActive(false);

        // 2. ���̃A�N�V�����{�^���iKan, Skip���j�𕜊�������
        UpdateActionButtons();
    }

    private void OnWinClicked() // �c��
    {
        HideAllActionButtons();
        //PlayRandomVoice(tsumoVoices);
        if (_localPlayer != null) _localPlayer.RequestTsumo();
    }

    private void OnRonClicked() // ����
    {
        HideAllActionButtons();
        PlayRandomVoice(ronVoices);
        if (_localPlayer != null) _localPlayer.RequestRon();
    }

    private void OnSkipClicked()
    {
        HideAllActionButtons();
        // �X�L�b�v���͉����Ȃ��A�܂��̓X�L�b�v��������΂����ōĐ�
        if (_localPlayer != null)
        {
            if (_localPlayer.IsRiichiPending) _localPlayer.SetRiichiPending(false);
            else if (_localPlayer.IsRiichi) _localPlayer.SkipRiichiAnkan();
        }
    }

    // ���ǉ�: �{�^���ꊇ��\���w���p�[�i�R�[�h�d�������炷���߁j
    private void HideAllActionButtons()
    {
        if (riichiButton != null) riichiButton.gameObject.SetActive(false);
        if (kanButton != null) kanButton.gameObject.SetActive(false);
        if (winButton != null) winButton.gameObject.SetActive(false);
        if (ronButton != null) ronButton.gameObject.SetActive(false);
        if (skipButton != null) skipButton.gameObject.SetActive(false);
    }

    // ���ύX: ���X�g���烉���_���ɑI��ōĐ�����w���p�[
    private void PlayRandomVoice(List<AudioClip> clips)
    {
        if (sfxAudioSource != null && clips != null && clips.Count > 0)
        {
            // ���X�g�̒����烉���_���ȃC���f�b�N�X���擾
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
        var client = FindAnyObjectByType<unityroom.Api.UnityroomApiClient>();
        if (client == null)
        {
            if (unityroomApiClientPrefab != null)
            {
                GameObject obj = Instantiate(unityroomApiClientPrefab);
                client = obj.GetComponent<unityroom.Api.UnityroomApiClient>();
                Debug.Log("<color=green>送信前に UnityroomApiClient を生成しました</color>");
            }
            else
            {
                Debug.LogError("UnityroomApiClient のプレハブが未設定です");
                return;
            }
        }

        int yakumanCount = 0;
        if (MahjongGameManager.Instance != null)
        {
            yakumanCount = MahjongGameManager.Instance.GetUnlockedYakumanCount();
        }

        if (client != null)
        {
            client.SendScore(1, _lastFinalScore, ScoreboardWriteMode.HighScoreDesc);
            client.SendScore(2, yakumanCount, ScoreboardWriteMode.HighScoreDesc);
            Debug.Log($"スコア送信: Board1={_lastFinalScore}, Board2(役満)={yakumanCount}");
        }

        if (sendRankingButton != null) sendRankingButton.interactable = false;
    }
    //    {
    //        // ���C��: �����Ȃ� .Instance ���Ă΂��A�܂��͑��݊m�F����
    //        // (Unity 2023�ȍ~�� FindAnyObjectByType, ����ȑO�� FindObjectOfType)
    //        var client = FindAnyObjectByType<unityroom.Api.UnityroomApiClient>();
    //
    //        // ���݂��Ȃ��ꍇ�̋~�ϑ[�u
    //        if (client == null)
    //        {
    //            if (unityroomApiClientPrefab != null)
    //            {
    //                Instantiate(unityroomApiClientPrefab);
    //                Debug.Log("<color=green>���M���O�� UnityroomApiClient ���ً}�������܂���</color>");
    //            }
    //            else
    //            {
    //                Debug.LogError("�y�ݒ�~�X�zMahjongCanvas �� Inspector �� UnityroomApiClient �̃v���n�u���ݒ肳��Ă��܂���I�����L���O���M�𒆎~���܂��B");
    //                return; // �����ŏ����𒆒f���Ȃ��Ǝ��̍s�ŃG���[�ɂȂ�
    //            }
    //        }
    //
    //        // --- ������������̏��� ---
    //        int yakumanCount = 0;
    //        if (MahjongGameManager.Instance != null)
    //        {
    //            yakumanCount = MahjongGameManager.Instance.GetUnlockedYakumanCount();
    //        }
    //
    //        // ���S�� Instance ���Ă�
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

    // ���C��: �v���n�u���g�킸�R�[�h�ŉ摜����
    public void ShowUkeirePanel(List<int> effectiveTiles)
    {
        if (ukeirePanel == null || ukeireGrid == null)
        {
            Debug.LogWarning("UkeirePanel or Grid is not assigned in Inspector.");
            return;
        }

        // 1. �O��̕\�����e���N���A
        foreach (Transform child in ukeireGrid)
        {
            Destroy(child.gameObject);
        }

        // 2. �L���v���Ȃ��ꍇ�͔�\���ɂ��ďI��
        if (effectiveTiles == null || effectiveTiles.Count == 0)
        {
            ukeirePanel.SetActive(false);
            return;
        }

        // 3. �v�̉摜���R�[�h�Ő������ĕ��ׂ�
        foreach (int tileId in effectiveTiles)
        {
            // �V�����Q�[���I�u�W�F�N�g���쐬
            GameObject tileObj = new GameObject($"Tile_{tileId}", typeof(RectTransform), typeof(Image));

            // Grid�̎q�v�f�ɂ���
            tileObj.transform.SetParent(ukeireGrid, false);

            // Image�R���|�[�l���g���擾���ăX�v���C�g��ݒ�
            Image img = tileObj.GetComponent<Image>();
            if (tileId >= 0 && tileId < tileSprites.Length)
            {
                img.sprite = tileSprites[tileId];
            }
            else
            {
                img.color = Color.white; // �X�v���C�g���Ȃ��ꍇ�͔��l�p
            }
        }

        // 4. �p�l����\��
        ukeirePanel.SetActive(true);
    }

    public void HideUkeirePanel()
    {
        if (ukeirePanel != null) ukeirePanel.SetActive(false);
    }

    [Header("Yakuman UI")]
    [SerializeField] private GameObject yakumanConfirmPanel; // �𖞊m�F�p�̃p�l���i�{�^�����܂ސe�j

    [Header("Yakuman UI")]
    [SerializeField] private GameObject yakumanRoot;       // �e�I�u�W�F�N�g�i��̃I�u�W�F�N�g�j
    [SerializeField] private GameObject yakumanPanelA;     // ��̃p�l���i�N���b�N�ŏ�����p�j
    [SerializeField] private GameObject yakumanPanelB;     // ���̃p�l���i�{�^��������p�j

    // �Q�[���J�n���⃊�U���g����鎞�ɌĂԃ��Z�b�g����
    public void ResetYakumanUI()
    {
        if (yakumanRoot != null) yakumanRoot.SetActive(false);
        if (yakumanPanelA != null) yakumanPanelA.SetActive(true); // A�͏����\��
        if (yakumanPanelB != null) yakumanPanelB.SetActive(false); // B�͔�\��
    }

    // GameManager����Ă΂��F���o�J�n
    public void ShowYakumanConfirmUI()
    {
        if (yakumanRoot != null)
        {
            yakumanRoot.SetActive(true);
            yakumanPanelA.SetActive(true);
            yakumanPanelB.SetActive(false);
        }
    }

    // �p�l��A���N���b�N�������ɌĂԁi�C���X�y�N�^�[����EventTrigger�ȂǂŐݒ�j
    public void OnPanelAClicked()
    {
        StartCoroutine(SwitchPanelAfterDelay());
    }

    IEnumerator SwitchPanelAfterDelay()
    {
        yield return new WaitForSeconds(0.3f);

        yakumanPanelA.SetActive(false);
        yakumanPanelB.SetActive(true);
    }


    // �ǉ��Ȃ�OK�i���̂܂܂�OK�j

    public void OnYakumanConfirmButtonClicked()
    {
        yakumanRoot.SetActive(false);

        MahjongGameManager.Instance.StartYakumanProduction();
    }
    [SerializeField] private GameObject blackPanel; // ���o�p�̍����p�l��

    public void OnShowBlackPanel()
    {
        StartCoroutine(WaitBlackPanel(1.5f)); // 1.5�b�\�����Ă��������
    }

    IEnumerator WaitBlackPanel(float duration)
    {
        if (blackPanel != null) blackPanel.SetActive(true);
        yield return new WaitForSeconds(duration);
        if (blackPanel != null) blackPanel.SetActive(false);
    }
}





