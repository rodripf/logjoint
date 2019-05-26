﻿using LogJoint.Postprocessing;
using LogJoint.Postprocessing.Timeline;
using System.Threading;
using System.Threading.Tasks;
using SVC = LogJoint.Symphony.SpringServiceLog;
using Sym = LogJoint.Symphony.Rtc;

namespace LogJoint.Symphony.Timeline
{
	public interface IPostprocessorsFactory
	{
		ILogSourcePostprocessor CreateSpringServiceLogPostprocessor();
		ILogSourcePostprocessor CreateSymRtcPostprocessor();
	};

	public class PostprocessorsFactory : IPostprocessorsFactory
	{
		readonly ITempFilesManager tempFiles;
		readonly Postprocessing.IModel postprocessing;

		public PostprocessorsFactory(
			ITempFilesManager tempFiles,
			Postprocessing.IModel postprocessing)
		{
			this.tempFiles = tempFiles;
			this.postprocessing = postprocessing;
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateSpringServiceLogPostprocessor()
		{
			return new LogSourcePostprocessor(
				PostprocessorKind.Timeline,
				i => RunForSpringServiceLog(new SVC.Reader(postprocessing.TextLogParser, i.CancellationToken).Read(
					i.LogFileName, i.ProgressHandler),
					i.OutputFileName, i.CancellationToken, i.TemplatesTracker,
					i.InputContentsEtag)
			);
		}

		ILogSourcePostprocessor IPostprocessorsFactory.CreateSymRtcPostprocessor()
		{
			return new LogSourcePostprocessor(
				PostprocessorKind.Timeline,
				i => RunForSymLog(new Sym.Reader(postprocessing.TextLogParser, i.CancellationToken).Read(i.LogFileName, i.ProgressHandler), i)
			);
		}

		async Task RunForSpringServiceLog(
			IEnumerableAsync<SVC.Message[]> input,
			string outputFileName,
			CancellationToken cancellation,
			ICodepathTracker templatesTracker,
			string contentsEtagAttr
		)
		{
/*			IPrefixMatcher matcher = postprocessing.CreatePrefixMatcher();
			var inputMultiplexed = input.Multiplex();
			var symEvents = RunForSymMessages(matcher, inputMultiplexed, templatesTracker, out var symLog);
			var endOfTimelineEventSource = new GenericEndOfTimelineEventSource<Sym.Message>();
			var eofEvts = endOfTimelineEventSource.GetEvents(inputMultiplexed);

			matcher.Freeze();

			var events = EnumerableAsync.Merge(
				symEvents,
				eofEvts
			);

			var serialize = TimelinePostprocessorOutput.SerializePostprocessorOutput(
				events,
				null,
				evtTrigger => TextLogEventTrigger.Make((Sym.Message)evtTrigger),
				contentsEtagAttr,
				outputFileName,
				tempFiles,
				cancellation
			);

			await Task.WhenAll(serialize, symLog.Open(), inputMultiplexed.Open());*/
		}

		static IEnumerableAsync<Event[]> TrackTemplates(IEnumerableAsync<Event[]> events, ICodepathTracker codepathTracker)
		{
			return events.Select(batch =>
			{
				if (codepathTracker != null)
					foreach (var e in batch)
						codepathTracker.RegisterUsage(e.TemplateId);
				return batch;
			});
		}

		async Task RunForSymLog(
			IEnumerableAsync<Sym.Message[]> input,
			LogSourcePostprocessorInput postprocessorInput
		)
		{
			IPrefixMatcher matcher = postprocessing.CreatePrefixMatcher();
			var inputMultiplexed = input.Multiplex();
			var symEvents = RunForSymMessages(matcher, inputMultiplexed, postprocessorInput.TemplatesTracker, out var symLog);
			var endOfTimelineEventSource = postprocessing.Timeline.CreateEndOfTimelineEventSource<Sym.Message>();
			var eofEvts = endOfTimelineEventSource.GetEvents(inputMultiplexed);

			matcher.Freeze();

			var events = EnumerableAsync.Merge(
				symEvents,
				eofEvts
			);

			var serialize = postprocessing.Timeline.SavePostprocessorOutput(
				events,
				null,
				evtTrigger => TextLogEventTrigger.Make((Sym.Message)evtTrigger),
				postprocessorInput
			);

			await Task.WhenAll(serialize, symLog.Open(), inputMultiplexed.Open());
		}

		private IEnumerableAsync<Event[]> RunForSymMessages(
			IPrefixMatcher matcher,
			IEnumerableAsync<Sym.Message[]> messages,
			ICodepathTracker templatesTracker,
			out IMultiplexingEnumerable<Sym.MessagePrefixesPair[]> symLog
		)
		{
			Sym.IMeetingsStateInspector symMeetingsStateInspector = new Sym.MeetingsStateInspector(matcher);
			Sym.IMediaStateInspector symMediaStateInsector = new Sym.MediaStateInspector(matcher, symMeetingsStateInspector);
			Sym.ITimelineEvents symTimelineEvents = new Sym.TimelineEvents(matcher);
			Sym.Diag.ITimelineEvents diagTimelineEvents = new Sym.Diag.TimelineEvents(matcher);

			symLog = Sym.Helpers.MatchPrefixes(messages, matcher).Multiplex();
			var symMeetingStateEvents = symMeetingsStateInspector.GetEvents(symLog);
			var symMediaStateEvents = symMediaStateInsector.GetEvents(symLog);

			var symMeetingEvents = postprocessing.Timeline.CreateInspectedObjectsLifetimeEventsSource(e =>
				e.ObjectType == Sym.MeetingsStateInspector.MeetingTypeInfo
			 || e.ObjectType == Sym.MeetingsStateInspector.MeetingSessionTypeInfo
			 || e.ObjectType == Sym.MeetingsStateInspector.MeetingRemoteParticipantTypeInfo
			 || e.ObjectType == Sym.MeetingsStateInspector.ProbeSessionTypeInfo
			 || e.ObjectType == Sym.MeetingsStateInspector.InvitationTypeInfo
			).GetEvents(symMeetingStateEvents);

			var symMediaEvents = postprocessing.Timeline.CreateInspectedObjectsLifetimeEventsSource(e =>
			   	e.ObjectType == Sym.MediaStateInspector.LocalScreenTypeInfo
			 || e.ObjectType == Sym.MediaStateInspector.LocalAudioTypeInfo
			 || e.ObjectType == Sym.MediaStateInspector.LocalVideoTypeInfo
			 || e.ObjectType == Sym.MediaStateInspector.TestSessionTypeInfo
			).GetEvents(symMediaStateEvents);

			var events = TrackTemplates(EnumerableAsync.Merge(
				symMeetingEvents,
				symMediaEvents,
				symTimelineEvents.GetEvents(symLog),
				diagTimelineEvents.GetEvents(symLog)
			), templatesTracker);

			return events;
		}
	};
}
