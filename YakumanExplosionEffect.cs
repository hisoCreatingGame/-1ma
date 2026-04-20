using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

public class YakumanExplosionEffect : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float scaleMultiplier = 2.0f;
    [Header("SE Keys")]
    [SerializeField] private string preVideoSeKey = SeKeys.GameYakumanPreVideoImage;

    private ParticleSystem _flash, _fireCore, _fireSub, _smoke, _sparks;
    private Canvas _overlayCanvas;
    private Image _overlayImage;
    private RawImage _videoScreen;
    private VideoPlayer _videoPlayer;
    private RenderTexture _videoTexture;

    private Material _fireMaterialAdditive;
    private Material _smokeMaterialAlpha;
    private VideoClip _targetClip;

    // 動画が終了したかどうか（GameManager側で待機するために参照可能）
    public bool IsVideoFinished { get; private set; } = false;

    // 1. 最初は爆発だけをセットアップして実行
    private void Start()
    {
        IsVideoFinished = false;
        SetupMaterials();
        CreateSystems();

        // 爆発のみを開始
        StartCoroutine(PlayExplosionOnly());
    }

    private void SetupMaterials()
    {
        Texture2D fireTex = CreateNoiseFireTexture(64);
        Shader additiveShader = FindFirstAvailableShader(
            "Mobile/Particles/Additive",
            "Legacy Shaders/Particles/Additive",
            "Particles/Standard Unlit",
            "Sprites/Default",
            "Unlit/Texture",
            "Standard");
        if (additiveShader == null)
        {
            Debug.LogWarning("[YakumanExplosionEffect] No additive shader found. Explosion may not be visible.");
        }
        if (additiveShader != null)
        {
            _fireMaterialAdditive = new Material(additiveShader) { mainTexture = fireTex };
        }

        Texture2D smokeTex = CreateSoftCircleTexture(64);
        Shader alphaShader = FindFirstAvailableShader(
            "Mobile/Particles/Alpha Blended",
            "Legacy Shaders/Particles/Alpha Blended",
            "Particles/Standard Unlit",
            "Sprites/Default",
            "Unlit/Texture",
            "Standard");
        if (alphaShader == null)
        {
            alphaShader = additiveShader;
        }
        if (alphaShader != null)
        {
            _smokeMaterialAlpha = new Material(alphaShader) { mainTexture = smokeTex };
        }
    }

    private static Shader FindFirstAvailableShader(params string[] shaderNames)
    {
        if (shaderNames == null)
        {
            return null;
        }

        foreach (var shaderName in shaderNames)
        {
            if (string.IsNullOrEmpty(shaderName))
            {
                continue;
            }

            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                return shader;
            }
        }

        return null;
    }

    private void CreateSystems()
    {
        CreateFlashSystem();
        CreateFireCoreSystem();
        CreateFireSubSystem();
        CreateSmokeSystem();
        CreateSparksSystem();
        CreateScreenOverlay();
        CreatePointLight();
    }

    // --- 爆発フェーズ ---
    private IEnumerator PlayExplosionOnly()
    {
        _overlayImage.color = new Color(1f, 1f, 1f, 0.0f);
        StartCoroutine(FadeImage(_overlayImage, new Color(1f, 1f, 1f, 0.0f), new Color(1f, 1f, 1f, 0.8f), 0.05f));

        _flash.Play();
        _fireCore.Play();
        _fireSub.Play();
        _sparks.Play();
        _smoke.Play();

        yield return new WaitForSeconds(0.1f);
        StartCoroutine(FadeImage(_overlayImage, _overlayImage.color, Color.clear, 0.4f));

        // 爆発後はそのまま待機（消滅させない）
    }

    // --- 動画フェーズ：GameManagerのButton押下後にこれを呼ぶ ---
    //public void SetupVideo(VideoClip clip)
    //{
    //    _targetClip = clip;
    //    if (_targetClip != null)
    //    {
    //        StartCoroutine(PlayVideoSequence());
    //    }
    //    else
    //    {
    //        IsVideoFinished = true; // 動画がない場合は即終了フラグ
    //    }
    //}

    //private IEnumerator PlayVideoSequence()
    //{
    //    // 1. 暗転
    //    Color darkColor = new Color(0.05f, 0.05f, 0.05f, 1.0f);
    //    yield return StartCoroutine(FadeImage(_overlayImage, Color.clear, darkColor, 0.8f));

    //    if (_targetClip != null && _videoPlayer != null)
    //    {
    //        _videoPlayer.clip = _targetClip;
    //        _videoPlayer.Prepare();

    //        // 準備完了まで待機
    //        while (!_videoPlayer.isPrepared) yield return null;

    //        _videoPlayer.Play();
    //        // 動画表示
    //        yield return StartCoroutine(FadeRawImage(_videoScreen, Color.clear, Color.white, 0.5f));

    //        // 動画の長さ分待機（少し短めにしてフェードアウトを被せる）
    //        float videoDuration = (float)_videoPlayer.length;
    //        if (videoDuration > 0.5f) yield return new WaitForSeconds(videoDuration - 0.5f);

    //        // 2. フェードアウト
    //        yield return StartCoroutine(FadeRawImage(_videoScreen, Color.white, Color.clear, 0.5f));
    //        yield return StartCoroutine(FadeImage(_overlayImage, _overlayImage.color, Color.clear, 0.8f));
    //    }

    //    IsVideoFinished = true; // GameManagerへ終了を通知
    //    Cleanup();
    //}

    // --- パーティクルエラー対策済みの生成メソッド (一部抜粋) ---

    private void CreateFireCoreSystem()
    {
        GameObject go = new GameObject("FireCore");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        _fireCore = go.AddComponent<ParticleSystem>();

        // ★エラー対策: mainに触る前に一度Stopする
        _fireCore.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _fireCore.main;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(30f, 60f);
        main.startSize = new ParticleSystem.MinMaxCurve(5f, 10f);
        main.loop = false;
        main.playOnAwake = false;

        // ... その他の設定（元のコードと同じ）
        var emission = _fireCore.emission;
        emission.rateOverTime = 150;
        var shape = _fireCore.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 3.0f;
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = _fireMaterialAdditive;
        renderer.renderMode = ParticleSystemRenderMode.Stretch;
        renderer.lengthScale = 3.0f;
    }

    // --- ユーティリティ系 ---

    private void CreateScreenOverlay()
    {
        GameObject canvasObj = new GameObject("ExplosionOverlayCanvas");
        _overlayCanvas = canvasObj.AddComponent<Canvas>();
        _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _overlayCanvas.sortingOrder = 999;

        // 常に一番手前に表示するための設定
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imgObj = new GameObject("OverlayImage");
        imgObj.transform.SetParent(canvasObj.transform, false);
        _overlayImage = imgObj.AddComponent<Image>();
        _overlayImage.color = Color.clear;
        _overlayImage.raycastTarget = false; // ボタンの邪魔をしない
        StretchToFill(imgObj.GetComponent<RectTransform>());
    }

    private void EnsureVideoObjects()
    {
        if (_overlayCanvas == null || _videoPlayer != null)
        {
            return;
        }

        GameObject vidObj = new GameObject("VideoScreen");
        vidObj.transform.SetParent(_overlayCanvas.transform, false);
        _videoScreen = vidObj.AddComponent<RawImage>();
        _videoScreen.color = Color.clear;
        _videoScreen.raycastTarget = false;
        StretchToFill(vidObj.GetComponent<RectTransform>());

        _videoPlayer = vidObj.AddComponent<VideoPlayer>();
        _videoPlayer.playOnAwake = false;
        _videoPlayer.renderMode = VideoRenderMode.RenderTexture;
        _videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

        _videoTexture = new RenderTexture(1920, 1080, 0);
        _videoPlayer.targetTexture = _videoTexture;
        _videoScreen.texture = _videoTexture;
    }

    private void DisposeOverlayResources()
    {
        if (_videoPlayer != null && _videoPlayer.isPlaying)
        {
            _videoPlayer.Stop();
        }

        if (_videoTexture != null)
        {
            _videoTexture.Release();
            Destroy(_videoTexture);
            _videoTexture = null;
        }

        if (_overlayCanvas != null)
        {
            Destroy(_overlayCanvas.gameObject);
            _overlayCanvas = null;
        }
    }

    private void Cleanup()
    {
        DisposeOverlayResources();
        Destroy(gameObject, 0.1f);
    }

    private void OnDestroy()
    {
        DisposeOverlayResources();
    }

    // --- 既存のテクスチャ生成・ライト・フェード処理はそのまま維持 ---
    // (CreateFlashSystem, CreateSmokeSystem, CreateSparksSystem 等は上記と同様に
    //  main設定前に Stop() を入れる形式で記述してください)

    private void StretchToFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private IEnumerator FadeImage(Image target, Color start, Color end, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.color = Color.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        target.color = end;
    }

    private IEnumerator FadeRawImage(RawImage target, Color start, Color end, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            target.color = Color.Lerp(start, end, elapsed / duration);
            yield return null;
        }
        target.color = end;
    }

    private void CreatePointLight()
    {
        GameObject lightObj = new GameObject("ExplosionLight");
        lightObj.transform.SetParent(transform, false);
        lightObj.transform.localPosition = new Vector3(0, 5, 0);
        
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.5f, 0.2f);
        light.range = 60f;
        light.intensity = 0f; 
        
        StartCoroutine(AnimateLight(light));
    }

    private IEnumerator AnimateLight(Light light)
    {
        float duration = 0.8f;
        float elapsed = 0f;
        float maxIntensity = 15f; 

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            light.intensity = Mathf.Lerp(maxIntensity, 0f, elapsed / duration);
            yield return null;
        }
        light.intensity = 0f;
    }

    private Texture2D CreateNoiseFireTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        float offset = Random.Range(0f, 100f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float noise = Mathf.PerlinNoise((x * 0.15f) + offset, (y * 0.15f) + offset);
                float alpha = Mathf.Clamp01(1.0f - (dist / radius) - (noise * 0.4f));
                alpha = Mathf.Pow(alpha, 1.5f);
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    private Texture2D CreateSoftCircleTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1.0f - (dist / radius));
                alpha = alpha * alpha; 
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------
    // パーティクル生成ロジック
    // ---------------------------------------------------------

    private void CreateFlashSystem()
    {
        GameObject go = new GameObject("Flash");
        go.transform.SetParent(transform, false);
        _flash = go.AddComponent<ParticleSystem>();
        
        var main = _flash.main;
        main.startLifetime = 0.2f; 
        main.startSpeed = 0; 
        main.startSize = 50f * scaleMultiplier; 
        main.startColor = new Color(1f, 0.9f, 0.7f, 1f); 
        main.loop = false; 
        main.playOnAwake = false;
        _flash.emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 1) });
        go.GetComponent<ParticleSystemRenderer>().material = _fireMaterialAdditive;
    }


    private void CreateFireSubSystem()
    {
        GameObject go = new GameObject("FireSub");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90, 0, 0);

        _fireSub = go.AddComponent<ParticleSystem>();
        var main = _fireSub.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 30f); 
        main.startSize = new ParticleSystem.MinMaxCurve(10f, 20f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.4f, 0f), new Color(1f, 0f, 0f));
        main.gravityModifier = -0.8f;
        main.loop = false; 
        main.playOnAwake = false;
        main.duration = 1.0f;

        var emission = _fireSub.emission;
        emission.rateOverTime = 100;

        var shape = _fireSub.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 30f;
        shape.radius = 6.0f * scaleMultiplier;

        var sz = _fireSub.sizeOverLifetime;
        sz.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 0.4f); curve.AddKey(0.3f, 1.0f); curve.AddKey(1.0f, 0.8f);
        sz.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

        var col = _fireSub.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.red, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        col.color = grad;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = _fireMaterialAdditive;
    }

    // ★強化した煙システム
    private void CreateSmokeSystem()
    {
        GameObject go = new GameObject("Smoke");
        go.transform.SetParent(transform, false);
        go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        
        _smoke = go.AddComponent<ParticleSystem>();
        var main = _smoke.main;
        main.duration = 8.0f; // 持続時間を延長
        main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 10f); // 長く残るように
        main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 20f);
        main.startSize = new ParticleSystem.MinMaxCurve(30f, 50f); // さらに巨大化
        // 濃いグレーからスタートして透明へ
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.1f, 0.1f, 0.1f, 0.9f), new Color(0f, 0f, 0f, 0.5f));
        main.gravityModifier = 0.05f; 
        main.maxParticles = 3000; // パーティクル数の上限を解放
        main.loop = false; 
        main.playOnAwake = false;

        var emission = _smoke.emission; 
        // バースト（爆発的な発生）を連発させる
        emission.SetBursts(new ParticleSystem.Burst[] { 
            new ParticleSystem.Burst(0.0f, 100), 
            new ParticleSystem.Burst(0.2f, 100),
            new ParticleSystem.Burst(0.5f, 100) 
        });
        emission.rateOverTime = 200; // 常時発生量も増やす
        
        var shape = _smoke.shape; 
        shape.shapeType = ParticleSystemShapeType.Hemisphere; 
        shape.radius = 15.0f * scaleMultiplier; // 発生範囲をさらに拡大

        // サイズの時間変化（モクモクと膨れ上がる）
        var sz = _smoke.sizeOverLifetime;
        sz.enabled = true;
        AnimationCurve curve = new AnimationCurve();
        curve.AddKey(0.0f, 0.5f);
        curve.AddKey(1.0f, 1.5f); // 最終的に1.5倍に膨張
        sz.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = _smokeMaterialAlpha; 
        renderer.sortMode = ParticleSystemSortMode.Distance; // 遠くのものから描画
    }

    private void CreateSparksSystem()
    {
        GameObject go = new GameObject("Sparks");
        go.transform.SetParent(transform, false);
        
        _sparks = go.AddComponent<ParticleSystem>();
        var main = _sparks.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(25f, 60f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.5f);
        main.startColor = Color.yellow; 
        main.gravityModifier = 1.0f; 
        main.loop = false; 
        main.playOnAwake = false;
        main.maxParticles = 1000;

        var emission = _sparks.emission; 
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 300) });
        
        var shape = _sparks.shape; 
        shape.shapeType = ParticleSystemShapeType.Sphere; 
        shape.radius = 3.0f;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.material = _fireMaterialAdditive; 
        renderer.trailMaterial = _fireMaterialAdditive; 
        
        var trails = _sparks.trails;
        trails.enabled = true;
        trails.ratio = 0.4f; 
        trails.lifetime = 0.25f;
        trails.widthOverTrail = 0.6f;
    }

    public void SetupVideo(string fileName)
    {
        EnsureVideoObjects();

        if (_videoPlayer == null || string.IsNullOrEmpty(fileName))
        {
            IsVideoFinished = true;
            return;
        }

        IsVideoFinished = false;

#if UNITY_WEBGL && !UNITY_EDITOR
        string path = Application.streamingAssetsPath + "/" + fileName;
#else
        string path = "file://" + Application.streamingAssetsPath + "/" + fileName;
#endif

        _videoPlayer.source = VideoSource.Url;
        _videoPlayer.url = path;

        StartCoroutine(PlayVideoSequence());
    }

    private IEnumerator PlayVideoSequence()
    {
        var se = SeController.GetOrFindInstance();
        if (se != null)
        {
            se.Play(preVideoSeKey);
        }

        // 1. 暗転
        Color darkColor = new Color(0.05f, 0.05f, 0.05f, 1.0f);
        yield return StartCoroutine(FadeImage(_overlayImage, Color.clear, darkColor, 0.8f));

        // ★修正: _targetClip ではなく _videoPlayer.url が空でないかで判定
        if (!string.IsNullOrEmpty(_videoPlayer.url) && _videoPlayer != null)
        {
            _videoPlayer.Prepare();

            // 準備完了まで待機
            float prepareTimeout = 12f;
            float elapsed = 0f;
            while (!_videoPlayer.isPrepared && elapsed < prepareTimeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!_videoPlayer.isPrepared)
            {
                Debug.LogWarning($"[YakumanExplosionEffect] Prepare timeout: {_videoPlayer.url}");
                IsVideoFinished = true;
                Cleanup();
                yield break;
            }

            _videoPlayer.Play();
            // 動画表示
            yield return StartCoroutine(FadeRawImage(_videoScreen, Color.clear, Color.white, 0.5f));

            // 動画の長さ分待機（少し短めにしてフェードアウトを被せる）
            float videoDuration = (float)_videoPlayer.length;
            if (videoDuration > 0.5f)
            {
                yield return new WaitForSeconds(videoDuration - 0.5f);
            }
            else
            {
                float waitTimeout = 45f;
                float waitElapsed = 0f;
                while (_videoPlayer.isPlaying && waitElapsed < waitTimeout)
                {
                    waitElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            // 2. フェードアウト
            yield return StartCoroutine(FadeRawImage(_videoScreen, Color.white, Color.clear, 0.5f));
            yield return StartCoroutine(FadeImage(_overlayImage, _overlayImage.color, Color.clear, 0.8f));
        }

        IsVideoFinished = true; // GameManagerへ終了を通知
        Cleanup();
    }
}
