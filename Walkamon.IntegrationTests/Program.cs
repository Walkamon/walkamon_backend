using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Claims;
using System.Text;
using BLL.Exceptions;
using BLL.Interfaces;
using BLL.Service;
using DAL.Data;
using DAL.DTO;
using DAL.GenericRepository;
using DAL.Models;
using DAL.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;

// Only an isolated, newly named LocalDB database is created/deleted. No appsettings or production connection.
var database = "WalkamonBRChecks_" + Guid.NewGuid().ToString("N");
var connection = $"Server=(localdb)\\WalkamonValidation;Database={database};Integrated Security=true;TrustServerCertificate=true";
var options = new DbContextOptionsBuilder<WalkamonContext>()
    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure()).Options;
var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
    DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")));
var roleId = 0;
var passed = 0;
Process? api = null;
var apiLog = new StringBuilder();
WalkamonContext Db() => new(options, new HttpContextAccessor());
GenericRepository<T> Repo<T>(WalkamonContext db) where T : class => new(db);
ShopService Shop(WalkamonContext db) => new(Repo<ShopItem>(db), Repo<Item>(db), Repo<ItemType>(db),
    Repo<Wallet>(db), Repo<InventoryItem>(db), Repo<ShopPurchase>(db), db);
FriendService Friends(WalkamonContext db) => new(Repo<FriendRequest>(db), Repo<Friendship>(db), new FriendRepository(db), db);
void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
    Console.WriteLine("PASS: " + message);
    passed++;
}
async Task Expect<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
async Task<bool> Attempt(Func<Task> action)
{
    try { await action(); return true; }
    catch (BadRequestException) { return false; }
    catch (ConflictException) { return false; }
}
async Task<Guid> Player(int balance = 1000)
{
    await using var db = Db();
    var id = Guid.NewGuid();
    var email = id.ToString("N") + "@br-test.invalid";
    db.Users.Add(new User
    {
        UserId = id, RoleId = roleId, Email = email, NormalizedEmail = email.ToUpperInvariant(),
        EmailConfirmed = true, StatusCode = "active", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        UserProfile = new UserProfile
        {
            UserId = id, Username = id.ToString("N")[..12], LanguageCode = "vi", ThemeCode = "light",
            TimeZoneId = "Asia/Ho_Chi_Minh", NotificationsEnabled = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        },
        Wallet = new Wallet { UserId = id, Balance = balance }
    });
    await db.SaveChangesAsync();
    return id;
}
async Task<BuyShopItemResponse> Buy(Guid user, Guid item, Guid key, int quantity = 2)
{
    await using var db = Db();
    return await Shop(db).BuyShopItemAsync(user, new BuyShopItemRequest { ShopItemId = item, Quantity = quantity, RequestId = key });
}
async Task Send(Guid sender, Guid receiver)
{
    await using var db = Db();
    await Friends(db).SendFriendRequestAsync(sender, new SendFriendRequestRequest { ReceiverUserId = receiver });
}
async Task Respond(Guid receiver, Guid request, bool accept)
{
    await using var db = Db();
    await Friends(db).RespondFriendRequestAsync(receiver, request, new RespondFriendRequestRequest { IsAccepted = accept });
}

