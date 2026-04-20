using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System; // Actionのために必要

public class YakuVoiceManager : MonoBehaviour
{
    [System.Serializable]
    public struct YakuVoiceData
    {
        [Tooltip("役名（自動生成されます）")]
        public string yakuName;
        
        [Tooltip("再生候補の音声リスト。ここからランダムに1つ選ばれます")]
        public List<AudioClip> voiceClips;
    }

    [Header("Settings")]
    public AudioSource audioSource;
    
    [Tooltip("役ごとの音声設定リスト（コンポーネントをResetすると自動でリストが作られます）")]
    public List<YakuVoiceData> yakuVoiceList;
    
    [Tooltip("点数区分（満貫・役満など）の音声設定リスト")]
    public List<YakuVoiceData> scoreVoiceList; // ★追加: 点数用リスト
    
    [Tooltip("音声がない場合のデフォルト待機時間（秒）")]
    public float defaultDelay = 0.5f;

    // 内部辞書
    private Dictionary<string, List<AudioClip>> _clipDict;

    private void Awake()
    {
        // 検索用に辞書に変換
        _clipDict = new Dictionary<string, List<AudioClip>>();
        
        // 役リストを辞書に登録
        if (yakuVoiceList != null)
        {
            RegisterToDict(yakuVoiceList);
        }

        // ★追加: 点数リストも辞書に登録
        if (scoreVoiceList != null)
        {
            RegisterToDict(scoreVoiceList);
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    // 辞書登録ヘルパー
    private void RegisterToDict(List<YakuVoiceData> list)
    {
        foreach (var data in list)
        {
            if (!_clipDict.ContainsKey(data.yakuName))
            {
                List<AudioClip> validClips = new List<AudioClip>();
                if (data.voiceClips != null)
                {
                    foreach (var clip in data.voiceClips)
                    {
                        if (clip != null) validClips.Add(clip);
                    }
                }

                // クリップが空でもキーだけは登録しておく
                if (validClips.Count > 0)
                {
                    _clipDict.Add(data.yakuName, validClips);
                }
            }
        }
    }

    /// <summary>
    /// 演出シーケンスを開始する
    /// </summary>
    // ★修正: コールバック処理(onYakuFinished)を受け取るように引数を変更
    public void StartAnnouncement(List<string> yakuList, TMP_Text targetText, string headerStr, string footerStr, string scoreName, Action onYakuFinished)
    {
        if (targetText == null) return;
        StopAllCoroutines(); // 念のため
        StartCoroutine(AnnounceRoutine(yakuList, targetText, headerStr, footerStr, scoreName, onYakuFinished));
    }

    private IEnumerator AnnounceRoutine(List<string> yakuList, TMP_Text targetText, string headerStr, string footerStr, string scoreName, Action onYakuFinished)
    {
        // 辞書が未初期化なら初期化（Awake失敗対策）
        if (_clipDict == null) Awake();

        string currentDisplayText = headerStr + "\n\n";
        targetText.text = currentDisplayText;

        yield return new WaitForSeconds(0.3f);

        // 1. 役の読み上げ
        foreach (string yaku in yakuList)
        {
            currentDisplayText += $"<size=80%>{yaku}</size>\n";
            targetText.text = currentDisplayText;

            // --- 検索キーの正規化ロジック ---
            string searchKey = yaku;

            // 特殊判定（ドラ系）
            if (yaku.Contains("赤ドラ")) searchKey = "赤ドラ";
            else if (yaku.Contains("裏ドラ")) searchKey = "裏ドラ";
            else if (yaku.Contains("抜きドラ")) searchKey = "抜きドラ";
            else if (yaku.Contains("ドラ")) searchKey = "ドラ";
            // 役牌判定の補正 ("役牌 白" -> "白")
            else if (yaku.StartsWith("役牌 ")) searchKey = yaku.Replace("役牌 ", "");

            // 再生処理
            yield return PlayVoiceIfExists(searchKey);
        }

        // 2. ★重要: 役読み上げ完了のコールバックを実行
        // ここでCanvas側が「点数テキストを表示」＆「SE再生」を行う
        yield return new WaitForSeconds(0.2f); // 少し間を開ける
        onYakuFinished?.Invoke();

        // 3. 点数区分（満貫、跳満など）を読み上げ
        if (!string.IsNullOrEmpty(scoreName))
        {
            // Canvas側でSEが鳴るタイミングと合わせるため、ほんの少し待つか即再生するか調整
            // ここではSEと被らないように少し待つ
            yield return new WaitForSeconds(0.4f); 
            
            yield return PlayVoiceIfExists(scoreName);
        }

        // 4. フッター表示 (Canvas側ですでにfooterの内容を含んだテキストを表示しているなら不要だが、
        // 念のためfooterStrを最後に追加する形は残しておく。
        // ただし、Canvas側でwinScoreTextにfooterを表示しているので、resultTextには追加しなくても良いかもしれない。
        // ここでは仕様維持のため、resultTextにも一応追加しておくが、UIが二重に見える場合は削除してください)
        // targetText.text = currentDisplayText + "\n" + footerStr; 
    }

    // 音声再生ヘルパー
    private IEnumerator PlayVoiceIfExists(string key)
    {
        AudioClip clipToPlay = null;

        // 辞書から検索（ランダム選択機能を維持）
        if (_clipDict.TryGetValue(key, out List<AudioClip> clips) && clips.Count > 0)
        {
            clipToPlay = clips[UnityEngine.Random.Range(0, clips.Count)];
        }

        if (clipToPlay != null)
        {
            audioSource.PlayOneShot(clipToPlay);
            yield return new WaitForSeconds(clipToPlay.length + 0.1f); // 少し余韻を持たせる
        }
        else
        {
            // Debug.LogWarning($"[YakuVoice] No voice clip for: {key}");
            yield return new WaitForSeconds(defaultDelay);
        }
    }

    // ==================================================================================
    // Unityエディタ用: コンポーネントをResetするとリストを自動生成
    // ==================================================================================
    private void Reset()
    {
        // AudioSourceの自動取得
        audioSource = GetComponent<AudioSource>();

        // デフォルトの役リスト定義
        string[] defaultYakus = new string[] 
        {
            "立直", "ダブル立直", "一発", "嶺上開花", "海底撈月", "河底撈魚",
            "門前清自摸和", "槍槓", "平和", "一盃口", "二盃口",
            "白", "發", "中", "自風牌", "場風牌",
            "七対子", "断幺九", "混全帯幺九", "一気通貫",
            "三色同順", "三色同刻", "三槓子", "対々和", "三暗刻", "小三元",
            "混老頭", "混一色", "純全帯幺九", "清一色",
            "天和", "地和", "大三元", "四暗刻", "字一色", "緑一色", "清老頭",
            "国士無双", "小四喜", "四槓子", "九蓮宝燈",
            "四暗刻単騎", "国士無双13面待ち", "純正九蓮宝燈", "大四喜",
            "ドラ", "赤ドラ", "抜きドラ", "裏ドラ", "真ん中強し",
        };

        // ★追加: デフォルトの点数リスト
        string[] defaultScores = new string[]
        {
            "満貫", "跳満", "倍満", "三倍満", "役満", "ダブル役満"
        };

        // 役リストの生成
        yakuVoiceList = new List<YakuVoiceData>();
        foreach (var yname in defaultYakus)
        {
            yakuVoiceList.Add(new YakuVoiceData 
            { 
                yakuName = yname, 
                voiceClips = new List<AudioClip>() 
            });
        }

        // 点数リストの生成
        scoreVoiceList = new List<YakuVoiceData>();
        foreach (var sname in defaultScores)
        {
            scoreVoiceList.Add(new YakuVoiceData
            {
                yakuName = sname,
                voiceClips = new List<AudioClip>()
            });
        }
    }
}