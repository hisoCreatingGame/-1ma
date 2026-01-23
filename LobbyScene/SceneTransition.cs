using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections; // コルーチンに必要

public class SceneTransition : MonoBehaviour
{
    [SerializeField] private string nextSceneName; // 遷移先のシーン名

    // ボタンに登録する関数
    public void OnTransitionButtonPressed()
    {
        StartCoroutine(WaitAndLoad());
    }

    IEnumerator WaitAndLoad()
    {
        // ここにクリック音などを再生するコードを入れても良いですね

        // 1秒待つ
        yield return new WaitForSeconds(1.0f);

        // シーンをロード
        SceneManager.LoadScene(nextSceneName);
    }
}