# Tasarım Notları

Bu belge uygulamanın görsel kararlarını ve arayüz kurallarını özetler. Yeni bir View ya da bileşen eklerken referans olarak kullanılabilir.

---

## Genel yaklaşım

Arayüz WinUI 3 / Fluent tasarım dilinden ilham alır. Koyu tema temeldir, gölge yerine ton farkları ile derinlik hissi yaratılır. Tüm ölçüler 4px grid üzerine oturur.

---

## Renkler

### Yüzeyler (koyu → açık sıra)

| Token | Kod | Kullanım |
|---|---|---|
| Arkaplan | `#131313` | Ana canvas |
| Yüzey (düşük) | `#1B1B1C` | Sidebar, alt çubuk |
| Yüzey (kart) | `#202020` | İçerik kartları |
| Yüzey (vurgu) | `#2A2A2A` | Seçili durum arka planı |
| Hover | `#353535` | Liste/kart hover |

### Aksiyon renkleri

| Renk | Kod | Kullanım |
|---|---|---|
| Birincil mavi | `#0078D4` | CTA butonları (Birleştir, Dönüştür vb.) |
| Seçim arkaplanı | `#2D3E50` | Sidebar aktif öğe |
| Kırmızı (yıkıcı) | `#C42B1C` | Sil, Temizle butonları |
| Metin (ana) | `#E5E2E1` | Başlıklar, gövde metni |
| Metin (ikincil) | `#C0C7D4` | Açıklamalar, yardımcı metin |
| Kenarlık | `#8A919E` | Girdi alanları, ayırıcılar |

---

## Tipografi

Font ailesi: **Inter** (Google Fonts). Monospace veriler için **JetBrains Mono**.

| Kullanım | Boyut | Ağırlık | Satır yüksekliği |
|---|---|---|---|
| Sayfa başlığı | 28px | 600 | 36px |
| Kart başlığı | 20px | 600 | 28px |
| Alt başlık | 16px | 500 | 24px |
| Gövde | 14px | 400 | 20px |
| Küçük metin | 13px | 400 | 18px |
| Etiket (büyük harf) | 11px | 700 | 16px |
| Hash/veri (mono) | 12px | 400 | 16px |

İkincil açıklamalar için ayrı renk yerine **%70 opaklık** tercih edilir.

---

## Yerleşim

- **Sidebar:** sabit 260px genişlik.
- **İçerik alanı:** akışkan, max 1440px; fazlası auto-margin ile ortalanır.
- Dış boşluk: 32px, kart iç boşluk: 16px, kartlar arası: 24px.
- Dar pencerelerde sağ panel alt tarafa katlanabilir.

---

## Yuvarlaklık

| Öğe | Değer |
|---|---|
| Butonlar, girdi alanları | 8px |
| Büyük kartlar, pencere çerçevesi | 12px |
| Küçük etiketler | 4px |

---

## Bileşen kuralları

### Butonlar

- **Birincil:** `#0078D4` dolgu, beyaz metin, 8px radius. Hover'da +%10 parlaklık.
- **İkincil/Hayalet:** Dolgu yok, 1px kenarlık (`#FFFFFF` %15), beyaz metin.
- **Yıkıcı:** `#C42B1C` dolgu, yüksek riskli işlemler için.

### Sidebar navigasyon

- Öğe yüksekliği: 40px, yatay padding: 12px.
- İkon: ince çizgi (1.5pt), metnin solunda.
- Seçili durum: `#2D3E50` arka plan + sol kenar `#0078D4` (4px genişlik).

### Girdi alanları

- Arka plan: `#252525`, alt kenarlık 1px (focus'ta mavi).
- Yükseklik: 36px, etiket üstte büyük harfli.

### Veri tabloları (denetim günlüğü)

- Başlık: `#191919` arka plan, sabit konum, büyük harfli etiketler.
- Satır ayırıcı: 1px (`#FFFFFF` %5). Zebra deseni yok.
- Kaydırma çubuğu: ince, hover'da görünür.

### Dosya bırakma alanı

- Boşken kesikli kenarlık (`#FFFFFF` %10).
- Dosya yüklenince düz tonal yüzeye dönüşür.