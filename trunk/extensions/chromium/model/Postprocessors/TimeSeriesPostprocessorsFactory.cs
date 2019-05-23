﻿using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using LogJoint.Postprocessing;
using LogJoint.Analytics;
using LogJoint.Analytics.TimeSeries;
using LogJoint.Postprocessing.TimeSeries;
using CDL = LogJoint.Chromium.ChromeDebugLog;
using DMP = LogJoint.Chromium.WebrtcInternalsDump;
using Sym = LogJoint.Symphony.Rtc;
using System.Xml;

namespace LogJoint.Chromium.TimeSeries
{
	public interface IPostprocessorsFactory
	{
		ILogSourcePostprocessor CreateChromeDebugPostprocessor();
		ILogSourcePostprocessor CreateWebRtcInternalsDumpPostprocessor();
		ILogSourcePostprocessor CreateSymphonyRtcPostprocessor();
	};

	public class PostprocessorsFactory : IPostprocessorsFactory
	{
		readonly ITimeSeriesTypesAccess timeSeriesTypesAccess;

		public PostprocessorsFactory(ITimeSeriesTypesAccess timeSeriesTypesAccess)
		{
			this.timeSeriesTypesAccess = timeSeriesTypesAccess;
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateChromeDebugPostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				PostprocessorKind.TimeSeries,
				i => RunForWebRtcNativeLogMessages(new CDL.Reader(i.CancellationToken).Read(
					i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler),
					i.OutputFileName, i.CancellationToken, i.TemplatesTracker,
					i.InputContentsEtag)
			);
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateWebRtcInternalsDumpPostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				PostprocessorKind.TimeSeries,
				i => RunForWebRtcInternalsDump(new DMP.Reader(i.CancellationToken).Read(
					i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler), 
					i.OutputFileName, i.CancellationToken, i.TemplatesTracker,
					i.InputContentsEtag)
			);
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateSymphonyRtcPostprocessor()
		{
			return new LogSourcePostprocessorImpl(
				PostprocessorKind.TimeSeries,
				i => RunForSymphonyRtc(new Sym.Reader(i.CancellationToken).Read(
					i.LogFileName, i.GetLogFileNameHint(), i.ProgressHandler),
					i.OutputFileName, i.CancellationToken, i.TemplatesTracker,
					i.InputContentsEtag)
			);
		}

		async Task RunForWebRtcNativeLogMessages(
			IEnumerableAsync<CDL.Message[]> input,
			string outputFileName, 
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			string contentsEtagAttr
		)
		{
			timeSeriesTypesAccess.CheckForCustomConfigUpdate();

			var inputMultiplexed = input.Multiplex();
			var symMessages = (new Sym.Reader()).FromChromeDebugLog(inputMultiplexed);

			ICombinedParser parser = new TimeSeriesCombinedParser(timeSeriesTypesAccess.GetMetadataTypes());

			var feedNativeEvents = parser.FeedLogMessages(inputMultiplexed);
			var feedSymEvents = parser.FeedLogMessages(symMessages, m => m.Logger, m => string.Format("{0}.{1}", m.Logger, m.Text));

			await Task.WhenAll(feedNativeEvents, feedSymEvents, inputMultiplexed.Open());

			foreach (var ts in parser.GetParsedTimeSeries())
			{
				ts.DataPoints = Analytics.TimeSeries.Filters.RemoveRepeatedValues.Filter(ts.DataPoints).ToList();
			}

			TimeSeriesPostprocessorOutput.SerializePostprocessorOutput(
				parser.GetParsedTimeSeries(),
				parser.GetParsedEvents(),
				outputFileName,
				timeSeriesTypesAccess);
		}

		async Task RunForWebRtcInternalsDump(
			IEnumerableAsync<DMP.Message[]> input,
			string outputFileName,
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			string contentsEtagAttr
		)
		{
			timeSeriesTypesAccess.CheckForCustomConfigUpdate();

			ICombinedParser parser = new TimeSeriesCombinedParser(timeSeriesTypesAccess.GetMetadataTypes());

			await parser.FeedLogMessages(input, m => m.ObjectId, m => m.Text);

			TimeSeriesPostprocessorOutput.SerializePostprocessorOutput(
				parser.GetParsedTimeSeries(),
				parser.GetParsedEvents(),
				outputFileName,
				timeSeriesTypesAccess);
		}

		async Task RunForSymphonyRtc(
			IEnumerableAsync<Sym.Message[]> input,
			string outputFileName,
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			string contentsEtagAttr
		)
		{
			timeSeriesTypesAccess.CheckForCustomConfigUpdate();

			ICombinedParser parser = new TimeSeriesCombinedParser(timeSeriesTypesAccess.GetMetadataTypes());

			await parser.FeedLogMessages(input, m => m.Logger, m => string.Format("{0}.{1}", m.Logger, m.Text));

			TimeSeriesPostprocessorOutput.SerializePostprocessorOutput(
				parser.GetParsedTimeSeries(),
				parser.GetParsedEvents(),
				outputFileName,
				timeSeriesTypesAccess);
		}
	};
}
