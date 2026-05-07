using FinancesApp_CQRS.Interfaces;
using System.Text.Json.Serialization;

namespace FinancesApp_Module_Credentials.Domain;
public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _uncommittedEvents = [];
    [JsonIgnore]
    public int NextVersion { get; private set; }
    [JsonIgnore]
    public int CurrentVersion { get; private set; }

    protected void Raise(IDomainEvent evt)
    {
        _uncommittedEvents.Add(evt);
        Apply(evt);
        NextVersion++;
    }

    public void SetAggregateVersions(int current)
    {
        CurrentVersion = current;
        NextVersion = current;
    }

    protected abstract void Apply(IDomainEvent evt);
    public abstract void RebuildFromEvents(List<IDomainEvent> events);
    public IReadOnlyList<IDomainEvent> GetUncommittedEvents() => _uncommittedEvents;
    public void ClearUncommittedEvents() => _uncommittedEvents.Clear();
}
