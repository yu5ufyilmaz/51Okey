using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SocialPlatforms.Impl;
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

    [Header("Scaling Settings")]
    private Vector3 _originalScale; // Taşın ilk boyutu
    private Vector3 _targetScale; // Olması gereken boyut
    public float scaleSpeed = 15f; // Büyüme/Küçülme hızı
    private List<Placeholder> allMeldPlaceholders = new List<Placeholder>();
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
        _originalScale = transform.localScale;
        _targetScale = _originalScale;
        Placeholder[] allPh = FindObjectsOfType<Placeholder>();

        // Sadece 'isMeldArea' kutucuğu işaretli olanları listemize ekle
        foreach (Placeholder ph in allPh)
        {
            if (ph.isMeldArea)
            {
                allMeldPlaceholders.Add(ph);
            }
        }
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
    void Update()
    {
        if (Vector3.Distance(transform.localScale, _targetScale) < 0.01f)
        {
            // Tam hedef boyuta eşitle ki küsuratlı kalmasın
            if (transform.localScale != _targetScale)
                transform.localScale = _targetScale;

            return; // Fonksiyondan çık, aşağıdaki işlemi yapma!
        }

        // Sadece boyut farkı varsa burası çalışır
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            _targetScale,
            Time.deltaTime * scaleSpeed
        );
    }

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
        _targetScale = _originalScale;
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
    [Header("Scaling Settings")]
    [SerializeField]
    private Vector3 shrinkScale = new Vector3(0.3f, 0.3f, 0.3f); // Küçülme boyutu

    public void OnDrag(PointerEventData eventData)
    {
        // --- 1. GÜVENLİK KONTROLLERİ (SENİN ORİJİNAL KODLARIN) ---
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDrop)
        {
            Debug.LogWarning("Taş atılamaz!");
            return;
        }
        if (!canDraw)
        {
            Debug.LogWarning("Taş çekilemez!");
            return;
        }
        if (gameObject.transform.parent.tag == "OtherSideTileContainer")
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!");
            return;
        }

        if (isIndicatorTile || gameObject.transform.parent == rightTileContainer)
        {
            Debug.LogWarning("Buradaki taşı hareket ettiremezsiniz!");
            return;
        }
        // --- HAREKET ---
        transform.position = Input.mousePosition;

        // --- SCALING MANTIĞI ---
        // Artık hem senin hem rakiplerin meld alanlarını algılar
        Placeholder visualTarget = GetVisualClosestPlaceholder(100f);

        if (visualTarget != null)
        {
            // Mesafe içindeyiz, küçül
            if (_targetScale != shrinkScale)
            {
                _targetScale = shrinkScale;
            }
        }
        else
        {
            // Mesafe dışındayız, büyü
            if (_targetScale != _originalScale)
            {
                _targetScale = _originalScale;
            }
        }
    }
    #endregion
    #region On End Drag
    [Header("Drag Settings")]
    [SerializeField]
    private float dropDistanceThreshold = 100f;

    public void OnEndDrag(PointerEventData eventData)
    {
        // --- 1. TEMEL GÜVENLİK KONTROLLERİ ---
        if (transform.parent.CompareTag("OtherSideTileContainer") || isIndicatorTile)
        {
            _targetScale = _originalScale;
            StartCoroutine(SmoothMove(transform, originalParent));
            return;
        }

        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int localQueInt = (int)queueValue;
        canvasGroup.blocksRaycasts = true;

        // --- 2. HEDEF TESPİTİ (MESAFE BAZLI) ---
        Placeholder bestTarget = null;
        float closestDistance = float.MaxValue;

        // A) Kendi Istakamızdaki Kutuları Tara
        foreach (Transform t in playerTileContainer)
        {
            float dist = Vector2.Distance(t.position, transform.position);
            if (dist < 100f && dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = t.GetComponent<Placeholder>();
            }
        }

        // B) Eğer Istakada Yer Bulamadıysak Masadaki (Meld) veya Atma Alanına Bak
        if (bestTarget == null)
        {
            Placeholder[] allPlaceholders = FindObjectsOfType<Placeholder>();
            foreach (Placeholder ph in allPlaceholders)
            {
                if (ph.isMeldArea || ph.isRight)
                {
                    float dist = Vector2.Distance(ph.transform.position, transform.position);
                    if (dist < 100f && dist < closestDistance)
                    {
                        closestDistance = dist;
                        bestTarget = ph;
                    }
                }
            }
        }

        // Hiçbir yer bulunamadıysa eski yerine dön
        if (bestTarget == null)
        {
            _targetScale = _originalScale;
            StartCoroutine(SmoothMove(transform, originalParent));
            return;
        }

        // TileUI.cs -> OnEndDrag -> if (bestTarget.isRight) bloğunun içi:

        // DURUM A: TAŞ ATMA (SAĞ TARAF)
        if (bestTarget.isRight)
        {
            if (turnManager.IsPlayerTurn() && turnManager.canDrop)
            {
                // --- [REFERANS BAĞLANTISI BURADA] ---
                // TurnManager'a soruyoruz: "Turu bitirmeme izin var mı?"
                // (Yandan aldıysam açtım mı/işledim mi kontrolünü o yapıyor)
                if (!turnManager.CanFinishTurn())
                {
                    Debug.LogWarning(
                        "KURAL HATASI: Yandan taş aldınız ama açmadınız/işlemediniz. İade ediliyor."
                    );

                    // Cezayı uygula ve taşı geri al
                    GameManager.Instance.HandleFailedSidePick(this.tileDataInfo);

                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, originalParent));
                    return; // İŞLEM İPTAL
                }

                // ... (Geri kalan kodlar aynı: Ceza kontrolü ve Tur devretme) ...

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.photonView.RPC(
                        "CheckPenaltyRPC",
                        RpcTarget.MasterClient,
                        localQueInt,
                        this.tileDataInfo
                    );
                }
                ExecuteNextTurn();
            }
            else
            {
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, originalParent));
            }
        }
        // DURUM B: KENDİ ISTAKASINDA DÜZENLEME VEYA TAŞ ÇEKME
        else if (bestTarget.transform.parent == playerTileContainer)
        {
            if (turnManager.IsPlayerTurn() && !turnManager.canDrop)
            {
                if (inMiddle) // Ortadan çekme
                {
                    SetTileData(tileDistrubite.allTiles[0]);
                    tileDistrubite.photonView.RPC(
                        "AddTileFromMiddlePlayerList",
                        RpcTarget.AllBuffered,
                        localQueInt
                    );
                    turnManager.canDrop = true;
                    inMiddle = false;
                }
                else if (fromLeftContainer) // Yandan çekme
                {
                    turnManager.hasPickedFromSide = true;
                    EventDispatcher.SummonEvent("OnSideTilePicked", this.tileDataInfo);
                    tileDistrubite.photonView.RPC(
                        "AddTileFromDropPlayerList",
                        RpcTarget.AllBuffered,
                        localQueInt
                    );
                    turnManager.canDrop = true;
                    fromLeftContainer = false;
                }
            }

            // Kendi ıstakasında kaydırma mantığı
            if (
                bestTarget.transform.childCount > 0
                && bestTarget != originalParent.GetComponent<Placeholder>()
            )
            {
                Transform displacedTile = bestTarget.transform.GetChild(0);
                int targetIndex = bestTarget.transform.GetSiblingIndex();
                int originalIndex = originalParent.GetSiblingIndex();

                if (targetIndex < originalIndex)
                    ShiftTilesRight(playerTileContainer, displacedTile, targetIndex + 1);
                else
                    ShiftTilesLeft(playerTileContainer, displacedTile, targetIndex - 1);
            }

            _targetScale = _originalScale;
            StartCoroutine(SmoothMove(transform, bestTarget.transform));
        }
        else
        {
            if (
                !turnManager.IsPlayerTurn()
                || (!scoreManager.hasOpenedSeries && !scoreManager.hasOpenedPairs)
            )
            {
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, originalParent));
                return;
            }

            // İşlenecek taş verisi
            Tiles beingProcessed = this.tileDataInfo;

            // ScoreManager'a sor: Bu taşı buraya koyabilir miyim?
            // NOT: ProcessManualDrop içindeki "PerformTileProcessing" metodu
            // zaten masaya YENİ bir görsel koyuyor.
            bool success = scoreManager.ProcessManualDrop(
                beingProcessed,
                bestTarget.transform,
                localQueInt
            );

            if (success)
            {
                // BAŞARILI OLDUYSA:
                // Sadece bu objeyi GİZLE (Yok etme!). Çünkü asıl yok etme işlemi
                // TileDistrubite'in listeyi güncellediği an yapılmalı.
                // Ama oyuncuya "takılma" hissi vermemek için görseli kapatıyoruz.
                gameObject.SetActive(false);

                // Buradaki kritik nokta: RemoveActiveTileFromPlayerList RPC'si
                // ScoreManager tarafından zaten çağrılıyor (PerformTileProcessing içinde).
                // O yüzden burada ekstra bir RPC çağırmıyoruz!

                // Destroy(gameObject); // BU SATIRI SİLİYORUZ!
            }
            else
            {
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, originalParent));
            }
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

        // 1. ELDEN TAŞ DÜŞME MANTIĞI (Aynı kalıyor)
        List<Tiles> realTimePlayerTiles = tileDistrubite.GetPlayerTiles();
        List<Tiles> currentHandForCheck = new List<Tiles>(realTimePlayerTiles);

        // Atılan taşı listeden düş
        var tileThrown = currentHandForCheck.FirstOrDefault(t =>
            t.color == tileDataInfo.color
            && t.number == tileDataInfo.number
            && t.type == tileDataInfo.type
        );
        if (tileThrown != null)
            currentHandForCheck.Remove(tileThrown);

        // Yeni açılan perleri düş
        if (scoreManager != null)
        {
            List<Tiles> pendingTiles = scoreManager.GetAllTilesPendingCommit();
            foreach (var pt in pendingTiles)
            {
                var it = currentHandForCheck.FirstOrDefault(t =>
                    t.color == pt.color && t.number == pt.number && t.type == pt.type
                );
                if (it != null)
                    currentHandForCheck.Remove(it);
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

        if (isGameReallyOver)
        {
            Destroy(gameObject);
            yield break;
        }

        // --- GÖRSEL HAREKET ---
        StartCoroutine(SmoothMove(transform, rightTileContainer));
        yield return new WaitForSeconds(0.05f);

        // --- VERİTABANINDAN SİLME (ATILAN TAŞ) ---
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

        // --- MASA GÜNCELLEMELERİ VE SENKRONİZASYON ---
        tileDistrubite.photonView.RPC("CheckForAvailableTiles", RpcTarget.All, playerQueInt);
        scoreManager.CommitAndStoreMelds();
        scoreManager.CommitJokerTransactions();

        // ** KRİTİK NOKTA: BEKLEYEN İŞLEMELERİ ŞİMDİ GÖNDER **
        scoreManager.ExecutePendingSyncs();

        yield return new WaitForSeconds(0.05f);

        // --- OYUN BİTİŞ / SIRA DEVRETME ---
        if (tileDistrubite.allTiles.Count == 0)
        {
            if (GameManager.Instance != null && !GameManager.Instance.isGameEnded)
                GameManager.Instance.photonView.RPC(
                    "FinishGameRPC",
                    RpcTarget.All,
                    -1,
                    false,
                    false
                );
            Destroy(gameObject);
            yield break;
        }

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
            _targetScale = Vector3.one;
        }

        // Hareket bitti, değişkeni temizle
        currentMoveCoroutine = null;
    }
    #endregion

    // TileUI.cs dosyasının en altındaki metodu bununla değiştir:
    private Placeholder GetVisualClosestPlaceholder(float detectionRadius = 100f)
    {
        Placeholder bestTarget = null;
        float closestDistanceSqr = detectionRadius * detectionRadius; // Karesini alıyoruz (Performans için)
        Vector3 currentPos = transform.position;

        // Start'ta doldurduğumuz "isMeldArea" listesini tara
        foreach (Placeholder ph in allMeldPlaceholders)
        {
            if (ph == null)
                continue;
            // Eğer placeholder kapalıysa (inactive) hesaplama
            if (!ph.gameObject.activeInHierarchy)
                continue;

            // Mesafeye bak
            float dSqr = (ph.transform.position - currentPos).sqrMagnitude;

            if (dSqr < closestDistanceSqr)
            {
                closestDistanceSqr = dSqr;
                bestTarget = ph;
            }
        }

        return bestTarget;
    }
}
