﻿using System;
using System.Collections.Generic;

namespace LogJoint
{
	public static class Selectors
	{
		public static Func<R> Create<A1, R>(
			Func<A1> argSelector1,
			Func<A1, R> resultSelector)
		{
			var memoArg1 = default(A1);
			var cmp1 = GetEqualityComparer<A1>();
			R memoRet = default(R);
			bool firstEvaluation = true;
			return () =>
			{
				var arg1 = argSelector1();
				if (firstEvaluation || !cmp1.Equals(arg1, memoArg1))
				{
					firstEvaluation = false;
					memoRet = resultSelector(arg1);
					memoArg1 = arg1;
				}
				return memoRet;
			};
		}

		public static Func<R> Create<A1, A2, R>(
			Func<A1> argSelector1,
			Func<A2> argSelector2,
			Func<A1, A2, R> resultSelector)
		{
			var memoArg1 = default(A1);
			var cmp1 = GetEqualityComparer<A1>();
			var memoArg2 = default(A2);
			var cmp2 = GetEqualityComparer<A2>();
			R memoRet = default(R);
			bool firstEvaluation = true;
			return () =>
			{
				var arg1 = argSelector1();
				var arg2 = argSelector2();
				if (firstEvaluation || !cmp1.Equals(arg1, memoArg1) || !cmp2.Equals(arg2, memoArg2))
				{
					firstEvaluation = false;
					memoRet = resultSelector(arg1, arg2);
					memoArg1 = arg1;
					memoArg2 = arg2;
				}
				return memoRet;
			};
		}

		public static Func<R> Create<A1, A2, A3, R>(
			Func<A1> argSelector1,
			Func<A2> argSelector2,
			Func<A3> argSelector3,
			Func<A1, A2, A3, R> resultSelector)
		{
			var memoArg1 = default(A1);
			var cmp1 = GetEqualityComparer<A1>();
			var memoArg2 = default(A2);
			var cmp2 = GetEqualityComparer<A2>();
			var memoArg3 = default(A3);
			var cmp3 = GetEqualityComparer<A3>();
			R memoRet = default(R);
			bool firstEvaluation = true;
			return () =>
			{
				var arg1 = argSelector1();
				var arg2 = argSelector2();
				var arg3 = argSelector3();
				if (firstEvaluation || !cmp1.Equals(arg1, memoArg1) || !cmp2.Equals(arg2, memoArg2) || !cmp3.Equals(arg3, memoArg3))
				{
					firstEvaluation = false;
					memoRet = resultSelector(arg1, arg2, arg3);
					memoArg1 = arg1;
					memoArg2 = arg2;
					memoArg3 = arg3;
				}
				return memoRet;
			};
		}

		public static Func<R> Create<A1, A2, A3, A4, R>(
			Func<A1> argSelector1,
			Func<A2> argSelector2,
			Func<A3> argSelector3,
			Func<A4> argSelector4,
			Func<A1, A2, A3, A4, R> resultSelector)
		{
			var memoArg1 = default(A1);
			var cmp1 = GetEqualityComparer<A1>();
			var memoArg2 = default(A2);
			var cmp2 = GetEqualityComparer<A2>();
			var memoArg3 = default(A3);
			var cmp3 = GetEqualityComparer<A3>();
			var memoArg4 = default(A4);
			var cmp4 = GetEqualityComparer<A4>();
			R memoRet = default(R);
			bool firstEvaluation = true;
			return () =>
			{
				var arg1 = argSelector1();
				var arg2 = argSelector2();
				var arg3 = argSelector3();
				var arg4 = argSelector4();
				if (firstEvaluation || !cmp1.Equals(arg1, memoArg1) || !cmp2.Equals(arg2, memoArg2) || !cmp3.Equals(arg3, memoArg3) || !cmp4.Equals(arg4, memoArg4))
				{
					firstEvaluation = false;
					memoRet = resultSelector(arg1, arg2, arg3, arg4);
					memoArg1 = arg1;
					memoArg2 = arg2;
					memoArg3 = arg3;
					memoArg4 = arg4;
				}
				return memoRet;
			};
		}

