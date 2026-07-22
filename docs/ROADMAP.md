# CV Match AI — Ürün Yol Haritası

> Bu belge, projenin bugünkü halinden "gerçekten iyi ürün" noktasına giden yolu tarifler.
> Her madde **fizibilite** (kaç günlük iş), **etki** (kullanıcı/jüri için değeri) ve **hikaye puanı** ile birlikte verilmiştir.
> Puanlama ekibin mevcut Fibonacci ölçeğini kullanır: 1 · 2 · 3 · 5 · 8 · 13

---

## 0. Önce dürüst bir tespit

README'de ürün şöyle tanımlanıyor:

> "Kullanıcıların kendi güncel özgeçmişlerini **ve hedefledikleri şirkete ait iş ilanının detaylarını** sisteme yüklemeleriyle başlar. Gelişmiş yapay zeka algoritmamız, yüklenen CV ile iş tanımını derinlemesine analiz ederek…"

ve hedef kitle bölümünde:

> "Yapay zekanın hesapladığı **uyumluluk skoruna** göre…"

Kodda ikisi de yok. `InterviewController.StartInterview` sadece `string jobTitle` alıyor. Yani ürünün adındaki **"Match"** henüz yazılmamış.

Bu, yol haritasının bir numaralı maddesi olmalı. Jüri README ile demoyu yan yana koyduğunda ilk buraya bakar; aynı zamanda ürünü "bir tane daha mülakat botu"ndan ayıran tek şey bu.

---

## 1. Öncelik matrisi

Yatay eksen = yapılabilirlik, dikey eksen = ürüne kattığı değer.

```
                        YÜKSEK ETKİ
                             │
   ATS/uyum skoru ●          │        ● Mülakat modları (hazırlık/gerçekçi)
   RAG soru havuzu ●         │        ● Zorluk seviyesi
   Gelişim grafiği ●         │        ● Karanlık mod + erişilebilir paletler
                             │        ● PDF dışı format desteği
   ZOR ─────────────────────┼───────────────────────── KOLAY
                             │
   Topluluk/forum ●          │        ● Fotoğrafı ayarlara taşıma
   Canlı video mülakat ●     │        ● Hazır pozisyon kartları
   Çok dilli mülakat ●       │        ● Boş ekran iyileştirmeleri
                             │
                        DÜŞÜK ETKİ
```

**Sağ üst çeyrek Sprint 3'ün çekirdeğidir.** Sol üst çeyrek Sprint 3–4'e yayılır. Sol alt çeyrek (topluluk) tek başına bir sprinttir, Sprint 3'e sıkıştırılmamalı.

---

## 2. Sprint 3 için istenen kapsam ve gerçekçilik kontrolü

Sprint 1 = 21 puan, Sprint 2 = 18 puan tamamlandı. Yani ekibin gerçek hızı **~20 puan/sprint**.

Sprint 3 için listelenen maddelerin ham toplamı:

| # | Özellik | Puan |
|---|---|---|
| 1 | İki tür mülakat modu (hazırlık 5 dk / gerçekçi 1 dk) | 5 |
| 2 | Kullanıcı tarafından zorluk seçimi | 3 |
| 3 | PDF dışı format desteği (DOCX, TXT, resim) | 3 |
| 4 | Fotoğraf yüklemeyi profil ayarlarına taşıma | 2 |
| 5 | Karanlık mod | 5 |
| 6 | Erişilebilirlik: renk modları, albinizm dostu tema | 5 |
| 7 | Erişilebilir renk paletleri (WCAG AA) | 3 |
| 8 | RAG ile teknik soru havuzu | 8 |
| 9 | ATS / rol uyum skoru | 8 |
| 10 | Topluluk / forum / platform dönüşümü | 13+ |
| | **TOPLAM** | **55+** |

**55 puan, 20 puanlık hıza sığmaz.** Üç sprintlik iş var.

### Önerilen bölünme

**Sprint 3 — "Kişiselleştirilmiş ve erişilebilir mülakat" (21 puan)**

| Özellik | Puan | Gerekçe |
|---|---|---|
| Mülakat modları + zorluk seviyesi (tek epic) | 8 | İkisi de aynı forma ve aynı prompt katmanına dokunuyor, birlikte yapılmalı |
| Karanlık mod + erişilebilir paletler + WCAG düzeltmeleri | 8 | Tema altyapısı bir kez kurulunca üç mod da aynı yerden gelir |
| PDF dışı format desteği | 3 | Docling zaten destekliyor, sadece filtre açılacak (aşağıda detay) |
| Fotoğrafı ayarlara taşıma | 2 | Küçük refactor, ana panel temizlenir |

