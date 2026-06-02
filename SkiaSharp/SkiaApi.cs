#nullable disable

using System;
using System.Runtime.InteropServices;
using sk_typeface_t = System.IntPtr;
using sk_path_t = System.IntPtr;

namespace SkiaSharp
{
	internal partial class SkiaApi
	{
#if __IOS__ || __TVOS__
		private const string SKIA = "@rpath/libSkiaSharp.framework/libSkiaSharp";
#else
		private const string SKIA = "libSkiaSharp";
#endif

#if USE_DELEGATES
		private static readonly Lazy<IntPtr> libSkiaSharpHandle =
			new Lazy<IntPtr> (() => LibraryLoader.LoadLocalLibrary<SkiaApi> (SKIA));

		private static T GetSymbol<T> (string name) where T : Delegate =>
			LibraryLoader.GetSymbolDelegate<T> (libSkiaSharpHandle.Value, name);
#endif

#if !USE_DELEGATES
#if USE_LIBRARY_IMPORT
		// Typeface compat
		[LibraryImport (SKIA)]
		internal static partial sk_typeface_t sk_typeface_create_default ();

		// Path builder compat shims — old sk_path_* API (present in native DLL)
		[LibraryImport (SKIA)]
		internal static partial void sk_path_move_to (sk_path_t path, float x, float y);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_rmove_to (sk_path_t path, float dx, float dy);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_line_to (sk_path_t path, float x, float y);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_rline_to (sk_path_t path, float dx, float dy);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_quad_to (sk_path_t path, float x0, float y0, float x1, float y1);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_rquad_to (sk_path_t path, float dx0, float dy0, float dx1, float dy1);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_conic_to (sk_path_t path, float x0, float y0, float x1, float y1, float w);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_rconic_to (sk_path_t path, float dx0, float dy0, float dx1, float dy1, float w);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_cubic_to (sk_path_t path, float x0, float y0, float x1, float y1, float x2, float y2);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_rcubic_to (sk_path_t path, float dx0, float dy0, float dx1, float dy1, float dx2, float dy2);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_arc_to (sk_path_t path, float rx, float ry, float xAxisRotate, SKPathArcSize largeArc, SKPathDirection sweep, float x, float y);
		[LibraryImport (SKIA)]
		internal static unsafe partial void sk_path_arc_to_with_oval (sk_path_t path, SKRect* oval, float startAngle, float sweepAngle, [MarshalAs (UnmanagedType.Bool)] bool forceMoveTo);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_arc_to_with_points (sk_path_t path, float x1, float y1, float x2, float y2, float radius);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_rarc_to (sk_path_t path, float rx, float ry, float xAxisRotate, SKPathArcSize largeArc, SKPathDirection sweep, float x, float y);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_close (sk_path_t path);
		[LibraryImport (SKIA)]
		internal static unsafe partial void sk_path_add_rect (sk_path_t path, SKRect* rect, SKPathDirection direction);
		[LibraryImport (SKIA)]
		internal static unsafe partial void sk_path_add_rect_start (sk_path_t path, SKRect* rect, SKPathDirection direction, uint startIndex);
		[LibraryImport (SKIA)]
		internal static unsafe partial void sk_path_add_oval (sk_path_t path, SKRect* oval, SKPathDirection direction);
		[LibraryImport (SKIA)]
		internal static unsafe partial void sk_path_add_arc (sk_path_t path, SKRect* oval, float startAngle, float sweepAngle);
		[LibraryImport (SKIA)]
		internal static unsafe partial void sk_path_add_rounded_rect (sk_path_t path, SKRect* rect, float rx, float ry, SKPathDirection dir);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_add_rrect (sk_path_t path, IntPtr rrect, SKPathDirection dir);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_add_rrect_start (sk_path_t path, IntPtr rrect, SKPathDirection dir, uint startIndex);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_add_circle (sk_path_t path, float x, float y, float radius, SKPathDirection dir);
		[LibraryImport (SKIA)]
		internal static unsafe partial void sk_path_add_poly (sk_path_t path, SKPoint* points, int count, [MarshalAs (UnmanagedType.Bool)] bool close);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_add_path_offset (sk_path_t path, sk_path_t other, float dx, float dy, SKPathAddMode mode);
		[LibraryImport (SKIA)]
		internal static unsafe partial void sk_path_add_path_matrix (sk_path_t path, sk_path_t other, SKMatrix* matrix, SKPathAddMode mode);
		[LibraryImport (SKIA)]
		internal static partial void sk_path_add_path (sk_path_t path, sk_path_t other, SKPathAddMode mode);
#else  // !USE_LIBRARY_IMPORT — DllImport fallbacks
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern sk_typeface_t sk_typeface_create_default ();
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_move_to (sk_path_t path, float x, float y);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_rmove_to (sk_path_t path, float dx, float dy);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_line_to (sk_path_t path, float x, float y);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_rline_to (sk_path_t path, float dx, float dy);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_quad_to (sk_path_t path, float x0, float y0, float x1, float y1);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_rquad_to (sk_path_t path, float dx0, float dy0, float dx1, float dy1);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_conic_to (sk_path_t path, float x0, float y0, float x1, float y1, float w);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_rconic_to (sk_path_t path, float dx0, float dy0, float dx1, float dy1, float w);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_cubic_to (sk_path_t path, float x0, float y0, float x1, float y1, float x2, float y2);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_rcubic_to (sk_path_t path, float dx0, float dy0, float dx1, float dy1, float dx2, float dy2);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_arc_to (sk_path_t path, float rx, float ry, float xAxisRotate, SKPathArcSize largeArc, SKPathDirection sweep, float x, float y);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_arc_to_with_oval (sk_path_t path, ref SKRect oval, float startAngle, float sweepAngle, bool forceMoveTo);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_arc_to_with_points (sk_path_t path, float x1, float y1, float x2, float y2, float radius);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_rarc_to (sk_path_t path, float rx, float ry, float xAxisRotate, SKPathArcSize largeArc, SKPathDirection sweep, float x, float y);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_close (sk_path_t path);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_rect (sk_path_t path, ref SKRect rect, SKPathDirection direction);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_rect_start (sk_path_t path, ref SKRect rect, SKPathDirection direction, uint startIndex);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_oval (sk_path_t path, ref SKRect oval, SKPathDirection direction);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_arc (sk_path_t path, ref SKRect oval, float startAngle, float sweepAngle);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_rounded_rect (sk_path_t path, ref SKRect rect, float rx, float ry, SKPathDirection dir);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_rrect (sk_path_t path, IntPtr rrect, SKPathDirection dir);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_rrect_start (sk_path_t path, IntPtr rrect, SKPathDirection dir, uint startIndex);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_circle (sk_path_t path, float x, float y, float radius, SKPathDirection dir);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_poly (sk_path_t path, IntPtr points, int count, bool close);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_path_offset (sk_path_t path, sk_path_t other, float dx, float dy, SKPathAddMode mode);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_path_matrix (sk_path_t path, sk_path_t other, ref SKMatrix matrix, SKPathAddMode mode);
		[DllImport (SKIA, CallingConvention = CallingConvention.Cdecl)]
		internal static extern void sk_path_add_path (sk_path_t path, sk_path_t other, SKPathAddMode mode);
#endif  // USE_LIBRARY_IMPORT
#endif  // !USE_DELEGATES
	}
}
