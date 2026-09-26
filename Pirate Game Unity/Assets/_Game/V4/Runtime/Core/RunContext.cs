using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;

namespace RoguePirates.V4
{
    /// <summary>
    /// What every module sees: the clock, the event stream, the per-run config copy and contract lookup. Read-only
    /// views (M6) are added here when presentation moves.
    /// </summary>
    public sealed class RunContext
    {
        readonly Dictionary<Type, object> _contracts = new Dictionary<Type, object>();

        public RunClock Clock { get; }
        public CombatEvents Events { get; }
        /// <summary>Per-run copies of the config set; null in tests that don't need config.</summary>
        public ConfigSetRuntime Config { get; }

        public RunContext(RunClock clock, CombatEvents events, ConfigSetRuntime config = null)
        {
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Config = config;
        }

        /// <summary>Registers the implementation of a contract. Contracts are interfaces; one implementation each.</summary>
        public void Register<T>(T implementation) where T : class
        {
            if (!typeof(T).IsInterface) throw new ArgumentException($"{typeof(T).Name} is not an interface contract.");
            if (implementation == null) throw new ArgumentNullException(nameof(implementation));
            if (_contracts.ContainsKey(typeof(T))) throw new InvalidOperationException($"{typeof(T).Name} is already registered.");
            _contracts[typeof(T)] = implementation;
        }

        public void Replace<T>(T implementation) where T : class
        {
            if (!typeof(T).IsInterface) throw new ArgumentException($"{typeof(T).Name} is not an interface contract.");
            _contracts[typeof(T)] = implementation ?? throw new ArgumentNullException(nameof(implementation));
        }

        public bool TryGet<T>(out T implementation) where T : class
        {
            if (_contracts.TryGetValue(typeof(T), out var value)) { implementation = (T)value; return true; }
            implementation = null;
            return false;
        }

        public T Get<T>() where T : class
        {
            if (TryGet<T>(out var implementation)) return implementation;
            throw new InvalidOperationException($"No module provides {typeof(T).Name}.");
        }
    }

    /// <summary>Owns the module list and runs lifecycle calls in TickOrder (ties broken by registration order).</summary>
    public sealed class ModuleRegistry
    {
        readonly List<IRunModule> _modules = new List<IRunModule>();
        readonly List<IRunModule> _ordered = new List<IRunModule>();
        bool _dirty;
        bool _iterating;

        public IReadOnlyList<IRunModule> Modules
        {
            get { EnsureOrder(); return _ordered; }
        }

        public void Add(IRunModule module)
        {
            if (module == null) throw new ArgumentNullException(nameof(module));
            if (_iterating) throw new InvalidOperationException($"Module '{module.Id}' cannot be registered while modules are being called.");
            foreach (var existing in _modules)
                if (existing.Id == module.Id) throw new InvalidOperationException($"Module id '{module.Id}' is already registered.");
            _modules.Add(module);
            _dirty = true;
        }

        public void InitializeAll(RunContext context) { _iterating = true; try { foreach (var m in Modules) m.Initialize(context); } finally { _iterating = false; } }
        public void BeginRunAll(in RunStart start) { _iterating = true; try { foreach (var m in Modules) m.BeginRun(start); } finally { _iterating = false; } }
        public void TickAll(in SimTick tick) { _iterating = true; try { foreach (var m in Modules) m.Tick(tick); } finally { _iterating = false; } }
        public void EndRunAll(in RunEnd end) { _iterating = true; try { foreach (var m in Modules) m.EndRun(end); } finally { _iterating = false; } }

        void EnsureOrder()
        {
            if (!_dirty) return;
            _dirty = false;
            _ordered.Clear();
            _ordered.AddRange(_modules);
            // Stable sort by TickOrder so equal orders keep registration order.
            for (var i = 1; i < _ordered.Count; i++)
            {
                var item = _ordered[i];
                var j = i - 1;
                while (j >= 0 && _ordered[j].TickOrder > item.TickOrder)
                {
                    _ordered[j + 1] = _ordered[j];
                    j--;
                }
                _ordered[j + 1] = item;
            }
        }
    }
}
