using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lobi ekranı: masa kartları, masa açma ve şifre pencereleri, alttaki üç buton.
public class ButtonFuncs : MonoBehaviourPunCallbacks
{
    const float RedrawInterval = 0.5f;
    const string WaitMessage = "Bağlantı bekleniyor, tekrar dene";

    [Header("Masa kartları")]
    public RoomCard roomCardPrefab;
    public RectTransform cardContent;
    public GameObject emptyCard;
    public UiSnapScroll snapScroll;

    [Header("Create Room Panel UI")]
    public GameObject createRoomPanel;
    public TMP_InputField roomNameInput;
    public TMP_InputField passwordInput;
    public Toggle passwordToggle;
    public int[] roundOptions = { 1 };
    public TMP_Text roundsText;

    [Header("Password Popup UI")]
    public GameObject passwordPopupPanel;
    public TMP_InputField passwordCheckInput;

    [Header("References")]
    public RoomManager roomManager;

    readonly LobbyRoomList rooms = new LobbyRoomList();
    readonly Dictionary<string, RoomCard> cards = new Dictionary<string, RoomCard>();
    float nextRedraw;
    bool redrawPending = true;
    int roundIndex;
    LobbyRoom selectedRoom; // şifre penceresi açıkken seçilen masa ve koltuk
    int selectedSeat;

    void Start()
    {
        if (PhotonNetwork.IsConnectedAndReady)
        {
            PhotonNetwork.JoinLobby();
        }
    }

    void Update()
    {
        // Çizim en çok yarım saniyede bir; arada gelen güncelleme bekler, kaybolmaz.
        if (redrawPending && Time.unscaledTime >= nextRedraw)
            Redraw();
    }

    public override void OnConnectedToMaster()
    {
        // 'InLobby' yerine 'JoinedLobby' kullanmalısın
        if (
            PhotonNetwork.NetworkClientState != ClientState.JoiningLobby
            && PhotonNetwork.NetworkClientState != ClientState.JoinedLobby
        )
        {
            Debug.Log("Master Sunucuya Bağlanıldı, Lobiye Giriş Yapılıyor...");
            PhotonNetwork.JoinLobby();
        }
        else
        {
            Debug.Log("Zaten lobiye giriliyor veya lobideyiz. JoinLobby çağrısı atlandı.");
        }
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
        ClearRooms();
    }

    public override void OnLeftLobby() => ClearRooms();

    // Lobide değilken Photon çağrıları reddedilir: oyuncuya söylenir, başka bir şey yapılmaz.
    bool InLobby()
    {
        if (PhotonNetwork.InLobby)
            return true;
        UIRoot.Toast(WaitMessage);
        return false;
    }

    public void JoinRandomRoom()
    {
        if (InLobby())
            roomManager.JoinRandomRoomOrCreateAuto();
    }

    public void PlayWithBots()
    {
        if (InLobby())
            roomManager.CreateBotRoom();
    }

    public void OpenCreateRoomPanel()
    {
        if (!InLobby())
            return;
        // Kural: Default oda adı oyuncunun adı olsun
        roomNameInput.text = PhotonNetwork.NickName;
        passwordToggle.isOn = false;
        OnPasswordToggleChanged();
        roundIndex = 0;
        ShowRounds();
        createRoomPanel.SetActive(true);
    }

    public void OnPasswordToggleChanged()
    {
        passwordInput.interactable = passwordToggle.isOn;
        if (!passwordToggle.isOn)
            passwordInput.text = "";
    }

    public void ChangeRounds(int step)
    {
        roundIndex = Mathf.Clamp(roundIndex + step, 0, roundOptions.Length - 1);
        ShowRounds();
    }

    void ShowRounds() => roundsText.text = Rounds + " el";

    int Rounds => roundOptions.Length > 0 ? roundOptions[Mathf.Clamp(roundIndex, 0, roundOptions.Length - 1)] : 1;

    // Paneldeki "MASAYI KUR" butonuna bağlı metod. Şifre özeti girilen adla tuzlanır: boş ad masayı girilmez yapar.
    public void ConfirmCreateRoom()
    {
        if (!InLobby())
            return;
        string rName = roomNameInput.text.Trim();
        if (rName.Length == 0)
        {
            UIRoot.Toast("Masa adı boş olamaz");
            return;
        }
        if (passwordToggle.isOn && string.IsNullOrWhiteSpace(passwordInput.text))
        {
            UIRoot.Toast("Şifre boş olamaz");
            return;
        }
        roomManager.CreateCustomRoom(rName, passwordInput.text, Rounds, passwordToggle.isOn);
    }

