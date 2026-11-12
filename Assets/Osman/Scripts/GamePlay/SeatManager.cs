using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class SeatManager : MonoBehaviourPunCallbacks
{
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

    [Header("Bot Settings")]
    // "Botlarla Oyna" seçili masada başka oyuncuların katılması için beklenen süre (sn). Sonra boş koltuklar botla dolar.
    public float botFillDelay = 10f;

    // Yalnız SyncAutoTest host'u atar: test editörlerinin yüklenip katılması 10 sn'den uzun sürer.
    public static float? BotFillDelayOverride;

    private void Awake()
    {
        TileSerialization.RegisterCustomTypes(); // Custom serialization for TileDataInfo
        // Alanların sahnedeki asıl adlarını sakla (ilk çağrı sadece kaydeder).
        RestoreDefaultNames(meldTileOffsets, ref defaultMeldNames);
        RestoreDefaultNames(tiledropOffset, ref defaultDropNames);
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
        // Odayı kuran direkt oturur (sahne yeniden yüklendiyse koltuğunu korur).
        int seatNumber = GetSeatNumberOfPlayer(PhotonNetwork.LocalPlayer);
        if (seatNumber <= 0)
            seatNumber = FirstFreeSeat();
        if (PhotonNetwork.IsMasterClient && seatNumber > 0)
        {
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

            // Masa açılırken "Botlarla Oyna" seçildiyse (RoomManager.CreateCustomRoom)
            if (
                PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("FillWithBots", out object fill)
                && (bool)fill
            )
                StartCoroutine(StartWithBotsAfterDelay());
        }
        UpdateSeatDisplay(); // Update the seat display for the local player
    }

    private IEnumerator StartWithBotsAfterDelay()
    {
        float delay = BotFillDelayOverride ?? botFillDelay;
        yield return new WaitForSeconds(delay);
        if (!gameIsStart)
        {
            // Koltuk seçmeden bekleyen insanlar önce ilk boş koltuklara oturur, kalan boşları botlar doldurur.
            foreach (Player p in PhotonNetwork.PlayerList)
            {
                int seat = FirstFreeSeat();
                if (!IsSeated(p) && seat > 0)
                    photonView.RPC("AssignSeatToPlayer", RpcTarget.AllBuffered, p.ActorNumber, seat);
            }
            Debug.Log(
                $"Oda {delay} sn içinde dolmadı. Boş koltuklar botla dolduruluyor ({PhotonNetwork.PlayerList.Length} insan)."
            );
            StartGame(true);
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // Rastgele/"Katıl" ile gelen ilk boş koltuğa oturur; odayı listeden seçen koltuğunu kendisi seçer (RequestSeat).
        if (
            PhotonNetwork.IsMasterClient
            && !gameIsStart
            && GetSeatNumberOfPlayer(newPlayer) <= 0
            && !WantsToPickSeat(newPlayer)
        )
        {
            int seatNumber = FirstFreeSeat();
            if (seatNumber > 0)
                photonView.RPC(
                    "AssignSeatToPlayer",
                    RpcTarget.AllBuffered,
                    newPlayer.ActorNumber,
                    seatNumber
                );
        }
        TryStartGame();
        UpdateSeatDisplay(); // Update the seat display for the local player
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // Free the seat that was occupied by the player who left
        int seatNumber_E = GetSeatNumberOfPlayer(otherPlayer);
        if (seatNumber_E != -1 && PhotonNetwork.IsMasterClient)
        {
            // Use RPC to remove the seat assignment from all clients
            photonView.RPC("FreeSeat", RpcTarget.AllBuffered, seatNumber_E);
        }
        UpdateSeatDisplay(); // Update the seat display for the local player
    }

    static bool WantsToPickSeat(Player p) =>
        p.CustomProperties.TryGetValue(RoomManager.PickSeatKey, out object v) && v is bool pick && pick;

    // Odadaki oyuncuların oturduğu koltuklar (except: yer değiştiren oyuncunun kendi koltuğu sayılmaz).
    HashSet<int> TakenSeats(Player except = null)
    {
        var taken = new HashSet<int>();
        foreach (Player p in PhotonNetwork.PlayerList)
        {
            int s = GetSeatNumberOfPlayer(p);
            if (s > 0 && (except == null || p.ActorNumber != except.ActorNumber))
                taken.Add(s);
        }
        return taken;
    }

    public int FirstFreeSeat()
    {
        var taken = TakenSeats();
        for (int s = 1; s <= 4; s++)
            if (!taken.Contains(s))
                return s;
        return -1;
    }

    public bool IsSeated(Player p) => GetSeatNumberOfPlayer(p) > 0;

    // Masa dolana (4 kişi oturana) kadar koltuk seçilebilir / değiştirilebilir. Kararı master verir.
    public void RequestSeat(int seat) => photonView.RPC(nameof(RequestSeatRPC), RpcTarget.MasterClient, seat);

    [PunRPC]
    private void RequestSeatRPC(int seat, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient || gameIsStart || info.Sender == null || seat < 1 || seat > 4)
            return;
        if (GetSeatNumberOfPlayer(info.Sender) == seat || TakenSeats(info.Sender).Contains(seat))
            return;
        photonView.RPC("AssignSeatToPlayer", RpcTarget.AllBuffered, info.Sender.ActorNumber, seat);
        TryStartGame();
    }

    // Geri sayımı 4 kişinin de oturmuş olması başlatır (odaya girmiş olması değil).
    private void TryStartGame()
    {
        if (PhotonNetwork.IsMasterClient && !gameIsStart && TakenSeats().Count == 4)
            StartGame();
    }

    // Geri sayım başladı: koltuklar kilitli, "OTUR" gizli. Room property ki herkes görsün.
    const string SeatsLockedKey = "SeatsLocked";

    bool SeatsLocked =>
        PhotonNetwork.CurrentRoom != null
        && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SeatsLockedKey, out object v)
        && v is bool locked
        && locked;

    public override void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged)
    {
        if (propertiesThatChanged.ContainsKey(SeatsLockedKey))
            UpdateSeatDisplay();
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
            // Koltuk bilgisini kaydet. Özelliği sadece master yazar: yer değiştirmede her client'ın eski/yeni
            // değeri farklı sırayla göndermesi gereksiz trafik; harita herkeste bu RPC ile aynı sırada güncellenir.
            if (PhotonNetwork.IsMasterClient)
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
        // Boş kalan alanlar eski oyuncunun adıyla kalırsa "<ad> meld" araması yanlış alanı bulur.
        RestoreDefaultNames(meldTileOffsets, ref defaultMeldNames);
        RestoreDefaultNames(tiledropOffset, ref defaultDropNames);

        // 2. YEREL OYUNCUNUN (SENİN) KOLTUK NUMARASINI BUL
        int localSeatNumber = GetSeatNumberOfPlayer(PhotonNetwork.LocalPlayer);

        // Henüz oturmamış (koltuğunu seçecek) oyuncu masayı 1. koltuk altta olacak şekilde görür.
        if (localSeatNumber == -1)
            localSeatNumber = 1;
        viewBaseSeat = localSeatNumber;

        // 3. TÜM OYUNCULARI GEZ VE KOLTUK NUMARASINA GÖRE YERLEŞTİR
        Player[] players = PhotonNetwork.PlayerList;
        var occupied = new bool[4];

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
                occupied[relativeIndex] = true;
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

        // 4. BOŞ KOLTUKLAR: masa dolana kadar "OTUR"; dokunan oraya geçer (ya da ilk kez oturur).
        bool canPick = !SeatsLocked && (GameManager.Instance == null || !GameManager.Instance.isGameReady);
        for (int i = 0; i < seatTextFields.Length && i < 4; i++)
        {
            TMP_Text field = seatTextFields[i];
            if (field == null)
                continue;
            bool pick = canPick && !occupied[i];
            if (pick)
                field.text = "OTUR";
            field.raycastTarget = pick;
            var button = field.GetComponent<UnityEngine.UI.Button>();
            if (button == null && pick)
            {
                int rel = i;
                button = field.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = field;
                button.onClick.AddListener(() => RequestSeat((viewBaseSeat - 1 + rel) % 4 + 1));
            }
            if (button != null)
                button.enabled = pick;
        }
    }

    // Koltuk seçerken ekranın altındaki koltuk (oturmuşsa kendi koltuğu, değilse 1).
    int viewBaseSeat = 1;
    string[] defaultMeldNames,
        defaultDropNames;

    static void RestoreDefaultNames(GameObject[] objects, ref string[] defaults)
    {
        if (objects == null)
            return;
        if (defaults == null)
        {
            defaults = new string[objects.Length];
            for (int i = 0; i < objects.Length; i++)
                defaults[i] = objects[i] != null ? objects[i].name : null;
            return;
        }
        for (int i = 0; i < objects.Length; i++)
            if (objects[i] != null && defaults[i] != null)
                objects[i].name = defaults[i];
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

    private void StartGame(bool fillWithBots = false)
    {
        if (!gameIsStart)
        {
            if (TakenSeats().Count == 4 || fillWithBots)
            {
                // Check if all players are assigned a seat
                if (tileDistrubite != null && PhotonNetwork.IsMasterClient)
                {
                    Debug.Log(PhotonNetwork.LocalPlayer.NickName + " is the master client.");
                    // Geri sayım sırasında ikinci kez başlamasın; oyun başladıktan sonra odaya kimse girmesin
                    gameIsStart = true;
                    PhotonNetwork.CurrentRoom.IsOpen = false;
                    PhotonNetwork.CurrentRoom.IsVisible = false;
                    PhotonNetwork.CurrentRoom.SetCustomProperties(
                        new ExitGames.Client.Photon.Hashtable { { SeatsLockedKey, true } }
                    );
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