		public static Func<R> Create<A1, A2, A3, A4, A5, R>(
			Func<A1> argSelector1,
			Func<A2> argSelector2,
			Func<A3> argSelector3,
			Func<A4> argSelector4,
			Func<A5> argSelector5,
			Func<A1, A2, A3, A4, A5, R> resultSelector)
		{
			var memoArg1 = default(A1);
			var cmp1 = GetEqualityComparer<A1>();
			var memoArg2 = default(A2);
			var cmp2 = GetEqualityComparer<A2>();
			var memoArg3 = default(A3);
			var cmp3 = GetEqualityComparer<A3>();
			var memoArg4 = default(A4);
			var cmp4 = GetEqualityComparer<A4>();
			var memoArg5 = default(A5);
			var cmp5 = GetEqualityComparer<A5>();
			R memoRet = default(R);
			bool firstEvaluation = true;
			return () =>
			{
				var arg1 = argSelector1();
				var arg2 = argSelector2();
				var arg3 = argSelector3();
				var arg4 = argSelector4();
				var arg5 = argSelector5();
				if (firstEvaluation || !cmp1.Equals(arg1, memoArg1) || !cmp2.Equals(arg2, memoArg2) || !cmp3.Equals(arg3, memoArg3) || !cmp4.Equals(arg4, memoArg4) || cmp5.Equals(arg5, memoArg5))
				{
					firstEvaluation = false;
					memoRet = resultSelector(arg1, arg2, arg3, arg4, arg5);
					memoArg1 = arg1;
					memoArg2 = arg2;
					memoArg3 = arg3;
					memoArg4 = arg4;
					memoArg5 = arg5;
				}
				return memoRet;
			};
		}
		internal static IEqualityComparer<T> GetEqualityComparer<T>()
		{
			return EqualityComparer<T>.Default;
		}
	}

	public static class Updaters
	{
		public static Action Create<A1>(Func<A1> argSelector1, Action<A1, A1> update)
		{
			var prevA1 = default(A1);
			bool firstUpdate = true;
			var keyComparer = Selectors.GetEqualityComparer<A1>();
			return () =>
			{
				var a1 = argSelector1();
				if (firstUpdate || !keyComparer.Equals(a1, prevA1))
				{
					firstUpdate = false;
					prevA1 = a1;
					update(a1, prevA1);
				}
			};
		}

		public static Action Create<A1>(Func<A1> argSelector1, Action<A1> update)
		{
			return Create(argSelector1, (key, oldKey) => update(key));
		}

		public static Action Create<A1, A2>(Func<A1> argSelector1, Func<A2> argSelector2, Action<A1, A2, A1, A2> update)
		{
			return Create(
				Selectors.Create(
					argSelector1,
					argSelector2,
					(a1, a2) => (a1, a2)
				),
				(key, prevKey) => update(key.a1, key.a2, prevKey.a1, prevKey.a2)
			);
		}

		public static Action Create<A1, A2>(Func<A1> argSelector1, Func<A2> argSelector2, Action<A1, A2> update)
		{
			return Create(argSelector1, argSelector2, (a1, a2, oldA1, oldA2) => update(a1, a2));
		}

		public static Action Create<A1, A2, A3>(Func<A1> argSelector1, Func<A2> argSelector2, Func<A3> argSelector3, Action<A1, A2, A3, A1, A2, A3> update)
		{
			return Create(
				Selectors.Create(
					argSelector1,
					argSelector2,
					argSelector3,
					(a1, a2, a3) => (a1, a2, a3)
				),
				(key, prevKey) => update(key.a1, key.a2, key.a3, prevKey.a1, prevKey.a2, prevKey.a3)
			);
		}

		public static Action Create<A1, A2, A3>(Func<A1> argSelector1, Func<A2> argSelector2, Func<A3> argSelector3, Action<A1, A2, A3> update)
		{
			return Create(argSelector1, argSelector2, argSelector3, (a1, a2, a3, oldA1, oldA2, oldA3) => update(a1, a2, a3));
		}
	};
}
