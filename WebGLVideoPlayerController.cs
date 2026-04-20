using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class WebGLVideoPlayerController : MonoBehaviour
{
    public static WebGLVideoPlayerController Instance;

    [Header("UI")]
    public RawImage videoImage;
    public CanvasGroup canvasGroup;

    [Header("Video")]
    public VideoPlayer videoPlayer;

    [Header("Timeout (seconds)")]
    [SerializeField] private float prepareTimeout = 25f;
    [SerializeField] private float firstTimePrepareTimeout = 90f;
    [SerializeField] private float reprepareRetryTimeout = 30f;
    [SerializeField] private float playbackTimeout = 45f;
    [SerializeField] private float firstFrameTimeout = 2f;
    [SerializeField] private float startupWarmupPrepareTimeout = 40f;
    [SerializeField] private float startupWarmupProbeSeconds = 2f;

    private bool isPrepared;
    private bool playRequested;
    private bool playbackStartedForCurrentSetup;
    private Coroutine prepareCoroutine;
    private Coroutine playCoroutine;
    private Coroutine timeoutCoroutine;
    private Coroutine warmupCoroutine;

    private bool hasStartupWarmup;
    private bool warmupAudioModeOverridden;
    private VideoAudioOutputMode audioOutputModeBeforeWarmup;
    private readonly HashSet<string> preparedVideoUrls = new HashSet<string>();
    private bool hiddenWarmupRunning;
    private bool suppressVideoVisual;
    private GameObject hiddenWarmupObject;
    private VideoPlayer hiddenWarmupPlayer;
    private RenderTexture hiddenWarmupTexture;

    public bool IsVideoFinished { get; private set; } = true;
    public bool IsPrepared => isPrepared;
    public bool HasStartupWarmup => hasStartupWarmup;

    private void Awake()
    {
        Instance = this;

        if (videoImage == null)
        {
            videoImage = GetComponent<RawImage>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null && videoImage != null)
            {
                canvasGroup = videoImage.gameObject.AddComponent<CanvasGroup>();
            }
        }

        SetAlpha(0f);

        if (videoPlayer == null)
        {
            videoPlayer = GetComponent<VideoPlayer>();
        }

        if (videoPlayer == null)
        {
            Debug.LogError("[WebGLVideoPlayerController] VideoPlayer is not assigned.");
            return;
        }

        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.source = VideoSource.Url;
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;

        // 既存シーンのシリアライズ値が小さすぎる場合の最低保証
        prepareTimeout = Mathf.Max(prepareTimeout, 25f);
        firstTimePrepareTimeout = Mathf.Max(firstTimePrepareTimeout, prepareTimeout);
        startupWarmupPrepareTimeout = Mathf.Max(startupWarmupPrepareTimeout, 25f);
        playbackTimeout = Mathf.Max(playbackTimeout, 45f);
        firstFrameTimeout = Mathf.Max(firstFrameTimeout, 1f);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
        }

        CleanupHiddenWarmupPlayer();
    }

    public void SetupVideo(string fileName)
    {
        if (videoPlayer == null || string.IsNullOrEmpty(fileName))
        {
            IsVideoFinished = true;
            return;
        }

        StopPlaybackAndCoroutines();
        SetVisualSuppressed(false);

        IsVideoFinished = false;
        isPrepared = false;
        playRequested = false;
        playbackStartedForCurrentSetup = false;
        videoPlayer.isLooping = false;
        SetAlpha(0f);
        ClearTargetTexture();

        videoPlayer.url = BuildStreamingAssetUrl(fileName);

        videoPlayer.Prepare();
        prepareCoroutine = StartCoroutine(PrepareRoutine());
    }

    public void PlayVideo()
    {
        if (videoPlayer == null)
        {
            IsVideoFinished = true;
            return;
        }

        if (playRequested && (!isPrepared || playbackStartedForCurrentSetup))
        {
            return;
        }

        playRequested = true;
        if (isPrepared && !playbackStartedForCurrentSetup)
        {
            StartPlaybackRoutine();
        }
    }

    // 起動直後に一度だけデコーダとキャッシュを温める
    public void WarmupStartupVideo(string fileName)
    {
        if (videoPlayer == null || string.IsNullOrEmpty(fileName) || hasStartupWarmup)
        {
            return;
        }

        if (warmupCoroutine != null)
        {
            return;
        }

        warmupCoroutine = StartCoroutine(WarmupStartupRoutine(fileName));
    }

    // Gameシーン開始時に無音・透明で裏再生し続け、初回本再生のラグを減らす
    public void StartHiddenWarmupLoop(string fileName)
    {
        if (videoPlayer == null || string.IsNullOrEmpty(fileName))
        {
            return;
        }

        if (warmupCoroutine != null || hiddenWarmupRunning)
        {
            return;
        }

        // 互換のためメソッド名は維持するが、実際は「完全非表示の1回確認」だけ行う
        warmupCoroutine = StartCoroutine(HiddenWarmupLoopRoutine(fileName));
    }

    private IEnumerator PrepareRoutine()
    {
        string targetUrl = videoPlayer.url;
        bool isFirstPrepareForUrl = !string.IsNullOrEmpty(targetUrl) && !preparedVideoUrls.Contains(targetUrl);
        float timeoutForThisPrepare = isFirstPrepareForUrl
            ? Mathf.Max(prepareTimeout, firstTimePrepareTimeout)
            : prepareTimeout;

        float elapsed = 0f;
        while (!videoPlayer.isPrepared && elapsed < timeoutForThisPrepare)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // WebGL初回読み込み時はPrepareが失敗することがあるため、1回だけ再試行する
        if (!videoPlayer.isPrepared && isFirstPrepareForUrl && reprepareRetryTimeout > 0f)
        {
            videoPlayer.Prepare();
            elapsed = 0f;
            while (!videoPlayer.isPrepared && elapsed < reprepareRetryTimeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        prepareCoroutine = null;

        if (!videoPlayer.isPrepared)
        {
            Debug.LogWarning($"[WebGLVideoPlayerController] Prepare timeout: {videoPlayer.url}");
            ForceFinishImmediate();
            yield break;
        }

        isPrepared = true;
        if (!string.IsNullOrEmpty(targetUrl))
        {
            preparedVideoUrls.Add(targetUrl);
        }

        if (playRequested)
        {
            StartPlaybackRoutine();
        }
    }

    private void StartPlaybackRoutine()
    {
        if (playbackStartedForCurrentSetup)
        {
            return;
        }

        playbackStartedForCurrentSetup = true;

        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
        }

        playCoroutine = StartCoroutine(PlayRoutine());

        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
        }
        timeoutCoroutine = StartCoroutine(PlaybackTimeoutRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (videoPlayer == null)
        {
            ForceFinishImmediate();
            yield break;
        }

        SetVisualSuppressed(false);
        videoPlayer.isLooping = false;
        SetAlpha(0f);
        ClearTargetTexture();
        videoPlayer.Play();

        float elapsed = 0f;
        while (elapsed < firstFrameTimeout && !IsVideoFinished)
        {
            bool firstFrameArrived = videoPlayer.frame > 0 || videoPlayer.time > 0.01;
            if (firstFrameArrived)
            {
                break;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (IsVideoFinished)
        {
            yield break;
        }

        yield return Fade(0f, 1f, 0.3f);
    }

    private IEnumerator WarmupStartupRoutine(string fileName)
    {
        // 通常再生系の処理を止めて、画面に出さずにウォームアップする
        if (prepareCoroutine != null)
        {
            StopCoroutine(prepareCoroutine);
            prepareCoroutine = null;
        }
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        playRequested = false;
        playbackStartedForCurrentSetup = false;
        isPrepared = false;
        IsVideoFinished = true;
        SetAlpha(0f);
        ClearTargetTexture();
        SetVisualSuppressed(true);

        // 自動再生制約の影響を減らすため、ウォームアップ中は無音化
        audioOutputModeBeforeWarmup = videoPlayer.audioOutputMode;
        warmupAudioModeOverridden = true;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.None;

        videoPlayer.source = VideoSource.Url;
        videoPlayer.url = BuildStreamingAssetUrl(fileName);
        videoPlayer.Prepare();

        float elapsed = 0f;
        while (!videoPlayer.isPrepared && elapsed < startupWarmupPrepareTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!videoPlayer.isPrepared)
        {
            Debug.LogWarning($"[WebGLVideoPlayerController] Startup warmup prepare timeout: {fileName}");
            RestoreWarmupAudioMode();
            ClearTargetTexture();
            warmupCoroutine = null;
            yield break;
        }

        videoPlayer.Play();

        float probeElapsed = 0f;
        while (probeElapsed < startupWarmupProbeSeconds)
        {
            bool gotFirstFrame = videoPlayer.frame > 0 || videoPlayer.time > 0.01;
            if (gotFirstFrame)
            {
                break;
            }

            probeElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
        videoPlayer.time = 0d;
        RestoreWarmupAudioMode();

        if (!string.IsNullOrEmpty(videoPlayer.url))
        {
            preparedVideoUrls.Add(videoPlayer.url);
        }
        hasStartupWarmup = true;
        ClearTargetTexture();
        warmupCoroutine = null;
    }

    private IEnumerator HiddenWarmupLoopRoutine(string fileName)
    {
        // 通常再生系の処理を止めて、画面に出さずにウォームアップする
        if (prepareCoroutine != null)
        {
            StopCoroutine(prepareCoroutine);
            prepareCoroutine = null;
        }
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        playRequested = false;
        playbackStartedForCurrentSetup = false;
        isPrepared = false;
        IsVideoFinished = true;
        hiddenWarmupRunning = true;
        SetAlpha(0f);
        ClearTargetTexture();
        SetVisualSuppressed(true);
        CleanupHiddenWarmupPlayer();
        hiddenWarmupObject = new GameObject("HiddenWarmupVideoPlayer");
        hiddenWarmupObject.transform.SetParent(transform, false);
        hiddenWarmupPlayer = hiddenWarmupObject.AddComponent<VideoPlayer>();
        hiddenWarmupPlayer.playOnAwake = false;
        hiddenWarmupPlayer.waitForFirstFrame = true;
        hiddenWarmupPlayer.source = VideoSource.Url;
        hiddenWarmupPlayer.audioOutputMode = VideoAudioOutputMode.None;
        hiddenWarmupPlayer.isLooping = false;
        hiddenWarmupPlayer.renderMode = VideoRenderMode.RenderTexture;
        hiddenWarmupTexture = new RenderTexture(32, 32, 0, RenderTextureFormat.ARGB32);
        hiddenWarmupTexture.Create();
        hiddenWarmupPlayer.targetTexture = hiddenWarmupTexture;
        hiddenWarmupPlayer.url = BuildStreamingAssetUrl(fileName);
        hiddenWarmupPlayer.Prepare();

        float elapsed = 0f;
        float prepareLimit = Mathf.Max(startupWarmupPrepareTimeout, firstTimePrepareTimeout);
        while (hiddenWarmupPlayer != null && !hiddenWarmupPlayer.isPrepared && elapsed < prepareLimit)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (hiddenWarmupPlayer == null || !hiddenWarmupPlayer.isPrepared)
        {
            Debug.LogWarning($"[WebGLVideoPlayerController] Hidden warmup prepare timeout: {fileName}");
            hiddenWarmupRunning = false;
            ClearTargetTexture();
            SetVisualSuppressed(true);
            CleanupHiddenWarmupPlayer();
            warmupCoroutine = null;
            yield break;
        }

        if (!string.IsNullOrEmpty(hiddenWarmupPlayer.url))
        {
            preparedVideoUrls.Add(hiddenWarmupPlayer.url);
        }
        hasStartupWarmup = true;

        hiddenWarmupPlayer.Play();

        float probeElapsed = 0f;
        while (hiddenWarmupPlayer != null && probeElapsed < Mathf.Max(startupWarmupProbeSeconds, 2f))
        {
            bool gotFirstFrame = hiddenWarmupPlayer.frame > 0 || hiddenWarmupPlayer.time > 0.01;
            if (gotFirstFrame)
            {
                break;
            }

            probeElapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 1回確認後は停止（裏ループはしない）
        CleanupHiddenWarmupPlayer();
        hiddenWarmupRunning = false;
        ClearTargetTexture();

        // 本再生開始までは非表示維持
        SetVisualSuppressed(true);
        warmupCoroutine = null;
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        if (hiddenWarmupRunning || suppressVideoVisual)
        {
            return;
        }

        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }
        StartCoroutine(FinishRoutine());
    }

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogWarning($"[WebGLVideoPlayerController] Video error: {message} ({vp.url})");
        ForceFinishImmediate();
    }

    private IEnumerator FinishRoutine()
    {
        yield return new WaitForSecondsRealtime(0.3f);
        yield return Fade(1f, 0f, 0.25f);

        if (videoPlayer != null)
        {
            if (videoPlayer.isPlaying)
            {
                videoPlayer.Stop();
            }
            videoPlayer.time = 0d;
            videoPlayer.isLooping = false;
        }

        ClearTargetTexture();
        SetVisualSuppressed(true);

        IsVideoFinished = true;
        playRequested = false;
        playCoroutine = null;
        isPrepared = false;
        playbackStartedForCurrentSetup = false;
    }

    private IEnumerator PlaybackTimeoutRoutine()
    {
        float elapsed = 0f;
        while (!IsVideoFinished && elapsed < playbackTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        timeoutCoroutine = null;

        if (!IsVideoFinished)
        {
            Debug.LogWarning($"[WebGLVideoPlayerController] Playback timeout: {videoPlayer.url}");
            ForceFinishImmediate();
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetAlpha(to);
            yield break;
        }

        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(Mathf.Lerp(from, to, t / duration));
            yield return null;
        }

        SetAlpha(to);
    }

    private void StopPlaybackAndCoroutines()
    {
        if (warmupCoroutine != null)
        {
            StopCoroutine(warmupCoroutine);
            warmupCoroutine = null;
        }
        hiddenWarmupRunning = false;
        CleanupHiddenWarmupPlayer();

        if (prepareCoroutine != null)
        {
            StopCoroutine(prepareCoroutine);
            prepareCoroutine = null;
        }
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }
        if (timeoutCoroutine != null)
        {
            StopCoroutine(timeoutCoroutine);
            timeoutCoroutine = null;
        }

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (videoPlayer != null)
        {
            videoPlayer.time = 0d;
            videoPlayer.isLooping = false;
        }

        RestoreWarmupAudioMode();

        playRequested = false;
        playbackStartedForCurrentSetup = false;
        SetAlpha(0f);
        SetVisualSuppressed(true);
        ClearTargetTexture();
    }

    private void ForceFinishImmediate()
    {
        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        if (videoPlayer != null)
        {
            videoPlayer.time = 0d;
            videoPlayer.isLooping = false;
        }

        RestoreWarmupAudioMode();
        CleanupHiddenWarmupPlayer();

        isPrepared = false;
        playRequested = false;
        playbackStartedForCurrentSetup = false;
        IsVideoFinished = true;
        SetAlpha(0f);
        SetVisualSuppressed(true);
        ClearTargetTexture();
    }

    private void SetAlpha(float alpha)
    {
        if (suppressVideoVisual)
        {
            alpha = 0f;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = alpha;
        }

        // CanvasGroup と RawImage の両方を同期し、どちらが効いても表示されるようにする。
        if (videoImage != null)
        {
            Color c = videoImage.color;
            c.a = alpha;
            videoImage.color = c;
        }
    }

    private void SetVisualSuppressed(bool suppressed)
    {
        suppressVideoVisual = suppressed;

        if (videoImage != null)
        {
            videoImage.enabled = !suppressed;
        }

        if (suppressed)
        {
            SetAlpha(0f);
        }
    }

    private static string BuildStreamingAssetUrl(string fileName)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return Application.streamingAssetsPath + "/" + fileName;
#else
        return "file://" + Application.streamingAssetsPath + "/" + fileName;
#endif
    }

    private void ClearTargetTexture()
    {
        if (videoPlayer == null || videoPlayer.targetTexture == null)
        {
            return;
        }

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = videoPlayer.targetTexture;
        GL.Clear(true, true, Color.clear);
        RenderTexture.active = prev;
    }

    private void RestoreWarmupAudioMode()
    {
        if (!warmupAudioModeOverridden || videoPlayer == null)
        {
            return;
        }

        videoPlayer.audioOutputMode = audioOutputModeBeforeWarmup;
        warmupAudioModeOverridden = false;
    }

    private void CleanupHiddenWarmupPlayer()
    {
        if (hiddenWarmupPlayer != null && hiddenWarmupPlayer.isPlaying)
        {
            hiddenWarmupPlayer.Stop();
        }

        if (hiddenWarmupTexture != null)
        {
            hiddenWarmupTexture.Release();
            Destroy(hiddenWarmupTexture);
            hiddenWarmupTexture = null;
        }

        if (hiddenWarmupObject != null)
        {
            Destroy(hiddenWarmupObject);
            hiddenWarmupObject = null;
            hiddenWarmupPlayer = null;
        }
    }
}
