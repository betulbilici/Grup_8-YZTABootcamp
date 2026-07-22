# CLAUDE.md

> Bu dosya Claude Code'un her oturumda okuduğu proje bağlamıdır.
> Repo kökündeki `CvInterviewPlatform/` klasörüne konmalıdır.
> Mimari veya konvansiyon değiştiğinde bu dosya da güncellenmelidir.

---

## Proje

**CV Match AI** — yapay zeka destekli mülakat hazırlık ve simülasyon platformu.
Kullanıcı CV'sini yükler, hedef pozisyonu girer, yapay zeka İK uzmanı personasıyla 5 soruluk bir mülakat yapar ve sonunda detaylı bir değerlendirme raporu alır.

Bu bir bootcamp projesidir (Grup 8, YZTA Bootcamp). Sprint bazlı geliştiriliyor, README'de sprint kayıtları tutuluyor.

**Takım:** Aslıhan Yeşilyurt Şengül (Scrum Master), Pelin Çelik (Product Owner), Betül Bilici, Abdulkadir Süslü, Burak Ege Kaya (Developer)

---

## Teknoloji yığını

| Katman | Teknoloji |
|---|---|
| Web | ASP.NET Core 9 MVC (C#), Razor views |
| Veritabanı | Google Firestore (`Google.Cloud.Firestore` 4.3.0) |
| Yapay zeka | Gemini 2.5 Flash (`Google.GenAI` 1.12.0) |
| Belge işleme | Python FastAPI + IBM Docling, ayrı mikroservis |
| Arayüz | Bootstrap 5, Bootstrap Icons (CDN), marked.js (CDN) |
| Ses | HTML5 Web Speech API (tarayıcı yerleşik, sunucu maliyeti yok) |
| Oturum | ASP.NET Session (cookie tabanlı) |

---

## Dizin yapısı

```
CvInterviewPlatform/
├── CvInterviewPlatform.sln
├── CvInterviewPlatform.Web/          ← ana MVC uygulaması
│   ├── Program.cs                    ← DI kaydı, session yapılandırması
│   ├── FirestoreService.cs           ← kök seviyede (diğer servisler Services/ altında)
│   ├── appsettings.json              ← Gemini:ApiKey placeholder, ParserService:BaseUrl
│   ├── Controllers/
│   │   ├── AccountController.cs      ← kayıt, giriş, şifre sıfırlama, çıkış
│   │   ├── HomeController.cs         ← panel, dosya yükleme, ayarlar
│   │   └── InterviewController.cs    ← mülakat akışının tamamı
│   ├── Models/
│   │   ├── User.cs
│   │   ├── InterviewSession.cs       ← InterviewStep sınıfı da burada
│   │   └── ErrorViewModel.cs
│   ├── Services/
│   │   ├── GeminiService.cs          ← soru üretimi + değerlendirme
│   │   └── CvParserService.cs        ← FastAPI'ye HTTP istemcisi
│   ├── Helpers/
│   │   └── PasswordHasher.cs         ← SHA256 (zayıf, değiştirilmeli)
│   ├── Views/
│   │   ├── Shared/_Layout.cshtml
│   │   ├── Account/{SignIn,Register,ForgotPassword}.cshtml
│   │   ├── Home/{Index,Settings,Privacy}.cshtml
│   │   └── Interview/{Index,Session}.cshtml
│   └── wwwroot/
│       ├── css/site.css
│       ├── js/site.js
│       ├── lib/                      ← bootstrap, jquery (yerel)
│       └── uploads/{cvs,profiles}/   ← .gitignore'da, PUBLIC ERİŞİLEBİLİR
└── CvParserService/                  ← Python mikroservis
    ├── main.py                       ← FastAPI, /health ve /parse
    └── requirements.txt
```

---

## Çalıştırma

**Ön koşullar:**
- `firebase-key.json` dosyası `CvInterviewPlatform.Web/` içinde olmalı (`.gitignore`'da, takım liderinden alınır)
- Gemini API anahtarı User Secrets veya `GEMINI_API_KEY` ortam değişkeninde

```bash
# 1. Gemini anahtarı (bir kez)
cd CvInterviewPlatform/CvInterviewPlatform.Web
dotnet user-secrets init
dotnet user-secrets set "Gemini:ApiKey" "ANAHTAR"

# 2. Parser mikroservisi (ayrı terminal, önce başlatılmalı)
cd CvInterviewPlatform/CvParserService
pip install -r requirements.txt
python main.py                    # http://127.0.0.1:8000

# 3. Web uygulaması
cd CvInterviewPlatform/CvInterviewPlatform.Web
dotnet run
```

**Not:** Docling ilk çalıştırmada model indirir, ilk parse işlemi yavaştır. `.NET` tarafındaki HttpClient timeout'u bu yüzden 120 saniyeye ayarlı.

---

## Veri modeli

### Firestore koleksiyonları

**`Users`** — doküman kimliği = `username` (benzersiz, değiştirilemez)
```
username, firstName, lastName, email, phoneNumber,
passwordHash, profilePictureUrl?, cvUrl?, cvContent?
```
`cvContent` parse edilmiş CV metnidir, Gemini'ye bağlam olarak gönderilir.

**`Sessions`** — doküman kimliği otomatik üretilir
```
username, jobTitle, startedAt, currentQuestionNumber,
isCompleted, history[], finalEvaluation?
```
`history` bir `InterviewStep` dizisidir: `{ question, answer, askedAt }`

### Önemli davranışlar

- `currentQuestionNumber` 1'den başlar, `history` dizisi 0'dan indekslenir. `SubmitAnswer` içinde `currentIndex = CurrentQuestionNumber - 1` deseni kullanılır.
- Mülakat sabit 5 sorudur. `SubmitAnswer` içinde `if (session.CurrentQuestionNumber < 5)` koşulu bunu belirler.
- Yeni bir soru üretildiğinde `history`'ye boş cevaplı bir adım eklenir; cevap sonradan doldurulur.
- 5. cevap gönderildiğinde değerlendirme üretilir ve `IsCompleted = true` olur.

---

## Konvansiyonlar

**Dil**
- Kullanıcıya görünen tüm metinler **Türkçe**
- Kod yorumları **Türkçe**
- Değişken, metot, sınıf adları **İngilizce**
- Log mesajları İngilizce (mevcut kod böyle)

**Firestore**
- Model sınıfları `[FirestoreData]`, property'ler `[FirestoreProperty("camelCase")]`
- Property adı C#'ta PascalCase, Firestore'da camelCase
- Doküman okuma deseni: `snapshot.ConvertTo<T>()`

**Controller deseni**
Her action'ın başında session kontrolü:
```csharp
string username = HttpContext.Session.GetString("Username") ?? "";
if (string.IsNullOrEmpty(username))
    return RedirectToAction("SignIn", "Account");
```
Sahiplik kontrolü gereken yerlerde ayrıca:
```csharp
if (session.Username != username) return Forbid();
```

**Kullanıcı bildirimleri**
- `TempData["Success"]`, `TempData["Error"]`, `TempData["Warning"]` — redirect sonrası
- `ViewBag.Error`, `ViewBag.Success` — aynı sayfada kalınca

**Hata yönetimi**
Servisler istisna fırlatmaz, güvenli varsayılan döner:
- `GeminiService` hata durumunda fallback soru metni döndürür
- `CvParserService` hata durumunda boş string döndürür
Bu desen korunmalı — mülakat akışı bir servis hatası yüzünden kırılmamalı.

**Razor**
- `asp-controller`, `asp-action`, `asp-route-*` tag helper'ları kullanılıyor
- `Session.cshtml` içinde Razor koşulu JavaScript'in içine giriyor, `<text>` bloğuyla sarılıyor
- CSS keyframe'lerinde `@` karakteri Razor tarafından yorumlanmasın diye `@@` yazılıyor

---

## Bilinen sorunlar ve tuzaklar

Bunlar farkında olunan durumlardır. Kod üzerinde çalışırken bunları bilerek hareket et.

### Güvenlik

| Sorun | Detay |
|---|---|
| **CV'ler herkese açık** | `wwwroot/uploads/cvs/{username}_cv.pdf` doğrudan URL ile indirilebilir. Kullanıcı adları tahmin edilebilir. |
| **CSRF koruması yok** | Projede hiç `[ValidateAntiForgeryToken]` yok |
| **Zayıf şifre hash'i** | SHA256, salt yok, iterasyon yok |
| **Yükleme doğrulaması yok** | Sunucu tarafında uzantı/MIME/boyut kontrolü yok. `Path.GetExtension(file.FileName)` kullanıcı girdisini diske taşıyor. |

### Deploy engelleri

- `FirestoreService` `projectId`'yi ("cvinterviewplatform") ve `firebase-key.json` dosya yolunu hardcode ediyor
- `wwwroot/uploads` yerel diske yazıyor, container yeniden başlayınca kaybolur
- Parser servisi varsayılan `127.0.0.1:8000`, prod URL'i tanımlı değil

### Fonksiyonel

- **Sayaç istemci tarafında.** Kodda "Cheat-Proof" yorumu var ama localStorage temizlenirse süre sıfırlanır. Sunucu doğrulaması yok.
- **Session timeout 30 dk**, mülakat 25 dk sürebiliyor — uzun oturumlarda düşebilir
- **STT sadece Chromium tabanlı tarayıcılarda** (`webkitSpeechRecognition`). Firefox ve Safari'de buton gizleniyor.
- **`<html lang="en">`** ama içerik Türkçe (WCAG 3.1.1 ihlali)
- **`html2pdf.js` yükleniyor ama kullanılmıyor** — PDF için tarayıcının kendi yazdırma motoru tercih edilmiş (pikselleşme sorunu nedeniyle, bu doğru karar). Kütüphane referansı temizlenebilir.

### Parser servisi tuzağı

`CvParserService.cs` içinde dosya multipart form'a **sabit adla** ekleniyor:
```csharp
form.Add(streamContent, "file", "cv_upload.pdf");
```
Docling dosya formatını **uzantıdan** anlıyor. PDF dışı format desteği eklenirken bu satır mutlaka düzeltilmeli, yoksa DOCX dosyası PDF sanılır ve parse başarısız olur.

### Geriye dönük uyumluluk

Firestore şemasız. `InterviewSession`'a yeni alan eklendiğinde eski dokümanlarda o alan **yok**. `ConvertTo<InterviewSession>()` çağrısının patlamaması için yeni property'lere mutlaka varsayılan değer verilmeli. Model değişikliği sonrası Sprint 2'de oluşturulmuş eski bir mülakatın açıldığı mutlaka test edilmeli.

---

## Test durumu

**Test projesi yok.** Solution'da tek proje var.

Test yazmaya başlamadan önce servislerin soyutlanması gerekiyor: `GeminiService`, `CvParserService` ve `FirestoreService` concrete sınıf olarak enjekte ediliyor, interface'leri yok, dolayısıyla mock'lanamıyorlar.

Sıralama: interface'leri çıkar → `Program.cs`'te interface üzerinden kaydet → xUnit projesi ekle → controller testleri yaz.

---

## Çalışma tercihleri

Bu proje üzerinde çalışırken:

1. **Önce oku, sonra yaz.** Değişiklik yapmadan önce ilgili dosyaları oku ve planını söyle. Onay almadan büyük refactor başlatma.
2. **Küçük ve hedefli düzenlemeler.** Özellikle `Session.cshtml` gibi büyük dosyalarda komple yeniden yazma yerine noktasal düzenleme yap.
3. **Her değişiklikten sonra `dotnet build`.** Razor derleme hataları çalıştırmadan görünmüyor.
4. **Yeni bağımlılık ekleme.** Yeni NuGet paketi, yeni CSS framework veya yeni JS kütüphanesi önermeden önce sor. Mevcut Bootstrap 5 üzerine bin.
5. **Konvansiyonlara uy.** Yeni kod mevcut desenlerle tutarlı olmalı — Türkçe yorum, İngilizce isimlendirme, aynı hata yönetimi yaklaşımı.
6. **Güvenlik açıklarını sessizce geçme.** Yukarıdaki listede olan bir soruna denk gelirsen, o an düzeltmesek bile hatırlat.
7. **Firestore okuma sayısını gözet.** Her okuma ücretli. Aynı dokümanı bir istekte birden fazla kez okuma.
8. **Erişilebilirlik varsayılan.** Yeni bir arayüz öğesi eklerken `aria-label`, klavye erişimi ve kontrast oranını en baştan düşün, sonradan eklenecek bir katman olarak görme.

---

## Sprint geçmişi

**Sprint 1** (21 puan) — Fikir geliştirme, pazar araştırması, rol dağılımı. Firestore bağlantısı, session mimarisi, kimlik doğrulama (kayıt/giriş/şifre sıfırlama), profil ayarları.

**Sprint 2** (18 puan) — Gemini 2.5 Flash entegrasyonu, kıdemli İK uzmanı personası, 5 soruluk mülakat akışı, STAR metoduyla değerlendirme raporu, Soru-Cevap Mentörlük Analizi, Web Speech API ile sesli mülakat (TTS + STT), 5 dakikalık geri sayım, PDF/TXT rapor indirme, PDF parser mikroservisi, User Secrets ile API anahtarı güvenliği.

**Sprint 3** (planlanan, 21 puan) — Mülakat modları (hazırlık/gerçekçi), zorluk seviyesi, karanlık mod, erişilebilir renk paletleri ve WCAG düzeltmeleri, PDF dışı format desteği, fotoğraf yüklemenin ayarlara taşınması.

Detaylı plan için `SPRINT-3-PLAN.md`, ürün yol haritası için `ROADMAP.md`, mevcut durum analizi için `MEVCUT-DURUM-VE-UI.md` dosyalarına bakılabilir.
