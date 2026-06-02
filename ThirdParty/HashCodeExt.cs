// Ref-extension helpers for System.HashCode to handle void* and function pointer types
// that cannot be passed as generic type arguments to HashCode.Add<T>().
// Used by SkiaSharp and HarfBuzzSharp generated code.

using System;
using System.Runtime.CompilerServices;

namespace SkiaSharp
{
	internal static class HashCodeExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void AddPtr(this ref System.HashCode h, void* value)
		{
			h.Add((nint)value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void AddPtr(this ref System.HashCode h,
			delegate* unmanaged[Cdecl]<nint, void*, void*, nint, bool> value)
		{
			h.Add((nint)value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void AddPtr(this ref System.HashCode h,
			delegate* unmanaged[Cdecl]<nint, void*, void> value)
		{
			h.Add((nint)value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void AddPtr(this ref System.HashCode h,
			delegate* unmanaged[Cdecl]<nint, void*, nint> value)
		{
			h.Add((nint)value);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void AddPtr(this ref System.HashCode h,
			delegate* unmanaged[Cdecl]<nint, void*, void*, void*, void*, ulong, void> value)
		{
			h.Add((nint)value);
		}
	}
}

namespace HarfBuzzSharp
{
	internal static class HashCodeExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static unsafe void AddPtr(this ref System.HashCode h, void* value)
		{
			h.Add((nint)value);
		}
	}
}
