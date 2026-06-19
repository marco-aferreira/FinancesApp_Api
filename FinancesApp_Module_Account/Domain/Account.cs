using FinancesApp_CQRS.Interfaces;
using FinancesApp_Module_Account.Domain.Events;
using FinancesApp_Module_Account.Domain.ValueObjects;

namespace FinancesApp_Module_Account.Domain;

public enum AccountType { Cash, Checking, CreditCard }
public enum AccountStatus { Active, Closed }
public enum OperationType { MoneyTransaction, Payment, CreditPurchase }
public enum TransactionType { Withdraw, Deposit }
public sealed class Account : AggregateRoot
{
    public Guid Id { get; private set; }
    public Guid UserId{ get; private set; }    
    public Money Balance{ get; private set; }
    public Money CreditLimit { get; private set; }
    public Money CurrentDebt { get; private set; }   
    public DateTimeOffset? PaymentDate { get; private set; }
    public DateTimeOffset? DueDate { get; private set; }
    public DateTimeOffset? PayedAt { get; private set; }
    public AccountStatus Status { get; private set; }
    public AccountType Type{ get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Account(Guid id,
                   Guid userId,
                   Money balance,
                   AccountType type)
    {
        Raise(new UpdatedAccountEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, id, userId, balance, new Money(0m, balance.Currency), type));
        CalculateCreditLimit(Balance);
    }


