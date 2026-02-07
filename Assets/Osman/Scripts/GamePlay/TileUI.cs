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

    private Vector3 _dragOffset; // Tıkladığın yer ile taşın merkezi arasındaki fark
    private bool _isAnimating = false; // OPTİMİZASYON: Sadece hareket varken true olur
    private Vector3 _currentVelocity; // SmoothDamp için
    private float _targetRotationZ;

    [Header("Game Feel")]
    [SerializeField]
    private float dragDamping = 0.01f; // Yumuşak takip (0.03 - 0.05 ideal)

    [SerializeField]
    private float slideDuration = 0.3f;

    [SerializeField]
    private float tiltAmount = 0.8f; // Eğilme katsayısı

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
    private RectTransform _parentRect; // Referans düzlemimiz
    private Vector3 _targetScale; // Olması gereken boyut
    public float scaleSpeed = 15f; // Büyüme/Küçülme hızı
    private List<Placeholder> allMeldPlaceholders = new List<Placeholder>();

    #region YENİ GÖRSEL AYARLAR (Burayı ekle)
    [Header("Game Feel - Hissiyat Ayarları")]
    [SerializeField]
    private float tiltStrength = 4f; // Sürüklerken ne kadar eğilsin?

    [SerializeField]
    private float tiltSpeed = 10f; // Eğilme hızı

    [SerializeField]
    private AnimationCurve slideCurve = AnimationCurve.EaseInOut(0, 0, 1, 1); // Kayma eğrisi

    // Drag Hesaplamaları için private değişkenler
    private Quaternion _defaultRotation; // Orijinal rotasyon
    #endregion
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
        // OPTİMİZASYON KİLİDİ: Eğer taş sürüklenmiyorsa VE boyutu/yeri oturmuşsa çalışma!
        if (!_isAnimating && canvasGroup.blocksRaycasts)
            return;

        bool isMoving = false; // Hala hareket var mı kontrolü

        // 1. SCALE ANIMASYONU
        if (Vector3.Distance(transform.localScale, _targetScale) > 0.001f)
        {
            transform.localScale = Vector3.Lerp(
                transform.localScale,
                _targetScale,
                Time.deltaTime * scaleSpeed
            );
            isMoving = true;
        }
        else
        {
            transform.localScale = _targetScale; // Küsuratı temizle
        }

        // 2. TILT (EĞİLME) EFEKTİ (Sadece sürüklerken)
        if (!canvasGroup.blocksRaycasts) // Sürükleniyor demektir
        {
            // Hedef rotasyona yumuşak geçiş
            Quaternion targetRot = Quaternion.Euler(0, 0, _targetRotationZ);
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * 15f
            );
            isMoving = true;
        }
        else if (transform.rotation != Quaternion.identity) // Sürükleme bitti, düzeliyor
        {
            // Eski haline (düz) dön
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.identity,
                Time.deltaTime * 15f
            );

            // Eğer çok yaklaştıysa tam düzelt ve işlemi bitir
            if (Quaternion.Angle(transform.rotation, Quaternion.identity) < 0.5f)
            {
                transform.rotation = Quaternion.identity;
            }
            else
            {
                isMoving = true; // Hala düzelmeye çalışıyor
            }
        }

        // Eğer hiçbir hareket kalmadıysa Update döngüsünü boşa çalıştırma
        _isAnimating = isMoving;
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
    #region On Begin Drag
    #region On Begin Drag
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (currentMoveCoroutine != null)
        {
            StopCoroutine(currentMoveCoroutine);
            currentMoveCoroutine = null;
        }
        _targetScale = _originalScale;

        // --- 1. SOL TARAFTAN ÇEKME KONTROLÜ ---
        if (transform.parent == leftTileContainer)
        {
            // A) SIRA SENDE Mİ?
            if (!turnManager.IsPlayerTurn())
            {
                Debug.LogWarning("Sıra sende değil! Yandaki taşa dokunamazsın.");
                eventData.pointerDrag = null;
                return;
            }

            // B) ZATEN TAŞ ÇEKTİN Mİ? (canDrop true ise çekmişsindir)
            if (turnManager.canDrop == true)
            {
                Debug.LogWarning("Zaten taş çektiniz! Yandan alamazsınız.");
                eventData.pointerDrag = null;
                return;
            }

            // C) AÇTIN MI / İŞLEDİN Mİ? (Yandan almak da bir çekme işlemidir)
            if (turnManager.hasOpenedThisTurn || turnManager.hasProcessedThisTurn)
            {
                Debug.LogWarning("El açtınız veya işlediniz, yandan taş alamazsınız!");
                eventData.pointerDrag = null;
                return;
            }

            // Şartlar uygunsa işaretle
            fromLeftContainer = true;
        }
        // --- 2. ORTA TAŞLARDAN ÇEKME KONTROLÜ (DÜZELTİLDİ) ---
        else if (
            transform.parent == middleTileContainer
            || transform.parent.CompareTag("MiddleTileContainer")
        )
        {
            // A) SIRA SENDE Mİ?
            if (!turnManager.IsPlayerTurn())
            {
                Debug.LogWarning("Sıra sende değil!");
                eventData.pointerDrag = null;
                return;
            }

            // B) KURAL İHLALİ KONTROLLERİ
            // - Zaten taş çektiyse (canDrop = true) -> DÜZELTİLEN KISIM BURASI
            // - Bu tur elini açtıysa (hasOpenedThisTurn)
            // - Bu tur yere taş işlediyse (hasProcessedThisTurn)
            if (
                turnManager.canDrop
                || turnManager.hasOpenedThisTurn
                || turnManager.hasProcessedThisTurn
            )
            {
                Debug.LogWarning(
                    "Hamle yaptınız (Çektiniz/Açtınız/İşlediniz), tekrar taş çekemezsiniz!"
                );
                eventData.pointerDrag = null;
                return;
            }

            // C) EL KAPASİTESİ KONTROLÜ (15 taş kuralı)
            if (playerTiles.Count >= 15)
            {
                Debug.LogWarning("Eliniz dolu (15 Taş), yeni taş çekemezsiniz!");
                eventData.pointerDrag = null;
                return;
            }
        }

        // --- 3. GENEL HAREKET İZNİ (CanMoveTile) ---
        var (canDrop, canDraw) = CanMoveTile();

        if (!canDraw)
        {
            Debug.LogWarning("Bu taşı şu an hareket ettiremezsiniz!");
            eventData.pointerDrag = null;
            return;
        }

        // --- 4. YASAKLI ALANLAR ---
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

        // --- 5. MASADAKİ (MELD) TAŞLARI KİLİTLEME ---
        Transform currentParent = transform.parent;
        Transform grandParent = currentParent.parent;

        if (
            grandParent != playerTileContainer
            && currentParent != middleTileContainer
            && currentParent != leftTileContainer
            && currentParent != rightTileContainer
        )
        {
            Debug.LogWarning("Masaya işlenmiş taşları hareket ettiremezsiniz!");
            eventData.pointerDrag = null;
            return;
        }
        _parentRect = transform.parent.GetComponent<RectTransform>();

        Vector3 worldPoint;
        // Tıklanan ekran noktasını, kutucuğun dünyadaki düzlemine çevir
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            _parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out worldPoint
        );

        // Aradaki farkı kaydet (Offset)
        _dragOffset = transform.position - worldPoint;

        // Diğer ayarlar
        _isAnimating = true;
        _currentVelocity = Vector3.zero;
        _targetRotationZ = 0f;
        // --- SÜRÜKLEME BAŞLATILIYOR ---
        originalParent = transform.parent;
        canvasGroup.blocksRaycasts = false;
        transform.SetParent(transform.root, true);
        _currentVelocity = Vector3.zero; // Hızı sıfırla
        _targetRotationZ = 0f;
    }
    #endregion
    #endregion
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
        Vector3 globalMousePos;

        // Yine parentRect referansına göre hesapla (Taş hareket etse de parent sabit!)
        if (
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _parentRect,
                eventData.position,
                eventData.pressEventCamera,
                out globalMousePos
            )
        )
        {
            // Hedef = Mouse'un Gerçek Yeri + İlk Tuttuğumuz Fark
            Vector3 targetPos = globalMousePos + _dragOffset;

            // Smooth Takip
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPos,
                ref _currentVelocity,
                dragDamping
            );

            // Tilt (Eğilme) Efekti
            float deltaX = eventData.delta.x;
            _targetRotationZ = Mathf.Clamp(-deltaX * tiltAmount, -10f, 10f);
        }

        _isAnimating = true;

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
        _targetRotationZ = 0f;
        _isAnimating = true;
        PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int localQueInt = (int)queueValue;
        canvasGroup.blocksRaycasts = true;

        // --- 2. HEDEF TESPİTİ ---
        Placeholder bestTarget = null;
        float closestDistance = float.MaxValue;

        // A) Kendi Istakamız
        foreach (Transform t in playerTileContainer)
        {
            float dist = Vector2.Distance(t.position, transform.position);
            if (dist < 100f && dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = t.GetComponent<Placeholder>();
            }
        }

        // B) Masa / Atma Alanı
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

        // Hedef yoksa eve dön
        if (bestTarget == null)
        {
            _targetScale = _originalScale;
            StartCoroutine(SmoothMove(transform, originalParent));
            return;
        }

        // --- 3. AKSİYONLAR ---

        // DURUM A: TAŞ ATMA (SAĞ TARAF)
        if (bestTarget.isRight)
        {
            if (turnManager.IsPlayerTurn() && turnManager.canDrop)
            {
                if (!turnManager.CanFinishTurn())
                {
                    Debug.LogWarning("KURAL HATASI: Açmadan bitiremezsiniz.");
                    GameManager.Instance.HandleFailedSidePick(this.tileDataInfo);
                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, originalParent));
                    return;
                }

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
        // DURUM B: KENDİ ISTAKAMIZ (SWAP ve AKILLI KAYDIRMA)
        else if (bestTarget.transform.parent == playerTileContainer)
        {
            // -- Taş Çekme Kontrolleri --
            if (turnManager.IsPlayerTurn() && !turnManager.canDrop)
            {
                if (inMiddle)
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
                else if (fromLeftContainer)
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

            // --- AKILLI KAYDIRMA VE SWAP İŞLEMİ ---

            // Bıraktığımız yer DOLU MU? ve orası zaten benim ESKİ YERİM DEĞİL Mİ?
            if (bestTarget.transform.childCount > 0 && bestTarget != originalParent.GetComponent<Placeholder>())
            {
                Transform residentTile = bestTarget.transform.GetChild(0);
                TileUI residentUI = residentTile.GetComponent<TileUI>();

                // Gösterge taşı kilitlidir, yerinden oynatılamaz.
                if (residentUI != null && residentUI.isIndicatorTile)
                {
                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, originalParent));
                    return;
                }

                // 1. ADIM: AKILLI BOŞLUK ARAMA
                Placeholder smartEmptySpot = FindSmartEmptyPlaceholder(
                    bestTarget.transform.parent, 
                    bestTarget.transform.GetSiblingIndex()
                );

                // Eğer uygun ve güvenli bir boşluk bulunduysa oraya kaydır
                if (smartEmptySpot != null)
                {
                    residentUI.StartCoroutine(residentUI.SmoothMove(residentTile, smartEmptySpot.transform));
                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, bestTarget.transform));
                }
                // Boşluk yoksa mecburen TAKAS (SWAP) yap
                else
                {
                    residentUI.StartCoroutine(residentUI.SmoothMove(residentTile, originalParent));
                    _targetScale = _originalScale;
                    StartCoroutine(SmoothMove(transform, bestTarget.transform));
                }
            }
            // Bıraktığımız yer BOŞSA -> Direkt yerleş
            else
            {
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, bestTarget.transform));
            }
        }
        // DURUM C: MASAYA İŞLEME (Aynı kalıyor)
        else
        {
            if (
                !turnManager.IsPlayerTurn()
                || !turnManager.canDrop
                || (!scoreManager.hasOpenedSeries && !scoreManager.hasOpenedPairs)
            )
            {
                if (!turnManager.canDrop)
                    Debug.LogWarning("Önce taş çekmelisiniz!");
                _targetScale = _originalScale;
                StartCoroutine(SmoothMove(transform, originalParent));
                return;
            }

            bool success = scoreManager.ProcessManualDrop(
                this.tileDataInfo,
                bestTarget.transform,
                localQueInt
            );

            if (success)
                gameObject.SetActive(false);
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
        if (GameManager.Instance.scoreManager != null)
        {
            PenaltySystem.Instance.CommitAllTurnPenalties();
            // Tur boyunca yaptığım işlemelerden doğan cezaları şimdi sunucuya gönderiyorum.
            GameManager.Instance.scoreManager.CommitFinalTableLimit();
        }
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

        // 1. ELDEN TAŞ DÜŞME MANTIĞI
        List<Tiles> realTimePlayerTiles = tileDistrubite.GetPlayerTiles();

        // Listeyi kopyalıyoruz ki orijinal veri bozulmadan simülasyon yapabilelim
        List<Tiles> currentHandForCheck = new List<Tiles>(realTimePlayerTiles);

        // --- [DÜZELTME 1] ID KONTROLÜ İLE TAŞI BUL ---
        // Eski yöntem (Renk/Sayı) yerine doğrudan benzersiz ID ile arıyoruz.
        var tileThrown = currentHandForCheck.FirstOrDefault(t => t.id == tileDataInfo.id);

        if (tileThrown != null)
        {
            currentHandForCheck.Remove(tileThrown);
        }
        else
        {
            Debug.LogError(
                $"[NextTurnRoutine] HATA: Atılan taş ID ({tileDataInfo.id}) listede bulunamadı!"
            );
        }

        // Yeni açılan perleri (henüz sunucuya gitmemiş olanları) de simülasyon listesinden düş
        if (scoreManager != null)
        {
            List<Tiles> pendingTiles = scoreManager.GetAllTilesPendingCommit();
            foreach (var pt in pendingTiles)
            {
                // Not: Perdeki taşlar için de ID kontrolü yapmak en sağlıklısıdır,
                // ancak ScoreManager yapına göre burada ID eşleşmesi yoksa şimdilik değer kontrolü kalabilir.
                // Eğer pendingTiles içindeki taşların ID'leri korunuyorsa burayı da ID'ye çevirebilirsin.
                var it = currentHandForCheck.FirstOrDefault(t =>
                    t.color == pt.color && t.number == pt.number && t.type == pt.type
                );
                // EĞER ID SİSTEMİN ScoreManager'DA TAM OTURDUYSA YUKARIDAKİ YERİNE ŞUNU KULLAN:
                // var it = currentHandForCheck.FirstOrDefault(t => t.id == pt.id);

                if (it != null)
                    currentHandForCheck.Remove(it);
            }
        }

        // El tamamen bitti mi? (0 taş kaldıysa bitmiştir)
        bool isGameReallyOver = (currentHandForCheck.Count == 0);

        // GameManager'a Bildir
        if (tileDistrubite != null)
        {
            HandData data = new HandData();
            data.actorNumber = PhotonNetwork.LocalPlayer.ActorNumber;
            // Eğer oyun bittiyse boş el gönder, bitmediyse göstermelik bir taş koy (kontrol için)
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

        // --- [DÜZELTME 2] VERİTABANINDAN SİLME (ATILAN TAŞ) ---
        // TileDistrubite üzerindeki gerçek listeden hangi indeksi sileceğimizi ID ile buluyoruz.
        int indexToRemove = -1;
        List<Tiles> actualPlayerTiles = tileDistrubite.GetPlayerTiles();

        for (int i = 0; i < actualPlayerTiles.Count; i++)
        {
            // ESKİ KOD: Renk ve Sayı kontrolü (Kimlik karmaşası yaratıyordu)
            // YENİ KOD: Sadece ID kontrolü
            if (actualPlayerTiles[i].id == tileDataInfo.id)
            {
                indexToRemove = i;
                break; // Doğru taşı bulduk, döngüden çık
            }
        }

        if (indexToRemove != -1)
        {
            tileDistrubite.photonView.RPC(
                "RemoveTileFromPlayerList",
                RpcTarget.All, // Herkeste silinsin
                playerQueInt,
                indexToRemove
            );
        }
        else
        {
            Debug.LogError(
                $"[KRİTİK HATA] Silinecek taşın ID'si ({tileDataInfo.id}) gerçek oyuncu listesinde bulunamadı!"
            );
        }

        yield return new WaitForSeconds(0.05f);

        // --- MASA GÜNCELLEMELERİ VE SENKRONİZASYON ---
        // Diğer oyuncular için "işlek taş" (available tiles) kontrolünü yenile
        tileDistrubite.photonView.RPC("CheckForAvailableTiles", RpcTarget.All, playerQueInt);

        scoreManager.CommitAndStoreMelds();
        scoreManager.CommitJokerTransactions();

        // ** KRİTİK NOKTA: BEKLEYEN İŞLEMELERİ ŞİMDİ GÖNDER **
        scoreManager.ExecutePendingSyncs();
        scoreManager.ClearTurnHistory();
        yield return new WaitForSeconds(0.05f);

        // --- OYUN BİTİŞ / SIRA DEVRETME ---
        // Ortada taş kalmadıysa beraberlik
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

        // Turu bitir, bayrakları sıfırla ve sırayı devret
        turnManager.canDrop = false;
        turnManager.photonView.RPC("NextTurn", RpcTarget.AllBuffered);

        // Görsel nesneyi yok et
        Destroy(gameObject);
    }
    #endregion
    #region SmoothMove
    // Private yerine PUBLIC yapıyoruz ki Swap sırasında diğer taşa erişebilelim
    public IEnumerator SmoothMove(Transform tile, Transform targetPlaceholder)
    {
        Vector3 startPos = tile.position;
        Vector3 targetPos = targetPlaceholder.position;

        float elapsedTime = 0f;
        float duration = slideDuration > 0 ? slideDuration : 0.25f;

        while (elapsedTime < duration)
        {
            if (tile == null)
                yield break;

            float t = elapsedTime / duration;
            // Eğri varsa kullan
            float curvedT =
                (slideCurve != null && slideCurve.length > 0)
                    ? slideCurve.Evaluate(t)
                    : Mathf.SmoothStep(0, 1, t);

            tile.position = Vector3.Lerp(startPos, targetPos, curvedT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        if (tile != null)
        {
            // [ÇÖZÜM]: true parametresi dünya pozisyonunu korur, zıplamayı önler.
            tile.SetParent(targetPlaceholder, true);

            // Görsel olarak zaten oradayız, şimdi verileri eşitleyelim.
            tile.localPosition = Vector3.zero;
            tile.localScale = Vector3.one;
            tile.localRotation = Quaternion.identity;

            FitToParent();
            CheckPlace();

            if (scoreManager != null)
                scoreManager.CheckForPer();
        }
    }
    #endregion
    /// <summary>
    /// Belirtilen hedef index'in (targetIndex) uygun ve boş olup olmadığını kontrol eder.
    /// Uygunsa residentUI taşını oraya gönderir ve true döner.
    /// Değilse (Doluysa veya Duvarsa) hiçbir şey yapmaz ve false döner.
    /// </summary>
    private bool CheckAndMoveNeighbor(Transform container, TileUI residentUI, int targetIndex)
    {
        // 1. Sınır Kontrolü (Index var mı?)
        if (targetIndex < 0 || targetIndex >= container.childCount)
            return false;

        Transform neighborPlaceholder = container.GetChild(targetIndex);
        Placeholder phScript = neighborPlaceholder.GetComponent<Placeholder>();

        // 2. Duvar Kontrolü (Atma alanı mı?)
        if (phScript != null && phScript.isRight)
            return false;

        // 3. Doluluk Kontrolü (Senin isteğin: Sadece boşsa kaydır, doluysa kaydırma)
        if (neighborPlaceholder.childCount == 0)
        {
            // BOŞ! O zaman taşı oraya kaydır.
            residentUI.StartCoroutine(
                residentUI.SmoothMove(residentUI.transform, neighborPlaceholder)
            );
            return true; // Başarılı, kaydırdık.
        }
        else
        {
            // DOLU! Kaydırma yapma, false dön (Böylece Swap devreye girecek)
            return false;
        }
    }

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
    /// <summary>
    /// Istaka içinde, verilen başlangıç noktasından itibaren (önce sağ, sonra sol)
    /// taşın kaydırılabileceği EN UYGUN ve BOŞ yeri bulur.
    /// Duvarlara (isRight) çarpınca durur.
    /// </summary>
    private Placeholder FindSmartEmptyPlaceholder(Transform container, int startIndex)
    {
        int maxIndex = container.childCount - 1;

        // --- 1. SAĞ TARAFI TARA ---
        for (int i = startIndex + 1; i <= maxIndex; i++)
        {
            Transform sibling = container.GetChild(i);
            Placeholder ph = sibling.GetComponent<Placeholder>();

            // GÜVENLİK: Script yoksa geç
            if (ph == null) continue;

            // KRİTİK DUVAR KONTROLÜ: 
            // Eğer baktığımız yer "Atma Alanı" (isRight) ise, DUR!
            // Buradan ötesine (veya buraya) taş kayamaz.
            if (ph.isRight) 
            {
                break; 
            }

            // Orta alana veya meld alanına kaymayı engelle (Ekstra önlem)
            if (ph.isMeldArea || ph.isDrop) continue;

            // BOŞ MU?
            if (sibling.childCount == 0)
            {
                return ph; // Bulduk!
            }
        }

        // --- 2. SOL TARAFI TARA (Eğer sağda yer yoksa) ---
        for (int i = startIndex - 1; i >= 0; i--)
        {
            Transform sibling = container.GetChild(i);
            Placeholder ph = sibling.GetComponent<Placeholder>();

            if (ph == null) continue;

            // Sol tarafta "isRight" (Atma Alanı) olması beklenmez ama yine de kontrol edelim
            if (ph.isRight) continue;

            if (ph.isMeldArea || ph.isDrop) continue;

            // BOŞ MU?
            if (sibling.childCount == 0)
            {
                return ph; // Bulduk!
            }
        }

        // Hiçbir yer yoksa null döner
        return null;
    }
}
