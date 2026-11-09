using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleHorizons.Core
{
    /// <summary>
    /// Minimal, allocation-free-at-steady-state pool.
    ///
    /// docs/UNISOC_BUDGET.md rule 5 is "prefer merged static meshes and instancing
    /// over hundreds of objects"; rule 6 is "no per-frame allocation". Pooling is
    /// the tool for the objects that genuinely have to appear and disappear
    /// (chunk roots, tracers, loot pickups, UI pings).
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        private readonly Func<T> _factory;
        private readonly Stack<T> _idle;
        private readonly HashSet<T> _leased = new HashSet<T>();
        private readonly Transform _parent;
        private readonly int _maxRetained;

        public ObjectPool(Func<T> factory, Transform parent = null, int prewarm = 0, int maxRetained = 256)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _parent = parent;
            _maxRetained = Mathf.Max(1, maxRetained);
            _idle = new Stack<T>(Mathf.Max(prewarm, 4));
            for (var i = 0; i < prewarm; i++)
            {
                var item = _factory();
                item.gameObject.SetActive(false);
                _idle.Push(item);
            }
        }

        public int LeasedCount => _leased.Count;
        public int IdleCount => _idle.Count;

        public T Rent()
        {
            var item = _idle.Count > 0 ? _idle.Pop() : _factory();
            if (_parent != null && item.transform.parent != _parent)
            {
                item.transform.SetParent(_parent, false);
            }

            item.gameObject.SetActive(true);
            _leased.Add(item);
            return item;
        }

        public void Return(T item)
        {
            if (item == null || !_leased.Remove(item))
            {
                return;
            }

            item.gameObject.SetActive(false);
            if (_idle.Count >= _maxRetained)
            {
                UnityEngine.Object.Destroy(item.gameObject);
                return;
            }

            _idle.Push(item);
        }

        public void ReturnAll()
        {
            if (_leased.Count == 0)
            {
                return;
            }

            var copy = new List<T>(_leased);
            for (var i = 0; i < copy.Count; i++)
            {
                Return(copy[i]);
            }
        }

        public void Dispose()
        {
            ReturnAll();
            while (_idle.Count > 0)
            {
                var item = _idle.Pop();
                if (item != null)
                {
                    UnityEngine.Object.Destroy(item.gameObject);
                }
            }
        }
    }
}
