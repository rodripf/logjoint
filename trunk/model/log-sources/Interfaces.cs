using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LogJoint
{
	public interface ILogSource
	{
		ILogProvider Provider { get; }
		string ConnectionId { get; }
		bool IsDisposed { get; }
		Task Dispose();
		ILogSourceThreads Threads { get; }
		ModelColor Color { get; }
		DateRange AvailableTime { get; }
		DateRange LoadedTime { get; }
#if !SILVERLIGHT
		System.Drawing.Brush SourceBrush { get; }
#endif
		bool Visible { get; set; }
		string DisplayName { get; }
		bool TrackingEnabled { get; set; }
		string Annotation { get; set; }
		TimeSpan TimeOffset { get; set; }
		Persistence.IStorageEntry LogSourceSpecificStorageEntry { get; }
		ITimeGapsDetector TimeGaps { get; }

		void StoreBookmarks();
	}

	public interface ILogSourcesManager
	{
		IEnumerable<ILogSource> Items { get; }
		ILogSourceInternal Create(ILogProviderFactory providerFactory, IConnectionParams cp);
		ILogSource Find(IConnectionParams connectParams);
		void NavigateTo(DateTime? d, NavigateFlag flags, ILogSource preferredSource);
		void SearchAllOccurences(SearchAllOccurencesParams searchParams);
		SearchAllOccurencesParams LastSearchOptions { get; }
		void CancelSearch();
		int GetSearchCompletionPercentage();
		bool IsShiftableUp { get; }
		void ShiftUp();
		bool IsShiftableDown { get; }
		void ShiftDown();
		void ShiftAt(DateTime t);
		void ShiftHome();
		void ShiftToEnd();
		void CancelShifting();
		bool IsInViewTailMode { get; }
		void Refresh();
		void OnCurrentViewPositionChanged(DateTime? d);
		void SetCurrentViewPositionIfNeeded();
		bool AtLeastOneSourceIsBeingLoaded();

		event EventHandler OnLogSourceAdded;
		event EventHandler OnLogSourceRemoved;
		event EventHandler OnLogSourceVisiblityChanged;
		event EventHandler OnLogSourceMessagesChanged;
		event EventHandler OnLogSourceSearchResultChanged;
		event EventHandler OnLogSourceTrackingFlagChanged;
		event EventHandler OnLogSourceAnnotationChanged;
		event EventHandler OnLogSourceTimeOffsetChanged;
		event EventHandler<LogSourceStatsEventArgs> OnLogSourceStatsChanged;
		event EventHandler OnLogTimeGapsChanged;
		event EventHandler OnSearchStarted;
		event EventHandler<SearchFinishedEventArgs> OnSearchCompleted;
		event EventHandler OnViewTailModeChanged;
	};


	public class SearchFinishedEventArgs : EventArgs
	{
		public bool SearchWasInterrupted { get { return searchWasInterrupted; } }
		public bool HitsLimitReached { get { return hitsLimitReached; } }
		public int HitsCount { get { return hitsCount; } }

		internal bool searchWasInterrupted;
		internal bool hitsLimitReached;
		internal int hitsCount;
	};

	public class LogSourceStatsEventArgs : EventArgs
	{
		public LogProviderStatsFlag Flags { get { return flags; } }

		internal LogProviderStatsFlag flags;
	};

	public interface ILogSourcesManagerInternal: ILogSourcesManager
	{
		List<ILogSource> Container { get; }
		void ReleaseDisposedControlledSources();

		#region Single-threaded notifications
		void FireOnLogSourceAdded(ILogSource sender);
		void FireOnLogSourceRemoved(ILogSource sender);
		void OnTimegapsChanged(ILogSource logSource);
		void OnSourceVisibilityChanged(ILogSource logSource);
		void OnSourceTrackingChanged(ILogSource logSource);
		void OnSourceAnnotationChanged(ILogSource logSource);
		void OnTimeOffsetChanged(ILogSource logSource);
		void OnSourceStatsChanged(ILogSource logSource, LogProviderStatsFlag flags);
		void FireOnLogSourceSearchResultChanged(ILogSource source);
		void FireOnLogSourceMessagesChanged(ILogSource source);
		#endregion

		#region Notifications methods, might be called from any thread
		void OnAvailableTimeChanged(ILogSource logSource, bool changedIncrementally);
		void OnAboutToIdle(ILogSource s);
		#endregion
	};

	public interface ILogSourceInternal : ILogSource, ILogProviderHost
	{
	};
}
