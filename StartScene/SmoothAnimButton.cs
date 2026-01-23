using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic; // Dictionaryに必要
using UnityEngine.SceneManagement; // シーン遷移イベント検知に必要

public class SmoothAnimButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("シーン間保持設定")]
    [Tooltip("チェックを入れると、シーン遷移してもこのボタンが消えなくなります")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Header("アニメーション設定")]
    [SerializeField] private Animator targetAnimator; // 動かしたいAnimator
    [SerializeField] private string triggerName = "OnPress"; // AnimatorのTriggerパラメータ名

    [Header("ズーム設定")]
    [SerializeField] private float zoomScale = 1.2f;
    [SerializeField] private float duration = 0.2f;
    [SerializeField] private float startDelay = 0.0f;

    private Vector3 defaultScale;
    private Coroutine currentCoroutine;

    // 重複防止のための管理リスト（名前で管理）
    private static Dictionary<string, SmoothAnimButton> instances = new Dictionary<string, SmoothAnimButton>();

    void Awake()
    {
        // シーン間保持が有効な場合
        if (persistAcrossScenes)
        {
            // 同じ名前のボタンが既に存在しているかチェック
            if (instances.ContainsKey(gameObject.name) && instances[gameObject.name] != null && instances[gameObject.name] != this)
            {
                // 既に存在する場合は、新しく作られた自分自身を破壊する（重複防止）
                Destroy(gameObject);
                return;
            }
            else
            {
                // リストに登録して、破壊されないように設定
                instances[gameObject.name] = this;
                DontDestroyOnLoad(gameObject);
            }
        }
    }

    void Start()
    {
        defaultScale = transform.localScale;

        // もしTargetAnimatorが空なら、自分自身のAnimatorを取得してみる
        if (targetAnimator == null)
        {
            targetAnimator = GetComponent<Animator>();
        }

        // 透明部分の無視設定
        Image img = GetComponent<Image>();
        if (img != null)
        {
            img.alphaHitTestMinimumThreshold = 0.1f;
        }
    }

    // シーンが切り替わったときのイベント登録
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 新しいシーンがロードされた時に呼ばれる
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!persistAcrossScenes) return;

        // 自分自身が破壊される予定のオブジェクトなら何もしない
        if (this == null) return;

        // UIはCanvasの子要素でないと表示されないため、
        // シーン遷移後に新しいシーンのCanvasを探して、その子供になる
        Canvas canvas = FindAnyObjectByType<Canvas>();
        
        // ※FakeCursorがCanvasを持っている場合、それに吸着すると困るので
        // "Default"や"UI"レイヤー、あるいは特定の名前のCanvasを探す手もありますが
        // ここでは単純に見つかったCanvasの直下に移動させます。
        // もし表示がおかしい場合は、MahjongCanvasなどがついているCanvasを明示的に探す必要があります。
        
        if (canvas != null)
        {
            // Canvasの下に移動（ローカル座標を維持しつつ親変更）
            transform.SetParent(canvas.transform, false);
            
            // 最前面に表示（任意）
            transform.SetAsLastSibling(); 
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(ScaleTo(defaultScale * zoomScale, startDelay));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (currentCoroutine != null) StopCoroutine(currentCoroutine);
        currentCoroutine = StartCoroutine(ScaleTo(defaultScale, 0f));
    }

    // クリック時の処理
    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetAnimator != null)
        {
            // Animatorに「トリガー」を送ってアニメーション再生
            targetAnimator.SetTrigger(triggerName);
        }
    }

    IEnumerator ScaleTo(Vector3 targetScale, float delay)
    {
        if (delay > 0) yield return new WaitForSeconds(delay);

        Vector3 startScale = transform.localScale;
        float time = 0;

        while (time < duration)
        {
            transform.localScale = Vector3.Lerp(startScale, targetScale, time / duration);
            time += Time.deltaTime;
            yield return null;
        }
        transform.localScale = targetScale;
    }
}