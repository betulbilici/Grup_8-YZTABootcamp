# Mevcut Durum, İyileştirme Alanları ve UI Rehberi

---

# Bölüm 1 — Şu ana kadar ne yapıldı

## 1.1 Teknoloji yığını

| Katman | Teknoloji | Not |
|---|---|---|
| Web uygulaması | ASP.NET Core 9 MVC (C#) | `net9.0`, Razor view'lar |
| Veritabanı | Google Firestore | `Google.Cloud.Firestore` 4.3.0 |
| Yapay zeka | Gemini 2.5 Flash | `Google.GenAI` 1.12.0, resmi C# SDK |
| Belge işleme | Python FastAPI + IBM Docling | Ayrı mikroservis, port 8000 |
| Arayüz | Bootstrap 5 + Bootstrap Icons | Bootstrap yerel, ikonlar CDN |
| Ses | HTML5 Web Speech API | Sunucu maliyeti yok |
| Markdown | marked.js | CDN |
| Oturum | ASP.NET Session (cookie) | 30 dakika timeout |

## 1.2 Tamamlanmış özellikler

**Kimlik ve hesap yönetimi**
- Kayıt: kullanıcı adı, ad, soyad, Gmail, telefon, şifre + şifre tekrarı
- Gmail-only regex doğrulaması, TR telefon regex'i (`0?5XXXXXXXXX`)
- Firestore üzerinde üçlü benzersizlik kontrolü (kullanıcı adı / e-posta / telefon)
- Giriş: kullanıcı adı **veya** e-posta ile — girdide `@` olup olmamasına göre otomatik ayrım
- Şifremi unuttum: beş kimlik alanının tamamı eşleşirse sıfırlama (compound query)
- SHA256 hash, session tabanlı oturum, navbar'da dinamik giriş/çıkış durumu

**Profil ve dosya yönetimi**
- Profil fotoğrafı yükleme (`username_profile.ext`)
- CV PDF yükleme → Docling ile otomatik parse → `cvContent` Firestore'a yazılıyor
- Parser servisi kapalıysa uygulama ayakta kalıyor, kullanıcıya uyarı gösteriliyor
- Ayarlar: ad, soyad, e-posta, telefon güncelleme (kullanıcı adı sabit)
- Güncellemede de benzersizlik kontrolü yapılıyor
- Panel içi şifre değiştirme, mevcut şifre doğrulamalı
- Ad değişince session güncelleniyor, navbar anında yenileniyor

**Mülakat motoru**
- Pozisyon adı girerek 5 soruluk oturum başlatma
- CV içeriği + tüm önceki diyalog her soru üretiminde modele gönderiliyor (bağlam korunuyor)
- İlerleme çubuğu, soru sayacı, sağ panelde canlı soru-cevap akışı
- Yarıda bırakılan mülakata geri dönebilme
- Mülakat geçmişi tablosu: pozisyon, tarih, X/5 durumu, tamamlandı/devam ediyor rozeti
- Sahiplik kontrollü silme, onay dialogu ile
- Gemini hata verirse fallback soru dönüyor, akış kırılmıyor
- Gönderimde form kilitleniyor, spinner çıkıyor (çift gönderim engeli)

**Sesli mülakat**
- Sayfa açılınca soruyu otomatik seslendirme (`tr-TR`)
- Ses listesi geç yüklenirse `onvoiceschanged` ile ikinci deneme
- Manuel oku/sustur toggle'ı, ikon durum değişimi
- `webkitSpeechRecognition` ile sürekli dinleme, transcript textarea'ya ekleniyor
- Mikrofon açıkken kırmızı renk + pulse animasyonu
- Eko önleyici: kayıt başlayınca TTS otomatik kesiliyor
- Tarayıcı desteklemiyorsa buton otomatik gizleniyor

**Süre yönetimi**
- Soru başına 5 dakika, `timer_{sessionId}_{soruNo}` anahtarıyla localStorage'da
- Sayfa yenilense bile kaldığı saniyeden devam
- Son 60 saniyede kırmızı + yanıp sönme
- Süre dolunca otomatik placeholder metin + form gönderimi

**Değerlendirme raporu**
- Yedi başlıklı Markdown rapor: genel izlenim, teknik yetkinlikler, soft skills, güçlü yönler, gelişim alanları, 10 üzerinden puan + işe alım tavsiyesi
- Soru-Cevap Mentörlük Analizi: her soru için cevap analizi, STAR metoduyla ideal cevap örneği, iki somut geliştirme tüyosu
- marked.js ile HTML render
- Tarayıcının kendi yazdırma motoruyla vektörel PDF indirme (html2pdf'in pikselleşme sorunu böyle aşılmış — doğru karar)
- Ham `.txt` indirme
- Yan panelde tam transcript

## 1.3 Kod istatistikleri

```
Toplam kod:              ~2.700 satır
En büyük dosya:          Session.cshtml (618 satır, ~400'ü JavaScript)
Controller sayısı:       3 (Account, Home, Interview)
Servis sayısı:           3 (Firestore, Gemini, CvParser)
Model sayısı:            3 (User, InterviewSession, InterviewStep)
View sayısı:             12
Test projesi:            0
```

**Git durumu:** 51 commit var ama yalnızca ikisi kod commit'i (`f066601`, `e4be44f`). Kalan 49'u README güncellemesi. Sprint 2'nin tüm kodu 17 Temmuz'da tek commit halinde girmiş.

---

# Bölüm 2 — Mevcut kodda iyileştirme alanları

## 2.1 Güvenlik — önce bunlar

### CV dosyaları herkese açık

Şu an CV'ler `wwwroot/uploads/cvs/kullaniciadi_cv.pdf` yolunda tutuluyor ve `wwwroot` klasörü statik dosya sunucusu tarafından herkese açık. Kullanıcı adını bilen (ki sistemde kullanıcı adları benzersiz ve tahmin edilebilir) herkes tarayıcıya URL yazıp başkasının özgeçmişini indirebilir.

CV'ler kişisel veri içerir: telefon, adres, iş geçmişi. KVKK açısından da problemli.

**Çözüm yönü:** Dosyaları `wwwroot` dışına, örneğin `App_Data/uploads/` altına taşı. İndirme için yetki kontrolü yapan bir controller action ekle:
```
GET /Home/DownloadCv → session kontrolü → FileStreamResult
```

### CSRF koruması hiç yok

Projede tek bir `[ValidateAntiForgeryToken]` yok. `DeleteSession` gibi yıkıcı bir işlem bile korumasız. Razor form tag helper'ları token'ı otomatik üretiyor ama sunucu tarafında doğrulanmıyor — yani token var, kontrol yok.

**Çözüm yönü:** `Program.cs`'te global filtre olarak `AutoValidateAntiforgeryTokenAttribute` ekle. Tek satır, tüm POST action'ları kapsar.

### Şifre hash'leme zayıf

`PasswordHasher` düz SHA256 kullanıyor: salt yok, iterasyon yok. GPU ile saniyede milyarlarca deneme yapılabilir. Aynı şifreyi kullanan iki kullanıcının hash'i birebir aynı çıkıyor.

**Çözüm yönü:** ASP.NET Core'un yerleşik `PasswordHasher<TUser>` sınıfı — PBKDF2, otomatik salt, ayarlanabilir iterasyon. Mevcut kullanıcıların şifreleri geçersiz kalır, ya migration yaz ya da bootcamp ortamında test hesaplarını sıfırla.

### Dosya yükleme doğrulanmıyor

`Path.GetExtension(profilePicture.FileName)` kullanıcıdan gelen dosya adını doğrudan alıp diske yazılan ada taşıyor. `wwwroot` altına `.html` veya `.js` uzantılı dosya yazılabilir, sonra doğrudan tarayıcıdan çağrılabilir. Boyut sınırı da yok.

**Çözüm yönü:** Faz 4'te ele alınıyor — beyaz liste, MIME kontrolü, boyut sınırı.

## 2.2 Deploy'u bloke eden şeyler

Retrospektifte "canlıya alma seçeneklerinin kararlaştırılması" maddesi var. Şu haliyle proje buluta çıkamaz:

**Firestore kimlik bilgisi dosya yoluna sabitlenmiş**
```csharp
string keyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "firebase-key.json");
string projectId = "cvinterviewplatform";
```
Çoğu PaaS'ta bu dosyayı repoya koymadan container'a taşımanın yolu yok. Application Default Credentials veya environment variable'dan JSON okuma desenine geçilmeli. Project ID de config'den gelmeli.

**Parser servisi localhost'a sabitlenmiş**
`http://127.0.0.1:8000` varsayılanı config'den okunuyor — bu doğru yapılmış. Ama iki servisi ayrı ayrı deploy edeceksin ve prod URL'i tanımlı değil. Docling ilk çalıştırmada model indiriyor, cold start uzun; bunu deploy planında hesaba kat.

**Yüklenen dosyalar kalıcı değil**
`wwwroot/uploads` container'ın kendi diskine yazıyor. Azure App Service, Render, Fly gibi ortamlarda container yeniden başlayınca bu klasör silinir. Firebase Storage veya benzeri bir nesne deposuna geçilmeli. (Bunu yaparken CV gizliliği sorunu da doğal olarak çözülür — imzalı URL ile erişim.)

## 2.3 Mimari ve kod kalitesi

**Yetki kontrolü her action'da elle tekrarlanıyor**

Her metodun başında aynı üç satır:
```csharp
string username = HttpContext.Session.GetString("Username") ?? "";
if (string.IsNullOrEmpty(username))
    return RedirectToAction("SignIn", "Account");
```
Bu on bir farklı yerde tekrar ediyor. Biri unutulursa açık kalıyor.

**Çözüm yönü:** Cookie authentication'a geç, `[Authorize]` attribute'unu kullan. Ya da minimum çözüm olarak bir `ActionFilter` yaz.

**Servisler soyutlanmamış**

`GeminiService` ve `CvParserService` doğrudan concrete sınıf olarak enjekte ediliyor. Interface yok, dolayısıyla mock'lanamıyor, dolayısıyla test yazılamıyor. Retrospektifte test aşamasına geçme kararı alınmış — bu adım olmadan başlanamaz.

**Çözüm yönü:** `IGeminiService`, `ICvParserService`, `IFirestoreService` interface'leri. `Program.cs`'te interface üzerinden kayıt. Sonra xUnit + Moq ile controller testleri yazılabilir.

**Session.cshtml çok büyük**

618 satır, 400'ü JavaScript. Sayaç, TTS, STT, PDF indirme, markdown render, form yönetimi — hepsi tek dosyada. Karanlık mod ve erişilebilirlik çalışmaları bu dosyaya dokunacak; önce bölünmesi gerekiyor. (Faz 0'da ele alınıyor.)

**Firestore okuma tekrarı**

`SubmitAnswer` her çağrıda hem session hem user dokümanını okuyor. Beş soruluk bir mülakatta on doküman okuması yapılıyor. Firestore ücretlendirmesi okuma başına; ölçek büyürse fark eder. CV içeriği session'a bir kez yazılabilir.

**Oturum zaman aşımı yetersiz**

Session `IdleTimeout` 30 dakika. Mülakat 5 soru × 5 dakika = 25 dakika, üstüne düşünme ve AI bekleme süreleri. Uzun bir mülakatta oturum ortada düşebilir ve kullanıcı tüm ilerlemesini kaybetmiş gibi hisseder (aslında Firestore'da duruyor ama giriş ekranına atılıyor). En az 60 dakikaya çıkarılmalı.

## 2.4 Mülakat sayacındaki güvenlik yanılsaması

Kodda şu yorum var:
```javascript
// 5 Dakikalık Mülakat Geri Sayım Sayacı (Cheat-Proof LocalStorage Destekli)
```

"Cheat-proof" değil. Sayaç tamamen istemci tarafında; DevTools açıp `localStorage.clear()` yazan herkes süreyi sıfırlar. Sayfa yenileme koruması sağlıyor, kötü niyetli kullanıcıya karşı hiçbir şey yapmıyor.

Bu bir hata değil, sadece yorum satırı gerçeği yansıtmıyor. Jüri kodu incelerse fark edebilir. İki seçenek:
1. Yorumu düzelt: "sayfa yenilemeye dayanıklı"
2. Gerçekten sunucu tarafında doğrula — `InterviewStep.AskedAt` zaten kayıtlı, `SubmitAnswer`'da geçen süre hesaplanabilir

İkincisi 1 puanlık iş ve savunması çok daha kolay.

---

# Bölüm 3 — UI iyileştirmeleri

## 3.1 Şu anki arayüzün dürüst değerlendirmesi

Arayüz **çalışıyor ve temiz** — bu küçümsenecek bir şey değil. Ama tanınabilir bir kimliği yok. Varsayılan Bootstrap 5 görünümü: mavi `btn-primary`, `shadow-sm` kartlar, `border-0 rounded-3`. Bir bootcamp jürisi aynı gün on projede aynı görünümü görür.

Tespit edilen somut noktalar:

| Sorun | Nerede | Etki |
|---|---|---|
| Marka kimliği yok — logo, renk, tipografi seçimi yapılmamış | Genel | Ürün akılda kalmıyor |
| Navbar'da hâlâ `CvInterviewPlatform` yazıyor, ürün adı `Cv Match AI` | `_Layout.cshtml` | Tutarsızlık |
| Footer'da `Privacy` ve `CvInterviewPlatform.Web` — varsayılan şablon kalıntısı | `_Layout.cshtml` | Bitmemiş izlenimi |
| Sayfa başlıklarında `- CvInterviewPlatform.Web` soneki | `_Layout.cshtml` | Aynı |
| Giriş yapılmadan görülen sayfalarda ürün tanıtımı yok | Yok | Yeni kullanıcı ne olduğunu anlamıyor |
| Bootstrap Icons her sayfada ayrı ayrı CDN'den çekiliyor | 3 farklı view | Performans + tutarsızlık |
| Mülakat ekranında AI'ı temsil eden görsel öğe yok | `Session.cshtml` | "Biriyle konuşuyorum" hissi zayıf |
| Boş durum ekranları yönlendirici değil | `Interview/Index` | Kullanıcı ne yapacağını bilmiyor |
| Rapor ekranı tek büyük markdown bloğu | `Session.cshtml` | Puan ve karar öne çıkmıyor |
| Mobil uyum test edilmemiş görünüyor | Genel | Sesli mülakat mobilde daha doğal, kaçırılan fırsat |
| Yükleniyor durumu jenerik spinner | `Session.cshtml` | 5-15 saniye boş bekleme |

## 3.2 Öncelikli UI iyileştirmeleri

### Marka kimliği (yarım gün, çok yüksek getiri)

Ürünün adı **Cv Match AI**. Şu an arayüzde bu isim hiç geçmiyor.

Yapılacaklar:
- Navbar markası: `CvInterviewPlatform` → `CV Match AI` + basit bir logo (harf işareti yeterli)
- Sayfa başlığı şablonu: `@ViewData["Title"] · CV Match AI`
- Footer'ı gerçek bilgiyle doldur: takım adı, bootcamp adı, yıl
- Bir birincil renk seç ve tutarlı kullan. Bootstrap'in varsayılan `#0d6efd` mavisi en tanıdık renklerden biri; onu değiştirmek tek başına projeyi diğerlerinden ayırır.
- Bir tipografi çifti seç. Başlıklar için karakterli bir yazı tipi, gövde için okunabilir bir tane. İkisi de Google Fonts'tan ücretsiz alınabilir.

Renk seçerken erişilebilirlik maddesini unutma: seçtiğin birincil renk beyaz metinle en az 4.5:1 kontrast vermeli.

### Karşılama sayfası (yarım gün)

Şu an giriş yapmamış kullanıcı doğrudan giriş formuna düşüyor. Ürünün ne yaptığını anlatan bir sayfa yok.

Minimum hali:
- Tek cümlelik değer önerisi
- Üç adımlık akış görseli: CV yükle → Mülakat yap → Rapor al
- İki buton: "Ücretsiz başla" ve "Giriş yap"
- Örnek rapor ekran görüntüsü

Bu, jüri sunumunda ilk gösterilecek ekran. Şu an o ekran bir login formu.

### Mülakat ekranını konuşma gibi göstermek

Şu an soru bir kartın içinde duruyor, cevap altta bir textarea. Teknik olarak doğru ama mülakat hissi vermiyor.

Öneriler:
- İK uzmanı için sabit bir avatar (ikon veya basit illüstrasyon), her soruda görünür
- Soru geldiğinde kısa bir yazılıyor animasyonu — gerçek konuşma ritmi
- Sağdaki geçmiş paneli sohbet balonu formatına yaklaşsın
- Sesli mod aktifken dalga formu veya seviye göstergesi — mikrofonun çalıştığı görsel olarak belli olsun

### Rapor ekranını yeniden düzenlemek

Şu an değerlendirme tek bir markdown bloğu olarak akıyor. En değerli bilgi (10 üzerinden puan ve işe alım tavsiyesi) metnin içinde kayboluyor.

Öneriler:
- Üstte bir **skor kartı**: büyük puan, renkli tavsiye rozeti, pozisyon adı, tarih
- Alt kategoriler için üç küçük gösterge: teknik, soft skills, genel izlenim
- Mentörlük analizi bölümü akordeon halinde — beş soru beş açılır panel, sayfa uzunluğu üçte birine iner
- Güçlü yönler yeşil, gelişim alanları amber çerçeveli kartlar

Bunun için raporu yapılandırılmış JSON olarak almak gerekir (ROADMAP 4.3). İkisi birlikte yapılmalı.

### Bekleme deneyimi

Cevap gönderince tam sayfa post oluyor ve kullanıcı 5-15 saniye spinner'a bakıyor.

Kısa vadeli iyileştirme: spinner metnini adım adım değiştir — "Cevabınız kaydediliyor" → "İK uzmanı analiz ediyor" → "Sonraki soru hazırlanıyor". Aynı süre, çok daha kısa hissettiriyor.

Uzun vadeli: cevabı AJAX ile gönder, Gemini'nin akış (streaming) çıktısını SSE ile ilet, soru harf harf yazılsın.

### Boş ekranlar

`Interview/Index`'e ilk giren kullanıcı boş bir tablo görüyor. Şunları ekle:
- Popüler pozisyon kartları: `.NET Developer`, `Veri Analisti`, `Ürün Yöneticisi`, `Dijital Pazarlama Uzmanı` — tıkla, doğrudan başla
- CV yüklenmemişse mülakat başlatma butonunun yanında uyarı: "CV yüklersen sorular sana özel olur"

---

# Bölüm 4 — UI konusunda hangi AI'lara danışılabilir

## 4.1 Önce kritik bir uyarı

Projenin arayüz katmanı **Razor (.cshtml) + Bootstrap 5**. Popüler UI üreten AI araçlarının neredeyse tamamı **React + Tailwind CSS** çıktısı veriyor.

Yani v0 veya Lovable'dan aldığın kodu projeye yapıştıramazsın. Bunları **görsel ilham ve tasarım kararı** için kullanabilirsin, üretilen kodu doğrudan kullanamazsın. Bu ayrımı bilmeden başlarsan bir gün kaybedersin.

İki stratejiden birini seç:
- **A:** Tasarım aracıyla görsel yön belirle → Razor/Bootstrap'e elle (veya Claude Code ile) çevir
- **B:** Doğrudan Razor/Bootstrap üreten araçla çalış → daha az görsel çeşitlilik, sıfır dönüştürme maliyeti

Bootcamp süresi kısıtlı olduğu için **B önerilir**, A'yı sadece karşılama sayfası gibi tek seferlik ekranlar için kullan.

## 4.2 Araç karşılaştırması

| Araç | Ne için iyi | Çıktı formatı | Bu projeye uygunluk |
|---|---|---|---|
| **Claude (Claude Code / bu sohbet)** | Razor + Bootstrap kodu doğrudan üretebiliyor, mevcut dosyaları okuyup tutarlı değişiklik yapabiliyor | İstediğin format | **En yüksek** — tek uyumlu seçenek |
| **Claude Design** | Tuval üzerinde tasarım, sohbetle iterasyon | Görsel + kod | Yüksek — konsept ve mockup için |
| **v0 (Vercel)** | Modern arayüz konseptleri, hızlı iterasyon | React + Tailwind | Orta — sadece ilham |
| **Lovable** | Uçtan uca uygulama iskeleti | React + Tailwind + Supabase | Düşük — farklı yığın |
| **Figma AI / Figma Make** | Tasarım dosyası, ekip paylaşımı, tasarım sistemi | Figma dosyası | Orta — ekipte tasarımcı varsa |
| **Google Stitch** | Ekran mockup'ları, hızlı varyasyon | Görsel + HTML/CSS | Orta — konsept aşaması |
| **Uizard** | Wireframe ve düşük detaylı taslak | Görsel | Orta — erken planlama |
| **Midjourney / Ideografik araçlar** | Karşılama sayfası görselleri, illüstrasyon | Görsel | Orta — sadece varlık üretimi |

## 4.3 Kod üretmeyen ama işi kolaylaştıran araçlar

Bunlar "AI"dan çok yardımcı araçlar ama UI kalitesine etkisi büyük:

**Renk paleti**
- **Coolors** — palet üretme ve kilitleyerek varyasyon
- **Realtime Colors** — seçtiğin paleti gerçek bir arayüz üzerinde canlı görme. Renk kararı vermenin en hızlı yolu.
- **Huemint** — makine öğrenmesi tabanlı palet üretimi, arayüz bağlamına göre

**Erişilebilirlik denetimi (Faz 3 için zorunlu)**
- **WebAIM Contrast Checker** — iki rengin WCAG oranını anında verir
- **Who Can Use** — seçtiğin renk çiftini farklı görme bozukluklarında simüle eder. Albinizm ve renk körlüğü maddeleriniz için doğrudan kanıt üretir.
- **axe DevTools** (tarayıcı eklentisi) — otomatik WCAG taraması, sprint sonu denetiminde kullan
- **WAVE** — görsel işaretlemeli erişilebilirlik raporu
- **Coblis** — yüklediğin ekran görüntüsünü renk körlüğü filtrelerinden geçirir

**Tipografi**
- **Fontpair** / **Google Fonts** — eşleşen yazı tipi çiftleri
- **Type Scale** — tutarlı başlık boyut hiyerarşisi üretir

## 4.4 Pratik iş akışı önerisi

Bootcamp takviminde en verimli yol:

**Adım 1 — Yön belirle (1 saat)**
Realtime Colors'ta 3-4 palet dene, birini seç. Google Fonts'tan bir başlık + gövde çifti belirle. Seçtiklerini WebAIM'de kontrast açısından doğrula.

**Adım 2 — Konsept üret (1 saat, opsiyonel)**
Karşılama sayfası gibi tek seferlik bir ekran için Claude Design veya v0'da birkaç varyasyon üret. Kodu değil, **görsel yönü** al.

**Adım 3 — Uygula (Claude Code ile)**
Seçtiğin renk, tipografi ve düzen kararlarını Claude Code'a net şekilde ver:

> Seçtiğim tasarım kararları:
> - Birincil renk: #XXXXXX, ikincil: #XXXXXX
> - Başlık yazı tipi: [font adı], gövde: [font adı]
> - Köşe yuvarlaklığı: 12px, gölge: yok, kenarlık ince
>
> Bunları `site.css`'e CSS değişkenleri olarak kur ve Bootstrap'in varsayılan değişkenlerini ez. Yeni CSS framework ekleme, mevcut Bootstrap 5 üzerine bin. Önce sadece `_Layout.cshtml` ve `Account/SignIn.cshtml`'e uygula, sonucu göstereyim, sonra diğer sayfalara yayalım.

**Adım 4 — Denetle**
axe DevTools taraması + Who Can Use ile renk kontrolü. Bulguları düzelt.

## 4.5 Sonuç

En pratik yol: **görsel kararları hafif araçlarla ver, uygulamayı Claude Code ile yap.** Projenin yığını Razor + Bootstrap olduğu sürece React üreten araçlar dolambaçlı yol.

Bir de şu var — bootcamp sunumunda "renk paletimizi renk körlüğü simülatöründe test ettik, üç farklı görme bozukluğu için ayrı palet ürettik" cümlesi, "modern bir arayüz tasarladık" cümlesinden çok daha ağır basar. Erişilebilirlik maddeniz sadece bir özellik değil, aynı zamanda sunumdaki en güçlü kozunuz olabilir.
