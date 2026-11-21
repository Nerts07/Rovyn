using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rovyn.UI
{
    // Reuses list item instances instead of destroying and recreating them on every refresh.
    // The container should hold only items of this list.
    public sealed class ItemList<T> where T : Component
    {
        readonly T _prefab;
        readonly Transform _container;
        readonly List<T> _items = new();

        public ItemList(T prefab, Transform container)
        {
            _prefab = prefab;
            _container = container;
        }

        public IReadOnlyList<T> Items => _items;

        public void Bind<TData>(IReadOnlyList<TData> data, Action<T, TData> bind)
        {
            for (int i = 0; i < data.Count; i++)
            {
                if (i == _items.Count)
                {
                    var instance = UnityEngine.Object.Instantiate(_prefab, _container, false);
                    UiPresentation.ApplyPalette(instance.gameObject);
                    _items.Add(instance);
                }
                var item = _items[i];
                if (!item.gameObject.activeSelf) item.gameObject.SetActive(true);
                bind(item, data[i]);
            }
            for (int i = data.Count; i < _items.Count; i++)
            {
                if (_items[i].gameObject.activeSelf) _items[i].gameObject.SetActive(false);
            }
        }
    }
}
