using UnityEngine;

public enum TileColor
{
    red,
    blue,
    black,
    yellow,
}

public enum TileType
{
    Number,
    Joker,
    FakeJoker,
}

[System.Serializable]
public class Tiles
{
    public string id; // YENİ: Benzersiz Kimlik
    public TileColor color;
    public int number;
    public TileType type;

    public Tiles(TileColor color, int number, TileType type)
    {
        this.id = System.Guid.NewGuid().ToString();
        this.color = color;
        this.number = number;
        this.type = type;
    }

    public Tiles()
    {
        // this.id = System.Guid.NewGuid().ToString();
    }
}
