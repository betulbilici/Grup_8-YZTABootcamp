using CvInterviewPlatform.Web;
using CvInterviewPlatform.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.StaticFiles;

var builder = WebApplication.CreateBuilder(args);
// Firestore Servisini .NET sistemine tekil (Singleton) olarak kaydediyoruz
builder.Services.AddSingleton<FirestoreService>();
builder.Services.AddSingleton<GeminiService>();
builder.Services.AddHttpClient<CvParserService>();

// Session servislerini projeye ekliyoruz ve ayarlarn yapyoruz
// Session servislerini projeye ekliyoruz ve ayarlarn yapyoruz
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Oturum 30 dakika ilem yaplmazsa der
    options.Cookie.HttpOnly = true; // Gvenlik iin: Session erezlerine JavaScript ile eriilemez
    options.Cookie.IsEssential = true; // KVKK/GDPR erez onaylarna taklmamas iin zorunlu iaretliyoruz
});

// Google ile giriş: OAuth el sıkışması sırasında kimliği geçici tutacak çerez
// tabanlı bir "sign-in scheme" + Google external provider'ı kaydediyoruz.
// Not: Uygulamanın geri kalanı hâlâ kendi Session mekanizmasını kullanıyor —
// bu cookie şeması sadece Google'dan dönen kullanıcı bilgisini AccountController'a
// taşımak için bir köprü, yetkilendirme kontrollerinde kullanılmıyor.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
    .AddCookie()
    .AddGoogle(googleOptions =>
    {
        googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
        googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
        googleOptions.CallbackPath = "/signin-google";
    });


// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// wwwroot altındaki statik dosyalar (css/js/font/lib) için uzun ömürlü cache.
// asp-append-version="true" zaten içerik hash'i tabanlı ?v= sorgu string'i ürettiği
// için dosya değişince URL de değişir — bu yüzden immutable/uzun max-age güvenli.
// Bu olmadan tarayıcı her tam sayfa navigasyonunda tüm CSS/font'u yeniden doğrulamak
// zorunda kalıyor, bu da "geç yükleniyor" hissi veren görünür bir flaşa yol açıyordu.
// UseRouting()'den ÖNCE konumlandırılıyor ki bir endpoint eşleşmesi olmadan
// doğrudan bu middleware dosyayı sunup pipeline'ı kısa devre yapsın.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=604800,immutable";
    }
});

app.UseRouting();
// Session mekanizmasn boru hattna (pipeline) dahil ediyoruz
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
