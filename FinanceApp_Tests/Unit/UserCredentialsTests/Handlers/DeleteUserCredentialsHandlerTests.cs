using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_Credentials.Application.Commands;
using FinancesApp_Module_Credentials.Application.Commands.Handlers;
using FinancesApp_Module_Credentials.Application.Repositories;
using FinancesApp_Module_Credentials.Domain;
using FinancesApp_Module_Credentials.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinancesApp_Tests.Unit.UserCredentialsTests.Handlers;

public class DeleteUserCredentialsHandlerTests
{
    private readonly IEventStore _mockEventStore;
    private readonly IUserCredentialsReadRepository _mockReadRepository;
    private readonly ILogger<DeleteUserCredentialsHandler> _mockLogger;
    private readonly DeleteUserCredentialsHandler _handler;

    public DeleteUserCredentialsHandlerTests()
    {
        _mockEventStore = Substitute.For<IEventStore>();
        _mockReadRepository = Substitute.For<IUserCredentialsReadRepository>();
        _mockLogger = Substitute.For<ILogger<DeleteUserCredentialsHandler>>();
        _handler = new DeleteUserCredentialsHandler(_mockEventStore, _mockReadRepository, _mockLogger);
    }

    private void SetupCredentials(Guid userId, Guid credentialsId)
    {
        _mockReadRepository.GetByUserIdAsync(userId, token: Arg.Any<CancellationToken>())
            .Returns(new UserCredentials(credentialsId, userId, "john_doe", string.Empty));

        var events = new List<IDomainEvent>
        {
            new CredentialsRegisteredEvent(Guid.NewGuid(), DateTimeOffset.UtcNow,
                credentialsId, userId, "john_doe", "$2a$11$hashedpassword")
        };

        _mockEventStore.Load(credentialsId, 0, Arg.Any<CancellationToken>())
            .Returns(events);
    }

    [Fact]
    public async Task Should_Return_True_When_Credentials_Deleted_Successfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var credentialsId = Guid.NewGuid();
        SetupCredentials(userId, credentialsId);

        var command = new DeleteUserCredentials(userId);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.Should().BeTrue();
        await _mockEventStore.Received(1).Append(
            credentialsId,
            Arg.Any<IReadOnlyList<IDomainEvent>>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_False_When_Credentials_Not_Found()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockReadRepository.GetByUserIdAsync(userId, token: Arg.Any<CancellationToken>())
            .Returns(new UserCredentials());

        var command = new DeleteUserCredentials(userId);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.Should().BeFalse();
        await _mockEventStore.DidNotReceive().Append(
            Arg.Any<Guid>(),
            Arg.Any<IReadOnlyList<IDomainEvent>>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_False_When_Exception_Occurs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var credentialsId = Guid.NewGuid();

        _mockReadRepository.GetByUserIdAsync(userId, token: Arg.Any<CancellationToken>())
            .Returns(new UserCredentials(credentialsId, userId, "john_doe", string.Empty));

        _mockEventStore.Load(credentialsId, 0, Arg.Any<CancellationToken>())
            .Throws(new Exception("Database connection failed"));

        var command = new DeleteUserCredentials(userId);

        // Act
        var result = await _handler.Handle(command);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Pass_CancellationToken_To_EventStore()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var credentialsId = Guid.NewGuid();
        var cancellationToken = new CancellationToken();
        SetupCredentials(userId, credentialsId);

        var command = new DeleteUserCredentials(userId);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        await _mockEventStore.Received(1).Append(
            credentialsId,
            Arg.Any<IReadOnlyList<IDomainEvent>>(),
            Arg.Any<int>(),
            cancellationToken);
    }
}
