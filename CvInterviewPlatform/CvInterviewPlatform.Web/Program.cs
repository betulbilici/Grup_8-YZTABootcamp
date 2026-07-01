using CvInterviewPlatform.Web;

var builder = WebApplication.CreateBuilder(args);
// Firestore Servisini .NET sistemine tekil (Singleton) olarak kaydediyoruz
builder.Services.AddSingleton<FirestoreService>();

// Session servislerini projeye ekliyoruz ve ayarlarýný yapýyoruz
// Session servislerini projeye ekliyoruz ve ayarlarýný yapýyoruz
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Oturum 30 dakika iþlem yapýlmazsa düþer
    options.Cookie.HttpOnly = true; // Güvenlik için: Session çerezlerine JavaScript ile eriþilemez
    options.Cookie.IsEssential = true; // KVKK/GDPR çerez onaylarýna takýlmamasý için zorunlu iþaretliyoruz
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
// Session mekanizmasýný boru hattýna (pipeline) dahil ediyoruz
app.UseSession();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
