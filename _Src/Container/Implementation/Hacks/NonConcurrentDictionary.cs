using System;
using System.Collections.Generic;
using System.Threading;

namespace SimpleContainer.Implementation.Hacks
{
	public class NonConcurrentDictionary<TKey, TValue>
	{
		private readonly Dictionary<TKey, TValue> impl = new Dictionary<TKey, TValue>();
		private readonly ReaderWriterLockSlim locker = new ReaderWriterLockSlim();

		public TValue GetOrAdd(TKey key, Func<TKey, TValue> creator)
		{
			if(ReferenceEquals(key, null))
				throw new ArgumentNullException("key");
			locker.EnterReadLock();
			TValue result;
			var found = impl.TryGetValue(key, out result);
			locker.ExitReadLock();
			if (found)
				return result;
			var created = creator(key);
			locker.EnterWriteLock();
			if (!impl.TryGetValue(key, out result))
			{
				impl.Add(key, created);
				result = created;
			}
			locker.ExitWriteLock();
			return result;
		}

		public bool ContainsKey(TKey key)
		{
			if (ReferenceEquals(key, null))
				throw new ArgumentNullException("key");
			locker.EnterReadLock();
			var found = impl.ContainsKey(key);
			locker.ExitReadLock();
			return found;
		}
		
		public bool TryGetValue(TKey key, out TValue value)
		{
			if (ReferenceEquals(key, null))
				throw new ArgumentNullException("key");
			locker.EnterReadLock();
			var found = impl.TryGetValue(key, out value);
			locker.ExitReadLock();
			return found;
		}
		
		public bool TryAdd(TKey key, TValue value)
		{
			if (ReferenceEquals(key, null))
				throw new ArgumentNullException("key");
			locker.EnterReadLock();
			var keyAlreadyExists = impl.ContainsKey(key);
			if (!keyAlreadyExists)
				impl.Add(key,value);
			locker.ExitReadLock();
			return !keyAlreadyExists;
		}

		public IEnumerable<TValue> Values
		{
			get
			{
				locker.EnterReadLock();
				var result = new TValue[impl.Count];
				impl.Values.CopyTo(result, 0);
				locker.ExitReadLock();
				return result;
			}
		}
	}
}