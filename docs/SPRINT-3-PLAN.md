# Sprint 3 — Faz Planı ve Claude Code Çalışma Rehberi

> Bu belge Sprint 3'ü uygulanabilir fazlara böler ve her fazda Claude Code ile atacağın adımları sırayla verir.
> Her faz tek başına çalışır durumda bitmelidir — yarım kalan faz bir sonrakini bloke etmemeli.

**Hedef puan:** 21
**Süre:** 1 hafta
**Sprint hedefi:** Mülakatı kişiselleştirilebilir ve herkes için erişilebilir hale getirmek

---

## Çalışma prensipleri

Bu sprinti Claude Code ile yürüteceksin. Birkaç kural işi ciddi ölçüde hızlandırır:

1. **Her faz kendi branch'inde.** `feature/interview-modes`, `feature/dark-mode` gibi. Sprint 2'de tüm kod tek dev commit halinde girmiş — bu sefer öyle olmasın, hem review edilebilir hem geri alınabilir olur.
2. **Faz başlangıcında bağlam ver, sonuna kadar aynı oturumda kal.** Yeni oturum açtığında `CLAUDE.md`'yi okutup devam et.
3. **Önce plan, sonra kod.** Her faza "önce dosyaları oku ve plan çıkar, kod yazma" diyerek başla. Planı onayladıktan sonra yazdır.
4. **Her faz sonunda `dotnet build` çalıştır.** Razor derleme hataları çalıştırmadan görünmez.
5. **Bir seferde bir dosya.** `Session.cshtml` 618 satır; komple yeniden yazdırmak yerine hedefli düzenlemeler iste.

---

# FAZ 0 — Zemin hazırlığı (2 puan)

Bunlar özellik değil ama sonraki her faz bunlara yaslanacak. Yarım gün sürer, atlarsan sonraki fazlarda iki katı zaman kaybedersin.

## Adım 0.1 — CLAUDE.md'yi projeye ekle

`CLAUDE.md` dosyasını repo kökündeki `CvInterviewPlatform/` klasörüne koy. Claude Code her oturumda bunu otomatik okur; mimariyi, konvansiyonları ve tuzakları tekrar tekrar anlatmak zorunda kalmazsın.

## Adım 0.2 — Bootstrap sürümünü doğrula

Karanlık mod fazının tamamı Bootstrap 5.3'ün `data-bs-theme` özelliğine dayanıyor. Sürüm 5.3'ün altındaysa önce güncellenmeli.

> **Claude Code'a:**
> `wwwroot/lib/bootstrap/dist/css/bootstrap.css` dosyasının ilk 10 satırındaki sürüm numarasını oku ve söyle. 5.3'ün altındaysa `libman.json` veya kullanılan paket yöneticisi üzerinden 5.3.x'e nasıl güncelleyeceğimi anlat, ama henüz güncelleme yapma.

## Adım 0.3 — Session.cshtml'deki JavaScript'i ayır

Bu tek başına sonraki iki fazı çok kolaylaştırır. Şu an tema, sayaç, TTS, STT ve rapor indirme kodu aynı dosyada iç içe.

> **Claude Code'a:**
> `Views/Interview/Session.cshtml` içindeki `@section Scripts` bloğunu `wwwroot/js/interview.js` dosyasına taşı. Razor'a özel değerler (`@Model.SessionId`, `@Model.CurrentQuestionNumber`, `@Model.IsCompleted`, `@Model.JobTitle`) JS dosyasına doğrudan gömülemez; bunları view'da bir `data-*` attribute'ları taşıyan gizli div ile veya `window.interviewConfig` nesnesiyle dışarı aktar. Davranış birebir aynı kalmalı. Taşıma sonrası dosyayı bana göster, ben tarayıcıda test edeceğim.

**Faz 0 tamamlanma kriteri:** Proje derleniyor, mülakat ekranı eskisi gibi çalışıyor, `interview.js` ayrı dosyada.

---

# FAZ 1 — Mülakat modları ve zorluk seviyesi (8 puan)

