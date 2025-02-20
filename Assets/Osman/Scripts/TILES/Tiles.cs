using UnityEngine;

public enum TileColor { red, blue, black, yellow }
public enum TileType { Number, Joker, FakeJoker }
public enum TilePerType {None ,Color ,Number ,Pair }

[System.Serializable]
public class Tiles
{
    public TileColor color;
    public TilePerType perType;
    public int number;
    public TileType type;

    public Tiles(TileColor color, int number, TileType type)
    {
        this.color = color;
        this.number = number;
        this.type = type;
    }

}

