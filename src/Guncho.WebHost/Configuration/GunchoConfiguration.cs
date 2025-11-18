using Guncho.Services;

namespace Guncho.WebHost.Configuration
{
    /// <summary>
    /// ASP.NET Core implementation of IServerConfiguration.
    /// Reads values from IConfiguration (appsettings.json, environment variables, etc.)
    /// </summary>
    public class GunchoConfiguration : IServerConfiguration
    {
        private readonly IConfiguration _configuration;

        public GunchoConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CachePath => _configuration["Guncho:CachePath"] ?? "e:\\guncho\\Cache";
        public string IndexPath => Path.Combine(CachePath, "Index");
        public string RealmDataPath => _configuration["Guncho:RealmDataPath"] ?? "e:\\guncho\\RealmData";
        public string LogPath => _configuration["Guncho:LogPath"] ?? "e:\\guncho\\Logs";
        public string NiInstallationsPath => _configuration["Guncho:NiInstallationsPath"] ?? "e:\\guncho\\Factories\\Inform7";
        public string NiSkeletonPath => _configuration["Guncho:NiSkeletonPath"] ?? "e:\\guncho\\Factories\\Inform7\\Skeleton.inform";
        public string StartRealmName => _configuration["Guncho:StartRealmName"] ?? "The Outer Realm";
        public int CompilerTimeout => int.Parse(_configuration["Guncho:CompilerTimeout"] ?? "300000");
        public int TransactionTimeout => int.Parse(_configuration["Guncho:TransactionTimeout"] ?? "5000");
        public int RealmFailuresAllowed => int.Parse(_configuration["Guncho:RealmFailuresAllowed"] ?? "5");
        public string WebAuthSecret => _configuration["Guncho:WebAuthSecret"] ?? "";
        public uint MaxHeapSize => uint.Parse(_configuration["Guncho:MaxHeapSize"] ?? "33554432");
        public string MotdFileName => _configuration["Guncho:MotdFileName"] ?? "motd.txt";
        public string GuestMotdFileName => _configuration["Guncho:GuestMotdFileName"] ?? "guest.txt";
        public string ConnectTextFileName => _configuration["Guncho:ConnectTextFileName"] ?? "connect.txt";
        public int GameServerPort => int.Parse(_configuration["Guncho:GameServerPort"] ?? "4108");
        public bool FilterBlankLines => bool.TryParse(_configuration["Guncho:FilterBlankLines"], out var filtered) && filtered;

        public string Inform6CompilerPath => _configuration["Guncho:Inform6CompilerPath"] ?? "e:\\guncho\\Factories\\Inform6";
        public string Inform6LibraryPath => _configuration["Guncho:Inform6LibraryPath"] ?? "e:\\guncho\\Factories\\Inform6\\library";
        // Computed paths
        public string MotdPath => Path.Combine(RealmDataPath, MotdFileName);
        public string GuestMotdPath => Path.Combine(RealmDataPath, GuestMotdFileName);
        public string ConnectTextPath => Path.Combine(RealmDataPath, ConnectTextFileName);
    }
}
