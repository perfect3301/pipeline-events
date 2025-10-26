using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public class MultiMethodHandler : OrderedMonoBehaviourEventHandler<IEvent>
{
    [System.Serializable]
    public class EventSubscription
    {
        [Header("Event Configuration")]
        public string eventTypeName;

        [Header("Target Configuration")]
        public MonoBehaviour targetComponent;
        public string methodName;
    }

    [SerializeField] private List<EventSubscription> subscriptions = new List<EventSubscription>();

    private IEventPublisher eventPublisher;
    private Dictionary<Type, object> eventHandlers = new Dictionary<Type, object>();

    protected override void Start()
    {
        base.Start();

        eventPublisher = ServiceLocator.Instance.GetService<IEventPublisher>();

        foreach (var subscription in subscriptions)
        {
            SubscribeToEvent(subscription);
        }
    }

    private void SubscribeToEvent(EventSubscription subscription)
    {
        Type eventType = GetEventType(subscription.eventTypeName);
        if (eventType != null)
        {
            var handlerType = typeof(EventHandler<>).MakeGenericType(eventType);
            var handler = Activator.CreateInstance(handlerType, this, subscription);

            var subscribeMethod = typeof(IEventPublisher).GetMethod("Subscribe")?
                .MakeGenericMethod(eventType);

            subscribeMethod?.Invoke(eventPublisher, new[] { handler });

            eventHandlers[eventType] = handler;
        }
    }

    private Type GetEventType(string typeName)
    {
        return Type.GetType(typeName) ??
               Type.GetType($"{typeName}, Assembly-CSharp") ??
               AppDomain.CurrentDomain.GetAssemblies()
                   .SelectMany(a => a.GetTypes())
                   .FirstOrDefault(t => t.Name == typeName && typeof(IEvent).IsAssignableFrom(t));
    }

    private class EventHandler<TEvent> : IEventHandler<TEvent> where TEvent : IEvent
    {
        private MultiMethodHandler subscriber;
        private EventSubscription subscription;

        public EventHandler(MultiMethodHandler subscriber, EventSubscription subscription)
        {
            this.subscriber = subscriber;
            this.subscription = subscription;
        }

        public void Handle(TEvent @event)
        {
            if (subscription.targetComponent != null && !string.IsNullOrEmpty(subscription.methodName))
            {
                subscriber.InvokeMethodWithParameters(subscription, @event);
            }
        }
    }

    private void InvokeMethodWithParameters<TEvent>(EventSubscription subscription, TEvent @event) where TEvent : IEvent
    {
        var componentType = subscription.targetComponent.GetType();

        var methods = componentType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name == subscription.methodName)
            .ToList();

        if (methods.Count == 0)
        {
            return;
        }

        var suitableMethod = FindSuitableMethod(methods, @event);

        if (suitableMethod.method != null)
        {
            suitableMethod.method.Invoke(subscription.targetComponent, suitableMethod.parameters);
        }
    }

    private (MethodInfo method, object[] parameters) FindSuitableMethod<TEvent>(List<MethodInfo> methods, TEvent @event) where TEvent : IEvent
    {
        foreach (var method in methods)
        {
            var parameters = method.GetParameters();

            if (parameters.Length == 0)
            {
                return (method, null);
            }
            else if (parameters.Length == 1)
            {
                var parameterType = parameters[0].ParameterType;
                object parameterValue = null;

                if (TryGetParameterValueFromEvent(@event, parameterType, out parameterValue))
                {
                    return (method, new object[] { parameterValue });
                }
            }
        }

        return (null, null);
    }

    private bool TryGetParameterValueFromEvent<TEvent>(TEvent @event, Type parameterType, out object value) where TEvent : IEvent
    {
        value = null;
        var eventType = @event.GetType();

        if (parameterType == eventType || parameterType == typeof(IEvent))
        {
            value = @event;
            return true;
        }

        var fields = eventType.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var properties = eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            if (field.FieldType == parameterType)
            {
                value = field.GetValue(@event);
                return true;
            }
        }

        foreach (var property in properties)
        {
            if (property.PropertyType == parameterType && property.CanRead)
            {
                value = property.GetValue(@event);
                return true;
            }
        }

        return false;
    }

    public override void Handle(IEvent @event)
    {
        foreach (var subscription in subscriptions)
        {
            var eventType = GetEventType(subscription.eventTypeName);
            if (eventType == @event.GetType())
            {
                InvokeMethodWithParameters(subscription, @event);
            }
        }
    }

    protected override void OnDestroy()
    {
        if (eventPublisher != null)
        {
            foreach (var kvp in eventHandlers)
            {
                var unsubscribeMethod = typeof(IEventPublisher).GetMethod("Unsubscribe")?
                    .MakeGenericMethod(kvp.Key);

                unsubscribeMethod?.Invoke(eventPublisher, new[] { kvp.Value });
            }
        }
        base.OnDestroy();
    }
}

