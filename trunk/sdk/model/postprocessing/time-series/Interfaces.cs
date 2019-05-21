﻿using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using TSBlocks = LogJoint.Analytics.TimeSeries;

namespace LogJoint.Postprocessing.TimeSeries
{
	public interface ITimeSeriesPostprocessorOutput
	{
		ILogSource LogSource { get; }

		/// <summary>
		/// Log display name as a separate property because LogSource may be disposed while
		/// Display name is still required.
		/// </summary>
		string LogDisplayName { get; }

		IEnumerable<TSBlocks.TimeSeriesData> TimeSeries { get; }

		IEnumerable<TSBlocks.EventBase> Events { get; }
	}

	public interface ITimeSeriesVisualizerModel
	{
		ICollection<ITimeSeriesPostprocessorOutput> Outputs { get; }

		event EventHandler Changed;
	};
}
