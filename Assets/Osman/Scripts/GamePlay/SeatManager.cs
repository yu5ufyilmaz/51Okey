using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class SeatManager : MonoBehaviourPunCallbacks
{
    public List<int> availableSeats = new List<int> { 1, 2, 3, 4 };
    public Dictionary<int, int> playerSeatMap = new Dictionary<int, int>(); // Maps player actor number to seat number

    public TMP_Text[] seatTextFields; // Array of Text components to display player names
    public GameObject[] tiledropOffset;
    public GameObject[] meldTileOffsets;
    public TileDistrubite tileDistrubite;
    public ScoreManager sManager;
    private TurnManager turnManager;

    [Header("Player Spawn Settings")]
    public int spawnIndex;
    public GameObject playerPrefab;
    public GameObject tileManagerPrefab;
    public GameObject scoreManagerPrefab;
    PhotonView playerPhotonView;
    public RectTransform[] spawnPositions;
    bool gameIsStart = false;

    private void Awake()
    {
        TileSerialization.RegisterCustomTypes(); // Custom serialization for TileDataInfo
    }

    // SeatManager.cs İÇİNE EKLE (Awake'den sonra, diğer metotlardan önce bir yere):

    private void Start()
    {
        // Eğer sahne açıldığında zaten bir odadaysak (Yani oyun yeniden başladıysa)
        // OnJoinedRoom otomatik çalışmaz, biz manuel tetiklemeliyiz.
        if (PhotonNetwork.IsConnected && PhotonNetwork.InRoom)
        {
            Debug.Log("Sahne yenilendi (Restart), oyun tekrar kuruluyor...");
            OnJoinedRoom();
        }
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
            GameObject tileManager = PhotonNetwork.InstantiateRoomObject(
                tileManagerPrefab.name,
                Vector3.zero,
                Quaternion.identity,
                0
            );

            turnManager = GameObject.Find("TurnManager").GetComponent<TurnManager>();
            tileDistrubite = tileManager.GetComponent<TileDistrubite>();

            photonView.RPC(
                "AssignSeatToPlayer",
                RpcTarget.AllBuffered,
                PhotonNetwork.LocalPlayer.ActorNumber,
                seatNumber
            );
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
            photonView.RPC(
                "AssignSeatToPlayer",
                RpcTarget.AllBuffered,
                newPlayer.ActorNumber,
                seatNumber
            );
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
            GameObject player = PhotonNetwork.Instantiate(
                playerPrefab.name,
                Vector3.zero,
                spawnRotation,
                0
            );

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
            // Koltuk bilgisini kaydet
            player.SetCustomProperties(
                new ExitGames.Client.Photon.Hashtable { { "SeatNumber", seatNumber } }
            );
            playerSeatMap[actorNumber] = seatNumber;
        }

        // EKRANI GÜNCELLE
        UpdateSeatDisplay();
    }

    // SeatManager.cs içine ekle:

    public override void OnPlayerPropertiesUpdate(
        Player targetPlayer,
        ExitGames.Client.Photon.Hashtable changedProps
    )
    {
        // Eğer değişen özellik "SeatNumber" (Koltuk Numarası) ise ekranı güncelle
        if (changedProps.ContainsKey("SeatNumber"))
        {
            Debug.Log(
                $"{targetPlayer.NickName} oyuncusunun koltuk verisi güncellendi. Ekran yenileniyor."
            );
            UpdateSeatDisplay();
        }
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

    // SeatManager.cs -> GetSeatNumberOfPlayer (GÜNCELLENMİŞ HIZLI VERSİYON)

    private int GetSeatNumberOfPlayer(Player player)
    {
        // YÖNTEM 1: Önce yerel hafızaya (Dictionary) bak (En Hızlısı)
        if (playerSeatMap.ContainsKey(player.ActorNumber))
        {
            return playerSeatMap[player.ActorNumber];
        }

        // YÖNTEM 2: Yoksa Photon özelliklerine bak (Yedek)
        if (player.CustomProperties.TryGetValue("SeatNumber", out object seatNumber))
        {
            return (int)seatNumber;
        }

        return -1; // Henüz veri yok
    }
    #endregion
    #region Relative Player Order
    //Seat Text changes from there.
    // SeatManager.cs -> UpdateSeatDisplay (DÜZELTİLMİŞ)

    private void UpdateSeatDisplay()
    {
        // 1. ÖNCE TEMİZLİK (Hayalet/Clone sorunu burada çözülüyor)
        // Her güncelleme öncesi masadaki isimleri siliyoruz ki eskiler orada kalmasın.
        // Eğer bunu yapmazsak, B kişisi koltuk değiştirirse eski yerinde ismi kalır.
        foreach (var textField in seatTextFields)
        {
            if (textField != null)
                textField.text = "";
        }

        // 2. YEREL OYUNCUNUN (SENİN) KOLTUK NUMARASINI BUL
        int localSeatNumber = GetSeatNumberOfPlayer(PhotonNetwork.LocalPlayer);

        // Eğer senin koltuk numaran henüz serverdan gelmediyse (-1 ise),
        // En azından kendini en ortaya (0. Index) yaz ki ekran boş kalmasın.
        if (localSeatNumber == -1)
        {
            if (seatTextFields.Length > 0 && seatTextFields[0] != null)
                seatTextFields[0].text = PhotonNetwork.LocalPlayer.NickName;

            // Diğerlerinin yerini sen oturmadan hesaplayamayız, o yüzden çıkıyoruz.
            return;
        }

        // 3. TÜM OYUNCULARI GEZ VE KOLTUK NUMARASINA GÖRE YERLEŞTİR
        Player[] players = PhotonNetwork.PlayerList;

        foreach (Player p in players)
        {
            // Oyuncunun koltuk numarasını al (Custom Property'den)
            int playerSeatNumber = GetSeatNumberOfPlayer(p);

            // Eğer oyuncunun koltuğu henüz atanmadıysa atla (Hata olmasın)
            if (playerSeatNumber == -1)
                continue;

            // --- KRİTİK MATEMATİKSEL HESAP ---
            // Bu formül, senin koltuğuna göre diğerlerinin nereye oturacağını hesaplar.
            // Sen (Local) her zaman 0 (Aşağıda) olursun. Diğerleri sana göre döner.
            // Formül: (HedefKoltuk - SeninKoltugun + ToplamKoltuk) % ToplamKoltuk

            int relativeIndex = (playerSeatNumber - localSeatNumber + 4) % 4;

            if (relativeIndex < seatTextFields.Length && relativeIndex >= 0)
            {
                // İSMİ YAZ (Üzerine yazma işlemi yapar, temizlediğimiz için sorun olmaz)
                if (seatTextFields[relativeIndex] != null)
                {
                    seatTextFields[relativeIndex].text = p.NickName;
                }

                // OBJELERİN İSMİNİ DÜZELT (Debug ve sistemin çalışması için)
                if (
                    meldTileOffsets.Length > relativeIndex
                    && meldTileOffsets[relativeIndex] != null
                )
                    meldTileOffsets[relativeIndex].name = p.NickName + " meld";

                if (tiledropOffset.Length > relativeIndex && tiledropOffset[relativeIndex] != null)
                    tiledropOffset[relativeIndex].name = p.NickName;
            }
        }
    }

    private List<Player> GetRelativePlayerOrder(Player localPlayer)
    {
        List<Player> orderedPlayers = new List<Player>(PhotonNetwork.PlayerList);

        // Sort players by seat number, excluding the local player initially
        orderedPlayers.Sort(
            (a, b) =>
            {
                int seatA = GetSeatNumberOfPlayer(a);
                int seatB = GetSeatNumberOfPlayer(b);
                return seatA.CompareTo(seatB);
            }
        );

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

    // SeatManager.cs -> CountdownAndShuffle metodu

    private IEnumerator CountdownAndShuffle()
    {
        // Countdown from 3 to 0
        for (int i = 3; i > 0; i--)
        {
            Debug.Log($"Countdown: {i}");
            //UpdateImageStates(i); 
            yield return new WaitForSeconds(1f); 
        }
        GameObject scoreManager = PhotonNetwork.InstantiateRoomObject(
            scoreManagerPrefab.name,
            Vector3.zero,
            Quaternion.identity,
            0
        );
        // -------------------------

        scoreManager.SetActive(true);
        sManager = scoreManager.GetComponent<ScoreManager>();

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
