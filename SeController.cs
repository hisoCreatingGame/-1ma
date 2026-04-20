using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SeKeys
{
    public const string StartToLobbyTransition = "start_to_lobby_transition";
    public const string LobbyYakumanIconTouch = "lobby_yakuman_icon_touch";
    public const string LobbyFaceZoomTouch = "lobby_face_zoom_touch";
    public const string LobbyToGameTransition = "lobby_to_game_transition";
    public const string LobbyBackButton = "lobby_back_button";
    public const string GameNormalTsumoImpact = "game_normal_tsumo_impact";
    public const string GameYakumanExplosion = "game_yakuman_explosion";
    public const string GameOpenSettings = "game_open_settings";
    public const string GameYakumanPreVideoImage = "game_yakuman_pre_video_image";
    public const string GameRoundStartEastKyoku = "game_round_start_east_kyoku";
    public const string GameScoreSlot = "game_score_slot";
}

public class SeController : MonoBehaviour
{
    [Serializable]
    public class SeEntry
    {
        public string key;
        public List<AudioClip> clips = new List<AudioClip>();
        [Range(0f, 1f)] public float volume = 1f;
        public bool random = true;
        public bool avoidImmediateRepeat = true;

        [NonSerialized] public int lastIndex = -1;
    }

    public static SeController Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource seSource;
    [SerializeField] private bool dontDestroyOnLoad = true;
    [SerializeField] private bool forceUnmuteSource = true;
    [SerializeField] private bool stopSeOnSceneChange = true;

    [Header("SE Table")]
    [SerializeField] private List<SeEntry> entries = new List<SeEntry>();

    [Header("Quick Assign (No key typing)")]
    [SerializeField] private List<AudioClip> startToLobbyTransitionClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> lobbyYakumanIconTouchClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> lobbyFaceZoomTouchClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> lobbyToGameTransitionClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> lobbyBackButtonClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> gameNormalTsumoImpactClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> gameYakumanExplosionClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> gameOpenSettingsClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> gameYakumanPreVideoImageClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> gameRoundStartEastKyokuClips = new List<AudioClip>();
    [SerializeField] private List<AudioClip> gameScoreSlotClips = new List<AudioClip>();

    private readonly Dictionary<string, SeEntry> entryMap = new Dictionary<string, SeEntry>(StringComparer.Ordinal);

    public static SeController GetOrFindInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        Instance = FindAnyObjectByType<SeController>();
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

        if (seSource == null)
        {
            var sourceObj = new GameObject("SEAudioSource");
            sourceObj.transform.SetParent(transform, false);
            seSource = sourceObj.AddComponent<AudioSource>();
        }

        if (forceUnmuteSource && seSource != null)
        {
            seSource.mute = false;
            seSource.volume = 1f;
        }

        seSource.playOnAwake = false;
        seSource.loop = false;
        seSource.spatialBlend = 0f;

