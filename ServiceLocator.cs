using System.Collections.Generic;
using UnityEngine;

public class ServiceLocator : MonoBehaviour
{
    public static ServiceLocator Instance { get; private set; }

    private readonly Dictionary<System.Type, object> _services = new();
    private bool _isInitialized = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeServices();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeServices()
    {
        var eventAggregator = new EventAggregator();
        RegisterService<IEventPublisher>(eventAggregator);

        _isInitialized = true;
    }

    public void RegisterService<T>(T service)
    {
        _services[typeof(T)] = service;
    }

    public T GetService<T>()
    {
        if (Instance == null)
        {
            throw new System.Exception("ServiceLocator instance is null!");
        }

        if (!_isInitialized)
        {
            throw new System.Exception("ServiceLocator not initialized yet!");
        }

        if (_services.TryGetValue(typeof(T), out var service))
        {
            return (T)service;
        }

        throw new System.Exception($"Service {typeof(T)} not registered");
    }

    public bool TryGetService<T>(out T service)
    {
        service = default;

        if (Instance == null || !_isInitialized)
            return false;

        if (_services.TryGetValue(typeof(T), out var objService))
        {
            service = (T)objService;
            return true;
        }

        return false;
    }

}
