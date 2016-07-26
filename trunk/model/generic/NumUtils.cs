using System.Diagnostics;


namespace LogJoint
{
	public static class NumUtils
	{
		/// <summary>
		/// Calculates a * b / c
		/// </summary>
		public static long MulDiv(long a, int b, int c)
		{
			long whole = (a / c) * b;
			long fraction = (a % c) * b / c;
			return whole + fraction;
		}
	}

	[DebuggerDisplay("{Value}")]
	public class Ref<T> where T: struct
	{
		public T Value;
	};
}
