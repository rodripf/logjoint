using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using System.Xml;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Runtime.ConstrainedExecution;
using FCLTraceSource = System.Diagnostics.TraceSource;

namespace LogJoint
{
#if !SILVERLIGHT
	public class LJTraceSource : FCLTraceSource
	{
		public LJTraceSource(string name): this(name, SourceLevels.Off)
		{
		}

		public LJTraceSource(string name, SourceLevels defaultLevel): base(name, defaultLevel)
		{
			autoFrame = new AutoFrame(this);
		}

		public static readonly LJTraceSource EmptyTracer = new LJTraceSource("LogJoint.EmptyTracer");

		public IDisposable NewFrame
		{
			get
			{
#if TRACE
				BeginFrameImpl(null);
#endif
				return autoFrame;
			}
		}

		public IDisposable NewNamedFrame(string nameFormat, params object[] args)
		{
#if TRACE
			BeginFrameImpl(string.Format(nameFormat, args));
#endif
			return autoFrame;
		}

		[Conditional("TRACE")]
		public void BeginFrame()
		{
			this.BeginFrameImpl(null);
		}

		[Conditional("TRACE")]
		public void EndFrame()
		{
			base.TraceEvent(TraceEventType.Stop, 0);
		}

		[Conditional("TRACE")]
		public void Error(string fmt, params object[] args)
		{
			Message(TraceEventType.Error, fmt, args);
		}

		[Conditional("TRACE")]
		public void Error(Exception e, string exceptionContextFmt, params object[] args)
		{
			Message(TraceEventType.Error, e, exceptionContextFmt, args);
		}

		[Conditional("TRACE")]
		public void Message(TraceEventType type, Exception e, string exceptionContextFmt, params object[] args)
		{
			if (Switch.ShouldTrace(type))
			{
				string str;
				if (string.IsNullOrEmpty(exceptionContextFmt))
				{
					str = "Exception has occured";
				}
				else
				{
					str = SafeFormat(exceptionContextFmt, args);
				}
				StringBuilder exceptionStr = new StringBuilder();
				WriteException(e, exceptionStr);
				base.TraceEvent(type, 0, "{0}{1}{2}", str, Environment.NewLine, exceptionStr.ToString());
			}
		}

		[Conditional("TRACE")]
		public void Warning(string fmt, params object[] args)
		{
			this.Message(TraceEventType.Warning, fmt, args);
		}

		[Conditional("TRACE")]
		public void Info(string fmt, params object[] args)
		{
			this.Message(TraceEventType.Information, fmt, args);
		}

		[Conditional("TRACE")]
		public void Message(TraceEventType t, string fmt, params object[] args)
		{
			base.TraceEvent(t, 0, fmt, args);
		}

		static void WriteException(Exception e, StringBuilder writer)
		{
			writer.AppendFormat("{0}: {1}{2}", e.GetType(), e.Message, Environment.NewLine);
			writer.AppendLine(e.StackTrace);
			Exception inner = e.InnerException;
			if (inner != null)
			{
				writer.AppendLine("-------------- Inner exception ------------");
				WriteException(inner, writer);
			}
		}

		void BeginFrameImpl(string name)
		{
			if (!Switch.ShouldTrace(TraceEventType.Start | TraceEventType.Stop))
				return;
			if (name == null)
			{
				MethodBase m = new StackFrame(2).GetMethod();
				base.TraceEvent(TraceEventType.Start, 0,
					m.DeclaringType != null ? "{0}, {1}" : "{0}",
					m, m.DeclaringType);
			}
			else
			{
				base.TraceEvent(TraceEventType.Start, 0, "{0}", name);
			}
		}

		internal static string SafeFormat(string fmt, params object[] args)
		{
			string str;
			if (fmt == null)
			{
				return "<null format string>";
			}
			if (args == null || args.Length == 0)
			{
				return fmt;
			}
			try
			{
				str = string.Format(fmt, args);
			}
			catch (FormatException fe)
			{
				return string.Format("<formatting error: {0}>", fe.Message);
			}
			return str;
		}

		class AutoFrame : IDisposable
		{
			public AutoFrame(LJTraceSource owner)
			{
				this.owner = owner;
			}

			public void Dispose()
			{
				owner.EndFrame();
			}

			readonly LJTraceSource owner;
		}

