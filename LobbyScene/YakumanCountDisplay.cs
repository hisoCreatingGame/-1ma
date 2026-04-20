using UnityEngine;
using TMPro;
using System.Collections; // コルーチン用

public class YakumanCountDisplay : MonoBehaviour
{
    [Header("表示設定")]
    [SerializeField] private TMP_Text textComponent;
    
    [Tooltip("役満解除数のフォーマット。{0}が解除数、{1}が総数")]
    [SerializeField] private string yakumanFormat = "実績解除数: {0} / {1}";

    [Tooltip("ハイスコアのフォーマット。{0}がスコア")]
    [SerializeField] private string highScoreFormat = "最高得点: {0}";

    [Tooltip("表示切り替え間隔（秒）")]
    [SerializeField] private float toggleInterval = 4.0f;

    // ★修正: MahjongGameManager.cs で保存しているキー（日本語）に合わせないとロードできません
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

    void Start()
    {
        if (textComponent == null)
        {
            textComponent = GetComponent<TMP_Text>();
        }

        // コルーチンを開始してループ表示させる
        StartCoroutine(DisplayLoopRoutine());
    }

    private IEnumerator DisplayLoopRoutine()
    {
        while (true)
        {
            // 1. 役満実績数を表示
            ShowYakumanCount();
            yield return new WaitForSeconds(toggleInterval);

            // 2. ハイスコアを表示
            ShowHighScore();
            yield return new WaitForSeconds(toggleInterval);
        }
    }

    /// <summary>
    /// 役満解除数を表示
    /// </summary>
    private void ShowYakumanCount()
    {
        if (textComponent == null) return;

        int unlockedCount = 0;
        int totalCount = TargetYakumanList.Length;

        foreach (var yaku in TargetYakumanList)
        {
            string key = "Yakuman_" + yaku;
            // 1 なら解除済み
            if (PlayerPrefs.GetInt(key, 0) == 1)
            {
                unlockedCount++;
            }
        }

        textComponent.text = string.Format(yakumanFormat, unlockedCount, totalCount);
    }

    /// <summary>
    /// ハイスコアを表示
    /// </summary>
    private void ShowHighScore()
    {
        if (textComponent == null) return;

        // MahjongCanvas.cs で保存しているキー "HighScore" を読み込む
        int currentHighScore = PlayerPrefs.GetInt("HighScore", 0);

        textComponent.text = string.Format(highScoreFormat, currentHighScore);
    }
    
    // デバッグ用: リセット機能
    public void ResetAllAchievements()
    {
        foreach (var yaku in TargetYakumanList)
        {
            string key = "Yakuman_" + yaku;
            PlayerPrefs.DeleteKey(key);
        }
        // ハイスコアもリセットしたい場合は以下を追加
        // PlayerPrefs.DeleteKey("HighScore");
        
        PlayerPrefs.Save();
        Debug.Log("Achievements reset.");
        
        // リセット直後の表示更新（コルーチンのタイミングを待たずに即反映したければ）
        ShowYakumanCount(); 
    }
}