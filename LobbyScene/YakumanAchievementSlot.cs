using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]            // Imageコンポーネント必須
[RequireComponent(typeof(SmoothHoverObject))] // SmoothHoverObjectコンポーネント必須
public class YakumanAchievementSlot : MonoBehaviour
{
    [Header("実績設定")]
    [Tooltip("MahjongGameManagerで定義した役満の名前と完全に一致させてください（例: 国士無双）")]
    public string targetYakumanName;

    [Header("画像設定")]
    public Sprite unlockedSprite; // 解除された時の画像（カラーなど）
    public Sprite lockedSprite;   // 未解除の時の画像（シルエットや鍵アイコンなど）

    private void Start()
    {
        UpdateSlotStatus();
    }

    public void UpdateSlotStatus()
    {
        // 1. 保存された実績データの確認
        // キーは Manager で保存した "Yakuman_" + 役満名
        string key = "Yakuman_" + targetYakumanName;
        bool isUnlocked = PlayerPrefs.GetInt(key, 0) == 1;

        // 2. 画像の切り替え
        Image targetImage = GetComponent<Image>();
        if (targetImage != null)
        {
            if (isUnlocked)
            {
                if (unlockedSprite != null) targetImage.sprite = unlockedSprite;
                targetImage.color = Color.white; // 本来の色
            }
            else
            {
                if (lockedSprite != null) targetImage.sprite = lockedSprite;
                else
                {
                    // ロック画像が設定されていない場合は、黒くするなどの対応
                    targetImage.color = Color.black; 
                }
            }
        }

        // 3. ホバー時のテキスト切り替え (SmoothHoverObject連携)
        SmoothHoverObject hoverScript = GetComponent<SmoothHoverObject>();
        if (hoverScript != null)
        {
            if (isUnlocked)
            {
                // 解除済みなら役満名を表示
                hoverScript.SetDisplayName(targetYakumanName);
            }
            else
            {
                // 未解除なら "???" にする
                hoverScript.SetDisplayName("???");
            }
        }
    }
    
    // デバッグ用: 強制的にロック状態をリセットしたい場合に使用
    public void ResetStatus()
    {
         string key = "Yakuman_" + targetYakumanName;
         PlayerPrefs.DeleteKey(key);
         UpdateSlotStatus();
    }
}