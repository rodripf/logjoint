﻿using System;
using System.Linq;
using System.Threading.Tasks;

namespace LogJoint.Tests.Integration
{
	internal static class TestAppExtensions
	{
		public static Task EmulateFileDragAndDrop(this TestAppInstance app, string fileName)
		{
			return app.ModelObjects.LogSourcesPreprocessings.Preprocess(
				new[] { app.ModelObjects.PreprocessingStepsFactory.CreateFormatDetectionStep(new Preprocessing.PreprocessingStepParams(fileName)) },
				fileName
			);
		}

		public static Task EmulateUrlDragAndDrop(this TestAppInstance app, Uri uri)
		{
			return app.ModelObjects.LogSourcesPreprocessings.Preprocess(
				new[] { app.ModelObjects.PreprocessingStepsFactory.CreateURLTypeDetectionStep(new Preprocessing.PreprocessingStepParams(uri.ToString())) },
				uri.ToString()
			);
		}

		public static async Task WaitFor(this TestAppInstance app, Func<bool> condition, string operationName = null, TimeSpan? timeout = null)
		{
			if (condition())
				return;
			var tcs = new TaskCompletionSource<int>();
			using (var subs = app.ModelObjects.ChangeNotification.CreateSubscription(() =>
			{
				if (condition())
					tcs.TrySetResult(0);
			}))
			{
				var delay = Task.Delay(timeout.GetValueOrDefault(TimeSpan.FromSeconds(15)));
				if (await Task.WhenAny(tcs.Task, delay) == delay)
				{
					throw new TimeoutException("Time out waiting for " + (operationName ?? "condition"));
				}
			}
		}

		public static string GetDisplayedLog(this TestAppInstance app)
		{
			var viewLines = app.ViewModel.LoadedMessagesLogViewer.ViewLines;
			var displayedText = string.Join("\n", viewLines.Select(vl => vl.TextLineValue));
			return displayedText;
		}

		public static bool IsLogDisplayed(this TestAppInstance app, string text)
		{
			var displayedText = app.GetDisplayedLog();
			return StringUtils.NormalizeLinebreakes(text) == StringUtils.NormalizeLinebreakes(displayedText);
		}

		public static async Task WaitForLogDisplayed(this TestAppInstance app, string expectedLog, string operationName = null, TimeSpan? timeout = null)
		{
			try
			{
				await app.WaitFor(
					() => app.IsLogDisplayed(expectedLog),
					operationName: operationName,
					timeout: timeout
				);
			}
			catch (TimeoutException)
			{
				Console.WriteLine("Actually displayed log: '{0}'", app.GetDisplayedLog());
				throw;
			}
		}

		public class UtilsImpl : IUtils
		{
			TestAppInstance app;

			public UtilsImpl(TestAppInstance app)
			{
				this.app = app;
			}

			Task IUtils.EmulateFileDragAndDrop(string filePath) => app.EmulateFileDragAndDrop(filePath);
			Task IUtils.EmulateUriDragAndDrop(Uri uri) => app.EmulateUrlDragAndDrop(uri);
			Task IUtils.WaitFor(Func<bool> condition, string operationName, TimeSpan? timeout) => app.WaitFor(condition, operationName, timeout);
		};

		public static IDisposable AutoAcceptPreprocessingUserInteration(this TestAppInstance app)
		{
			UI.Presenters.PreprocessingUserInteractions.DialogViewData acceptedData = null;
			var subs = app.ModelObjects.ChangeNotification.CreateSubscription(() =>
			{
				var currentData = app.ViewModel.PreprocessingUserInteractions.DialogData;
				if (currentData != null && currentData != acceptedData)
				{
					acceptedData = currentData;
					app.ViewModel.PreprocessingUserInteractions.OnCloseDialog(accept: true);
				}
			});
			return subs;
		}
	};
}
