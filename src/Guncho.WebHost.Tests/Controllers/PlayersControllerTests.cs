using Guncho.Services;
using Guncho.Shared.Models;
using Guncho.WebHost.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Guncho.WebHost.Tests.Controllers;

public class PlayersControllerTests
{
    private readonly Mock<IPlayerService> _mockPlayerService;
    private readonly Mock<ILogger<PlayersController>> _mockLogger;
    private readonly PlayersController _controller;

    public PlayersControllerTests()
    {
        _mockPlayerService = new Mock<IPlayerService>();
        _mockLogger = new Mock<ILogger<PlayersController>>();
        _controller = new PlayersController(_mockPlayerService.Object, _mockLogger.Object);
    }

    [Fact]
    public void Get_ReturnsAllPlayers()
    {
        // Arrange
        var players = new List<Player>
        {
            new Player(1, "player1", false, false),
            new Player(2, "player2", true, false),
            new Player(3, "guest", false, true)
        };
        _mockPlayerService.Setup(s => s.GetAllPlayers()).Returns(players);

        // Act
        var result = _controller.Get().ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("player1", result[0].Name);
        Assert.Equal("player2", result[1].Name);
        Assert.True(result[1].IsAdmin);
        Assert.True(result[2].IsGuest);
    }

    [Fact]
    public async Task GetPlayerByNameAsync_WithValidName_ReturnsPlayer()
    {
        // Arrange
        var player = new Player(1, "testplayer", false, false);
        _mockPlayerService.Setup(s => s.GetPlayerByNameAsync("testplayer"))
            .ReturnsAsync(player);

        // Act
        var result = await _controller.GetPlayerByNameAsync("testplayer");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<PlayerDto>(okResult.Value);
        Assert.Equal("testplayer", dto.Name);
        Assert.Equal(1, dto.Id);
    }

    [Fact]
    public async Task GetPlayerByNameAsync_WithInvalidName_ReturnsNotFound()
    {
        // Arrange
        _mockPlayerService.Setup(s => s.GetPlayerByNameAsync("nonexistent"))
            .ReturnsAsync((Player?)null);

        // Act
        var result = await _controller.GetPlayerByNameAsync("nonexistent");

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPlayerByNameAsync_WithMe_ReturnsCurrentUser()
    {
        // Arrange
        var player = new Player(1, "currentuser", false, false);
        _mockPlayerService.Setup(s => s.GetPlayerByNameAsync("currentuser"))
            .ReturnsAsync(player);

        var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[]
        {
            new Claim(ClaimTypes.Name, "currentuser")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        // Act
        var result = await _controller.GetPlayerByNameAsync("me");

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<PlayerDto>(okResult.Value);
        Assert.Equal("currentuser", dto.Name);
    }

    [Fact]
    public async Task GetPlayerByNameAsync_WithMeButNoAuth_ReturnsUnauthorized()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await _controller.GetPlayerByNameAsync("me");

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }
}
