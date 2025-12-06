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
    private Coroutine currentMoveCoroutine;

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
        if (currentMoveCoroutine != null)
        {
            StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine = null;
        }
        // --- 1. SOL TARAFTAN ÇEKME KONTROLÜ (EN BAŞA KOYUYORUZ) ---
        // Eğer taş Soldaki (Yandaki) kutudaysa...
        if (transform.parent == leftTileContainer)
        {
            // A) SIRA SENDE Mİ?
            if (!turnManager.IsPlayerTurn())
            {
                Debug.LogWarning("Sıra sende değil! Yandaki taşa dokunamazsın.");
                eventData.pointerDrag = null; // Sürüklemeyi iptal et
                return;
            }

            // B) ZATEN TAŞ ÇEKTİN Mİ? (Ortadan veya Yandan)
            // canDrop == true ise, elinde atılacak taş var demektir, yani çekmişsindir.
            if (turnManager.canDrop == true)
            {
                Debug.LogWarning("Zaten taş çektiniz! Yandan alamazsınız.");
                eventData.pointerDrag = null; // Sürüklemeyi iptal et
                return;
            }

            // Şartlar uygunsa işaretle
            fromLeftContainer = true;
        }
        // -----------------------------------------------------------

        // 2. Genel Hareket İzni (Istaka içi düzenleme vs. için)
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDraw)
        {
            // Eğer soldan çekmeye çalışıyorsak yukarıdaki blok zaten yakalardı.
            // Burası daha çok ıstaka içi veya orta taş için genel kontrol.
            Debug.LogWarning("Bu taşı şu an hareket ettiremezsiniz!");
            eventData.pointerDrag = null;
            return;
        }

        // 3. Yasaklı Alanlar (Rakip, Gösterge, Atma Yeri)
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

        // 4. Masadaki (Meld) Taşları Kilitleme
        Transform currentParent = transform.parent;
        Transform grandParent = currentParent.parent;

        // Eğer taşın dedesi PlayerTileContainer değilse (yani ıstakada değilse)
        if (grandParent != playerTileContainer)
        {
            // Ve bu taş Çekilebilir Alanlarda da (Orta, Sol) değilse...
            if (
                currentParent != middleTileContainer
                && currentParent != leftTileContainer
                && currentParent != rightTileContainer
            )
            {
                // Demek ki masaya açılmış bir taş. KİLİTLE.
                Debug.LogWarning("Masaya işlenmiş taşları hareket ettiremezsiniz!");
                eventData.pointerDrag = null;
                return;
            }
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
    [Header("Drag Settings")]
    [SerializeField]
    private float dropDistanceThreshold = 100f;

    public void OnEndDrag(PointerEventData eventData)
    {
        // 1. YETKİSİZ ALAN KONTROLÜ
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

        // En yakın kutucuğu bul
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
        if (closestPlaceholder != null && closestDistance < dropDistanceThreshold)
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
                        // --- [KRİTİK] ORTADA TAŞ BİTTİ Mİ KONTROLÜ ---
                        if (tileDistrubite.allTiles.Count == 0)
                        {
                            Debug.LogWarning("Ortada çekilecek taş kalmadı! Oyun BİTİRİLİYOR.");

                            // Oyunu Bitir Sinyali (-1: Kazanan Yok, Berabere/Bitti)
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
                        // ---------------------------------------------

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
                        turnManager.hasPickedFromSide = true;
                        turnManager.hasOpenedThisTurn = false;
                        turnManager.hasProcessedThisTurn = false;

                        EventDispatcher.SummonEvent("OnSideTilePicked", this.tileDataInfo);

                        StartCoroutine(SmoothMove(transform, closestPlaceholder));
                        tileDistrubite.photonView.RPC(
                            "AddTileFromDropPlayerList",
                            RpcTarget.AllBuffered,
                            queueValue
                        );

                        turnManager.canDrop = true;
                        tileDistrubite.dropTile = this.tileDataInfo;
                        fromLeftContainer = false;

                        Debug.Log("Yandan taş çekildi.");
                    }
                    // C) SADECE YER DEĞİŞTİRME (Henüz çekmedi)
                    else
                    {
                        // Çekmeden sağa atamaz
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
                    // D) TEKRAR ÇEKMEYE ÇALIŞMA
                    if (
                        gameObject.transform.parent == middleTileContainer
                        || fromLeftContainer == true
                    )
                    {
                        Debug.LogWarning("Zaten taş çektiniz.");
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
                            if (!turnManager.CanFinishTurn())
                            {
                                Debug.LogError("KURAL İHLALİ: Yandan taş aldınız ama açmadınız!");
                                GameManager.Instance.HandleFailedSidePick(this.tileDataInfo);
                                StartCoroutine(SmoothMove(transform, originalParent));
                                return;
                            }

                            Debug.Log("Taş atılıyor, sıra değişecek.");

                            // --- [YENİ] CEZA KONTROLÜ İÇİN RPC GÖNDER ---
                            // EventDispatcher yerine direkt Master Client'a "Ben (queueValue) bu taşı attım, kontrol et" diyoruz.
                            if (GameManager.Instance != null)
                            {
                                int myQue = 0;
                                if (
                                    PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                                        "PlayerQue",
                                        out object q
                                    )
                                )
                                    myQue = (int)q;

                                GameManager.Instance.photonView.RPC(
                                    "CheckPenaltyRPC",
                                    RpcTarget.MasterClient, // Sadece Master hesaplasın
                                    myQue, // BENİM ID'M (Yanlış kişiye yazılmasın diye)
                                    this.tileDataInfo // Attığım Taş
                                );
                            }
                            ExecuteNextTurn();
                        }
                        // F) MASAYA İŞLEME (Available Yerlere)
                        else if (
                            closestPlaceholder.gameObject.GetComponent<Placeholder>().available
                            == true
                        )
                        {
                            // (İşleme kodları ActivePers içinde otomatik yapılıyor, burası manuel sürükleme için)
                            // Buradaki manuel işleme mantığını ActivePers ile senkronize etmek lazım ama
                            // şu anlık basit bir geri atma yapalım, işlemek için butonu kullansınlar.
                            // Veya buraya da ActivePers mantığı eklenebilir.

                            // Şimdilik kafa karıştırmasın diye yerine oturtuyorum.
                            // Eğer manuel sürükleyerek işleme yapıyorsan buradaki kodların ActivePers ile aynı olmalı.
                            // Ama genellikle "İşle" butonu daha sağlıklıdır.

                            Debug.Log("Manuel işleme denemesi (Buton kullanılması önerilir).");
                            StartCoroutine(SmoothMove(transform, originalParent));
                        }
                        // G) ISTAKA İÇİNDE YER DEĞİŞTİRME
                        else
                        {
                            StartCoroutine(SmoothMove(transform, closestPlaceholder));
                        }
                    }
                }
            }
            else // SIRA OYUNCUDA DEĞİLSE
            {
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
            if (closestPlaceholder.parent == playerTileContainer)
            {
                if (closestPlaceholder.childCount > 1)
                {
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
        else // GEÇERSİZ BİR YERE BIRAKILDIYSA
        {
            StartCoroutine(SmoothMove(transform, originalParent));
        }
    }
    #endregion
    #endregion

    #region Next Turn Event
    // 1. BU METODU ÇAĞIRACAKSIN (Eski NextTurnEvents yerine)
    private void ExecuteNextTurn()
    {
        // Coroutine başlatıyoruz ki işlemleri zamana yayabilelim
        StartCoroutine(NextTurnRoutine());
    }

    // TileUI.cs içindeki NextTurnRoutine metodunu bununla değiştir:

    private IEnumerator NextTurnRoutine()
    {
        if (canvasGroup != null)
            canvasGroup.blocksRaycasts = false;

        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int playerQueInt = (int)queueValue;

        // --- 1. OYUN BİTİŞ KONTROLÜ (ELİM BİTTİ Mİ?) ---
        List<Tiles> realTimePlayerTiles = tileDistrubite.GetPlayerTiles();
        List<Tiles> currentHandForCheck = new List<Tiles>(realTimePlayerTiles);

        var tileThrown = currentHandForCheck.FirstOrDefault(t =>
            t.color == tileDataInfo.color
            && t.number == tileDataInfo.number
            && t.type == tileDataInfo.type
        );

        if (tileThrown != null)
            currentHandForCheck.Remove(tileThrown);

        if (scoreManager != null)
        {
            List<Tiles> pendingTiles = scoreManager.GetAllTilesPendingCommit();
            foreach (var pendingTile in pendingTiles)
            {
                var itemToRemove = currentHandForCheck.FirstOrDefault(t =>
                    t.color == pendingTile.color
                    && t.number == pendingTile.number
                    && t.type == pendingTile.type
                );
                if (itemToRemove != null)
                    currentHandForCheck.Remove(itemToRemove);
            }
        }

        bool isGameReallyOver = (currentHandForCheck.Count == 0);

        // GameManager'a Bildir
        if (tileDistrubite != null)
        {
            HandData data = new HandData();
            data.actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            data.handTiles = isGameReallyOver
                ? new List<Tiles>()
                : new List<Tiles> { new Tiles(TileColor.black, 1, TileType.Number) };

            EventDispatcher.SummonEvent("OnPlayerMoveFinished", data);
        }

        // --- ELİM BİTTİYSE ÇIK ---
        if (isGameReallyOver)
        {
            Destroy(gameObject);
            yield break;
        }

        // --- 2. İŞLEMLER ---
        StartCoroutine(SmoothMove(transform, rightTileContainer));
        yield return new WaitForSeconds(0.05f);

        // Taşı Sil
        int indexToRemove = -1;
        List<Tiles> actualPlayerTiles = tileDistrubite.GetPlayerTiles();
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
            tileDistrubite.photonView.RPC(
                "RemoveTileFromPlayerList",
                RpcTarget.All,
                playerQueInt,
                indexToRemove
            );

        yield return new WaitForSeconds(0.05f);

        // Masa Güncellemeleri
        tileDistrubite.photonView.RPC("CheckForAvailableTiles", RpcTarget.All, playerQueInt);
        scoreManager.CommitAndStoreMelds();
        scoreManager.CommitJokerTransactions();

        yield return new WaitForSeconds(0.05f);

        var placements = scoreManager.GetAndClearPendingActivePlacements();
        if (placements != null && placements.Count > 0)
        {
            foreach (var p in placements)
            {
                tileDistrubite.photonView.RPC(
                    "RemoveActiveTileFromPlayerList",
                    RpcTarget.All,
                    playerQueInt,
                    p.tileData
                );
                yield return new WaitForEndOfFrame();
            }
            tileDistrubite.photonView.RPC(
                "InstantiateActiveTiles",
                RpcTarget.All,
                placements.ToArray()
            );
        }

        yield return new WaitForSeconds(0.05f);

        // --- [YENİ EKLENEN KISIM] ORTADA TAŞ KALDI MI? ---
        // Eğer ortada taş sayısı 0 ise, sırayı devretme, OYUNU BİTİR (Beraberlik).
        if (tileDistrubite.allTiles.Count == 0)
        {
            Debug.LogWarning("Hamle yapıldı ve ortada taş kalmadı. Oyun BERABERE bitiyor.");

            if (GameManager.Instance != null && !GameManager.Instance.isGameEnded)
            {
                // -1 Kazanan Yok (Berabere) demektir.
                GameManager.Instance.photonView.RPC(
                    "FinishGameRPC",
                    RpcTarget.All,
                    -1,
                    false,
                    false
                );
            }

            Destroy(gameObject);
            yield break; // Fonksiyonu burada kes, NextTurn çalışmasın.
        }
        // ------------------------------------------------

        // Eğer taş varsa sırayı devret
        turnManager.canDrop = false;
        turnManager.photonView.RPC("NextTurn", RpcTarget.AllBuffered);

        Destroy(gameObject);
    }
    #endregion
    #region Shift_Tiles
    private void ShiftTilesRight(Transform parentContainer, Transform tileToShift, int startIndex)
    {
        // ... (İlk kontroller aynı) ...
        if (parentContainer != playerTileContainer)
            return;
        if (gameObject.transform.parent == middleTileContainer)
            return;
        if (gameObject.transform.parent.tag == "MeldPlaceholder")
            return;

        TileUI tileUI = tileToShift.GetComponent<TileUI>();
        if (tileUI != null && tileUI.isIndicatorTile)
            return;

        // --- 1. DUVAR ANALİZİ (LOGLU) ---
        int wallIndex = -1;
        for (int i = startIndex; i < parentContainer.childCount; i++)
        {
            Placeholder ph = parentContainer.GetChild(i).GetComponent<Placeholder>();
            if (ph != null && ph.isRight)
            {
                wallIndex = i;
                // LOG: Duvarın yerini tespit et
                Debug.Log(
                    $"[LOG] Duvar (isRight) tespit edildi. Index: {wallIndex}, Name: {ph.name}"
                );
                break;
            }
        }
        if (wallIndex == -1)
            wallIndex = parentContainer.childCount;

        // --- 2. YER KONTROLÜ ---
        bool hasSpace = false;
        for (int i = startIndex; i < wallIndex; i++)
        {
            if (parentContainer.GetChild(i).childCount == 0)
            {
                hasSpace = true;
                break;
            }
        }

        // --- 3. KARAR ANI (LOGLU) ---
        if (!hasSpace)
        {
            Debug.LogWarning(
                $"[LOG] SAĞ TARAF DOLU! StartIndex: {startIndex}, DuvarIndex: {wallIndex}. İşlem İPTAL ediliyor."
            );

            if (tileToShift == transform)
            {
                StartCoroutine(SmoothMove(transform, originalParent));
            }
            return; // ÇIK, KİMSE KIPIRDAMASIN.
        }

        // -------------------------------------------------------------
        // 4. KAYDIRMA (LOGLU)
        // -------------------------------------------------------------
        for (int i = startIndex; i < wallIndex; i++)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);

            // LOG: Hangi kutuya işlem yapıyoruz?
            Placeholder checkPh = currentPlaceholder.GetComponent<Placeholder>();
            if (checkPh != null && checkPh.isRight)
            {
                Debug.LogError(
                    $"[KRİTİK HATA] Kod 'isRight' olan yere girmeye çalıştı! Index: {i}, Name: {currentPlaceholder.name}"
                );
                return; // ACİL ÇIKIŞ
            }

            // A) BOŞLUK
            if (currentPlaceholder.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));
                Debug.Log($"[LOG] Taş {i}. indekse yerleşti.");
                return;
            }
            // B) DOLU
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
        // 1. Temel Kontroller
        if (parentContainer != playerTileContainer)
            return;
        if (gameObject.transform.parent == middleTileContainer)
            return;
        if (gameObject.transform.parent.tag == "MeldPlaceholder")
            return;

        TileUI tileUI = tileToShift.GetComponent<TileUI>();
        if (tileUI != null && tileUI.isIndicatorTile)
            return;

        // --- [YENİ] SOL TARAFTA BOŞLUK VAR MI? ---
        // StartIndex'ten 0'a kadar olan kısımda en az 1 tane BOŞLUK lazım.
        // Eğer hiç boşluk yoksa, en soldaki (0. indeks) taş boşluğa düşer ve kaybolur.

        bool hasSpace = false;

        for (int i = startIndex; i >= 0; i--)
        {
            if (parentContainer.GetChild(i).childCount == 0)
            {
                hasSpace = true;
                break; // Yer bulduk!
            }
        }

        // --- KARAR ANI ---
        if (!hasSpace)
        {
            Debug.LogWarning(
                "Sol taraf tamamen dolu! Kaydırma yapılırsa taş kaybolacak. İŞLEM İPTAL."
            );

            // Eğer bu fonksiyonu çağıran, elimizdeki sürüklenen taş ise onu eski yerine gönder.
            if (tileToShift == transform)
            {
                StartCoroutine(SmoothMove(transform, originalParent));
            }
            return; // ÇIK, HİÇBİR TAŞI OYNATMA.
        }

        // -------------------------------------------------------------
        // GÜVENLİ SOLA KAYDIRMA
        // -------------------------------------------------------------
        for (int i = startIndex; i >= 0; i--)
        {
            Transform currentPlaceholder = parentContainer.GetChild(i);

            // Yasaklı Alan Kontrolü (Atma Yeri vb.)
            Placeholder ph = currentPlaceholder.GetComponent<Placeholder>();
            if (ph != null && ph.isRight)
                return; // Sol tarafta isRight olmaz ama güvenlik olsun.

            // A) BOŞLUK BULUNDU
            if (currentPlaceholder.childCount == 0)
            {
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));
                return; // Taş yerleşti, zincir bitti.
            }
            // B) DOLU İSE ZİNCİRLEME DEVAM
            else
            {
                // Buradaki taşı eline al
                Transform nextTileToShift = currentPlaceholder.GetChild(0);

                // Elimdeki taşı buraya koy
                StartCoroutine(SmoothMove(tileToShift, currentPlaceholder));

                // Eline aldığın taşla bir sonraki (daha soldaki) kutuya git
                tileToShift = nextTileToShift;
            }
        }
    }
    #endregion

    #region SmoothMove
    private void StartSmoothMove(Transform tile, Transform target)
    {
        // Eğer halihazırda çalışan bir hareket varsa, anında DURDUR.
        if (currentMoveCoroutine != null)
        {
            StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine = null;
        }
        // Yeni hareketi başlat ve referansını sakla
        currentMoveCoroutine = StartCoroutine(SmoothMove(tile, target));
    }

    private IEnumerator SmoothMove(Transform tile, Transform targetPlaceholder)
    {
        Vector3 startPos = tile.position;
        Vector3 targetPos = targetPlaceholder.position;
        float elapsedTime = 0f;
        // Hız 0 gelirse donmasın diye güvenlik
        float speed = (moveSpeed > 0) ? moveSpeed : 5f;
        float journeyTime = 0.5f / speed;

        while (elapsedTime < journeyTime)
        {
            // Eğer taş yok olduysa döngüyü kır
            if (tile == null)
                yield break;

            tile.position = Vector3.Lerp(startPos, targetPos, (elapsedTime / journeyTime));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (tile != null)
        {
            tile.SetParent(targetPlaceholder, false);
            FitToParent();
            CheckPlace();
            if (scoreManager != null)
                scoreManager.CheckForPer();

            tile.localPosition = Vector3.zero;
            tile.localScale = Vector3.one;
        }

        // Hareket bitti, değişkeni temizle
        currentMoveCoroutine = null;
    }
    #endregion
}
