using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class RoomManager : MonoBehaviourPunCallbacks
{
    // Canlı denemeyle doğrulandı: PlayerTtl sınırsız kabul edilir, EmptyRoomTtl üst sınırı 300000 (fazlası hata verir).
    public const int PlayerTtlMs = 7200000;
    public const int EmptyRoomTtlMs = 300000;

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

    // Photon çağrıyı eşzamanlı reddederse (istemci hazır değil) geri çağrı hiç gelmez; yükleme açık kalmasın
    static void Refused()
    {
        UIRoot.HideLoading();
        UIRoot.Toast(Loc.T("toast.wait_connection"));
    }

    // Oda oluşturma işlemi
    public void CreateCustomRoom(
        string roomName,
        string password,
        int totalRounds,
        bool isPasswordProtected,
        bool fillWithBots = false,
        bool hidden = false
    )
    {
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = (byte)maxPlayers,
            IsVisible = !hidden, // Bot masası lobi listesinde ve rastgele katılmada görünmez
            CleanupCacheOnLeave = cleanupCacheOnLeave,
            EmptyRoomTtl = EmptyRoomTtlMs,
            PlayerTtl = PlayerTtlMs,
        };
        Debug.Log($"[ODA] PlayerTtl={roomOptions.PlayerTtl} EmptyRoomTtl={roomOptions.EmptyRoomTtl}");

        Hashtable roomProps = new Hashtable();
        // Şifrenin kendisi hiçbir oda özelliğine yazılmaz; lobi yalnız özeti görür.
        roomProps.Add(PwHashKey, isPasswordProtected ? RoomPassword.Hash(roomName, password) : "");
        roomProps.Add("TotalRounds", totalRounds);
        roomProps.Add("CurrentRound", 1);
        roomProps.Add("FillWithBots", fillWithBots); // SeatManager okur: boş koltuklar botla dolar
        // Lobiye yalnız kurulurken var olan anahtarlar gider; koltukları SeatManager (master) günceller.
        roomProps.Add(SeatsKey, new[] { "", "", "", "" });

        roomOptions.CustomRoomProperties = roomProps;
        roomOptions.CustomRoomPropertiesForLobby = new string[] { PwHashKey, "TotalRounds", SeatsKey };

        SetPickSeat(false);
        UIRoot.ShowLoading(Loc.T("room.creating"));
        if (!PhotonNetwork.CreateRoom(roomName, roomOptions, TypedLobby.Default)) Refused();
    }

    // Odayı listeden bilerek seçen koltuğunu bekleme ekranında kendisi seçer; kuran ve rastgele gelen ilk boş koltuğa oturur.
    // Odaya girmeden ayarlanan özellik girişle birlikte gider; master yeni gelene buna göre koltuk verir (SeatManager).
    public const string PickSeatKey = "PickSeat";

    // Lobiden koltuğa dokunarak gelenin istediği koltuk (oyuncu özelliği); SeatManager masada bir kez okur ve siler.
    public const string WantedSeatKey = "WantedSeat";

    // Oda özellikleri: şifre özeti (şifresizse "") ve koltuk -> ad dizisi (string[4], boş koltuk "").
    public const string PwHashKey = "PwHash";
    public const string SeatsKey = "Seats";

    // Oyuncu özellikleri odadan çıkınca silinmez: önceki odanın koltuk/sıra bilgisi yeni odaya taşınmasın (null = sil).
    static void SetPickSeat(bool pick, int wantedSeat = 0) =>
        PhotonNetwork.LocalPlayer.SetCustomProperties(
            new Hashtable
            {
                { PickSeatKey, pick },
                { "SeatNumber", null },
                { "PlayerQue", null },
                { WantedSeatKey, wantedSeat > 0 ? (object)wantedSeat : null },
            }
        );

    // Aynı adda masa varken kurma reddedilmesin: ada dört rakam eklenir.
    public static string UniqueRoomName(string baseName) =>
        baseName + "-" + Random.Range(0, 10000).ToString("D4");

    // "Botlarla oyna": şifresiz, 1 el, listede görünmez; boş koltuklar botla dolar.
    public void CreateBotRoom() =>
        CreateCustomRoom(UniqueRoomName(PhotonNetwork.NickName), "", 1, false, fillWithBots: true, hidden: true);

    // JOIN ROOM Butonu için: Önce rastgele şifresiz oda dener, yoksa kurar
    public void JoinRandomRoomOrCreateAuto()
    {
        // Sadece şifresi boş ("") olan odaları filtreliyoruz
        ExitGames.Client.Photon.Hashtable expectedProps = new ExitGames.Client.Photon.Hashtable
        {
            { PwHashKey, "" },
        };
        SetPickSeat(false);
        UIRoot.ShowLoading(Loc.T("room.joining"));
        if (!PhotonNetwork.JoinRandomRoom(expectedProps, (byte)maxPlayers)) Refused();
    }

    // Eğer JoinRandomRoom başarısız olursa (Oda yoksa) Photon bu callback'i çağırır
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Debug.Log("Uygun şifresiz oda bulunamadı, varsayılan oda kuruluyor...");
        // Kural: Oyuncu adı, şifresiz, 1 el
        CreateCustomRoom(UniqueRoomName(PhotonNetwork.NickName), "", 1, false);
    }

    // Scripts/Photons/RoomManager.cs
    // Scripts/Photons/RoomManager.cs içindeki ilgili metod:
    public bool JoinRoomWithPassword(
        string _roomName,
        string enteredPassword,
        string passwordHash,
        int seat = 0
    )
    {
        // Girilen şifrenin özeti odanınkiyle karşılaştırılır (boşluklar temizlenir)
        if (RoomPassword.Matches(_roomName, enteredPassword, passwordHash))
        {
            JoinRoomAtSeat(_roomName, seat); // Artık isim ve şifre kesinlikle doğru
            return true;
        }
        UIRoot.Toast(Loc.T("toast.wrong_password"));
        return false;
    }

    // Listeden tıklayarak katılma (Şifreli odalar için ileride buraya şifre sorma eklenecek)
    public void JoinRoomManual(string _roomName) => JoinRoomAtSeat(_roomName, 0);

    // Odayı seçen oturmadan gelir; seat > 0 ise masada o koltuğu ister (SeatManager).
    public void JoinRoomAtSeat(string roomName, int seat)
    {
        SetPickSeat(true, seat);
        UIRoot.ShowLoading(Loc.T("room.joining"));
        if (!PhotonNetwork.JoinRoom(roomName)) Refused();
    }

    public override void OnJoinedRoom()
    {
        // Dönüş: masa sahnesini PUN yükler; puan sıfırlama ve ikinci sahne yüklemesi taze girişe aittir.
        if (PhotonNetwork.LocalPlayer.HasRejoined)
            return;
        Debug.Log("Odaya girildi. Eski puanlar temizleniyor...");

        Hashtable resetProps = new Hashtable();
        resetProps["PlayerScore"] = 0;
        PhotonNetwork.LocalPlayer.SetCustomProperties(resetProps);

        SceneChangeManager.Instance.ChangeScene("Table");
    }
}
