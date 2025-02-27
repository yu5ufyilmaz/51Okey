using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using UnityEngine;

public static class TileSerialization
{
    public static void RegisterCustomTypes()
    {
        PhotonPeer.RegisterType(typeof(Tiles), 100, SerializeTiles, DeserializeTiles);
        PhotonPeer.RegisterType(typeof(List<Tiles>), 101, SerializeListOfTiles, DeserializeListOfTiles);
        PhotonPeer.RegisterType(typeof(Vector2Int), 102, SerializeVector2Int, DeserializeVector2Int);
        PhotonPeer.RegisterType(typeof(List<Vector2Int>), 103, SerializeListOfVector2Int, DeserializeListOfVector2Int);
        PhotonPeer.RegisterType(typeof(List<List<Tiles>>), 104, SerializeListOfListsOfTiles, DeserializeListOfListsOfTiles);
        PhotonPeer.RegisterType(typeof(List<List<Vector2Int>>), 105, SerializeListOfListsOfVector2Int, DeserializeListOfListsOfVector2Int);
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
    private static short SerializeListOfListsOfVector2Int(StreamBuffer outStream, object customObject)
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
}