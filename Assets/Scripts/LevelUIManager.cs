using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Shows win/lose panels and handles next-level and restart.
///
/// SETUP:
///   _winPanel  — GameObject with a "Next Level" button
///   _losePanel — GameObject with a "Restart" button
///   _levels    — ordered array of all LevelData assets
///   Set _currentIndex to the index of the level loaded in this scene.
/// </summary>
public class LevelUIManager : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private GameEventRegistry _events;
    [SerializeField] private LevelManager      _levelManager;

    [Header("Panels")]
    [SerializeField] private GameObject _winPanel;
    [SerializeField] private GameObject _losePanel;
    [SerializeField] private Image      _backdrop;

    [Header("Animation")]
    [SerializeField] private float _slideDistance = 300f;
    [SerializeField] private float _slideDuration  = 0.45f;

    [Header("Levels")]
    [SerializeField] private LevelData[] _levels;
    [SerializeField] private int         _currentIndex;
    [SerializeField] private TMP_Text    _levelNameText;

    // ------------------------------------------------------------------ lifecycle
    private void Awake()
    {
        HideAll();
        UpdateLevelName();
    }

    private void OnEnable()
    {
        _events.onLevelComplete.Subscribe(OnLevelComplete);
        _events.onLevelFailed.Subscribe(OnLevelFailed);
    }

    private void OnDisable()
    {
        _events.onLevelComplete.Unsubscribe(OnLevelComplete);
        _events.onLevelFailed.Unsubscribe(OnLevelFailed);
    }

    // ------------------------------------------------------------------ event handlers
    private void OnLevelComplete()
    {
        ShowBackdrop(true);
        if (_winPanel == null) return;

        bool hasNext = _levels != null && _currentIndex + 1 < _levels.Length;
        var nextBtn  = _winPanel.GetComponentInChildren<Button>();
        if (nextBtn != null) nextBtn.gameObject.SetActive(hasNext);

        SlideIn(_winPanel);
    }

    private void OnLevelFailed()
    {
        ShowBackdrop(true);
        if (_losePanel != null) SlideIn(_losePanel);
    }

    // ------------------------------------------------------------------ button callbacks (wire in Inspector)
    public void OnNextPressed()
    {
        int next = _currentIndex + 1;
        if (_levels == null || next >= _levels.Length) return;

        _currentIndex = next;
        HideAll();
        UpdateLevelName();
        _levelManager.StartLevel(_levels[next]);
    }

    public void OnRestartPressed()
    {
        if (_levels == null || _currentIndex >= _levels.Length) return;

        HideAll();
        _levelManager.StartLevel(_levels[_currentIndex]);
    }

    // ------------------------------------------------------------------ animation
    private void SlideIn(GameObject panel)
    {
        var rt = panel.GetComponent<RectTransform>();
        if (rt == null) return;

        panel.SetActive(true);
        rt.DOKill();

        Vector2 target = rt.anchoredPosition;
        rt.anchoredPosition = target + Vector2.down * _slideDistance;
        rt.DOAnchorPos(target, _slideDuration).SetEase(Ease.OutBack).SetUpdate(true);
    }

    // ------------------------------------------------------------------ helpers
    private void ShowBackdrop(bool show)
    {
        if (_backdrop == null) return;
        _backdrop.enabled       = show;
        _backdrop.raycastTarget = show;
    }

    private void HideAll()
    {
        if (_winPanel  != null) { _winPanel.GetComponent<RectTransform>()?.DOKill();  _winPanel.SetActive(false); }
        if (_losePanel != null) { _losePanel.GetComponent<RectTransform>()?.DOKill(); _losePanel.SetActive(false); }
        ShowBackdrop(false);
    }

    private void UpdateLevelName()
    {
        if (_levelNameText == null || _levels == null || _currentIndex >= _levels.Length) return;
        _levelNameText.text = _levels[_currentIndex].levelTitle;
    }
}
