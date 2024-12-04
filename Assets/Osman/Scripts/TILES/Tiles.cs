using UnityEngine;

public enum TileColor { Red, Blue, Green, Yellow }
public enum TileType { Number, Special }

[System.Serializable]
public class Tiles
{
    public TileColor color;
    public int number;
    public TileType type;

    public Tiles(TileColor color, int number, TileType type)
    {
        this.color = color;
        this.number = number;
        this.type = type;
    }

    public TileDataInfo ToTileDataInfo()
    {
        return new TileDataInfo(color, number, type);
    }
}

