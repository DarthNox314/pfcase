using UnityEngine;

[CreateAssetMenu(fileName = "Level_01", menuName = "PixelFlow/LevelData")]
public class LevelData : ScriptableObject
{
    [System.Serializable]
    public struct CubeEntry
    {
        [Tooltip("Grid column (X).")]
        public int col;
        [Tooltip("Grid row (Y).")]
        public int row;
        [Tooltip("Color index into GameConfig.pigColors.")]
        public int colorIndex;
        [Tooltip("How many hits this cube requires to clear.")]
        public int hitPoints;
    }

    public enum LaneItemType { Pig, Lock }

    [System.Serializable]
    public struct PigEntry
    {
        [Tooltip("Pig or Lock.")]
        public LaneItemType itemType;
        [Tooltip("Color index into GameConfig.pigColors. (Pigs only)")]
        public int colorIndex;
        [Tooltip("How many balls this pig carries before leaving. (Pigs only)")]
        public int ammo;
        [Tooltip("Which lane this entry belongs to (0–3).")]
        [Range(0, 3)]
        public int laneIndex;
        [Tooltip("If true, pig appears gray with '?' until it reaches the front of its lane. (Pigs only)")]
        public bool isHidden;
    }

    [System.Serializable]
    public struct KeyEntry
    {
        [Tooltip("Grid column of the top-left cell of this key's 2x2 footprint.")]
        public int col;
        [Tooltip("Grid row of the top-left cell of this key's 2x2 footprint.")]
        public int row;
    }

    [Header("Identity")]
    public int    levelNumber;
    public string levelTitle;

    [Header("Board")]
    public int         boardColumns = 8;
    public int         boardRows    = 8;
    public CubeEntry[] cubes;

    [Header("Pig Lanes")]
    [Tooltip("All lane items (pigs and locks) for this level.")]
    public PigEntry[] pigQueue;

    [Header("Board Keys")]
    [Tooltip("2x2 key objects placed on the board. Each unlocks one lock when its adjacent pixels are cleared.")]
    public KeyEntry[] keys;
}
