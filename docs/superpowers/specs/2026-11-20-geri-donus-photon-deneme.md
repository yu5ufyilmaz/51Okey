# Geri dönüş: canlı Photon denemesinin sonuçları

Tarih: 2026-11-20. Yöntem: iki Unity editörü (A = ana proje, B = `51Okey_clone_0`), proje Photon ayarları
(`ConnectUsingSettings`), kullanıcı kimlikleri `spike-A`, `spike-B`, oda adları `spike*`. Deneme bileşeni
(`RejoinSpike`) silindi; `[SPIKE] <saniye>` satırları bileşenin kendi günlüğünden aynen alındı.
Oyunun `GameServices` / Firebase katmanına dokunulmadı. Oyunun kendi `PhotonManager`'ı deneme sahnesinde yoktu;
oyun kodunda kendiliğinden bağlanan bir başlatıcı görülmedi (Play'de `PeerCreated` durumunda kaldı).

Tasarımla çelişen sonuç yok (DURDU satırı yok). Ama iki ayrıntı tasarımı/planı etkiler: `EmptyRoomTtl` en çok 300000 ms
(bkz. S1) ve 201/202 olay kodları kullanılamıyor (bkz. S2).

## Özet tablo

| Soru | Tasarımın varsayımı | Gözlenen | Uyum |
|---|---|---|---|
| S1 sınırlar | PlayerTtl 2 saat, EmptyRoomTtl 5 dk kabul edilir | 7200000 ve 300000 kabul; EmptyRoomTtl 600000 reddedildi (hata -2, "max allowed is 300000"); PlayerTtl -1 kabul | evet |
| S2 kapalı odaya dönüş | kapalı odaya `ReconnectAndRejoin` ile dönülür; ActorNumber ve özellikler korunur | aynı ActorNumber (2), `HasRejoined=True`, `SeatNumber=2`, `PlayerQue=3`; önbellekli olaylar geri geldi | evet |
| S3 sıra | OnLeftRoom sonra OnDisconnected | `OnLeftRoom` ile `OnDisconnected(DisconnectByClientLogic)` aynı saniyede, önce `OnLeftRoom` | evet |
| S4 soğuk dönüş | Play'den çıkıp aynı userId ile `RejoinRoom` çalışır | başarılı, ActorNumber 2, `HasRejoined=True`, özellikler duruyor | evet |
| S5 hata kodları | 32758 oda yok, 32748 oyuncu yok, 32746 geçici | 32758 ve 32748 doğrulandı; 32746 iki denemede de görülmedi, benzetilmiş kopuştan sonra ilk denemede (+0,4 sn) başarı | evet (32746 ölçülemedi) |
| S6 etkin oyuncusuz oda | oda EmptyRoomTtl kadar yaşar | 63 sn sonra dönüş başarılı (B tek etkin ve master); ~328 sn sonra 32758 | evet |
| S7 masa sahibi kopması | master devri olur, dönen eski master olmaz | `OnMasterClientSwitched` önce, `OnPlayerLeftRoom(IsInactive=True)` sonra (aynı an); A dönünce master değil | evet |
| S8 bilerek etkin olmayan ayrılma | yalnız bilgi | `LeaveRoom(true)` sonrası `RejoinRoom` sunucuca kabul edildi (aynı ActorNumber, özellikler duruyor) | bilgi |

Kabul edilen değerler:

- `PlayerTtlMs = 7200000` (2 saat; -1 de kabul edildi)
- `EmptyRoomTtlMs = 300000` (üst sınır; 600000 reddediliyor)
- 32746 penceresi: ölçülemedi (≈ 0 sn: kopuş algılandıktan 0,4 sn sonra ilk `ReconnectAndRejoin` başarılı oldu)
- Geri çağrı sırası: kasıtlı `Disconnect()` ve `SimulateConnectionLoss` sonrası ikisinde de `OnLeftRoom` önce, hemen ardından `OnDisconnected` (aynı zaman damgası); master kopunca diğer istemcide `OnMasterClientSwitched` önce, `OnPlayerLeftRoom(IsInactive=True)` sonra.

## Ek bulgular (plan için)

- **201 ve 202 olay kodları kullanılamaz.** `PhotonNetwork.RaiseEvent(201 ...)` ve `(202 ...)` `false` döndü (200 ve üstü PUN'a ayrılmış). Kontrol olarak 101 ve 102 ile aynı denemeler çalıştı. Tasarımın `RaiseEvent` mesajları 0-199 aralığında olmalı.
- Dönen istemciye **önbellekli olaylar geri gönderiliyor**: hem `Others + AddToRoomCache` (101) hem `All + AddToRoomCache` (102), hatta kendi gönderdiği olaylar da, `OnJoinedRoom`'dan hemen sonra. Dönüşte aynı olay tekrar işleneceği için işleyiciler tekrara dayanıklı olmalı ya da önbellek kullanılmamalı.
- `PhotonNetwork.LeaveRoom()` varsayılanı `becomeInactive=true`: düz `LeaveRoom()` de oyuncuyu etkin olmayan yapıp odada tutuyor (S1 sırasında görüldü: `OnPlayerLeftRoom #2 IsInactive=True`).
- `UserId` odadaki oyuncularda boş geliyor (`PublishUserId` kapalı); tanıma ActorNumber ile yapılmalı, UserId ile değil.
- Odanın var olması: etkin oyuncu kalmayınca da oda 5 dk'ya kadar yaşıyor, o sırada dönen oyuncu odayı geri alıyor.

## S1 Sınırlar

Beklenen: PlayerTtl 2 saat kırpılmaz. Gözlenen: A `Create("spike1", 7200000, 300000)`, B `Join` sonrası B'nin okuduğu değerler odadakiyle aynı.

```
A [SPIKE] 52.00 Create spike1 ptl=7200000 ert=300000 sent=True
A [SPIKE] 52.52 OnJoinedRoom actor=1 HasRejoined=False props={} room=spike1
A [SPIKE] 56.87 OnPlayerEnteredRoom #2 HasRejoined=False
B [SPIKE] 59.55 Room spike1 PlayerTtl=7200000 EmptyRoomTtl=300000 PlayerCount=2 open=True players=[#2,#1(M)]
A [SPIKE] 81.93 Create spike1b ptl=7200000 ert=600000 sent=True
A [SPIKE] 82.77 OnCreateRoomFailed -2 Can not create game with EmptyRoomTtl=600000 max allowed is 300000.
A [SPIKE] 93.12 Create spike1c ptl=-1 ert=300000 sent=True
A [SPIKE] 93.63 OnJoinedRoom actor=1 HasRejoined=False props={} room=spike1c
A [SPIKE] 97.53 Room spike1c PlayerTtl=-1 EmptyRoomTtl=300000 PlayerCount=1 open=True players=[#1(M)]
```

Sonuç: PlayerTtl 7200000 kırpılmıyor; EmptyRoomTtl üst sınırı 300000, fazlası hata -2 ile reddediliyor; PlayerTtl -1 kabul. Uyum: evet.

## S2 Kapalı odaya dönüş

Oda `spike2` (PlayerTtl 7200000, EmptyRoomTtl 300000). B `SetProps`, A `CloseRoom`, B `CacheEvents`, B `Drop`, sonra B `Reconnect`.

```
B [SPIKE] 154.56 SetProps
A [SPIKE] 158.12 CloseRoom
B [SPIKE] 159.38 CacheEvents 201=False 202=False 101=True 102=True
B [SPIKE] 162.84 Drop() called Joined inRoom=True master=False
A [SPIKE] 164.05 OnPlayerLeftRoom #2 IsInactive=True
B [SPIKE] 179.93 Reconnect ReconnectAndRejoin=True ConnectingToGameServer inRoom=False master=False
B [SPIKE] 180.39 OnJoinedRoom actor=2 HasRejoined=True props={PlayerQue=3,SeatNumber=2} room=spike2
B [SPIKE] 180.39 OnEvent code=101 sender=2
B [SPIKE] 180.39 OnEvent code=102 sender=2
A [SPIKE] 181.52 OnPlayerEnteredRoom #2 HasRejoined=True
B [SPIKE] 187.35 Room spike2 PlayerTtl=7200000 EmptyRoomTtl=300000 PlayerCount=2 open=False players=[#2,#1(M)]
```

Sonuç: kapalı (`IsOpen=false`) odaya dönüldü; aynı ActorNumber, özellikler korundu. 201 ve 202 gönderilemedi (`False`); 101 ve 102 (kontrol) geri geldi. Uyum: evet.

## S3 Sıra

S2'deki `Drop()` sonrası B:

```
B [SPIKE] 162.84 Drop() called Joined inRoom=True master=False
B [SPIKE] 162.84 OnLeftRoom
B [SPIKE] 162.84 OnDisconnected DisconnectByClientLogic
```

Sıra: `OnLeftRoom`, sonra `OnDisconnected`; aradaki süre ölçülemeyecek kadar kısa (aynı saniye damgası, aynı kare). Benzetilmiş kopuşta da aynı (S5). Uyum: evet.

## S4 Soğuk dönüş

B Play'den çıktı (A'da `OnPlayerLeftRoom #2 IsInactive=True` düştü), yeniden girdi, `Connect("spike-B")`, `Rejoin("spike2")`.

```
B [SPIKE] 16.03 Connect spike-B ok=True
B [SPIKE] 17.58 OnConnectedToMaster
B [SPIKE] 22.48 Rejoin spike2 sent=True
B [SPIKE] 23.08 OnJoinedRoom actor=2 HasRejoined=True props={PlayerQue=3,SeatNumber=2} room=spike2
B [SPIKE] 23.08 OnEvent code=101 sender=2
B [SPIKE] 23.08 OnEvent code=102 sender=2
A [SPIKE] 229.51 OnPlayerEnteredRoom #2 HasRejoined=True
```

Sonuç: başarı, aynı ActorNumber. Uyum: evet.

## S5 Hata kodları

```
B [SPIKE] 50.89 Rejoin yok-oda sent=True
B [SPIKE] 51.73 OnJoinRoomFailed 32758 Game does not exist state=ConnectedToMasterServer
B [SPIKE] 61.72 Connect spike-X ok=True
B [SPIKE] 68.22 Rejoin spike2 sent=True
B [SPIKE] 69.11 OnJoinRoomFailed 32748 User does not exist in this game state=ConnectedToMasterServer
```

Kopuş benzetimi (`Lose()`, `DisconnectTimeout=5000`; `OnDisconnected` gelince hemen 2 sn aralıklı yeniden deneme döngüsü):

```
B [SPIKE] 39.22 Arm
B [SPIKE] 40.53 Lose()
B [SPIKE] 47.94 OnLeftRoom
B [SPIKE] 47.95 OnDisconnected ClientTimeout
B [SPIKE] 47.96 retry+0.0 ReconnectAndRejoin=True
B [SPIKE] 48.38 OnJoinedRoom actor=2 HasRejoined=True props={PlayerQue=3,SeatNumber=2} room=spike2
```

İkinci deneme (`DisconnectTimeout=1500`) aynı: 94.76 `OnDisconnected ClientTimeout`, 95.20 `OnJoinedRoom ... HasRejoined=True`.

Sonuç: 32758 ve 32748 beklendiği gibi. 32746 iki kopuş benzetiminde de çıkmadı (ilk denemede başarı, +0,4 sn); gerçek ağ kesintisinde çıkıp çıkmayacağı ölçülemedi (ölçülmedi, uydurulmadı). Tasarımın "32746 geçici, yeniden dene" kuralı bu yüzden denenmedi ama zararsız. Uyum: evet (32746 hariç). `SimulateConnectionLoss` deneme sonunda kapatıldı (`Reconnect`/`StartRetry` ilk iş kapatır).

## S6 Etkin oyuncusuz oda

A ve B `Drop()`. B 63 sn sonra `Connect` + `Rejoin`:

```
B [SPIKE] 176.08 OnMasterClientSwitched new=#2
B [SPIKE] 177.40 Drop() called Joined inRoom=True master=True
B [SPIKE] 233.82 Connect spike-B ok=True
B [SPIKE] 240.40 Rejoin spike2 sent=True
B [SPIKE] 240.94 OnJoinedRoom actor=2 HasRejoined=True props={PlayerQue=3,SeatNumber=2} room=spike2
B [SPIKE] 245.87 Room spike2 PlayerTtl=7200000 EmptyRoomTtl=300000 PlayerCount=2 open=False players=[#2(M),#1(inactive)]
```

(A, 1,3 sn önce kopmuştu, hâlâ etkin değil; B tek etkin ve master.) Sonra B tekrar `Drop()`, 321 sn bekleyip yeniden:

```
B [SPIKE] 254.33 Drop() called Joined inRoom=True master=True
B [SPIKE] 575.89 Connect spike-B ok=True
B [SPIKE] 582.43 Rejoin spike2 sent=True
B [SPIKE] 583.42 OnJoinRoomFailed 32758 Game does not exist state=ConnectedToMasterServer
```

Sonuç: 60 sn'de oda duruyor ve dönüş başarılı; EmptyRoomTtl (300 sn) + ~28 sn sonra oda yok (32758). Arası (63..328 sn) ince ölçülmedi. Uyum: evet.

## S7 Masa sahibi kopması

A (master, ActorNumber 1) `Drop()`:

```
A [SPIKE] 130.40 Drop() called Joined inRoom=True master=True
B [SPIKE] 129.30 OnMasterClientSwitched new=#2
B [SPIKE] 129.30 OnPlayerLeftRoom #1 IsInactive=True
B [SPIKE] 135.66 Room spike2 ... players=[#2(M),#1(inactive)]
A [SPIKE] 138.27 Reconnect ReconnectAndRejoin=True ConnectingToGameServer inRoom=False master=False
A [SPIKE] 138.65 OnJoinedRoom actor=1 HasRejoined=True props={} room=spike2
B [SPIKE] 137.44 OnPlayerEnteredRoom #1 HasRejoined=True
B [SPIKE] 144.92 Room spike2 ... players=[#2(M),#1]
```

(A ile B'nin saatleri ayrı editörlerin saatleri; yalnız her tarafın kendi içindeki sıra anlamlı.) Sonuç: B'de `OnMasterClientSwitched` önce, `OnPlayerLeftRoom(IsInactive=True)` sonra; A döndü, master değil (#2 master). Uyum: evet.

## S8 Bilerek etkin olmayan ayrılma (yalnız bilgi)

B `LeaveInactive()`, sonra `Rejoin`:

```
B [SPIKE] 154.02 LeaveInactive sent=True
B [SPIKE] 154.10 OnLeftRoom
B [SPIKE] 154.44 OnConnectedToMaster
B [SPIKE] 161.38 Rejoin spike2 sent=True
B [SPIKE] 161.92 OnJoinedRoom actor=2 HasRejoined=True props={PlayerQue=3,SeatNumber=2} room=spike2
```

Sonuç: sunucu `LeaveRoom(true)` sonrası dönüşe izin veriyor. Bilerek ayrılmayı engellemek yalnızca bizim kayıt silme kuralımıza kalıyor (tasarımla uyumlu).