        RebuildMap();
    }

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnValidate()
    {
        RebuildMap();
    }

    public float Play(string key)
    {
        if (string.IsNullOrEmpty(key) || seSource == null)
        {
            return 0f;
        }

        if (!entryMap.TryGetValue(key, out var entry))
        {
            RebuildMap();
            if (!entryMap.TryGetValue(key, out entry))
            {
                return PlayQuickAssigned(key);
            }
        }

        AudioClip clip = PickClip(entry);
        if (clip == null)
        {
            return PlayQuickAssigned(key);
        }

        seSource.PlayOneShot(clip, Mathf.Clamp01(entry.volume));
        return clip.length;
    }

    public IEnumerator PlayAndWaitRoutine(string key, float minimumWaitSeconds = 0f)
    {
        float clipLength = Play(key);
        float wait = Mathf.Max(clipLength, Mathf.Max(0f, minimumWaitSeconds));
        if (wait > 0f)
        {
            yield return new WaitForSecondsRealtime(wait);
        }
    }

    public static IEnumerator PlayAndWait(string key, float minimumWaitSeconds = 0f)
    {
        var se = GetOrFindInstance();
        if (se == null)
        {
            if (minimumWaitSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(minimumWaitSeconds);
            }

            yield break;
        }

        yield return se.PlayAndWaitRoutine(key, minimumWaitSeconds);
    }

    private void RebuildMap()
    {
        entryMap.Clear();

        if (entries == null)
        {
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            SeEntry entry = entries[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.key))
            {
                continue;
            }

            string key = entry.key.Trim();
            if (!entryMap.ContainsKey(key))
            {
                entryMap.Add(key, entry);
            }
        }
    }

    private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (!stopSeOnSceneChange || seSource == null)
        {
            return;
        }

        seSource.Stop();
    }

    private static AudioClip PickClip(SeEntry entry)
    {
        if (entry == null || entry.clips == null || entry.clips.Count == 0)
        {
            return null;
        }

        List<int> validIndices = null;
        for (int i = 0; i < entry.clips.Count; i++)
        {
            if (entry.clips[i] == null)
            {
                continue;
            }

            if (validIndices == null)
            {
                validIndices = new List<int>();
            }
            validIndices.Add(i);
        }

        if (validIndices == null || validIndices.Count == 0)
        {
            return null;
        }

        int picked;
        if (!entry.random || validIndices.Count == 1)
        {
            picked = validIndices[0];
        }
        else
        {
            int selected = validIndices[UnityEngine.Random.Range(0, validIndices.Count)];
            if (entry.avoidImmediateRepeat && validIndices.Count > 1 && selected == entry.lastIndex)
            {
                int next = (validIndices.IndexOf(selected) + 1) % validIndices.Count;
                selected = validIndices[next];
            }
            picked = selected;
        }

        entry.lastIndex = picked;
        return entry.clips[picked];
    }

    private float PlayQuickAssigned(string key)
    {
        List<AudioClip> clips = GetQuickAssignedClips(key);
        if (clips == null || clips.Count == 0)
        {
            Debug.LogWarning($"[SeController] SE not found or empty: {key}");
            return 0f;
        }

        AudioClip clip = PickClip(clips, -1, true, true);
        if (clip == null)
        {
            Debug.LogWarning($"[SeController] SE clip list has no valid clip: {key}");
            return 0f;
        }

        seSource.PlayOneShot(clip, 1f);
        return clip.length;
    }

    private List<AudioClip> GetQuickAssignedClips(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        string normalized = key.Trim();
        if (normalized.Equals(SeKeys.StartToLobbyTransition, StringComparison.OrdinalIgnoreCase)) return startToLobbyTransitionClips;
        if (normalized.Equals(SeKeys.LobbyYakumanIconTouch, StringComparison.OrdinalIgnoreCase)) return lobbyYakumanIconTouchClips;
        if (normalized.Equals(SeKeys.LobbyFaceZoomTouch, StringComparison.OrdinalIgnoreCase)) return lobbyFaceZoomTouchClips;
        if (normalized.Equals(SeKeys.LobbyToGameTransition, StringComparison.OrdinalIgnoreCase)) return lobbyToGameTransitionClips;
        if (normalized.Equals(SeKeys.LobbyBackButton, StringComparison.OrdinalIgnoreCase)) return lobbyBackButtonClips;
        if (normalized.Equals(SeKeys.GameNormalTsumoImpact, StringComparison.OrdinalIgnoreCase)) return gameNormalTsumoImpactClips;
        if (normalized.Equals(SeKeys.GameYakumanExplosion, StringComparison.OrdinalIgnoreCase)) return gameYakumanExplosionClips;
        if (normalized.Equals(SeKeys.GameOpenSettings, StringComparison.OrdinalIgnoreCase)) return gameOpenSettingsClips;
        if (normalized.Equals(SeKeys.GameYakumanPreVideoImage, StringComparison.OrdinalIgnoreCase)) return gameYakumanPreVideoImageClips;
        if (normalized.Equals(SeKeys.GameRoundStartEastKyoku, StringComparison.OrdinalIgnoreCase)) return gameRoundStartEastKyokuClips;
        if (normalized.Equals(SeKeys.GameScoreSlot, StringComparison.OrdinalIgnoreCase)) return gameScoreSlotClips;
        return null;
    }

    private static AudioClip PickClip(List<AudioClip> clips, int lastIndex, bool random, bool avoidImmediateRepeat)
    {
        if (clips == null || clips.Count == 0)
        {
            return null;
        }

        List<int> validIndices = null;
        for (int i = 0; i < clips.Count; i++)
        {
            if (clips[i] == null)
            {
                continue;
            }

            if (validIndices == null)
            {
                validIndices = new List<int>();
            }
            validIndices.Add(i);
        }

        if (validIndices == null || validIndices.Count == 0)
        {
            return null;
        }

        int picked;
        if (!random || validIndices.Count == 1)
        {
            picked = validIndices[0];
        }
        else
        {
            int selected = validIndices[UnityEngine.Random.Range(0, validIndices.Count)];
            if (avoidImmediateRepeat && validIndices.Count > 1 && selected == lastIndex)
            {
                int next = (validIndices.IndexOf(selected) + 1) % validIndices.Count;
                selected = validIndices[next];
            }
            picked = selected;
        }

        return clips[picked];
    }

    [ContextMenu("SE Test: Start -> Lobby")]
    private void TestStartToLobbySe()
    {
        Play(SeKeys.StartToLobbyTransition);
    }
}
