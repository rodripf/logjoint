using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace LogJoint
{
	public class TempFilesManager: ITempFilesManager
	{
		public TempFilesManager()
		{
			var tracer = new LJTraceSource("App", "tmp");
			using (tracer.NewFrame)
			{
#if !SILVERLIGHT
				folder = Path.Combine(Path.GetTempPath(), "LogJoint");
#else
#endif
				bool thisIsTheOnlyInstance = false;
				runningInstanceMutex = new Mutex(true, "LogJoint/TempFilesManager", out thisIsTheOnlyInstance);

				tracer.Info("Temp directory: {0}", folder);
				
				if (!Directory.Exists(folder))
				{
					tracer.Info("Temp directory doesn't exist. Creating it.");
					Directory.CreateDirectory(folder);
				}
				else
				{
					if (!thisIsTheOnlyInstance)
					{
						tracer.Info("Temp directory exists and I am NOT the only instance in the system. Skipping temp cleanup.");
					}
					else
					{
						tracer.Info("Temp directory exists. Deleting it first.");
						try
						{
							Directory.Delete(folder, true);
						}
						catch (Exception e)
						{
							tracer.Error(e, "Failed to delete tempdir");
						}
					}
					tracer.Info("Creating temp directory.");
					Directory.CreateDirectory(folder);
				}
			}
		}

		public void Dispose()
		{
			runningInstanceMutex.Dispose ();
		}

		public string GenerateNewName()
		{
			string fname = Path.Combine(folder, Guid.NewGuid().ToString() + ".tmp");
			return fname;
		}

		public bool IsTemporaryFile(string filePath)
		{
			return string.Compare(Path.GetDirectoryName(filePath), folder, true) == 0;
		}

		readonly string folder;
		readonly Mutex runningInstanceMutex;
		static TempFilesManager instance;
	}

	public class TempFilesCleanupList : ITempFilesCleanupList
	{
		readonly ITempFilesManager tempFiles;
		readonly object sync = new object();
		readonly List<string> files = new List<string>();
		bool disposed;

		public TempFilesCleanupList(ITempFilesManager tempFiles)
		{
			this.tempFiles = tempFiles;
		}

		void ITempFilesCleanupList.Add(string fileName)
		{
			if (!tempFiles.IsTemporaryFile(fileName))
				return;
			lock (sync)
			{
				if (disposed)
					throw new ObjectDisposedException("ScopedTempFilesCollection");
				files.Add(fileName);
			}
		}

		void IDisposable.Dispose()
		{
			lock (sync)
			{
				if (disposed)
					return;
				disposed = true;
			}
			files.ForEach(f => File.Delete(f));
			files.Clear();
		}
	};
}