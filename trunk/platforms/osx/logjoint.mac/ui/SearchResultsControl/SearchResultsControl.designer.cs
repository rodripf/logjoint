// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace LogJoint.UI
{
	[Register ("SearchResultsControlAdapter")]
	partial class SearchResultsControlAdapter
	{
		[Outlet]
		AppKit.NSButton closeSearchResultsButton { get; set; }

		[Outlet]
		AppKit.NSButton dropdownButton { get; set; }

		[Outlet]
		AppKit.NSClipView dropdownClipView { get; set; }

		[Outlet]
		LogJoint.UI.NSCustomizableView dropdownContainerView { get; set; }

		[Outlet]
		AppKit.NSLayoutConstraint dropdownHeightConstraint { get; set; }

		[Outlet]
		AppKit.NSScrollView dropdownScrollView { get; set; }

		[Outlet]
		AppKit.NSView logViewerPlaceholder { get; set; }

		[Outlet]
		AppKit.NSTableColumn pinColumn { get; set; }

		[Outlet]
		AppKit.NSProgressIndicator progressIndicator { get; set; }

		[Outlet]
		AppKit.NSButton selectCurrentTimeButton { get; set; }

		[Outlet]
		AppKit.NSTableColumn statusColumn { get; set; }

		[Outlet]
		AppKit.NSTableView tableView { get; set; }

		[Outlet]
		AppKit.NSTableColumn textColumn { get; set; }

		[Outlet]
		AppKit.NSTableColumn visiblityColumn { get; set; }

		[Action ("OnCloseSearchResultsButtonClicked:")]
		partial void OnCloseSearchResultsButtonClicked (Foundation.NSObject sender);

		[Action ("OnDropdownButtonClicked:")]
		partial void OnDropdownButtonClicked (Foundation.NSObject sender);

		[Action ("OnSelectCurrentTimeClicked:")]
		partial void OnSelectCurrentTimeClicked (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (closeSearchResultsButton != null) {
				closeSearchResultsButton.Dispose ();
				closeSearchResultsButton = null;
			}

			if (dropdownButton != null) {
				dropdownButton.Dispose ();
				dropdownButton = null;
			}

			if (dropdownClipView != null) {
				dropdownClipView.Dispose ();
				dropdownClipView = null;
			}

			if (dropdownContainerView != null) {
				dropdownContainerView.Dispose ();
				dropdownContainerView = null;
			}

			if (dropdownHeightConstraint != null) {
				dropdownHeightConstraint.Dispose ();
				dropdownHeightConstraint = null;
			}

			if (dropdownScrollView != null) {
				dropdownScrollView.Dispose ();
				dropdownScrollView = null;
			}

			if (logViewerPlaceholder != null) {
				logViewerPlaceholder.Dispose ();
				logViewerPlaceholder = null;
			}

			if (pinColumn != null) {
				pinColumn.Dispose ();
				pinColumn = null;
			}

			if (selectCurrentTimeButton != null) {
				selectCurrentTimeButton.Dispose ();
				selectCurrentTimeButton = null;
			}

			if (statusColumn != null) {
				statusColumn.Dispose ();
				statusColumn = null;
			}

			if (tableView != null) {
				tableView.Dispose ();
				tableView = null;
			}

			if (textColumn != null) {
				textColumn.Dispose ();
				textColumn = null;
			}

			if (visiblityColumn != null) {
				visiblityColumn.Dispose ();
				visiblityColumn = null;
			}

			if (progressIndicator != null) {
				progressIndicator.Dispose ();
				progressIndicator = null;
			}
		}
	}

	[Register ("SearchResultsControl")]
	partial class SearchResultsControl
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