Sprint'in en yüksek değerli parçası. Kullanıcı ilk defa deneyimi üzerinde söz sahibi oluyor.

## Adım 1.1 — Veri modelini genişlet

> **Claude Code'a:**
> `Models/InterviewSession.cs` dosyasına şu alanları ekle. Firestore attribute'larını mevcut konvansiyona uygun yaz (camelCase property adı):
> - `InterviewMode Mode` — enum: `Preparation`, `Realistic`. Firestore enum'ları doğrudan desteklemediği için string olarak sakla.
> - `int TimeLimitSeconds` — varsayılan 300
> - `string DifficultyLevel` — "Junior" | "Mid" | "Senior", varsayılan "Mid"
> - `int TotalQuestions` — varsayılan 5 (soru sayısını sabit 5'ten çıkarmaya hazırlık)
>
> Mevcut Firestore dokümanlarında bu alanlar yok; `ConvertTo<InterviewSession>()` çağrısı eski kayıtlarda patlamamalı. Varsayılan değerlerin bunu güvence altına aldığını doğrula.

**Dikkat:** Firestore'da var olan eski oturumlar bu alanlara sahip değil. Geri uyumluluğu mutlaka test et — mülakat geçmişi sayfası eski kayıtlarda çökerse demo biter.

## Adım 1.2 — Başlatma ekranını yeniden tasarla

Şu an `Interview/Index.cshtml`'de tek bir metin kutusu var. Artık bir konfigürasyon adımı gerekiyor.

> **Claude Code'a:**
> `Views/Interview/Index.cshtml` içindeki "Yeni Mülakat" kartını genişlet:
> - Pozisyon adı (mevcut)
> - Mod seçimi: iki radyo kartı. **Hazırlık Modu** — "Süre baskısı yok, ipucu alabilirsin, soruyu tekrar dinleyebilirsin." **Gerçekçi Mod** — "Soru başına 1 dakika, ipucu yok, gerçek mülakat temposu."
> - Zorluk: üç butonlu grup (Junior / Mid / Senior), her birinin altında tek satır açıklama
> - Hazırlık modu seçiliyse süre için ek seçenek: Süresiz / 5 dakika / 10 dakika
>
> Bootstrap 5 kart ve form yapısı kullan, mevcut sayfanın görsel diliyle uyumlu olsun. Seçili kartın kenarlığı belirgin olsun. Yeni CSS dosyası açma, mevcut `site.css`'e ekle. Radyo butonlarını gizleyip kartın kendisini tıklanabilir yap ama klavye erişimini bozma — `<label>` sarmalaması kullan, `display:none` değil `.visually-hidden` ile gizle.

## Adım 1.3 — Controller'ı güncelle

> **Claude Code'a:**
> `InterviewController.StartInterview` imzasını yeni parametreleri alacak şekilde genişlet: `mode`, `difficulty`, `timeLimit`. Gelen değerleri **beyaz liste ile doğrula** — kullanıcıdan gelen string'i doğrudan Firestore'a yazma. Geçersiz değer gelirse varsayılana düş. Gerçekçi mod seçildiyse `TimeLimitSeconds` her koşulda 60 olsun, kullanıcının gönderdiği değere bakma.

## Adım 1.4 — Prompt katmanını modlara duyarlı hale getir

Burası işin kalitesini belirleyen kısım. Acele etme.

> **Claude Code'a:**
> `Services/GeminiService.cs` içinde:
> 1. `GenerateQuestionAsync` imzasına `InterviewSession session` parametresi ekle (tek tek jobTitle/cvContent geçmek yerine oturumun kendisini geçmek daha temiz olur, refactor et).
> 2. Zorluk seviyesine göre prompt fragment'ı üreten `private string GetDifficultyInstruction(string level)` metodu yaz:
>    - Junior: temel kavram bilgisi, öğrenme isteği, staj ve okul projeleri, "nasıl öğrendin" tarzı sorular
>    - Mid: uygulamalı senaryolar, takım içi problem çözme, hata ayıklama deneyimi, "şöyle bir durumda ne yapardın"
>    - Senior: mimari kararlar, teknik liderlik, ödünleşim analizi, "neden bu yaklaşımı seçtin ve alternatifi neydi"
> 3. Moda göre ton fragment'ı üreten `private string GetModeInstruction(InterviewMode mode)`:
>    - Hazırlık: destekleyici ton, soru öncesi kısa bağlam cümlesi, adayı rahatlatan dil
>    - Gerçekçi: doğrudan ve kısa, gereksiz nezaket cümlesi yok, gerçek mülakat temposu
> 4. `GenerateEvaluationAsync` prompt'una şu satırı ekle: "Bu aday {seviye} seviye pozisyon için değerlendiriliyor. Puanlamayı bu seviyenin beklentilerine göre kalibre et; junior bir adayı senior standardıyla değerlendirme."
>
> Sistem talimatını (`GetSystemInstruction`) değiştirme, persona aynı kalsın. Değişiklikler sadece kullanıcı prompt'unda olsun.

## Adım 1.5 — İpucu özelliği (yalnızca hazırlık modu)

> **Claude Code'a:**
> `GeminiService`'e `GenerateHintAsync(string question, string jobTitle, string difficulty)` metodu ekle. Bu metot **cevabı vermemeli**; adayın hangi noktalara değinmesi gerektiğini 2-3 madde halinde söylemeli. Prompt'ta bunu açıkça belirt: "Cevabı yazma, sadece adayın hangi konulara değinmesi gerektiğini maddeler halinde söyle."
>
> `InterviewController`'a `[HttpPost] GetHint(string id)` action'ı ekle. JSON döndürsün. Oturum sahipliği kontrolü diğer action'lardaki desenle aynı olsun. Hazırlık modu değilse `BadRequest` dön — istemci tarafına güvenme.
>
> `Session.cshtml`'de sadece hazırlık modunda görünen bir "İpucu al" butonu ekle, tıklanınca fetch ile çağırıp sonucu soru altında bir kutuda göster.

## Adım 1.6 — Sayacı dinamikleştir

> **Claude Code'a:**
> `wwwroot/js/interview.js` içindeki sabit `300` değerini `window.interviewConfig.timeLimitSeconds` ile değiştir. Süre 0 veya negatifse (süresiz mod) sayaç tamamen devre dışı kalsın ve sayaç kutusu DOM'da hiç render edilmesin. Uyarı eşiği sabit 60 saniye değil, toplam sürenin %20'si olsun; 1 dakikalık modda 60 saniyelik uyarı anlamsız olur.

## Adım 1.7 — Geçmiş tablosuna rozetleri ekle

> **Claude Code'a:**
> `Views/Interview/Index.cshtml`'deki geçmiş tablosuna mod ve zorluk rozetleri ekle. Eski kayıtlarda bu alanlar boş olacak; boşsa rozet gösterme, tabloyu bozma.

**Faz 1 tamamlanma kriteri**
- Her iki modda mülakat başlatılabiliyor
- Gerçekçi modda sayaç 1 dakikadan başlıyor, hazırlık modunda seçilen süreden
- Üç zorluk seviyesinde üretilen sorular gözle bakıldığında farklı
- İpucu butonu sadece hazırlık modunda görünüyor ve cevabı vermiyor
- Eski mülakat kayıtları hâlâ açılıyor

---

# FAZ 2 — Tema altyapısı ve karanlık mod (5 puan)

## Adım 2.1 — Tema değişkenlerini kur

> **Claude Code'a:**
> `wwwroot/css/site.css` dosyasına CSS custom property tabanlı bir tema katmanı kur. Bootstrap 5.3'ün `data-bs-theme` mekanizmasının üstüne bin, paralel bir sistem kurma. Tanımlayacağın değişkenler:
> `--app-bg`, `--app-surface`, `--app-text`, `--app-text-muted`, `--app-border`, `--app-accent`, `--app-warning`, `--app-danger`, `--app-success`
>
> `[data-bs-theme="light"]` ve `[data-bs-theme="dark"]` blokları için değerleri ayrı ayrı ver. Karanlık temada saf siyah (`#000`) kullanma, `#1a1d20` civarı daha okunabilir. Açık temada saf beyaz yerine hafif kırık beyaz kullan — bu aynı zamanda Faz 3'teki fotofobi ihtiyacına zemin hazırlar.

## Adım 2.2 — FOUC engelini kur

> **Claude Code'a:**
> `Views/Shared/_Layout.cshtml` dosyasının `<head>` bölümüne, **CSS linklerinden önce** çalışan minimal bir inline script ekle. localStorage'dan tema tercihini okuyup `document.documentElement`'e `data-bs-theme` attribute'unu anında yazsın. Tercih yoksa `prefers-color-scheme` medya sorgusuna baksın. Bu script harici dosyada olamaz — sayfa render'ından önce senkron çalışması gerekiyor.
>
> Ayrıca `<html lang="en">` ifadesini `<html lang="tr">` yap. İçerik Türkçe, bu WCAG 3.1.1 ihlali ve ekran okuyucular yanlış dilde okuyor.

## Adım 2.3 — Tema değiştiriciyi ekle

> **Claude Code'a:**
> Navbar'a üç seçenekli bir tema değiştirici ekle: Açık / Koyu / Sistem. Bootstrap dropdown kullan, ikon olarak Bootstrap Icons'tan `bi-sun-fill`, `bi-moon-stars-fill`, `bi-circle-half`. Seçim localStorage'a yazılsın, `data-bs-theme` anında güncellensin, sayfa yenilenmesin. Butona `aria-label` ver, aktif seçeneğe `aria-current` koy.

## Adım 2.4 — Hardcode edilmiş renkleri temizle

İşin en sıkıcı ama en gerekli kısmı. Sistematik git.

> **Claude Code'a:**
> Tüm `.cshtml` dosyalarını tara ve karanlık temada kırılacak sınıfları listele: `text-dark`, `bg-light`, `bg-white`, `text-muted`, `border-secondary-subtle` ve inline `style="color:..."` kullanımları. **Önce sadece listeyi ver, dosya dosya, satır numarasıyla.** Değişiklik yapma.
>
> Listeyi gözden geçirdikten sonra dosya dosya ilerleyeceğiz. `Session.cshtml` en yoğunu, onu en sona bırak.

Liste geldikten sonra her dosya için ayrı ayrı ilerle. Toplu değişiklik isteme; Razor dosyalarında toplu replace kolayca bozar.

## Adım 2.5 — PDF çıktısını koru

> **Claude Code'a:**
> `interview.js` içindeki PDF indirme fonksiyonu yeni bir pencere açıp `cardElement.outerHTML` yazıyor. Bu pencere her zaman açık temada render edilmeli — koyu temada yazdırılırsa kağıt israfı ve okunmaz çıktı olur. Açılan pencerenin `<html>` elementine `data-bs-theme="light"` yaz ve tema değişkenlerinin açık değerlerini o pencereye enjekte et.

**Faz 2 tamamlanma kriteri**
- Üç tema seçeneği çalışıyor, sayfa yenilenince korunuyor
- Sayfa açılışında beyaz parlama yok
- Tüm sayfalar koyu temada okunabilir (giriş, kayıt, panel, ayarlar, mülakat, rapor)
- PDF çıktısı koyu temada da açık renkte çıkıyor
- `lang="tr"` düzeltilmiş

---

# FAZ 3 — Erişilebilirlik (5 puan)

Bu fazı "ekstra" olarak değil, ürünün kalite eşiği olarak ele al. Sprint sunumunda en çok konuşulacak kısım burası olabilir.

## Adım 3.1 — Erişilebilir paletleri tanımla

> **Claude Code'a:**
> Faz 2'de kurduğun tema değişkenlerine üç yeni mod ekle. Bunlar `data-app-palette` attribute'u ile açılsın, `data-bs-theme`'den bağımsız çalışsın:
>
> 1. `high-contrast` — metin/zemin kontrastı en az 7:1 (WCAG AAA), tüm kenarlıklar belirgin, gri tonlar yok
> 2. `low-light` — albinizm ve fotofobi için: düşük parlaklıkta zemin, saf beyaz kesinlikle yok, kontrast yeterli ama göz yormayan, sıcak tonlu
> 3. `colorblind-safe` — Okabe-Ito paleti temel alınsın (protanopi, döteranopi ve tritanopi için ayırt edilebilir olduğu doğrulanmış 8 renklik bilimsel palet). Kırmızı-yeşil ikilisine dayanan hiçbir ayrım kalmasın.
>
> Her palet için seçtiğin hex değerlerinin kontrast oranlarını da yaz, doğrulayabileyim.

## Adım 3.2 — Erişilebilirlik panelini kur

> **Claude Code'a:**
> Navbar'a erişilebilirlik menüsü ekle (`bi-universal-access` ikonu). İçinde:
> - **Renk paleti**: Varsayılan / Yüksek kontrast / Düşük parlaklık / Renk körlüğü güvenli
> - **Yazı boyutu**: Normal / Büyük (%125) / Çok büyük (%150)
> - **Animasyonlar**: Açık / Kapalı
>
> Tüm tercihler localStorage'da saklansın. Yazı boyutu `html` elementinin `font-size` değerini değiştirsin, rem tabanlı ölçekleme çalışsın. Bootstrap'in px değerleri bundan etkilenmez, kritik yerlerde rem'e çevir.
> Menü klavyeyle tam kullanılabilir olsun, her seçeneğin `aria-label`'ı olsun.

## Adım 3.3 — Rengin tek başına bilgi taşımasını engelle

> **Claude Code'a:**
> `interview.js` içindeki sayaç uyarısı şu an sadece renk değiştiriyor ve yanıp sönüyor. Bu WCAG 1.4.1 ihlali. Şuna çevir:
> - Renk değişimi kalsın
> - Yanına uyarı ikonu eklensin (`bi-exclamation-triangle-fill`)
> - Metin eklensin: "Son 1 dakika"
> - Sayaç kutusuna `role="timer"` ve `aria-live="polite"` eklensin; her 30 saniyede bir değil, sadece eşik geçildiğinde duyurulsun (sürekli duyuru ekran okuyucu kullanıcısını boğar)

## Adım 3.4 — Animasyonları kontrol edilebilir yap

> **Claude Code'a:**
> `Session.cshtml`'deki `pulseMic` ve `blinkTimer` keyframe animasyonlarını şu iki koşula bağla:
> 1. `@media (prefers-reduced-motion: reduce)` içinde tamamen kapalı
> 2. Erişilebilirlik panelinden "Animasyonlar: Kapalı" seçilirse `html[data-app-motion="off"]` selektörüyle kapalı
>
> Animasyon kapalıyken mikrofonun kayıtta olduğu **başka bir yolla** anlaşılmalı — kalıcı kırmızı arka plan ve buton metninin "Kaydediliyor…" olması yeterli. Bilgi kaybolmamalı, sadece hareket durmalı.

## Adım 3.5 — Sesli mülakatı ekran okuyucuya bağla

> **Claude Code'a:**
> Sesli mülakat kontrollerine ARIA desteği ekle:
> - TTS butonu: `aria-label="Soruyu sesli oku"` / okurken `aria-label="Okumayı durdur"`, `aria-pressed` durumu
> - STT butonu: `aria-label="Sesli yanıt ver"`, kayıt sırasında `aria-pressed="true"`
> - Kayıt başladı/durdu bilgisi için görsel olarak gizli bir `aria-live="assertive"` bölgesi ekle
> - Soru metnine `aria-live="polite"` — yeni soru geldiğinde duyurulsun

## Adım 3.6 — Form etiketlerini ve odak halkalarını düzelt

> **Claude Code'a:**
> Tüm formları (`Account/SignIn`, `Account/Register`, `Account/ForgotPassword`, `Home/Settings`, `Interview/Index`) tara:
> - Her input'un `id`'si ve karşılık gelen `<label for="...">` bağı var mı
> - Hata mesajları `role="alert"` taşıyor mu
> - Zorunlu alanlarda `aria-required="true"` var mı
> - Tüm interaktif öğelerde görünür odak halkası var mı — Bootstrap'in varsayılanı bazı yerlerde `outline: none` ile eziliyor olabilir
>
> Önce eksiklerin listesini ver, sonra düzeltelim.

**Faz 3 tamamlanma kriteri**
- Dört renk paleti çalışıyor ve tercihler korunuyor
- Yazı boyutu %150'ye çıkarıldığında hiçbir sayfa bozulmuyor
- Animasyonlar kapatılabiliyor ve kapalıyken bilgi kaybı olmuyor
- axe DevTools taramasında kritik hata yok
- Tüm mülakat akışı yalnızca klavyeyle tamamlanabiliyor

---

# FAZ 4 — Dosya formatları ve profil düzenlemesi (5 puan)

## Adım 4.1 — Python servisini genişlet

> **Claude Code'a:**
> `CvParserService/main.py` dosyasını güncelle:
> - `ALLOWED_EXTENSIONS = {".pdf", ".docx", ".txt", ".md", ".png", ".jpg", ".jpeg"}` seti tanımla
> - Uzantı doğrulamasını bu sete göre yap, sadece `.pdf`'e değil
> - `DocumentConverter` başlatılırken `allowed_formats` parametresiyle bu formatları etkinleştir
> - Geçici dosyayı oluştururken orijinal uzantıyı koru — Docling formatı uzantıdan çıkarıyor, `.pdf` suffix'i sabit kalırsa DOCX dosyası PDF sanılır ve parse patlar
> - Dosya boyutu sınırı ekle: 10 MB üstünü reddet
> - `/health` endpoint'ine desteklenen format listesini de ekle

## Adım 4.2 — .NET tarafındaki sabit dosya adını düzelt

Bu, formatın çalışmasını engelleyen asıl satır.

> **Claude Code'a:**
> `Services/CvParserService.cs` içinde şu satır her dosyayı PDF olarak gönderiyor:
> ```csharp
> form.Add(streamContent, "file", "cv_upload.pdf");
> ```
> `ParsePdfAsync` metodunu `ParseDocumentAsync` olarak yeniden adlandır ve dosya adını orijinal uzantıyı koruyacak şekilde üret. Dosya adı ASCII güvenli kalmalı (Türkçe karakterli dosya adları multipart header'da soruna yol açabiliyor) ama uzantı doğru olmalı — örneğin `cv_upload.docx`. Çağıran yerdeki `HomeController` referansını da güncelle.

## Adım 4.3 — Sunucu taraflı yükleme doğrulaması

Şu an hiç yok. Görünüşte `accept` attribute'u var ama bu sadece tarayıcı dosya seçicisini filtreler; API'ye doğrudan istek atan biri istediğini yükler.

> **Claude Code'a:**
> `HomeController` içindeki yükleme mantığına sunucu taraflı doğrulama ekle:
> - CV için izinli uzantılar: `.pdf`, `.docx`, `.txt`, `.md`, `.png`, `.jpg`, `.jpeg`
> - Profil fotoğrafı için: `.png`, `.jpg`, `.jpeg`, `.webp`
> - Boyut sınırı: CV 10 MB, fotoğraf 5 MB
> - Uzantıyı **kullanıcının gönderdiği dosya adından değil**, izinli listeyle eşleştirerek belirle. Şu anki `Path.GetExtension(profilePicture.FileName)` kullanımı kullanıcı girdisini doğrudan diske yazılan dosya adına taşıyor — `wwwroot` altına `.html` uzantılı dosya yazılabilir.
> - Reddedilen dosyalar için anlaşılır Türkçe `TempData["Error"]` mesajı

## Adım 4.4 — Fotoğraf yüklemeyi ayarlara taşı

> **Claude Code'a:**
> `HomeController.UploadDocuments` action'ını ikiye böl:
> - `[HttpPost] UploadCv(IFormFile cvFile)` — ana panelde kalır
> - `[HttpPost] UploadProfilePicture(IFormFile profilePicture)` — ayarlar sayfasına taşınır
>
> `Views/Home/Settings.cshtml`'e fotoğraf yükleme bölümü ekle: mevcut fotoğrafın önizlemesi, dosya seçici, yükle butonu. Form `enctype="multipart/form-data"` taşımalı.
> `Views/Home/Index.cshtml`'den fotoğraf input'unu kaldır; profil kartı fotoğrafı göstermeye devam etsin ama altında "Fotoğrafı değiştir" linki ayarlara gitsin.
> Ana panel artık sadece CV yükleme ve mülakat başlatmaya odaklanmalı.

**Faz 4 tamamlanma kriteri**
- DOCX ve TXT dosyaları başarıyla parse ediliyor, içerik Firestore'a yazılıyor
- Desteklenmeyen format anlaşılır hata veriyor
- 10 MB üstü dosya reddediliyor
- Fotoğraf yükleme ayarlar sayfasında, ana panel sadeleşmiş

---

# FAZ 5 — Kapanış (yaklaşık yarım gün)

## Adım 5.1 — Regresyon testi

Elle geçilecek senaryo listesi:

| # | Senaryo | Beklenen |
|---|---|---|
| 1 | Yeni kullanıcı kaydı → giriş | Çalışıyor |
| 2 | PDF CV yükleme | Parse ediliyor |
| 3 | DOCX CV yükleme | Parse ediliyor |
| 4 | 20 MB dosya yükleme | Reddediliyor, hata mesajı görünüyor |
| 5 | Hazırlık modu + Junior + süresiz mülakat | Sayaç yok, ipucu butonu var |
| 6 | Gerçekçi mod + Senior + 5 soru | Sayaç 1 dk, ipucu yok |
| 7 | Süre dolunca otomatik geçiş | Çalışıyor |
| 8 | Sprint 2'de oluşturulmuş eski mülakatı açma | Çökmüyor |
| 9 | Koyu temada tüm sayfalar | Okunabilir |
| 10 | Dört palet arası geçiş | Tercih korunuyor |
| 11 | Yazı boyutu %150 | Düzen bozulmuyor |
| 12 | Yalnızca klavyeyle tam mülakat | Tamamlanabiliyor |
| 13 | Koyu temada PDF indirme | Açık renkte çıkıyor |
| 14 | Fotoğraf değiştirme (ayarlardan) | Çalışıyor |

## Adım 5.2 — README güncelleme

> **Claude Code'a:**
> `README.md` dosyasına Sprint 2 bölümünün altına aynı `<details>` yapısında Sprint 3 bölümü ekle. Alt sekmeler: Site Ekran Görüntüleri, Proje Yönetimi, Daily Scrum, Sprint Notları (içinde Sprint Değerlendirmesi ve Retrospective). HTML yapısını mevcut Sprint 2 bölümüyle birebir aynı tut, iç içe `<details>` kapanışlarına dikkat et — Sprint 1 bölümünde kapanış etiketleri düzensiz, aynı hatayı tekrarlama.

## Adım 5.3 — Sprint retrospektifi için notlar

Sprint boyunca şunları not al, retro toplantısında işine yarar:
- Hangi tahmin tuttu, hangisi tutmadı
- Claude Code ile hangi adım beklenenden uzun sürdü
- Karanlık mod refactor'ünde kaç dosya dokunuldu (bir sonraki tema işi için referans)
- Erişilebilirlik denetiminde çıkan hata sayısı (öncesi/sonrası)

---

## Zaman dağılımı önerisi

| Gün | Faz | Puan |
|---|---|---|
| Pazartesi | Faz 0 + Faz 1 başlangıç | 2 + 3 |
| Salı | Faz 1 tamamlama | 5 |
| Çarşamba | Faz 2 | 5 |
| Perşembe | Faz 3 | 5 |
| Cuma | Faz 4 | 5 |
| Cumartesi | Faz 5 + tampon | — |

Cumartesiyi tampon olarak boş bırak. Her sprintte bir şey beklenenden uzun sürer; bu sprintte muhtemelen Faz 2'deki hardcode renk temizliği olacak.