    public void CloseCreateRoomPanel()
    {
        // Oda oluşturma panelini kapatır
        createRoomPanel.SetActive(false);
    }

    public void CancelPasswordPanel()
    {
        passwordPopupPanel.SetActive(false);
        passwordCheckInput.text = "";
        selectedRoom = null;
        selectedSeat = 0;
    }

    // Karttaki OTUR: şifresiz masaya doğrudan, şifreliye pencereden girilir.
    void OnSit(LobbyRoom room, int seat)
    {
        if (!InLobby())
            return;
        if (!room.HasPassword)
        {
            roomManager.JoinRoomAtSeat(room.Name, seat);
            return;
        }
        selectedRoom = room;
        selectedSeat = seat;
        passwordCheckInput.text = "";
        passwordPopupPanel.SetActive(true);
    }

    // Şifre yanlışsa pencere açık kalır, yalnız alan temizlenir
    public void ConfirmPasswordAndJoin()
    {
        if (!InLobby() || selectedRoom == null)
            return;
        bool joining = roomManager.JoinRoomWithPassword(
            selectedRoom.Name,
            passwordCheckInput.text,
            selectedRoom.PasswordHash,
            selectedSeat
        );
        if (joining)
            passwordPopupPanel.SetActive(false);
        passwordCheckInput.text = "";
    }

    // Photon yalnız farkı gönderir; hepsi listeye işlenir, ekran Visible() üzerinden çizilir.
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        var delta = new List<LobbyRoom>(roomList.Count);
        foreach (RoomInfo info in roomList)
            delta.Add(ToLobbyRoom(info));
        ApplyRooms(delta);
    }

    void ApplyRooms(List<LobbyRoom> delta)
    {
        rooms.Apply(delta);
        redrawPending = true;
    }

    void ClearRooms()
    {
        rooms.Clear();
        redrawPending = true;
    }

    // Kartlar ada göre yeniden kullanılır; sıra Visible() sırasıdır.
    void Redraw()
    {
        redrawPending = false;
        nextRedraw = Time.unscaledTime + RedrawInterval;
        List<LobbyRoom> visible = rooms.Visible();
        var keep = new HashSet<string>();
        for (int i = 0; i < visible.Count; i++)
        {
            LobbyRoom r = visible[i];
            keep.Add(r.Name);
            if (!cards.TryGetValue(r.Name, out RoomCard card))
                cards[r.Name] = card = Instantiate(roomCardPrefab, cardContent);
            card.Show(r, OnSit);
            card.transform.SetSiblingIndex(i);
        }
        foreach (string name in new List<string>(cards.Keys))
        {
            if (keep.Contains(name))
                continue;
            cards[name].gameObject.SetActive(false); // yerleşim bu karede de saymasın
            Destroy(cards[name].gameObject);
            cards.Remove(name);
        }
        emptyCard.SetActive(visible.Count == 0);
        snapScroll.Settle();
    }

    // Oda özellikleri başka istemciden gelir: eksik ya da bozuk değer varsayılana döner.
    static LobbyRoom ToLobbyRoom(RoomInfo info)
    {
        var p = info.CustomProperties;
        object rounds = null, hash = null, seats = null;
        if (p != null)
        {
            p.TryGetValue("TotalRounds", out rounds);
            p.TryGetValue(RoomManager.PwHashKey, out hash);
            p.TryGetValue(RoomManager.SeatsKey, out seats);
        }
        return new LobbyRoom
        {
            Name = info.Name,
            PlayerCount = info.PlayerCount,
            MaxPlayers = info.MaxPlayers == 0 ? 4 : info.MaxPlayers,
            IsOpen = info.IsOpen,
            Removed = info.RemovedFromList,
            Rounds = rounds is int n && n > 0 ? n : 1,
            PasswordHash = hash as string ?? "",
            Seats = FourSeats(seats),
        };
    }

    // Her zaman dört eleman, null yok (string[] de object[] sayılır).
    static string[] FourSeats(object raw)
    {
        var seats = new[] { "", "", "", "" };
        if (raw is object[] arr)
            for (int i = 0; i < 4 && i < arr.Length; i++)
                seats[i] = arr[i] as string ?? "";
        return seats;
    }
}
