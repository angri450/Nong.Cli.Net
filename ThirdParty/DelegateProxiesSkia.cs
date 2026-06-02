// Copy of Binding.Shared/DelegateProxies.shared.cs for SkiaSharp namespace
// (HarfBuzz copy was merged — HarfBuzzSharp now uses its own inline helpers)
#nullable disable

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SkiaSharp
{
	// helper delegates
	internal delegate Delegate GetMultiDelegateDelegate (Type index);
	internal delegate object UserDataDelegate ();

	internal static partial class DelegateProxies
	{
		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void Create (object managedDel, out GCHandle gch, out IntPtr contextPtr)
		{
			if (managedDel == null) {
				gch = default (GCHandle);
				contextPtr = IntPtr.Zero;
				return;
			}
			gch = GCHandle.Alloc (managedDel);
			contextPtr = GCHandle.ToIntPtr (gch);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static T Get<T> (IntPtr contextPtr, out GCHandle gch)
		{
			if (contextPtr == IntPtr.Zero) {
				gch = default (GCHandle);
				return default (T);
			}
			gch = GCHandle.FromIntPtr (contextPtr);
			return (T)gch.Target;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateUserData (object userData, bool makeWeak = false)
		{
			userData = makeWeak ? new WeakReference (userData) : userData;
			var del = new UserDataDelegate (() => userData);
			Create (del, out _, out var ctx);
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
		public static IntPtr CreateMulti<T1, T2> (T1 wrappedDelegate1, T2 wrappedDelegate2)
			where T1 : Delegate
			where T2 : Delegate
		{
			var del = new GetMultiDelegateDelegate ((type) => {
				if (type == typeof (T1)) return wrappedDelegate1;
				if (type == typeof (T2)) return wrappedDelegate2;
				throw new ArgumentOutOfRangeException (nameof (type));
			});
			Create (del, out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateMulti<T1, T2, T3> (T1 w1, T2 w2, T3 w3)
			where T1 : Delegate where T2 : Delegate where T3 : Delegate
		{
			var del = new GetMultiDelegateDelegate ((type) => {
				if (type == typeof (T1)) return w1;
				if (type == typeof (T2)) return w2;
				if (type == typeof (T3)) return w3;
				throw new ArgumentOutOfRangeException (nameof (type));
			});
			Create (del, out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static T GetMulti<T> (IntPtr contextPtr, out GCHandle gch) where T : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (contextPtr, out gch);
			return (T)multi.Invoke (typeof (T));
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void GetMulti<T1, T2> (IntPtr cp, out T1 w1, out T2 w2, out GCHandle gch)
			where T1 : Delegate where T2 : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			w1 = (T1)multi.Invoke (typeof (T1));
			w2 = (T2)multi.Invoke (typeof (T2));
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void GetMulti<T1, T2, T3> (IntPtr cp, out T1 w1, out T2 w2, out T3 w3, out GCHandle gch)
			where T1 : Delegate where T2 : Delegate where T3 : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			w1 = (T1)multi.Invoke (typeof (T1));
			w2 = (T2)multi.Invoke (typeof (T2));
			w3 = (T3)multi.Invoke (typeof (T3));
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateMultiUserData<T> (T w, object userData, bool makeWeak = false) where T : Delegate
		{
			userData = makeWeak ? new WeakReference (userData) : userData;
			var userDataDel = new UserDataDelegate (() => userData);
			var del = new GetMultiDelegateDelegate ((type) => {
				if (type == typeof (T)) return w;
				if (type == typeof (UserDataDelegate)) return userDataDel;
				throw new ArgumentOutOfRangeException (nameof (type));
			});
			Create (del, out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateMultiUserData<T1, T2> (T1 w1, T2 w2, object userData, bool makeWeak = false)
			where T1 : Delegate where T2 : Delegate
		{
			userData = makeWeak ? new WeakReference (userData) : userData;
			var userDataDel = new UserDataDelegate (() => userData);
			var del = new GetMultiDelegateDelegate ((type) => {
				if (type == typeof (T1)) return w1;
				if (type == typeof (T2)) return w2;
				if (type == typeof (UserDataDelegate)) return userDataDel;
				throw new ArgumentOutOfRangeException (nameof (type));
			});
			Create (del, out _, out var ctx);
			return ctx;
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static IntPtr CreateMultiUserData<T1, T2, T3> (T1 w1, T2 w2, T3 w3, object userData, bool makeWeak = false)
			where T1 : Delegate where T2 : Delegate where T3 : Delegate
		{
			userData = makeWeak ? new WeakReference (userData) : userData;
			var userDataDel = new UserDataDelegate (() => userData);
			var del = new GetMultiDelegateDelegate ((type) => {
				if (type == typeof (T1)) return w1;
				if (type == typeof (T2)) return w2;
				if (type == typeof (T3)) return w3;
				if (type == typeof (UserDataDelegate)) return userDataDel;
				throw new ArgumentOutOfRangeException (nameof (type));
			});
			Create (del, out _, out var ctx);
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
			w = (T)multi.Invoke (typeof (T));
			ud = GetUserData<TUserData> (multi);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void GetMultiUserData<T1, T2, TUserData> (IntPtr cp, out T1 w1, out T2 w2, out TUserData ud, out GCHandle gch)
			where T1 : Delegate where T2 : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			w1 = (T1)multi.Invoke (typeof (T1));
			w2 = (T2)multi.Invoke (typeof (T2));
			ud = GetUserData<TUserData> (multi);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		public static void GetMultiUserData<T1, T2, T3, TUserData> (IntPtr cp, out T1 w1, out T2 w2, out T3 w3, out TUserData ud, out GCHandle gch)
			where T1 : Delegate where T2 : Delegate where T3 : Delegate
		{
			var multi = Get<GetMultiDelegateDelegate> (cp, out gch);
			w1 = (T1)multi.Invoke (typeof (T1));
			w2 = (T2)multi.Invoke (typeof (T2));
			w3 = (T3)multi.Invoke (typeof (T3));
			ud = GetUserData<TUserData> (multi);
		}

		[MethodImpl (MethodImplOptions.AggressiveInlining)]
		private static TUserData GetUserData<TUserData> (GetMultiDelegateDelegate multi)
		{
			var userDataDelegate = (UserDataDelegate)multi.Invoke (typeof (UserDataDelegate));
			var value = userDataDelegate.Invoke ();
			return value is WeakReference weak ? (TUserData)weak.Target : (TUserData)value;
		}
	}
}
