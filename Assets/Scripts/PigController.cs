using System.Collections;
using System.Collections.Generic;
using Solo.MOST_IN_ONE;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// Pig walks the 4-segment conveyor path once (non-looping).
/// Each substep checks for same-color cubes in line-of-sight along the inward axis.
/// Exits via FinishPass (ammo remaining) or ExpireWithAnimation (ammo depleted mid-walk).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class PigController : MonoBehaviour, IStackable, IConvMovable
{
    // ------------------------------------------------------------------ state
    public int   ColorIndex      { get; private set; }
    public int   Ammo            { get; private set; }
    public bool  IsDispatched    { get; private set; }
    public int   LaneIndex       { get; private set; }
    public bool  IsHidden        { get; private set; }
    public float SpeedMultiplier { get; set; } = 1f;

    // ------------------------------------------------------------------ deps
    private GameConfig        _config;
    private AnimationConfig   _anim;
    private GameEventRegistry _events;
    private BoardManager      _board;
    private ConveyorPath      _path;
    private LaneManager       _manager;

    // ------------------------------------------------------------------ shooting state
    // Tracks which columns (world-space perpendicular coordinate) were fired
    // at during the current segment so each column gets exactly one shot.
    // Reset every time the pig reaches a new corner.
    private readonly HashSet<int> _firedColumns = new HashSet<int>();

    // ------------------------------------------------------------------ components
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private TextMeshPro    _ammoLabel;
    private Collider2D _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
    }

    // ------------------------------------------------------------------ init
    public void Initialize(int colorIndex, int ammo, int laneIndex, bool isHidden,
                           GameConfig config, AnimationConfig anim, GameEventRegistry events,
                           BoardManager board, ConveyorPath path, LaneManager manager)
    {
        ColorIndex = colorIndex;
        Ammo       = ammo;
        LaneIndex  = laneIndex;
        IsHidden   = isHidden;
        _config    = config;
        _anim      = anim;
        _events    = events;
        _board     = board;
        _path      = path;
        _manager   = manager;

        ApplyColor();
        UpdateAmmoLabel();
    }

    public void Reveal()
    {
        if (!IsHidden) return;
        IsHidden = false;
        ApplyColor();
        UpdateAmmoLabel();
        _events?.onPigRevealed.Raise(this);

        // Only reset scale — do NOT kill position tweens so SlideLaneUp's DOMove keeps running.
        DOTween.Kill("pigReveal");
        transform.localScale = Vector3.one;
        DOTween.Sequence()
            .Append(transform.DOScale(_anim.pigRevealScalePeak, _anim.pigRevealDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(1.0f,                     _anim.pigRevealDuration).SetEase(Ease.InQuad))
            .SetId("pigReveal");
    }

    // ------------------------------------------------------------------ IStackable
    /// <summary>Called by LaneManager when this pig slides to the front of its lane.</summary>
    public void OnMovedToFront()
    {
        if (IsHidden) Reveal();
    }

    // ------------------------------------------------------------------ click
    private void OnMouseDown()
    {
        if (IsDispatched) return;
        MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.LightImpact);
        _manager.RequestDispatch(this);
    }

    // ------------------------------------------------------------------ IConvMovable
    public bool IsOnConveyor => IsDispatched;

    public void OnConveyorSegmentEntered(int segmentIndex, Vector2 inwardDirection)
    {
        _firedColumns.Clear();
    }

    public void Dispatch()
    {
        if (IsDispatched) return;
        IsDispatched = true;
        _collider.enabled = false;

        Vector3 entry = _path.Waypoints[0].position;

        transform.DOKill();
        transform.localScale = Vector3.one;

        // Move to belt entry and punch scale in parallel, then start walking
        transform.DOMove(entry, _anim.pigDispatchMoveDuration).SetEase(Ease.InOutQuad);
        DOTween.Sequence()
            .Append(transform.DOScale(_anim.pigDispatchScalePeak, _anim.pigDispatchScaleDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(1.00f,                      _anim.pigDispatchScaleDuration).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                transform.position = entry;
                ApplyFacing(0);
                StartCoroutine(WalkPerimeter());
            });
    }

    public void ReturnToSlot(Vector3 slotPosition)
    {
        transform.DOKill();
        transform.localScale = Vector3.one;

        transform.DOMove(slotPosition, _anim.pigReturnMoveDuration).SetEase(Ease.InOutQuad)
            .OnComplete(() => transform.position = slotPosition);
        DOTween.Sequence()
            .Append(transform.DOScale(_anim.pigReturnScalePeak, _anim.pigReturnScaleDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(1.00f,                    _anim.pigReturnScaleDuration).SetEase(Ease.InQuad));
    }

    // ------------------------------------------------------------------ walk loop
    private IEnumerator WalkPerimeter()
    {
        var waypoints = _path.Waypoints;

        for (int seg = 0; seg < _path.SegmentCount; seg++)
        {
            int     nextCorner   = seg + 1;
            Vector3 destination  = waypoints[nextCorner].position;
            Vector2 inward       = _path.InwardDirection(seg);

            OnConveyorSegmentEntered(seg, inward);

            // Move toward next corner in substeps so the pig never skips
            // past a cube column between frames, regardless of belt speed.
            // Use the board's runtime step (not config cubeSize) — on large maps
            // the auto-scaled step is much smaller than the config value.
            float halfCube = _board.RuntimeStep * 0.5f;

            while (Vector3.Distance(transform.position, destination) > 0.02f)
            {
                float frameDist = Mathf.Min(
                    _config.beltSpeed * SpeedMultiplier * Time.deltaTime,
                    Vector3.Distance(transform.position, destination));

                int   substeps = Mathf.Max(1, Mathf.CeilToInt(frameDist / halfCube));
                float subDist  = frameDist / substeps;

                for (int s = 0; s < substeps; s++)
                {
                    transform.position = Vector3.MoveTowards(
                        transform.position, destination, subDist);

                    if (Ammo > 0)
                        TryFireInline(inward);
                }

                yield return null;
            }

            transform.position = destination;
            ApplyFacing(nextCorner);
        }

        FinishPass();
    }

    // ------------------------------------------------------------------ fire-on-move
    // Each perpendicular column gets exactly one shot per segment (tracked by _firedColumns).
    // Back-to-back cubes share a column, so only the nearest is hit per pass.
    private void TryFireInline(Vector2 inward)
    {
        List<IHittable> targets = _board.FindTargetsInLine(ColorIndex, transform.position, inward);
        if (targets.Count == 0) return;

        IHittable target = targets[0];

        // Column key: cube's lateral world coordinate at 1cm precision.
        // Using the cube's own position (not / step) avoids quantization
        // collisions when the board isn't perfectly aligned to the grid step.
        Vector2 perp   = new Vector2(-inward.y, inward.x);
        int     colKey = Mathf.RoundToInt(Vector2.Dot((Vector2)target.Position, perp) * 100f);

        if (_firedColumns.Contains(colKey)) return;

        FireAt(target);
        _firedColumns.Add(colKey);
        Ammo--;
        UpdateAmmoLabel();

        if (Ammo <= 0)
        {
            StopAllCoroutines();
            _firedColumns.Clear();
            ExpireWithAnimation();
        }
    }

    private void ExpireWithAnimation()
    {
        _events.onPigExpired.Raise(this);
        transform.DOKill();
        transform.DOScale(Vector3.zero, _anim.pigExpireDuration)
            .SetEase(Ease.InBack)
            .OnComplete(() => Destroy(gameObject));
    }

    private void FireAt(IHittable target)
    {
        var proj = ProjectilePool.Instance.Get();
        proj.transform.position = transform.position;
        proj.Initialize(target, _config.projectileSpeed, ColorIndex);

        transform.DOKill();
        transform.localScale = Vector3.one;
        DOTween.Sequence()
            .Append(transform.DOScale(_anim.pigFireScalePeak, _anim.pigFireScaleDuration).SetEase(Ease.OutQuad))
            .Append(transform.DOScale(1.00f,                  _anim.pigFireScaleDuration).SetEase(Ease.InQuad));
    }

    // ------------------------------------------------------------------ finish
    private void FinishPass()
    {
        transform.DOKill();
        IsDispatched = false;
        _firedColumns.Clear();
        _collider.enabled = true;

        // Label world rotation was counter-rotated during the walk; reset its
        // local rotation so it reads correctly once the pig is at identity rotation.
        if (_ammoLabel != null)
            _ammoLabel.transform.localRotation = Quaternion.identity;

        if (Ammo <= 0)
        {
            ExpireWithAnimation();
        }
        else
        {
            if (!_manager.IsFrenzy)
                MOST_HapticFeedback.Generate(MOST_HapticFeedback.HapticTypes.Selection);
            _events.onPigFinishedPass.Raise(this);
        }
    }

    // ------------------------------------------------------------------ facing
    private void ApplyFacing(int waypointIndex)
    {
        float angle = _path.SegmentFacingAngle(waypointIndex);
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // Keep the ammo label upright regardless of pig rotation
        if (_ammoLabel != null)
            _ammoLabel.transform.rotation = Quaternion.identity;
    }

    // ------------------------------------------------------------------ visuals
    private void ApplyColor()
    {
        if (_renderer == null || _config == null) return;
        _renderer.color = IsHidden
            ? Color.gray
            : _config.pigColors[Mathf.Clamp(ColorIndex, 0, _config.pigColors.Length - 1)];
    }

    private void UpdateAmmoLabel()
    {
        if (_ammoLabel != null)
            _ammoLabel.text = IsHidden ? "?" : Ammo.ToString();
    }
}
