using System;
using System.Collections.Generic;

namespace VideoCreatorWPF.Core
{
    public class EventBus
    {
        private static readonly Lazy<EventBus> _instance = new(() => new EventBus());
        public static EventBus Instance => _instance.Value;

        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (!_handlers.ContainsKey(typeof(TEvent)))
            {
                _handlers[typeof(TEvent)] = new List<Delegate>();
            }
            _handlers[typeof(TEvent)].Add(handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        {
            if (_handlers.ContainsKey(typeof(TEvent)))
            {
                _handlers[typeof(TEvent)].Remove(handler);
                if (_handlers[typeof(TEvent)].Count == 0)
                {
                    _handlers.Remove(typeof(TEvent));
                }
            }
        }

        public void Publish<TEvent>(TEvent evt) where TEvent : class
        {
            if (_handlers.ContainsKey(typeof(TEvent)))
            {
                foreach (var handler in _handlers[typeof(TEvent)])
                {
                    if (handler is Action<TEvent> typedHandler)
                    {
                        typedHandler.Invoke(evt);
                    }
                }
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }
}
