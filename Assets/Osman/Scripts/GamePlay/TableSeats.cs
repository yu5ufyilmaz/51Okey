using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

[System.Serializable]
public class TableSeats
{
    public int seatNumber;
    public bool seatOccupied = false;
    public string playerName;

}
