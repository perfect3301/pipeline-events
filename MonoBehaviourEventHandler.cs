using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class MonoBehaviourEventHandler<TEvent> : MonoBehaviour, IOrderedEventHandler<TEvent>
    where TEvent : IEvent
{
    [SerializeField] protected int _order = 0;
    [SerializeField] private bool autoSubscribe = true;

    public virtual int Order => _order;

    protected IEventPublisher EventPublisher { get; private set; }

    protected virtual void Start()
    {
        EventPublisher = ServiceLocator.Instance.GetService<IEventPublisher>();

        if (autoSubscribe)
        {
            Subscribe();
        }
    }

    protected virtual void OnDestroy()
    {
        Unsubscribe();
    }

    public void Subscribe()
    {
        EventPublisher?.Subscribe<TEvent>(this);
    }

    public void Unsubscribe()
    {
        EventPublisher?.Unsubscribe<TEvent>(this);
    }

    public abstract void Handle(TEvent @event);
}

public interface IOrderedEventHandler<TEvent> : IEventHandler<TEvent>
    where TEvent : IEvent
{
    int Order { get; }
}

public interface IEventPublisher
{
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent;
    void Publish<TEvent>(TEvent @event) where TEvent : IEvent;
}

public interface IEvent {}

public struct LevelStartEvent : IEvent
{
    public int DifficultyLevel;
}

public struct NullVauleEvent: IEvent { }
public struct NullPositionsEvent : IEvent { }
public struct NulledVelocityEvent : IEvent { }
public struct NulledDataEvent : IEvent { }
public struct StarClearEvent: IEvent { }

public struct PerfectLendingEvent : IEvent { }
public struct GoodLendingEvent : IEvent { }

public interface IEventHandler<TEvent> where TEvent : IEvent
{
    void Handle(TEvent @event);
}


// Файл: Core/Events/EventAggregator.cs
public class EventAggregator : IEventPublisher
{
    private readonly Dictionary<System.Type, List<object>> _handlers = new();

    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        if (!_handlers.ContainsKey(eventType))
            _handlers[eventType] = new List<object>();

        // Удаляем если уже есть (чтобы избежать дублирования)
        _handlers[eventType].Remove(handler);

        // Добавляем и сортируем по порядку
        _handlers[eventType].Add(handler);
        SortHandlers<TEvent>();
    }

    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        if (_handlers.ContainsKey(eventType))
        {
            _handlers[eventType].Remove(handler);
        }
    }

    public void Publish<TEvent>(TEvent @event) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        if (_handlers.ContainsKey(eventType))
        {
            // Создаем копию для безопасной итерации
            var handlers = _handlers[eventType].ToArray();

            foreach (var handler in handlers)
            {
                ((IEventHandler<TEvent>)handler).Handle(@event);
            }
        }
    }

    private void SortHandlers<TEvent>() where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        if (_handlers.ContainsKey(eventType))
        {
            _handlers[eventType] = _handlers[eventType]
                .OrderBy(handler =>
                {
                    var orderedHandler = handler as IOrderedEventHandler<TEvent>;
                    return orderedHandler?.Order ?? int.MaxValue;
                })
                .ToList();
        }
    }
}

public abstract class OrderedMonoBehaviourEventHandler<TEvent> : MonoBehaviour, IOrderedEventHandler<TEvent>
    where TEvent : IEvent
{
    [SerializeField] protected int order = 0;
    [SerializeField] private bool autoSubscribe = true;

    public virtual int Order => order;

    protected IEventPublisher EventPublisher { get; private set; }

    protected virtual void Start()
    {
        if (autoSubscribe)
        {
            StartCoroutine(DelayedSubscribe());
        }
    }

    private IEnumerator DelayedSubscribe()
    {
        yield return new WaitUntil(() => ServiceLocator.Instance != null);

        yield return new WaitForEndOfFrame();

        EventPublisher = ServiceLocator.Instance.GetService<IEventPublisher>();
        if (EventPublisher != null)
        {
            EventPublisher.Subscribe<TEvent>(this);
        }
    }

    protected virtual void OnDestroy()
    {
        if (EventPublisher != null)
        {
            EventPublisher.Unsubscribe<TEvent>(this);
        }
    }

    public abstract void Handle(TEvent @event);
}