    public Account(Guid userId,
                   Money balance,
                   AccountType type)
    {
        Raise(new AccountCreatedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), userId, balance, new Money(0m, balance.Currency), type));
        CalculateCreditLimit(Balance);
    }


    public Account(Guid accountId)
    {
        Id = accountId;  
    }
   
    public Account()
    {
        
    }

    public Account(Guid id, 
                   Guid userId, 
                   Money balance, 
                   Money creditLimit, 
                   Money currentDebt,
                   AccountStatus status, 
                   AccountType type,
                   DateTimeOffset? dueDate,
                   DateTimeOffset createdAt, 
                   DateTimeOffset? closedAt)
    {
        Id = id;
        UserId = userId;
        Balance = balance;
        CreditLimit = creditLimit;
        CurrentDebt = currentDebt;
        Status = status;
        Type = type;
        DueDate = dueDate;
        CreatedAt = createdAt;
        ClosedAt = closedAt;
    }

    public Account(Guid id,
                Guid userId,
                Money balance,
                Money creditLimit,
                Money currentDebt,
                AccountStatus status,
                AccountType type,
                DateTimeOffset? paymentDate,
                DateTimeOffset? dueDate,
                DateTimeOffset createdAt,
                DateTimeOffset? closedAt)
    {
        Id = id;
        UserId = userId;
        Balance = balance;
        CreditLimit = creditLimit;
        CurrentDebt = currentDebt;
        Status = status;
        Type = type;
        PaymentDate = paymentDate;
        DueDate = dueDate;
        CreatedAt = createdAt;
        ClosedAt = closedAt;
    }

    public void ApplyDelta(Money delta,
                           OperationType opType = OperationType.MoneyTransaction,
                           TransactionType transactionType = TransactionType.Withdraw)
    {
        var error = ValidateApplyDelta(delta, opType, transactionType);
        if (error is not null)
        {
            Raise(new ApplyDeltaErrorEvent(Guid.NewGuid(),
                                           DateTimeOffset.UtcNow,
                                           Id,
                                           UserId,
                                           error,
                                           delta,
                                           opType,
                                           transactionType));
            return;
        }

        if (Type == AccountType.CreditCard || opType == OperationType.CreditPurchase)
        {
            UpdateCredit(delta, opType);
            return;
        }

        if (transactionType == TransactionType.Withdraw)
            Raise(new WithdrawEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, Id, UserId, delta, DateTimeOffset.UtcNow));
        else
            Raise(new DepositEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, Id, UserId, delta, DateTimeOffset.UtcNow));
    }

    public void PayCreditCardDebt(Money amount)
    {
        EnsureActive();
        EnsureCurrency(amount.Currency);

        if (Type != AccountType.CreditCard)
            throw new InvalidOperationException("Only credit card accounts can process credit payments.");

        Raise(new CredidCardStatementPaymentEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, Id, UserId, amount));
    }

    public void Close()
    {
        EnsureActive();

        if (!Balance.IsZero)
            throw new InvalidOperationException("Account must have zero balance before closing.");

        Raise(new AccountClosedEvent(Guid.NewGuid(), DateTimeOffset.UtcNow, Id, UserId));
    }
    public override void RebuildFromEvents(List<IDomainEvent> events)
    {
        ClearUncommittedEvents();
        foreach (var evt in events)
            Apply(evt);
        SetAggregateVersions(events.Count);
    }
    private void CalculateCreditLimit(Money balance)
    {

        if(Type == AccountType.CreditCard)
        {
            Raise(new CalculatedCreditLimitEvent(Guid.NewGuid(),
                                                 DateTimeOffset.UtcNow,
                                                 Id,
                                                 UserId,
                                                 new Money(4500m, balance.Currency)));          
            return;
        }

        if (balance.Amount <= 350)
            Raise(new CalculatedCreditLimitEvent(Guid.NewGuid(),
                                                 DateTimeOffset.UtcNow,
                                                 Id,
                                                 UserId,
                                                 new Money(500m, balance.Currency)));
        else
            Raise(new CalculatedCreditLimitEvent(Guid.NewGuid(),
                                                 DateTimeOffset.UtcNow,
                                                 Id,
                                                 UserId,
                                                 new Money(decimal.Ceiling(balance.Amount * 2), balance.Currency)));
    }

    private void ValidateCreditCardDueDate()
    {
        if (!DueDate.HasValue)
            throw new InvalidOperationException("Due date is not set for this credit card account.");
    }

    private void RecalculateDebt(DateTimeOffset debtStarted)
    {
        var daysPastDue = (debtStarted - DueDate!.Value).Days;
        var interestRate = 0.05m;
        var interest = CurrentDebt.Amount * interestRate * daysPastDue;

        var newDebt = CurrentDebt.Add(new Money(interest, CurrentDebt.Currency));

        CurrentDebt = new Money(newDebt.Amount, newDebt.Currency);
    }

    protected override void Apply(IDomainEvent evt)
    {
        switch (evt)
        {
            case AccountCreatedEvent e:

                Id = e.Id;
                UserId = e.UserId;
                Type = e.Type;
                CurrentDebt = e.Debt;
                CreatedAt = e.Timestamp;

                if (ValidateInitialBalance(e.Balance))
                    Balance = e.Balance;
                break;

            case UpdatedAccountEvent e:

                Id = e.Id;
                UserId = e.userId;
                Type = e.type;
                CurrentDebt = e.debt;
                UpdatedAt = e.Timestamp;

                if (ValidateInitialBalance(e.balance))
                    Balance = e.balance;

                break;

            case DepositEvent e:

                Balance = Balance.Add(e.Amount);
                break;

            case WithdrawEvent e:

                Balance = Balance.Subtract(e.Amount);
                break;

            case ApplyDeltaErrorEvent:

                break;

            case CalculatedCreditLimitEvent e:

                CreditLimit = new Money(e.Value.Amount, e.Value.Currency);
                SetCreditCardPaymentDates(e.Timestamp);
                break;

            case CreditUpdatedEvent e:

                CurrentDebt = new Money(e.NewDebt.Amount, e.NewDebt.Currency);
                break;

            case AccountClosedEvent e:

                Status = AccountStatus.Closed;
                ClosedAt = e.Timestamp;
                break;
            
            case DebtRecalculatedEvent e:

                RecalculateDebt(e.Timestamp);
                break;

            case CredidCardStatementPaymentEvent e:

                ValidateCreditCardDueDate();

                if (e.Timestamp > DueDate!.Value)
                    RecalculateDebt(e.Timestamp);

                UpdateCredit(e.Amount, OperationType.Payment, raiseEvent: false);

                if (CurrentDebt.IsZero)
                    PayedAt = e.Timestamp;

                SetCreditCardPaymentDates(e.Timestamp, payedAt: e.Timestamp);

                break;

            default:
                throw new NotImplementedException(string.Format("No event handler for selected operation {0}", evt.GetType().Name));
        }
    }

    private void SetCreditCardPaymentDates(DateTimeOffset reference, DateTimeOffset? payedAt = default)
    {
        if (Type != AccountType.CreditCard)
            return;

        if (payedAt.HasValue)
            PaymentDate = payedAt.Value;

        DueDate = new DateTimeOffset(reference.Month == 12 ? reference.Year + 1 : reference.Year,
                                     reference.Month == 12 ? 1 : reference.Month + 1,
                                     10, 23, 59, 59, reference.Offset);
    }

    private void UpdateCredit(Money delta, 
                              OperationType opType,
                              bool raiseEvent = true)
    {
        Money newDebt;

        if (opType == OperationType.Payment)
            newDebt = CurrentDebt.Subtract(delta.Amount < 0 ? delta.Negate() : delta);
        else
            newDebt = CurrentDebt.Add(delta.Amount < 0 ? delta.Negate() : delta);

        var debt = newDebt.Amount < 0 ? new Money(0m, delta.Currency) : newDebt;
        
        if(raiseEvent)
            Raise(new CreditUpdatedEvent(Guid.NewGuid(),
                                         DateTimeOffset.UtcNow,
                                         Id,
                                         UserId,
                                         NewDebt: debt,
                                         CurrentDebt: CurrentDebt));
        else
            CurrentDebt = debt;
    }
  
    private void EnsureCurrency(string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required.");

        bool ok = Type switch
        {
            AccountType.Cash or AccountType.Checking
                => string.Equals(Balance.Currency, currency, StringComparison.OrdinalIgnoreCase),

            AccountType.CreditCard
                => string.Equals(CreditLimit.Currency, currency, StringComparison.OrdinalIgnoreCase),

            _ => throw new ArgumentOutOfRangeException("Unknown account type.")
        };

        if (!ok)
            throw new InvalidOperationException("Currency mismatch.");
    }

    private void EnsureActive()
    {
        if(Status == AccountStatus.Closed)
            throw new InvalidOperationException("Account is closed.");
    }

    private bool ValidateInitialBalance(Money balance)
    {
        if (balance.Amount < 0 && Type != AccountType.CreditCard)
            throw new InvalidOperationException("Initial balance cannot be negative for non credit-card accounts.");
        return true;
    }

    private string? ValidateApplyDelta(Money delta, OperationType opType, TransactionType transactionType)
    {
        if (Status == AccountStatus.Closed)
            return "Account is closed.";

        var accountCurrency = Type switch
        {
            AccountType.Cash or AccountType.Checking => Balance.Currency,
            AccountType.CreditCard => CreditLimit.Currency,
            _ => null
        };

        if (accountCurrency is null)
            return "Unknown account type.";

        if (!string.Equals(accountCurrency, delta.Currency, StringComparison.OrdinalIgnoreCase))
            return "Currency mismatch.";

        if (Type == AccountType.CreditCard && opType != OperationType.CreditPurchase && opType != OperationType.Payment)
            return "Credit card accounts only support credit card operations.";

        var abs = Math.Abs(delta.Amount);

        if (Type == AccountType.CreditCard || opType == OperationType.CreditPurchase)
        {
            var newDebt = opType == OperationType.Payment
                ? CurrentDebt.Amount - abs
                : CurrentDebt.Amount + abs;

            if (newDebt > CreditLimit.Amount)
                return "Credit limit exceeded.";
        }

        if (Type == AccountType.Cash
            && opType != OperationType.CreditPurchase
            && transactionType == TransactionType.Withdraw
            && Balance.Amount - abs < 0)
        {
            return "Insufficient funds in cash account.";
        }

        return null;
    }
}
