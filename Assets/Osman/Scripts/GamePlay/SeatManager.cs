using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class SeatManager : MonoBehaviourPunCallbacks
{
    private List<int> availableSeats = new List<int> { 1, 2, 3, 4 };
    private Dictionary<int, int> playerSeatMap = new Dictionary<int, int>(); // Maps player actor number to seat number

    public TMP_Text[] seatTextFields; // Array of Text components to display player names

    public override void OnJoinedRoom()
    {
        // If the player is the first to join, assign them a seat
        if (PhotonNetwork.IsMasterClient && availableSeats.Count > 0)
        {
            int seatNumber = availableSeats[0];
            availableSeats.RemoveAt(0); // Remove the assigned seat
            // Use RPC to assign the seat to the player on all clients
            photonView.RPC("AssignSeatToPlayer", RpcTarget.AllBuffered, PhotonNetwork.LocalPlayer.ActorNumber, seatNumber);
        }
        UpdateSeatDisplay(); // Update the seat display for the local player
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // Assign the smallest available seat number
        if (availableSeats.Count > 0 && PhotonNetwork.IsMasterClient)
        {
            int seatNumber = availableSeats[0];
            availableSeats.RemoveAt(0); // Remove the assigned seat
            // Use RPC to assign the seat to the player on all clients
            photonView.RPC("AssignSeatToPlayer", RpcTarget.AllBuffered, newPlayer.ActorNumber, seatNumber);
        }
        UpdateSeatDisplay(); // Update the seat display for the local player
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Free the seat that was occupied by the player who left
        int seatNumber = GetSeatNumberOfPlayer(otherPlayer);
        if (seatNumber != -1)
        {
            availableSeats.Add(seatNumber);
            availableSeats.Sort(); // Keep the list sorted for the smallest seat number
            // Use RPC to remove the seat assignment from all clients
            photonView.RPC("FreeSeat", RpcTarget.AllBuffered, seatNumber);
        }
        UpdateSeatDisplay(); // Update the seat display for the local player
    }

    [PunRPC]
    private void AssignSeatToPlayer(int actorNumber, int seatNumber)
    {
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);
        if (player != null)
        {
            // Store seat assignment in player's custom properties
            player.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "SeatNumber", seatNumber } });
            playerSeatMap[actorNumber] = seatNumber; // Update the player-seat map
            Debug.Log($"Player {player.NickName} is assigned to seat {seatNumber}");
        }
        UpdateSeatDisplay(); // Update the seat display for the local player
    }

    [PunRPC]
    private void FreeSeat(int seatNumber)
    {
        // Remove any player associated with the seat number
        int actorToRemove = -1;
        foreach (var entry in playerSeatMap)
        {
            if (entry.Value == seatNumber)
            {
                actorToRemove = entry.Key;
                break;
            }
        }
        if (actorToRemove != -1)
        {
            playerSeatMap.Remove(actorToRemove);
        }
        // Handle the logic for freeing the seat across all clients
        Debug.Log($"Seat {seatNumber} is now available.");
        UpdateSeatDisplay(); // Update the seat display for the local player
    }
    private int GetSeatNumberOfPlayer(Player player)
    {
        if (player.CustomProperties.TryGetValue("SeatNumber", out object seatNumber))
        {
            return (int)seatNumber;
        }
        return -1; // Seat number not found
    }

    private void UpdateSeatDisplay()
    {
        // Get the local player's seat number
        int localSeatNumber = GetSeatNumberOfPlayer(PhotonNetwork.LocalPlayer);
        if (localSeatNumber == -1) return;

        // Create a sorted list of players based on seat numbers
        List<KeyValuePair<int, int>> sortedPlayers = new List<KeyValuePair<int, int>>(playerSeatMap);
        sortedPlayers.Sort((x, y) => x.Value.CompareTo(y.Value)); // Sort by seat number

        // Find the local player's position in the sorted list
        int localPlayerIndex = sortedPlayers.FindIndex(pair => pair.Key == PhotonNetwork.LocalPlayer.ActorNumber);

        // Update the seatTextFields with relative positions
        for (int i = 0; i < seatTextFields.Length; i++)
        {
            int relativeIndex = (localPlayerIndex + i) % sortedPlayers.Count;
            int displayIndex = (i == 0) ? 0 : (4 - i) % sortedPlayers.Count; // Adjust for player's view
            int actorNumber = sortedPlayers[relativeIndex].Key;
            Player player = PhotonNetwork.CurrentRoom.GetPlayer(actorNumber);

            seatTextFields[displayIndex].text = player != null ? player.NickName : "Empty";
        }
    }
}
