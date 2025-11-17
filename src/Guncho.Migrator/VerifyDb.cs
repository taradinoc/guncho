using Guncho.Data;
using Microsoft.EntityFrameworkCore;

if (args.Length < 1)
{
    Console.WriteLine("Usage: VerifyDb <DatabasePath>");
    return;
}

var dbPath = args[0];
var optionsBuilder = new DbContextOptionsBuilder<GunchoDbContext>();
optionsBuilder.UseSqlite($"Data Source={dbPath}");

using var dbContext = new GunchoDbContext(optionsBuilder.Options);

// Check tables exist
Console.WriteLine("Database Tables:");
Console.WriteLine("================");

var players = await dbContext.Players.CountAsync();
Console.WriteLine($"Players: {players}");

var playerAttributes = await dbContext.PlayerAttributes.CountAsync();
Console.WriteLine($"PlayerAttributes: {playerAttributes}");

var realms = await dbContext.Realms.CountAsync();
Console.WriteLine($"Realms: {realms}");

var realmAssets = await dbContext.RealmAssets.CountAsync();
Console.WriteLine($"RealmAssets: {realmAssets}");

var realmAccess = await dbContext.RealmAccess.CountAsync();
Console.WriteLine($"RealmAccess: {realmAccess}");

var storageEntries = await dbContext.StorageEntries.CountAsync();
Console.WriteLine($"StorageEntries: {storageEntries}");

Console.WriteLine();
Console.WriteLine("Realm Assets:");
Console.WriteLine("=============");

var assets = await dbContext.RealmAssets
    .Include(a => a.Realm)
    .ToListAsync();

foreach (var asset in assets)
{
    Console.WriteLine($"- Realm: {asset.Realm.Name}");
    Console.WriteLine($"  Name: {asset.Name}");
    Console.WriteLine($"  Type: {asset.ContentType}");
    Console.WriteLine($"  Size: {asset.Content.Length:N0} bytes");
    Console.WriteLine($"  Updated: {asset.UpdatedAt}");
    Console.WriteLine();
}
