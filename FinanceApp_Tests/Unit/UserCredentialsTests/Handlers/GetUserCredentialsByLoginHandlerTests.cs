using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_Credentials.Application.Queries;
using FinancesApp_Module_Credentials.Application.Queries.Handlers;
using FinancesApp_Module_Credentials.Application.Repositories;
using FinancesApp_Module_Credentials.Domain;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinancesApp_Tests.Unit.UserCredentialsTests.Handlers;

public class GetUserCredentialsByLoginHandlerTests
{
    private readonly IUserCredentialsReadRepository _mockRepository;
    private readonly IEventStore _mockEventStore;
    private readonly ILogger<GetUserCredentialsByLoginHandler> _mockLogger;
    private readonly GetUserCredentialsByLoginHandler _handler;

    public GetUserCredentialsByLoginHandlerTests()
    {
        _mockRepository = Substitute.For<IUserCredentialsReadRepository>();
        _mockEventStore = Substitute.For<IEventStore>();
        _mockLogger = Substitute.For<ILogger<GetUserCredentialsByLoginHandler>>();
        _handler = new GetUserCredentialsByLoginHandler(_mockRepository, _mockEventStore, _mockLogger);
    }

    /// <summary>
    /// Builds a real event-sourced aggregate (so the stream carries a genuine bcrypt hash),
    /// wires the read repo + event store mocks, and returns the read-model view.
    /// </summary>
    private UserCredentials SetupCredentials(string login, string plainPassword)
    {
        var aggregate = new UserCredentials(Guid.NewGuid(), login, plainPassword);
        var readModel = new UserCredentials(aggregate.Id, aggregate.UserId, login, string.Empty);

        _mockRepository.GetByLoginAsync(login, token: Arg.Any<CancellationToken>())
            .Returns(readModel);

        _mockEventStore.Load(aggregate.Id, 0, Arg.Any<CancellationToken>())
            .Returns(aggregate.GetUncommittedEvents().ToList());

        return readModel;
    }

    [Fact]
    public async Task Should_Return_Credentials_When_Found()
    {
        // Arrange
        var login = "john_doe";
        var expected = new UserCredentials(Guid.NewGuid(), Guid.NewGuid(), login, string.Empty);

        _mockRepository.GetByLoginAsync(login, token: Arg.Any<CancellationToken>())
            .Returns(expected);

        var query = new GetUserCredentialsByLogin { Login = login };

        // Act
        var result = await _handler.Handle(query);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(expected.Id);
        result.UserId.Should().Be(expected.UserId);
        result.Email.Should().Be(login);
        result.Password.Should().BeEmpty();
        await _mockRepository.Received(1).GetByLoginAsync(login, token: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Not_Touch_EventStore_When_No_Password_Supplied()
    {
        // Arrange
        var login = "john_doe";

        _mockRepository.GetByLoginAsync(login, token: Arg.Any<CancellationToken>())
            .Returns(new UserCredentials(Guid.NewGuid(), Guid.NewGuid(), login, string.Empty));

        var query = new GetUserCredentialsByLogin { Login = login };

        // Act
        await _handler.Handle(query);

        // Assert
        await _mockEventStore.DidNotReceive().Load(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_Credentials_When_Password_Correct()
    {
        // Arrange
        var login = "john_doe";
        var expected = SetupCredentials(login, "SecurePass1!");

        var query = new GetUserCredentialsByLogin { Login = login, Password = "SecurePass1!" };

        // Act
        var result = await _handler.Handle(query);

        // Assert
        result.Id.Should().Be(expected.Id);
        result.Password.Should().BeEmpty();
    }

    [Fact]
    public async Task Should_Return_Empty_Credentials_When_Password_Wrong()
    {
        // Arrange
        var login = "john_doe";
        SetupCredentials(login, "SecurePass1!");

        var query = new GetUserCredentialsByLogin { Login = login, Password = "WrongPassword1!" };

        // Act
        var result = await _handler.Handle(query);

        // Assert
        result.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Should_Return_Empty_Credentials_When_Aggregate_Deleted()
    {
        // Arrange
        var login = "john_doe";
        var aggregate = new UserCredentials(Guid.NewGuid(), login, "SecurePass1!");
        aggregate.Delete();

        _mockRepository.GetByLoginAsync(login, token: Arg.Any<CancellationToken>())
            .Returns(new UserCredentials(aggregate.Id, aggregate.UserId, login, string.Empty));

        _mockEventStore.Load(aggregate.Id, 0, Arg.Any<CancellationToken>())
            .Returns(aggregate.GetUncommittedEvents().ToList());

        var query = new GetUserCredentialsByLogin { Login = login, Password = "SecurePass1!" };

        // Act
        var result = await _handler.Handle(query);

        // Assert
        result.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Should_Return_Empty_Credentials_When_No_Events_Found()
    {
        // Arrange
        var login = "john_doe";

        _mockRepository.GetByLoginAsync(login, token: Arg.Any<CancellationToken>())
            .Returns(new UserCredentials(Guid.NewGuid(), Guid.NewGuid(), login, string.Empty));

        _mockEventStore.Load(Arg.Any<Guid>(), 0, Arg.Any<CancellationToken>())
            .Returns([]);

        var query = new GetUserCredentialsByLogin { Login = login, Password = "SecurePass1!" };

        // Act
        var result = await _handler.Handle(query);

        // Assert
        result.Id.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Should_Return_Empty_Credentials_When_Not_Found()
    {
        // Arrange
        var login = "unknown_user";

        _mockRepository.GetByLoginAsync(login, token: Arg.Any<CancellationToken>())
            .Returns(new UserCredentials());

        var query = new GetUserCredentialsByLogin { Login = login };

        // Act
        var result = await _handler.Handle(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new UserCredentials());
    }

    [Fact]
    public async Task Should_Return_Empty_Credentials_When_Exception_Occurs()
    {
        // Arrange
        var login = "john_doe";

        _mockRepository.GetByLoginAsync(login, token: Arg.Any<CancellationToken>())
            .Throws(new Exception("Database connection failed"));

        var query = new GetUserCredentialsByLogin { Login = login };

        // Act
        var result = await _handler.Handle(query);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(new UserCredentials());
    }

    [Fact]
    public async Task Should_Pass_CancellationToken_To_Repository()
    {
        // Arrange
        var login = "john_doe";
        var cancellationToken = new CancellationToken();

        _mockRepository.GetByLoginAsync(login, token: cancellationToken)
            .Returns(new UserCredentials());

        var query = new GetUserCredentialsByLogin { Login = login };

        // Act
        await _handler.Handle(query, cancellationToken);

        // Assert
        await _mockRepository.Received(1).GetByLoginAsync(login, token: cancellationToken);
    }
}