		AutoFrame autoFrame;
	}

	public class Listener : TextWriterTraceListener
	{
		public Listener(): base()
		{
		}
		public Listener(string fileName)
			: base(fileName)
		{
		}

		public override void Close()
		{
			if (!this.closed)
			{
				this.closed = true;
				if (base.Writer != null)
				{
					base.Writer.Write("<eol/>");
					base.Writer.Flush();
				}
			}
			base.Close();
		}

		public override void Fail(string message, string detailMessage)
		{
			StringBuilder builder = new StringBuilder(message);
			if (detailMessage != null)
			{
				builder.Append(" ");
				builder.Append(detailMessage);
			}
			this.TraceEvent(null, thisSourceName, TraceEventType.Error, 0, builder.ToString());
		}

		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
		{
			TraceEvent(eventCache, source, eventType, id, message, null);
		}

		void WriteMessage(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string fmt, params object[] args)
		{
			ThreadData d = Data;
			XmlWriter writer = d.writer;
			Token t = NewToken();

			writer.WriteStartElement("m");
			writer.WriteAttributeString("d", FormatDate(t.DateIssued));
			writer.WriteAttributeString("t", d.IDStr);
			switch (eventType)
			{
				case TraceEventType.Warning:
					writer.WriteAttributeString("s", "w");
					break;
				case TraceEventType.Error:
				case TraceEventType.Critical:
					writer.WriteAttributeString("s", "e");
					break;
			}
			writer.WriteString(newLine);
			writer.WriteString("\t");
			writer.WriteString(LJTraceSource.SafeFormat(fmt, args));
			writer.WriteString(newLine);
			if ((this.TraceOutputOptions & TraceOptions.Callstack) != 0)
			{
				writer.WriteString(Environment.StackTrace);
				writer.WriteString(newLine);
			}
			if ((this.TraceOutputOptions & TraceOptions.LogicalOperationStack) != 0)
			{
				writer.WriteString("LogicalOperationStack=");
				foreach (string op in Trace.CorrelationManager.LogicalOperationStack)
				{
					writer.WriteString(op);
					writer.WriteString("-");
				}
				writer.WriteString(newLine);
			}
			if ((this.TraceOutputOptions & TraceOptions.Timestamp) != 0)
			{
				writer.WriteString(string.Format("Timestamp={0}", Stopwatch.GetTimestamp()));
				writer.WriteString(newLine);
			}
			writer.WriteEndElement();
			Flush(d, t);
		}

		void WriteEndFrame()
		{
			ThreadData d = Data;
			XmlWriter writer = d.writer;
			Token t = NewToken();

			writer.WriteStartElement("ef");
			writer.WriteAttributeString("d", FormatDate(t.DateIssued));
			writer.WriteAttributeString("t", d.IDStr);
			writer.WriteEndElement();

			Flush(d, t);
		}

		void WriteBeginFrame(string fmt, params object[] args)
		{
			ThreadData d = Data;
			XmlWriter writer = d.writer;
			Token t = NewToken();
			writer.WriteStartElement("f");
			writer.WriteAttributeString("d", FormatDate(t.DateIssued));
			writer.WriteAttributeString("t", d.IDStr);
			writer.WriteString("\n\t");
			writer.WriteString(LJTraceSource.SafeFormat(fmt, args));
			writer.WriteString("\n");
			writer.WriteEndElement();
			Flush(d, t);
		}

		public static string ThreadInfoPrefix
		{
			get { return "Thread info: "; }
		}

		public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string fmt, params object[] args)
		{
			if (base.Filter != null && !base.Filter.ShouldTrace(eventCache, source, eventType, id, fmt, args, null, null))
				return;

			switch (eventType)
			{
				case TraceEventType.Start:
					WriteBeginFrame(fmt, args);
					break;
				case TraceEventType.Stop:
					WriteEndFrame();
					break;
				default:
					WriteMessage(eventCache, source, eventType, id, fmt, args);
					break;
			}
		}

