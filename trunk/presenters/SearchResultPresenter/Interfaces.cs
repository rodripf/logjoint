using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;

namespace LogJoint.UI.Presenters.SearchResult
{
	public interface IView
	{
		void SetEventsHandler(IViewEvents presenter);
		Presenters.LogViewer.IView MessagesView { get; }
		bool IsMessagesViewFocused { get; }
		void FocusMessagesView();
		void UpdateItems(IList<ViewItem> items);
		void UpdateExpandedState(bool isExpandable, bool isExpanded, int preferredListHeightInRows, string expandButtonHint, string unexpandButtonHint);
	};

	public class ViewItem
	{
		public object Data;
		public string Text;
		public bool IsWarningText;
		public bool VisiblityControlChecked;
		public string VisiblityControlHint;
		public bool PinControlChecked;
		public string PinControlHint;
		public bool ProgressVisible;
		public double ProgressValue;
	};

	public interface IPresenter
	{
		Task<IMessage> Search(LogViewer.SearchOptions opts);
		bool IsViewFocused { get; }
		void ReceiveInputFocus();
		IMessage FocusedMessage { get; }
		IBookmark GetFocusedMessageBookmark();
		IBookmark MasterFocusedMessage { get; set; }
		void FindCurrentTime();

		event EventHandler OnClose;
		event EventHandler OnResizingStarted;
	};

	[Flags]
	public enum MenuItemId
	{
		None,
		Visible = 1,
		Pinned = 2,
		Delete = 4,
		VisibleOnTimeline = 8
	};

	public struct ContextMenuViewData
	{
		public MenuItemId VisibleItems;
		public MenuItemId CheckedItems;
	};

	public interface IViewEvents
	{
		void OnResizingStarted();
		void OnResizingFinished();
		void OnResizing(int delta);
		void OnToggleBookmarkButtonClicked();
		void OnFindCurrentTimeButtonClicked();
		void OnCloseSearchResultsButtonClicked();
		void OnRefreshButtonClicked();
		void OnExpandSearchesListClicked();
		void OnVisibilityCheckboxClicked(ViewItem item);
		void OnPinCheckboxClicked(ViewItem item);
		void OnDropdownContainerLostFocus();
		void OnDropdownEscape();
		void OnDropdownTextClicked();
		ContextMenuViewData OnContextMenuPopup(ViewItem viewItem);
		void OnMenuItemClicked(ViewItem viewItem, MenuItemId menuItemId);
	};
};