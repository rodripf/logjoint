using System;
using System.Runtime.InteropServices;

namespace LogJoint
{
	unsafe public class StringSliceReallocator : IStringSliceReallocator, IDisposable
	{
		char* bufPtr;
		string buf;
		int bufPosition;
		GCHandle bufHandle;

		public StringSliceReallocator()
		{
			AllocateNewBuffer(0);
		}

		void IDisposable.Dispose()
		{
			FreeBuffer();
		}

#if MONOMAC
		unsafe static char* memmove(char* dest, char* src, UIntPtr count)
		{
			Buffer.MemoryCopy((void*)src, (void*)dest, count.ToUInt32(), count.ToUInt32());
			return dest;
		}
#else
		[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
		unsafe static extern char* memmove(char* dest, char* src, UIntPtr count);
#endif

		StringSlice IStringSliceReallocator.Reallocate(StringSlice value)
		{
			if (value.Length == 0)
			{
				return value;
			}
			if (bufPosition + value.Length > buf.Length)
			{
				FreeBuffer();
				AllocateNewBuffer(value.Length);
			}
			var ret = new StringSlice(buf, bufPosition, value.Length);
			fixed (char* src = value.Buffer)
			{
				memmove(bufPtr + bufPosition, src + value.StartIndex, new UIntPtr((uint)value.Length * sizeof(char)));
				bufPosition += value.Length;
			}
			return ret;
		}

		void AllocateNewBuffer(int minLen)
		{
			int sz = 64 * 1024;
			while (sz < minLen)
				sz *= 2;
			buf = new string('\0', sz);
			bufHandle = GCHandle.Alloc(buf, GCHandleType.Pinned);
			bufPtr = (char*)bufHandle.AddrOfPinnedObject();
			bufPosition = 0;
		}

		void FreeBuffer()
		{
			bufHandle.Free();
		}
	};
}
