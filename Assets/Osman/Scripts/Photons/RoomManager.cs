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
        bool isPasswordProtected
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

        roomOptions.CustomRoomProperties = roomProps;
        roomOptions.CustomRoomPropertiesForLobby = new string[] { "Password", "TotalRounds" };

        PhotonNetwork.CreateRoom(roomName, roomOptions, TypedLobby.Default);
    }

    // JOIN ROOM Butonu için: Önce rastgele şifresiz oda dener, yoksa kurar
    public void JoinRandomRoomOrCreateAuto()
    {
        // Sadece şifresi boş ("") olan odaları filtreliyoruz
        ExitGames.Client.Photon.Hashtable expectedProps = new ExitGames.Client.Photon.Hashtable
        {
            { "Password", "" },
        };
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
