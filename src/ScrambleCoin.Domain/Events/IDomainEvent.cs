namespace ScrambleCoin.Domain.Events;

/// <summary>
/// Marker interface for all domain events raised by aggregate roots.
/// Domain events are raised (not dispatched) — the infrastructure layer is
/// responsible for reading and dispatching them after a unit of work commits.
/// </summary>
public interface IDomainEvent;
