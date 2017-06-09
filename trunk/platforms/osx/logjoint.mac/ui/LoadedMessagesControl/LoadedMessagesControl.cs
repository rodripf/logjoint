﻿
using System;
using System.Collections.Generic;
using System.Linq;
using Foundation;
using AppKit;

namespace LogJoint.UI
{
	public partial class LoadedMessagesControl : AppKit.NSView
	{
		#region Constructors

		// Called when created from unmanaged code
		public LoadedMessagesControl(IntPtr handle)
			: base(handle)
		{
			Initialize();
		}
		
		// Called when created directly from a XIB file
		[Export("initWithCoder:")]
		public LoadedMessagesControl(NSCoder coder)
			: base(coder)
		{
			Initialize();
		}
		
		// Shared initialization code
		void Initialize()
		{
		}

		#endregion
	}
}

