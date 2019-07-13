using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace LogJoint
{
	public class LogSourcesManager : ILogSourcesManager, ILogSourcesManagerInternal
	{
		public LogSourcesManager(
			IHeartBeatTimer heartbeat,
			ISynchronizationContext invoker,
			IModelThreadsInternal threads,
			ITempFilesManager tempFilesManager,
			Persistence.IStorageManager storageManager,
			IBookmarks bookmarks,
			Settings.IGlobalSettingsAccessor globalSettingsAccess,
			MRU.IRecentlyUsedEntities recentlyUsedEntities,
			IShutdown shutdown,
			ITraceSourceFactory traceSourceFactory,
			IChangeNotification changeNotification
		) : this(heartbeat, recentlyUsedEntities, shutdown, traceSourceFactory, changeNotification,
			new LogSourceFactory(threads, bookmarks, invoker, storageManager, tempFilesManager, globalSettingsAccess, traceSourceFactory))
		{
		}

		internal LogSourcesManager(
			IHeartBeatTimer heartbeat,
			MRU.IRecentlyUsedEntities recentlyUsedEntities,
			IShutdown shutdown,
			ITraceSourceFactory traceSourceFactory,
			IChangeNotification changeNotification,
			ILogSourceFactory logSourceFactory
		)
		{
			this.tracer = traceSourceFactory.CreateTraceSource("LogSourcesManager", "lsm");
			this.logSourceFactory = logSourceFactory;
			this.recentlyUsedEntities = recentlyUsedEntities;
			this.changeNotification = changeNotification;

			heartbeat.OnTimer += (s, e) =>
			{
				if (e.IsRareUpdate)
					PeriodicUpdate();
			};

			shutdown.Cleanup += (sender, e) =>
			{
				shutdown.AddCleanupTask(this.DeleteAllLogs());
			};
		}

		public event EventHandler OnLogSourceAdded;
		public event EventHandler OnLogSourceRemoved;
		public event EventHandler OnLogSourceVisiblityChanged;
		public event EventHandler OnLogSourceTrackingFlagChanged;
		public event EventHandler OnLogSourceAnnotationChanged;
		public event EventHandler OnLogSourceTimeOffsetChanged;
		public event EventHandler OnLogSourceColorChanged;
		public event EventHandler<LogSourceStatsEventArgs> OnLogSourceStatsChanged;
		public event EventHandler OnLogTimeGapsChanged;

		IReadOnlyList<ILogSource> ILogSourcesManager.Items => logSources;

		ILogSource ILogSourcesManager.Create(ILogProviderFactory providerFactory, IConnectionParams cp)
		{
			ILogSource src = ((ILogSourcesManager)this).Find(cp);
			if (src != null && src.Provider.Stats.State == LogProviderState.LoadError)
			{
				src.Dispose();
				src = null;
			}
			if (src == null)
			{
				src = logSourceFactory.CreateLogSource(
					this,
					++lastLogSourceId,
					providerFactory,
					cp
				);
			}
			recentlyUsedEntities.RegisterRecentLogEntry(src.Provider, src.Annotation);
			return src;
		}

		ILogSource ILogSourcesManager.Find(IConnectionParams connectParams)
		{
			return logSources.FirstOrDefault(s => ConnectionParamsUtils.ConnectionsHaveEqualIdentities(s.Provider.ConnectionParams, connectParams));
		}

		void ILogSourcesManager.Refresh()
		{
			foreach (ILogSource s in logSources.Where(s => s.Visible))
			{
				s.Provider.Refresh();
			}
		}

		ITimeOffsetsBuilder ILogSourcesManager.CreateTimeOffsetsBuilder()
		{
			return new TimeOffsets.Builder();
		}

		void ILogSourcesManagerInternal.Add(ILogSource ls)
		{
			logSources = logSources.Add(ls);
			changeNotification.Post();
		}

		void ILogSourcesManagerInternal.Remove(ILogSource ls)
		{
			logSources = logSources.Remove(ls);
			changeNotification.Post();
		}

		void ILogSourcesManagerInternal.FireOnLogSourceAdded(ILogSource sender)
		{
			OnLogSourceAdded?.Invoke(sender, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.FireOnLogSourceRemoved(ILogSource sender)
		{
			OnLogSourceRemoved?.Invoke(sender, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceVisibilityChanged(ILogSource t)
		{
			OnLogSourceVisiblityChanged?.Invoke(this, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceTrackingChanged(ILogSource t)
		{
			OnLogSourceTrackingFlagChanged?.Invoke(t, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceAnnotationChanged(ILogSource t)
		{
			recentlyUsedEntities.UpdateRecentLogEntry(t.Provider, t.Annotation);
			OnLogSourceAnnotationChanged?.Invoke(t, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceColorChanged(ILogSource logSource)
		{
			OnLogSourceColorChanged?.Invoke(logSource, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnTimeOffsetChanged(ILogSource logSource)
		{
			OnLogSourceTimeOffsetChanged?.Invoke(logSource, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceStatsChanged(ILogSource logSource, LogProviderStatsFlag flags)
		{
			OnLogSourceStatsChanged?.Invoke(logSource, new LogSourceStatsEventArgs(flags));
		}

		void ILogSourcesManagerInternal.OnTimegapsChanged(ILogSource logSource)
		{
			OnLogTimeGapsChanged?.Invoke(logSource, EventArgs.Empty);
		}

		#region Implementation

		void PeriodicUpdate()
		{
			foreach (ILogSource s in logSources.Where(s => s.Visible && s.TrackingEnabled))
			{
				s.Provider.PeriodicUpdate();
			}
		}

		#endregion

		#region Data

		readonly ILogSourceFactory logSourceFactory;
		readonly MRU.IRecentlyUsedEntities recentlyUsedEntities;
		readonly LJTraceSource tracer;
		readonly IChangeNotification changeNotification;
		ImmutableList<ILogSource> logSources = ImmutableList<ILogSource>.Empty;

		int lastLogSourceId;

		#endregion
	};
}
