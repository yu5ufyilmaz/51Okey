using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class TileUI : MonoBehaviourPunCallbacks, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    #region Serialized Fields

    [SerializeField]
    private ScoreManager scoreManager;

    [SerializeField]
    private List<Tiles> playerTiles;

    [SerializeField]
    private Image tileImage;

    [SerializeField]
    private float moveSpeed = 5f; // Hareket hızı ayarı
    #endregion

    #region Private Fields
    private Transform originalParent;
    private CanvasGroup canvasGroup;
    private TurnManager turnManager;
    private TileDistrubite tileDistrubite;

    // Container Transformları
    public Transform middleTileContainer; // Orta taş havuzu
    public Transform rightTileContainer; // Sağ taş alanı
    public Transform leftTileContainer; // Sol taş alanı
    public Transform playerTileContainer; // Oyuncu taşı bölmesi
    private Transform playerMeldContainer;

    public int tileRow;
    public int tileColumn;

    // Durum Değişkenleri
    public Tiles tileDataInfo;
    public int tilePlaceInt;
    private string spritePath = "Sprites/Tiles";
    private string spriteName;
    private bool inMiddle = false;
    private bool fromLeftContainer = false;
    public bool isIndicatorTile = false;
    #endregion
    #region Awake ve Start
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        originalParent = transform.parent;
    }

    private void Start()
    {
        middleTileContainer = GameObject.Find("MiddleTileContainer").transform;
        rightTileContainer = GameObject.Find("RightTileContainer").transform;
        leftTileContainer = GameObject.FindWithTag("LeftTileContainer").transform;
        playerTileContainer = GameObject.Find("PlayerTileContainer").transform;
        playerMeldContainer = GameObject.Find("TileMeldPlace").transform;
        CheckPlace();
        if (gameObject.transform.parent == middleTileContainer)
        {
            inMiddle = true;
        }
        turnManager = GameObject.Find("TurnManager").GetComponent<TurnManager>();
        tileDistrubite = GameObject.Find("TileManager(Clone)").GetComponent<TileDistrubite>();
        scoreManager = GameObject.Find("ScoreManager(Clone)").GetComponent<ScoreManager>();
        playerTiles = null;
        playerTiles = tileDistrubite.GetPlayerTiles();
        tileDistrubite.RegisterTileUI(this);
        scoreManager.CheckForPer();
        FitToParent();
    }

    void CheckPlace()
    {
        if (transform.parent.parent == playerTileContainer)
        {
            tilePlaceInt = transform.parent.GetSiblingIndex();
        }
        else
        {
            Debug.LogWarning("Current transform is not a child of playerTileContainer");
        }
    }

    public void CheckRowColoumn(int rowIndex, int columnIndex)
    {
        tileRow = rowIndex;
        tileColumn = columnIndex;
    }
    #endregion

    #region Tile Set UI
    public void SetTileData(Tiles tileData)
    {
        if (tileData != null && tileImage != null)
        {
            tileDataInfo = tileData;
            // Sprite adını oluştur
            if (tileData.type == TileType.FakeJoker)
                spriteName = "FakeJoker";
            else if (tileData.type == TileType.Joker)
                spriteName = "Empty";
            else
                spriteName = tileData.color.ToString() + "_" + tileData.number.ToString();

            // Sprite'ı Resources klasöründen yükle
            Sprite loadedSprite = Resources.Load<Sprite>($"{spritePath}/{spriteName}");

            // Eğer sprite başarıyla yüklendiyse, Image bileşenine ekle
            if (loadedSprite != null)
            {
                tileImage.sprite = loadedSprite;
            }
            else
            {
                Debug.LogError($"Sprite bulunamadı: {spritePath}/{spriteName}");
            }
        }
        else
        {
            Debug.LogError("TileData veya TileImage eksik!");
        }
    }

    // TileUI.cs içine eklenecek TEK VE SON metod:

    // TileUI.cs içine:

    public void FitToParent()
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            // 1. Pivotu Ortala
            rt.pivot = new Vector2(0.5f, 0.5f);

            // 2. Anchors'ı "Stretch" (Tam Kapla) yap
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;

            // 3. SIFIR NOKTASI: Kenar boşluklarını ve boyutu sıfırla
            // (Ebeveyn ne kadarsa o kadar ol demektir)
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // 4. SCALE ZORLAMASI: En önemli kısım burası!
            transform.localScale = Vector3.one;
            transform.localRotation = Quaternion.identity;
            transform.localPosition = Vector3.zero;
        }
    }
    #endregion

    #region DRAG_HANDLERS

    private (bool canDrop, bool canDraw) CanMoveTile()
    {
        if (isIndicatorTile)
        {
            Debug.LogWarning("Gösterge taşını hareket ettiremezsiniz!");
            return (false, false);
        }

        if (!turnManager.IsPlayerTurn())
        {
            if (gameObject.transform.parent == middleTileContainer)
            {
                Debug.LogWarning("Şu an taşı çekemezsiniz!");
                return (false, false);
            }
            else
                return (true, true);
        }
        else
        {
            if (
                gameObject.transform.parent == middleTileContainer
                || gameObject.transform.parent == leftTileContainer
            )
            {
                if (playerTiles.Count >= 15)
                {
                    Debug.LogWarning("Şu an taşı çekemezsiniz!");
                    return (false, false);
                }
                else
                    return (true, true);
            }
            else
                return (true, true); // Taş atılabilir, taş çekilebilir
        }
    }

    #region On Begin Drag
    public void OnBeginDrag(PointerEventData eventData)
    {
        // 1. Hareket İzni Kontrolü (TurnManager vb.)
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDraw)
        {
            Debug.LogWarning("Bu taşı şu an hareket ettiremezsiniz!");
            eventData.pointerDrag = null;
            return;
        }

        // 2. Rakip Alan ve Gösterge Kontrolü
        if (
            gameObject.transform.parent.tag == "OtherSideTileContainer"
            || isIndicatorTile
            || gameObject.transform.parent == rightTileContainer
        )
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!");
            eventData.pointerDrag = null;
            return;
        }

        // --- 3. KRİTİK DÜZELTME: MASADAKİ (MELD) TAŞLARI KİLİTLEME ---

        Transform currentParent = transform.parent; // Taşın içinde olduğu Placeholder
        Transform grandParent = currentParent.parent; // Placeholder'ın bağlı olduğu Container

        // Eğer taşın "Büyük Babası" (GrandParent) Istaka (PlayerTileContainer) DEĞİLSE...
        // (Yani taş ıstakada durmuyorsa)
        if (grandParent != playerTileContainer)
        {
            // Ve taş Orta, Sol veya Sağ atma alanında da değilse...
            // (Yani çekilebilir bir taş da değilse)
            if (
                currentParent != middleTileContainer
                && currentParent != leftTileContainer
                && currentParent != rightTileContainer
            )
            {
                // O ZAMAN BU TAŞ MASAYA AÇILMIŞ BİR TAŞTIR! DOKUNMA!
                Debug.LogWarning("Masaya işlenmiş taşları hareket ettiremezsiniz!");
                eventData.pointerDrag = null; // Sürüklemeyi anında iptal et
                return;
            }
        }
        // --------------------------------------------------------------

        // 4. Sol Konteyner Kontrolü
        if (gameObject.transform.parent == leftTileContainer)
        {
            fromLeftContainer = true;
        }

        // --- SÜRÜKLEME BAŞLATILIYOR ---
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root, true);
    }
    #endregion
    #region On Drag
    public void OnDrag(PointerEventData eventData)
    {
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDrop)
        {
            Debug.LogWarning("Taş atılamaz!"); // Hata ayıklama logu
            return; // Taş atılamıyorsa çık
        }
        if (!canDraw)
        {
            Debug.LogWarning("Taş çekilemez!"); // Hata ayıklama logu
            return; // Taş atılamıyorsa çık
        }
        if (gameObject.transform.parent.tag == "OtherSideTileContainer")
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!"); // Hata ayıklama logu
            return;
        }

        // Taş çekilebilir durumda ise
        if (isIndicatorTile || gameObject.transform.parent == rightTileContainer)
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!"); // Hata ayıklama logu
            return;
        }

        transform.position = Input.mousePosition;
    }
    #endregion
    #region On End Drag
    public void OnEndDrag(PointerEventData eventData)
    {
        // 1. YETKİSİZ ALAN KONTROLÜ
        // Rakip alanındaki veya gösterge taşları hareket ettirilemez.
        if (gameObject.transform.parent.tag == "OtherSideTileContainer" || isIndicatorTile)
        {
            StartCoroutine(SmoothMove(transform, originalParent));
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!");
            return;
        }

        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        canvasGroup.blocksRaycasts = true;

        Transform parentContainer = playerTileContainer;
        Transform closestPlaceholder = null;
        float closestDistance = float.MaxValue;

        // En yakın "Placeholder" (Boş Kutu) Bulma
        foreach (Transform placeholder in parentContainer)
        {
            if (placeholder.CompareTag("Placeholder"))
            {
                float distance = Vector3.Distance(placeholder.position, transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlaceholder = placeholder;
                }
            }
        }

        // GEÇERLİ BİR YER BULUNDUYSA VE MESAFE UYGUNSA
        if (closestPlaceholder != null && closestDistance < 40f)
        {
            if (turnManager.IsPlayerTurn() == true) // SIRA BİZDE Mİ?
            {
                // -----------------------------------------------------------
                // AŞAMA 1: TAŞ ÇEKME (HENÜZ ÇEKİLMEMİŞSE)
                // -----------------------------------------------------------
                if (turnManager.canDrop == false)
                {
                    // A) ORTADAN ÇEKME
                    if (inMiddle == true)
                    {
                        // --- GÜVENLİK KONTROLÜ: LİSTE BOŞ MU? ---
                        if (tileDistrubite.allTiles.Count == 0)
                        {
                            Debug.LogWarning("Ortada çekilecek taş kalmadı! Oyun BİTİRİLİYOR.");

                            // YENİ EKLENEN KISIM: Oyunu Bitir Sinyali Gönder
                            // -1 gönderiyoruz çünkü kazanan yok (taş bitti).
                            // Bu RPC herkesin ekranında FinishGameRPC'yi çalıştıracak.
                            if (GameManager.Instance != null && !GameManager.Instance.isGameEnded)
                            {
                                GameManager.Instance.photonView.RPC(
                                    "FinishGameRPC",
                                    RpcTarget.All,
                                    -1,
                                    false,
                                    false
                                );
                            }

                            StartCoroutine(SmoothMove(transform, originalParent));
                            return;
                        }
                        // ----------------------------------------


                        SetTileData(tileDistrubite.allTiles[0]);
                        tileDistrubite.photonView.RPC(
                            "AddTileFromMiddlePlayerList",
                            RpcTarget.AllBuffered,
                            queueValue
                        );

                        turnManager.canDrop = true; // Artık atabilir
                        inMiddle = false;

                        StartCoroutine(SmoothMove(transform, closestPlaceholder));
                        Debug.Log("Orta desteden taş çekildi.");
                    }
                    // B) SOLDAN (YANDAN) ÇEKME
                    else if (fromLeftContainer == true)
                    {
                        // 1. Durumları İşaretle
                        turnManager.hasPickedFromSide = true; // Yandan alındı
                        turnManager.hasOpenedThisTurn = false; // Henüz açmadı
                        turnManager.hasProcessedThisTurn = false; // Henüz işlemedi

                        // 2. GameManager'a Haber Ver (İade butonu ve ceza takibi için)
                        EventDispatcher.SummonEvent("OnSideTilePicked", this.tileDataInfo);

                        // 3. Görsel ve Verisel İşlemler
                        StartCoroutine(SmoothMove(transform, closestPlaceholder));
                        tileDistrubite.photonView.RPC(
                            "AddTileFromDropPlayerList",
                            RpcTarget.AllBuffered,
                            queueValue
                        );

                        turnManager.canDrop = true;
                        tileDistrubite.dropTile = this.tileDataInfo;
                        fromLeftContainer = false;

                        Debug.Log("Yandan taş çekildi. Kural: Açmak veya işlemek zorundasınız!");
                    }
                    // C) SADECE YER DEĞİŞTİRME (Henüz çekmedi, elini düzenliyor)
                    else
                    {
                        // Çekmeden atamaz
                        if (
                            closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight
                            == true
                        )
                        {
                            Debug.LogWarning("Şu an taş atamazsın, önce taş çekmelisin.");
                            StartCoroutine(SmoothMove(transform, originalParent));
                            return;
                        }
                        else
                        {
                            StartCoroutine(SmoothMove(transform, closestPlaceholder));
                        }
                    }
                }
                // -----------------------------------------------------------
                // AŞAMA 2: TAŞ ATMA / İŞLEME (ZATEN ÇEKİLMİŞSE)
                // -----------------------------------------------------------
                else
                {
                    // D) TEKRAR ÇEKMEYE ÇALIŞMA HATASI
                    if (
                        gameObject.transform.parent == middleTileContainer
                        || fromLeftContainer == true
                    )
                    {
                        Debug.LogWarning(
                            "Zaten taş çektiniz, elinizde 15 taş var. Birini atmalısınız."
                        );
                        StartCoroutine(SmoothMove(transform, originalParent));
                        return;
                    }
                    else
                    {
                        // E) TAŞ ATMA (TUR BİTİRME - SAĞ TARAFA SÜRÜKLEME)
                        if (
                            closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight
                            == true
                        )
                        {
                            // KURAL: Yandan aldıysa -> (Açmış OLMALI) VEYA (İşlemiş OLMALI)
                            if (!turnManager.CanFinishTurn())
                            {
                                Debug.LogError(
                                    "KURAL İHLALİ: Yandan taş aldınız ama ne açtınız ne de işlediniz!"
                                );

                                // 1. GameManager üzerinden başarısızlık senaryosunu (Ceza + İade) çalıştır
                                GameManager.Instance.HandleFailedSidePick(this.tileDataInfo);

                                // 2. Görsel olarak taşı iptal et (GameManager zaten veriyi silecek ve düzeltecek)
                                StartCoroutine(SmoothMove(transform, originalParent));
                                return;
                            }

                            // BAŞARILI HAMLE
                            Debug.Log("Taş atılıyor, sıra değişecek.");

                            // Event: Taş atıldı (GameManager işlek/okey cezası kontrolü yapacak)
                            EventDispatcher.SummonEvent("OnTileThrown", this.tileDataInfo);

                            NextTurnEvents(); // Turu bitir ve işlemleri yap
                        }
                        // TileUI.cs -> OnEndDrag -> F Bloğu (GÜNCELLENMİŞ)

                        else if (
                            closestPlaceholder.gameObject.GetComponent<Placeholder>().available
                            == true
                        )
                        {
                            // ... (Son taş kontrolü kodu burada duracak) ...

                            Tiles reqTile = closestPlaceholder
                                .gameObject.GetComponent<Placeholder>()
                                .AvailableTileInfo;

                            if (
                                reqTile.color == tileDataInfo.color
                                && reqTile.number == tileDataInfo.number
                            )
                            {
                                turnManager.hasProcessedThisTurn = true;

                                if (turnManager.hasPickedFromSide)
                                    Debug.Log("Taş işlendi. Ceza kalktı.");

                                // 1. Görsel Hareket (Taşı masaya götür)
                                StartCoroutine(SmoothMove(transform, closestPlaceholder));

                                // 2. [YENİ] Verisel Silme (Eldeki listeden düş)
                                // Bu taş artık elde değil, masada. Listeden silinmesi lazım ki "Count" azalsın.
                                // Not: Görseli Destroy etmiyoruz çünkü SmoothMove ile masaya taşıdık.
                                // Sadece veri listesinden siliyoruz.

                                PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                                    "PlayerQue",
                                    out object qVal
                                );
                                int pQue = (int)qVal;

                                // Sadece VERİDEN silmek için özel bir RPC veya Local işlem yapmalıyız.
                                // RemoveTileFromPlayerListByValue görseli de silmeye çalışır ama
                                // taşın parent'ı değiştiği için (PlayerContainer değil artık) görseli bulamaz ve sadece veriyi siler.
                                // Bu tam istediğimiz şey!

                                tileDistrubite.photonView.RPC(
                                    "RemoveTileFromPlayerListByValue",
                                    RpcTarget.AllBuffered,
                                    pQue,
                                    tileDataInfo
                                );

                                // 3. Masaya İşlendiğini Sisteme Kaydet (Active Placement)
                                // Eğer ScoreManager activeTiles kullanıyorsa buraya eklenmeli.
                                // Ancak senin yapında görsel olarak oraya gitmesi yeterli görünüyorsa RPC ile herkese
                                // "Bu taş buraya gitti" demen gerekebilir.
                                // Şimdilik SmoothMove sadece sende çalışır.
                                // DOĞRUSU: Bu taşın oraya gittiğini diğerlerine de bildirmen lazım.

                                // Basit Çözüm: Taşı işlediğinde ScoreManager üzerinden tüm masayı güncellemek.
                                // Veya ActiveTilePlacementInfo oluşturup yollamak.
                                // Şimdilik en azından elinden silinmesini sağladık.
                            }
                            else
                            {
                                Debug.Log("Yanlış taşı işlemeye çalışıyorsun.");
                                StartCoroutine(SmoothMove(transform, originalParent));
                            }
                        }
                        // G) ISTAKA İÇİNDE YER DEĞİŞTİRME (SIRALAMA)
                        else
                        {
                            StartCoroutine(SmoothMove(transform, closestPlaceholder));
                        }
                    }
                }
            }
            else // SIRA OYUNCUDA DEĞİLSE
            {
                // Sadece yer değiştirebilir
                if (
                    gameObject.transform.parent == middleTileContainer
                    || gameObject.transform.parent == leftTileContainer
                )
                {
                    StartCoroutine(SmoothMove(transform, originalParent));
                    Debug.LogWarning("Sıra sende değil, taş çekemezsin.");
                    return;
                }
                else
                {
                    if (closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight != true)
                    {
                        StartCoroutine(SmoothMove(transform, closestPlaceholder));
                    }
                    else
                    {
                        StartCoroutine(SmoothMove(transform, originalParent));
                        Debug.Log("Sıra sende değil, taş atamazsın.");
                        return;
                    }
                }
            }

            // --- KAYDIRMA (SHIFT) MANTIĞI ---
            // Sadece Istaka içi hareketlerde çalışır (Diğer taşları sağa/sola kaydırma)
            if (closestPlaceholder.parent == playerTileContainer)
            {
                if (closestPlaceholder.childCount > 1)
                {
                    // Masadaki taşların üzerine bırakılamaz
                    if (closestPlaceholder.parent != playerTileContainer)
                    {
                        StartCoroutine(SmoothMove(transform, originalParent));
                        return;
                    }

                    if (closestPlaceholder.gameObject.GetComponent<Placeholder>().isRight == false)
                    {
                        Transform displacedTile = closestPlaceholder.GetChild(0);
                        int targetIndex = closestPlaceholder.GetSiblingIndex();

                        if (targetIndex < originalParent.GetSiblingIndex())
                        {
                            ShiftTilesRight(playerTileContainer, displacedTile, targetIndex + 1);
                        }
                        else
                        {
                            ShiftTilesLeft(playerTileContainer, displacedTile, targetIndex - 1);
                        }
                    }
                }
            }
        }
        else // GEÇERSİZ BİR YERE BIRAKILDIYSA (BOŞLUĞA)
        {
            StartCoroutine(SmoothMove(transform, originalParent));
        }
    }
    #endregion
    #endregion

    // TileUI.cs -> NextTurnEvents (TAM VE GÜVENLİ HALİ)

    void NextTurnEvents()
    {
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int playerQueInt = (int)queueValue;

        // -----------------------------------------------------------------------
        // 1. OYUN BİTİŞ KONTROLÜ İÇİN VERİYİ HAZIRLA
        // -----------------------------------------------------------------------

        // Kopyasını al (Gerçek listeyi bozmamak için)
        List<Tiles> currentHandForCheck = new List<Tiles>(playerTiles);

        // A) ATILAN TAŞI DÜŞ
        var tileInHand = currentHandForCheck.FirstOrDefault(t =>
            t.color == tileDataInfo.color
            && t.number == tileDataInfo.number
            && t.type == tileDataInfo.type
        );

        if (tileInHand != null)
        {
            currentHandForCheck.Remove(tileInHand);
        }
        else
        {
            // Eğer referans hatası olursa ve elde 1 taş varsa, o taş atılandır.
            if (playerTiles.Count == 1)
                currentHandForCheck.Clear();
        }

        // --- KRİTİK DÜZELTME BAŞLANGICI ---
        // B) MASAYA AÇILMIŞ AMA HENÜZ COMMİT EDİLMEMİŞ TAŞLARI DA DÜŞ
        // Eğer oyuncu bu tur per açtıysa, o taşlar henüz elinden silinmedi (CommitAndStoreMelds aşağıda çağrılıyor).
        // Bu yüzden kontrol listesinden manuel olarak çıkarıyoruz.

        if (scoreManager != null)
        {
            // 1. Açılan Perler (Seri/Çift)
            if (
                scoreManager.pendingMeldedTiles != null
                && scoreManager.pendingMeldedTiles.Count > 0
            )
            {
                foreach (var meldedTile in scoreManager.pendingMeldedTiles)
                {
                    var itemToRemove = currentHandForCheck.FirstOrDefault(t =>
                        t.color == meldedTile.color
                        && t.number == meldedTile.number
                        && t.type == meldedTile.type
                    );

                    if (itemToRemove != null)
                    {
                        currentHandForCheck.Remove(itemToRemove);
                    }
                }
            }

            // 2. İşlenen Taşlar (Active Placements)
            // Eğer oyuncu işlek bir taş koyduysa onu da düşmeliyiz
            // Not: pendingActivePlacements ScoreManager'da private ise, public bir getter veya direkt erişim gerekebilir.
            // Eğer erişemiyorsan ScoreManager'a "public List<ActiveTilePlacementInfo> GetPendingActivePlacementsRef()" gibi bir metod ekle.
            // Şimdilik varsayım üzerinden gidiyorum, active taşlar genelde pendingMeldedTiles mantığına benzer.
        }
        // --- KRİTİK DÜZELTME SONU ---

        Debug.Log(
            $"Hamle Sonu (Açılanlar Dahil) Elde Kalan Tahmini Taş Sayısı: {currentHandForCheck.Count}"
        );

        // GameManager'a Bildir
        if (tileDistrubite != null)
        {
            HandData data = new HandData();
            data.actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            data.handTiles = currentHandForCheck; // Güncel (azalmış) el

            EventDispatcher.SummonEvent("OnPlayerMoveFinished", data);
        }
        // -----------------------------------------------------------------------

        // 2. GÖRSEL HAREKET
        StartCoroutine(SmoothMove(transform, rightTileContainer));

        // 3. RPC İLE SİLME (NESNEYİ İNDEKSE ÇEVİRME İŞLEMİ)
        List<Tiles> actualPlayerTiles = tileDistrubite.GetPlayerTiles();
        int indexToRemove = -1;

        for (int i = 0; i < actualPlayerTiles.Count; i++)
        {
            if (
                actualPlayerTiles[i].color == tileDataInfo.color
                && actualPlayerTiles[i].number == tileDataInfo.number
                && actualPlayerTiles[i].type == tileDataInfo.type
            )
            {
                indexToRemove = i;
                break;
            }
        }

        if (indexToRemove != -1)
        {
            tileDistrubite.photonView.RPC(
                "RemoveTileFromPlayerList",
                RpcTarget.All,
                queueValue,
                indexToRemove
            );
        }
        else
        {
            Debug.LogError("HATA: Atılan taş oyuncunun listesinde bulunamadı!");
        }

        // 4. DİĞER İŞLEMLER (Commit işlemi burada yapılıyor, yukarıdaki hesaplama o yüzden gerekliydi)
        tileDistrubite.photonView.RPC("CheckForAvailableTiles", RpcTarget.All, queueValue);
        scoreManager.CommitAndStoreMelds();
        scoreManager.CommitJokerTransactions();

        List<ActiveTilePlacementInfo> placements =
            scoreManager.GetAndClearPendingActivePlacements();

        if (placements != null && placements.Count > 0)
        {
            foreach (var placement in placements)
            {
                tileDistrubite.photonView.RPC(
                    "RemoveActiveTileFromPlayerList",
                    RpcTarget.All,
                    playerQueInt,
                    placement.tileData
                );
            }
            tileDistrubite.photonView.RPC(
                "InstantiateActiveTiles",
                RpcTarget.All,
                placements.ToArray()
            );
        }

        turnManager.canDrop = false;
        turnManager.photonView.RPC("NextTurn", RpcTarget.AllBuffered);

        Destroy(gameObject);
    }

    #region Shift_Tiles
    private void ShiftTilesRight(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        if (parentContainer != playerTileContainer)
            return;
        if (gameObject.transform.parent == middleTileContainer)
            return;
        if (gameObject.transform.parent.tag == "MeldPlaceholder")
            return;

        TileUI tileUI = tileToShift.GetComponent<TileUI>();
        if (tileUI != null && tileUI.isIndicatorTile)
        {
            Debug.LogWarning("Gösterge taşını kaydıramazsın!");
            return; // Metottan çık, kaydırma yapma.
        }
        for (int i = startIndex; i < parentContainer.childCount; i++)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);

            // Eğer sağ alan boşsa, taş buraya taşınabilir
            if (currentPlaceholder == rightTileContainer && rightTileContainer.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, rightTileContainer));

                return;
            }

            if (currentPlaceholder.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));

                return;
            }
            else
            {
                Transform nextTileToShift = currentPlaceholder.GetChild(0);
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));

                tileToShift = nextTileToShift;
            }
        }
    }

    private void ShiftTilesLeft(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        if (parentContainer != playerTileContainer)
        {
            return;
        }
        if (gameObject.transform.parent == middleTileContainer)
            return;
        if (gameObject.transform.parent.tag == "MeldPlaceholder")
            return;
        TileUI tileUI = tileToShift.GetComponent<TileUI>();
        if (tileUI != null && tileUI.isIndicatorTile)
        {
            Debug.LogWarning("Gösterge taşını kaydıramazsın!");
            return; // Metottan çık, kaydırma yapma.
        }
        for (int i = startIndex; i >= 0; i--)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);
            if (currentPlaceholder == rightTileContainer && rightTileContainer.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, rightTileContainer));

                return;
            }
            if (currentPlaceholder.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));

                return;
            }
            else
            {
                Transform nextTileToShift = currentPlaceholder.GetChild(0);
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));

                tileToShift = nextTileToShift;
            }
        }
    }
    #endregion

    #region SmoothMove
    private IEnumerator SmoothMove(Transform tile, Transform targetPlaceholder)
    {
        Vector3 startPos = tile.position;
        Vector3 targetPos = targetPlaceholder.position;
        float elapsedTime = 0f;
        float journeyTime = 0.5f / moveSpeed;

        while (elapsedTime < journeyTime)
        {
            tile.position = Vector3.Lerp(startPos, targetPos, (elapsedTime / journeyTime));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        tile.SetParent(targetPlaceholder, false);
        FitToParent();
        CheckPlace();
        scoreManager.CheckForPer();
        tile.localPosition = Vector3.zero;
        tile.localScale = Vector3.one;
    }
    #endregion
}
