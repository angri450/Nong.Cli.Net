// HarfBuzzSharp DelegateProxies — rewritten for merged ThirdParty assembly
// Originally was a partial class sharing code with SkiaSharp via Binding.Shared
#nullable disable

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HarfBuzzSharp
{
	public delegate void ReleaseDelegate ();
	public delegate Blob GetTableDelegate (Face face, Tag tag);

	// helper delegates (was in shared file)
	internal delegate Delegate GetMultiDelegateDelegate (Type index);
	internal delegate object UserDataDelegate ();

	internal static unsafe partial class DelegateProxies
	{
		// === Shared helper methods (was in Binding.Shared/DelegateProxies.shared.cs) ===

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void Create (object managedDel, out GCHandle gch, out IntPtr contextPtr)
		{
			if (managedDel == null) { gch = default; contextPtr = IntPtr.Zero; return; }
			gch = GCHandle.Alloc (managedDel);
			contextPtr = GCHandle.ToIntPtr (gch);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static T Get<T> (IntPtr contextPtr, out GCHandle gch)
		{
			if (contextPtr == IntPtr.Zero) { gch = default; return default; }
			gch = GCHandle.FromIntPtr (contextPtr);
			return (T)gch.Target;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateUserData (object userData, bool makeWeak = false)
		{
			userData = makeWeak ? new WeakReference (userData) : userData;
			Create (new UserDataDelegate (() => userData), out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static T GetUserData<T> (IntPtr contextPtr, out GCHandle gch)
		{
			var del = Get<UserDataDelegate> (contextPtr, out gch);
			var value = del.Invoke ();
			return value is WeakReference weak ? (T)weak.Target : (T)value;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateMulti<T1, T2> (T1 w1, T2 w2) where T1 : Delegate where T2 : Delegate
		{
			Create (new GetMultiDelegateDelegate (t => {
				if (t == typeof (T1)) return w1;
				if (t == typeof (T2)) return w2;
				throw new ArgumentOutOfRangeException (nameof (t));
			}), out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateMulti<T1, T2, T3> (T1 w1, T2 w2, T3 w3) where T1 : Delegate where T2 : Delegate where T3 : Delegate
		{
			Create (new GetMultiDelegateDelegate (t => {
				if (t == typeof (T1)) return w1; if (t == typeof (T2)) return w2; if (t == typeof (T3)) return w3;
				throw new ArgumentOutOfRangeException (nameof (t));
			}), out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static T GetMulti<T> (IntPtr cp, out GCHandle gch) where T : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			return (T)multi.Invoke (typeof (T));
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void GetMulti<T1, T2> (IntPtr cp, out T1 w1, out T2 w2, out GCHandle gch) where T1 : Delegate where T2 : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			w1 = (T1)multi.Invoke (typeof (T1)); w2 = (T2)multi.Invoke (typeof (T2));
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void GetMulti<T1, T2, T3> (IntPtr cp, out T1 w1, out T2 w2, out T3 w3, out GCHandle gch) where T1 : Delegate where T2 : Delegate where T3 : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			w1 = (T1)multi.Invoke (typeof (T1)); w2 = (T2)multi.Invoke (typeof (T2)); w3 = (T3)multi.Invoke (typeof (T3));
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateMultiUserData<T> (T w, object userData, bool makeWeak = false) where T : Delegate
		{
			userData = makeWeak ? new WeakReference (userData) : userData;
			Create (new GetMultiDelegateDelegate (t => {
				if (t == typeof (T)) return w;
				if (t == typeof (UserDataDelegate)) return new UserDataDelegate (() => userData);
				throw new ArgumentOutOfRangeException (nameof (t));
			}), out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateMultiUserData<T1, T2> (T1 w1, T2 w2, object userData, bool makeWeak = false) where T1 : Delegate where T2 : Delegate
		{
			userData = makeWeak ? new WeakReference (userData) : userData;
			Create (new GetMultiDelegateDelegate (t => {
				if (t == typeof (T1)) return w1; if (t == typeof (T2)) return w2;
				if (t == typeof (UserDataDelegate)) return new UserDataDelegate (() => userData);
				throw new ArgumentOutOfRangeException (nameof (t));
			}), out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateMultiUserData<T1, T2, T3> (T1 w1, T2 w2, T3 w3, object userData, bool makeWeak = false) where T1 : Delegate where T2 : Delegate where T3 : Delegate
		{
			userData = makeWeak ? new WeakReference (userData) : userData;
			Create (new GetMultiDelegateDelegate (t => {
				if (t == typeof (T1)) return w1; if (t == typeof (T2)) return w2; if (t == typeof (T3)) return w3;
				if (t == typeof (UserDataDelegate)) return new UserDataDelegate (() => userData);
				throw new ArgumentOutOfRangeException (nameof (t));
			}), out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static TUserData GetMultiUserData<TUserData> (IntPtr cp, out GCHandle gch)
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			return GetUserData<TUserData> (multi);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void GetMultiUserData<T, TUserData> (IntPtr cp, out T w, out TUserData ud, out GCHandle gch) where T : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			w = (T)multi.Invoke (typeof (T)); ud = GetUserData<TUserData> (multi);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void GetMultiUserData<T1, T2, TUserData> (IntPtr cp, out T1 w1, out T2 w2, out TUserData ud, out GCHandle gch) where T1 : Delegate where T2 : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			w1 = (T1)multi.Invoke (typeof (T1)); w2 = (T2)multi.Invoke (typeof (T2)); ud = GetUserData<TUserData> (multi);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void GetMultiUserData<T1, T2, T3, TUserData> (IntPtr cp, out T1 w1, out T2 w2, out T3 w3, out TUserData ud, out GCHandle gch) where T1 : Delegate where T2 : Delegate where T3 : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			w1 = (T1)multi.Invoke (typeof (T1)); w2 = (T2)multi.Invoke (typeof (T2)); w3 = (T3)multi.Invoke (typeof (T3)); ud = GetUserData<TUserData> (multi);
		}

		private static TUserData GetUserData<TUserData> (GetMultiDelegateDelegate multi)
		{
			var userDataDelegate = (UserDataDelegate)multi.Invoke (typeof (UserDataDelegate));
			var value = userDataDelegate.Invoke ();
			return value is WeakReference weak ? (TUserData)weak.Target : (TUserData)value;
		}

		// === HarfBuzzSharp-specific partial methods ===

		private static partial void DestroyProxyImplementation (void* user_data)
		{
			var del = Get<ReleaseDelegate> ((IntPtr)user_data, out var gch);
			try { del.Invoke (); } finally { gch.Free (); }
		}

		private static partial void DestroyProxyImplementationForMulti (void* user_data)
		{
			var del = GetMulti<ReleaseDelegate> ((IntPtr)user_data, out var gch);
			try { del?.Invoke (); } finally { gch.Free (); }
		}

		private static partial IntPtr ReferenceTableProxyImplementation (IntPtr face, uint tag, void* user_data)
		{
			GetMultiUserData<GetTableDelegate, Face> ((IntPtr)user_data, out var getTable, out var userData, out _);
			var blob = getTable.Invoke (userData, tag);
			return blob?.Handle ?? IntPtr.Zero;
		}
	}
}
