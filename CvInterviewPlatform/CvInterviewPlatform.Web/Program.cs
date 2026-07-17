using CvInterviewPlatform.Web;
using CvInterviewPlatform.Web.Services;

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
app.UseRouting();
// Session mekanizmasn boru hattna (pipeline) dahil ediyoruz
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
