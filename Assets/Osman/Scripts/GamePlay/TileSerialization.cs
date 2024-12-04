using System.Collections.Generic;
using System.IO;
using ExitGames.Client.Photon;
using Photon.Pun;
public static class TileSerialization
{
    public static void RegisterCustomTypes()
    {
        PhotonPeer.RegisterType(typeof(TileDataInfo), (byte)'T', SerializeTileDataInfo, DeserializeTileDataInfo);
        PhotonPeer.RegisterType(typeof(List<TileDataInfo>), (byte)'L', SerializeTileDataInfoList, DeserializeTileDataInfoList);
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
    public static short SerializeTileDataInfoList(StreamBuffer outStream, object customObject)
    {
        List<TileDataInfo> list = (List<TileDataInfo>)customObject;
        outStream.WriteByte((byte)list.Count); // İlk olarak listenin uzunluğunu yaz
        foreach (var tileData in list)
        {
            SerializeTileDataInfo(outStream, tileData); // Her bir TileDataInfo nesnesini serileştir
        }
        return (short)(1 + list.Count * (4 * sizeof(int))); // Uzunluk
    }

    public static object DeserializeTileDataInfoList(StreamBuffer inStream, short length)
    {
        int count = inStream.ReadByte(); // İlk olarak listenin uzunluğunu oku
        List<TileDataInfo> list = new List<TileDataInfo>(count);
        for (int i = 0; i < count; i++)
        {
            list.Add((TileDataInfo)DeserializeTileDataInfo(inStream, (short)(length - 1))); // Her bir TileDataInfo nesnesini deserialize et
        }
        return list;
    }
    public static short SerializeTileDataInfo(StreamBuffer outStream, object customObject)
    {
        TileDataInfo tileData = (TileDataInfo)customObject;
        byte[] bytes = new byte[3 * sizeof(int)];
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            BinaryWriter writer = new BinaryWriter(ms);
            writer.Write((int)tileData.color);
            writer.Write(tileData.number);
            writer.Write((int)tileData.type);
        }
        outStream.Write(bytes, 0, bytes.Length);
        return (short)bytes.Length;
    }

    public static object DeserializeTileDataInfo(StreamBuffer inStream, short length)
    {
        byte[] bytes = new byte[length];
        inStream.Read(bytes, 0, length);
        using (MemoryStream ms = new MemoryStream(bytes))
        {
            BinaryReader reader = new BinaryReader(ms);
            TileColor color = (TileColor)reader.ReadInt32();
            int number = reader.ReadInt32();
            TileType type = (TileType)reader.ReadInt32();
            return new TileDataInfo(color, number, type);
        }
    }
}
