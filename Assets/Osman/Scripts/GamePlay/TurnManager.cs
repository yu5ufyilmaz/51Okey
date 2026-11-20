using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class TurnManager : MonoBehaviourPunCallbacks
{
    // Singleton Instance

    public int currentTurnPlayer = 1; // İlk sıradaki oyuncu

    [SerializeField]
    private bool localPlayerTurn;
    public bool canDrop = false;
    public bool hasPickedFromSide = false; // Yandan mı çekti?
    public bool hasOpenedThisTurn = false; // Bu el per açtı mı?

    private bool isBotMoving = false; // Bot şu an hamle yapıyor mu?
    private float botTimeoutTimer = 0f; // Bot süresini sayar
    private const float MAX_BOT_TIME = 6.0f; // Bir botun maksimum hamle süresi (saniye)

    [Header("Safety Lock")]
    public bool isBotLogicPaused = false; // Bot beynini donduran kilit

    [Header("Bot System")]
    public bool[] isBotActive = new bool[5];

    [Header("Tur Süresi")]
    [SerializeField]
    private float turnSeconds = 30f;
    public float TurnSeconds => turnSeconds;

    // Herkeste aynı saat: sırayı başlatan RPC'nin sunucu zamanı. 0 = sayaç yok.
    public double TurnStartTime { get; private set; }
    public float TurnRemaining =>
        Mathf.Max(0f, turnSeconds - (float)(PhotonNetwork.Time - TurnStartTime));

    private bool autoPlaying;

    // El başında 0; her kabul edilen NextTurn'de gelen seriye eşitlenir.
    public int TurnSerial { get; private set; }

    public void StartGame()
    {
        TurnStartTime = PhotonNetwork.Time;
        Player player = PhotonNetwork.LocalPlayer;
        // PlayerQue sunucu sırasıyla gelir: henüz yoksa canDrop'u StartRoundVisuals/SetTurnDirectly açar.
        if (player.CustomProperties.TryGetValue("PlayerQue", out object queueValue) && (int)queueValue == 1)
        {
            canDrop = true;
        }
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);
        }
        if (PhotonNetwork.IsMasterClient)
            RecalculateBotStates();
    }

    void Update()
    {
        TickTurnTimer();

        // Sadece MasterClient yönetir
        if (!PhotonNetwork.IsMasterClient)
            return;

        // 1. BASİT KONTROL: Oyun bitmişse veya HENÜZ HAZIR DEĞİLSE (Taşlar dağıtılmadıysa) hiçbir şey yapma.
        if (
            GameManager.Instance != null
            && (GameManager.Instance.isGameEnded || !GameManager.Instance.isGameReady)
        )
            return;

        // 2. Şu anki sıranın koltuğunu insan mı oynuyor? Ctl bitine bakılır (kopan oyuncunun biti master'da kapanır).
        bool isCurrentPlayerHuman = IsSeatHuman(currentTurnPlayer);

        // Kopanın yarım turu geri alınana kadar (master kendi RollbackTurn'ünü işleyene dek) bot başlamaz.
        if (
            !isCurrentPlayerHuman
            && !isBotMoving
            && GameManager.Instance != null
            && GameManager.Instance.pendingRollbackSerial == TurnSerial
        )
        {
            GameManager.Instance.RollbackWatchdog();
            return;
        }

        // Koltuk insana döndü (geri dönüş): çalışan bot hamlesi bırakılır.
        if (isCurrentPlayerHuman && isBotMoving && botRoutine != null && TurnGate.MayCancelBot(botDrew))
        {
            Debug.Log($"[BOT] iptal sıra {botQue}");
            StopCoroutine(botRoutine);
            botRoutine = null;
        }

        // 3. HAMLE MANTIĞI: Eğer İnsan YOKSA, demek ki sıra Botta. Oyna.
        if (!isCurrentPlayerHuman)
        {
            // Eğer bot henüz hamle yapmaya başlamadıysa başlat
            if (!isBotMoving)
            {
                botQue = currentTurnPlayer;
                botRoutine = StartCoroutine(PlayBotTurn(currentTurnPlayer));
            }
            // Bot zaten oynuyorsa süresini say (Takılırsa müdahale etmek için)
            else
            {
                botTimeoutTimer += Time.deltaTime;
                if (botTimeoutTimer > MAX_BOT_TIME)
                {
                    Debug.LogError(
                        $"[WATCHDOG] Bot (P{currentTurnPlayer}) takıldı! Zorla geçiliyor."
                    );
                    ForceSkipBotTurn();
                }
            }
        }
        else
        {
            // Sıra İnsanda ise bot zamanlayıcılarını sıfırla ve bekle (çekmiş bot hamlesini bitirir).
            botTimeoutTimer = 0f;
            if (!(isBotMoving && botDrew))
                isBotMoving = false;
        }
    }

    // Süre biten oyuncunun hamlesini kendi cihazı yapar: rafı ve sürükle-bırak yolu onda.
    // Test oyuncuları (editör otopilotu) kendi temposunda oynar, onlara karışılmaz.
    private void TickTurnTimer()
    {
        var gm = GameManager.Instance;
        if (
            autoPlaying
            || SyncAutoTest.Instance != null
            || gm == null
            || !gm.isGameReady
            || gm.isGameEnded
            || TurnStartTime <= 0
            || TurnRemaining > 0f
            || !IsPlayerTurn()
            // Oyuncu taş tutuyor ya da atış kayıyor: hamle onun elinde, araya girilirse taş üst üste biner.
            || TileUI.Busy
        )
            return;
        StartCoroutine(AutoPlayTurn());
    }

    // Süre doldu: yandan alıp açamadıysa geri bırak, çekmediyse ortadan çek, botun seçtiği taşı at.
    private IEnumerator AutoPlayTurn()
    {
        autoPlaying = true;
        int que = currentTurnPlayer;
        Debug.Log($"[SÜRE] P{que} süresi doldu, hamle otomatik yapılıyor.");

        if (hasPickedFromSide && !hasOpenedThisTurn && !hasProcessedThisTurn)
        {
            FindObjectOfType<SidePickUIManager>()?.OnCancelButtonClicked();
            yield return WaitUntilOrTimeout(() => !hasPickedFromSide && !canDrop, 3f);
        }

        Transform hand = GameObject.Find("PlayerTileContainer").transform;
        if (!canDrop)
        {
            // Ortadaki TileUI'ler görüntü; üstteki = son sibling.
            Transform middle = GameObject.Find("MiddleTileContainer").transform;
            TileUI top =
                middle.childCount > 0
                    ? middle.GetChild(middle.childCount - 1).GetComponent<TileUI>()
                    : null;
            Placeholder slot = hand.GetComponentsInChildren<Placeholder>()
                .FirstOrDefault(p => !p.isRight && !p.isMeldArea && p.transform.childCount == 0);
            if (top != null && slot != null && TileUI.SimulateDrag(top, slot.transform))
                yield return WaitUntilOrTimeout(() => canDrop, 3f);
        }

        if (canDrop && currentTurnPlayer == que)
        {
            string id = FindObjectOfType<ScoreManager>().FindBestTileToDiscardForBot(que);
            TileUI tile = hand.GetComponentsInChildren<TileUI>()
                .FirstOrDefault(x => x.tileDataInfo != null && x.tileDataInfo.id == id);
            if (tile != null)
                TileUI.SimulateDrag(tile, GameObject.Find("RightTileContainer").transform);
            yield return WaitUntilOrTimeout(() => currentTurnPlayer != que, 5f);
        }

        // Hamle yine de olmadıysa (ör. reddedildi) yeni süre ver, her karede tekrar denemesin.
        if (currentTurnPlayer == que)
            TurnStartTime = PhotonNetwork.Time;
        autoPlaying = false;
    }

    private static IEnumerator WaitUntilOrTimeout(System.Func<bool> done, float seconds)
    {
        for (float t = 0f; t < seconds && !done(); t += Time.deltaTime)
            yield return null;
    }

    private void ForceSkipBotTurn()
    {
        // Tüm coroutine'leri durdur (Sıkışanı iptal et)
        StopAllCoroutines();

        // Değişkenleri sıfırla
        botTimeoutTimer = 0f;
        isBotMoving = false;

        // Sırayı zorla devret
        SendNextTurn(true);
    }

    // Yetki (masa sahibi) gönderimi sunucu sırasıyla gider; insan atışı All: kendi hamlesinin ardından yerelde işlenir.
    public void SendNextTurn(bool authority) => SendNextTurn(authority, TurnSerial + 1);

    public void SendNextTurn(bool authority, int serial) =>
        photonView.RPC("NextTurn", authority ? RpcTarget.AllViaServer : RpcTarget.All, serial);

    // TurnManager.cs içine eklenecek:
    [PunRPC]
    public void SetTurnDirectly(int turnIndex, PhotonMessageInfo info)
    {
        if (RestoreGate.Defer(() => SetTurnDirectly(turnIndex, info))) return;
        currentTurnPlayer = turnIndex;
        TurnStartTime = info.SentServerTime;
        // Sıra göstergelerini güncelle
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);

        // Eğer ben sıradaki kişiysem butonlarımı aç
        if (
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object q)
            && (int)q == currentTurnPlayer
        )
        {
            canDrop = true;
        }
    }

    // live = RestoreGate.Live. İnsan kapısı sunucudan dönen Ctl değerine bakar.
    public bool IsPlayerTurn()
    {
        if (
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                "PlayerQue",
                out object queueValue
            )
        )
        {
            localPlayerTurn = true;
            bool human = PhotonNetwork.CurrentRoom != null
                && SeatManager.Instance != null
                && CtlBits.Has(CtlBits.Read(PhotonNetwork.CurrentRoom.CustomProperties), SeatManager.Instance.LocalSeat);
            return TurnGate.MayAct(RestoreGate.Live, human, (int)queueValue == currentTurnPlayer);
        }
        return false;
    }

    // Yalnız master: bu sıranın koltuğunu insan mı oynuyor (masa sahibinin niyet ettiği Ctl; eşleme yoksa bot).
    public bool IsSeatHuman(int que)
    {
        var td = GameManager.Instance != null ? GameManager.Instance.tileDistrubite : null;
        int seat = SeatMath.MeldSeat(td != null ? td.seatOfQue : null, que);
        return SeatManager.Instance != null && CtlBits.Has(SeatManager.Instance.IntendedCtl, seat);
    }

    // Çalışan PlayBotTurn bu sıra için mi.
    public bool BotRunningFor(int que) => botRoutine != null && isBotMoving && botQue == que;

    private Coroutine botRoutine;
    private bool botDrew; // çalışan bot çekme RPC'sini gönderdi
    private bool botDrawPending; // çekme gönderildi, masa sahibinde henüz uygulanmadı (sunucu dönüşü)

    public void BotDrawApplied(int que)
    {
        if (que == botQue)
            botDrawPending = false;
    }
    private int botQue;

    // TurnManager.cs içine:

    public int GetPreviousPlayerQue(int currentPlayerQue)
    {
        // Eğer sıra 1 ise, önceki 4'tür. Değilse 1 eksiğidir.
        // (Toplam 4 oyuncu olduğunu varsayıyoruz)
        if (currentPlayerQue == 1)
            return 4;
        return currentPlayerQue - 1;
    }

    // TurnManager.cs

    public bool hasProcessedThisTurn = false; // Oyuncu bu tur yere taş işledi mi?

    public void ResetTurnFlags()
    {
        hasPickedFromSide = false;
        hasOpenedThisTurn = false;
        hasProcessedThisTurn = false; // Sıfırla
        canDrop = false;
    }

    public bool CanFinishTurn()
    {
        // Eğer yandan taş aldıysa kuralları kontrol et
        if (hasPickedFromSide)
        {
            // ScoreManager referansını al
            ScoreManager sm = GameManager.Instance.scoreManager;
            if (sm == null)
                sm = FindObjectOfType<ScoreManager>();

            // KURAL: Yandan taş alan oyuncu, O TUR İÇİNDE mutlaka:
            // 1. Ya Yeni Per Açmalı (hasOpenedThisTurn)
            // 2. Ya da Mevcut Perlere Taş işlemeli (hasProcessedThisTurn)
            // NOT: "Daha önce açmış olması" (alreadyOpened) bu kuralı bypass etmez!
            // O yüzden alreadyOpened değişkenini bu kontrole dahil etmiyoruz.

            if (!hasOpenedThisTurn && !hasProcessedThisTurn)
            {
                // Şartları sağlamadı, turu bitiremez (veya ceza yemeli)
                return false;
            }
        }

        // Yandan almadıysa veya şartları sağladıysa turu bitirebilir.
        return true;
    }

    [PunRPC]
    public void RPC_ResetTurnForNewRound()
    {
        if (RestoreGate.Defer(() => RPC_ResetTurnForNewRound())) return;
        // 1. Sadece verileri sıfırla, EKRANA DOKUNMA.
        currentTurnPlayer = 1;
        TurnSerial = 0;
        ResetTurnFlags();

        isBotMoving = false;
        botTimeoutTimer = 0f;
        isBotLogicPaused = true; // Botları kilitle, veri oturana kadar oynamasınlar.

        // UIManager güncellemesini buradan SİLİYORUZ.
        // Çünkü daha kimin Player 1 olduğu belli değil (Eski veri var).
        // if (UIManager.Instance != null) UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer); <-- BU SATIRI SİL

        Debug.Log("Tur verileri sıfırlandı. Yeni dağıtım bekleniyor.");
    }

    private IEnumerator DelayedBotRecalcForNewRound()
    {
        // 1.5 - 2 saniye bekle (İyice otursun veriler)
        yield return new WaitForSeconds(2.0f);

        // Şimdi kim insan kim bot hesapla
        RecalculateBotStates();

        // Hesaplama bitti, artık kilit açılabilir
        isBotLogicPaused = false;
        Debug.Log("<color=green>BOT MANTIĞI TEKRAR AKTİF (Veriler Oturdu)</color>");
    }

    [PunRPC]
    private void NextTurn(int serial, PhotonMessageInfo info)
    {
        if (RestoreGate.Defer(() => NextTurn(serial, info), serial)) return;
        if (!TurnSerialRule.Accept(TurnSerial, serial))
        {
            Debug.LogWarning($"[TUR] eski seri {serial} <= {TurnSerial}");
            return;
        }
        if (serial > TurnSerial + 1)
            Debug.LogWarning($"[TUR] seri atladı {TurnSerial} -> {serial}");
        TurnSerial = serial;
        localPlayerTurn = false;
        TurnStartTime = info.SentServerTime;

        // Kilidi ve zamanlayıcıyı sıfırla
        isBotMoving = false;
        botTimeoutTimer = 0f;

        currentTurnPlayer++;
        if (currentTurnPlayer > 4)
            currentTurnPlayer = 1;

        TileDistrubite tileDistrubite = FindObjectOfType<TileDistrubite>();
        if (tileDistrubite != null)
        {
            tileDistrubite.RecalculateAllAvailableSlots();
            // Taş kaybı/ikiz silinmesi: ıstakada görünen taşlar verideki elle aynı olmalı.
            string rack = tileDistrubite.RackMismatch();
            if (rack != null)
                Debug.LogError($"[ISTAKA] sıra {currentTurnPlayer}: {rack}");
        }

        if (
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object q)
            && (int)q == currentTurnPlayer
        )
        {
            ResetTurnFlags();
            Sfx.Play(SfxId.MyTurn);
        }

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);

        // Bot kontrolü artık Update() içinde yapılıyor.

        if (tileDistrubite != null)
            tileDistrubite.CaptureTurnStart(currentTurnPlayer);

        DesyncDetector.Report("NextTurn");
        TableDigestLog.AtTurnBoundary();
        RejoinHandshake.OnTurnBoundary(); // özet kurulduktan sonra: fotoğraf / özet raporu
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Debug.LogWarning($"OYUNCU DÜŞTÜ: {otherPlayer.NickName}. Bot modu devreye giriyor.");

        // Düşen oyuncunun 'PlayerQue' (Sıra Numarası) değerini bulmamız lazım.
        if (otherPlayer.CustomProperties.TryGetValue("PlayerQue", out object queueVal))
        {
            int leftPlayerQue = (int)queueVal;

            // 1. O koltuğu BOT olarak işaretle
            if (leftPlayerQue >= 1 && leftPlayerQue <= 4)
            {
                isBotActive[leftPlayerQue] = true;
            }

            // 2. Eğer tam şu an sıra o düşen oyuncudaysa, oyun donmasın diye botu hemen oynat.
            // Sadece MasterClient çalıştırır (herkes çalıştırırsa kaos olur).
            if (PhotonNetwork.IsMasterClient && currentTurnPlayer == leftPlayerQue)
            {
                StartCoroutine(DelayedBotRecalc());
            }
        }
    }

    // TurnManager.cs içine uygun bir yere ekle:

    public void RecalculateBotStates()
    {
        // Sadece Master Client bu hesaplamayı yapar
        if (!PhotonNetwork.IsMasterClient)
            return;

        Debug.Log("[TurnManager] Bot durumları yeniden hesaplanıyor...");

        // 1. Önce herkesi BOT olarak işaretle (Varsayılan: Herkes bot)
        for (int i = 1; i <= 4; i++)
        {
            isBotActive[i] = true;
        }

        // 2. Odadaki GERÇEK oyuncuları tara
        foreach (var p in PhotonNetwork.PlayerList)
        {
            // Oyuncunun "PlayerQue" özelliği yüklenmiş mi?
            if (p.CustomProperties.TryGetValue("PlayerQue", out object q))
            {
                int queueIndex = (int)q;

                // Eğer geçerli bir koltuk numarası varsa, o koltuk İNSANDIR.
                if (queueIndex >= 1 && queueIndex <= 4)
                {
                    isBotActive[queueIndex] = false; // İnsan olduğu için false yap
                    Debug.Log(
                        $"[BOT-CHECK] Oyuncu {p.NickName} (Queue: {queueIndex}) İNSAN olarak işaretlendi."
                    );
                }
            }
            else
            {
                // Eğer oyuncu odada ama PlayerQue değeri yoksa (henüz yüklenmediyse), hata logu bas
                Debug.LogWarning(
                    $"[BOT-CHECK-UYARI] Oyuncu {p.NickName} odada ama 'PlayerQue' verisi YOK! Bot sanılabilir."
                );
            }
        }

        Debug.Log(
            $"[SON DURUM] P1 Bot:{isBotActive[1]} | P2 Bot:{isBotActive[2]} | P3 Bot:{isBotActive[3]} | P4 Bot:{isBotActive[4]}"
        );
    }

    private IEnumerator DelayedBotRecalc()
    {
        yield return new WaitForSeconds(0.5f);
        RecalculateBotStates();
    }

    // --- GÜVENLİ HALE GETİRİLMİŞ BOT OYNAMA METODU ---
    private IEnumerator PlayBotTurn(int playerQue)
    {
        isBotMoving = true;
        botDrew = false;
        botDrawPending = false;
        botTimeoutTimer = 0f; // Sayacı başlat

        // Hata yakalama bloğu (Try-Catch)
        // Eğer botun zekasında bir kod patlarsa, catch bloğu çalışır ve oyun donmaz.
        yield return new WaitForSeconds(1.5f);

        // 1. Durum Kontrolü
        if (
            currentTurnPlayer != playerQue
            || (GameManager.Instance != null && GameManager.Instance.isGameEnded)
        )
        {
            isBotMoving = false;
            yield break;
        }

        TileDistrubite tileDist = FindObjectOfType<TileDistrubite>();
        ScoreManager scoreMgr = FindObjectOfType<ScoreManager>();

        if (IsSeatHuman(playerQue)) // bekleme sırasında insan geri döndü
        {
            isBotMoving = false;
            yield break;
        }
        List<Tiles> botHand = tileDist.GetPlayerTilesForBot(playerQue);

        if (botHand == null)
        {
            Debug.LogError("[BOT] El boş geldi, zorla geçiliyor.");
            ForceSkipBotTurn();
            yield break;
        }

        // --- ADIM 1: TAŞ ÇEK ---
        bool hasDrawn = false;
        var rec = tileDist.TurnStart;
        bool alreadyDrew = rec != null && rec.que == playerQue && rec.serial == TurnSerial && rec.drawn.Count > 0;
        if (BotDrawRule.ShouldDraw(botHand.Count, alreadyDrew))
        {
            // Ortadan çek
            tileDist.photonView.RPC(
                "AddTileFromMiddlePlayerList",
                RpcTarget.AllViaServer,
                playerQue
            );
            hasDrawn = true;
            botDrew = true;
            botDrawPending = true;
            yield return new WaitForSeconds(1.2f);
        }

        // --- ADIM 2: ATACAK TAŞ BUL ---
        // Burada hata olursa oyun durmasın diye try-catch kullanıyoruz
        string worstTileID = "";
        try
        {
            worstTileID = scoreMgr.FindBestTileToDiscardForBot(playerQue);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[BOT-AI ERROR] Zeka hesaplarken hata: {e.Message}");
            // Hata olursa rastgele bir taş seç
            if (botHand.Count > 0)
                worstTileID = botHand[0].id;
        }

        // --- ADIM 3: AT ---
        if (!botDrew && IsSeatHuman(playerQue))
        {
            isBotMoving = false;
            yield break;
        }
        if (!string.IsNullOrEmpty(worstTileID))
        {
            tileDist.photonView.RPC(
                "BotDiscardTileRPC",
                RpcTarget.AllViaServer,
                playerQue,
                worstTileID
            );
            // Atış ve sonucu (beraberlik ya da sıra geçişi) aynı karede, aynı gönderenden: masa sahibi
            // arada düşse de yeni masa sahibi atılmış taşla birlikte sıra değişimini de görür.
            // isBotMoving = false işlemini NextTurn yapacak.
            if (BotEndRule.PileEmpty(tileDist.allTiles.Count, botDrawPending))
            {
                if (GameManager.Instance != null && !GameManager.Instance.isGameEnded)
                    GameManager.Instance.photonView.RPC(
                        "FinishGameRPC",
                        RpcTarget.AllViaServer,
                        -1, // Kazanan yok (-1 = Beraberlik)
                        false, // isPenalty
                        false // isNormalWin
                    );
            }
            else
            {
                canDrop = false;
                SendNextTurn(true);
            }
        }
        else
        {
            Debug.LogError("[BOT] Atacak taş ID'si boş! Zorla geçiliyor.");
            ForceSkipBotTurn();
        }
    }

    // Fotoğraftan kurulum: sıra, seri ve süre fotoğraftan; koltuk geri verilmediği için hiçbir işlem izni açık kalmaz.
    public void RestoreTurn(int currentTurn, int serial, double turnStartTime)
    {
        currentTurnPlayer = currentTurn;
        TurnSerial = serial;
        TurnStartTime = turnStartTime;
        ResetTurnFlags();
        autoPlaying = false;
        localPlayerTurn = false;
        isBotMoving = false;
        botTimeoutTimer = 0f;
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);
    }

    // Bu fonksiyonu TileDistrubite çağıracak (Veriler geldikten sonra)
    public void StartRoundVisuals()
    {
        // 1. Sıra numarasını garantiye al
        currentTurnPlayer = 1;
        TurnStartTime = PhotonNetwork.Time;

        // 2. Bot kilidini aç (Artık veriler taze, bot oynayabilir)
        isBotLogicPaused = false;

        // 3. Görselleri ŞİMDİ güncelle (Çünkü artık PlayerQue verileri güncel)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);
        }

        // 4. Eğer ben 1. oyuncuysam (veya o anki oyuncuysam) butonlarımı aç
        if (
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(
                "PlayerQue",
                out object queueValue
            )
        )
        {
            int myQue = (int)queueValue;
            if (myQue == currentTurnPlayer)
            {
                canDrop = true; // Sadece sırası gelene atma izni ver
                Debug.Log("Sıra bende, butonlar aktif.");
                Sfx.Play(SfxId.MyTurn);
            }
        }
    }
}
