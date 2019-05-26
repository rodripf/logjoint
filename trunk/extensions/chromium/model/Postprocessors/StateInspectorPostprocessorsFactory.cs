﻿using System.Threading.Tasks;
using System.Linq;
using LogJoint.Postprocessing;
using CDL = LogJoint.Chromium.ChromeDebugLog;
using WRD = LogJoint.Chromium.WebrtcInternalsDump;
using LogJoint.Postprocessing.StateInspector;
using System.Threading;
using System.Collections.Generic;

namespace LogJoint.Chromium.StateInspector
{
	public interface IPostprocessorsFactory
	{
		ILogSourcePostprocessor CreateChromeDebugPostprocessor();
		ILogSourcePostprocessor CreateWebRtcInternalsDumpPostprocessor();
	};

	public class PostprocessorsFactory : IPostprocessorsFactory
	{
		private readonly Postprocessing.IModel postprocessing;
		private readonly PluginModel pluginModel;

		public PostprocessorsFactory(
			Postprocessing.IModel postprocessing,
			PluginModel pluginModel)
		{
			this.postprocessing = postprocessing;
			this.pluginModel = pluginModel;
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateChromeDebugPostprocessor()
		{
			return new LogSourcePostprocessor(
				PostprocessorKind.StateInspector,
				i => RunForChromeDebug(new CDL.Reader(postprocessing.TextLogParser, i.CancellationToken).Read(i.LogFileName, i.ProgressHandler), i)
			);
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateWebRtcInternalsDumpPostprocessor()
		{
			return new LogSourcePostprocessor(
				PostprocessorKind.StateInspector,
				i => RunForWebRTCDump(new WRD.Reader(postprocessing.TextLogParser, i.CancellationToken).Read(i.LogFileName, i.ProgressHandler), i)
			);
		}

		static IEnumerableAsync<Event[]> TrackTemplates(IEnumerableAsync<Event[]> events, ICodepathTracker codepathTracker) // todo: have extesnion in sdk
		{
			return events.Select(batch =>
			{
				if (codepathTracker != null)
					foreach (var e in batch)
						codepathTracker.RegisterUsage(e.TemplateId);
				return batch;
			});
		}

		async Task RunForChromeDebug(
			IEnumerableAsync<CDL.Message[]> inputMessages,
			LogSourcePostprocessorInput postprocessorInput
		)
		{
			var inputMultiplexed = inputMessages.Multiplex();

			IPrefixMatcher matcher = postprocessing.CreatePrefixMatcher();
			var logMessages = CDL.Helpers.MatchPrefixes(inputMultiplexed, matcher).Multiplex();

			CDL.IWebRtcStateInspector webRtcStateInspector = new CDL.WebRtcStateInspector(matcher);

			var webRtcEvts = webRtcStateInspector.GetEvents(logMessages);

			var extensionSources = pluginModel
				.ChromeDebugStateEventSources.Select(
					source => source(matcher, inputMultiplexed)
				)
				.ToArray();

			var eventSources = new List<IEnumerableAsync<Event[]>>
			{
				webRtcEvts
			};
			eventSources.AddRange(extensionSources.Select(s => s.Events));

			matcher.Freeze();

			var events = TrackTemplates(
				EnumerableAsync.Merge(eventSources.ToArray()),
				postprocessorInput.TemplatesTracker
			);

			var serialize = postprocessing.StateInspector.SavePostprocessorOutput(
				events,
				null,
				evtTrigger => TextLogEventTrigger.FromUnknownTrigger(evtTrigger),
				postprocessorInput
			);

			var tasks = new List<Task>();
			tasks.Add(serialize);
			tasks.AddRange(extensionSources.SelectMany(s => s.MultiplexingEnumerables.Select(e => e.Open())));
			tasks.Add(logMessages.Open());
			tasks.Add(inputMultiplexed.Open());
			await Task.WhenAll(tasks);
		}

		async Task RunForWebRTCDump(
			IEnumerableAsync<WRD.Message[]> inputMessages,
			LogSourcePostprocessorInput postprocessorInput
		)
		{
			IPrefixMatcher matcher = postprocessing.CreatePrefixMatcher();
			var logMessages = WRD.Helpers.MatchPrefixes(inputMessages, matcher).Multiplex();

			WRD.IWebRtcStateInspector webRtcStateInspector = new WRD.WebRtcStateInspector(matcher);

			var webRtcEvts = webRtcStateInspector.GetEvents(logMessages);

			matcher.Freeze();

			var events = TrackTemplates(EnumerableAsync.Merge(
				webRtcEvts
			), postprocessorInput.TemplatesTracker);

			var serialize = postprocessing.StateInspector.SavePostprocessorOutput(
				events,
				null,
				evtTrigger => TextLogEventTrigger.Make((WRD.Message)evtTrigger),
				postprocessorInput
			);
			await Task.WhenAll(serialize, logMessages.Open());
		}
	};

}
