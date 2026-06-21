using UnityEngine;

/// <summary>
/// Global game configuration. Create one instance via Assets > Create > PixelFlow > GameConfig.
/// Tweak values here without touching any gameplay code.
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "PixelFlow/GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Conveyor Belt")]
    [Tooltip("How many pigs can ride the belt simultaneously.")]
    public int beltCapacity = 3;

    [Tooltip("Speed at which pigs travel along the belt (units/sec).")]
    public float beltSpeed = 2f;

    [Tooltip("Number of waiting (reserve) slots beside the belt.")]
    public int waitingSlotCount = 5;

    [Header("Shooting")]
    [Tooltip("Projectile travel speed (units/sec).")]
    public float projectileSpeed = 8f;

    [Header("Board")]
    [Tooltip("Pixel cube size in world units.")]
    public float cubeSize = 0.5f;

    [Tooltip("Gap between cubes in world units.")]
    public float cubeGap = 0.05f;

    [Header("Colors")]
    [Tooltip("All possible pig/cube colors in the game. Order matters for IDs.")]
    public Color[] pigColors = new Color[]
    {
        new Color(1f,   0.33f, 0.33f), // Red
        new Color(0.33f,0.78f, 0.33f), // Green
        new Color(0.33f,0.55f, 1f),    // Blue
        new Color(1f,   0.85f, 0.2f),  // Yellow
        new Color(0.75f,0.33f, 1f),    // Purple
    };
}
