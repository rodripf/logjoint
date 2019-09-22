﻿using LogJoint.UI.Presenters.Reactive;
using System;
using System.Collections.Generic;

namespace LogJoint.UI.Presenters.Postprocessing.StateInspectorVisualizer
{
	public interface IView
	{
		void SetViewModel(IViewModel value);
		void Show();

		void ScrollStateHistoryItemIntoView(int itemIndex);
	};

	public interface IObjectsTreeNode: ITreeNode
	{
	};

	public enum NodeColoring
	{
		Alive = 1,
		Deleted = 2,
		NotCreatedYet = 3
	};

	public enum Key
	{
		None,
		Enter,
		BookmarkShortcut,
	};

	public interface IViewModel
	{
		ColorThemeMode ColorTheme { get; }

		IChangeNotification ChangeNotification { get; }

		void OnVisibleChanged(bool value);

		IObjectsTreeNode ObjectsTreeRoot { get; }
		PaintNodeDelegate PaintNode { get; }
		void OnExpandNode(IObjectsTreeNode node);
		void OnCollapseNode(IObjectsTreeNode node);
		void OnSelect(IReadOnlyCollection<IObjectsTreeNode> value);
		MenuData OnNodeMenuOpening();
		void OnNodeDeleteKeyPressed();


		string CurrentTimeLabelText { get; }


		IReadOnlyList<KeyValuePair<string, object>> ObjectsProperties { get; }
		void OnPropertiesRowDoubleClicked(int rowIndex);
		PropertyCellPaintInfo OnPropertyCellPaint(int rowIndex);
		void OnPropertyCellClicked(int rowIndex);
		void OnPropertyCellCopyShortcutPressed(int propertyIndex);


		IReadOnlyList<IStateHistoryItem> ChangeHistoryItems { get; }
		Predicate<IStateHistoryItem> IsChangeHistoryItemBookmarked { get; }
		Tuple<int, int> FocusedMessagePositionInChangeHistory { get; }
		void OnChangeHistoryItemDoubleClicked(IStateHistoryItem item);
		void OnChangeHistoryItemKeyEvent(IStateHistoryItem item, Key key);
		void OnChangeHistoryChangeSelection(IEnumerable<IStateHistoryItem> items);
		void OnFindCurrentPositionInChangeHistory();
	};

	public struct PropertyCellPaintInfo
	{
		public bool PaintAsLink;
		public bool AddLeftPadding;
	};

	public struct NodePaintInfo
	{
		public bool DrawingEnabled;
		public NodeColoring Coloring;
		public string PrimaryPropValue;
		public bool DrawFocusedMsgMark;
	};

	public delegate NodePaintInfo PaintNodeDelegate(IObjectsTreeNode node, bool getPrimaryPropValue);

	public interface IStateHistoryItem: IListItem
	{
		string Time { get; }
		string Message { get; }
	};
}