**Sprint 4 — "Match" (21 puan)**

| Özellik | Puan |
|---|---|
| İlan girişi + ATS/uyum skoru | 8 |
| RAG teknik soru havuzu | 8 |
| Yapılandırılmış AI çıktısı + gelişim grafiği | 5 |

**Sprint 5 — "Topluluk" (20 puan)**

| Özellik | Puan |
|---|---|
| Forum: konu, gönderi, yorum | 8 |
| Mülakat deneyimi paylaşımı + oylama | 5 |
| Moderasyon + rapor etme | 5 |
| Profil sayfası / rozet sistemi | 2 |

> Sprint 3'ün kapsamını daraltmak zayıflık değil. 21 puanı bitirip demo etmek, 55 puanın yarısını yarım bırakmaktan her ölçüde iyidir.

---

## 3. Sprint 3 maddelerinin detaylı analizi

### 3.1 İki tür mülakat modu — 5 puan

**Kullanıcı hikayesi**
> Bir aday olarak, önce baskısız pratik yapmak sonra gerçek mülakat stresini simüle etmek istiyorum; böylece hazırlığımı kademeli olarak gerçek koşullara yaklaştırabilirim.

**Kabul kriterleri**
- Mülakat başlatma ekranında iki mod kartı: **Hazırlık Modu** ve **Gerçekçi Mod**
- Hazırlık Modu: süre sınırı yok veya 5 dakika, soru tekrar okutulabilir, ipucu butonu aktif
- Gerçekçi Mod: soru başına 1 dakika, ipucu yok, soru bir kez okunur, süre dolunca otomatik geçiş
- Seçilen mod `InterviewSession` dokümanına yazılır, oturum boyunca değişmez
- Mülakat geçmişi tablosunda mod rozeti görünür

**Alt görevler**
- `InterviewSession` modeline `Mode` (enum: Preparation/Realistic) ve `TimeLimitSeconds` alanları
- `Interview/Index.cshtml`'e mod seçim kartları
- `Session.cshtml`'deki sabit `300` değeri `@Model.TimeLimitSeconds` ile değiştirilir
- `GeminiService.GenerateQuestionAsync` imzasına mod parametresi; gerçekçi modda prompt'a "kısa ve doğrudan sor, adayı rahatlatma" talimatı eklenir
- Hazırlık modunda "İpucu al" butonu → ayrı Gemini çağrısı, cevabı değil ipucunu döndürür

**Dikkat:** 1 dakikalık zorunlu süre + otomatik gönderim, WCAG 2.2.1 (Timing Adjustable) açısından problemlidir. Hazırlık modunda süreyi tamamen kapatılabilir yapmak bu sorunu çözer ve erişilebilirlik maddenizle doğal olarak birleşir. Bunu sprint notlarında birbirine bağlarsanız güçlü bir gerekçe olur.

---

### 3.2 Zorluk seviyesi — 3 puan

**Kullanıcı hikayesi**
> Bir aday olarak deneyim seviyeme uygun sorularla karşılaşmak istiyorum; yeni mezunken senior sorularıyla, senior'ken basit sorularla zaman kaybetmek istemiyorum.

