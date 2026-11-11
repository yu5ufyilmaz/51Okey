# 51 Okey — Kodun bugün uyguladığı kurallar

Tarih: 2026-11-11 · Amaç: test oyuncusunun "beklenen sonuç" kaynağı. Her satırı onayla ya da düzelt.
**!** = kodda şüpheli, çelişkili ya da ölü kod. Dosyalar `Assets/Osman/Scripts/` altında
(SM = GamePlay/ScoreManager.cs, GM = GamePlay/GameManager.cs, PS = GamePlay/PenaltySystem.cs,
RE = GamePlay/OkeyRuleEngine.cs, TM = GamePlay/TurnManager.cs, TD = TILES/TileDistrubite.cs, UI = GamePlay/TileUI.cs).

Kodda "Joker" = gerçek okey (göstergenin bir üstü, aynı renk). "FakeJoker" = sahte okey. "Gösterge kopyası" = göstergeyle aynı ikinci taş.

| # | Kural (kodun uyguladığı) | Değer | Yer | Doğru mu? |
|---|---|---|---|---|
| **Dağıtım** | | | | |
| 1 | 4 renk × 1–13 × 2 adet + 2 sahte okey | 106 taş | TD:123-148 | |
| 2 | Karıştırma sonrası ilk taş gösterge olur, desteden çıkar | – | TD:195-196 | |
| 3 | Okey = gösterge rengi, sayı +1 (13'ten sonra 1). Sahte okeyler okeyin renk/sayısını alır | – | TD:215-222, 264-278 | |
| 4 | Gösterge sahte okey çıkarsa diğer sahte okey tek okey olur | – | TD:202-208, 231-241 | |
| 5 | 1. sıra 15, diğerleri 14 taş alır | 15 / 14 | TD:454-457 | |
| 6 | 1. sıra çekmeden atar. Sıra 1→2→3→4→1 | – | TM:34-37, 242-244 | |
| 7 | Elde ≥15 taş varken çekilemez (ortadan da yandan da) | 15 | UI:301, 388 | |
| **Per geçerliliği** | | | | |
| 8 | Seri: ≥3 taş, tek renk, ardışık (artan/azalan), 13→1 geçişi yok, okey boşluk doldurur | min 3 | RE:49-147 | |
| 9 | **!** Seri sonunda tekrar eden 13 veya 1 kabul ediliyor (12-13-13, 2-1-1, 1-1-1) | – | RE:69-71 | |
| 10 | **!** Gösterge kopyası seride renk kontrolünden muaf (renk jokeri gibi davranıyor), sayısı uymalı | – | RE:33-35 | |
| 11 | Grup: 3–4 taş, aynı sayı, hepsi farklı renk, okey kullanılabilir | 3–4 | RE:152-192 | |
| 12 | Çift: aynı renk+sayı 2 taş. Okey+herhangi, okey+okey geçerli. **!** Herhangi taş + gösterge kopyası da geçerli | 2 | RE:198-229 | |
| 13 | Puan = taş sayıları toplamı; seri/grupta okey yerine geçtiği sayıyı alır. **!** Çiftte okey kendi kayıtlı sayısını alır (geri almadan sonra 0 olabilir) | – | RE:233-251 | |
| **Açma** | | | | |
| 14 | Açmak için sıra sende ve taş çekmiş olmalısın | – | SM:551, 596 | |
| 15 | Masa limiti 51 başlar, her el 51'e döner | 51 | GM:31, 95, 620 | |
| 16 | Seri açma: tüm geçerli seri+grup toplamı limitten **büyük** olmalı (`>`). Seri açmış olana limit yok | `>` | SM:568-572 | |
| 17 | Seri ve grup puanı ortak, çift puanı ayrı. Sadece 2'li çiftler çift sayılır | – | SM:425-434 | |
| 18 | Açtıktan sonra elde en az 1 taş kalmalı | ≥1 | SM:559, 613 | |
| 19 | Sadece çift açan seri açamaz | – | SM:545 | |
| 20 | Seri açan, masada biri çift açmadıkça çift açamaz | – | SM:599 | |
| 21 | Çift açma: daha önce açtıysan ya da masada biri çift açtıysa limit yok; yoksa çift toplamı `>` limit | `>` | SM:626-636 | |
| 22 | **!** İkinci çift kontrolü `>=` kullanıyor (pratikte ulaşılmıyor) | `>=` | SM:1026 | |
| 23 | En az 1 geçerli çift yeterli; 4/5 çift şartı yok | ≥1 | SM:640 | |
| 24 | **!** Biri çift açınca, açmamış herkes tek çiftle limitsiz açabiliyor | – | SM:632, 1024 | |
| 25 | Limit atışta yükselir: açanın puanı ≥ limit ise yeni limit = o puan; sonraki açan onu `>` ile geçmeli | – | SM:2198-2216 | |
| 26 | **!** Limiti yükselten puan butona basıldığı an alınıyor, açma başarısız olsa bile. Çift puanı da aynı limiti yükseltiyor. Önceden açmış biri tekrar Seri'ye basarak limiti yükseltebiliyor | – | SM:544, 594, 2201 | |
| 27 | Geri alma bu turun perlerini geri verir; önceden açılmış per varsa "açtı" bayrağı kalır | – | SM:1182-1253 | |
| **Yandan alma** | | | | |
| 28 | Sadece sıran gelince, çekmeden önce, bu tur açmadan/işlemeden önce; el <15 olmalı | – | UI:327-351 | |
| 29 | Yandan alan o tur yeni per açmadan atamaz. **!** Kuralda "veya işlemeli" yazıyor ama `hasProcessedThisTurn` hiç true olmuyor, işleme sayılmıyor | – | TM:175-200 | |
| 30 | **!** Yandan alınan taşın açılan perde kullanıldığı kontrol edilmiyor | – | – | |
| 31 | Yandan alınan taş elle işlenemez. **!** Otomatik işle butonu (`ActivePers`) bunu kontrol etmiyor | – | SM:2032-2044 | |
| 32 | Açmadan iptal: taş yana döner, ceza yok | 0 | GM:230-236 | |
| 33 | Açmadan atmaya çalışırsa: alana +250, taş yana döner, ortadan 1 taş çekilir, atma iptal | +250 | UI:613-616; GM:190-261 | |
| 34 | **!** Bu durumda `hasPickedFromSide` sıfırlanmıyor; her yeni atma denemesi tekrar +250 ve tekrar çekme | +250 her seferde | GM:254-266 | |
| 35 | Yandan alan açınca, taşı atan önceki oyuncuya taş sayısı × 10 (sayı 0 ise 250) | n×10 / 250 | GM:214-228 | |
| 36 | **!** Bu ceza hem seri hem çift yolundan gönderiliyor, iki kez yazılabilir. Açan master ise ikinci çağrı 250 veriyor. Geri almada geri alınmıyor | – | GM:223 | |
| **İşleme** | | | | |
| 37 | Sadece açmış oyuncu, kendi sırasında, çektikten sonra işleyebilir | – | UI:717-728 | |
| 38 | Herkesin perine işlenebilir (kendi perine de) | – | SM:1599, 2139 | |
| 39 | Seri iki uçtan uzar (1–13, geçiş yok); gruba eksik renk eklenir; çifte sadece okey değişimi | – | TD:1188-1276 | |
| 40 | Elden okey her boşluğa uyar. **!** Otomatik işle, elindeki okeyi ilk boşluğa koyar | – | SM:2065, 1627-1633 | |
| 41 | Grup işleme: sayı aynı, renk grupta yok | – | SM:2114-2125 | |
| 42 | Okey değişimi: gerçek taşı okeyin yerine koyan okeyi alır. Okey-okey değişimi yok | – | SM:1293-1805 | |
| 43 | Gruptan okey alma sadece 4'lü grupta (elle). Otomatik işle değişimden sonra durur | 4 | SM:2083, 1676-1682 | |
| 44 | Başkasının perine işleyince per sahibine taş sayısı × 10 ceza | n×10 | SM:1716-1729; PS:86-90 | |
| 45 | İşlenen taş göstergeyle aynıysa per sahibine × 20 | n×20 | PS:92-107 | |
| 46 | **!** Okey işlenirse okeyin kayıtlı sayısı × 10 (0 olabilir) | – | PS:90 | |
| 47 | İşleme cezaları atışta yazılır; geri alma kaldırır | – | PS:118-176 | |
| 48 | **!** `ApplyProcessingPenaltyRPC`, `ApplyBulkProcessingPenaltyRPC`, `RevertProcessingPenaltyRPC` hiç çağrılmıyor (ölü kod) | – | GM:271-314 | |
| **Atma cezaları** | | | | |
| 49 | Okey (gerçek) atmak: atana +250 | +250 | GM:330-351 | |
| 50 | Gösterge kopyası atmak: +250 | +250 | GM:353-361 | |
| 51 | Masada işlenebilecek (işlek) taş atmak: +250. Sahte okey muaf | +250 | GM:365-379 | |
| 52 | Bir atışta en fazla bir ceza | max 250 | GM:383-385 | |
| 53 | **!** Gösterge sahte okeyse "okey" siyah 2/3, "gösterge" siyah 1/2 oluyor; bu değerdeki normal taşlar cezalanıyor | – | GM:333-338 | |
| 54 | **!** Biten atış da kontrol ediliyor: okeyi atarak bitiren yine +250 yiyor. Bot atışları hiç kontrol edilmiyor | – | UI:624-631; TD:2170 | |
| **El sonu** | | | | |
| 55 | Atıştan sonra elde taş kalmazsa o oyuncu kazanır | – | UI:814-826 | |
| 56 | Orta deste biterse el kazanansız biter | – | UI:887-896 | |
| 57 | **!** Okey atarak bitirme ve çift bitirme bayrakları hep `false`; özel bitiş puanı yok | – | GM:446-464 | |
| 58 | El sonu: kazanan −600; hiç açmamış +600; açmış ama bitirmemiş = kalan taş **adedi** × 10 × çarpan (taş değeri değil) | −600 / +600 / adet×10×ç | PS:44-80 | |
| 59 | Çarpan göstergeden: sahte okey 8, sarı 6, kırmızı 5, siyah 4, mavi 3. **!** Yorumda "Sarı x2, Kırmızı x3" yazıyor, kodla çelişiyor | 8/6/5/4/3 | GM:113-136 | |
| 60 | El sonucu = el içi cezalar + el sonu cezası. **!** El içi cezalar eller arasında sıfırlanmıyor, sonraki ele taşınıyor | – | GM:481-504; SM:139-180 | |

## Kural kitabıyla karşılaştırma (51 Okey.pdf, normal mod)

Kullanıcı: #58 bug değil (taş adedi × 10 × çarpan doğru).

### Kodda yanlış (kural kitabıyla çelişiyor)

| # | Kitap ne diyor | Kod ne yapıyor |
|---|---|---|
| 9 | Seri ardışık olmalı | 12-13-13, 2-1-1, 1-1-1 seri sayılıyor |
| 10 | Gösterge kopyası çift açmak için | Seride renk jokeri gibi kullanılabiliyor |
| 21, 24 | Çift açmak için de 51'i (katlamalıda yeri) geçmek gerekir | Masada biri çift açınca herkes tek çiftle limitsiz açıyor |
| 29 | Normal modda yandan alınan taşla işleme yapılabilir (Hardcore'da yapılamaz) | Yandan alan açmadan atamaz; işleme sayılmıyor (`hasProcessedThisTurn` hiç true olmuyor) |
| 31 | Aynı sebep: normal modda yandan alınan taş işlenebilir | Elle işleme engelli (Hardcore kuralı), otomatik işle engellemiyor (tutarsız) |
| 54 | Okey atarak bitmek ödüllü bitiş (diğerleri 2 kat) | Okeyi atarak biten +250 ceza yiyor |
| 57 | Çiftten veya okey atarak bitiş: diğerleri 2 kat; kimse açmadan okey atarak: 4 kat; çarpanlar çarpılır | Özel bitiş yok, bayraklar hep false |
| 60 | Her el kendi cezasını yazar | El içi cezalar (`playerScores`) sadece oyun başında sıfırlanıyor; her el sonucu öncekilerin toplamını tekrar ekliyor. Üstelik sıra numarasına (que) göre tutuluyor, sıra her el değişince eski cezalar başka oyuncuya geçiyor |

### Kodda eksik (kitapta var, kodda yok)

| Kitap | Not |
|---|---|
| Açmış oyuncunun elinde kalan her okey: el sonunda 250 ("kırmızı, 3 taş + 1 okey = 400") | Kod okeyi normal taş gibi sayıyor (4 × 10 × 5 = 200) |
| Çift açan oyuncu biterse değil, başkası biterse: eldeki taşlar 2 kat (okey hariç) ("mavi, 3 taş = 180") | Kod çift açanı ayırmıyor (90 yazar) |
| İşleme: bir pere aynı elde bir uçtan en fazla 2 taş (6-7-8'e 9,10 işlenir, 11 işlenmez; 5,4 işlenebilir) | Sınır yok |
| Gösterge kopyası çift açınca "kendi sayısıyla" sayılır (gösterge 5 ise 13'ün yanında bile çift 10 sayılır) | Kontrol edilmeli (#13) |

### Kitapla uyumlu

1-8, 11, 15, 16 (kitapta "yeri geçmek zorundasınız"), 19, 20, 25, 35, 44, 45, 49, 50, 51, 58, 59 (kod doğru, sadece yorum yanlış).

### Açık sorular

1. İlk açma tam 51 ile olur mu? Kod 52 ve üstünü istiyor (`> 51`). Kitap: "toplamı 51 yapmalı".
2. Oyun hep "katlamalı" mı? Kod limiti her açılışta yükseltiyor; kitap bunu katlamalı mod için söylüyor.
3. Açmayan oyuncunun 600'ü sabit mi, renk çarpanıyla mı? (Kitap örneği sarı için 600.)
4. "Aynı elde en fazla 2 taş işleme"deki "el" bir tur mu, yoksa oyunun bir eli mi?
