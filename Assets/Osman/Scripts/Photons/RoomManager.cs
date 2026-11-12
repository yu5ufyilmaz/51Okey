using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [SerializeField]
    private int emptyRoomTtl = 0;

    [SerializeField]
    private bool cleanupCacheOnLeave = true;

    [SerializeField]
    private bool isOpen = true;

    [SerializeField]
    private bool isVisible = true;
    private int maxPlayers = 4;

    [SerializeField]
    private GameObject tileManagerPrefab;

    void Start()
    {
        EventDispatcher.RegisterFunction<string>("JoinRoom", JoinRoomManual);
        // Rastgele katılma için yeni bir register
        EventDispatcher.RegisterFunction("JoinRandomOrCreate", JoinRandomRoomOrCreateAuto);
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    // Oda oluşturma işlemi
    public void CreateCustomRoom(
        string roomName,
        string password,
        int totalRounds,
        bool isPasswordProtected,
        bool fillWithBots = false
    )
    {
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)maxPlayers,
            CleanupCacheOnLeave = cleanupCacheOnLeave,
            EmptyRoomTtl = emptyRoomTtl,
            PlayerTtl =
                60000 // Yeniden bağlanma için 1 dakika süre
            ,
        };

        Hashtable roomProps = new Hashtable();
        roomProps.Add("Password", isPasswordProtected ? password : "");
        roomProps.Add("TotalRounds", totalRounds);
        roomProps.Add("CurrentRound", 1);
        roomProps.Add("FillWithBots", fillWithBots); // SeatManager okur: boş koltuklar botla dolar

        roomOptions.CustomRoomProperties = roomProps;
        roomOptions.CustomRoomPropertiesForLobby = new string[] { "Password", "TotalRounds" };

        SetPickSeat(false);
        PhotonNetwork.CreateRoom(roomName, roomOptions, TypedLobby.Default);
    }

    // Odayı listeden bilerek seçen koltuğunu bekleme ekranında kendisi seçer; kuran ve rastgele gelen ilk boş koltuğa oturur.
    // Odaya girmeden ayarlanan özellik girişle birlikte gider; master yeni gelene buna göre koltuk verir (SeatManager).
    public const string PickSeatKey = "PickSeat";

    // Oyuncu özellikleri odadan çıkınca silinmez: önceki odanın koltuk/sıra bilgisi yeni odaya taşınmasın (null = sil).
    static void SetPickSeat(bool pick) =>
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable { { PickSeatKey, pick }, { "SeatNumber", null }, { "PlayerQue", null } }
        );

    // JOIN ROOM Butonu için: Önce rastgele şifresiz oda dener, yoksa kurar
    public void JoinRandomRoomOrCreateAuto()
    {
        // Sadece şifresi boş ("") olan odaları filtreliyoruz
        ExitGames.Client.Photon.Hashtable expectedProps = new ExitGames.Client.Photon.Hashtable
        {
            { "Password", "" },
        };
        SetPickSeat(false);
        PhotonNetwork.JoinRandomRoom(expectedProps, (byte)maxPlayers);
    }

    // Eğer JoinRandomRoom başarısız olursa (Oda yoksa) Photon bu callback'i çağırır
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("Uygun şifresiz oda bulunamadı, varsayılan oda kuruluyor...");
        // Kural: Oyuncu adı, şifresiz, 1 el
        CreateCustomRoom(PhotonNetwork.NickName, "", 1, false);
    }

    // Scripts/Photons/RoomManager.cs
    // Scripts/Photons/RoomManager.cs içindeki ilgili metod:
    public void JoinRoomWithPassword(
        string _roomName,
        string enteredPassword,
        string actualPassword
    )
    {
        // Boşlukları temizleyerek karşılaştırıyoruz
        string cleanEntered = enteredPassword.Trim();
        string cleanActual = actualPassword.Trim();

        Debug.Log(
            $"[Giriş Denemesi] Oda: '{_roomName}' | Girilen: '{cleanEntered}' | Beklenen: '{cleanActual}'"
        );

        if (cleanEntered == cleanActual)
        {
            SetPickSeat(true);
            PhotonNetwork.JoinRoom(_roomName); // Artık isim ve şifre kesinlikle doğru
        }
        else
        {
            Debug.LogError("Hatalı Şifre! Girilen: " + cleanEntered);
        }
    }

    // Listeden tıklayarak katılma (Şifreli odalar için ileride buraya şifre sorma eklenecek)
    public void JoinRoomManual(string _roomName)
    {
        SetPickSeat(true);
        PhotonNetwork.JoinRoom(_roomName);
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Odaya girildi. Eski puanlar temizleniyor...");

        Hashtable resetProps = new Hashtable();
        resetProps["PlayerScore"] = 0;
        PhotonNetwork.LocalPlayer.SetCustomProperties(resetProps);

        SceneChangeManager.Instance.ChangeScene("Table");
    }
}
