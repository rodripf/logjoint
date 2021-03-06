﻿// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace LogJoint.UI
{
	[Register ("AboutDialogAdapter")]
	partial class AboutDialogAdapter
	{
		[Outlet]
		AppKit.NSButton copyMacLinkButton { get; set; }

		[Outlet]
		AppKit.NSButton copyWinLinkButton { get; set; }

		[Outlet]
		AppKit.NSTextField feedbackLebel { get; set; }

		[Outlet]
		LogJoint.UI.NSLinkLabel feedbackLinkLabel { get; set; }

		[Outlet]
		AppKit.NSTextField macInstallerLinkField { get; set; }

		[Outlet]
		AppKit.NSTextField macInstallerText { get; set; }

		[Outlet]
		AppKit.NSTextField shareLabel { get; set; }

		[Outlet]
		AppKit.NSTextField textField { get; set; }

		[Outlet]
		AppKit.NSButton updateNowButton { get; set; }

		[Outlet]
		AppKit.NSTextField updatesCaptionLabel { get; set; }

		[Outlet]
		AppKit.NSTextField updatesStatusLabel { get; set; }

		[Outlet]
		AppKit.NSTextField winInstallerLinkLabel { get; set; }

		[Outlet]
		AppKit.NSTextField winInstallerText { get; set; }

		[Action ("OnCopyMacInstallerClicked:")]
		partial void OnCopyMacInstallerClicked (Foundation.NSObject sender);

		[Action ("OnCopyWinInstallerClicked:")]
		partial void OnCopyWinInstallerClicked (Foundation.NSObject sender);

		[Action ("OnUpdateNowClicked:")]
		partial void OnUpdateNowClicked (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (copyMacLinkButton != null) {
				copyMacLinkButton.Dispose ();
				copyMacLinkButton = null;
			}

			if (copyWinLinkButton != null) {
				copyWinLinkButton.Dispose ();
				copyWinLinkButton = null;
			}

			if (feedbackLebel != null) {
				feedbackLebel.Dispose ();
				feedbackLebel = null;
			}

			if (feedbackLinkLabel != null) {
				feedbackLinkLabel.Dispose ();
				feedbackLinkLabel = null;
			}

			if (macInstallerLinkField != null) {
				macInstallerLinkField.Dispose ();
				macInstallerLinkField = null;
			}

			if (macInstallerText != null) {
				macInstallerText.Dispose ();
				macInstallerText = null;
			}

			if (shareLabel != null) {
				shareLabel.Dispose ();
				shareLabel = null;
			}

			if (textField != null) {
				textField.Dispose ();
				textField = null;
			}

			if (winInstallerLinkLabel != null) {
				winInstallerLinkLabel.Dispose ();
				winInstallerLinkLabel = null;
			}

			if (winInstallerText != null) {
				winInstallerText.Dispose ();
				winInstallerText = null;
			}

			if (updatesCaptionLabel != null) {
				updatesCaptionLabel.Dispose ();
				updatesCaptionLabel = null;
			}

			if (updatesStatusLabel != null) {
				updatesStatusLabel.Dispose ();
				updatesStatusLabel = null;
			}

			if (updateNowButton != null) {
				updateNowButton.Dispose ();
				updateNowButton = null;
			}
		}
	}

	[Register ("AboutDialog")]
	partial class AboutDialog
	{
		
		void ReleaseDesignerOutlets ()
		{
		}
	}
}
