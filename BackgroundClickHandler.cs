using UnityEngine;
using UnityEngine.EventSystems;

public class BackgroundClickHandler : MonoBehaviour, IPointerClickHandler
{
    // ダブルクリックの間隔を判定する閾値（秒）
    private const float DOUBLE_CLICK_THRESHOLD = 0.4f;

    public void OnPointerClick(PointerEventData eventData)
    {
        // clickCount が 2 の時に実行
        if (eventData.clickCount == 2)
        {
            if (MahjongGameManager.Instance != null)
            {
                var player = MahjongGameManager.Instance.connectedPlayers[0];
                if (player != null)
                {
                    // デバッグログを出しておくと動作確認しやすいです
                    Debug.Log("画面ダブルクリックによるツモ切り実行");
                    player.PerformTsumogiri();
                }
            }
        }
    }
}