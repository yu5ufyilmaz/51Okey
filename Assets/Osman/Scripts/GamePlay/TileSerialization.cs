using System.Collections.Generic;
using System.IO;
using ExitGames.Client.Photon;
using Photon.Pun;
public static class TileSerialization
{
    public static void RegisterCustomTypes()
    {
        PhotonPeer.RegisterType(typeof(Tiles), 100, SerializeTiles, DeserializeTiles);
    }
    private static short SerializeTiles(StreamBuffer outStream, object customObject)
    {
        Tiles tile = (Tiles)customObject;
        outStream.WriteByte((byte)tile.color);
        outStream.WriteByte((byte)tile.number);
        outStream.WriteByte((byte)tile.type);
        return 0; // Başarı durumu
    }

    private static object DeserializeTiles(StreamBuffer inStream, short length)
    {
        TileColor color = (TileColor)inStream.ReadByte();
        int number = inStream.ReadByte();
        TileType type = (TileType)inStream.ReadByte();
        return new Tiles(color, number, type);
    }

    
}
