namespace Guncho.Services
{
    /// <summary>
    /// Provides configuration settings for the game server.
    /// </summary>
    public interface IServerConfiguration
    {
        string CachePath { get; }
        string IndexPath { get; }
        string RealmDataPath { get; }
        string LogPath { get; }
        string NiInstallationsPath { get; }
        string NiSkeletonPath { get; }
        string StartRealmName { get; }
        int CompilerTimeout { get; }
        int TransactionTimeout { get; }
        int RealmFailuresAllowed { get; }
        string WebAuthSecret { get; }
        uint MaxHeapSize { get; }
        string MotdFileName { get; }
        string GuestMotdFileName { get; }
        string ConnectTextFileName { get; }
        int GameServerPort { get; }
        bool FilterBlankLines { get; }

        string Inform6CompilerPath { get; }
        string Inform6LibraryPath { get; }
        // Computed paths
        string MotdPath { get; }
        string GuestMotdPath { get; }
        string ConnectTextPath { get; }
    }
}
