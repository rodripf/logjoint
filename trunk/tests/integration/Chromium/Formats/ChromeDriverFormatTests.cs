﻿using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using LogJoint.Chromium;
using LogJoint.UI.Presenters.Postprocessing.MainWindowTabPage;

namespace LogJoint.Tests.Integration.Chromium
{
	[TestFixture]
	class ChromeDriverFormatTests
	{
		PluginLoader pluginLoader = new PluginLoader();
		SamplesUtils samples = new SamplesUtils();
		TestAppInstance app;

		[SetUp]
		public async Task BeforeEach()
		{
			app = await TestAppInstance.Create();
			app.Model.PluginFormatsManager.RegisterPluginFormats(pluginLoader.Manifest);
			Factory.Create(app.Model.ExpensibilityEntryPoint);
		}

		[TearDown]
		public async Task AfterEach()
		{
			await app.Dispose();
		}

		[Test]
		public async Task LoadsLogFileAndEnablesPostprocessors()
		{
			await app.SynchronizationContext.InvokeAndAwait(async () =>
			{
				await app.EmulateFileDragAndDrop(await samples.GetSampleAsLocalFile("chromedriver_1.log"));

				await app.WaitFor(() => !app.ViewModel.LoadedMessagesLogViewer.ViewLines.IsEmpty);

				Assert.AreEqual("[1548250986.197][INFO]: Waiting for pending navigations...", app.ViewModel.LoadedMessagesLogViewer.ViewLines[2].TextLineValue);
				app.ViewModel.MainForm.OnTabChanging(app.ViewModel.PostprocessingTabPageId);
				var postprocessorsControls = app.ViewModel.PostprocessingTabPage.ControlsState;
				Assert.IsFalse(postprocessorsControls[ViewControlId.Timeline].Disabled);

				return 0;
			});
		}
	}
}
