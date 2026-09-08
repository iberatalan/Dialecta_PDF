# 📑 Dialecta PDF - Kişisel & Çevrimdışı PDF İşlem Paketi
*(ILovePDF Premium Kişisel Masaüstü Alternatifi)*

Dialecta PDF, popüler çevrimiçi PDF servislerinin sunduğu tüm temel özellikleri **%100 çevrimdışı (offline)**, **sıfır harici sunucu**, **hiçbir ağ trafiği üretmeyen**, **kurulumsuz (portable)** ve **tek parça (self-contained single-file) .exe** olarak sunan modern bir Windows WPF masaüstü uygulamasıdır.

---

## 🚀 Öne Çıkan Özellikler

- **📑 PDF Birleştir (Merge)**: Birden çok PDF belgesini dilediğiniz sıralamayla tek bir belgede birleştirin.
- **✂️ PDF Ayır (Split)**: Sayfa aralıklarına göre (örn: `1-3, 5, 8-10`), tek tek veya her N sayfada bir bölümlere ayırın.
- **🔄 Sayfaları Düzenle & Döndür (Organize)**: Canlı küçük resim (thumbnail) ızgarası üzerinden sayfaları 90°/180°/270° döndürün, sıralarını değiştirin veya gereksiz sayfaları silin.
- **🗜️ PDF Sıkıştır (Compress)**: Akış optimizasyonu ve sıkıştırma seviyeleri (Düşük / Orta / Yüksek) ile belge boyutunu küçültün.
- **💧 Filigran Ekle (Watermark)**: Saydamlık, açı, konumlandırma ve renk seçenekleriyle metin veya logo filigranı ekleyin.
- **⬛ Hassas Veri Karartma (Redaction)**: TCKN, ad, soyad, telefon veya özel anahtar kelimeleri PDF üzerinden kalıcı olarak siyah bantla maskeleyin.
- **🔍 Yerel OCR (Metin Tanıma)**: Taranmış belgeleri ve görselleri dahili Tesseract motoruyla (Türkçe + İngilizce) metne dönüştürün ve panoya kopyalayın.
- **✍️ Yerel Kriptografik Mühür & İmza (Sign & Seal)**: Belgenin SHA-256 özetini içeren yerel mühür sertifikası ve görsel kaşe/imza yerleştirin.
- **🛡️ Denetim Günlüğü & Bütünlük Doğrulama**: Tüm işlemleri zaman damgası ve SHA-256 özetleriyle yerel `denetim_kayitlari.jsonl` kütüğünde kayıt altına alın; herhangi bir PDF'i sürükleyip bırakarak orijinalliğini doğrulayın.
- **📦 1-Tıkla Yedekleme**: Tüm çıktıları, orijinalleri ve denetim kayıtlarını tek tıkla `.ZIP` arşivine yedekleyin.

---

## 🔒 Güvenlik, Gizlilik ve Mimari Taahhütleri

1. **Sıfır Ağ Trafiği & Telemetri Yok**: Uygulama hiçbir dış sunucuya bağlanmaz, port dinlemez, veri göndermez.
2. **Arka Plan Süreci Yok**: Uygulama penceresi kapatıldığında tüm alt süreçler ve işlem motorları anında sonlandırılır.
3. **Orijinallerin Korunması (İmmutability)**: Yüklenen orijinal belgeler asla ezilmez, değiştirilmez veya silinmez; `Orijinaller` klasöründe salt-okunur (read-only) kopyası saklanır.
4. **Versiyonlu Çıktılar**: Her işlem `[DosyaAdi]_[İşlem]_v[ZamanDamgası].pdf` formatında kaydedilir.
5. **Office-to-PDF Kapsamı**: Uygulamanın sıfır kurulumlu, taşınabilir ve bağımsız yapısını korumak adına harici ağır ofis paketleri (LibreOffice / MS Office COM) gerektiren dönüştürücüler bilinçli olarak dahil edilmemiştir.

---

## ⚠️ Düşük Riskli Kullanım Uyarısı

> **ÖNEMLİ BİLDİRİM:** Bu uygulama tamamen kişisel arşivleme, düzenleme ve düşük riskli kullanım amacıyla geliştirilmiştir. Uygulama içinde sağlanan dijital mühürleme ve görsel imza özelliği, **5070 sayılı Elektronik İmza Kanunu** kapsamındaki *Nitelikli Elektronik Sertifika (NES)* veya resmi kamu kimlik doğrulama yerine geçmez.

---

## 📂 Yerel Veri Depolama ve Dizin Yapısı

Tüm veriler uygulamanın çalıştığı dizindeki `DialectaPDF_Veri` klasöründe saklanır:

```text
DialectaPDF_Veri/
├── Orijinaller/              # İçe aktarılan orijinal belgeler (Salt-okunur)
├── Ciktilar/                 # Versiyonlu olarak üretilen çıktı PDF'leri
├── Yedekler/                 # 1-tıkla oluşturulan .ZIP yedekleme paketleri
└── denetim_kayitlari.jsonl   # Append-only SHA-256 işlem denetim kütüğü
```

### Yedekleme ve Geri Yükleme
- **Yedekleme**: Uygulama içindeki *Denetim & Doğrulama* sekmesinden **"📦 1-Tıkla Yedekle (.ZIP)"** butonuna tıklayarak tüm verilerinizi sıkıştırılmış arşiv olarak alabilirsiniz.
- **Geri Yükleme**: Alınan `.zip` dosyasının içeriğini yeni cihazdaki `DialectaPDF_Veri` klasörüne çıkarmanız yeterlidir.

---

## 🛠️ Geliştirme, Test ve Yayınlama Komutları

### 1. Projeyi Derleme
```powershell
dotnet build
```

### 2. Birim ve Uçtan Uca (E2E) Testleri Çalıştırma
```powershell
dotnet test
```

### 3. Tek Dosya (.exe) Kurulumsuz Yayınlama (Self-Contained Publish)
Hedef makinede .NET Runtime, Python veya herhangi bir kütüphane kurulu olması **gerekmez**:
```powershell
dotnet publish PdfMaster/PdfMaster.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o ./publish
```

Üretilen `publish/DialectaPDF.exe` dosyasına çift tıklayarak uygulamayı anında kullanabilirsiniz.

---

## 📄 Lisans

Bu proje [GNU General Public License v3.0](LICENSE) ile lisanslanmıştır.
