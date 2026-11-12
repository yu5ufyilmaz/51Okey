# Koltuk seçimi (bekleme ekranı) — kısa tasarım

Tarih: 2026-11-12. Kaynak: kullanıcının isteği (sohbet).

## Ne ve neden
Oyuncu masada nereye oturacağını seçebilsin (kimin solunda, karşısında oturacağı, dolayısıyla sırası),
masa dolana kadar yerini değiştirebilsin.

## Kurallar (kullanıcının)
1. Odayı kuran direkt oturur.
2. "Katıl" (rastgele) ile gelen ilk boş koltuğa otomatik oturur.
3. Oda listesinden odayı bilerek seçen otomatik oturmaz; bekleme ekranında boş bir koltuk seçip oturur.
4. Masa dolana kadar oturan herkes istediği boş koltuğa geçebilir.
5. Son oyuncu gelince oyun başlar (geri sayım); son gelen pratikte kalan koltuğa oturur, ancak biri kalkarsa seçenek olur.

## Kararlar (benim; düzeltilebilir)
- Odaya 4. kişi girdiği an: henüz koltuk seçmemiş herkes ilk boş koltuklara oturtulur, geri sayım başlar,
  koltuklar kilitlenir. Böylece seçmeyen biri oyunu kilitleyemez.
- "Botlarla oyna": 10 sn sonra önce seçmemiş insanlar oturtulur, sonra boşlar botla dolar (bugünkü gibi).
- Seçim yeri: masadaki boş isimliklerde "OTUR" yazar; dokununca oraya geçilir. Henüz oturmamış oyuncu masayı
  1. koltuk altta olacak şekilde görür; oturunca her zamanki gibi kendisi altta olur.
- Koltuğu master verir (istek RPC'si): aynı koltuğa iki kişi oturamaz, geri sayım başladıysa istek reddedilir.
- Giriş yolu oyuncu özelliğiyle (`PickSeat`) bildirilir; listeden girişte true, kurma/rastgelede false.
- Boş koltukların per/atma alanı adları her güncellemede eski haline döner; yer değiştiren oyuncunun eski adı
  boş alanda kalıp `"<ad> meld"` aramasını şaşırtmasın.

## Yaklaşımlar
- A (seçilen): Mevcut `SeatManager` + oyuncu özelliği `SeatNumber` korunur; master'a "koltuk iste" RPC'si ve
  `PickSeat` bayrağı eklenir. Oyunun geri kalanı (sıra, atma/per alanları) koltuk numarasından okumaya devam eder.
- B: Ayrı bir lobi/bekleme sahnesi. Daha çok iş, sahne geçişi ve test akışı değişir; istenen davranış için gerekmez.

## Kapsam dışı
Koltuktan kalkıp izleyici olmak, oyun başladıktan sonra yer değiştirmek, master değişince koltuk tablosunun
yeniden kurulması (bugün de yok).

## Test
Test oyuncuları listeden (şifreyle) girdiği için koltuğu kendileri seçer: ilk boş koltuğu isterler. Ayrıca bir koşuda
bir test oyuncusu oturduktan sonra başka boş koltuğa geçer; koltuk numaraları tüm client'larda aynı olmalı.
