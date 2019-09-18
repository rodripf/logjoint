using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Linq;
using LogJoint.Workspaces;
using LogJoint.Preprocessing;
using LogJoint.MRU;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LogJoint.UI.Presenters.HistoryDialog
{
	public class Presenter : IPresenter, IViewEvents
	{
		readonly IView view;
		readonly ILogSourcesManager logSourcesManager;
		readonly MRU.IRecentlyUsedEntities mru;
		readonly Preprocessing.IManager sourcesPreprocessingManager;
		readonly Preprocessing.IStepsFactory preprocessingStepsFactory;
		readonly QuickSearchTextBox.IPresenter searchBoxPresenter;
		readonly LJTraceSource trace;
		readonly IAlertPopup alerts;
		List<ViewItem> items, displayItems;
		bool itemsFiltered;

		public Presenter(
			ILogSourcesManager logSourcesManager,
			IView view,
			Preprocessing.IManager sourcesPreprocessingManager,
			Preprocessing.IStepsFactory preprocessingStepsFactory,
			MRU.IRecentlyUsedEntities mru,
			QuickSearchTextBox.IPresenter searchBoxPresenter,
			IAlertPopup alerts,
			ITraceSourceFactory traceSourceFactory
		)
		{
			this.view = view;
			this.logSourcesManager = logSourcesManager;
			this.sourcesPreprocessingManager = sourcesPreprocessingManager;
			this.preprocessingStepsFactory = preprocessingStepsFactory;
			this.mru = mru;
			this.searchBoxPresenter = searchBoxPresenter;
			this.trace = traceSourceFactory.CreateTraceSource("UI", "hist-dlg");
			this.alerts = alerts;

			searchBoxPresenter.OnSearchNow += (s, e) =>
			{
				UpdateItems();
				FocusItemsListAndSelectFirstItem();
			};
			searchBoxPresenter.OnRealtimeSearch += (s, e) => UpdateItems();
			searchBoxPresenter.OnCancelled += (s, e) =>
			{
				if (itemsFiltered)
				{
					UpdateItems();
					searchBoxPresenter.Focus(null);
				}
				else
				{
					view.Hide();
				}
			};

			view.SetEventsHandler(this);
		}


		void IPresenter.ShowDialog()
		{
			view.AboutToShow();
			UpdateItems(ignoreFilter: true);
			view.Show();
		}

		void IViewEvents.OnDialogShown()
		{
			searchBoxPresenter.Focus("");
			UpdateOpenButton();
		}

		void IViewEvents.OnDialogHidden()
		{
			Cleanup();
		}

		void IViewEvents.OnOpenClicked()
		{
			OpenEntries();
		}

		void IViewEvents.OnDoubleClick()
		{
			OpenEntries();
		}

		void IViewEvents.OnFindShortcutPressed()
		{
			searchBoxPresenter.Focus(null);
		}

		void IViewEvents.OnSelectedItemsChanged()
		{
			UpdateOpenButton();
		}

		void IViewEvents.OnClearHistoryButtonClicked()
		{
			if (alerts.ShowPopup(
				"Clear history",
				string.Format("Do you want to clear the history ({0} items)?", items.Count),
				AlertFlags.YesNoCancel | AlertFlags.WarningIcon
			) == AlertFlags.Yes)
			{
				mru.ClearRecentLogsList();
			}
		}

		private async void OpenEntries()
		{
			var selected = view.SelectedItems;
			if (selected.All(i => i.Data == null))
				return;
			view.Hide();
			if (selected.Any(i => i.Data is RecentWorkspaceEntry))
			{
				await Task.WhenAll(logSourcesManager.DeleteAllLogs(), sourcesPreprocessingManager.DeleteAllPreprocessings());
			}
			var tasks = selected.Select(async item => 
			{
				try
				{
					if (item.Data is RecentLogEntry log)
						await sourcesPreprocessingManager.Preprocess(log);
					else if (item.Data is RecentWorkspaceEntry ws)
						await sourcesPreprocessingManager.OpenWorkspace(preprocessingStepsFactory, ws.Url);
					else if (item.Data is IRecentlyUsedEntity[] container)
						await Task.WhenAll(container.OfType<RecentLogEntry>().Select(innerLog => sourcesPreprocessingManager.Preprocess(innerLog)));
				}
				catch (Exception e)
				{
					trace.Error(e, "failed to open '{0}'", item.Text);
					alerts.ShowPopup("Error", "Failed to open " + item.Text, AlertFlags.Ok | AlertFlags.WarningIcon);
				}
			});
			await Task.WhenAll(tasks);
		}

		private void Cleanup()
		{
			items.Clear();
			items.Capacity = 0;
			displayItems.Clear();
			displayItems.Capacity = 0;
			view.Update(new ViewItem[0]);
		}

		private void UpdateItems(bool ignoreFilter = false)
		{
			var filter = ignoreFilter ? "" : searchBoxPresenter.Text;
			this.itemsFiltered = !string.IsNullOrEmpty(filter);
			this.items = new List<ViewItem>();
			this.displayItems = new List<ViewItem>();
			var timeGroups = MakeTimeGroups();
			foreach (
				var i in 
				mru.GetMRUList()
				.Where(e =>
					!itemsFiltered
					|| e.UserFriendlyName.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0
					|| (e.Annotation ?? "").IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0
				))
			{
				var d = i.UseTimestampUtc.GetValueOrDefault(DateTime.MinValue.AddYears(1)).ToLocalTime();
				var gidx = timeGroups.BinarySearch(0, timeGroups.Count, g => g.begin > d);
				timeGroups[Math.Min(gidx, timeGroups.Count - 1)].items.Add(i);
			}
			foreach (var timeGroup in timeGroups.Where(g => g.items.Count > 0))
			{
				var timeGroupItem = new ViewItem()
				{
					Type = ViewItemType.Comment,
					Text = timeGroup.name,
					Children = new List<ViewItem>()
				};
				displayItems.Add(timeGroupItem);
				foreach (var containerGroup in timeGroup.items.GroupBy(i => 
				{
					string containerName = null;
					var cp = i.ConnectionParams;
					if (cp != null)
						containerName = sourcesPreprocessingManager.ExtractContentsContainerNameFromConnectionParams(cp);
					if (containerName == null)
						containerName = i.GetHashCode().ToString();
					return containerName;
				}))
				{
					var groupItems = containerGroup.ToArray();
					var containerGroupItem = timeGroupItem;
					if (groupItems.Length > 1)
					{
						containerGroupItem = new ViewItem()
						{
							Type = ViewItemType.ItemsContainer,
							Text = containerGroup.Key,
							Children = new List<ViewItem>(),
							Data = groupItems
						};
						items.Add(containerGroupItem);
						timeGroupItem.Children.Add(containerGroupItem);
					}
					foreach (var e in groupItems)
					{
						var vi = new ViewItem()
						{
							Type = ViewItemType.Leaf,
							Text = e.UserFriendlyName,
							Annotation = e.Annotation,
							Data = e
						};
						items.Add(vi);
						containerGroupItem.Children.Add(vi);
					}
				}
			}
			view.Update(displayItems.ToArray());
			UpdateOpenButton();
		}

		static List<ItemsGroup> MakeTimeGroups()
		{
			var groups = new List<ItemsGroup>();
			var now = DateTime.Now.Date;
			groups.Add(new ItemsGroup()
			{
				name = "Today",
				begin = now
			});
			groups.Add(new ItemsGroup()
			{
				name = "Yesterday",
				begin = now.AddDays(-1)
			});
			for (int i = -2; i > -7; --i)
			{
				groups.Add(new ItemsGroup()
				{
					name = now.AddDays(i).ToLongDateString(),
					begin = now.AddDays(i)
				});
			}
			groups.Add(new ItemsGroup()
			{
				name = "Older than week",
				begin = now.AddDays(-30)
			});
			groups.Add(new ItemsGroup()
			{
				name = "Older than month",
				begin = DateTime.MinValue
			});
			return groups;
		}

		private void FocusItemsListAndSelectFirstItem()
		{
			view.PutInputFocusToItemsList();
			view.SelectedItems = items.Take(1).ToArray();
		}

		void UpdateOpenButton()
		{
			var canOpen = view.SelectedItems.Any(i => 
				i.Type == ViewItemType.Leaf || i.Type == ViewItemType.ItemsContainer);
			view.EnableOpenButton(canOpen);
		}

		[DebuggerDisplay("{name} {begin}")]
		class ItemsGroup
		{
			public string name;
			public DateTime begin;
			public List<IRecentlyUsedEntity> items = new List<IRecentlyUsedEntity>();
		};
	};
};