using ConnectFour.Hubs;
using ConnectFour.Services;
using ConnectFour.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

builder.Services.AddSingleton<GameEngine>();
builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<BackgroundTaskService>();
builder.Services.AddHostedService<BackgroundTaskService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<GameHub>("/gameHub");

app.Run();
