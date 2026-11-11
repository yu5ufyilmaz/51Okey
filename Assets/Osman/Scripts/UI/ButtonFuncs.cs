using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ButtonFuncs : MonoBehaviourPunCallbacks
{
    [Tooltip("The prefab for instantiating room items")]
    public RoomItem roomItemPrefab;

    [SerializeField]
    private Transform _content;

    List<RoomInfo> list = new List<RoomInfo>();
    private List<RoomItem> roomItemsList = new List<RoomItem>();

    //Odaları yenileme süresi
    private float timeBetweenUpdates = 1.5f;
    private float nextUpdateTime = 0.0f;

    //Oda sayısı
    private int roomCount;

    [Header("Create Room Panel UI")]
    public GameObject createRoomPanel;
    public TMP_InputField roomNameInput;
    public TMP_InputField passwordInput;
    public TMP_Dropdown roundsDropdown;
    public Toggle passwordToggle;
    private Toggle botsToggle; // Sahnede yok: Start'ta şifre toggle'ı kopyalanarak oluşturulur

    [Header("Password Popup UI")]
    public GameObject passwordPopupPanel;
    public TMP_InputField passwordCheckInput;
    private string selectedRoomName;
    private string selectedRoomPassword;

    [Header("References")]
    public RoomManager roomManager;

    //Lobi Manager Scripti
    void Start()
    {
        botsToggle = CreateBotsToggle();

        if (PhotonNetwork.IsConnectedAndReady)
        {
            PhotonNetwork.JoinLobby();
        }
    }

    // "Botlarla Oyna" seçeneği: şifre kutucuğunun kopyası, tur seçiminin (Dropdown) sağında durur
    private Toggle CreateBotsToggle()
    {
        Toggle toggle = Instantiate(passwordToggle, passwordToggle.transform.parent);
        toggle.name = "BotsToggle";
        toggle.onValueChanged = new Toggle.ToggleEvent(); // Kopyalanan OnPasswordToggleChanged bağlantısını sil
        toggle.isOn = false;

        RectTransform rect = (RectTransform)toggle.transform;
        rect.anchoredPosition = new Vector2(
            75f,
            ((RectTransform)roundsDropdown.transform).anchoredPosition.y
        );

        // Yazı stili tur seçimindeki yazıdan alınır (aynı font ve renk)
        TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform))
            .AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(toggle.transform, false);
        label.font = roundsDropdown.captionText.font;
        label.color = roundsDropdown.captionText.color;
        label.text = "Botlarla Oyna";
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18f;
        label.fontSizeMax = 48f;
        label.raycastTarget = false;
        label.rectTransform.anchoredPosition = new Vector2(165f, 0f); // Kutucuğun sağı
        label.rectTransform.sizeDelta = new Vector2(210f, 100f);

        return toggle;
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

    public void CreateGame()
    {
        if (PhotonNetwork.InLobby)
            EventDispatcher.SummonEvent("CreateRoom");
    }

    public void JoinRoom(string _roomName)
    {
        if (PhotonNetwork.InLobby)
            EventDispatcher.SummonEvent("JoinRoom", _roomName);
    }

    public void JoinRandomRoom()
    {
        if (PhotonNetwork.InLobby)
        {
            roomManager.JoinRandomRoomOrCreateAuto();
        }
    }

    public void RefreshList()
    {
        if (PhotonNetwork.InLobby)
            UpdateRoomList(list);
    }

    public void OpenCreateRoomPanel()
    {
        createRoomPanel.SetActive(true);
        // Kural: Default oda adı oyuncunun adı olsun
        roomNameInput.text = PhotonNetwork.NickName;
        passwordToggle.isOn = false;
        passwordInput.interactable = false;
        botsToggle.isOn = false;
    }

    public void OnPasswordToggleChanged()
    {
        passwordInput.interactable = passwordToggle.isOn;
        if (!passwordToggle.isOn)
            passwordInput.text = "";
    }

    // Paneldeki "Odayı Oluştur" butonuna bağlı metod
    public void ConfirmCreateRoom()
    {
        string rName = roomNameInput.text;
        string pass = passwordInput.text;

        // Hata buradaydı: int.Parse direkt metni çeviremiyordu.
        // Seçeneğin içindeki metni alıp içinden sadece sayıları ayıklıyoruz.
        string dropdownText = roundsDropdown.options[roundsDropdown.value].text;

        // Sadece sayıları almak için basit bir kontrol ekliyoruz
        int rounds;
        // Eğer dropdown metni sadece "5" değil de "5 El" ise sadece "5" kısmını alır
        string numericPart = System.Text.RegularExpressions.Regex.Match(dropdownText, @"\d+").Value;

        if (int.TryParse(numericPart, out rounds))
        {
            roomManager.CreateCustomRoom(rName, pass, rounds, passwordToggle.isOn, botsToggle.isOn);
        }
        else
        {
            Debug.LogError("Dropdown seçeneğinde geçerli bir sayı bulunamadı: " + dropdownText);
            // Varsayılan bir değer atayabilirsin
            roomManager.CreateCustomRoom(rName, pass, 1, passwordToggle.isOn, botsToggle.isOn);
        }
    }

    //Burası Oda Listesini yenileme kısmı Oda bulmayla alakalı sorunları Buradan çözüceğiz.
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        if (PhotonNetwork.InLobby)
        {
            if (Time.time >= nextUpdateTime)
            {
                roomCount = roomList.Count;
                UpdateRoomList(roomList);
                nextUpdateTime = Time.time + timeBetweenUpdates; // 1.5 saniye
            }
        }
    }

    void UpdateRoomList(List<RoomInfo> roomList)
    {
        // Mevcut listedeki eski oda objelerini temizle
        foreach (RoomItem item in roomItemsList)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }
        roomItemsList.Clear();

        foreach (RoomInfo room in roomList)
        {
            // Eğer oda listeden kaldırılmışsa oluşturma
            if (room.RemovedFromList)
                continue;

            RoomItem newRoom = Instantiate(roomItemPrefab, _content);

            // Odadaki "Password" bilgisini CustomProperties üzerinden al
            string pass = "";
            if (room.CustomProperties.ContainsKey("Password"))
            {
                pass = room.CustomProperties["Password"].ToString();
            }

            // RoomItem'a gerçek oda adı, oyuncu sayısı ve şifreyi gönder
            newRoom.SetRoomName(room.Name, room.PlayerCount, pass);

            roomItemsList.Add(newRoom);
        }
    }

    // ButtonFuncs.cs içine ekle
    public void CancelPasswordPanel()
    {
        // Paneli gizle
        passwordPopupPanel.SetActive(false);

        // Input alanını temizle (güvenlik ve temizlik için)
        passwordCheckInput.text = "";

        // Seçili oda verilerini sıfırla
        selectedRoomName = "";
        selectedRoomPassword = "";
    }

    public void CloseCreateRoomPanel()
    {
        // Oda oluşturma panelini kapatır
        createRoomPanel.SetActive(false);
    }

    // Scripts/UI/ButtonFuncs.cs
    public void OnRoomItemClicked(string _roomName, string _roomPassword)
    {
        selectedRoomName = _roomName;
        selectedRoomPassword = _roomPassword;

        if (!string.IsNullOrEmpty(_roomPassword))
        {
            passwordPopupPanel.SetActive(true);
            passwordCheckInput.text = "";
        }
        else
        {
            // Şifresizse direkt gir
            roomManager.JoinRoomManual(_roomName);
        }
    }

    // ButtonFuncs.cs içine eklenecek kısım
    public void ConfirmPasswordAndJoin()
    {
        // Giriş alanındaki şifreyi al
        string enteredPass = passwordCheckInput.text;

        // RoomManager üzerinden şifre kontrolünü ve odaya girişi başlat
        // selectedRoomName ve selectedRoomPassword değerleri OnRoomItemClicked ile atanmıştı
        roomManager.JoinRoomWithPassword(selectedRoomName, enteredPass, selectedRoomPassword);

        // Paneli kapat ve temizle
        passwordPopupPanel.SetActive(false);
        passwordCheckInput.text = "";
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("Joined Lobby");
    }
}
