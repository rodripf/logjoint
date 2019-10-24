﻿using System;
using System.Linq;
using NSubstitute;
using NUnit.Framework;
using LogJoint.Postprocessing;
using LogJoint.Postprocessing.Correlation;
using System.Xml;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LogJoint.Tests.Postprocessing.PostprocessorsManager
{
	[TestFixture]
	public class PostprocessorsManagerTests
	{
		IManagerInternal manager;

		ILogSourcesManager logSources;
		Telemetry.ITelemetryCollector telemetry;
		ManualSynchronizationContext mockedSyncContext;
		IHeartBeatTimer heartbeat;
		Progress.IProgressAggregator progressAggregator;
		Settings.IGlobalSettingsAccessor settingsAccessor;

		ILogProviderFactory logProviderFac1;
		ILogSource logSource1;
		ILogSourcePostprocessor logSourcePP1;
		Persistence.ISaxXMLStorageSection pp1outputXmlSection;
		IPostprocessorOutputETag pp1PostprocessorOutput;
		IPostprocessorRunSummary pp1RunSummary;
		IOutputDataDeserializer outputDataDeserializer;
		ILogPartTokenFactories logPartTokenFactories;
		ISameNodeDetectionTokenFactories sameNodeDetectionTokenFactories;
		IChangeNotification changeNotification;


		[SetUp]
		public void BeforeEach()
		{
			logSources = Substitute.For<ILogSourcesManager>();
			telemetry = Substitute.For<Telemetry.ITelemetryCollector>();
			mockedSyncContext = new ManualSynchronizationContext();
			heartbeat = Substitute.For<IHeartBeatTimer>();
			progressAggregator = Substitute.For<Progress.IProgressAggregator>();
			settingsAccessor = Substitute.For<Settings.IGlobalSettingsAccessor>();
			logSource1 = Substitute.For<ILogSource>();
			logProviderFac1 = Substitute.For<ILogProviderFactory>();
			logSource1.Provider.Factory.Returns(logProviderFac1);
			logSource1.Provider.ConnectionParams.Returns(new ConnectionParams($"{ConnectionParamsKeys.PathConnectionParam}=/log.txt"));
			logSource1.Provider.Stats.Returns(new LogProviderStats()
			{
				ContentsEtag = null
			});
			logSourcePP1 = Substitute.For<ILogSourcePostprocessor>();
			logSourcePP1.Kind.Returns(PostprocessorKind.SequenceDiagram);
			pp1outputXmlSection = Substitute.For<Persistence.ISaxXMLStorageSection>();
			logSource1.LogSourceSpecificStorageEntry.OpenSaxXMLSection("postproc-sequencediagram.xml", Persistence.StorageSectionOpenFlag.ReadOnly).Returns(pp1outputXmlSection);
			pp1outputXmlSection.Reader.Returns(Substitute.For<XmlReader>());
			pp1PostprocessorOutput = Substitute.For<IPostprocessorOutputETag>();
			outputDataDeserializer = Substitute.For<IOutputDataDeserializer>();
			outputDataDeserializer.Deserialize(PostprocessorKind.SequenceDiagram, Arg.Any<LogSourcePostprocessorDeserializationParams>()).Returns(pp1PostprocessorOutput);
			pp1RunSummary = Substitute.For<IPostprocessorRunSummary>();
			logSourcePP1.Run(null).ReturnsForAnyArgs(Task.FromResult(pp1RunSummary));
			pp1RunSummary.GetLogSpecificSummary(null).ReturnsForAnyArgs((IPostprocessorRunSummary)null);
			logPartTokenFactories = Substitute.For<ILogPartTokenFactories>();
			sameNodeDetectionTokenFactories = Substitute.For<ISameNodeDetectionTokenFactories>();
			changeNotification = Substitute.For<IChangeNotification>();

			manager = new LogJoint.Postprocessing.PostprocessorsManager(
				logSources, telemetry, mockedSyncContext, mockedSyncContext, heartbeat, progressAggregator, settingsAccessor, outputDataDeserializer, new TraceSourceFactory(),
				logPartTokenFactories, sameNodeDetectionTokenFactories, changeNotification);

			manager.Register(new LogSourceMetadata(logProviderFac1, logSourcePP1));
		}

		private void EmitTimerUpdate()
		{
			heartbeat.OnTimer += Raise.EventWith(heartbeat, new HeartBeatEventArgs(HeartBeatEventType.NormalUpdate));
		}

		private void EmitLogSource1Addition()
		{
			logSources.Items.Returns(new[] { logSource1 });
			logSources.OnLogSourceAdded += Raise.EventWith(logSources, EventArgs.Empty);
			EmitTimerUpdate();
		}

		[Test]
		public void CanRunPostprocessingIfItWasNotRunBefore()
		{
			pp1outputXmlSection.Reader.Returns((XmlReader)null);
			EmitLogSource1Addition();
			mockedSyncContext.Deplete();

			var exposedOutput = manager.LogSourcePostprocessors.Single();
			Assert.AreSame(null, exposedOutput.OutputData);
			Assert.AreEqual(LogSourcePostprocessorState.Status.NeverRun, exposedOutput.OutputStatus);
			Assert.AreSame(null, exposedOutput.LastRunSummary);
			Assert.AreSame(new double?(), exposedOutput.Progress);
			Assert.AreEqual(logSource1, exposedOutput.LogSource);
			Assert.AreEqual(logSourcePP1, exposedOutput.Postprocessor);


			var pp1runResult = new TaskCompletionSource<IPostprocessorRunSummary>();
			logSourcePP1.Run(null).ReturnsForAnyArgs(pp1runResult.Task);

			Task runTask = manager.RunPostprocessors(
				new[] { exposedOutput }, null);
			mockedSyncContext.Deplete();
			Assert.IsFalse(runTask.IsCompleted);

			exposedOutput = manager.LogSourcePostprocessors.Single();
			Assert.AreSame(null, exposedOutput.OutputData);
			Assert.AreEqual(LogSourcePostprocessorState.Status.InProgress, exposedOutput.OutputStatus);
			Assert.AreSame(null, exposedOutput.LastRunSummary);
			Assert.AreSame(new double?(), exposedOutput.Progress);
			Assert.AreEqual(logSource1, exposedOutput.LogSource);
			Assert.AreEqual(logSourcePP1, exposedOutput.Postprocessor);


			pp1outputXmlSection.Reader.Returns(Substitute.For<XmlReader>());
			pp1runResult.SetResult(pp1RunSummary);
			mockedSyncContext.Deplete();
			Assert.IsTrue(runTask.IsCompleted);

			exposedOutput = manager.LogSourcePostprocessors.Single();
			Assert.AreEqual(LogSourcePostprocessorState.Status.Finished, exposedOutput.OutputStatus);
			Assert.AreSame(pp1PostprocessorOutput, exposedOutput.OutputData);
			Assert.AreSame(pp1RunSummary, exposedOutput.LastRunSummary);
			Assert.AreSame(new double?(), exposedOutput.Progress);
			Assert.AreEqual(logSource1, exposedOutput.LogSource);
			Assert.AreEqual(logSourcePP1, exposedOutput.Postprocessor);
		}

		[Test]
		public void InitialChangeOfETagShouldNotTriggerPostprocessorOutputReload()
		{
			EmitLogSource1Addition();
			mockedSyncContext.Deplete();

			logSource1.Provider.Stats.Returns(new LogProviderStats() { ContentsEtag = 123 });
			logSources.OnLogSourceStatsChanged += Raise.EventWith(logSource1,
				new LogSourceStatsEventArgs(logSource1.Provider.Stats, null, LogProviderStatsFlag.ContentsEtag));
			mockedSyncContext.Deplete();

			outputDataDeserializer.Received(1).Deserialize(PostprocessorKind.SequenceDiagram, Arg.Any<LogSourcePostprocessorDeserializationParams>());
		}
	}
}
