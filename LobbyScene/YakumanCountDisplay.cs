using UnityEngine;
using TMPro;

// TMP_Text がついているオブジェクトにアタッチするか、
// Inspectorで TextComponent を指定してください
public class YakumanCountDisplay : MonoBehaviour
{
    [Header("表示設定")]
    [SerializeField] private TMP_Text textComponent;
    
    [Tooltip("表示フォーマット。{0}が解除数、{1}が総数に置換されます")]
    [SerializeField] private string format = "Unlocked: {0} / {1}";

    // カウント対象の役満リスト
    // MahjongGameManager.TargetYakumanList と同じ内容です
    private readonly string[] TargetYakumanList = new string[]
    {
        "KokushiMusou", "Kokushi13Men", 
        "SuAnko", "SuTan", 
        "DaiSanGen", 
        "RyuIso", 
        "TsuIso", 
        "ShoSushi", "DaiSushi", 
        "ChinRoTo", 
        "SuKantsu", 
        "ChurenPoto", "JunseiChurenPoto", 
        "Tenho", "ManNakaTSUYOSHI"
    };

    void Start()
    {
        // アタッチされたコンポーネントを自動取得（設定がなければ）
        if (textComponent == null)
        {
            textComponent = GetComponent<TMP_Text>();
        }

        UpdateDisplay();
    }

    /// <summary>
    /// 保存データを確認して表示を更新する
    /// </summary>
    public void UpdateDisplay()
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

        // テキストを更新
        textComponent.text = string.Format(format, unlockedCount, totalCount);
    }
    
    // デバッグ用: リセット機能
    public void ResetAllAchievements()
    {
        foreach (var yaku in TargetYakumanList)
        {
            string key = "Yakuman_" + yaku;
            PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        UpdateDisplay();
        Debug.Log("All Yakuman achievements reset.");
    }
}