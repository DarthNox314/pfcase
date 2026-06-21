using UnityEngine;

/// <summary>
/// Central ScriptableObject for all animation timings and scale peaks.
/// Create via:  Assets > Create > PixelFlow > AnimationConfig
/// Assign to every MonoBehaviour that runs tweens.
/// </summary>
[CreateAssetMenu(fileName = "AnimationConfig", menuName = "PixelFlow/AnimationConfig")]
public class AnimationConfig : ScriptableObject
{
    [Header("Pig — Reveal")]
    public float pigRevealScalePeak  = 1.30f;
    public float pigRevealDuration   = 0.10f;   // per half (up + down)

    [Header("Pig — Dispatch")]
    public float pigDispatchMoveDuration  = 0.20f;
    public float pigDispatchScalePeak     = 1.25f;
    public float pigDispatchScaleDuration = 0.10f;

    [Header("Pig — Fire Recoil")]
    public float pigFireScalePeak     = 1.25f;
    public float pigFireScaleDuration = 0.10f;

    [Header("Pig — Return to Slot")]
    public float pigReturnMoveDuration  = 0.20f;
    public float pigReturnScalePeak     = 1.25f;
    public float pigReturnScaleDuration = 0.10f;

    [Header("Pig — Expire (out of ammo)")]
    public float pigExpireDuration = 0.25f;

    [Header("Lane — Slide Up")]
    public float laneSlideUpDuration = 0.20f;

    [Header("Cube — Destroy")]
    public float cubeDestroyScalePeak    = 1.30f;
    public float cubeDestroyScaleUpDur   = 0.05f;
    public float cubeDestroyScaleDownDur = 0.05f;

    [Header("Cube — Hit Feedback")]
    public float cubeHitScalePeak  = 1.55f;
    public float cubeHitDuration   = 0.25f;

    [Header("Lock — Destroy")]
    public float lockDestroyScalePeak    = 1.40f;
    public float lockDestroyScaleUpDur   = 0.10f;
    public float lockDestroyScaleDownDur = 0.10f;
    public float lockDestroyHoldDur      = 0.04f;

    [Header("Key — Unlock Pulse")]
    public float keyUnlockPunchStrength = 0.25f;
    public float keyUnlockPunchDuration = 0.25f;

    [Header("Key — Destroy")]
    public float keyDestroyScalePeak    = 1.40f;
    public float keyDestroyScaleUpDur   = 0.10f;
    public float keyDestroyScaleDownDur = 0.10f;
    public float keyDestroyHoldDur      = 0.04f;

    [Header("Key — Fly to Lock")]
    public float keyFlyDelay    = 0.15f;
    public float keyFlyDuration = 0.50f;
}
