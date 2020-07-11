using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogJoint;
using LogJoint.Preprocessing;
using LogJoint.Persistence;
using NSubstitute;
using Microsoft.JSInterop;

namespace LogJoint.Wasm
{

	public class ViewModelObjects // todo: renam to ViewProxies
	{
        public UI.LogViewer.ViewProxy LoadedMessagesLogViewerViewProxy = new UI.LogViewer.ViewProxy();
        public LogJoint.UI.Presenters.MainForm.IViewModel MainForm;
		public LogJoint.UI.Presenters.PreprocessingUserInteractions.IViewModel PreprocessingUserInteractions;
		public LogJoint.UI.Presenters.Postprocessing.MainWindowTabPage.IViewModel PostprocessingTabPage;
		public string PostprocessingTabPageId;
		public LogJoint.UI.Presenters.LoadedMessages.IViewModel LoadedMessages;
		public LogJoint.UI.Presenters.LogViewer.IViewModel SearchResultLogViewer;
		public LogJoint.UI.Presenters.MessagePropertiesDialog.IDialogViewModel MessagePropertiesDialog;
		public LogJoint.UI.Presenters.SourcesManager.IViewModel SourcesManager;
		public LogJoint.UI.Presenters.SourcesList.IViewModel SourcesList;
		public LogJoint.UI.Presenters.SourcePropertiesWindow.IViewModel SourcePropertiesWindow;
	};

    class Mocks
    {
        public Preprocessing.ICredentialsCache CredentialsCache;
        public WebViewTools.IWebViewTools WebBrowserDownloader;
        public Persistence.IWebContentCacheConfig WebContentCacheConfig;
        public Preprocessing.ILogsDownloaderConfig LogsDownloaderConfig;

        public LogJoint.UI.Presenters.IClipboardAccess ClipboardAccess;
        public LogJoint.UI.Presenters.IShellOpen ShellOpen;
        public LogJoint.UI.Presenters.IAlertPopup AlertPopup;
        public LogJoint.UI.Presenters.IFileDialogs FileDialogs;
        public LogJoint.UI.Presenters.IPromptDialog PromptDialog;
        public LogJoint.UI.Presenters.About.IAboutConfig AboutConfig;
        public LogJoint.UI.Presenters.MainForm.IDragDropHandler DragDropHandler;
        public LogJoint.UI.Presenters.ISystemThemeDetector SystemThemeDetector;

        public LogJoint.UI.Presenters.Factory.IViewsFactory Views;

        public Mocks(ViewModelObjects viewModel)
        {
            CredentialsCache = Substitute.For<Preprocessing.ICredentialsCache>();
            WebBrowserDownloader = Substitute.For<WebViewTools.IWebViewTools>();
            WebContentCacheConfig = Substitute.For<Persistence.IWebContentCacheConfig>();
            LogsDownloaderConfig = Substitute.For<Preprocessing.ILogsDownloaderConfig>();

            ClipboardAccess = Substitute.For<LogJoint.UI.Presenters.IClipboardAccess>();
            ShellOpen = Substitute.For<LogJoint.UI.Presenters.IShellOpen>();
            AlertPopup = Substitute.For<LogJoint.UI.Presenters.IAlertPopup>();
            FileDialogs = Substitute.For<LogJoint.UI.Presenters.IFileDialogs>();
            PromptDialog = Substitute.For<LogJoint.UI.Presenters.IPromptDialog>();
            AboutConfig = Substitute.For<LogJoint.UI.Presenters.About.IAboutConfig>();
            DragDropHandler = Substitute.For<LogJoint.UI.Presenters.MainForm.IDragDropHandler>();
            SystemThemeDetector = Substitute.For<LogJoint.UI.Presenters.ISystemThemeDetector>();
            Views = Substitute.For<LogJoint.UI.Presenters.Factory.IViewsFactory>();

            Views.CreateLoadedMessagesView().MessagesView.Returns(viewModel.LoadedMessagesLogViewerViewProxy);
			Views.CreateSourcesManagerView().SetViewModel(
				Arg.Do<LogJoint.UI.Presenters.SourcesManager.IViewModel>(x => viewModel.SourcesManager = x));
			Views.CreateSourcesListView().SetViewModel(
				Arg.Do<LogJoint.UI.Presenters.SourcesList.IViewModel>(x => viewModel.SourcesList = x));
        }
    };
}

namespace LogJoint.Wasm
{
    public class Program
    {
        class BlazorSynchronizationContext: ISynchronizationContext
        {
            void ISynchronizationContext.Post(Action action)
            {
                System.Threading.ThreadPool.QueueUserWorkItem(_ => action(), null);
            }
        };

        class WebContentConfig : IWebContentCacheConfig, ILogsDownloaderConfig
        {
            bool IWebContentCacheConfig.IsCachingForcedForHost(string hostName) => false;
            LogDownloaderRule ILogsDownloaderConfig.GetLogDownloaderConfig(Uri forUri) => null;
            void ILogsDownloaderConfig.AddRule(Uri uri, LogDownloaderRule rule) {}
        };


        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddSingleton<ModelObjects>(serviceProvider =>
            {
                ISynchronizationContext invokingSynchronization = new BlazorSynchronizationContext();
                WebContentConfig webContentConfig = new WebContentConfig();

                var model = ModelFactory.Create(
                    new ModelConfig
                    {
                        WorkspacesUrl = "",
                        TelemetryUrl = "",
                        IssuesUrl = "",
                        AutoUpdateUrl = "",
                        PluginsUrl = "",
                        WebContentCacheConfig = webContentConfig,
                        LogsDownloaderConfig = webContentConfig,
                        TraceListeners = new[] { new TraceListener(";console=1") },
                        FormatsRepositoryAssembly = System.Reflection.Assembly.GetExecutingAssembly(),
                        FileSystem = new LogJoint.Wasm.FileSystem(serviceProvider.GetService<IJSRuntime>())
                    },
                        invokingSynchronization,
                        (storageManager) => null /*new PreprocessingCredentialsCache (
                        mainWindow.Window,
                        storageManager.GlobalSettingsEntry,
                        invokingSynchronization
                    )*/,
                        (shutdown, webContentCache, traceSourceFactory) => null /*new Presenters.WebViewTools.Presenter (
                        new WebBrowserDownloaderWindowController (),
                        invokingSynchronization,
                        webContentCache,
                        shutdown,
                        traceSourceFactory
                    )*/,
                    null/*new Drawing.Matrix.Factory()*/,
                    LogJoint.RegularExpressions.FCLRegexFactory.Instance
                );
                return model;
            });
            builder.Services.AddSingleton<ViewModelObjects>(serviceProvider =>
            {
                var model = serviceProvider.GetService<ModelObjects>();

                var viewModel = new ViewModelObjects();
                var mocks = new Mocks(viewModel);

                var presentationObjects = LogJoint.UI.Presenters.Factory.Create(
                    model,
                    mocks.ClipboardAccess,
                    mocks.ShellOpen,
                    mocks.AlertPopup,
                    mocks.FileDialogs,
                    mocks.PromptDialog,
                    mocks.AboutConfig,
                    mocks.DragDropHandler,
                    mocks.SystemThemeDetector,
                    mocks.Views
                );

                return viewModel;
            });

            await builder.Build().RunAsync();
        }
    }
}
