using UnityEngine;

public enum TileColor { red, blue, black, yellow }
public enum TileType { Number, Joker, FakeJoker }

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

}

