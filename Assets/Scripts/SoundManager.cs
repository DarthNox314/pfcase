using UnityEngine;

/// <summary>
/// Plays sound effects in response to game events.
/// Uses a fixed pool of AudioSources (round-robin) to cap simultaneous
/// identical sounds and avoid per-hit allocations.
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameEventRegistry _events;

    [Header("Clips")]
    [SerializeField] private AudioClip _cubeHitClip;
    [SerializeField] private AudioClip _keyUnlockClip;
    [SerializeField] private AudioClip _levelWinClip;
    [SerializeField] private AudioClip _levelLoseClip;

    [Header("Settings")]
    [Tooltip("Max overlapping cube-hit sounds. Beyond this the oldest is evicted.")]
    [SerializeField] [Range(1, 8)] private int _maxSimultaneous = 3;

    [Tooltip("Random pitch range around 1.0 to avoid mechanical repetition.")]
    [SerializeField] private float _pitchVariance = 0.05f;

    [SerializeField] [Range(0f, 1f)] private float _volume = 1f;

    // ------------------------------------------------------------------ pool
    private AudioSource[] _sources;
    private int           _index;
    private AudioSource   _keyUnlockSource;
    private AudioSource   _levelEndSource;

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        _sources = new AudioSource[_maxSimultaneous];
        for (int i = 0; i < _maxSimultaneous; i++)
        {
            _sources[i]             = gameObject.AddComponent<AudioSource>();
            _sources[i].playOnAwake = false;
        }

        _keyUnlockSource             = gameObject.AddComponent<AudioSource>();
        _keyUnlockSource.playOnAwake = false;

        _levelEndSource             = gameObject.AddComponent<AudioSource>();
        _levelEndSource.playOnAwake = false;
    }

    private void OnEnable()
    {
        _events.onCubeHit.Subscribe(OnCubeHit);
        _events.onLockUnlocked.Subscribe(OnLockUnlocked);
        _events.onLevelComplete.Subscribe(OnLevelComplete);
        _events.onLevelFailed.Subscribe(OnLevelFailed);
    }

    private void OnDisable()
    {
        _events.onCubeHit.Unsubscribe(OnCubeHit);
        _events.onLockUnlocked.Unsubscribe(OnLockUnlocked);
        _events.onLevelComplete.Unsubscribe(OnLevelComplete);
        _events.onLevelFailed.Unsubscribe(OnLevelFailed);
    }

    // ------------------------------------------------------------------ handlers
    private void OnCubeHit(IHittable _)
    {
        if (_cubeHitClip == null) return;

        var source       = _sources[_index];
        source.pitch     = 1f + Random.Range(-_pitchVariance, _pitchVariance);
        source.PlayOneShot(_cubeHitClip, _volume);

        _index = (_index + 1) % _maxSimultaneous;
    }

    private void OnLockUnlocked(IStackable _)
    {
        if (_keyUnlockClip == null) return;
        _keyUnlockSource.PlayOneShot(_keyUnlockClip, _volume);
    }

    private void OnLevelComplete()
    {
        if (_levelWinClip == null) return;
        _levelEndSource.Stop();
        _levelEndSource.PlayOneShot(_levelWinClip, _volume);
    }

    private void OnLevelFailed()
    {
        if (_levelLoseClip == null) return;
        _levelEndSource.Stop();
        _levelEndSource.PlayOneShot(_levelLoseClip, _volume);
    }
}