**Kabul kriterleri**
- Üç seviye: Junior / Mid / Senior — mülakat başlangıcında seçilir
- Seçim tüm soru üretimini ve değerlendirme beklentisini etkiler
- Değerlendirme raporundaki puanlama seçilen seviyeye göre kalibre edilir (junior'a senior standardı uygulanmaz)
- Seçim mülakat geçmişinde görünür

**Alt görevler**
- `InterviewSession.DifficultyLevel` alanı
- `GeminiService`'te seviyeye göre prompt fragment'ları:
  - Junior: temel kavramlar, öğrenme isteği, staj/proje deneyimi
  - Mid: uygulamalı senaryolar, takım içi problem çözme
  - Senior: mimari kararlar, teknik liderlik, trade-off analizi
- Değerlendirme prompt'una "Bu aday {seviye} pozisyonu için değerlendiriliyor, beklentilerini buna göre ayarla" satırı

---

### 3.3 PDF dışı format desteği — 3 puan (sanılandan çok daha kolay)

**Kritik bilgi:** Docling zaten DOCX, PPTX, XLSX, HTML, Markdown, CSV ve resim formatlarını destekliyor. Yeni kütüphane gerekmiyor. Şu an sizi kısıtlayan iki satır kod var:

1. `CvParserService/main.py` içinde:
   ```python
   if not filename.endswith(".pdf"):
       raise HTTPException(status_code=400, ...)
   ```
2. `Services/CvParserService.cs` içinde dosya adı sabit gönderiliyor:
   ```csharp
   form.Add(streamContent, "file", "cv_upload.pdf");
   ```
   Docling formatı **uzantıdan** anlıyor. Bu satır her dosyayı PDF sanmasına yol açar. Gerçek uzantı korunmalı.

**Kabul kriterleri**
- Desteklenen formatlar: `.pdf`, `.docx`, `.txt`, `.md`, `.png`, `.jpg`
- Sunucu tarafında hem uzantı hem MIME tipi doğrulaması (şu an hiçbiri yok)
- Maksimum dosya boyutu sınırı (öneri: 10 MB)
- Desteklenmeyen format yüklenirse anlaşılır Türkçe hata mesajı
- Yükleme alanında kabul edilen formatlar kullanıcıya gösterilir

**Alt görevler**
- `main.py`: `ALLOWED_EXTENSIONS` seti, `DocumentConverter(allowed_formats=[...])`
- `CvParserService.cs`: orijinal uzantıyı koruyan güvenli dosya adı üretimi
- `HomeController.UploadDocuments`: sunucu taraflı uzantı + boyut + MIME kontrolü
- Görsel formatlarda Docling OCR'a düşer, ilk çağrı yavaş olabilir — timeout zaten 120 sn, yeterli

---

### 3.4 Fotoğrafı profil ayarlarına taşıma — 2 puan

**Kullanıcı hikayesi**
> Bir kullanıcı olarak profil bilgilerimi tek bir yerden yönetmek istiyorum; fotoğrafımın ana panelde, adımın ayarlarda olması kafa karıştırıcı.

**Kabul kriterleri**
- Fotoğraf yükleme formu `Home/Settings` sayfasına taşınır
- Ana panel yalnızca CV yükleme ve mülakat başlatmaya odaklanır
- Ana paneldeki profil kartı fotoğrafı göstermeye devam eder ama yükleme yapmaz; "Değiştir" linki ayarlara gider
- Mevcut fotoğraflar bozulmaz

**Alt görevler**
- `UploadDocuments` action'ını ikiye böl: `UploadCv` ve `UploadProfilePicture`
- `Settings.cshtml`'e `enctype="multipart/form-data"` ile fotoğraf alanı
- `Home/Index.cshtml`'den fotoğraf input'unu kaldır, "Değiştir" linkine dönüştür

---

### 3.5 Karanlık mod — 5 puan

**Kabul kriterleri**
- Üç seçenek: Açık / Koyu / Sistem tercihi
- Seçim `localStorage`'da saklanır, sayfa yenilenince korunur
- Sayfa açılışında beyaz ekran parlaması (FOUC) olmaz
- Tüm sayfalar destekler: giriş, kayıt, panel, ayarlar, mülakat, rapor
- PDF çıktısı her zaman açık temada üretilir (yazıcı için)

**Alt görevler**
- Bootstrap 5.3'ün `data-bs-theme="dark"` özelliği kullanılır — sıfırdan CSS yazmaya gerek yok
- Ancak projedeki Bootstrap sürümü kontrol edilmeli; 5.3 altındaysa güncellenmeli
- `_Layout.cshtml`'in `<head>` bölümüne, CSS'ten önce çalışan küçük bir inline script (FOUC engeli)
- Navbar'a tema değiştirici
- Hardcode edilmiş `text-dark`, `bg-light`, `text-muted` sınıfları taranıp tema değişkenlerine çevrilir — `Session.cshtml` bunlarla dolu, işin çoğu burada

---

### 3.6 Erişilebilirlik ve renk modları — 5 puan

Bu maddeyi ciddiye alırsanız jüri karşısında en ayırt edici çalışmanız olabilir; çünkü çoğu bootcamp projesinde hiç yoktur.

**Albinizm ne gerektirir?**
Albinizmli bireylerde tipik olarak fotofobi (ışığa aşırı duyarlılık), düşük görme keskinliği ve nistagmus görülür. Yani ihtiyaç:
- Parlak beyaz zemin **olmaması** — saf `#FFFFFF` yerine düşük parlaklıkta kırık beyaz veya koyu tema
- Yüksek kontrast ama düşük parlaklık — bunlar farklı şeyler
- Büyütülebilir metin (%200'e kadar bozulmadan)
- Animasyon ve yanıp sönmenin kapatılabilmesi
- Geniş tıklama/dokunma alanları

**Renk körlüğü modları**
- Protanopi (kırmızı zayıflığı), Döteranopi (yeşil zayıflığı), Tritanopi (mavi zayıflığı)
- Kritik nokta: bilgiyi **sadece renkle** iletmemek

**Mevcut kodda tespit edilen WCAG ihlalleri**

| Kural | Sorun | Nerede |
|---|---|---|
| 3.1.1 Sayfa Dili | `<html lang="en">` ama içerik Türkçe | `_Layout.cshtml` |
| 1.4.1 Rengin Kullanımı | Süre uyarısı yalnızca kırmızı renkle veriliyor | `Session.cshtml` |
| 2.3.1 Yanıp Sönme | Sayaç ve mikrofon sürekli yanıp sönüyor, kapatılamıyor | `Session.cshtml` |
| 2.2.1 Zaman Ayarlanabilirliği | 5 dk sabit, uzatılamıyor, kapatılamıyor | `Session.cshtml` |
| 4.1.3 Durum Mesajları | Kayıt başladı/bitti ekran okuyucuya bildirilmiyor | `Session.cshtml` |
| 1.3.1 Bilgi ve İlişkiler | Form alanlarının bir kısmında `<label>` bağı eksik | Tüm formlar |
| 1.4.3 Kontrast | `text-muted` üzerine `bg-light` bazı yerlerde 4.5:1 altında | Genel |

**Kabul kriterleri**
- Erişilebilirlik paneli: tema, renk modu, yazı boyutu, animasyon açık/kapalı
- Tercihler `localStorage`'da saklanır
- Süre uyarısı renk + ikon + metin + `aria-live` ile birlikte verilir
- `prefers-reduced-motion` medya sorgusuna uyulur
- Tüm interaktif öğeler klavyeyle erişilebilir, görünür odak halkası var
- Otomatik denetimde (axe DevTools) kritik hata kalmaz

**Alt görevler**
- CSS custom property tabanlı tema katmanı (`--app-bg`, `--app-text`, `--app-accent`, `--app-warning`)
- Dört palet: varsayılan, yüksek kontrast, düşük parlaklık (albinizm dostu), renk körlüğü güvenli
- `Session.cshtml`'deki inline `<style>` bloğu bu değişkenlere taşınır
- Sesli mülakat butonlarına `aria-label` ve `aria-pressed`
- Sayaca `role="timer"` ve `aria-live="polite"`

---

## 4. Sprint 4+ için: ürünü gerçekten ayrıştıracak özellikler

### 4.1 İlan tabanlı eşleştirme ve ATS skoru — 8 puan

Ürünün eksik yarısı. Mülakat başlatma ekranına iş ilanı metni alanı eklenir ve üç çıktı üretilir:

1. **Uyumluluk skoru** — CV ile ilanın karşılaştırılması, yüzdelik + eşleşen/eksik yetkinlik listesi
2. **İlana özel sorular** — model artık jenerik "React Developer" değil, ilandaki gerçek maddeleri soruyor
3. **Anahtar kelime boşluk analizi** — "İlanda Docker geçiyor, CV'nde yok"

Teknik olarak yeni altyapı gerektirmiyor: `cvContent` zaten Firestore'da, ilan metni yeni bir alan, gerisi tek bir Gemini çağrısı. Zorluk kodda değil, çıktının güvenilir ve tekrarlanabilir olmasında.

### 4.2 RAG ile teknik soru havuzu — 8 puan

**Öneri: basit tutun.** Bootcamp ölçeğinde Pinecone/Qdrant gibi ayrı vektör veritabanı kurmak gereksiz karmaşıklık ve deploy yükü getirir.

Yeterli ve dürüst bir RAG şu şekilde kurulur:
1. 200–300 soruluk bir soru havuzu JSON dosyası hazırlanır (rol, konu, seviye, soru, ideal cevap ipuçları)
2. Her sorunun embedding'i Gemini embedding modeliyle bir kez hesaplanıp dosyaya yazılır
3. Çalışma zamanında CV + pozisyon embedding'i alınır, kosinüs benzerliğiyle en yakın 5–10 soru bulunur
4. Bu sorular prompt'a "referans soru havuzu" olarak eklenir, model bunlardan seçer veya uyarlar

Avantajı: dış servis yok, deploy karmaşıklığı yok, tamamen deterministik, jüriye anlatması kolay. Firestore'un yerleşik vektör arama özelliği de alternatif olarak değerlendirilebilir.

### 4.3 Yapılandırılmış çıktı ve gelişim grafiği — 5 puan

Şu an rapor tek bir string. Gemini'nin şema destekli JSON çıktısıyla `OverallScore`, `TechnicalScore`, `SoftSkillScore`, `Recommendation` alanları ayrı ayrı kaydedilirse şunlar bir anda mümkün olur:

- Mülakat geçmişi tablosunda puan kolonu ve sıralama
- **Zaman içindeki gelişim grafiği** — ürünün en bağımlılık yaratan parçası
- Pozisyona göre ortalama, en zayıf kategori tespiti
- "Son 5 mülakatta iletişim puanın 6.2'den 8.1'e çıktı" tarzı geri bildirim

Efor küçük, açtığı kapı büyük.

### 4.4 Konuşma analizi — 5 puan

Web Speech API'den zaten transcript ve zaman damgası alıyorsunuz. Ek maliyet olmadan ölçebilecekleriniz:

- **Dolgu kelime sayımı**: "şey", "yani", "hani", "aslında" → "Bu mülakatta 12 kez 'yani' dediniz"
- **Konuşma hızı** (kelime/dakika)
- **İlk cevaba kadar geçen süre** (düşünme süresi)
- **Cevap uzunluğu dengesi**

Tamamen istemci tarafında hesaplanır, rapora "Sunum Metrikleri" başlığı olarak eklenir. Rakiplerin çoğunda yoktur.

### 4.5 Topluluk / forum — 13+ puan, tek başına bir sprint

Kapsamı küçümsemeyin. Minimum uygulanabilir hali bile şunları gerektirir:
- Yeni Firestore koleksiyonları: `Posts`, `Comments`, `Votes`, `Reports`
- Rol sistemi (kullanıcı / moderatör)
- Moderasyon akışı ve içerik raporlama — halka açık bir üründe bu opsiyonel değildir
- Sayfalama, sıralama, arama
- Kötüye kullanım koruması (rate limit, spam filtresi)

**Öneri:** Sprint 3'te sadece "Mülakat deneyimini paylaş" butonunu ekleyin — kullanıcı tamamladığı mülakatın raporunu anonimleştirip paylaşabilsin. Bu, tam forumun 1/5'i kadar iş ve topluluk fikrinin tohumunu atar. Tam forum Sprint 5'e kalsın.

---

## 5. Teknik borç — hiçbir sprintte atlanmaması gerekenler

Bunlar "özellik" değil ama biri patlarsa demo çöker.

| Konu | Aciliyet | Puan |
|---|---|---|
| CV dosyaları `wwwroot` altında herkese açık — URL bilen indirir | **Kritik** | 3 |
| Hiçbir formda CSRF koruması yok | **Kritik** | 2 |
| Şifreler saltsız SHA256 | Yüksek | 2 |
| Dosya yüklemede sunucu taraflı tip/boyut kontrolü yok | Yüksek | 2 |
| Firestore kimlik bilgisi dosya yoluna sabitlenmiş — buluta çıkamaz | Yüksek (deploy öncesi) | 3 |
| Test projesi yok | Orta | 5 |
| `Session.cshtml` 618 satır, 400'ü JavaScript | Orta | 3 |
| Oturum zaman aşımı 30 dk, mülakat 25 dk sürebiliyor | Orta | 1 |

---

## 6. Bir sonraki 6 ay için vizyon

Sprint 5 sonrasında ürünün gidebileceği yerler:

- **Mülakat kaydı ve tekrar izleme** — ses kaydı saklanır, aday kendi cevabını dinler
- **Şirket modu** — İK ekipleri kendi ilanlarını yükler, adaylara ön eleme mülakatı gönderir. Ücretli katmanın doğal yeri burasıdır.
- **Mobil uygulama** — sesli mülakat mobilde çok daha doğal
- **Sektör şablonları** — yazılım, finans, sağlık, akademi için özelleşmiş persona ve soru havuzları
- **Çoklu dil** — İngilizce mülakat pratiği tek başına yeni bir kitle açar
- **Video analizi** — göz teması, duruş, mimik. Etik ve teknik açıdan en zor madde, en sona bırakılmalı.