		public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object data)
		{
			WriteMessage(eventCache, source, TraceEventType.Verbose, id, "{0}", data);
		}

		public override void TraceData(TraceEventCache eventCache, string source, TraceEventType eventType, int id, object[] data)
		{
			StringBuilder sb = new StringBuilder();
			foreach (object i in data)
				sb.AppendLine(i.ToString());
			WriteMessage(eventCache, source, TraceEventType.Verbose, id, sb.ToString(), null);
		}

		void Flush(ThreadData d, Token t)
		{
			d.writer.Flush();
			t.Data = d.stream.ToString();
			d.stream.Length = 0;
			this.FlushQueue();
		}

		public static string FormatDate(DateTime d)
		{
			return d.ToString(
				"yyyy-MM-ddTHH:mm:ss.fffffff", 
				System.Globalization.DateTimeFormatInfo.InvariantInfo);
		}

		public static XmlWriterSettings XmlSettings
		{
			get { return settings; }
		}

		static void AppendSeparatorIfNeeded(StringBuilder sb)
		{
			if (sb.Length > 0)
				sb.Append(", ");
		}

		static ThreadData Data
		{
			get
			{
				Thread t = Thread.CurrentThread;
				if (data == null)
				{
					data = new ThreadData();
					data.ID = Interlocked.Increment(ref lastThreadID);
					data.IDStr = data.ID.ToString();
					data.stream = new StringBuilder();
					data.writer = XmlWriter.Create(data.stream, settings);
					XmlWriter writer = data.writer;
					writer.WriteStartElement("m");
					writer.WriteAttributeString("d", FormatDate(DateTime.Now));
					writer.WriteAttributeString("t", data.IDStr);
					StringBuilder threadDesc = new StringBuilder();
					threadDesc.Append("\n\t");
					threadDesc.Append(ThreadInfoPrefix);
					if (!string.IsNullOrEmpty(t.Name))
						threadDesc.AppendFormat("{0}, ", t.Name);
					threadDesc.AppendFormat("ManagedID={0}, ProcessID={1}", t.ManagedThreadId.ToString(),
						Process.GetCurrentProcess().Id);
					if (t.IsThreadPoolThread)
					{
						threadDesc.Append(", threadpool thread");
					}
					if (t.IsBackground)
					{
						threadDesc.Append(", background thread");
					}
					threadDesc.Append("\n");
					writer.WriteString(threadDesc.ToString());
					writer.WriteEndElement();
				}
				return data;
			}
		}

		class ThreadData
		{
			public int ID;
			public string IDStr;
			public StringBuilder stream;
			public XmlWriter writer;
		}

		class Token
		{
			public volatile string Data;
			public readonly DateTime DateIssued;
			public Token()
			{
				DateIssued = DateTime.Now;
			}
		};
		readonly Queue<Token> queue = new Queue<Token>();

		Token NewToken()
		{
			lock (queue)
			{
				Token t = new Token();
				queue.Enqueue(t);
				return t;
			}
		}

		void FlushQueue()
		{
			lock (queue)
			{
				if (this.Writer == null)
					return;
				int written = 0;
				for (; ; )
				{
					if (queue.Count == 0)
						break;
					Token t = queue.Peek();
					if (t.Data == null)
						break;
					++written;
					this.Writer.Write(t.Data);
					queue.Dequeue();
				}
				if (written != 0)
				{
					this.Writer.Flush();
				}
			}
		}

		[ThreadStatic]
		static ThreadData data;
		static int lastThreadID;

		static readonly string newLine = Environment.NewLine;
		static readonly XmlWriterSettings settings = new XmlWriterSettings();
		static string thisSourceName = "LogJoint.Listener";

		bool closed;

		static Listener()
		{
			settings.Indent = true;
			settings.OmitXmlDeclaration = true;
			settings.ConformanceLevel = ConformanceLevel.Fragment;
		}

		public override void Write(string message)
		{
			TraceEvent(null, thisSourceName, TraceEventType.Information, 0, message);
		}

		public override void WriteLine(string message)
		{
			Write(message);
		}
	}
#else
	public class Source
	{
		public static readonly Source EmptyTracer = new Source();

		public void BeginFrame()
		{
		}

		public void EndFrame()
		{
		}

		public void Error(string fmt, params object[] args)
		{
		}

		public void Error(Exception e, string exceptionContextFmt, params object[] args)
		{
		}

		public void Warning(string fmt, params object[] args)
		{
		}

		public void Info(string fmt, params object[] args)
		{
		}

		public IDisposable NewFrame
		{
			get { return null; }
		}

	};
#endif
}
