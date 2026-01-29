using System;
using System.Collections.Generic;
using System.Text;
using ExitGames.Client.Photon;
using UnityEngine;

public static class TileSerialization
{
    public static void RegisterCustomTypes()
    {
        PhotonPeer.RegisterType(typeof(Tiles), 100, SerializeTiles, DeserializeTiles);
        PhotonPeer.RegisterType(
            typeof(List<Tiles>),
            101,
            SerializeListOfTiles,
            DeserializeListOfTiles
        );
        PhotonPeer.RegisterType(
            typeof(Vector2Int),
            102,
            SerializeVector2Int,
            DeserializeVector2Int
        );
        PhotonPeer.RegisterType(
            typeof(List<Vector2Int>),
            103,
            SerializeListOfVector2Int,
            DeserializeListOfVector2Int
        );
        PhotonPeer.RegisterType(
            typeof(List<List<Tiles>>),
            104,
            SerializeListOfListsOfTiles,
            DeserializeListOfListsOfTiles
        );
        PhotonPeer.RegisterType(
            typeof(List<List<Vector2Int>>),
            105,
            SerializeListOfListsOfVector2Int,
            DeserializeListOfListsOfVector2Int
        );
        PhotonPeer.RegisterType(
            typeof(ActiveTilePlacementInfo),
            106,
            SerializePlacementInfo,
            DeserializePlacementInfo
        );
        PhotonPeer.RegisterType(typeof(HandData), 107, SerializeHandData, DeserializeHandData);
    }

    private static short SerializeHandData(StreamBuffer outStream, object customObject)
    {
        HandData data = (HandData)customObject;
        outStream.WriteByte((byte)data.actorNumber);
        // Bool değeri byte'a çevir
        outStream.WriteByte((byte)(data.isFinishMove ? 1 : 0));
        // Listeyi serialize etmek için zaten yazdığın metodu kullanabilirsin
        SerializeListOfTiles(outStream, data.handTiles);

        // Not: Uzunluk hesabı dinamik olduğu için tam sayı veremeyiz ama
        // stream'e yazdığımız için Photon halleder.
        return 0;
    }

    private static object DeserializeHandData(StreamBuffer inStream, short length)
    {
        HandData data = new HandData();
        data.actorNumber = inStream.ReadByte();
        data.isFinishMove = inStream.ReadByte() == 1;
        data.handTiles = (List<Tiles>)DeserializeListOfTiles(inStream, 0);
        return data;
    }

    private static short SerializePlacementInfo(StreamBuffer outStream, object customObject)
    {
        ActiveTilePlacementInfo info = (ActiveTilePlacementInfo)customObject;

        // Tile verisini yaz (3 byte)
        outStream.WriteByte((byte)info.tileData.color);
        outStream.WriteByte((byte)info.tileData.number);
        outStream.WriteByte((byte)info.tileData.type);

        // Diğer verileri de basitçe byte olarak yaz (3 byte daha)
        outStream.WriteByte((byte)info.ownerPlayerQue);
        outStream.WriteByte((byte)info.meldType);
        outStream.WriteByte((byte)info.placeholderIndex);

        // TOPLAM YAZILAN BYTE SAYISI: 3 (tile) + 1 + 1 + 1 = 6
        return 6;
    }

    private static object DeserializePlacementInfo(StreamBuffer inStream, short length)
    {
        // Tile verisini oku (3 byte)
        TileColor color = (TileColor)inStream.ReadByte();
        int number = inStream.ReadByte();
        TileType type = (TileType)inStream.ReadByte();
        Tiles tile = new Tiles(color, number, type);

        // Diğer verileri byte olarak oku (3 byte daha)
        int ownerQue = inStream.ReadByte();
        ScoreManager.MeldType meldType = (ScoreManager.MeldType)inStream.ReadByte();
        int placeholderIndex = inStream.ReadByte();

        return new ActiveTilePlacementInfo(tile, ownerQue, meldType, placeholderIndex);
    }

    // TileSerialization.cs -> SerializeTiles Metodu
    private static short SerializeTiles(StreamBuffer outStream, object customObject)
    {
        Tiles tile = (Tiles)customObject;

        // ID Yazma (String helper kullanarak)
        WriteStringToStream(outStream, tile.id);

        // Diğer veriler
        outStream.WriteByte((byte)tile.color);
        outStream.WriteByte((byte)tile.number);
        outStream.WriteByte((byte)tile.type);
        return 0;
    }

    private static object DeserializeTiles(StreamBuffer inStream, short length)
    {
        // Önce boş nesne oluştur (Artık Tiles.cs'de boş constructor var!)
        Tiles tile = new Tiles();

        // ID Okuma
        tile.id = ReadStringFromStream(inStream);

        // Diğer veriler
        tile.color = (TileColor)inStream.ReadByte();
        tile.number = inStream.ReadByte();
        tile.type = (TileType)inStream.ReadByte();

        return tile;
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

