using System;

[System.Serializable]
public class TileDataInfo
{
    public TileColor color;
    public int number;
    public TileType type;

    public TileDataInfo(TileColor color, int number, TileType type)
    {
        this.color = color;
        this.number = number;
        this.type = type;
    }

    public TileDataInfo(Tiles tile)
    {
        this.color = tile.color;
        this.number = tile.number;
        this.type = tile.type;
    }

    public Tiles ToTile()
    {
        return new Tiles(color, number, type);
    }
}
