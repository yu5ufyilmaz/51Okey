using UnityEngine;

[CreateAssetMenu(fileName = "TileData", menuName = "ScriptableObjects/TileData", order = 1)]
public class TileData : ScriptableObject
{
    public string color; // Tile color ("Red", "Black", "Blue", "Yellow" or empty for Joker)
    public int number;   // Tile number (1-13 or 0 for Joker)
    public Sprite tileSprite; // Sprite for the tile
    public bool isJoker; // Is this tile a Joker?

    public bool IsJoker()
    {
        return isJoker;
    }
}