    private static short SerializeVector2Int(StreamBuffer outStream, object customObject)
    {
        Vector2Int vector = (Vector2Int)customObject;
        byte[] xBytes = BitConverter.GetBytes(vector.x); // x değerini byte dizisine çevir
        byte[] yBytes = BitConverter.GetBytes(vector.y); // y değerini byte dizisine çevir

        outStream.Write(xBytes, 0, xBytes.Length); // x değerini yaz
        outStream.Write(yBytes, 0, yBytes.Length); // y değerini yaz
        return 0; // Success
    }

    private static object DeserializeVector2Int(StreamBuffer inStream, short length)
    {
        int x = BitConverter.ToInt32(ReadBytes(inStream, 4), 0); // x değerini oku
        int y = BitConverter.ToInt32(ReadBytes(inStream, 4), 0); // y değerini oku
        return new Vector2Int(x, y); // Yeni Vector2Int nesnesi oluştur
    }

    // List<Vector2Int> için serileştirme

    private static short SerializeListOfVector2Int(StreamBuffer outStream, object customObject)
    {
        List<Vector2Int> list = (List<Vector2Int>)customObject;
        outStream.WriteByte((byte)list.Count); // Write list count

        foreach (var vector in list)
        {
            SerializeVector2Int(outStream, vector); // Serialize each Vector2Int object
        }

        return 0; // Success
    }

    private static object DeserializeListOfVector2Int(StreamBuffer inStream, short length)
    {
        int count = inStream.ReadByte(); // Read list count
        List<Vector2Int> list = new List<Vector2Int>(count);

        for (int i = 0; i < count; i++)
        {
            Vector2Int vector = (Vector2Int)DeserializeVector2Int(inStream, 0); // Deserialize each Vector2Int object
            list.Add(vector); // Listeye ekle
        }

        return list; // Listeyi döndür
    }

    private static short SerializeListOfListsOfTiles(StreamBuffer outStream, object customObject)
    {
        List<List<Tiles>> listOfLists = (List<List<Tiles>>)customObject;
        outStream.WriteByte((byte)listOfLists.Count); // Write outer list count

        foreach (var innerList in listOfLists)
        {
            SerializeListOfTiles(outStream, innerList); // Serialize each inner List<Tiles>
        }

        return 0; // Success
    }

    private static object DeserializeListOfListsOfTiles(StreamBuffer inStream, short length)
    {
        int outerCount = inStream.ReadByte(); // Read outer list count
        List<List<Tiles>> listOfLists = new List<List<Tiles>>(outerCount);

        for (int i = 0; i < outerCount; i++)
        {
            List<Tiles> innerList = (List<Tiles>)DeserializeListOfTiles(inStream, 0); // Deserialize each inner List<Tiles>
            listOfLists.Add(innerList); // Add to outer list
        }

        return listOfLists; // Return the outer list
    }

    private static short SerializeListOfListsOfVector2Int(
        StreamBuffer outStream,
        object customObject
    )
    {
        List<List<Vector2Int>> listOfLists = (List<List<Vector2Int>>)customObject;
        outStream.WriteByte((byte)listOfLists.Count); // Write outer list count

        foreach (var innerList in listOfLists)
        {
            SerializeListOfVector2Int(outStream, innerList); // Serialize each inner List<Vector2Int>
        }

        return 0; // Success
    }

    private static object DeserializeListOfListsOfVector2Int(StreamBuffer inStream, short length)
    {
        int outerCount = inStream.ReadByte(); // Read outer list count
        List<List<Vector2Int>> listOfLists = new List<List<Vector2Int>>(outerCount);

        for (int i = 0; i < outerCount; i++)
        {
            List<Vector2Int> innerList = (List<Vector2Int>)DeserializeListOfVector2Int(inStream, 0); // Deserialize each inner List<Vector2Int>
            listOfLists.Add(innerList); // Add to outer list
        }

        return listOfLists; // Return the outer list
    }

    private static byte[] ReadBytes(StreamBuffer inStream, int count)
    {
        byte[] buffer = new byte[count];
        inStream.Read(buffer, 0, count); // Belirtilen sayıda byte oku
        return buffer; // Okunan byte dizisini döndür
    }

    private static void WriteIntToStream(StreamBuffer outStream, int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        outStream.Write(bytes, 0, bytes.Length);
    }

    private static int ReadIntFromStream(StreamBuffer inStream)
    {
        byte[] bytes = new byte[4];
        inStream.Read(bytes, 0, 4);
        return BitConverter.ToInt32(bytes, 0);
    }

    private static void WriteStringToStream(StreamBuffer outStream, string value)
    {
        if (value == null)
            value = "";
        byte[] strBytes = Encoding.UTF8.GetBytes(value);
        WriteIntToStream(outStream, strBytes.Length); // Önce uzunluk
        outStream.Write(strBytes, 0, strBytes.Length); // Sonra veri
    }

    private static string ReadStringFromStream(StreamBuffer inStream)
    {
        int length = ReadIntFromStream(inStream);
        if (length == 0)
            return "";

        byte[] strBytes = new byte[length];
        inStream.Read(strBytes, 0, length);
        return Encoding.UTF8.GetString(strBytes);
    }
}
