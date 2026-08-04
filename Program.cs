using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RouteXWms.Data;
using RouteXWms.Filters;
using RouteXWms.Services;

// Webアプリケーションビルダーの初期化
var builder = WebApplication.CreateBuilder(args);

// 監査インターセプター（AuditInterceptor）等でHTTPコンテキストを取得するためのHttpContextAccessorを登録
builder.Services.AddHttpContextAccessor();

// カスタムサービス（最安倉庫選定サービスなど）の依存関係注入（DI）登録
builder.Services.AddScoped<CheapestWarehouseService>();

// データベースプロバイダーおよび接続文字列の取得
string dbProvider = builder.Configuration["DatabaseProvider"] ?? "SqlServer";
string sqlServerConn = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\mssqllocaldb;Database=RouteXWmsDb;Trusted_Connection=True;TrustServerCertificate=True;";
string sqliteConn = builder.Configuration.GetConnectionString("SqliteConnection")
    ?? "Data Source=RouteXWms.db";

// DbContextの設定（設定されたプロバイダーに応じて切り替え。SQL Server / Azure SQL / SQLite 対応）
builder.Services.AddDbContext<WmsDbContext>(options =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(sqliteConn);
    }
    else if (dbProvider.Equals("AzureSql", StringComparison.OrdinalIgnoreCase))
    {
        string azureConn = builder.Configuration.GetConnectionString("AzureSqlConnection") ?? sqlServerConn;
        options.UseSqlServer(azureConn, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    }
    else
    {
        // SQL Server LocalDB / オンプレミス SQL Server
        options.UseSqlServer(sqlServerConn, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
    }
});

// クッキー認証（Cookie Authentication）の追加（標準Claims認証プロバイダー）
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "RouteXWms.Auth";
    });

// セッション状態の管理とメモリキャッシュの設定
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8); // セッション有効期限: 8時間
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// MVCコントローラーおよびビューの追加
builder.Services.AddControllersWithViews();

// アプリケーションのビルド
var app = builder.Build();

// データベースの自動初期化および初期データ（管理者アカウント・マスターデータ・権限データ）の投入
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<WmsDbContext>();
    DbInitializer.Initialize(db);
}

// エラーハンドリングとHSTSの設定（本番環境用）
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 認証および認可・セッションミドルウェアの有効化
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// デフォルトルーティングの設定
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// アプリケーションの実行開始
app.Run();
