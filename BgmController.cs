using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BgmController : MonoBehaviour
{
    public static BgmController Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Header("BGM Clips")]
    [SerializeField] private AudioClip startBgm;
    [SerializeField] private AudioClip lobbyBgm;
    [SerializeField] private AudioClip gameStartBgm;
    [SerializeField] private List<AudioClip> riichiBgms = new List<AudioClip>();
    [SerializeField] private AudioClip endKyokuBgm;

    [Header("Auto Scene Routing")]
    [SerializeField] private bool autoSwitchByScene = true;
    [SerializeField] private string startSceneName = "StartScene";
    [SerializeField] private string lobbySceneName = "LobbyScene";
    [SerializeField] private string gameSceneName = "GameScene";

    private string currentStateKey;
    private int lastRiichiIndex = -1;
    private bool isSuspendedByOverlay;

    public static BgmController GetOrFindInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindAnyObjectByType<BgmController>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (bgmSource == null)
        {
            bgmSource = GetComponent<AudioSource>();
        }

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
        }

        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        if (autoSwitchByScene)
        {
            RouteSceneBgm(SceneManager.GetActiveScene().name);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!autoSwitchByScene)
        {
            return;
        }

        RouteSceneBgm(scene.name);
    }

    private void RouteSceneBgm(string sceneName)
    {
        if (sceneName == startSceneName)
        {
            PlayStartBgm();
        }
        else if (sceneName == lobbySceneName)
        {
            PlayLobbyBgm();
        }
        else if (sceneName == gameSceneName)
        {
            PlayGameStartBgm();
        }
    }

    public void PlayStartBgm()
    {
        PlayStateBgm("start", startBgm, true);
    }

    public void PlayLobbyBgm()
    {
        PlayStateBgm("lobby", lobbyBgm, true);
    }

    public void PlayGameStartBgm()
    {
        PlayStateBgm("game", gameStartBgm, true);
    }

    public void PlayRiichiBgmRandom()
    {
        if (riichiBgms == null || riichiBgms.Count == 0)
        {
            return;
        }

        List<int> valid = new List<int>();
        for (int i = 0; i < riichiBgms.Count; i++)
        {
            if (riichiBgms[i] != null)
            {
                valid.Add(i);
            }
        }

        if (valid.Count == 0)
        {
            return;
        }

        int selectedIndex = valid[Random.Range(0, valid.Count)];
        if (valid.Count > 1 && selectedIndex == lastRiichiIndex)
        {
            selectedIndex = valid[Random.Range(0, valid.Count)];
        }

        lastRiichiIndex = selectedIndex;
        PlayStateBgm($"riichi_{selectedIndex}", riichiBgms[selectedIndex], true);
    }

    public void PlayEndKyokuBgm()
    {
        PlayStateBgm("end", endKyokuBgm, true);
    }

    public void StopBgm()
    {
        if (bgmSource == null)
        {
            return;
        }

        bgmSource.Stop();
        currentStateKey = string.Empty;
    }

    public void StopBgmForOverlay()
    {
        if (bgmSource == null)
        {
            return;
        }

        if (bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }

        isSuspendedByOverlay = true;
    }

    public void ResumeBgmIfSuspended()
    {
        if (bgmSource == null || !isSuspendedByOverlay)
        {
            return;
        }

        if (bgmSource.clip != null && !bgmSource.isPlaying)
        {
            bgmSource.Play();
        }

        isSuspendedByOverlay = false;
    }

    private void PlayStateBgm(string stateKey, AudioClip clip, bool loop)
    {
        if (bgmSource == null || clip == null)
        {
            return;
        }

        if (currentStateKey == stateKey && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.loop = loop;

        if (bgmSource.clip != clip)
        {
            bgmSource.clip = clip;
            bgmSource.Play();
        }
        else if (!bgmSource.isPlaying)
        {
            bgmSource.Play();
        }

        currentStateKey = stateKey;
    }
}
