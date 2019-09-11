using System;
using System.Threading;

namespace LogJoint.MultiInstance
{
	public class InstancesCounter: IInstancesCounter
	{
		readonly string semaphoreName = "LogJoint.Lock";
		Semaphore sema;
		bool isFirstInstance;

		public InstancesCounter(IShutdown shutdown)
		{
			sema = new Semaphore(0, 1000, semaphoreName, out isFirstInstance);

			shutdown.Cleanup += (s, e) =>
			{
				if (sema != null)
				{
					sema.Release();
					sema = null;
				}
			};
		}
			
		bool IInstancesCounter.IsPrimaryInstance { get { return isFirstInstance; } }

		string IInstancesCounter.MutualExecutionKey { get { return semaphoreName; } }

		int IInstancesCounter.Count { get { throw new NotImplementedException(); } }
	};

	public class DummyInstancesCounter : IInstancesCounter
	{
		bool IInstancesCounter.IsPrimaryInstance => true;

		string IInstancesCounter.MutualExecutionKey => "";

		int IInstancesCounter.Count => 1;
	};
}
