using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_Credentials.Application.Commands;
using FinancesApp_Module_Credentials.Application.Commands.Handlers;
using FinancesApp_Module_Credentials.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinancesApp_Tests.Unit.UserCredentialsTests.Handlers;

public class RegisterUserCredentialsHandlerTests
{
    private readonly IEventStore _mockEventStore;
    private readonly ILogger<RegisterUserCredentialsHandler> _mockLogger;
    private readonly RegisterUserCredentialsHandler _handler;

    public RegisterUserCredentialsHandlerTests()
    {
        _mockEventStore = Substitute.For<IEventStore>();
        _mockLogger = Substitute.For<ILogger<RegisterUserCredentialsHandler>>();
        _handler = new RegisterUserCredentialsHandler(_mockEventStore, _mockLogger);
    }

    [Fact]
    public async Task Should_Return_Inserted_Id_When_Successful()
    {
        // Arrange
        var credentials = new UserCredentials(Guid.NewGuid(), "john_doe", "SecurePass1!");

        var command = new RegisterUserCredentials(credentials);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.Should().NotBe(Guid.Empty);
        await _mockEventStore.Received(1).Append(
            credentials.Id,
            Arg.Any<IReadOnlyList<IDomainEvent>>(),
            credentials.CurrentVersion,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_Guid_Empty_When_Exception_Occurs()
    {
        // Arrange
        var credentials = new UserCredentials(Guid.NewGuid(), "john_doe", "SecurePass1!");

        _mockEventStore.Append(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<IDomainEvent>>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Throws(new Exception("Database connection failed"));

        var command = new RegisterUserCredentials(credentials);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Should_Pass_CancellationToken_To_EventStore()
    {
        // Arrange
        var credentials = new UserCredentials(Guid.NewGuid(), "john_doe", "SecurePass1!");
        var cancellationToken = new CancellationToken();

        var command = new RegisterUserCredentials(credentials);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        await _mockEventStore.Received(1).Append(
            credentials.Id,
            Arg.Any<IReadOnlyList<IDomainEvent>>(),
            credentials.CurrentVersion,
            cancellationToken);
    }
}
