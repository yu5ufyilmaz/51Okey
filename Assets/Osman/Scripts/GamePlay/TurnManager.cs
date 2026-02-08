using System.Collections;
using System.Collections.Generic;
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

    public void StartGame()
    {
        Player player = PhotonNetwork.LocalPlayer;
        player.CustomProperties.TryGetValue("PlayerQue", out object queueValue);
        int queueValueInt = (int)queueValue;
        if (queueValueInt == 1)
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
        // Sadece MasterClient yönetir
        if (!PhotonNetwork.IsMasterClient)
            return;

        // 1. BASİT KONTROL: Oyun bitmişse veya HENÜZ HAZIR DEĞİLSE (Taşlar dağıtılmadıysa) hiçbir şey yapma.
        if (
            GameManager.Instance != null
            && (GameManager.Instance.isGameEnded || !GameManager.Instance.isGameReady)
        )
            return;

        // 2. CANLI KONTROL: Şu anki sıra numarasında (currentTurnPlayer) oturan bir İNSAN var mı?
        bool isCurrentPlayerHuman = false;
        foreach (var p in PhotonNetwork.PlayerList)
        {
            // Oyuncunun sıra numarasına bak, eğer şu anki sırayla eşleşiyorsa o bir İnsandır.
            if (
                p.CustomProperties.TryGetValue("PlayerQue", out object q)
                && (int)q == currentTurnPlayer
            )
            {
                isCurrentPlayerHuman = true;
                break;
            }
        }

        // 3. HAMLE MANTIĞI: Eğer İnsan YOKSA, demek ki sıra Botta. Oyna.
        if (!isCurrentPlayerHuman)
        {
            // Eğer bot henüz hamle yapmaya başlamadıysa başlat
            if (!isBotMoving)
            {
                StartCoroutine(PlayBotTurn(currentTurnPlayer));
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
            // Sıra İnsanda ise bot zamanlayıcılarını sıfırla ve bekle.
            botTimeoutTimer = 0f;
            isBotMoving = false;
        }
    }

    private void ForceSkipBotTurn()
    {
        // Tüm coroutine'leri durdur (Sıkışanı iptal et)
        StopAllCoroutines();

        // Değişkenleri sıfırla
        botTimeoutTimer = 0f;
        isBotMoving = false;

        // Sırayı zorla devret
        photonView.RPC("NextTurn", RpcTarget.All);
    }

    // TurnManager.cs içine eklenecek:
    [PunRPC]
    public void SetTurnDirectly(int turnIndex)
    {
        currentTurnPlayer = turnIndex;
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
            return (int)queueValue == currentTurnPlayer;
        }
        return false;
    }

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
        // 1. Sadece verileri sıfırla, EKRANA DOKUNMA.
        currentTurnPlayer = 1;
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
    private void NextTurn()
    {
        localPlayerTurn = false;

        // Kilidi ve zamanlayıcıyı sıfırla
        isBotMoving = false;
        botTimeoutTimer = 0f;

        currentTurnPlayer++;
        if (currentTurnPlayer > 4)
            currentTurnPlayer = 1;

        TileDistrubite tileDistrubite = FindObjectOfType<TileDistrubite>();
        if (tileDistrubite != null)
            tileDistrubite.RecalculateAllAvailableSlots();

        if (
            PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("PlayerQue", out object q)
            && (int)q == currentTurnPlayer
        )
        {
            ResetTurnFlags();
        }

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateTurnIndicators(currentTurnPlayer);

        // Bot kontrolü artık Update() içinde yapılıyor.
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

        List<Tiles> botHand = tileDist.GetPlayerTilesForBot(playerQue);

        if (botHand == null)
        {
            Debug.LogError("[BOT] El boş geldi, zorla geçiliyor.");
            ForceSkipBotTurn();
            yield break;
        }

        // --- ADIM 1: TAŞ ÇEK ---
        bool hasDrawn = false;
        if (botHand.Count < 15)
        {
            // Ortadan çek
            tileDist.photonView.RPC(
                "AddTileFromMiddlePlayerList",
                RpcTarget.AllBuffered,
                playerQue
            );
            hasDrawn = true;
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
        if (!string.IsNullOrEmpty(worstTileID))
        {
            tileDist.photonView.RPC(
                "BotDiscardTileRPC",
                RpcTarget.AllBuffered,
                playerQue,
                worstTileID
            );
            // RPC içinde NextTurn var, o yüzden burada çağırmıyoruz.
            // isBotMoving = false işlemini NextTurn yapacak.
        }
        else
        {
            Debug.LogError("[BOT] Atacak taş ID'si boş! Zorla geçiliyor.");
            ForceSkipBotTurn();
        }
    }

    // Bu fonksiyonu TileDistrubite çağıracak (Veriler geldikten sonra)
    public void StartRoundVisuals()
    {
        // 1. Sıra numarasını garantiye al
        currentTurnPlayer = 1;

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
            }
        }
    }
}
