using System.Collections.Generic;
using ExitGames.Client.Photon;

public static class TileSerialization
{
    public static void RegisterCustomTypes()
    {
        PhotonPeer.RegisterType(typeof(Tiles), 100, SerializeTiles, DeserializeTiles);
        PhotonPeer.RegisterType(typeof(List<Tiles>), 101, SerializeListOfTiles, DeserializeListOfTiles);
    }

    private static short SerializeTiles(StreamBuffer outStream, object customObject)
    {
        Tiles tile = (Tiles)customObject;
        outStream.WriteByte((byte)tile.color);
        outStream.WriteByte((byte)tile.number);
        outStream.WriteByte((byte)tile.type);
        return 0; // Success
    }

    private static object DeserializeTiles(StreamBuffer inStream, short length)
    {
        TileColor color = (TileColor)inStream.ReadByte();
        int number = inStream.ReadByte();
        TileType type = (TileType)inStream.ReadByte();
        return new Tiles(color, number, type);
    }

    private static short SerializeListOfTiles(StreamBuffer outStream, object customObject)
    {
        List<Tiles> list = (List<Tiles>)customObject;
        outStream.WriteByte((byte)list.Count); // Write list count

        foreach (var tile in list)
        {
            SerializeTiles(outStream, tile); // Serialize each Tiles object
        }

        return 0; // Success
    }

    private static object DeserializeListOfTiles(StreamBuffer inStream, short length)
    {
        int count = inStream.ReadByte(); // Read list count
        List<Tiles> list = new List<Tiles>(count);

        for (int i = 0; i < count; i++)
        {
            Tiles tile = (Tiles)DeserializeTiles(inStream, 0); // Deserialize each Tiles object
            list.Add(tile);
        }

        return list;
    }
}