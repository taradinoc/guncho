using Guncho.Services;
using Xunit;

namespace Guncho.Engine.Tests.Services;

public class PlayerServiceTests
{
    [Fact]
    public void Player_Constructor_SetsPropertiesCorrectly()
    {
        // Arrange & Act
        var player = new Player(1, "testuser", false, false);

        // Assert
        Assert.Equal(1, player.ID);
        Assert.Equal("testuser", player.Name);
        Assert.False(player.IsAdmin);
        Assert.False(player.IsGuest);
    }

    [Fact]
    public void Player_SetAttribute_StoresValue()
    {
        // Arrange
        var player = new Player(1, "testuser", false, false);

        // Act
        player.SetAttribute("TestKey", "TestValue");

        // Assert
        Assert.Equal("TestValue", player.GetAttribute("TestKey"));
    }

    [Fact]
    public void Player_GetAttribute_WithNonexistentKey_ReturnsEmptyString()
    {
        // Arrange
        var player = new Player(1, "testuser", false, false);

        // Act
        var result = player.GetAttribute("NonexistentKey");

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Player_GetAllAttributes_ReturnsAllSetAttributes()
    {
        // Arrange
        var player = new Player(1, "testuser", false, false);
        player.SetAttribute("Key1", "Value1");
        player.SetAttribute("Key2", "Value2");

        // Act
        var attributes = player.GetAllAttributes().ToList();

        // Assert
        Assert.Equal(2, attributes.Count);
        Assert.Contains(attributes, kvp => kvp.Key == "Key1" && kvp.Value == "Value1");
        Assert.Contains(attributes, kvp => kvp.Key == "Key2" && kvp.Value == "Value2");
    }

    [Fact]
    public void Player_AdminPlayer_IsMarkedAsAdmin()
    {
        // Arrange & Act
        var admin = new Player(1, "admin", true, false);

        // Assert
        Assert.True(admin.IsAdmin);
        Assert.False(admin.IsGuest);
    }

    [Fact]
    public void Player_GuestPlayer_IsMarkedAsGuest()
    {
        // Arrange & Act
        var guest = new Player(-1, "Guest1", false, true);

        // Assert
        Assert.True(guest.IsGuest);
        Assert.False(guest.IsAdmin);
    }
}
