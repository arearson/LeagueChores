using LeagueChores.Windows;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Windows.Forms;
using Serilog;
using System.IO;
using System.Threading.Tasks;

namespace LeagueChores
{
	internal static class Program
	{
		public static readonly string applicationName = "LeagueChores";
		public static readonly string logFileName = "LeagueChores_.log";

#if DEBUG
		public static readonly bool isDebug = true;
#else
		public static readonly bool isDebug = false;
#endif

		static NotifyIcon m_trayIcon;
		static RegistryKey m_bootKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
		static ToolStripMenuItem m_startOnBootMenuItem;
		static SettingsWindow m_settingsWindow = null;

		// Chores
		public static LootChore lootChore { get; private set; }
		public static HotkeyChore hotkeyChore { get; private set; }
		public static ChampSelectChore champSelectChore { get; private set; }

		[STAThread]
		static void Main()
		{
			try
			{
				using (new SingleGlobalInstance(500))
				{
					SetupLogger();

					Log.Information("Registering chores..");
					RegisterChores();

					Log.Information("Registering windows..");
					RegisterSettingsWindows();

					Log.Information("Creating tray icon.");
					CreateTrayIcon();

					Log.Information("Adding LCU listeners..");
					LCU.onValid += (s, e) => OnConnected();
					if (LCU.isValid)
						OnConnected();

					// By default only show settings once in release mode, and always in debug
					bool shouldOpenSettingsMenu = Settings.File.data.openSettingsMenuOnStart || isDebug || Settings.File.isFirstRun;
					if (shouldOpenSettingsMenu)
						ShowSettings();

					Application.Run();
					Log.CloseAndFlush();
				}
			}
			catch (ApplicationAlreadyRunningException)
			{
				MessageBox.Show($"{applicationName} is already running!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				// TODO: bring settings window to front instead
			}
		}

		private static void RegisterChores()
		{
			lootChore = new LootChore();
			hotkeyChore = new HotkeyChore();
			champSelectChore = new ChampSelectChore();
		}

		private static void RegisterSettingsWindows()
		{
			SettingsWindow.RegisterGeneralHandler<ApplicationSettingsWindowHandler>(); // This will do for now
			// SettingsWindow.RegisterSummonerHandler<SummonerSettingsWindowHandler>();

			SettingsWindow.RegisterHandler<ApplicationSettingsWindowHandler>();
			SettingsWindow.RegisterHandler<Chores.ChampSelect.ChampSelectSettingsWindowHandler>();
			SettingsWindow.RegisterHandler<Chores.Loot.LootSettingsWindowHandler>();
#if ENABLE_HOTKEYS
			SettingsWindow.RegisterHandler<Chores.HotkeySettingsWindowHandler>();
#endif
		}

		static void OnConnected()
		{
			if (Settings.File.data.showConnectedNotification == false)
				return;

			ShowBalloon($"{applicationName} is now connected to League of Legends.");
		}

		static async void SetupLogger()
		{
			Log.Logger = new LoggerConfiguration()
				.MinimumLevel.Information()
				.WriteTo.Console()
				.WriteTo.File(logFileName, rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true)
				.CreateLogger();
		}

		static void CreateTrayIcon()
		{
			m_trayIcon = new NotifyIcon
			{
				Icon = Resources.PoroIcon,
				Visible = true,
				BalloonTipTitle = applicationName
			};
			m_trayIcon.ContextMenuStrip = new ContextMenuStrip();

			SyncBootKey();

			var showItems = new ToolStripMenuItem("Show Settings");
			showItems.Click += delegate { ShowSettings(); };
			m_trayIcon.ContextMenuStrip.Items.Add(showItems);

			m_startOnBootMenuItem = new ToolStripMenuItem("Start with Windows");
			m_startOnBootMenuItem.Click += (s, e) => willStartWithWindows = !willStartWithWindows;
			m_startOnBootMenuItem.Checked = willStartWithWindows;
			m_trayIcon.ContextMenuStrip.Items.Add(m_startOnBootMenuItem);

			ToolStripMenuItem quitMenuItem = new ToolStripMenuItem("Quit");
			quitMenuItem.Click += (a, b) =>
			{
				m_trayIcon.Dispose();
				m_trayIcon = null;

				Log.CloseAndFlush();
				Application.Exit();
				Environment.Exit(1);
			};
			m_trayIcon.ContextMenuStrip.Items.Add(quitMenuItem);

			m_trayIcon.DoubleClick += (s, e) => ShowSettings();
		}

		public static bool willStartWithWindows 
		{ 
			get { return m_bootKey.GetValue(applicationName) != null; }
			set
			{
#if !DEBUG
				if (value == willStartWithWindows)
					return;

				if (value) 
				{ 
					m_bootKey.SetValue(applicationName, Application.ExecutablePath); 
					m_startOnBootMenuItem.Checked = value;
					Settings.File.data.startWithWindows = value;
				}
				else 
				{ 
					m_bootKey.DeleteValue(applicationName, false); 
					m_startOnBootMenuItem.Checked = value;
					Settings.File.data.startWithWindows = value;
				}
				ShowBalloon($"{applicationName} {(value ? "will now" : "won't")} start with Windows from now on.");
#endif
			}
		}

		public static void SyncBootKey()
		{
#if !DEBUG
			if (Settings.File.data.startWithWindows == willStartWithWindows)
				return;

			willStartWithWindows = Settings.File.data.startWithWindows; // Ensure that the settings match the boot status
			ShowBalloon($"{applicationName} {(willStartWithWindows ? "will now" : "won't")} start with Windows from now on.");
#endif
		}

		public static void ShowBalloon(string text)
		{
			Log.Information($"Showing balloon: '{text}'");
			m_trayIcon.BalloonTipText = text;
			m_trayIcon.ShowBalloonTip(1000);
		}

		static void ShowSettings()
		{
			if (m_settingsWindow != null)
				return;

			m_settingsWindow = new SettingsWindow();
			m_settingsWindow.Show();
			m_settingsWindow.FormClosed += (s, e) =>
			{
				
				m_settingsWindow?.Dispose();
				m_settingsWindow = null;
			};
		}

	}
}
