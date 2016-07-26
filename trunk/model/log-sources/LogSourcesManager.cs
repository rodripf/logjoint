using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Linq;
using System.Xml.Linq;

namespace LogJoint
{
	public class LogSourcesManager : ILogSourcesManager, ILogSourcesManagerInternal
	{
		public LogSourcesManager(IHeartBeatTimer heartbeat,
			IInvokeSynchronization invoker, IModelThreads threads, ITempFilesManager tempFilesManager,
			Persistence.IStorageManager storageManager, IBookmarks bookmarks,
			Settings.IGlobalSettingsAccessor globalSettingsAccess)
		{
			this.tracer = new LJTraceSource("LogSourcesManager", "lsm");
			this.bookmarks = bookmarks;
			this.tempFilesManager = tempFilesManager;
			this.invoker = invoker;
			this.storageManager = storageManager;
			this.globalSettingsAccess = globalSettingsAccess;

			this.threads = threads;
			if (this.threads == null)
				throw new ArgumentException("threads cannot be null");

			if (invoker == null)
				throw new ArgumentException("invoker cannot be null");

			this.bookmarks.OnBookmarksChanged += Bookmarks_OnBookmarksChanged;

			heartbeat.OnTimer += (s, e) =>
			{
				if (e.IsRareUpdate)
					PeriodicUpdate();
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

		// todo: remove search events
		public event EventHandler OnSearchStarted;
		public event EventHandler<SearchFinishedEventArgs> OnSearchCompleted;
		public event EventHandler OnViewTailModeChanged;

		IEnumerable<ILogSource> ILogSourcesManager.Items
		{
			get { return logSources; }
		}

		ILogSourceInternal ILogSourcesManager.Create(ILogProviderFactory providerFactory, IConnectionParams cp)
		{
			return new LogSource(
				this,
				++lastLogSourceId,
				providerFactory,
				cp,
				threads,
				tempFilesManager,
				storageManager,
				invoker,
				globalSettingsAccess,
				bookmarks
			);
		}

		ILogSource ILogSourcesManager.Find(IConnectionParams connectParams)
		{
			return logSources.FirstOrDefault(s => ConnectionParamsUtils.ConnectionsHaveEqualIdentities(s.Provider.ConnectionParams, connectParams));
		}

		int ILogSourcesManager.GetSearchCompletionPercentage()
		{
			int sum = 0;
			int count = 0;
			foreach (ILogProvider p in lastSearchProviders)
			{
				if (p.IsDisposed)
					continue;
				sum += p.Stats.SearchCompletionPercentage;
				count++;
			}
			if (count == 0)
				return 0;
			return sum / count;
		}

		bool ILogSourcesManager.IsInViewTailMode
		{
			get
			{
				return false; // todo: reimplement "view tail" 
			}
		}

		void ILogSourcesManager.Refresh()
		{
			foreach (ILogSource s in logSources.Where(s => s.Visible))
			{
				s.Provider.Refresh();
			}
		}

		List<ILogSource> ILogSourcesManagerInternal.Container { get { return logSources; }}

		void ILogSourcesManagerInternal.FireOnLogSourceAdded(ILogSource sender)
		{
			if (OnLogSourceAdded != null)
				OnLogSourceAdded(this, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.FireOnLogSourceRemoved(ILogSource sender)
		{
			if (OnLogSourceRemoved != null)
				OnLogSourceRemoved(this, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceVisibilityChanged(ILogSource t)
		{
			if (OnLogSourceVisiblityChanged != null)
				OnLogSourceVisiblityChanged(this, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceTrackingChanged(ILogSource t)
		{
			if (OnLogSourceTrackingFlagChanged != null)
				OnLogSourceTrackingFlagChanged(t, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceAnnotationChanged(ILogSource t)
		{
			if (OnLogSourceAnnotationChanged != null)
				OnLogSourceAnnotationChanged(t, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceColorChanged(ILogSource logSource)
		{
			if (OnLogSourceColorChanged != null)
				OnLogSourceColorChanged(logSource, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnTimeOffsetChanged(ILogSource logSource)
		{
			if (OnLogSourceTimeOffsetChanged != null)
				OnLogSourceTimeOffsetChanged(logSource, EventArgs.Empty);
		}

		void ILogSourcesManagerInternal.OnSourceStatsChanged(ILogSource logSource, LogProviderStatsFlag flags)
		{
			if (OnLogSourceStatsChanged != null)
				OnLogSourceStatsChanged(logSource, new LogSourceStatsEventArgs() { flags = flags });
		}

		void ILogSourcesManagerInternal.OnTimegapsChanged(ILogSource logSource)
		{
			if (OnLogTimeGapsChanged != null)
				OnLogTimeGapsChanged(logSource, EventArgs.Empty);
		}

		#region Implementation

		void PeriodicUpdate()
		{
			foreach (ILogSource s in logSources.Where(s => !s.IsDisposed && s.Visible && s.TrackingEnabled))
			{
				s.Provider.PeriodicUpdate();
			}
		}

		private void SearchCompletionHandler(ILogProvider provider, object result, Exception error)
		{
			lastSearchProviders.Remove(provider);
			SearchAllOccurencesResponseData searchResponse = result as SearchAllOccurencesResponseData;
			if (searchResponse != null)
			{
				if (searchResponse.SearchWasInterrupted)
					lastSearchWasInterrupted = true;
				if (searchResponse.HitsLimitReached)
					lastSearchReachedHitsLimit = true;
				lastSearchHitsCount = searchResponse.Hits;
			}
			if (lastSearchProviders.Count == 0)
				SearchFinishedInternal();
		}

		private void SearchFinishedInternal()
		{
			if (OnSearchCompleted != null)
				OnSearchCompleted(this, new SearchFinishedEventArgs()
				{
					searchWasInterrupted = lastSearchWasInterrupted,
					hitsLimitReached = lastSearchReachedHitsLimit,
					hitsCount = lastSearchHitsCount
				});
		}

		void Bookmarks_OnBookmarksChanged(object sender, BookmarksChangedEventArgs e)
		{
			if (e.Type == BookmarksChangedEventArgs.ChangeType.Added || e.Type == BookmarksChangedEventArgs.ChangeType.Removed ||
				e.Type == BookmarksChangedEventArgs.ChangeType.RemovedAll || e.Type == BookmarksChangedEventArgs.ChangeType.Purged)
			{
				foreach (var affectedSource in
					e.AffectedBookmarks
					.Select(b => b.GetLogSource())
					.Where(LogSourceIsOkToStoreBookmarks)
					.Distinct())
				{
					try
					{
						affectedSource.StoreBookmarks();
					}
					catch (Persistence.StorageException storageException)
					{
						tracer.Error(storageException, "Failed to store bookmarks for log {0}", affectedSource.GetSafeConnectionId());
					}
				}
			}
		}

		static bool LogSourceIsOkToStoreBookmarks(ILogSource s)
		{
			if (s == null || s.IsDisposed)
				return false;
			if (s.Provider == null || s.Provider.IsDisposed)
				return false;
			var state = s.Provider.Stats.State;
			if (state == LogProviderState.LoadError || state == LogProviderState.NoFile)
				return false;
			return true;
		}

		#endregion

		#region Data

		readonly List<ILogSource> logSources = new List<ILogSource>();
		readonly IModelThreads threads;
		readonly LJTraceSource tracer;
		readonly IBookmarks bookmarks;
		readonly IInvokeSynchronization invoker;
		readonly Persistence.IStorageManager storageManager;
		readonly ITempFilesManager tempFilesManager;
		readonly Settings.IGlobalSettingsAccessor globalSettingsAccess;

		// todo: remove search stuff
		readonly List<ILogProvider> lastSearchProviders = new List<ILogProvider>();
		bool lastSearchWasInterrupted;
		bool lastSearchReachedHitsLimit;
		int lastSearchHitsCount;

		int lastLogSourceId;

		#endregion
	};
}
