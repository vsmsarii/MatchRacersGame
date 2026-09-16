using System;
using System.Collections.Generic;

namespace CasualKit.Core
{
    public sealed class ServiceProvider
    {
        private readonly Dictionary<Type, IService> m_Services = new();
        private readonly HashSet<IService> m_InitializedServices = new();
        private readonly List<IService> m_Order = new();
        private readonly GameLoop m_GameLoop;

        public ServiceProvider(GameLoop gameLoop)
        {
            m_GameLoop = gameLoop;
        }

        public void Register<T>(T service) where T : class, IService
        {
            if (!m_InitializedServices.Contains(service))
            {
                service.Initialize();
                m_InitializedServices.Add(service);
                m_Order.Add(service);

                if (service is ITickable || service is IFixedTickable || service is ILateTickable)
                    m_GameLoop.Register(service);

                EditorLog.Info($"Registered service: {service.GetType().Name}");
            }
            else
            {
                EditorLog.Warning($"Service already registered: {service.GetType().Name}");
                return;
            }

            m_Services[typeof(T)] = service;

            Type[] interfaces = service.GetType().GetInterfaces();
            for (int index = 0; index < interfaces.Length; index++)
            {
                Type interfaceType = interfaces[index];
                if (interfaceType == typeof(IService) || !typeof(IService).IsAssignableFrom(interfaceType))
                    continue;

                m_Services[interfaceType] = service;
            }
        }

        public T Get<T>() where T : class, IService
        {
            return (T)m_Services[typeof(T)];
        }

        public bool TryGet<T>(out T service) where T : class, IService
        {
            if (m_Services.TryGetValue(typeof(T), out IService found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        public void Dispose()
        {
            for (int i = m_Order.Count - 1; i >= 0; i--)
            {
                IService service = m_Order[i];
                m_GameLoop.Unregister(service);
                service.Dispose();
            }

            m_Order.Clear();
            m_InitializedServices.Clear();
            m_Services.Clear();
        }
    }
}