try
{
    await using (var db = Db())
    {
        await db.Database.EnsureCreatedAsync();
        var role = new Role { RoleName = "User", RoleCode = "user", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        roleId = role.RoleId;
    }
    var user = await Player();
    var other = await Player();
    var shopId = Guid.NewGuid();
    var itemId = Guid.NewGuid();
    var petId = Guid.NewGuid();
    await using (var db = Db())
    {
        db.ShopItems.Add(new ShopItem
        {
            ShopItemId = shopId, PriceAmount = 50, IsActive = true,
            Item = new Item
            {
                ItemId = itemId, ItemName = "BR recovery", IsActive = true,
                ItemType = new ItemType { ItemTypeId = Guid.NewGuid(), ItemTypeName = "BR care", IsActive = true,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }
            }
        });
        db.Pets.Add(new Pet { PetId = petId, PetName = "BR spirit", Exp = 100, Energy = 100, Bond = 100, LifeForce = 100,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    var key = Guid.NewGuid();
    var same = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Buy(user, shopId, key)));
    await using (var db = Db())
    {
        Check(await db.ShopPurchases.CountAsync(x => x.PurchaseId == key) == 1 &&
            await db.Wallets.Where(x => x.UserId == user).Select(x => x.Balance).SingleAsync() == 900 &&
            await db.InventoryItems.Where(x => x.UserId == user && x.ItemId == itemId).Select(x => x.Quantity).SingleAsync() == 2 &&
            same.All(x => x.PurchaseId == key), "8 concurrent retries grant and charge once");
    }
    await Expect<ConflictException>(() => Buy(user, shopId, key, 3));
    await Expect<ConflictException>(() => Buy(other, shopId, key));
    Check(true, "retry key rejects changed payload and different owner");
    await Expect<BadRequestException>(() => Buy(user, shopId, Guid.Empty));
    await Expect<BadRequestException>(() => Buy(user, shopId, Guid.NewGuid(), 0));
    Check(true, "empty retry key and zero quantity are rejected before mutation");
    var funded = await Player(500);
    await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Buy(funded, shopId, Guid.NewGuid())));
    await using (var db = Db())
        Check(await db.Wallets.Where(x => x.UserId == funded).Select(x => x.Balance).SingleAsync() == 300 &&
            await db.InventoryItems.Where(x => x.UserId == funded && x.ItemId == itemId).Select(x => x.Quantity).SingleAsync() == 4,
            "distinct concurrent purchases preserve both balance and inventory updates");
    var poor = await Player(100);
    var buys = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Attempt(() => Buy(poor, shopId, Guid.NewGuid()))));
    await using (var db = Db())
    {
        Check(buys.Count(x => x) == 1 && await db.Wallets.Where(x => x.UserId == poor).Select(x => x.Balance).SingleAsync() == 0,
            "concurrent distinct purchases cannot overspend or overwrite balance");
    }
    await using (var db = Db())
    {
        var shop = await db.ShopItems.SingleAsync(x => x.ShopItemId == shopId);
        shop.IsActive = false;
        await db.SaveChangesAsync();
    }
    Check((await Buy(user, shopId, key)).PurchaseId == key, "successful retry remains valid after shop deactivation");
    await Expect<NotFoundException>(() => Buy(user, shopId, Guid.NewGuid()));
    await using (var db = Db())
    {
        var shop = await db.ShopItems.SingleAsync(x => x.ShopItemId == shopId);
        shop.IsActive = true;
        await db.SaveChangesAsync();
    }

    var failedKey = Guid.NewGuid();
    await using (var db = Db())
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER br_reject_audit ON audit_logs AFTER INSERT AS THROW 51000, 'Injected audit failure', 1;");
    await using (var db = Db())
    {
        (await db.UserProfiles.SingleAsync(x => x.UserId == user)).Bio = "must roll back";
        await Expect<DbUpdateException>(() => db.SaveChangesAsync());
    }
    await using (var db = Db())
        Check(await db.UserProfiles.Where(x => x.UserId == user).Select(x => x.Bio).SingleAsync() == null,
            "audit failure also rolls back a standalone save without an outer transaction");
    await using (var retryDb = Db())
    {
        var service = Shop(retryDb);
        var request = new BuyShopItemRequest { ShopItemId = shopId, Quantity = 2, RequestId = failedKey };
        await Expect<DbUpdateException>(() => service.BuyShopItemAsync(user, request));
        await using (var db = Db())
        {
            Check(!await db.ShopPurchases.AnyAsync(x => x.PurchaseId == failedKey) &&
                await db.Wallets.Where(x => x.UserId == user).Select(x => x.Balance).SingleAsync() == 900 &&
                await db.InventoryItems.Where(x => x.UserId == user && x.ItemId == itemId).Select(x => x.Quantity).SingleAsync() == 2,
                "audit failure rolls back purchase, wallet and inventory");
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER br_reject_audit;");
        }
        var retry = await service.BuyShopItemAsync(user, request);
        Check(retry.WalletBalance == 800 && retry.InventoryQuantity == 4, "retry after rollback reloads state, no double deduction");
    }

    var a = await Player();
    var b = await Player();
    var sends = await Task.WhenAll(Attempt(() => Send(a, b)), Attempt(() => Send(b, a)));
    FriendRequest pending;
    await using (var db = Db())
    {
        pending = await db.FriendRequests.SingleAsync();
        Check(sends.Count(x => x) == 1 && pending.StatusCode == "pending", "opposite concurrent friend requests produce one pending request");
    }
    await Respond(pending.ReceiverUserId, pending.RequestId, false);
    await Expect<ConflictException>(() => Respond(pending.ReceiverUserId, pending.RequestId, true));
    Check(true, "rejected friend request cannot be accepted again");
    await Send(a, b);
    Guid cancelId;
    await using (var db = Db())
    {
        cancelId = await db.FriendRequests.Where(x => x.StatusCode == "pending").Select(x => x.RequestId).SingleAsync();
        await Friends(db).CancelFriendRequestAsync(a, cancelId);
    }
    await Expect<ConflictException>(() => Respond(b, cancelId, true));
    Check(true, "cancelled friend request cannot be accepted");
    await Send(a, b);
    Guid acceptId;
    await using (var db = Db())
        acceptId = await db.FriendRequests.Where(x => x.StatusCode == "pending").Select(x => x.RequestId).SingleAsync();
    var responses = await Task.WhenAll(Attempt(() => Respond(b, acceptId, true)), Attempt(() => Respond(b, acceptId, true)));
    Check(responses.Count(x => x) == 1, "concurrent accepts create friendship once");
    await using (var db = Db())
    {
        await Expect<ConflictException>(() => Friends(db).CancelFriendRequestAsync(a, acceptId));
        await Friends(db).RemoveFriendAsync(a, b);
    }
    await Expect<ConflictException>(() => Respond(b, acceptId, true));
    Check(true, "removed friendship cannot be restored by accepting an old request");

    var walker = await Player();
    await using (var db = Db())
    {
        db.StepGoals.Add(new StepGoal { UserId = walker, EffectiveFrom = today.AddDays(-1), TargetSteps = 500 });
        db.StepGoals.Add(new StepGoal { UserId = walker, EffectiveFrom = today.AddDays(1), TargetSteps = 5000 });
        db.DailySteps.Add(new DailyStep { UserId = walker, StepDate = today.AddDays(-1), StepCount = 500, EligibleStepCount = 500, UpdatedAt = DateTime.UtcNow });
        db.DailySteps.Add(new DailyStep { UserId = walker, StepDate = today, StepCount = 1000, EligibleStepCount = 499, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await Expect<BadRequestException>(() => new StreakRewardService(db, new StepGoalRepository(db)).ClaimRewardAsync(walker));
    }
    Check(true, "yesterday's streak/raw steps do not qualify today's claim");
    await using (var db = Db())
    {
        var steps = await db.DailySteps.SingleAsync(x => x.UserId == walker && x.StepDate == today);
        steps.EligibleStepCount = steps.StepCount = 500;
        await db.SaveChangesAsync();
    }
    var claims = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
    {
        await using var db = Db();
        return await Attempt(() => new StreakRewardService(db, new StepGoalRepository(db)).ClaimRewardAsync(walker));
    }));
    await using (var db = Db())
    {
        Check(claims.Count(x => x) == 1 && await db.StreakRewardClaims.CountAsync(x => x.UserId == walker) == 1 &&
            await db.Wallets.Where(x => x.UserId == walker).Select(x => x.Balance).SingleAsync() == 1020,
            "completed current goal grants two-day streak once; future goal is ignored");
        await Expect<BadRequestException>(() => new StreakRewardService(db, new StepGoalRepository(db)).ClaimRewardAsync(other));
    }
    Check(true, "missing walking goal cannot claim");

    var disabled = await Player();
    var deleted = await Player();
    await using (var db = Db())
    {
        (await db.Users.SingleAsync(x => x.UserId == disabled)).StatusCode = "disabled";
        (await db.Users.SingleAsync(x => x.UserId == deleted)).DeletedAt = DateTime.UtcNow;
        foreach (var id in new[] { user, disabled, deleted })
            db.UserPets.Add(new UserPet { UserId = id, PetId = petId, PetName = "BR", Level = 1 });
        db.PetStages.AddRange(Enumerable.Range(1, 3).Select(n => new PetStage { StageId = Guid.NewGuid(), PetId = petId,
            StageNo = n, StageName = "Stage " + n, RequiredLevel = 15 * n, IsActive = n != 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }));
        await db.SaveChangesAsync();
        var pets = new PetRepository(db);
        Check((await pets.GetLeaderboardAsync()).Select(x => x.UserId).SequenceEqual(new[] { user }), "pet leaderboard excludes disabled and soft-deleted users");
        Check(await pets.GetFirstStageAsync(petId) == null, "inactive first evolution cannot skip directly to stage two");
        Check((await pets.GetNextStageAsync(petId, 1))?.StageNo == 2, "active next evolution remains selectable");
        (await db.PetStages.SingleAsync(x => x.PetId == petId && x.StageNo == 2)).IsActive = false;
        await db.SaveChangesAsync();
        Check(await pets.GetNextStageAsync(petId, 1) == null && (await pets.GetStagesByPetIdAsync(petId)).Count == 1,
            "inactive next stage is excluded without skipping stages");
        db.PetStages.Add(new PetStage { StageId = Guid.NewGuid(), PetId = petId, StageNo = 0, StageName = "Starter",
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Check((await pets.GetFirstStageAsync(petId))?.StageNo == 0, "first-stage lookup preserves stage-zero starter artwork");
    }

    await using (var db = Db())
    {
        (await db.UserProfiles.SingleAsync(x => x.UserId == other)).NotificationsEnabled = false;
        foreach (var id in new[] { user, other, disabled, deleted })
            db.DeviceTokens.Add(new DeviceToken { UserId = id, FcmToken = id.ToString(), IsActive = true, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }
    await using (var db = Db())
    {
        var fcm = new RecordingPush();
        var notices = new NotificationService(new NotificationRepository(db), fcm, NullLogger<NotificationService>.Instance);
        await notices.CreateAdminNotificationAsync(user, new CreateAdminNotificationRequest
        { TypeCode = "server_announcement", TargetAudienceCode = "all_users", Title = "BR test", Content = "Local only", SendNow = true });
        Check(fcm.Recipients.SequenceEqual(new[] { user }), "broadcast push respects opt-out and account status");
        Check(await db.UserNotifications.AnyAsync(x => x.UserId == other), "opt-out preserves in-app inbox and history");
        await notices.CreateAdminNotificationAsync(user, new CreateAdminNotificationRequest
        { TypeCode = "server_announcement", TargetAudienceCode = "all_users", Title = "BR scheduled", Content = "Local only",
            SendNow = false, ScheduleTime = DateTime.UtcNow.AddHours(1) });
        var pendingNotice = await db.Notifications.SingleAsync(x => x.StatusCode == "scheduled");
        pendingNotice.ScheduledAt = DateTime.UtcNow.AddSeconds(-1);
        (await db.UserProfiles.SingleAsync(x => x.UserId == user)).NotificationsEnabled = false;
        await db.SaveChangesAsync();
        var before = fcm.Recipients.Count;
        await notices.ProcessDueScheduledNotificationsAsync();
        Check(fcm.Recipients.Count == before, "scheduled broadcast respects opt-out changed after scheduling");
    }

    // Start the real API with an empty content root, no workers or production settings.
    var contentRoot = Path.Combine(Path.GetTempPath(), database);
    Directory.CreateDirectory(contentRoot);
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    var start = new ProcessStartInfo("dotnet") { WorkingDirectory = contentRoot, UseShellExecute = false,
        CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
    start.ArgumentList.Add(typeof(Walkamon.Controllers.ShopController).Assembly.Location);
    start.ArgumentList.Add("--urls"); start.ArgumentList.Add($"http://127.0.0.1:{port}");
    start.ArgumentList.Add("--contentRoot"); start.ArgumentList.Add(contentRoot);
    const string jwtKey = "local-br-regression-test-only-key-20260921";
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    start.Environment["DOTNET_ENVIRONMENT"] = "Development";
    start.Environment["ConnectionStrings__DefaultConnection"] = connection;
    start.Environment["Jwt__Key"] = jwtKey;
    start.Environment["Jwt__Issuer"] = "br-test";
    start.Environment["Jwt__Audience"] = "br-test";
    start.Environment["BackgroundServices__Enabled"] = "false";
    start.Environment["Logging__LogLevel__Default"] = "Warning";
    api = Process.Start(start) ?? throw new Exception("API did not start");
    api.OutputDataReceived += (_, e) => { lock (apiLog) apiLog.AppendLine(e.Data); };
    api.ErrorDataReceived += (_, e) => { lock (apiLog) apiLog.AppendLine(e.Data); };
    api.BeginOutputReadLine(); api.BeginErrorReadLine();
    using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(3) };
    var ready = false;
    for (var i = 0; i < 100 && !api.HasExited; i++)
    {
        try { ready = (await http.GetAsync("/health/live")).IsSuccessStatusCode; }
        catch (HttpRequestException) { }
        if (ready) break;
        await Task.Delay(100);
    }
    if (!ready) throw new Exception("Local API failed: " + apiLog);
    var jwt = new JwtSecurityTokenHandler().CreateEncodedJwt("br-test", "br-test", new ClaimsIdentity(new[]
    {
        new Claim(ClaimTypes.NameIdentifier, user.ToString()), new Claim(ClaimTypes.Role, "User")
    }), DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(10), DateTime.UtcNow,
        new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)), SecurityAlgorithms.HmacSha256));
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
    Check((await http.GetAsync("/api/shop")).StatusCode == HttpStatusCode.OK, "real JWT middleware accepts active account");
    await using (var db = Db())
    {
        (await db.Users.SingleAsync(x => x.UserId == user)).StatusCode = "disabled";
        await db.SaveChangesAsync();
    }
    Check((await http.GetAsync("/api/shop")).StatusCode == HttpStatusCode.Unauthorized, "same previously issued token is rejected immediately after disable");
    await using (var db = Db())
    {
        var account = await db.Users.SingleAsync(x => x.UserId == user);
        account.StatusCode = "active"; account.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
    Check((await http.GetAsync("/api/shop")).StatusCode == HttpStatusCode.Unauthorized, "active status cannot bypass soft deletion");
    Console.WriteLine($"SUCCESS: {passed} BR regression checks (SQL Server + real HTTP middleware)");
}
finally
{
    if (api is { HasExited: false }) { api.Kill(entireProcessTree: true); await api.WaitForExitAsync(); }
    api?.Dispose();
    SqlConnection.ClearAllPools();
    await using var cleanup = Db();
    await cleanup.Database.EnsureDeletedAsync();
}

sealed class RecordingPush : IFcmPushService
{
    public bool IsConfigured => true;
    public List<Guid> Recipients { get; } = [];
    public Task SendAsync(DeviceToken token, Notification notification, CancellationToken cancellationToken = default)
    {
        Recipients.Add(token.UserId);
        return Task.CompletedTask;
    }
}
