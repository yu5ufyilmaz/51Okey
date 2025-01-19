using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using System.Collections;
using UnityEngine.UIElements;

public class SeatManager : MonoBehaviourPunCallbacks
{
    public List<int> availableSeats = new List<int> { 1, 2, 3, 4 };
    public Dictionary<int, int> playerSeatMap = new Dictionary<int, int>(); // Maps player actor number to seat number

    public TMP_Text[] seatTextFields; // Array of Text components to display player names
    public TileDistrubite tileDistrubite;

    [Header("Player Spawn Settings")]
    public int spawnIndex;
    public GameObject playerPrefab;
    public GameObject tileManagerPrefab;
    PhotonView playerPhotonView;
    public RectTransform[] spawnPositions;
    bool gameIsStart = false;





    private void Awake()
    {
        TileSerialization.RegisterCustomTypes(); // Custom serialization for TileDataInfo
    }
    #region Player Join and Left Functions
    public override void OnJoinedRoom()
    {
        AssignPositionAndInstantiate();
        // If the player is the first to join, assign them a seat
        if (PhotonNetwork.IsMasterClient && availableSeats.Count > 0)
        {
            int seatNumber = availableSeats[0];
            availableSeats.RemoveAt(0); // Remove the assigned seat
            // Use RPC to assign the seat to the player on all clients
            GameObject tileManager = PhotonNetwork.Instantiate(tileManagerPrefab.name, Vector3.zero, Quaternion.identity, 0);
            Debug.Log(tileManager.name);
            tileDistrubite = tileManager.GetComponent<TileDistrubite>();
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
            for (int i = 0; i < availableSeats.Count; i++)
            {
                if (availableSeats[i] <= seatNumber)
                {
                    seatNumber = availableSeats[i];
                    availableSeats.RemoveAt(i);

                    break;
                }
            }
            // Remove the assigned seat
            // Use RPC to assign the seat to the player on all clients
            photonView.RPC("AssignSeatToPlayer", RpcTarget.AllBuffered, newPlayer.ActorNumber, seatNumber);
        }
        StartGame();
        UpdateSeatDisplay(); // Update the seat display for the local player
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Free the seat that was occupied by the player who left
        int seatNumber_E = GetSeatNumberOfPlayer(otherPlayer);
        if (seatNumber_E != -1)
        {
            availableSeats.Add(seatNumber_E);
            availableSeats.Sort(); // Keep the list sorted for the smallest seat number
            // Use RPC to remove the seat assignment from all clients
            photonView.RPC("FreeSeat", RpcTarget.AllBuffered, seatNumber_E);
        }
        UpdateSeatDisplay(); // Update the seat display for the local player
    }
    #endregion
    #region  Spawn and Instantiate Players
    private void AssignPositionAndInstantiate()
    {

        if (playerPrefab != null)
        {

            Quaternion spawnRotation = Quaternion.identity;

            // Oyuncuyu belirlenen pozisyona yerleştir
            GameObject player = PhotonNetwork.Instantiate(playerPrefab.name, Vector3.zero, spawnRotation, 0);


            Vector3 spawnPosition = spawnPositions[spawnIndex].position;

            playerPhotonView = player.GetComponent<PhotonView>();

            Player playerr = PhotonNetwork.LocalPlayer;

            playerPhotonView.RPC("SetPlayerName", RpcTarget.AllBuffered, PhotonNetwork.NickName);
            playerPhotonView.RPC("SetPlayerSeat", RpcTarget.AllBuffered, spawnPosition);

        }
        else
        {
            Debug.LogError("No available spawn positions found or playerPrefab is not assigned!");
        }
    }
    #endregion
    #region Seat Assignment Functions
    //Burada Oyuncuya kendimiz bir özellik ekliyoruz Set Custom Properties ile Her oyuncunun oturduğu seati biliyoruz.
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
        player.CustomProperties.TryGetValue("SeatNumber", out object seatNumber);
        if (seatNumber != null)
        {
            Debug.Log("Seat Number: " + (int)seatNumber);
            return (int)seatNumber;
        }
        else
            return -1; // Seat number not found
    }
    #endregion
    #region Relative Player Order
    //Seat Text changes from there.
    private void UpdateSeatDisplay()
    {
        Player[] players = PhotonNetwork.PlayerList;
        int localPlayerIndex = System.Array.IndexOf(players, PhotonNetwork.LocalPlayer);
        if (localPlayerIndex == -1)
        {
            Debug.LogError("Local player not found in the player list!");
            return;
        }
        // Clear seat text fields first
        for (int i = 0; i < players.Length; i++)
        {
            // Her oyuncu için relativeIndex, kendisini sıfırıncı indexte görmeli ve diğerlerini göreceli olarak sıralamalıdır
            int relativeIndex = (i - localPlayerIndex + players.Length) % players.Length;

            if (relativeIndex < seatTextFields.Length)
            {
                seatTextFields[relativeIndex].text = players[i].NickName;
            }
        }

    }



    private List<Player> GetRelativePlayerOrder(Player localPlayer)
    {
        List<Player> orderedPlayers = new List<Player>(PhotonNetwork.PlayerList);

        // Sort players by seat number, excluding the local player initially
        orderedPlayers.Sort((a, b) =>
        {
            int seatA = GetSeatNumberOfPlayer(a);
            int seatB = GetSeatNumberOfPlayer(b);
            return seatA.CompareTo(seatB);
        });

        // Create a new list to maintain the order
        List<Player> relativeOrder = new List<Player>();
        relativeOrder.Add(localPlayer); // Add the local player first

        // Add other players in order
        foreach (var player in orderedPlayers)
        {
            if (player.ActorNumber != localPlayer.ActorNumber)
            {
                relativeOrder.Add(player);
            }
        }

        return relativeOrder;
    }
    #endregion
    #region Starting Game
    public GameObject[] imageGameObjects;
    private void StartGame()
    {
        if (!gameIsStart)
        {
            if (PhotonNetwork.PlayerList.Length == 4)
            {
                // Check if all players are assigned a seat
                if (tileDistrubite != null && PhotonNetwork.IsMasterClient)
                {
                    Debug.Log(PhotonNetwork.LocalPlayer.NickName + " is the master client.");
                    StartCoroutine(CountdownAndShuffle());
                }
            }
        }
    }

    private IEnumerator CountdownAndShuffle()
    {
        // Countdown from 3 to 0
        for (int i = 3; i > 0; i--)
        {
            Debug.Log($"Countdown: {i}");
            //UpdateImageStates(i); // Update image states for the current countdown number
            yield return new WaitForSeconds(1f); // Wait for 1 second
        }

        // After countdown, shuffle the tiles
        tileDistrubite.ShuffleTiles();
        gameIsStart = true; // Set the game as started
    }

    private void UpdateImageStates(int countdownValue)
    {
        // Activate the corresponding image for the countdown value
        for (int i = 0; i < imageGameObjects.Length; i++)
        {
            if (i == countdownValue - 1) // Activate the image corresponding to the countdown value
            {
                imageGameObjects[i].SetActive(true);
            }
            else
            {
                imageGameObjects[i].SetActive(false);
            }
        }

        // Optionally, deactivate all images after a short delay
        StartCoroutine(DeactivateImagesAfterDelay());
    }

    private IEnumerator DeactivateImagesAfterDelay()
    {
        yield return new WaitForSeconds(1f); // Wait for 1 second
        for (int i = 0; i < imageGameObjects.Length; i++)
        {
            //imageGameObjects[i].SetActive(false); // Deactivate all images
        }
    }
    #endregion
}
