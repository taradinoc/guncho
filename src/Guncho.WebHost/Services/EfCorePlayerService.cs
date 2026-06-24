using Guncho.Repositories;
using Guncho.Services;
using Microsoft.EntityFrameworkCore;

namespace Guncho.WebHost.Services;

/// <summary>
/// EF Core-based implementation of IPlayerService using PlayerRepository.
/// This wraps the repository to maintain in-memory cache for performance.
/// </summary>
public class EfCorePlayerService : IPlayerService
{
    private readonly IDbContextFactory<Data.GunchoDbContext> _dbContextFactory;
    private readonly Microsoft.Extensions.Logging.ILogger<EfCorePlayerService> _logger;

    // In-memory cache for performance (still needed for event queue pattern)
    private readonly Dictionary<string, Player> _players = new();
    private readonly Dictionary<int, Player> _playersById = new();
    private readonly object _lock = new();

    public EfCorePlayerService(
        IDbContextFactory<Data.GunchoDbContext> dbContextFactory,
        Microsoft.Extensions.Logging.ILogger<EfCorePlayerService> logger)
    {
        _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync()
    {
        // Load all players into memory cache on startup
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var repository = new PlayerRepository(dbContext);
        var players = await repository.GetAllAsync();

        lock (_lock)
        {
            _players.Clear();
            _playersById.Clear();

            foreach (var player in players)
            {
                _players[player.Name.ToLower()] = player;
                _playersById[player.ID] = player;
            }
        }

        _logger.LogDebug($"Loaded {_players.Count} players from database");
    }

    public Task<Player?> GetPlayerByNameAsync(string name)
    {
        lock (_lock)
        {
            _players.TryGetValue(name.ToLower(), out var player);
            return Task.FromResult(player);
        }
    }

    public Task<Player?> GetPlayerByIdAsync(string id)
    {
        if (int.TryParse(id, out var playerId))
        {
            lock (_lock)
            {
                _playersById.TryGetValue(playerId, out var player);
                return Task.FromResult(player);
            }
        }
        return Task.FromResult<Player?>(null);
    }

    public Task<bool> ValidateLogInAsync(Player player, string password)
    {
        ArgumentNullException.ThrowIfNull(player);

        if (string.IsNullOrEmpty(player.PasswordHash))
            return Task.FromResult(false);

        // Support both old SHA1+salt format and new BCrypt format
        if (!string.IsNullOrEmpty(player.PasswordSalt))
        {
            // Old format: SHA1 with salt
            var hash = HashPasswordOldTimey(player.PasswordSalt, password);
            return Task.FromResult(hash == player.PasswordHash);
        }
        else
        {
            // New format: BCrypt (hash includes salt)
            try
            {
                return Task.FromResult(BCrypt.Net.BCrypt.Verify(password, player.PasswordHash));
            }
            catch
            {
                return Task.FromResult(false);
            }
        }
    }

    private static string HashPasswordOldTimey(string salt, string password)
    {
        var bytes = new List<byte>();
        if (salt != null)
            bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(salt));
        if (password != null)
            bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(password));

        using var sha1 = System.Security.Cryptography.SHA1.Create();
        byte[] hash = sha1.ComputeHash(bytes.ToArray());
        return Convert.ToBase64String(hash);
    }

    public async Task SavePlayerAsync(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        // Update database
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var repository = new PlayerRepository(dbContext);
        await repository.SaveAsync(player);

        // Update cache
        lock (_lock)
        {
            _players[player.Name.ToLower()] = player;
            _playersById[player.ID] = player;
        }

        _logger.LogDebug($"Saved player: {player.Name}");
    }

    public async Task DeletePlayerAsync(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);

        // Remove from database
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync();
        var repository = new PlayerRepository(dbContext);
        await repository.DeleteAsync(player);

        // Remove from cache
        lock (_lock)
        {
            _players.Remove(player.Name.ToLower());
            _playersById.Remove(player.ID);
        }

        _logger.LogDebug($"Deleted player: {player.Name}");
    }

    public IEnumerable<Player> GetAllPlayers()
    {
        lock (_lock)
        {
            return _players.Values.ToList();
        }
    }
}
