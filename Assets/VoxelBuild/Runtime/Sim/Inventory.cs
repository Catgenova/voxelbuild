using System.Collections.Generic;

namespace VoxelBuild.Sim
{
    using VoxelBuild.Core;

    /// <summary>What a colonist carries. Capacity is a total item count regardless of type.</summary>
    public sealed class Inventory
    {
        private readonly Dictionary<ItemType, int> items = new Dictionary<ItemType, int>();
        public int Capacity;

        public Inventory(int capacity)
        {
            Capacity = capacity;
        }

        public int Total
        {
            get
            {
                int n = 0;
                foreach (var kv in items) n += kv.Value;
                return n;
            }
        }

        public int FreeSpace => Capacity - Total;
        public bool IsFull => FreeSpace <= 0;
        public bool IsEmpty => Total == 0;

        public int Count(ItemType type) => items.TryGetValue(type, out var n) ? n : 0;
        public bool Has(ItemType type, int count) => Count(type) >= count;

        /// <summary>Adds up to <paramref name="count"/> items. Returns how many were actually added.</summary>
        public int Add(ItemType type, int count)
        {
            if (type == ItemType.None || count <= 0) return 0;
            int add = System.Math.Min(count, FreeSpace);
            if (add <= 0) return 0;
            items[type] = Count(type) + add;
            return add;
        }

        /// <summary>Removes up to <paramref name="count"/> items. Returns how many were removed.</summary>
        public int Remove(ItemType type, int count)
        {
            int have = Count(type);
            int take = System.Math.Min(have, count);
            if (take <= 0) return 0;
            if (have - take == 0) items.Remove(type);
            else items[type] = have - take;
            return take;
        }

        public IEnumerable<ItemStack> Stacks()
        {
            foreach (var kv in items)
                if (kv.Value > 0) yield return new ItemStack(kv.Key, kv.Value);
        }

        public List<ItemStack> ToList()
        {
            var l = new List<ItemStack>();
            foreach (var s in Stacks()) l.Add(s);
            return l;
        }

        public bool HasAnyEdible()
        {
            foreach (var kv in items)
                if (kv.Value > 0 && ItemRegistry.IsEdible(kv.Key)) return true;
            return false;
        }

        public ItemType FirstEdible()
        {
            foreach (var kv in items)
                if (kv.Value > 0 && ItemRegistry.IsEdible(kv.Key)) return kv.Key;
            return ItemType.None;
        }

        public void Clear() => items.Clear();
    }
}
