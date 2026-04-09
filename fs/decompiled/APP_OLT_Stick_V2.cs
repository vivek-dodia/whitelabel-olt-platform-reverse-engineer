#define DEBUG
using System;
using System.CodeDom.Compiler;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using APP_OLT_Stick_Eth;
using APP_OLT_Stick_V2;
using Dapper;
using PacketDotNet;
using SharpPcap;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.Default | DebuggableAttribute.DebuggingModes.DisableOptimizations | DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints | DebuggableAttribute.DebuggingModes.EnableEditAndContinue)]
[assembly: TargetFramework(".NETCoreApp,Version=v8.0", FrameworkDisplayName = ".NET 8.0")]
[assembly: AssemblyCompany("APP_OLT_Stick_V2")]
[assembly: AssemblyConfiguration("Debug")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyInformationalVersion("1.0.0")]
[assembly: AssemblyProduct("APP_OLT_Stick_V2")]
[assembly: AssemblyTitle("APP_OLT_Stick_V2")]
[assembly: TargetPlatform("Windows7.0")]
[assembly: SupportedOSPlatform("Windows7.0")]
[assembly: AssemblyVersion("1.0.0.0")]
[module: RefSafetyRules(11)]
namespace APP_OLT_Stick_Eth
{
	public class CPE_Online : Form
	{
		public Form1 _form1;

		private DataTable dataTable_sql = new DataTable();

		private DataTable dataTable;

		private string dbPath = "example.db";

		private string connectionString = "";

		private string olt_sn = "";

		private string sql_table = "";

		private IContainer components = null;

		private DataGridView dataGridView_ONU_Online_List;

		private ContextMenuStrip contextMenuStrip_CPE_Online;

		private ToolStripMenuItem statusToolStripMenuItem;

		private System.Windows.Forms.Timer timer_CPE_online_display;

		public CPE_Online(Form1 form1, string path, string OLT_SN, string function_sel)
		{
			InitializeComponent();
			olt_sn = OLT_SN;
			dbPath = path;
			_form1 = form1;
			sql_table = "ONU_Status_List";
			connect_SQL();
			timer_CPE_online_display.Enabled = true;
			base.Icon = new Icon("FSLogo.ico");
		}

		public void connect_SQL()
		{
			connectionString = "Data Source=" + dbPath + ";Version=3;";
			using SQLiteConnection sQLiteConnection = new SQLiteConnection(connectionString);
			try
			{
				sQLiteConnection.Open();
			}
			catch
			{
				MessageBox.Show("Configuration is missed, please contact WST");
				Close();
			}
			try
			{
				string commandText = "select * from " + sql_table + " where OLT_SN='nosnisrquired'";
				using SQLiteCommand cmd = new SQLiteCommand(commandText, sQLiteConnection);
				using (SQLiteDataAdapter sQLiteDataAdapter = new SQLiteDataAdapter(cmd))
				{
					dataTable_sql = new DataTable();
					sQLiteDataAdapter.Fill(dataTable_sql);
					dataGridView_ONU_Online_List.DataSource = dataTable_sql;
					dataTable = dataTable_sql.Clone();
				}
				dataGridView_ONU_Online_List.Visible = true;
				dataGridView_ONU_Online_List.Columns["datetime"].Visible = false;
				dataGridView_ONU_Online_List.Columns["datetime_double"].Visible = false;
				dataGridView_ONU_Online_List.Columns["OLT_SN"].Visible = false;
			}
			catch
			{
				MessageBox.Show("Configuration is missed, please contact vendor");
			}
		}

		private void contextMenuStrip_CPE_Online_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			int num = 0;
			if (dataGridView_ONU_Online_List.Tag == null)
			{
				return;
			}
			num = (int)dataGridView_ONU_Online_List.Tag;
			DataGridViewRow dataGridViewRow = dataGridView_ONU_Online_List.Rows[num];
			try
			{
				string text = "";
				if (dataGridView_ONU_Online_List.Rows[num].Cells["CPE_SN"].Value != null)
				{
					text = dataGridView_ONU_Online_List.Rows[num].Cells["CPE_SN"].Value.ToString();
				}
			}
			catch
			{
			}
		}

		private string Search_alarm(byte[] onusn, ONU_Alarm_list oNU_alarm_List_temp)
		{
			string result = "";
			if (oNU_alarm_List_temp != null)
			{
				int count = oNU_alarm_List_temp.oNU_Alarm_List.Count;
				for (int i = 0; i < count; i++)
				{
					if (onusn == oNU_alarm_List_temp.oNU_Alarm_List[i].ONU_SN)
					{
						result = ONU_OP.ONU_Alarm_GEN(oNU_alarm_List_temp.oNU_Alarm_List[i].onu_alarm);
					}
				}
			}
			return result;
		}

		private void timer_CPE_online_display_Tick(object sender, EventArgs e)
		{
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			try
			{
				_form1.ONU_SN_Status_rpt(IPAddress.Parse(olt_sn), out ONU_STATUS_SN_list oNU_STATUS_SN_List_temp, out ONU_Alarm_list oNU_alarm_List_temp);
				if (oNU_STATUS_SN_List_temp != null)
				{
					int count = oNU_STATUS_SN_List_temp.oNU_STATUS_SNs_List.Count;
					dataTable.Clear();
					for (int i = 0; i < count; i++)
					{
						DataRow dataRow = dataTable.NewRow();
						dataRow["id"] = i;
						dataRow["OLT_SN"] = olt_sn;
						dataRow["CPE_SN"] = HexStringParser.ByteArrayToString(oNU_STATUS_SN_List_temp.oNU_STATUS_SNs_List[i].ONU_SN);
						dataRow["Current_status"] = ONU_OP.ONU_status_GEN(oNU_STATUS_SN_List_temp.oNU_STATUS_SNs_List[i].onu_state);
						dataRow["datetime_double"] = oNU_STATUS_SN_List_temp.reading_time_double;
						dataRow["datetime"] = oNU_STATUS_SN_List_temp.reading_time_str;
						dataRow["Comment"] = oNU_STATUS_SN_List_temp.oNU_STATUS_SNs_List[i].onu_id;
						dataRow["Alarm"] = Search_alarm(oNU_STATUS_SN_List_temp.oNU_STATUS_SNs_List[i].ONU_SN, oNU_alarm_List_temp);
						dataTable.Rows.Add(dataRow);
					}
					dataGridView_ONU_Online_List.DataSource = dataTable;
				}
			}
			catch
			{
			}
		}

		private void contextMenuStrip_CPE_Online_Opening(object sender, CancelEventArgs e)
		{
		}

		private void dataGridView_ONU_Online_List_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
			{
				dataGridView_ONU_Online_List.CurrentCell = dataGridView_ONU_Online_List.Rows[e.RowIndex].Cells[e.ColumnIndex];
				dataGridView_ONU_Online_List.Tag = e.RowIndex;
			}
		}

		private void statusToolStripMenuItem_Click(object sender, EventArgs e)
		{
			contextMenuStrip_CPE_Online.Close();
			string text = statusToolStripMenuItem.Text;
			if (text == "Read")
			{
				statusToolStripMenuItem.Text = "Stop";
				timer_CPE_online_display.Enabled = true;
			}
			else
			{
				statusToolStripMenuItem.Text = "Read";
				timer_CPE_online_display.Enabled = false;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.dataGridView_ONU_Online_List = new System.Windows.Forms.DataGridView();
			this.contextMenuStrip_CPE_Online = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.statusToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.timer_CPE_online_display = new System.Windows.Forms.Timer(this.components);
			((System.ComponentModel.ISupportInitialize)this.dataGridView_ONU_Online_List).BeginInit();
			this.contextMenuStrip_CPE_Online.SuspendLayout();
			base.SuspendLayout();
			this.dataGridView_ONU_Online_List.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView_ONU_Online_List.ContextMenuStrip = this.contextMenuStrip_CPE_Online;
			this.dataGridView_ONU_Online_List.Location = new System.Drawing.Point(3, 4);
			this.dataGridView_ONU_Online_List.Name = "dataGridView_ONU_Online_List";
			this.dataGridView_ONU_Online_List.ReadOnly = true;
			this.dataGridView_ONU_Online_List.RowHeadersWidth = 51;
			this.dataGridView_ONU_Online_List.Size = new System.Drawing.Size(1053, 434);
			this.dataGridView_ONU_Online_List.TabIndex = 11;
			this.dataGridView_ONU_Online_List.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(dataGridView_ONU_Online_List_CellMouseDown);
			this.contextMenuStrip_CPE_Online.ImageScalingSize = new System.Drawing.Size(20, 20);
			this.contextMenuStrip_CPE_Online.Items.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.statusToolStripMenuItem });
			this.contextMenuStrip_CPE_Online.Name = "contextMenuStrip_CPE_Online";
			this.contextMenuStrip_CPE_Online.Size = new System.Drawing.Size(114, 28);
			this.contextMenuStrip_CPE_Online.Opening += new System.ComponentModel.CancelEventHandler(contextMenuStrip_CPE_Online_Opening);
			this.contextMenuStrip_CPE_Online.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(contextMenuStrip_CPE_Online_ItemClicked);
			this.statusToolStripMenuItem.Name = "statusToolStripMenuItem";
			this.statusToolStripMenuItem.Size = new System.Drawing.Size(113, 24);
			this.statusToolStripMenuItem.Text = "Stop";
			this.statusToolStripMenuItem.Click += new System.EventHandler(statusToolStripMenuItem_Click);
			this.timer_CPE_online_display.Interval = 1000;
			this.timer_CPE_online_display.Tick += new System.EventHandler(timer_CPE_online_display_Tick);
			base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(1064, 450);
			base.Controls.Add(this.dataGridView_ONU_Online_List);
			base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
			base.MaximizeBox = false;
			base.Name = "CPE_Online";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "CPE_Online_Information";
			((System.ComponentModel.ISupportInitialize)this.dataGridView_ONU_Online_List).EndInit();
			this.contextMenuStrip_CPE_Online.ResumeLayout(false);
			base.ResumeLayout(false);
		}
	}
	public class Form1 : Form
	{
		private struct Netport_info
		{
			public string MAC_Address;

			public string IP_Address;
		}

		private string Vendor_Read_Key = "";

		private string Vendor_Write_Key = "";

		private string APP_version = "";

		private string dbPath = "example.db";

		private string connectionString = "";

		private DatabaseHelper databaseHelper;

		private double ONUperformancesavetime = 0.0;

		private double OLTperformancesavetime = 0.0;

		private readonly object Lock_Received_Package_List = new object();

		private List<UDP_Analysis_Package> Received_Package_List = new List<UDP_Analysis_Package>();

		private readonly object Lock_Send_Package_List = new object();

		private List<UDP_Analysis_Package> Send_Package_List = new List<UDP_Analysis_Package>();

		public readonly object Lock_Save_database = new object();

		private ushort Frame_Sent_ID_Code_main = 0;

		public readonly object Lock_Frame_Sent_ID_Code = new object();

		private MAC mAC = null;

		private MAC manual_mAC = null;

		private MAC optics_mAC = null;

		private readonly object textBoxLock = new object();

		private readonly object textBoxLock_management = new object();

		private readonly object Lock_timer_OLTList_read = new object();

		private List<IpMacAddressPair> List_ipMacAddressPairs = new List<IpMacAddressPair>();

		private long ARP_Aging_Timer_Counter = 0L;

		private readonly object Lock_oLT_Stick_Managements_List = new object();

		private List<OLT_Stick_Management> oLT_Stick_Managements_List = new List<OLT_Stick_Management>();

		private readonly object Lock_CPE_White_Table_Change = new object();

		private readonly object LOCK_ONU_performance_Report = new object();

		private readonly object LOCK_OLT_performance_Report = new object();

		private CancellationTokenSource _cts;

		private Task _runningTask;

		private List<CPE_Management> _openCPEForms = new List<CPE_Management>();

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_CMD0x41 = new ConcurrentQueue<UDP_Analysis_Package>();

		private BindingSource _selectedOLTUserBindingSource = new BindingSource();

		private readonly BindingSource _bindingSourceOLT = new BindingSource();

		private Logger logger;

		private static readonly object MacSendFrameGlobalLock = new object();

		private CancellationTokenSource _cts1;

		public int UI_Read_access_Flag = -1;

		public int UI_Write_access_Flag = -1;

		public int readerror = 0;

		public int writeerror = 0;

		public double last_password_write_time = 0.0;

		private IContainer components = null;

		private Button button_Eth_Net_Selection;

		private TextBox Log_textBox;

		private Label label1;

		private Button button_Send_MAC_Frame;

		private TextBox textBox_Send_Message;

		private System.Windows.Forms.Timer timer_Receiver_Message_Process;

		private ComboBox comboBox_OLT_stick_ADD;

		private System.Windows.Forms.Timer timer_OLTList_read;

		private DataGridView dataGridView_OLT_Online_List;

		private Label OLT_On_Line_information_label;

		private ContextMenuStrip contextMenuStrip_OLT_Check;

		private ToolStripMenuItem oNUStatusToolStripMenuItem;

		private ToolStripMenuItem FToolStripMenuItem;

		private ToolStripMenuItem OLT_Read_ToolStripMenuItem;

		private ToolStripMenuItem oLT地址ToolStripMenuItem;

		private ToolStripMenuItem oLT_LIST_ToolStripMenuItem;

		private ToolStripMenuItem cPEToolStripMenuItem;

		private ToolStripMenuItem cPEToolStripMenuItem1;

		private MenuStrip menuStrip1;

		private ToolStripMenuItem oLTIPAddChangeToolStripMenuItem;

		private ToolStripMenuItem cPEWhiteListToolStripMenuItem;

		private TextBox textBox_LOG_Management;

		private ToolStripMenuItem oLTSDKUpgradeToolStripMenuItem;

		private ToolStripMenuItem oNUServicepushingToolStripMenuItem;

		private ToolStripMenuItem pushing2OLTToolStripMenuItem;

		private ToolStripMenuItem pollingToolStripMenuItem;

		private ToolStripMenuItem pushingtoOLTToolStripMenuItem;

		private ToolStripMenuItem pollingbackToolStripMenuItem;

		private System.Windows.Forms.Timer timer_ONU_optics_read;

		private ToolStripMenuItem oNUHistoryToolStripMenuItem;

		private ToolStripMenuItem oLTRemoveToolStripMenuItem;

		private ToolStripMenuItem OLT_Write_ToolStripMenuItem;

		private Label RW_Status_Lable;

		private CustomComboBox comboBox_net_ports;

		private System.Windows.Forms.Timer timer_Performace_save;

		public bool IsRunning
		{
			get
			{
				Task runningTask = _runningTask;
				return runningTask != null && runningTask.Status == TaskStatus.Running;
			}
		}

		public static ONU_AppData AppData { get; } = new ONU_AppData();


		public static OLT_AppData OLT_AppData { get; } = new OLT_AppData();


		public event EventHandler<OLTPropertyChangedEventArgs> OLTPropertyChanged;

		public void StartProcessingTask()
		{
			_cts?.Cancel();
			_cts = new CancellationTokenSource();
			_runningTask = Task.Run(() => OLTPolling_Async(_cts.Token), _cts.Token);
		}

		public Form1()
		{
			InitializeComponent();
			base.Icon = new Icon("FSLogo.ico");
			menuStrip1.Renderer = new CustomMenuRenderer();
			contextMenuStrip_OLT_Check.Renderer = new CustomMenuRenderer();
			menuStrip1.BackColor = ColorTranslator.FromHtml("#14C9BB");
			button_Eth_Net_Selection.FlatStyle = FlatStyle.Flat;
			button_Eth_Net_Selection.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#14C9BB");
			button_Eth_Net_Selection.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 20, 201, 187);
			button_Eth_Net_Selection.BackColor = ColorTranslator.FromHtml("#FFFFFF");
			button_Eth_Net_Selection.ForeColor = ColorTranslator.FromHtml("#14C9BB");
			button_Send_MAC_Frame.FlatStyle = FlatStyle.Flat;
			button_Send_MAC_Frame.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#14C9BB");
			button_Send_MAC_Frame.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 20, 201, 187);
			button_Send_MAC_Frame.BackColor = ColorTranslator.FromHtml("#FFFFFF");
			button_Send_MAC_Frame.ForeColor = ColorTranslator.FromHtml("#14C9BB");
			OLT_On_Line_information_label.ForeColor = ColorTranslator.FromHtml("#212519");
			dataGridView_OLT_Online_List.BackgroundColor = ColorTranslator.FromHtml("#F6F6F6");
			dataGridView_OLT_Online_List.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#14C9BB");
			string currentDirectory = Directory.GetCurrentDirectory();
			dbPath = Path.Combine(currentDirectory, "mydemo");
			connectionString = "Data Source=" + dbPath + ";Version=3;";
			databaseHelper = new DatabaseHelper(dbPath, connectionString, 1.0);
			logger = new Logger("logtext.txt", 50L);
			databaseHelper.connect_SQL();
			ConfigureDataGridColumns();
			read_ini_configuration();
			Text = APP_version;
			SetupDataBindings_OLT();
			SubscribeToEvents();
			timer_enable();
		}

		protected virtual void OnOLTPropertyChanged(OLTPropertyChangedEventArgs e)
		{
			this.OLTPropertyChanged?.Invoke(this, e);
		}

		private void SubscribeToEvents()
		{
			OLT_AppData.OLTPropertyChanged += OnOLTPropertyChanged;
		}

		private void OnOLTPropertyChanged(object sender, OLTPropertyChangedEventArgs e)
		{
			object sender2 = sender;
			OLTPropertyChangedEventArgs e2 = e;
			if (base.InvokeRequired)
			{
				Invoke(delegate
				{
					OnOLTPropertyChanged(sender2, e2);
				});
				return;
			}
			if (e2.PropertyName == "olt_status")
			{
				UpdateStatusCell(e2.OLT);
			}
			if (e2.PropertyName == "olt_temperature" || e2.PropertyName == "alarm_status")
			{
			}
			if (e2.OLT != OLT_AppData.SelectedUser)
			{
			}
		}

		private void UpdateStatusCell(OLT_Performance_Notity olt)
		{
			int num = OLT_AppData.Users.IndexOf(olt);
			if (num >= 0)
			{
				if (olt.olt_status == "OFF_LINE")
				{
					dataGridView_OLT_Online_List.Rows[num].DefaultCellStyle.BackColor = Color.Gray;
				}
				else if (olt.olt_status == "ON_LINE")
				{
					dataGridView_OLT_Online_List.Rows[num].DefaultCellStyle.BackColor = Color.Green;
				}
				else if (olt.olt_status == "FLASH")
				{
					dataGridView_OLT_Online_List.Rows[num].DefaultCellStyle.BackColor = Color.Yellow;
				}
			}
		}

		private void SetupDataBindings_OLT()
		{
			try
			{
				_bindingSourceOLT.DataSource = OLT_AppData.Users;
				dataGridView_OLT_Online_List.DataSource = _bindingSourceOLT;
				dataGridView_OLT_Online_List.SelectionChanged += DataGridView_SelectionChanged;
				OLT_AppData.PropertyChanged += AppData_PropertyChanged;
				_selectedOLTUserBindingSource.DataSource = OLT_AppData;
				_selectedOLTUserBindingSource.DataMember = "SelectedUser";
				OLT_On_Line_information_label.DataBindings.Add("Text", OLT_AppData, "TotalUsers", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, "0", "Total online OLTs: {0}");
				comboBox_OLT_stick_ADD.DataSource = _bindingSourceOLT;
				comboBox_OLT_stick_ADD.DisplayMember = "olt_ip";
				comboBox_OLT_stick_ADD.ValueMember = "olt_ip";
			}
			catch (Exception ex)
			{
				MessageBox.Show("Data binding failed: " + ex.Message + "\n\n" + ex.StackTrace, "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		private void DataGridView_SelectionChanged(object sender, EventArgs e)
		{
			if (dataGridView_OLT_Online_List.CurrentRow != null && dataGridView_OLT_Online_List.CurrentRow.DataBoundItem is OLT_Performance_Notity selectedUser)
			{
				OLT_AppData.SelectedUser = selectedUser;
			}
			else
			{
				OLT_AppData.SelectedUser = null;
			}
		}

		private void AppData_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == "SelectedUser")
			{
				UpdateDataGridViewSelection();
			}
		}

		private void UpdateDataGridViewSelection()
		{
			if (dataGridView_OLT_Online_List.InvokeRequired)
			{
				dataGridView_OLT_Online_List.Invoke(UpdateDataGridViewSelection);
				return;
			}
			try
			{
				if (OLT_AppData.SelectedUser != null)
				{
					int num = OLT_AppData.Users.IndexOf(OLT_AppData.SelectedUser);
					if (num >= 0 && num < dataGridView_OLT_Online_List.Rows.Count)
					{
						dataGridView_OLT_Online_List.ClearSelection();
						dataGridView_OLT_Online_List.Rows[num].Selected = true;
						dataGridView_OLT_Online_List.CurrentCell = dataGridView_OLT_Online_List.Rows[num].Cells[0];
					}
				}
				else
				{
					dataGridView_OLT_Online_List.ClearSelection();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Selection update failed: " + ex.Message);
			}
		}

		private void ConfigureDataGridColumns()
		{
			dataGridView_OLT_Online_List.AutoGenerateColumns = false;
			dataGridView_OLT_Online_List.Columns.Clear();
			AddColumn("IP", "olt_ip", 120);
			AddColumn("SN", "olt_sn", 120);
			AddColumn("MAC", "olt_mac", 120);
			AddColumn("Staus", "olt_status", 80);
			AddColumn("TX_PWR", "olt_tx_pwr", 80);
			AddColumn("Bias", "olt_bias", 80);
			AddColumn("Temperature", "olt_temperature", 80);
			AddColumn("Voltage", "olt_voltage", 80);
			AddColumn("RSSI", "olt_rssi", 80);
			AddColumn("Alarm", "alarm_status", 80);
			AddColumn("TIME", "time_string", 80);
			DataGridViewImageColumn dataGridViewColumn = new DataGridViewImageColumn
			{
				HeaderText = "Status",
				Name = "StatusIcon",
				Width = 30,
				ImageLayout = DataGridViewImageCellLayout.Zoom
			};
			dataGridView_OLT_Online_List.Columns.Add(dataGridViewColumn);
		}

		private void AddColumn(string header, string dataPropertyName, int width)
		{
			dataGridView_OLT_Online_List.Columns.Add(new DataGridViewTextBoxColumn
			{
				HeaderText = header,
				DataPropertyName = dataPropertyName,
				Width = width,
				Name = dataPropertyName
			});
		}

		private void timer_enable()
		{
			timer_OLTList_read.Enabled = true;
		}

		private void read_ini_configuration()
		{
			string[] array = File.ReadAllLines("appmanagement.ini");
			int num = array.Length;
			if (num < 3)
			{
				MessageBox.Show("appmanagement.ini is missed, please check");
				Dispose();
			}
			else
			{
				Vendor_Read_Key = array[0].Trim();
				Vendor_Write_Key = array[1].Trim();
				APP_version = array[2];
			}
		}

		public static void AppendOrCreateLog(string content)
		{
			try
			{
				string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				string path = Path.Combine(directoryName, "logtext.txt");
				if (File.Exists(path))
				{
					File.AppendAllText(path, content + Environment.NewLine);
				}
				else
				{
					File.WriteAllText(path, content + Environment.NewLine);
				}
			}
			catch
			{
			}
		}

		private void Update_textBox_LOG_Management(string text)
		{
			string text2 = text;
			if (textBox_LOG_Management.InvokeRequired)
			{
				textBox_LOG_Management.Invoke(delegate
				{
					Update_textBox_LOG_Management(text2);
				});
				return;
			}
			lock (textBoxLock_management)
			{
				if (textBox_LOG_Management.Text.Length > 10240)
				{
					logger.Log(textBox_LOG_Management.Text);
					textBox_LOG_Management.Text = textBox_LOG_Management.Text.Substring(textBox_LOG_Management.Text.Length - 500);
					textBox_LOG_Management.SelectionStart = textBox_LOG_Management.Text.Length;
				}
				textBox_LOG_Management.AppendText(text2 + Environment.NewLine);
			}
		}

		private void UpdateTextBox(string text)
		{
			string text2 = text;
			if (Log_textBox.InvokeRequired)
			{
				Log_textBox.Invoke(delegate
				{
					UpdateTextBox(text2);
				});
				return;
			}
			lock (textBoxLock)
			{
				if (Log_textBox.Text.Length > 5000)
				{
					Log_textBox.Text = Log_textBox.Text.Substring(Log_textBox.Text.Length - 1000);
					Log_textBox.SelectionStart = Log_textBox.Text.Length;
				}
				Log_textBox.AppendText(text2 + Environment.NewLine);
			}
		}

		public void Textlog_With_Time(string text_show)
		{
			string text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
			Update_textBox_LOG_Management("[" + text + "]==>" + text_show);
		}

		private void ClearComboBoxItems()
		{
			try
			{
				if (comboBox_OLT_stick_ADD.InvokeRequired)
				{
					comboBox_OLT_stick_ADD.Invoke(delegate
					{
						comboBox_OLT_stick_ADD.Items.Clear();
					});
				}
				else
				{
					comboBox_OLT_stick_ADD.Items.Clear();
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error clearing combo box items: " + ex.Message);
			}
		}

		public int IP_add_Check(IPAddress iPAddress, IPAddress old)
		{
			IPAddress iPAddress2 = iPAddress;
			int num = 0;
			try
			{
				ONU_Performance_Notity oNU_Performance_Notity = AppData.Users.FirstOrDefault((ONU_Performance_Notity olt) => olt != null && olt.olt_ip != null && olt.olt_ip.Equals(iPAddress2));
				if (oNU_Performance_Notity != null)
				{
					Textlog_With_Time("new IP=" + iPAddress2.ToString() + "  conflict with current oLTs");
					return -1;
				}
				num = 0;
			}
			catch
			{
				num = -1;
			}
			return num;
		}

		private void button_Eth_Net_Selection_Click(object sender, EventArgs e)
		{
			List<Netport_info> list = new List<Netport_info>();
			Dictionary<string, List<string>> dictionary = new Dictionary<string, List<string>>();
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface networkInterface in allNetworkInterfaces)
			{
				if (networkInterface.OperationalStatus != OperationalStatus.Up)
				{
					continue;
				}
				PhysicalAddress physicalAddress = networkInterface.GetPhysicalAddress();
				byte[] addressBytes = physicalAddress.GetAddressBytes();
				string key = BitConverter.ToString(addressBytes).Replace("-", ":");
				IPInterfaceProperties iPProperties = networkInterface.GetIPProperties();
				if (!dictionary.ContainsKey(key))
				{
					dictionary[key] = new List<string>();
				}
				foreach (UnicastIPAddressInformation unicastAddress in iPProperties.UnicastAddresses)
				{
					if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
					{
						dictionary[key].Add(unicastAddress.Address.ToString());
					}
				}
			}
			comboBox_net_ports.DataSource = null;
			comboBox_net_ports.Items.Clear();
			foreach (KeyValuePair<string, List<string>> item2 in dictionary)
			{
				UpdateTextBox("MAC Address: " + item2.Key);
				foreach (string item3 in item2.Value)
				{
					UpdateTextBox("  IP Address: " + item3);
					Netport_info item = default(Netport_info);
					if (item3 != "" && item2.Key != "")
					{
						item.IP_Address = item3;
						item.MAC_Address = item2.Key;
						list.Add(item);
						comboBox_net_ports.Items.Add(item.IP_Address + "---MAC:" + item.MAC_Address);
					}
				}
			}
			if (comboBox_net_ports.Items.Count > 0)
			{
				comboBox_net_ports.SelectedIndex = 0;
			}
		}

		private static void Start_Monitor_MAC_Package(ref MAC mac)
		{
			mac.StartListening();
		}

		private void MAC_Instance_init()
		{
			string text = " ";
			if (comboBox_net_ports.SelectedItem == null)
			{
				UpdateTextBox("请选择网管主机地址");
				return;
			}
			text = comboBox_net_ports.SelectedItem.ToString();
			if (text == null)
			{
				UpdateTextBox("网管主机地址选择错误，请核实！");
				return;
			}
			string[] array = text.Split(new string[1] { "---MAC:" }, StringSplitOptions.None);
			text = array[1];
			IPAddress sourceIP = IPAddress.Parse(array[0]);
			ushort mAC_Type = 2048;
			mAC = new MAC(text, mAC_Type, sourceIP);
			if (mAC.MAC_init_Error != 0)
			{
				UpdateTextBox("Init set fault, error code =" + mAC.MAC_init_Error);
				return;
			}
			optics_mAC = new MAC(text, mAC_Type, sourceIP);
			manual_mAC = new MAC(text, mAC_Type, sourceIP);
			if (manual_mAC.MAC_init_Error != 0)
			{
				UpdateTextBox("Init set fault, error code =" + manual_mAC.MAC_init_Error);
				return;
			}
			Task.Run(delegate
			{
				Start_Monitor_MAC_Package(ref mAC);
			});
			UpdateTextBox("Network interface ready - starting data transfer.");
			timer_Receiver_Message_Process.Enabled = true;
			StartProcessingTask();
		}

		private static byte[] HexStringToByteArray(string hex)
		{
			hex = hex.Replace(" ", "");
			if (hex.Length % 2 != 0)
			{
				throw new ArgumentException("Invalid hex string length.");
			}
			int num = hex.Length / 2;
			byte[] array = new byte[num];
			for (int i = 0; i < num; i++)
			{
				array[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
			}
			return array;
		}

		private int Send_Ethernet_Frame(byte[] Frame_Content, PhysicalAddress physicalAddress, IPAddress iPAddress)
		{
			int result = 0;
			try
			{
				manual_mAC.SendFrame(Frame_Content, physicalAddress, iPAddress);
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		private static byte[] ConvertStringToHexArray(string input)
		{
			byte[] array = new byte[input.Length];
			for (int i = 0; i < input.Length; i++)
			{
				int num = input[i];
				array[i] = (byte)num;
			}
			return array;
		}

		private void button_Send_MAC_Frame_Click(object sender, EventArgs e)
		{
			string text = textBox_Send_Message.Text;
			string text2 = textBox_Send_Message.Text;
			byte[] array = ConvertStringToHexArray(text2);
			byte b = 0;
			try
			{
				if (comboBox_OLT_stick_ADD.SelectedValue.ToString().Contains("255:255:255:255"))
				{
					return;
				}
			}
			catch
			{
			}
			if (text2.Length < 3)
			{
				return;
			}
			b = (byte)(((array[0] < 65 || array[0] > 90) && (array[0] < 97 || array[0] > 122)) ? 64 : 65);
			switch (b)
			{
			case 64:
			{
				byte[] array3 = new byte[array.Length + 1];
				Array.Copy(array, 0, array3, 1, array.Length);
				array3[0] = b;
				try
				{
					string text4 = " ";
					if (comboBox_OLT_stick_ADD.SelectedValue == null)
					{
						UpdateTextBox("请选择OLT_Stick地址");
						break;
					}
					text4 = comboBox_OLT_stick_ADD.SelectedValue?.ToString();
					ushort upd_ID2 = 0;
					lock (Lock_Frame_Sent_ID_Code)
					{
						Frame_Sent_ID_Code_main = (ushort)(Frame_Sent_ID_Code_main++ % 16383);
						upd_ID2 = Frame_Sent_ID_Code_main;
					}
					UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
					uDP_Analysis_Package.content = array3;
					uDP_Analysis_Package.task_owner = Task_Owner.Manual_send;
					uDP_Analysis_Package.time_window = 10.0;
					uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					uDP_Analysis_Package.SenderProtocolAddress = IPAddress.Parse(text4 ?? "127.1.1.1");
					int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management frame) => frame.oLT_share_data.iPAddress.ToString() == uDP_Analysis_Package.SenderProtocolAddress.ToString());
					if (num2 >= 0)
					{
						uDP_Analysis_Package.SenderHardwareAddress = oLT_Stick_Managements_List[num2].oLT_share_data.physicalAddress;
						uDP_Analysis_Package.cmd_Code = (Command_Code)array3[0];
						uDP_Analysis_Package.upd_ID = upd_ID2;
						Send_Package_List.Add(uDP_Analysis_Package);
						if (Send_Ethernet_Frame(array3, uDP_Analysis_Package.SenderHardwareAddress, uDP_Analysis_Package.SenderProtocolAddress) != 0)
						{
							MessageBox.Show("cannot connect with OLT！");
						}
						else
						{
							Textlog_With_Time("Send command --" + text);
						}
					}
					else
					{
						MessageBox.Show("no matched IP add.,please check");
					}
					break;
				}
				catch
				{
					break;
				}
			}
			case 65:
			{
				byte[] array2 = new byte[array.Length + 3];
				Array.Copy(array, 0, array2, 3, array.Length);
				array2[0] = b;
				try
				{
					string text3 = " ";
					if (comboBox_OLT_stick_ADD.SelectedValue == null)
					{
						UpdateTextBox("请选择OLT_Stick地址");
						break;
					}
					text3 = comboBox_OLT_stick_ADD.SelectedValue?.ToString();
					ushort upd_ID = 0;
					lock (Lock_Frame_Sent_ID_Code)
					{
						Frame_Sent_ID_Code_main = (ushort)(Frame_Sent_ID_Code_main++ % 16383);
						upd_ID = Frame_Sent_ID_Code_main;
					}
					UDP_Analysis_Package uDP_Analysis_Package2 = new UDP_Analysis_Package();
					array2[1] = (byte)(Frame_Sent_ID_Code_main >> 8);
					array2[2] = (byte)(Frame_Sent_ID_Code_main & 0xFFu);
					uDP_Analysis_Package2.content = array2;
					uDP_Analysis_Package2.task_owner = Task_Owner.Manual_send;
					uDP_Analysis_Package2.time_window = 10.0;
					uDP_Analysis_Package2.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					uDP_Analysis_Package2.SenderProtocolAddress = IPAddress.Parse(text3 ?? "");
					int num = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management frame) => frame.oLT_share_data.iPAddress.ToString() == uDP_Analysis_Package2.SenderProtocolAddress.ToString());
					if (num >= 0)
					{
						uDP_Analysis_Package2.SenderHardwareAddress = oLT_Stick_Managements_List[num].oLT_share_data.physicalAddress;
						uDP_Analysis_Package2.cmd_Code = (Command_Code)array2[0];
						uDP_Analysis_Package2.upd_ID = upd_ID;
						Send_Package_List.Add(uDP_Analysis_Package2);
						if (Send_Ethernet_Frame(array2, uDP_Analysis_Package2.SenderHardwareAddress, uDP_Analysis_Package2.SenderProtocolAddress) != 0)
						{
							MessageBox.Show("cannot connect with OLT！");
						}
						else
						{
							Textlog_With_Time("Send command --" + text);
						}
					}
					else
					{
						MessageBox.Show("no matched IP add.,please check");
					}
					break;
				}
				catch
				{
					break;
				}
			}
			}
		}

		public string ExtractSerialNumberRegex(string input)
		{
			Match match = Regex.Match(input, "SN:(\\w{12})");
			if (match.Success)
			{
				return match.Groups[1].Value;
			}
			return null;
		}

		private void timer_Receiver_Message_Process_Tick(object sender, EventArgs e)
		{
			string text = RW_Status_Lable.Text;
			string text2 = "";
			if (UI_Read_access_Flag == 1)
			{
				text2 = "R-EN ";
			}
			else if (UI_Read_access_Flag == 0)
			{
				text2 = "R-DIS ";
			}
			if (UI_Write_access_Flag == 1)
			{
				text2 += "W-EN";
			}
			else if (UI_Write_access_Flag == 0)
			{
				text2 += "W-DIS";
			}
			RW_Status_Lable.Text = text2;
			List<MAC_Frame_Content> list = new List<MAC_Frame_Content>();
			List<Arp_Frame_Content> copyArpCircularBuffer = new List<Arp_Frame_Content>();
			int num = 0;
			lock (SharedResources.LockObject)
			{
				num = mAC.mCircularBuffer.Count;
				if (num > 1000)
				{
					num = 1000;
				}
				for (int j = 0; j < num; j++)
				{
					MAC_Frame_Content item = default(MAC_Frame_Content);
					item.Content = mAC.mCircularBuffer.Dequeue().Content;
					list.Add(item);
				}
			}
			for (int k = 0; k < num; k++)
			{
				UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
				if (list.Count <= 0)
				{
					continue;
				}
				byte[] array = new byte[6];
				Array.Copy(list[k].Content, 0, array, 0, 6);
				PhysicalAddress senderHardwareAddress = new PhysicalAddress(array);
				uDP_Analysis_Package.SenderHardwareAddress = senderHardwareAddress;
				byte[] array2 = new byte[4];
				Array.Copy(list[k].Content, 20, array2, 0, 4);
				IPAddress iPAddress = new IPAddress(array2);
				uDP_Analysis_Package.SenderProtocolAddress = iPAddress;
				uDP_Analysis_Package.cmd_Code = (Command_Code)list[k].Content[36];
				uDP_Analysis_Package.upd_ID = (ushort)(list[k].Content[37] * 256 + list[k].Content[38]);
				byte[] content = new byte[1600];
				uDP_Analysis_Package.content = content;
				Array.Copy(list[k].Content, 36, uDP_Analysis_Package.content, 0, list[k].Content.Length - 36);
				uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
				uDP_Analysis_Package.time_window = 60.0;
				uDP_Analysis_Package.UDP_package_length = (ushort)(list[k].Content[32] * 256 + list[k].Content[33]) - 8;
				lock (Lock_oLT_Stick_Managements_List)
				{
					OLT_Stick_Management oLT_Stick_Management = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(iPAddress));
					if (oLT_Stick_Management == null)
					{
						continue;
					}
					switch (uDP_Analysis_Package.cmd_Code)
					{
					case Command_Code.shake_hand:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						UpdateTextBox("shake_hand =1200");
						oLT_Stick_Management.shake_Hands.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.OLT_Update_BIN_cmd:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.in_band_FW_Upgrade.uDP_Analysis_Packages_receive_bin_sent.Push(uDP_Analysis_Package);
						break;
					case Command_Code.ip_configuration:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.ip_Add_Set.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.cpe_white_list_send:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.cPE_WhiteLst_Set.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.ONU_WLIST_RPT:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.oNU_Whitelst_GET.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.cpe_service_type_send:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.cPE_ServiceType_Set.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.SERVICE_CONFIG_RPT:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.oNU_ServiceType_GET.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.cpe_sn_status:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.oNU_SN_STATUS_GET.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.cpe_alarm_report:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.oNU_Alarm_GET.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.olt_alarm_report:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.oLT_STATUS_GET.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.Password_cmd:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.pWD_Set.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case Command_Code.Password_check_cmd:
						oLT_Stick_Management.oLT_share_data.age_time = 1200;
						oLT_Stick_Management.pWD_ACK.uDP_Analysis_Packages_receive.Push(uDP_Analysis_Package);
						break;
					case (Command_Code)65:
						try
						{
							byte[] array3 = new byte[uDP_Analysis_Package.UDP_package_length - 1];
							byte[] array4 = new byte[uDP_Analysis_Package.UDP_package_length - 2];
							Array.Copy(uDP_Analysis_Package.content, 1, array3, 0, uDP_Analysis_Package.UDP_package_length - 1);
							Array.Copy(uDP_Analysis_Package.content, 2, array4, 0, uDP_Analysis_Package.UDP_package_length - 2);
							if (array3[0] == 65)
							{
								string @string = Encoding.ASCII.GetString(array4);
								Textlog_With_Time(iPAddress.ToString() + "-" + @string);
								if (@string.Contains("dying gasp!"))
								{
									string onusn = ExtractSerialNumberRegex(@string);
									Speical_status_onu_sn_ONU_Monitor(iPAddress.ToString(), onusn, "DYING_GASP");
								}
								else if (@string.Contains("online!"))
								{
									string onusn2 = ExtractSerialNumberRegex(@string);
									Speical_status_onu_sn_ONU_Monitor(iPAddress.ToString(), onusn2, "ON_LINE");
								}
								else if (@string.Contains("offline!"))
								{
									string onusn3 = ExtractSerialNumberRegex(@string);
									Speical_status_onu_sn_ONU_Monitor(iPAddress.ToString(), onusn3, "OFF_LINE");
								}
								else
								{
									uDP_Analysis_Packages_CMD0x41.Enqueue(uDP_Analysis_Package);
								}
							}
						}
						catch
						{
						}
						break;
					}
				}
			}
			lock (SharedResources.ArplockObject)
			{
				try
				{
					num = mAC.mArpCircularBuffer.Count;
					if (num > 1000)
					{
						num = 1000;
					}
					for (int l = 0; l < num; l++)
					{
						Arp_Frame_Content arp_Frame_Content = default(Arp_Frame_Content);
						arp_Frame_Content = mAC.mArpCircularBuffer.Dequeue();
						copyArpCircularBuffer.Add(arp_Frame_Content);
					}
				}
				catch
				{
				}
			}
			int i;
			for (i = 0; i < num; i++)
			{
				if (copyArpCircularBuffer[i].SenderProtocolAddress == null)
				{
					continue;
				}
				string text3 = copyArpCircularBuffer[i].ToString();
				if (!(mAC._sourceIP.ToString() != copyArpCircularBuffer[i].SenderProtocolAddress.ToString()))
				{
					continue;
				}
				if (copyArpCircularBuffer[i].Opcode == 1)
				{
					lock (Lock_oLT_Stick_Managements_List)
					{
						OLT_Stick_Management oLT_Stick_Management2 = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(copyArpCircularBuffer[i].SenderProtocolAddress));
						if (oLT_Stick_Management2 != null && oLT_Stick_Management2.oLT_share_data.active)
						{
							UpdateTextBox($"{i + 1}arp:" + text3);
							oLT_Stick_Management2.oLT_share_data.age_time = 1200;
							UpdateTextBox("ARP_1 =1200");
							oLT_Stick_Management2.oLT_share_data.physicalAddress = copyArpCircularBuffer[i].SenderHardwareAddress;
						}
						else if (copyArpCircularBuffer[i].specified_code == "OLT")
						{
							UpdateTextBox($"{i + 1}arp:" + text3);
							OLT_share_data oLT_share_data = new OLT_share_data();
							oLT_share_data.iPAddress = copyArpCircularBuffer[i].SenderProtocolAddress;
							oLT_share_data.physicalAddress = copyArpCircularBuffer[i].SenderHardwareAddress;
							oLT_share_data.OLT_SN = "new OLT";
							oLT_share_data.Comment = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
							OLT_Stick_Management oLT_Stick_Management3 = new OLT_Stick_Management(oLT_share_data, oLT_Stick_Managements_List.Count + 1);
							oLT_Stick_Management3.oLT_share_data.age_time = 0;
							oLT_Stick_Management3.oLT_share_data.active = true;
							lock (Lock_oLT_Stick_Managements_List)
							{
								oLT_Stick_Managements_List.Add(oLT_Stick_Management3);
								Adding_OLT_AppData_user(oLT_Stick_Management3);
							}
						}
					}
				}
				else
				{
					if (copyArpCircularBuffer[i].Opcode != 2)
					{
						continue;
					}
					lock (Lock_oLT_Stick_Managements_List)
					{
						OLT_Stick_Management oLT_Stick_Management4 = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(copyArpCircularBuffer[i].SenderProtocolAddress));
						if (oLT_Stick_Management4 != null)
						{
							oLT_Stick_Management4.oLT_share_data.age_time = 1500;
							UpdateTextBox("ACK_ARP =1500");
							oLT_Stick_Management4.oLT_share_data.physicalAddress = copyArpCircularBuffer[i].SenderHardwareAddress;
						}
					}
				}
			}
			ARP_Aging_Timer_Counter++;
			int num2 = 1000 / timer_Receiver_Message_Process.Interval;
			if (num2 < ARP_Aging_Timer_Counter)
			{
				ARP_Aging_Timer_Counter = 0L;
				lock (Lock_oLT_Stick_Managements_List)
				{
					for (int m = 0; m < oLT_Stick_Managements_List.Count; m++)
					{
						if (oLT_Stick_Managements_List[m].oLT_share_data.age_time > 1)
						{
							oLT_Stick_Managements_List[m].oLT_share_data.age_time--;
						}
					}
				}
			}
			if (dataGridView_OLT_Online_List.RowCount > 1)
			{
				dataGridView_OLT_Online_List.Enabled = true;
			}
			else
			{
				dataGridView_OLT_Online_List.Enabled = false;
			}
		}

		private void Adding_OLT_AppData_user(OLT_Stick_Management temp)
		{
			OLT_Performance_Notity oLT_Performance_Notity = new OLT_Performance_Notity();
			oLT_Performance_Notity.olt_ip = temp.oLT_share_data.iPAddress;
			oLT_Performance_Notity.olt_sn = temp.oLT_share_data.OLT_SN;
			oLT_Performance_Notity.olt_mac = temp.oLT_share_data.physicalAddress.ToString();
			oLT_Performance_Notity.needsaverightnow = "YES";
			oLT_Performance_Notity.time_second = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			oLT_Performance_Notity.time_string = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
			OLT_AppData.AddOLT(oLT_Performance_Notity);
		}

		private static bool IsValidIpAddress(string ipAddress)
		{
			string pattern = "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
			return Regex.IsMatch(ipAddress, pattern);
		}

		private static bool IsValidMacAddress(string macAddress)
		{
			string pattern = "^(?:[0-9A-Fa-f]{2}[:-]?){5}[0-9A-Fa-f]{2}$";
			return Regex.IsMatch(macAddress, pattern);
		}

		private void ConfOpen_ToolStripMenuItem_Click(object sender, EventArgs e)
		{
		}

		private void cPEToolStripMenuItem1_Click(object sender, EventArgs e)
		{
			if (File.Exists(dbPath))
			{
				CPE_Management cPE_Management = new CPE_Management(this, dbPath, AppData);
				cPE_Management.Show();
				_openCPEForms.Add(cPE_Management);
				cPE_Management.FormClosed += delegate
				{
					_openCPEForms.Remove(cPE_Management);
				};
			}
			else
			{
				MessageBox.Show("System management document is missed, please contact vendor");
			}
		}

		private void oLTListToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (File.Exists(dbPath))
			{
				OLT_HISTORY oLT_HISTORY = new OLT_HISTORY(dbPath);
				oLT_HISTORY.Show();
			}
			else
			{
				MessageBox.Show("System management document is missed, please contact vendor");
			}
		}

		private void timer_OLTList_read_Tick(object sender, EventArgs e)
		{
			if (oLT_Stick_Managements_List.Count > 0)
			{
				int num = oLT_Stick_Managements_List[0].send_arp(mAC);
				if (num == -1)
				{
					Textlog_With_Time("Alarm: need check network connection");
					mAC.Dispose();
					optics_mAC.Dispose();
					manual_mAC.Dispose();
					MAC_Instance_init();
				}
			}
		}

		private void comboBox_net_ports_SelectedIndexChanged(object sender, EventArgs e)
		{
			string text = " ";
			if (comboBox_net_ports.SelectedItem == null)
			{
				UpdateTextBox("请选择网管主机地址");
				return;
			}
			text = comboBox_net_ports.SelectedItem?.ToString();
			if (text == null)
			{
				UpdateTextBox("网管主机地址选择错误，请核实！");
				return;
			}
			string[] array = text.Split(new string[1] { "---MAC:" }, StringSplitOptions.None);
			text = array[1];
			IPAddress obj = IPAddress.Parse(array[0]);
			if (mAC == null || mAC._destinationIP == null || !mAC._destinationIP.Equals(obj))
			{
				MAC_Instance_init();
			}
		}

		private void comboBox_OLT_stick_ADD_SelectedIndexChanged(object sender, EventArgs e)
		{
		}

		private void comboBox_OLT_stick_ADD_MouseClick(object sender, MouseEventArgs e)
		{
		}

		private void contextMenuStrip_OLT_Check_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
		{
			contextMenuStrip_OLT_Check.Close();
			int num = 0;
			if (dataGridView_OLT_Online_List.Tag == null)
			{
				return;
			}
			num = (int)dataGridView_OLT_Online_List.Tag;
			DataGridViewRow dataGridViewRow = dataGridView_OLT_Online_List.Rows[num];
			if (e.ClickedItem != oLTIPAddChangeToolStripMenuItem)
			{
				return;
			}
			try
			{
				if (dataGridView_OLT_Online_List.CurrentCell.Value == null)
				{
					return;
				}
			}
			catch
			{
				MessageBox.Show("please select OLT IP address");
				return;
			}
			try
			{
				string comment = "";
				object value = dataGridView_OLT_Online_List.Rows[num].Cells["olt_ip"].Value;
				if (value == null)
				{
					return;
				}
				comment = value.ToString();
				int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management a) => a.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
				if (num2 != -1)
				{
					if (oLT_Stick_Managements_List[num2].oLT_share_data.status_int == 0)
					{
						Textlog_With_Time("cannot change ip-add in OLT IP_Add=" + comment + ", since it's not active");
					}
					lock (Lock_oLT_Stick_Managements_List)
					{
						OLT_Stick_Management oLT_Stick_Management = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
						if (oLT_Stick_Management != null)
						{
							OLT_IP_Change_Notify oLT_IP_Change_Notify = new OLT_IP_Change_Notify();
							IP_Add_Change iP_Add_Change = new IP_Add_Change(this, comment, oLT_IP_Change_Notify);
							iP_Add_Change.Show();
							oLT_Stick_Management.ip_Add_Set.oLT_IP_Change_Notify_list.Push(oLT_IP_Change_Notify);
						}
						return;
					}
				}
				Textlog_With_Time("cannot push cpe list-add in OLT IP_Add=" + comment + ", since it's not active");
			}
			catch
			{
			}
		}

		private void shake_hand(OLT_Stick_Management working_OLT)
		{
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					working_OLT.shake_Hands.shake_hands_fun(mAC);
					Update_IP_OLT_Monitor(working_OLT);
				}
			}
			catch
			{
			}
		}

		private void password_check_ack(OLT_Stick_Management working_OLT)
		{
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					working_OLT.pWD_ACK.PWD_Check(mAC);
				}
			}
			catch
			{
			}
		}

		private async Task OLTPolling_Async(CancellationToken token)
		{
			while (true)
			{
				try
				{
					foreach (OLT_Stick_Management working_OLT in oLT_Stick_Managements_List)
					{
						bool Active = false;
						lock (Lock_oLT_Stick_Managements_List)
						{
							Active = working_OLT.oLT_share_data.active;
						}
						if (Active)
						{
							shake_hand(working_OLT);
							pwd_write(working_OLT);
							password_check_ack(working_OLT);
							if (working_OLT.oLT_share_data.olt_stick_Lifecycle == 1 && working_OLT.oLT_share_data.olt_Read_accessable_level > 0)
							{
								await Task.Delay(500);
								OLT_SDK_UPGRADE(working_OLT);
								await Task.Delay(20);
								OLT_IP_ADD_MODIFICATION(working_OLT);
								await Task.Delay(20);
								CPE_WHTIE_LIST_PUshing(working_OLT);
								await Task.Delay(20);
								CPE_SERVICETYPE_PUshing(working_OLT);
								await Task.Delay(20);
								ONU_SN_READING(working_OLT);
								await Task.Delay(20);
								await Task.Delay(20);
								OLT_STATUS_READING(working_OLT);
								await Task.Delay(20);
								ONU_WHITELST_READING(working_OLT);
								await Task.Delay(20);
								ONU_SERVICETYPE_READING(working_OLT);
								await Task.Delay(20);
							}
						}
					}
				}
				catch (Exception)
				{
				}
			}
		}

		private void RestartIfStopped()
		{
			Task runningTask = _runningTask;
			if (runningTask != null && runningTask.IsCompleted)
			{
				Textlog_With_Time("Restarting stopped polling task");
				StartProcessingTask();
			}
		}

		public void StartsavingTask(string myONU_Whitelst_Info)
		{
			string myONU_Whitelst_Info2 = myONU_Whitelst_Info;
			_cts1?.Cancel();
			_cts1 = new CancellationTokenSource();
			Task.Run(async delegate
			{
				await whitelistsaving(_cts1.Token, myONU_Whitelst_Info2);
			}, _cts1.Token);
		}

		private Task whitelistsaving(CancellationToken token, string myONU_Whitelst_Info)
		{
			string myONU_Whitelst_Info2 = myONU_Whitelst_Info;
			if (base.InvokeRequired)
			{
				Invoke(delegate
				{
					whitelistsaving(token, myONU_Whitelst_Info2);
				});
				return Task.CompletedTask;
			}
			using (SaveFileDialog saveFileDialog = new SaveFileDialog())
			{
				saveFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
				saveFileDialog.FilterIndex = 1;
				saveFileDialog.RestoreDirectory = true;
				saveFileDialog.Title = "Save Whitelist txt file";
				saveFileDialog.DefaultExt = "txt";
				saveFileDialog.AddExtension = true;
				if (saveFileDialog.ShowDialog(this) == DialogResult.OK)
				{
					try
					{
						File.WriteAllText(saveFileDialog.FileName, myONU_Whitelst_Info2);
						Textlog_With_Time("Save White List Success");
					}
					catch (Exception ex)
					{
						MessageBox.Show(this, "保存白名单失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Hand);
					}
				}
			}
			return Task.CompletedTask;
		}

		private void ONU_WHITELST_READING(OLT_Stick_Management working_OLT)
		{
			bool flag = false;
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					if (working_OLT.oNU_Whitelst_GET.read_immediately)
					{
						flag = true;
						Textlog_With_Time("*IP = " + working_OLT.oLT_share_data.iPAddress.ToString() + ": ONUs white list read start:");
					}
					int num = working_OLT.oNU_Whitelst_GET.onuwhitelst_read(mAC);
					switch (num)
					{
					case 0:
						break;
					default:
						Textlog_With_Time($"*IP = {working_OLT.oLT_share_data.iPAddress.ToString()}: ONUs white list read failed :error = {Math.Abs(num)}");
						break;
					case 1:
					{
						string text = "";
						text = text + "#OLT SN: " + working_OLT.oLT_share_data.OLT_SN.ToString() + " " + Environment.NewLine;
						text = text + "OLT_IP: " + working_OLT.oLT_share_data.iPAddress.ToString() + Environment.NewLine;
						text = text + "# CPE SN-Service Type --active-- CPE description" + Environment.NewLine;
						int count = working_OLT.oNU_Whitelst_GET.oNU_Whitelst_Info.cPE_Struct.Count;
						for (int i = 0; i < count; i++)
						{
							string value = "NO";
							if (working_OLT.oNU_Whitelst_GET.oNU_Whitelst_Info.cPE_Struct[i].active)
							{
								value = "YES";
							}
							text = text + $"{working_OLT.oNU_Whitelst_GET.oNU_Whitelst_Info.cPE_Struct[i].SNStr.ToString()}  {working_OLT.oNU_Whitelst_GET.oNU_Whitelst_Info.cPE_Struct[i].service_type.ToString()}  {value}  #polling back NO {i + 1}" + Environment.NewLine;
						}
						if (flag)
						{
							StartsavingTask(text);
						}
						break;
					}
					}
				}
			}
			catch
			{
			}
		}

		private void ONU_SERVICETYPE_READING(OLT_Stick_Management working_OLT)
		{
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					if (working_OLT.oNU_ServiceType_GET.read_immediately)
					{
						Textlog_With_Time("---ONU service type read start:");
					}
					int num = working_OLT.oNU_ServiceType_GET.onuservicetpye_read(mAC);
					if (num == 1)
					{
						ONU_Service_Type_Info_Stru oNU_Service_Type_Info_Stru = new ONU_Service_Type_Info_Stru();
						oNU_Service_Type_Info_Stru = working_OLT.oNU_ServiceType_GET.oNU_Service_Type_Info_List_stru;
						Saving_ServicetypeTask(oNU_Service_Type_Info_Stru, working_OLT.oLT_share_data.iPAddress.ToString());
					}
				}
			}
			catch
			{
			}
		}

		private void Saving_ServicetypeTask(ONU_Service_Type_Info_Stru oNU_Service_Type_Info_Stru, string ip_add)
		{
			string ip_add2 = ip_add;
			_cts1?.Cancel();
			_cts1 = new CancellationTokenSource();
			Task.Run(async delegate
			{
				await Save_onu_service_typetofolder(oNU_Service_Type_Info_Stru, ip_add2, _cts1.Token);
			}, _cts1.Token);
		}

		private Task Save_onu_service_typetofolder(ONU_Service_Type_Info_Stru oNU_Service_Type_Info_Stru, string ip_add, CancellationToken token)
		{
			string ip_add2 = ip_add;
			if (base.InvokeRequired)
			{
				Invoke(delegate
				{
					Save_onu_service_typetofolder(oNU_Service_Type_Info_Stru, ip_add2, token);
				});
				return Task.CompletedTask;
			}
			FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
			folderBrowserDialog.Description = "File Folder Seletion";
			folderBrowserDialog.ShowNewFolderButton = true;
			string text = DateTime.Now.ToString("yyyy-MM-dd");
			if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
			{
				string selectedPath = folderBrowserDialog.SelectedPath;
				for (int i = 0; i < oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List.Count; i++)
				{
					string text2 = ip_add2 + "_" + text + $"_Type_{i + 1}";
					string path = ip_add2 + "_" + text + $"_Type_{i + 1}.txt";
					string path2 = Path.Combine(selectedPath, path);
					string text3 = "";
					text3 = text3 + "#ONU service tpye template\r\n>ONU_service_name:    " + text2 + Environment.NewLine;
					text3 = text3 + $">ONU_service_number:  {oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].ONU_Service_Type_No}                             #very important, must be 0~255" + Environment.NewLine;
					text3 += Environment.NewLine;
					for (int j = 0; j < 5; j++)
					{
						byte onuid = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].onuid;
						byte wan_id = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].wan_id;
						ushort tcont_id = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].tcont_id;
						ushort vlan_id = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].vlan_id;
						ushort max = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].max;
						ushort fix = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].fix;
						ushort ass = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].ass;
						ushort gem_port = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].gem_port;
						byte type = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].type;
						byte priority = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].priority;
						byte weight = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].weight;
						byte valid = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].valid;
						ushort reserved = oNU_Service_Type_Info_Stru.oNU_Service_Type_Info_List[i].oNU_Service_Flows[j].Reserved;
						text3 = text3 + $"<flow {j + 1} start>" + Environment.NewLine;
						text3 = text3 + $"     >wan_id   {onuid}                   //0~255" + Environment.NewLine;
						text3 = text3 + $"     >tcont_id   {tcont_id}                   //range 0~4096" + Environment.NewLine;
						text3 = text3 + $"     >vlan_id   {vlan_id}                   //service difference" + Environment.NewLine;
						text3 = text3 + $"     >max_BD   {max}                   //max bandwith for this flow , Unit Mbit/s" + Environment.NewLine;
						text3 = text3 + $"     >fix_BD   {fix}                   //fixed bandwith for this flow , Unit Mbit/s" + Environment.NewLine;
						text3 = text3 + $"     >ass_BD   {ass}                   //assure bandwith for this flow ,Unit Mbit/s" + Environment.NewLine;
						text3 = text3 + $"     >gem_port   {gem_port}                   //range 0~4096" + Environment.NewLine;
						text3 = text3 + $"     >type   {type}                   //service type" + Environment.NewLine;
						text3 = text3 + $"     >priority   {priority}                   //priority" + Environment.NewLine;
						text3 = text3 + $"     >weight   {weight}                   //range" + Environment.NewLine;
						text3 = text3 + $"     >valid   {valid}                   //1: valid; 0: invalid" + Environment.NewLine;
						text3 = text3 + $"     >reserved   {reserved}                   //reserved for future use" + Environment.NewLine;
						text3 = text3 + "<end>" + Environment.NewLine;
						text3 += Environment.NewLine;
					}
					text3 = text3 + "#end of service type definition" + Environment.NewLine;
					try
					{
						File.WriteAllText(path2, text3);
					}
					catch (Exception ex)
					{
						MessageBox.Show(this, "save failed: " + ex.Message, "Fault", MessageBoxButtons.OK, MessageBoxIcon.Hand);
					}
				}
			}
			return Task.CompletedTask;
		}

		private void OLT_STATUS_READING(OLT_Stick_Management working_OLT)
		{
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					if (working_OLT.oLT_STATUS_GET.read_immediately)
					{
						Textlog_With_Time("---OLT status read start:");
					}
					int num = working_OLT.oLT_STATUS_GET.OLT_status_read(mAC);
					if (num == 1)
					{
						OLT_Status_Report oLT_Status_Report = new OLT_Status_Report();
						if (working_OLT.oLT_STATUS_GET.oLT_Status_Report_stack.TryPop(out OLT_Status_Report result))
						{
							oLT_Status_Report = result;
							Update_IP_OLT_Monitor(oLT_Status_Report, working_OLT);
						}
					}
				}
			}
			catch
			{
			}
		}

		private void ONU_ALARM_READING(OLT_Stick_Management working_OLT)
		{
			if (working_OLT.oNU_Alarm_GET.read_immediately)
			{
				Textlog_With_Time("---ONUs Alarm read start:");
			}
			int num = working_OLT.oNU_Alarm_GET.onualarm_read(mAC);
			if (num == 1)
			{
			}
		}

		private void ONU_SN_READING(OLT_Stick_Management working_OLT)
		{
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					if (working_OLT.oNU_SN_STATUS_GET.read_immediately)
					{
						Textlog_With_Time("---ONUs status read start:");
					}
					int num = working_OLT.oNU_SN_STATUS_GET.onusn_read(mAC);
					if (num == 1 && working_OLT.oNU_SN_STATUS_GET.oNU_STATUS_SNs.TryPop(out ONU_STATUS_SN_list result))
					{
						int count = result.oNU_STATUS_SNs_List.Count;
						for (int i = 0; i < count; i++)
						{
							string onusn = HexStringParser.ByteArrayToString(result.oNU_STATUS_SNs_List[i].ONU_SN);
							string current_status = ONU_OP.ONU_status_GEN(result.oNU_STATUS_SNs_List[i].onu_state);
							addonu_sn_ONU_Monitor(working_OLT.oLT_share_data.iPAddress.ToString(), onusn, current_status);
						}
					}
				}
			}
			catch
			{
			}
		}

		private void CPE_SERVICETYPE_PUshing(OLT_Stick_Management working_OLT)
		{
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					if (!working_OLT.cPE_ServiceType_Set.oNU_Service_Type_Change_Notify_stack.IsEmpty && working_OLT.cPE_ServiceType_Set.oNU_Service_Type_Change_Notify_stack.TryPeek(out ONU_Service_Type_Change_Notify result) && result != null && result.Change_Notify && result.oNU_Service_Type_Info.oNU_Service_Flows.Count >= 1)
					{
						Textlog_With_Time("***OLT IP =" + working_OLT.oLT_share_data.iPAddress.ToString() + "start ONU Service Type upgrade------, please wait ......");
						int num = working_OLT.cPE_ServiceType_Set.servicetype_push(mAC);
						if (num == 1)
						{
							Textlog_With_Time("ONU Service Type pushing success, OK...");
						}
						else
						{
							Textlog_With_Time($"ONU Service Type pushing failed :error = {Math.Abs(num)}");
						}
						working_OLT.shake_Hands.last_reading_package_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					}
				}
			}
			catch
			{
			}
		}

		private void CPE_WHTIE_LIST_PUshing(OLT_Stick_Management working_OLT)
		{
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					if (!working_OLT.cPE_WhiteLst_Set.cpe_WHITE_LIST_Change_Notify_stack.IsEmpty && working_OLT.cPE_WhiteLst_Set.cpe_WHITE_LIST_Change_Notify_stack.TryPeek(out CPE_WHITE_LIST_Change_Notify result) && result != null && result.Change_Notify && result.oNU_Whitelst_Info.cPE_Struct.Count >= 1)
					{
						result.Change_Notify = false;
						Textlog_With_Time("***OLT IP =" + working_OLT.oLT_share_data.iPAddress.ToString() + "start ONU white list upgrade------, please wait ......");
						int num = working_OLT.cPE_WhiteLst_Set.whitelst_push(mAC);
						switch (num)
						{
						case 1:
							Textlog_With_Time("ONU white list SN pushing success, OK...");
							break;
						case 2:
						{
							string text = HexStringParser.ByteArrayToString(working_OLT.cPE_WhiteLst_Set.Last_whitelist_SN_Outofrange);
							Textlog_With_Time("ONU white list SN pushing error, the white list number is great than the max., please check, the last ONU = " + text);
							break;
						}
						default:
							Textlog_With_Time($"ONU white list SN pushing failed :error = {Math.Abs(num)}");
							break;
						}
						working_OLT.shake_Hands.last_reading_package_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					}
				}
			}
			catch
			{
			}
		}

		private void OLT_IP_ADD_MODIFICATION(OLT_Stick_Management working_OLT)
		{
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					if (working_OLT.ip_Add_Set.oLT_IP_Change_Notify_list.IsEmpty || !working_OLT.ip_Add_Set.oLT_IP_Change_Notify_list.TryPeek(out OLT_IP_Change_Notify result) || result == null || result.OLT_IP_OLD == IPAddress.None || result.OLT_IP_NEW == IPAddress.None)
					{
						return;
					}
					Textlog_With_Time("***OLT IP =" + working_OLT.oLT_share_data.iPAddress.ToString() + "start SDK upgrade------, please wait ......");
					int num = working_OLT.ip_Add_Set.ip_change(mAC);
					if (num == 1)
					{
						Textlog_With_Time("Ip set success, OK...");
						using SQLiteConnection sQLiteConnection = new SQLiteConnection(connectionString);
						sQLiteConnection.Open();
						string commandText = $"update OLT_Information_List set IP_Add = '{result.OLT_IP_NEW.ToString()}' WHERE IP_Add = '{result.OLT_IP_OLD.ToString()}'";
						using SQLiteCommand sQLiteCommand = new SQLiteCommand(commandText, sQLiteConnection);
						int num2 = sQLiteCommand.ExecuteNonQuery();
					}
					else
					{
						Textlog_With_Time($"Ip set failed :error = {Math.Abs(num)}");
					}
					working_OLT.shake_Hands.last_reading_package_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
				}
			}
			catch
			{
			}
		}

		private void OLT_SDK_UPGRADE(OLT_Stick_Management working_OLT)
		{
			bool flag = false;
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					if (!working_OLT.in_band_FW_Upgrade.oLT_SDK_Upgrade_Notify.IsEmpty)
					{
						flag = true;
						timer_ONU_optics_read.Enabled = false;
						Textlog_With_Time("***OLT IP =" + working_OLT.oLT_share_data.iPAddress.ToString() + ", start SDK upgrade------, please wait ......");
						int num = working_OLT.in_band_FW_Upgrade.FW_download_onestop_fun(mAC);
						if (num == 0)
						{
							Textlog_With_Time("SDK upgrade success, OK...");
						}
						else
						{
							Textlog_With_Time($"SDK upgrade failed :error = {Math.Abs(num)}");
						}
						working_OLT.shake_Hands.last_reading_package_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					}
				}
			}
			catch
			{
			}
			finally
			{
				if (flag)
				{
					if (base.InvokeRequired)
					{
						Invoke(delegate
						{
							timer_ONU_optics_read.Interval = 5000;
							timer_ONU_optics_read.Enabled = true;
						});
					}
					else
					{
						timer_ONU_optics_read.Interval = 5000;
						timer_ONU_optics_read.Enabled = true;
					}
				}
			}
		}

		private void pwd_write(OLT_Stick_Management working_OLT)
		{
			bool flag = false;
			bool flag2 = false;
			int olt_Write_accessable_level = working_OLT.oLT_share_data.olt_Write_accessable_level;
			int olt_Read_accessable_level = working_OLT.oLT_share_data.olt_Read_accessable_level;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (totalSeconds - last_password_write_time > 120.0)
			{
				last_password_write_time = totalSeconds;
				working_OLT.oLT_share_data.olt_Write_accessable_level = -1;
			}
			try
			{
				lock (MacSendFrameGlobalLock)
				{
					if (OLT_Write_ToolStripMenuItem.Text == "Write_Disable")
					{
						working_OLT.pWD_Set.change_W = 87;
					}
					else
					{
						if (!(OLT_Write_ToolStripMenuItem.Text == "Write_Enable"))
						{
							working_OLT.pWD_Set.change_W = 0;
							UI_Write_access_Flag = working_OLT.oLT_share_data.olt_Write_accessable_level;
							UI_Read_access_Flag = working_OLT.oLT_share_data.olt_Read_accessable_level;
							return;
						}
						working_OLT.pWD_Set.change_W = 119;
					}
					if (working_OLT.oLT_share_data.olt_Write_accessable_level == 1 && working_OLT.pWD_Set.change_W == 119)
					{
						flag = false;
						flag2 = true;
					}
					else if (working_OLT.oLT_share_data.olt_Write_accessable_level != 1 && working_OLT.pWD_Set.change_W == 87)
					{
						flag = true;
						flag2 = true;
					}
					else if (working_OLT.oLT_share_data.olt_Write_accessable_level == -1)
					{
						if (working_OLT.pWD_Set.change_W == 119)
						{
							flag = false;
							flag2 = true;
						}
						else
						{
							flag = true;
							flag2 = true;
						}
					}
					else
					{
						flag = false;
						flag2 = false;
					}
					if (working_OLT.oLT_share_data.olt_stick_Lifecycle == 1 && flag2)
					{
						working_OLT.pWD_Set.Vendor_Read_Key = Vendor_Read_Key;
						working_OLT.pWD_Set.Vendor_Write_Key = Vendor_Write_Key;
						int num = working_OLT.pWD_Set.PWD_Change(mAC, flag, 1);
						switch (num)
						{
						case 0:
							if (flag)
							{
								if (working_OLT.pWD_Set.change_W == 87 && olt_Write_accessable_level != 1)
								{
									Textlog_With_Time("Write access, OK...");
								}
							}
							else if (working_OLT.pWD_Set.change_W == 119 && olt_Write_accessable_level == 1)
							{
								Textlog_With_Time("disable write access, OK...");
							}
							break;
						default:
							if (writeerror == 0)
							{
								Textlog_With_Time($"access operation failed :error = {Math.Abs(num)}");
							}
							writeerror++;
							if (writeerror > 400)
							{
								writeerror = 0;
							}
							break;
						case 1:
							break;
						}
						UI_Write_access_Flag = working_OLT.oLT_share_data.olt_Write_accessable_level;
						UI_Read_access_Flag = working_OLT.oLT_share_data.olt_Read_accessable_level;
						working_OLT.shake_Hands.last_reading_package_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					}
					totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					if (totalSeconds - last_password_write_time > 120.0)
					{
						last_password_write_time = totalSeconds;
						working_OLT.oLT_share_data.olt_Read_accessable_level = -1;
					}
					flag = false;
					flag2 = false;
					if (OLT_Read_ToolStripMenuItem.Text == "Read_Disable")
					{
						working_OLT.pWD_Set.change_R = 82;
					}
					else
					{
						if (!(OLT_Read_ToolStripMenuItem.Text == "Read_Enable"))
						{
							working_OLT.pWD_Set.change_R = 0;
							UI_Write_access_Flag = working_OLT.oLT_share_data.olt_Write_accessable_level;
							UI_Read_access_Flag = working_OLT.oLT_share_data.olt_Read_accessable_level;
							return;
						}
						working_OLT.pWD_Set.change_R = 114;
					}
					if (working_OLT.oLT_share_data.olt_Read_accessable_level != 1 && working_OLT.pWD_Set.change_R == 82)
					{
						flag = true;
						flag2 = true;
					}
					else if (working_OLT.oLT_share_data.olt_Read_accessable_level == 1 && working_OLT.pWD_Set.change_R == 114)
					{
						flag = false;
						flag2 = true;
					}
					else
					{
						if (working_OLT.oLT_share_data.olt_Read_accessable_level != -1)
						{
							flag = false;
							flag2 = false;
							UI_Write_access_Flag = working_OLT.oLT_share_data.olt_Write_accessable_level;
							UI_Read_access_Flag = working_OLT.oLT_share_data.olt_Read_accessable_level;
							return;
						}
						if (working_OLT.pWD_Set.change_R == 114)
						{
							flag = false;
							flag2 = true;
						}
						else
						{
							flag = true;
							flag2 = true;
						}
					}
					if (working_OLT.oLT_share_data.olt_stick_Lifecycle == 1 && flag2)
					{
						working_OLT.pWD_Set.Vendor_Read_Key = Vendor_Read_Key;
						working_OLT.pWD_Set.Vendor_Write_Key = Vendor_Write_Key;
						int num2 = working_OLT.pWD_Set.PWD_Change(mAC, flag, 2);
						switch (num2)
						{
						case 0:
							if (flag)
							{
								if (working_OLT.pWD_Set.change_R == 82 && olt_Read_accessable_level != 1)
								{
									Textlog_With_Time("Read access, OK...");
								}
							}
							else if (working_OLT.pWD_Set.change_R == 114 && olt_Read_accessable_level != 1)
							{
								Textlog_With_Time("disable read access, OK...");
							}
							break;
						default:
							if (readerror == 0)
							{
								Textlog_With_Time($"access operation failed :error = {Math.Abs(num2)}");
							}
							readerror++;
							if (readerror > 400)
							{
								readerror = 0;
							}
							break;
						case 1:
							break;
						}
						working_OLT.shake_Hands.last_reading_package_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					}
				}
			}
			catch
			{
			}
			UI_Write_access_Flag = working_OLT.oLT_share_data.olt_Write_accessable_level;
			UI_Read_access_Flag = working_OLT.oLT_share_data.olt_Read_accessable_level;
		}

		private void Form1_Load(object sender, EventArgs e)
		{
		}

		private void dataGridView_OLT_Online_List_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
			{
				dataGridView_OLT_Online_List.CurrentCell = dataGridView_OLT_Online_List.Rows[e.RowIndex].Cells[e.ColumnIndex];
				dataGridView_OLT_Online_List.Tag = e.RowIndex;
			}
		}

		private void oLTSDKUpgradeToolStripMenuItem_Click(object sender, EventArgs e)
		{
			contextMenuStrip_OLT_Check.Close();
			bool flag = true;
			try
			{
				if (dataGridView_OLT_Online_List.CurrentCell.Value == null)
				{
					return;
				}
			}
			catch
			{
				MessageBox.Show("please select OLT IP address");
				return;
			}
			try
			{
				int num = 0;
				if (dataGridView_OLT_Online_List.Tag == null)
				{
					return;
				}
				num = (int)dataGridView_OLT_Online_List.Tag;
				DataGridViewRow dataGridViewRow = dataGridView_OLT_Online_List.Rows[num];
				string comment = "";
				object value = dataGridView_OLT_Online_List.Rows[num].Cells["olt_ip"].Value;
				if (value == null)
				{
					return;
				}
				comment = value.ToString();
				IPAddress.Parse(comment);
				int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management a) => a.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
				if (num2 != -1)
				{
					if (oLT_Stick_Managements_List[num2].oLT_share_data.status_int != 0)
					{
						OLT_SDK_Upgrade_Notify oLT_SDK_Upgrade_Notify = new OLT_SDK_Upgrade_Notify();
						try
						{
							using OpenFileDialog openFileDialog = new OpenFileDialog();
							openFileDialog.Filter = "Text Files (*.bin)|*.bin|All Files (*.*)|*.*";
							if (openFileDialog.ShowDialog() == DialogResult.OK)
							{
								string fileName = openFileDialog.FileName;
								byte[] bin_file = File.ReadAllBytes(fileName);
								oLT_SDK_Upgrade_Notify.bin_file = bin_file;
								long length = new FileInfo(fileName).Length;
								if (length > 550000)
								{
									oLT_SDK_Upgrade_Notify.FW_Package_type = "FPGA_GE";
								}
								else
								{
									oLT_SDK_Upgrade_Notify.FW_Package_type = "M4";
								}
							}
						}
						catch (Exception ex)
						{
							MessageBox.Show(ex.Message);
							Textlog_With_Time("Bin file reading: failed");
							return;
						}
						oLT_SDK_Upgrade_Notify.iPAddress = IPAddress.Parse(comment);
						oLT_SDK_Upgrade_Notify.Change_Notify = true;
						lock (Lock_oLT_Stick_Managements_List)
						{
							oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(oLT_SDK_Upgrade_Notify.iPAddress))?.in_band_FW_Upgrade.oLT_SDK_Upgrade_Notify.Push(oLT_SDK_Upgrade_Notify);
							return;
						}
					}
					Textlog_With_Time("cannot upgrade the SDK in OLT IP_Add=" + comment + ", since it's not active");
				}
				else
				{
					Textlog_With_Time("cannot upgrade the SDKt in OLT IP_Add=" + comment + ", since it's not active");
				}
			}
			catch
			{
				Textlog_With_Time("failed to SDK upgrade");
				MessageBox.Show("failed to SDK upgrade");
			}
		}

		public void set_service_type(IPAddress ip)
		{
		}

		private void oLTIPAddChangeToolStripMenuItem_Click(object sender, EventArgs e)
		{
		}

		private void contextMenuStrip_OLT_Check_Opening(object sender, CancelEventArgs e)
		{
		}

		private void oLTIPAddChangeToolStripMenuItem_Click_1(object sender, EventArgs e)
		{
		}

		private void deleteToolStripMenuItem_Click(object sender, EventArgs e)
		{
			contextMenuStrip_OLT_Check.Close();
			bool flag = true;
			try
			{
				if (dataGridView_OLT_Online_List.CurrentCell.Value == null)
				{
					return;
				}
			}
			catch
			{
				MessageBox.Show("please select OLT IP address");
				return;
			}
			try
			{
				int num = 0;
				if (dataGridView_OLT_Online_List.Tag == null)
				{
					return;
				}
				num = (int)dataGridView_OLT_Online_List.Tag;
				DataGridViewRow dataGridViewRow = dataGridView_OLT_Online_List.Rows[num];
				string text = "";
				object value = dataGridView_OLT_Online_List.Rows[num].Cells["olt_ip"].Value;
				if (value == null)
				{
					return;
				}
				text = value.ToString();
				IPAddress.Parse(text);
				using SQLiteConnection sQLiteConnection = new SQLiteConnection(connectionString);
				sQLiteConnection.Open();
				string commandText = "delete from OLT_Information_List WHERE IP_Add = '" + text + "'";
				using SQLiteCommand sQLiteCommand = new SQLiteCommand(commandText, sQLiteConnection);
				int num2 = sQLiteCommand.ExecuteNonQuery();
			}
			catch
			{
			}
		}

		private void oNUStatusToolStripMenuItem_Click(object sender, EventArgs e)
		{
			contextMenuStrip_OLT_Check.Close();
			try
			{
				if (dataGridView_OLT_Online_List.CurrentCell.Value == null)
				{
					return;
				}
			}
			catch
			{
				MessageBox.Show("please select OLT IP address");
				return;
			}
			try
			{
				int num = 0;
				if (dataGridView_OLT_Online_List.Tag == null)
				{
					return;
				}
				num = (int)dataGridView_OLT_Online_List.Tag;
				DataGridViewRow dataGridViewRow = dataGridView_OLT_Online_List.Rows[num];
				string comment = "";
				object value = dataGridView_OLT_Online_List.Rows[num].Cells["olt_ip"].Value;
				if (value != null)
				{
					comment = value.ToString();
					IPAddress.Parse(comment);
					int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management a) => a.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
					if (num2 == -1)
					{
						Textlog_With_Time("cannot set ONU service in OLT IP_Add=" + comment + ", since it's not active");
						return;
					}
					lock (Lock_oLT_Stick_Managements_List)
					{
						OLT_Stick_Management oLT_Stick_Management = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
						if (oLT_Stick_Management != null)
						{
							oLT_Stick_Management.oNU_SN_STATUS_GET.read_immediately = true;
							oLT_Stick_Management.oNU_Alarm_GET.read_immediately = true;
						}
					}
					CPE_Online cPE_Online = new CPE_Online(this, dbPath, comment, "status");
					cPE_Online.Show();
				}
				else
				{
					MessageBox.Show("please click one OLT!");
				}
			}
			catch
			{
				Textlog_With_Time("failed to polling ONU status upgrade");
			}
		}

		public void ONU_SN_Status_rpt(IPAddress olt_ip, out ONU_STATUS_SN_list oNU_STATUS_SN_List_temp, out ONU_Alarm_list oNU_alarm_List_temp)
		{
			IPAddress olt_ip2 = olt_ip;
			oNU_STATUS_SN_List_temp = new ONU_STATUS_SN_list();
			oNU_alarm_List_temp = new ONU_Alarm_list();
			lock (Lock_oLT_Stick_Managements_List)
			{
				OLT_Stick_Management oLT_Stick_Management = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(olt_ip2));
				if (oLT_Stick_Management != null && oLT_Stick_Management.oNU_SN_STATUS_GET.Current_ONU_STATUS_SN_list != null)
				{
					oNU_STATUS_SN_List_temp = oLT_Stick_Management.oNU_SN_STATUS_GET.Current_ONU_STATUS_SN_list;
				}
			}
		}

		public void OLT_performance_report_UI(IPAddress olt_ip, out OLT_Status_Report oLT_Status_Report_temp)
		{
			IPAddress olt_ip2 = olt_ip;
			oLT_Status_Report_temp = new OLT_Status_Report();
			lock (Lock_oLT_Stick_Managements_List)
			{
				OLT_Stick_Management oLT_Stick_Management = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(olt_ip2));
				if (oLT_Stick_Management != null && oLT_Stick_Management.oLT_STATUS_GET.oLT_Status_Report_stack.TryPop(out OLT_Status_Report result))
				{
					oLT_Status_Report_temp = result;
					oLT_Status_Report_temp.olt_ip = oLT_Stick_Management.oLT_share_data.iPAddress;
					oLT_Status_Report_temp.olt_sn = oLT_Stick_Management.oLT_share_data.OLT_SN;
				}
			}
		}

		private void pushing2OLTToolStripMenuItem_Click(object sender, EventArgs e)
		{
			contextMenuStrip_OLT_Check.Close();
			bool flag = true;
			try
			{
				if (dataGridView_OLT_Online_List.CurrentCell.Value == null)
				{
					return;
				}
			}
			catch
			{
				MessageBox.Show("please select OLT IP address");
				return;
			}
			try
			{
				int num = 0;
				if (dataGridView_OLT_Online_List.Tag == null)
				{
					return;
				}
				num = (int)dataGridView_OLT_Online_List.Tag;
				DataGridViewRow dataGridViewRow = dataGridView_OLT_Online_List.Rows[num];
				string comment = "";
				object value = dataGridView_OLT_Online_List.Rows[num].Cells["olt_ip"].Value;
				if (value == null)
				{
					return;
				}
				comment = value.ToString();
				int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management a) => a.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
				if (num2 == -1)
				{
					Textlog_With_Time("cannot push cpe list in OLT IP_Add=" + comment + ", since it's not active");
					return;
				}
				IPAddress.Parse(comment);
				CPE_White_list_OP cPE_White_list_OP = new CPE_White_list_OP(dbPath);
				if (cPE_White_list_OP.File_read() != 0)
				{
					return;
				}
				CPE_WHITE_LIST_Change_Notify cPE_WHITE_LIST_Change_Notify = new CPE_WHITE_LIST_Change_Notify();
				lock (Lock_oLT_Stick_Managements_List)
				{
					OLT_Stick_Management oLT_Stick_Management = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
					DialogResult dialogResult = MessageBox.Show("ONU SN pushing confirmation？", comment + " White List SN Pushing", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
					if (dialogResult == DialogResult.No)
					{
						Textlog_With_Time("OLTip=" + comment + " white sn pushing cancelled");
						return;
					}
					cPE_WHITE_LIST_Change_Notify.Change_Notify = true;
					cPE_WHITE_LIST_Change_Notify.oNU_Whitelst_Info = cPE_White_list_OP.oNU_Whitelst_Info;
					oLT_Stick_Management?.cPE_WhiteLst_Set.cpe_WHITE_LIST_Change_Notify_stack.Push(cPE_WHITE_LIST_Change_Notify);
				}
			}
			catch (Exception ex)
			{
				Textlog_With_Time(ex.Message + ", ----2137");
			}
		}

		private void pushingtoOLTToolStripMenuItem_Click(object sender, EventArgs e)
		{
			contextMenuStrip_OLT_Check.Close();
			bool flag = true;
			try
			{
				if (dataGridView_OLT_Online_List.CurrentCell.Value == null)
				{
					return;
				}
			}
			catch
			{
				MessageBox.Show("please select OLT IP address");
				return;
			}
			try
			{
				int num = 0;
				if (dataGridView_OLT_Online_List.Tag == null)
				{
					return;
				}
				num = (int)dataGridView_OLT_Online_List.Tag;
				DataGridViewRow dataGridViewRow = dataGridView_OLT_Online_List.Rows[num];
				string comment = "";
				object value = dataGridView_OLT_Online_List.Rows[num].Cells["olt_ip"].Value;
				if (value != null)
				{
					comment = value.ToString();
					IPAddress.Parse(comment);
					int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management a) => a.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
					if (num2 == -1)
					{
						Textlog_With_Time("cannot set ONU service in OLT IP_Add=" + comment + ", since it's not active");
						return;
					}
					ONU_Service_Type_Parse oNU_Service_Type_Parse = new ONU_Service_Type_Parse(dbPath, IPAddress.Parse(comment));
					if (oNU_Service_Type_Parse.File_read() != 0)
					{
						return;
					}
					ONU_Service_Type_Change_Notify oNU_Service_Type_Change_Notify = new ONU_Service_Type_Change_Notify();
					lock (Lock_oLT_Stick_Managements_List)
					{
						OLT_Stick_Management oLT_Stick_Management = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
						DialogResult dialogResult = MessageBox.Show("ONU Service change confirmation？", comment + "ONU Service Pushing", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
						if (dialogResult == DialogResult.No)
						{
							Textlog_With_Time("OLTip=" + comment + " ONU Service pushing cancelled");
							return;
						}
						oNU_Service_Type_Change_Notify.Change_Notify = true;
						oNU_Service_Type_Change_Notify.oNU_Service_Type_Info = oNU_Service_Type_Parse.oNU_Service_Type_Info;
						oLT_Stick_Management?.cPE_ServiceType_Set.oNU_Service_Type_Change_Notify_stack.Push(oNU_Service_Type_Change_Notify);
						return;
					}
				}
				MessageBox.Show("please click one OLT!");
			}
			catch
			{
				Textlog_With_Time("failed to ONU service upgrade");
			}
		}

		private void pollingToolStripMenuItem_Click(object sender, EventArgs e)
		{
			contextMenuStrip_OLT_Check.Close();
			bool flag = true;
			try
			{
				if (dataGridView_OLT_Online_List.CurrentCell.Value == null)
				{
					return;
				}
			}
			catch
			{
				MessageBox.Show("please select OLT IP address");
				return;
			}
			try
			{
				int num = 0;
				if (dataGridView_OLT_Online_List.Tag == null)
				{
					return;
				}
				num = (int)dataGridView_OLT_Online_List.Tag;
				DataGridViewRow dataGridViewRow = dataGridView_OLT_Online_List.Rows[num];
				string comment = "";
				object value = dataGridView_OLT_Online_List.Rows[num].Cells["olt_ip"].Value;
				if (value != null)
				{
					comment = value.ToString();
					IPAddress.Parse(comment);
					int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management a) => a.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
					if (num2 != -1)
					{
						lock (Lock_oLT_Stick_Managements_List)
						{
							OLT_Stick_Management oLT_Stick_Management = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
							DialogResult dialogResult = MessageBox.Show("ONU Read white list confirmation？", comment + "ONU White List Polling", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
							if (dialogResult == DialogResult.No)
							{
								Textlog_With_Time("OLTip=" + comment + " ONU white list polling cancelled");
							}
							else if (oLT_Stick_Management != null)
							{
								oLT_Stick_Management.oNU_Whitelst_GET.read_immediately = true;
							}
							return;
						}
					}
					Textlog_With_Time("cannot set ONU service in OLT IP_Add=" + comment + ", since it's not active");
				}
				else
				{
					MessageBox.Show("please click one OLT!");
				}
			}
			catch
			{
				Textlog_With_Time("failed to read ONU white list");
			}
		}

		private void Update_IP_OLT_Monitor(OLT_Status_Report oLT_Status_Report, OLT_Stick_Management olt_instant)
		{
			OLT_Status_Report oLT_Status_Report2 = oLT_Status_Report;
			try
			{
				OLT_Performance_Notity oLT_Performance_Notity = OLT_AppData.Users.FirstOrDefault((OLT_Performance_Notity olt) => olt != null && olt.olt_ip != null && olt.olt_ip.Equals(oLT_Status_Report2.olt_ip));
				if (oLT_Performance_Notity != null)
				{
					oLT_Performance_Notity.olt_status = olt_instant.oLT_share_data.Current_status;
					oLT_Performance_Notity.olt_rssi = oLT_Status_Report2.rx_power;
					oLT_Performance_Notity.olt_temperature = oLT_Status_Report2.temperature;
					oLT_Performance_Notity.olt_voltage = oLT_Status_Report2.voltage;
					oLT_Performance_Notity.olt_tx_pwr = oLT_Status_Report2.tx_power;
					oLT_Performance_Notity.olt_bias = oLT_Status_Report2.tx_bias;
					oLT_Performance_Notity.alarm_status = oLT_Status_Report2.alarm.ToString();
					oLT_Performance_Notity.olt_sn = oLT_Status_Report2.olt_sn;
					oLT_Performance_Notity.time_second = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					oLT_Performance_Notity.time_string = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
				}
			}
			catch
			{
			}
		}

		private void Update_IP_OLT_Monitor(OLT_Stick_Management olt_instant)
		{
			OLT_Stick_Management olt_instant2 = olt_instant;
			try
			{
				OLT_Performance_Notity oLT_Performance_Notity = OLT_AppData.Users.FirstOrDefault((OLT_Performance_Notity olt) => olt != null && olt.olt_ip != null && olt.olt_ip.Equals(olt_instant2.oLT_share_data.iPAddress));
				if (oLT_Performance_Notity != null)
				{
					if (oLT_Performance_Notity.olt_status != olt_instant2.oLT_share_data.Current_status)
					{
						oLT_Performance_Notity.needsaverightnow = "YES";
					}
					oLT_Performance_Notity.olt_status = olt_instant2.oLT_share_data.Current_status;
					oLT_Performance_Notity.olt_sn = olt_instant2.oLT_share_data.OLT_SN;
					oLT_Performance_Notity.time_second = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					oLT_Performance_Notity.time_string = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
				}
			}
			catch
			{
			}
		}

		public void Speical_status_onu_sn_ONU_Monitor(string oltsn, string onusn, string Current_status)
		{
			string onusn2 = onusn;
			try
			{
				ONU_Performance_Notity oNU_Performance_Notity = AppData.Users.FirstOrDefault((ONU_Performance_Notity onu) => onu != null && onu.onu_sn != null && onu.onu_sn.Equals(onusn2));
				if (oNU_Performance_Notity == null)
				{
					ONU_Performance_Notity newONU = new ONU_Performance_Notity
					{
						olt_ip = IPAddress.Parse(oltsn),
						onu_sn = (onusn2 ?? ""),
						onu_status = Current_status,
						onu_rx_pwr = "-inf",
						onu_tx_pwr = "-inf",
						onu_temperature = "-inf",
						time_second = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds,
						time_string = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
						needsaverightnow = "YES"
					};
					if (base.InvokeRequired)
					{
						Invoke(delegate
						{
							AppData.AddONU(newONU);
						});
					}
					else
					{
						AppData.AddONU(newONU);
					}
				}
				else
				{
					oNU_Performance_Notity.onu_status = Current_status;
					if (!oNU_Performance_Notity.olt_ip.Equals(IPAddress.Parse(oltsn)))
					{
						oNU_Performance_Notity.olt_ip = IPAddress.Parse(oltsn);
					}
					if (oNU_Performance_Notity.onu_status == Current_status)
					{
						oNU_Performance_Notity.needsaverightnow = "YES";
					}
					oNU_Performance_Notity.time_second = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					oNU_Performance_Notity.time_string = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
				}
			}
			catch
			{
			}
		}

		public void addonu_sn_ONU_Monitor(string oltsn, string onusn, string Current_status)
		{
			string onusn2 = onusn;
			try
			{
				ONU_Performance_Notity oNU_Performance_Notity = AppData.Users.FirstOrDefault((ONU_Performance_Notity onu) => onu != null && onu.onu_sn != null && onu.onu_sn.Equals(onusn2));
				if (oNU_Performance_Notity == null)
				{
					ONU_Performance_Notity newONU = new ONU_Performance_Notity
					{
						olt_ip = IPAddress.Parse(oltsn),
						onu_sn = (onusn2 ?? ""),
						onu_status = Current_status,
						onu_rx_pwr = "-inf",
						onu_tx_pwr = "-inf",
						onu_temperature = "-inf",
						time_second = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds,
						time_string = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
						needsaverightnow = "YES"
					};
					if (base.InvokeRequired)
					{
						Invoke(delegate
						{
							AppData.AddONU(newONU);
						});
					}
					else
					{
						AppData.AddONU(newONU);
					}
				}
				else
				{
					if (!oNU_Performance_Notity.olt_ip.Equals(IPAddress.Parse(oltsn)))
					{
						oNU_Performance_Notity.olt_ip = IPAddress.Parse(oltsn);
					}
					if (oNU_Performance_Notity.onu_status != Current_status)
					{
						oNU_Performance_Notity.needsaverightnow = "YES";
					}
					oNU_Performance_Notity.onu_status = Current_status;
				}
			}
			catch
			{
			}
			lock (Lock_Save_database)
			{
				databaseHelper.SaveOnuList(AppData.Save2databaseImmediatelylist());
			}
		}

		private int Manual_Ethernet_Frame_Send(string Selected_OLT_address, string onusn, MAC mymAC)
		{
			string text = "get_onu_optics(\"" + onusn + "\")";
			byte[] array = ConvertStringToHexArray(text);
			byte b = 65;
			int num = 0;
			if (b == 65)
			{
				byte[] array2 = new byte[array.Length + 3];
				Array.Copy(array, 0, array2, 3, array.Length);
				array2[0] = b;
				try
				{
					ushort upd_ID = 0;
					lock (Lock_Frame_Sent_ID_Code)
					{
						Frame_Sent_ID_Code_main = (ushort)(Frame_Sent_ID_Code_main++ % 16383);
						upd_ID = Frame_Sent_ID_Code_main;
					}
					UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
					array2[1] = (byte)(Frame_Sent_ID_Code_main >> 8);
					array2[2] = (byte)(Frame_Sent_ID_Code_main & 0xFFu);
					uDP_Analysis_Package.content = array2;
					uDP_Analysis_Package.task_owner = Task_Owner.Manual_send;
					uDP_Analysis_Package.time_window = 10.0;
					uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
					uDP_Analysis_Package.SenderProtocolAddress = IPAddress.Parse(Selected_OLT_address);
					int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management frame) => frame.oLT_share_data.iPAddress.ToString() == uDP_Analysis_Package.SenderProtocolAddress.ToString());
					if (num2 < 0)
					{
						return -1;
					}
					uDP_Analysis_Package.SenderHardwareAddress = oLT_Stick_Managements_List[num2].oLT_share_data.physicalAddress;
					uDP_Analysis_Package.cmd_Code = (Command_Code)array2[0];
					uDP_Analysis_Package.upd_ID = upd_ID;
					Send_Package_List.Add(uDP_Analysis_Package);
					try
					{
						mymAC.SendFrame(array2, uDP_Analysis_Package.SenderHardwareAddress, uDP_Analysis_Package.SenderProtocolAddress);
					}
					catch
					{
						num = -1;
					}
					if (num != 0)
					{
						MessageBox.Show("cannot connect with OLT！");
					}
					else
					{
						Textlog_With_Time("Send command --" + text);
					}
				}
				catch
				{
				}
			}
			return num;
		}

		private static float ExtractFloat(Regex regex, string input)
		{
			Match match = regex.Match(input);
			if (match.Success && float.TryParse(match.Value, out var result))
			{
				return result;
			}
			return -99f;
		}

		public static (float voltage, float temperature, float bias, float tx_pwr, float rx_pwr) ParseOnuParameters(string input)
		{
			float item = 0f;
			float item2 = 0f;
			float item3 = 0f;
			float item4 = 0f;
			float item5 = 0f;
			string[] array = input.Split(';', ':');
			if (array.Length >= 6)
			{
				Regex regex = new Regex("[-+]?\\d+\\.?\\d*");
				item = ExtractFloat(regex, array[1]);
				item2 = ExtractFloat(regex, array[2]);
				item3 = ExtractFloat(regex, array[3]);
				item4 = ExtractFloat(regex, array[4]);
				item5 = ExtractFloat(regex, array[5]);
			}
			else
			{
				Console.WriteLine("输入字符串格式不正确，字段数量不足");
			}
			return (voltage: item, temperature: item2, bias: item3, tx_pwr: item4, rx_pwr: item5);
		}

		public void ONU_optics_performance_analysis()
		{
			try
			{
				while (!uDP_Analysis_Packages_CMD0x41.IsEmpty)
				{
					if (uDP_Analysis_Packages_CMD0x41.TryDequeue(out UDP_Analysis_Package result) && result != null)
					{
						string input = "";
						byte[] array = new byte[result.UDP_package_length - 1];
						byte[] array2 = new byte[result.UDP_package_length - 2];
						Array.Copy(result.content, 1, array, 0, result.UDP_package_length - 1);
						Array.Copy(result.content, 2, array2, 0, result.UDP_package_length - 2);
						if (array[0] == 65)
						{
							input = Encoding.ASCII.GetString(array2);
						}
						try
						{
							(float voltage, float temperature, float bias, float tx_pwr, float rx_pwr) tuple = ParseOnuParameters(input);
							float item = tuple.voltage;
							float item2 = tuple.temperature;
							float item3 = tuple.bias;
							float item4 = tuple.tx_pwr;
							float item5 = tuple.rx_pwr;
							byte[] array3 = new byte[12];
							Array.Copy(array2, array3, 12);
							string onusn = Encoding.ASCII.GetString(array3);
							ONU_Performance_Notity oNU_Performance_Notity = AppData.Users.FirstOrDefault((ONU_Performance_Notity onu) => onu != null && onu.onu_sn != null && onu.onu_sn.Equals(onusn));
							if (oNU_Performance_Notity != null)
							{
								oNU_Performance_Notity.onu_temperature = item2.ToString("F2");
								oNU_Performance_Notity.onu_bias = item3.ToString("F2");
								oNU_Performance_Notity.onu_tx_pwr = item4.ToString("F2");
								oNU_Performance_Notity.onu_rx_pwr = item5.ToString("F2");
								oNU_Performance_Notity.onu_voltage = item.ToString("F2");
								oNU_Performance_Notity.time_second = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
								oNU_Performance_Notity.time_string = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
								oNU_Performance_Notity.needsaverightnow = "YES";
							}
						}
						catch
						{
						}
					}
					bool flag = true;
				}
			}
			catch (Exception ex)
			{
				Textlog_With_Time(ex.Message + ", ---line2999");
			}
		}

		private void timer_ONU_optics_read_Tick(object sender, EventArgs e)
		{
			try
			{
				if (optics_mAC == null)
				{
					return;
				}
				foreach (List<ONU_Performance_Notity> item in AppData.GetGroupsByOLT())
				{
					foreach (ONU_Performance_Notity item2 in item)
					{
						string Selected_OLT_address = item2.olt_ip.ToString();
						string onusn = item2.onu_sn;
						if (!(item2.onu_status == "ON_LINE") && !(item2.onu_status == "AUTH_DONE"))
						{
							continue;
						}
						ONU_Performance_Notity oNU_Performance_Notity = AppData.Users.FirstOrDefault((ONU_Performance_Notity onu) => onu != null && onu.onu_sn != null && onu.onu_sn.Equals(onusn));
						if (oNU_Performance_Notity != null && (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds - oNU_Performance_Notity.time_second > 20.0)
						{
							oNU_Performance_Notity.time_second = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
							int num = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management a) => a.oLT_share_data.iPAddress.Equals(IPAddress.Parse(Selected_OLT_address)));
							if (num != -1 && oLT_Stick_Managements_List[num].oLT_share_data.Current_status == "ON_LINE" && oLT_Stick_Managements_List[num].oLT_share_data.sql_station != "downloadFW" && oLT_Stick_Managements_List[num].oLT_share_data.sql_station != "WhlstSet" && oLT_Stick_Managements_List[num].oLT_share_data.olt_Read_accessable_level > 0)
							{
								Manual_Ethernet_Frame_Send(Selected_OLT_address, onusn, optics_mAC);
								oNU_Performance_Notity.read_FLAG = 1;
								break;
							}
						}
					}
				}
			}
			catch
			{
			}
		}

		private void oNUServicepushingToolStripMenuItem_Click(object sender, EventArgs e)
		{
		}

		private void oNUHistoryToolStripMenuItem_Click(object sender, EventArgs e)
		{
			if (File.Exists(dbPath))
			{
				ONU_HISTORY oNU_HISTORY = new ONU_HISTORY(dbPath);
				oNU_HISTORY.Show();
			}
			else
			{
				MessageBox.Show("System management document is missed, please contact vendor");
			}
		}

		private void pollingbackToolStripMenuItem_Click(object sender, EventArgs e)
		{
			contextMenuStrip_OLT_Check.Close();
			string text = "";
			bool flag = true;
			try
			{
				if (dataGridView_OLT_Online_List.CurrentCell.Value == null)
				{
					return;
				}
			}
			catch
			{
				MessageBox.Show("please select OLT IP address");
				return;
			}
			try
			{
				int num = 0;
				if (dataGridView_OLT_Online_List.Tag == null)
				{
					return;
				}
				num = (int)dataGridView_OLT_Online_List.Tag;
				DataGridViewRow dataGridViewRow = dataGridView_OLT_Online_List.Rows[num];
				string comment = "";
				object value = dataGridView_OLT_Online_List.Rows[num].Cells["olt_ip"].Value;
				text = value.ToString();
				if (value != null)
				{
					comment = value.ToString();
					IPAddress.Parse(comment);
					int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management a) => a.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
					if (num2 != -1)
					{
						lock (Lock_oLT_Stick_Managements_List)
						{
							OLT_Stick_Management oLT_Stick_Management = oLT_Stick_Managements_List.FirstOrDefault((OLT_Stick_Management olt) => olt.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
							if (oLT_Stick_Management != null)
							{
								oLT_Stick_Management.oNU_ServiceType_GET.read_immediately = true;
							}
							return;
						}
					}
					Textlog_With_Time("cannot set ONU service in OLT IP_Add=" + comment + ", since it's not active");
				}
				else
				{
					MessageBox.Show("please click one OLT!");
				}
			}
			catch
			{
				Textlog_With_Time("failed to read service type back from " + text);
			}
		}

		private void oLTRemoveToolStripMenuItem_Click(object sender, EventArgs e)
		{
			contextMenuStrip_OLT_Check.Close();
			bool flag = true;
			try
			{
				if (dataGridView_OLT_Online_List.CurrentCell.Value == null)
				{
					return;
				}
			}
			catch
			{
				MessageBox.Show("please select OLT IP address");
				return;
			}
			try
			{
				int num = 0;
				if (dataGridView_OLT_Online_List.Tag == null)
				{
					return;
				}
				num = (int)dataGridView_OLT_Online_List.Tag;
				DataGridViewRow dataGridViewRow = dataGridView_OLT_Online_List.Rows[num];
				string comment = "";
				object value = dataGridView_OLT_Online_List.Rows[num].Cells["olt_ip"].Value;
				if (value != null)
				{
					comment = value.ToString();
					IPAddress.Parse(comment);
					int num2 = oLT_Stick_Managements_List.FindIndex((OLT_Stick_Management a) => a.oLT_share_data.iPAddress.Equals(IPAddress.Parse(comment)));
					if (num2 >= 0)
					{
						oLT_Stick_Managements_List.RemoveAt(num2);
					}
					OLT_AppData.RemovedOLTbyIP(IPAddress.Parse(comment));
				}
			}
			catch
			{
				Textlog_With_Time("failed to Remove OLT from list");
			}
		}

		private void OLT_Read_ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string text = OLT_Read_ToolStripMenuItem.Text;
			if (text == "Read_Enable")
			{
				OLT_Read_ToolStripMenuItem.Text = "Read_Disable";
			}
			else
			{
				OLT_Read_ToolStripMenuItem.Text = "Read_Enable";
			}
		}

		private void OLT_Write_ToolStripMenuItem_Click(object sender, EventArgs e)
		{
			string text = OLT_Write_ToolStripMenuItem.Text;
			if (text == "Write_Enable")
			{
				OLT_Write_ToolStripMenuItem.Text = "Write_Disable";
			}
			else
			{
				OLT_Write_ToolStripMenuItem.Text = "Write_Enable";
			}
		}

		private void timer_Performace_save_Tick(object sender, EventArgs e)
		{
			ONU_optics_performance_analysis();
			lock (Lock_Save_database)
			{
				databaseHelper.SaveOnuList(AppData.Save2databaseImmediatelylist());
			}
			databaseHelper.SaveOLTList(OLT_AppData.Save2databaseImmediatelylist());
			if (AppData.Users.Count > 0 && (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds - OLTperformancesavetime > databaseHelper._savetime_gap)
			{
				databaseHelper.SaveOLTList(OLT_AppData.Save2databaseNormallist());
				OLTperformancesavetime = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle = new System.Windows.Forms.DataGridViewCellStyle();
			System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(APP_OLT_Stick_Eth.Form1));
			this.button_Eth_Net_Selection = new System.Windows.Forms.Button();
			this.Log_textBox = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.button_Send_MAC_Frame = new System.Windows.Forms.Button();
			this.textBox_Send_Message = new System.Windows.Forms.TextBox();
			this.timer_Receiver_Message_Process = new System.Windows.Forms.Timer(this.components);
			this.comboBox_OLT_stick_ADD = new System.Windows.Forms.ComboBox();
			this.timer_OLTList_read = new System.Windows.Forms.Timer(this.components);
			this.dataGridView_OLT_Online_List = new System.Windows.Forms.DataGridView();
			this.contextMenuStrip_OLT_Check = new System.Windows.Forms.ContextMenuStrip(this.components);
			this.oNUStatusToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.oLTIPAddChangeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.cPEWhiteListToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.pushing2OLTToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.pollingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.oLTSDKUpgradeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.oNUServicepushingToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.pushingtoOLTToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.pollingbackToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.oLTRemoveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.OLT_On_Line_information_label = new System.Windows.Forms.Label();
			this.FToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.OLT_Write_ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.OLT_Read_ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.oLT地址ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.oLT_LIST_ToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.cPEToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.cPEToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
			this.oNUHistoryToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.textBox_LOG_Management = new System.Windows.Forms.TextBox();
			this.timer_ONU_optics_read = new System.Windows.Forms.Timer(this.components);
			this.RW_Status_Lable = new System.Windows.Forms.Label();
			this.comboBox_net_ports = new APP_OLT_Stick_V2.CustomComboBox();
			this.timer_Performace_save = new System.Windows.Forms.Timer(this.components);
			((System.ComponentModel.ISupportInitialize)this.dataGridView_OLT_Online_List).BeginInit();
			this.contextMenuStrip_OLT_Check.SuspendLayout();
			this.menuStrip1.SuspendLayout();
			base.SuspendLayout();
			this.button_Eth_Net_Selection.Location = new System.Drawing.Point(6, 37);
			this.button_Eth_Net_Selection.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button_Eth_Net_Selection.Name = "button_Eth_Net_Selection";
			this.button_Eth_Net_Selection.Size = new System.Drawing.Size(159, 29);
			this.button_Eth_Net_Selection.TabIndex = 0;
			this.button_Eth_Net_Selection.Text = "Server IP Selection :";
			this.button_Eth_Net_Selection.UseVisualStyleBackColor = true;
			this.button_Eth_Net_Selection.Click += new System.EventHandler(button_Eth_Net_Selection_Click);
			this.Log_textBox.Location = new System.Drawing.Point(894, 31);
			this.Log_textBox.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.Log_textBox.Multiline = true;
			this.Log_textBox.Name = "Log_textBox";
			this.Log_textBox.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.Log_textBox.Size = new System.Drawing.Size(536, 283);
			this.Log_textBox.TabIndex = 3;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(39, 80);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(127, 20);
			this.label1.TabIndex = 5;
			this.label1.Text = "OLT IP Selection";
			this.button_Send_MAC_Frame.Location = new System.Drawing.Point(28, 127);
			this.button_Send_MAC_Frame.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button_Send_MAC_Frame.Name = "button_Send_MAC_Frame";
			this.button_Send_MAC_Frame.Size = new System.Drawing.Size(138, 29);
			this.button_Send_MAC_Frame.TabIndex = 6;
			this.button_Send_MAC_Frame.Text = "Send_FCmd";
			this.button_Send_MAC_Frame.UseVisualStyleBackColor = true;
			this.button_Send_MAC_Frame.Click += new System.EventHandler(button_Send_MAC_Frame_Click);
			this.textBox_Send_Message.Location = new System.Drawing.Point(172, 126);
			this.textBox_Send_Message.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.textBox_Send_Message.Multiline = true;
			this.textBox_Send_Message.Name = "textBox_Send_Message";
			this.textBox_Send_Message.Size = new System.Drawing.Size(457, 60);
			this.textBox_Send_Message.TabIndex = 7;
			this.timer_Receiver_Message_Process.Interval = 10;
			this.timer_Receiver_Message_Process.Tick += new System.EventHandler(timer_Receiver_Message_Process_Tick);
			this.comboBox_OLT_stick_ADD.FormattingEnabled = true;
			this.comboBox_OLT_stick_ADD.Location = new System.Drawing.Point(172, 76);
			this.comboBox_OLT_stick_ADD.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.comboBox_OLT_stick_ADD.Name = "comboBox_OLT_stick_ADD";
			this.comboBox_OLT_stick_ADD.Size = new System.Drawing.Size(457, 28);
			this.comboBox_OLT_stick_ADD.TabIndex = 9;
			this.comboBox_OLT_stick_ADD.SelectedIndexChanged += new System.EventHandler(comboBox_OLT_stick_ADD_SelectedIndexChanged);
			this.comboBox_OLT_stick_ADD.MouseClick += new System.Windows.Forms.MouseEventHandler(comboBox_OLT_stick_ADD_MouseClick);
			this.timer_OLTList_read.Interval = 10000;
			this.timer_OLTList_read.Tick += new System.EventHandler(timer_OLTList_read_Tick);
			dataGridViewCellStyle.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
			dataGridViewCellStyle.BackColor = System.Drawing.SystemColors.Control;
			dataGridViewCellStyle.Font = new System.Drawing.Font("Microsoft YaHei UI", 9f);
			dataGridViewCellStyle.ForeColor = System.Drawing.SystemColors.WindowText;
			dataGridViewCellStyle.SelectionBackColor = System.Drawing.SystemColors.Highlight;
			dataGridViewCellStyle.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
			dataGridViewCellStyle.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
			this.dataGridView_OLT_Online_List.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle;
			this.dataGridView_OLT_Online_List.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView_OLT_Online_List.ContextMenuStrip = this.contextMenuStrip_OLT_Check;
			dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
			dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
			dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft YaHei UI", 9f);
			dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
			dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
			dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
			dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
			this.dataGridView_OLT_Online_List.DefaultCellStyle = dataGridViewCellStyle2;
			this.dataGridView_OLT_Online_List.Enabled = false;
			this.dataGridView_OLT_Online_List.Location = new System.Drawing.Point(6, 219);
			this.dataGridView_OLT_Online_List.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.dataGridView_OLT_Online_List.Name = "dataGridView_OLT_Online_List";
			this.dataGridView_OLT_Online_List.ReadOnly = true;
			this.dataGridView_OLT_Online_List.RowHeadersWidth = 51;
			this.dataGridView_OLT_Online_List.Size = new System.Drawing.Size(882, 475);
			this.dataGridView_OLT_Online_List.TabIndex = 10;
			this.dataGridView_OLT_Online_List.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(dataGridView_OLT_Online_List_CellMouseDown);
			this.contextMenuStrip_OLT_Check.ImageScalingSize = new System.Drawing.Size(20, 20);
			this.contextMenuStrip_OLT_Check.Items.AddRange(new System.Windows.Forms.ToolStripItem[6] { this.oNUStatusToolStripMenuItem, this.oLTIPAddChangeToolStripMenuItem, this.cPEWhiteListToolStripMenuItem, this.oLTSDKUpgradeToolStripMenuItem, this.oNUServicepushingToolStripMenuItem, this.oLTRemoveToolStripMenuItem });
			this.contextMenuStrip_OLT_Check.Name = "contextMenuStrip_OLT_Check";
			this.contextMenuStrip_OLT_Check.Size = new System.Drawing.Size(300, 148);
			this.contextMenuStrip_OLT_Check.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(contextMenuStrip_OLT_Check_ItemClicked);
			this.oNUStatusToolStripMenuItem.Name = "oNUStatusToolStripMenuItem";
			this.oNUStatusToolStripMenuItem.Size = new System.Drawing.Size(299, 24);
			this.oNUStatusToolStripMenuItem.Text = "ONU_Status";
			this.oNUStatusToolStripMenuItem.Click += new System.EventHandler(oNUStatusToolStripMenuItem_Click);
			this.oLTIPAddChangeToolStripMenuItem.Name = "oLTIPAddChangeToolStripMenuItem";
			this.oLTIPAddChangeToolStripMenuItem.Size = new System.Drawing.Size(299, 24);
			this.oLTIPAddChangeToolStripMenuItem.Text = "OLT_IP_Add_Change";
			this.oLTIPAddChangeToolStripMenuItem.Click += new System.EventHandler(oLTIPAddChangeToolStripMenuItem_Click_1);
			this.cPEWhiteListToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[2] { this.pushing2OLTToolStripMenuItem, this.pollingToolStripMenuItem });
			this.cPEWhiteListToolStripMenuItem.Name = "cPEWhiteListToolStripMenuItem";
			this.cPEWhiteListToolStripMenuItem.Size = new System.Drawing.Size(299, 24);
			this.cPEWhiteListToolStripMenuItem.Text = "ONU_White_List_Management";
			this.pushing2OLTToolStripMenuItem.Name = "pushing2OLTToolStripMenuItem";
			this.pushing2OLTToolStripMenuItem.Size = new System.Drawing.Size(208, 26);
			this.pushing2OLTToolStripMenuItem.Text = "Pushing_to_OLT";
			this.pushing2OLTToolStripMenuItem.Click += new System.EventHandler(pushing2OLTToolStripMenuItem_Click);
			this.pollingToolStripMenuItem.Name = "pollingToolStripMenuItem";
			this.pollingToolStripMenuItem.Size = new System.Drawing.Size(208, 26);
			this.pollingToolStripMenuItem.Text = "Polling_Back";
			this.pollingToolStripMenuItem.Click += new System.EventHandler(pollingToolStripMenuItem_Click);
			this.oLTSDKUpgradeToolStripMenuItem.Name = "oLTSDKUpgradeToolStripMenuItem";
			this.oLTSDKUpgradeToolStripMenuItem.Size = new System.Drawing.Size(299, 24);
			this.oLTSDKUpgradeToolStripMenuItem.Text = "OLT_SDK_Upgrade";
			this.oLTSDKUpgradeToolStripMenuItem.Click += new System.EventHandler(oLTSDKUpgradeToolStripMenuItem_Click);
			this.oNUServicepushingToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[2] { this.pushingtoOLTToolStripMenuItem, this.pollingbackToolStripMenuItem });
			this.oNUServicepushingToolStripMenuItem.Name = "oNUServicepushingToolStripMenuItem";
			this.oNUServicepushingToolStripMenuItem.Size = new System.Drawing.Size(299, 24);
			this.oNUServicepushingToolStripMenuItem.Text = "ONU_Service_Type";
			this.pushingtoOLTToolStripMenuItem.Name = "pushingtoOLTToolStripMenuItem";
			this.pushingtoOLTToolStripMenuItem.Size = new System.Drawing.Size(208, 26);
			this.pushingtoOLTToolStripMenuItem.Text = "Pushing_to_OLT";
			this.pushingtoOLTToolStripMenuItem.Click += new System.EventHandler(pushingtoOLTToolStripMenuItem_Click);
			this.pollingbackToolStripMenuItem.Name = "pollingbackToolStripMenuItem";
			this.pollingbackToolStripMenuItem.Size = new System.Drawing.Size(208, 26);
			this.pollingbackToolStripMenuItem.Text = "Polling_Back";
			this.pollingbackToolStripMenuItem.Click += new System.EventHandler(pollingbackToolStripMenuItem_Click);
			this.oLTRemoveToolStripMenuItem.Name = "oLTRemoveToolStripMenuItem";
			this.oLTRemoveToolStripMenuItem.Size = new System.Drawing.Size(299, 24);
			this.oLTRemoveToolStripMenuItem.Text = "OLT_Remove";
			this.oLTRemoveToolStripMenuItem.Click += new System.EventHandler(oLTRemoveToolStripMenuItem_Click);
			this.OLT_On_Line_information_label.AutoSize = true;
			this.OLT_On_Line_information_label.ForeColor = System.Drawing.SystemColors.MenuHighlight;
			this.OLT_On_Line_information_label.Location = new System.Drawing.Point(10, 196);
			this.OLT_On_Line_information_label.Name = "OLT_On_Line_information_label";
			this.OLT_On_Line_information_label.Size = new System.Drawing.Size(108, 20);
			this.OLT_On_Line_information_label.TabIndex = 11;
			this.OLT_On_Line_information_label.Text = "Current OLTs:";
			this.FToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[2] { this.OLT_Write_ToolStripMenuItem, this.OLT_Read_ToolStripMenuItem });
			this.FToolStripMenuItem.Name = "FToolStripMenuItem";
			this.FToolStripMenuItem.Size = new System.Drawing.Size(66, 24);
			this.FToolStripMenuItem.Text = "File(F)";
			this.OLT_Write_ToolStripMenuItem.Name = "OLT_Write_ToolStripMenuItem";
			this.OLT_Write_ToolStripMenuItem.Size = new System.Drawing.Size(187, 26);
			this.OLT_Write_ToolStripMenuItem.Text = "Write_Enable";
			this.OLT_Write_ToolStripMenuItem.Click += new System.EventHandler(OLT_Write_ToolStripMenuItem_Click);
			this.OLT_Read_ToolStripMenuItem.Name = "OLT_Read_ToolStripMenuItem";
			this.OLT_Read_ToolStripMenuItem.Size = new System.Drawing.Size(187, 26);
			this.OLT_Read_ToolStripMenuItem.Text = "Read_Enable";
			this.OLT_Read_ToolStripMenuItem.Click += new System.EventHandler(OLT_Read_ToolStripMenuItem_Click);
			this.oLT地址ToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[1] { this.oLT_LIST_ToolStripMenuItem });
			this.oLT地址ToolStripMenuItem.Name = "oLT地址ToolStripMenuItem";
			this.oLT地址ToolStripMenuItem.Size = new System.Drawing.Size(52, 24);
			this.oLT地址ToolStripMenuItem.Text = "OLT";
			this.oLT_LIST_ToolStripMenuItem.Name = "oLT_LIST_ToolStripMenuItem";
			this.oLT_LIST_ToolStripMenuItem.Size = new System.Drawing.Size(178, 26);
			this.oLT_LIST_ToolStripMenuItem.Text = "OLT History";
			this.oLT_LIST_ToolStripMenuItem.Click += new System.EventHandler(oLTListToolStripMenuItem_Click);
			this.cPEToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[2] { this.cPEToolStripMenuItem1, this.oNUHistoryToolStripMenuItem });
			this.cPEToolStripMenuItem.Name = "cPEToolStripMenuItem";
			this.cPEToolStripMenuItem.Size = new System.Drawing.Size(58, 24);
			this.cPEToolStripMenuItem.Text = "ONU";
			this.cPEToolStripMenuItem1.Name = "cPEToolStripMenuItem1";
			this.cPEToolStripMenuItem1.Size = new System.Drawing.Size(224, 26);
			this.cPEToolStripMenuItem1.Text = "ONU Performance";
			this.cPEToolStripMenuItem1.Click += new System.EventHandler(cPEToolStripMenuItem1_Click);
			this.oNUHistoryToolStripMenuItem.Name = "oNUHistoryToolStripMenuItem";
			this.oNUHistoryToolStripMenuItem.Size = new System.Drawing.Size(224, 26);
			this.oNUHistoryToolStripMenuItem.Text = "ONU History";
			this.oNUHistoryToolStripMenuItem.Click += new System.EventHandler(oNUHistoryToolStripMenuItem_Click);
			this.menuStrip1.BackColor = System.Drawing.SystemColors.ActiveCaption;
			this.menuStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[3] { this.FToolStripMenuItem, this.oLT地址ToolStripMenuItem, this.cPEToolStripMenuItem });
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(1434, 28);
			this.menuStrip1.TabIndex = 8;
			this.menuStrip1.Text = "menuStrip1";
			this.textBox_LOG_Management.Location = new System.Drawing.Point(894, 320);
			this.textBox_LOG_Management.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.textBox_LOG_Management.Multiline = true;
			this.textBox_LOG_Management.Name = "textBox_LOG_Management";
			this.textBox_LOG_Management.ScrollBars = System.Windows.Forms.ScrollBars.Both;
			this.textBox_LOG_Management.Size = new System.Drawing.Size(536, 364);
			this.textBox_LOG_Management.TabIndex = 12;
			this.timer_ONU_optics_read.Enabled = true;
			this.timer_ONU_optics_read.Interval = 5000;
			this.timer_ONU_optics_read.Tick += new System.EventHandler(timer_ONU_optics_read_Tick);
			this.RW_Status_Lable.AutoSize = true;
			this.RW_Status_Lable.Location = new System.Drawing.Point(790, 184);
			this.RW_Status_Lable.Name = "RW_Status_Lable";
			this.RW_Status_Lable.Size = new System.Drawing.Size(89, 20);
			this.RW_Status_Lable.TabIndex = 13;
			this.RW_Status_Lable.Text = "R/W Status";
			this.comboBox_net_ports.BorderColor = System.Drawing.Color.FromArgb(20, 201, 187);
			this.comboBox_net_ports.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
			this.comboBox_net_ports.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.comboBox_net_ports.FillColor = System.Drawing.Color.FromArgb(20, 201, 187);
			this.comboBox_net_ports.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.comboBox_net_ports.FormattingEnabled = true;
			this.comboBox_net_ports.ItemHeight = 20;
			this.comboBox_net_ports.Location = new System.Drawing.Point(172, 39);
			this.comboBox_net_ports.Margin = new System.Windows.Forms.Padding(4);
			this.comboBox_net_ports.Name = "comboBox_net_ports";
			this.comboBox_net_ports.Size = new System.Drawing.Size(457, 26);
			this.comboBox_net_ports.TabIndex = 14;
			this.comboBox_net_ports.TextColor = System.Drawing.Color.Black;
			this.comboBox_net_ports.SelectedIndexChanged += new System.EventHandler(comboBox_net_ports_SelectedIndexChanged);
			this.timer_Performace_save.Enabled = true;
			this.timer_Performace_save.Interval = 1000;
			this.timer_Performace_save.Tick += new System.EventHandler(timer_Performace_save_Tick);
			base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(1434, 696);
			base.Controls.Add(this.comboBox_net_ports);
			base.Controls.Add(this.RW_Status_Lable);
			base.Controls.Add(this.textBox_LOG_Management);
			base.Controls.Add(this.OLT_On_Line_information_label);
			base.Controls.Add(this.dataGridView_OLT_Online_List);
			base.Controls.Add(this.comboBox_OLT_stick_ADD);
			base.Controls.Add(this.textBox_Send_Message);
			base.Controls.Add(this.button_Send_MAC_Frame);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.Log_textBox);
			base.Controls.Add(this.button_Eth_Net_Selection);
			base.Controls.Add(this.menuStrip1);
			base.Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
			base.MainMenuStrip = this.menuStrip1;
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "Form1";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "FS PON Manager V1.3";
			base.Load += new System.EventHandler(Form1_Load);
			((System.ComponentModel.ISupportInitialize)this.dataGridView_OLT_Online_List).EndInit();
			this.contextMenuStrip_OLT_Check.ResumeLayout(false);
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
	public class IP_Add_Change : Form
	{
		private Form1 _form1;

		private OLT_IP_Change_Notify myOLT_IP_Change_Notify;

		private string old_IP = "";

		private IContainer components = null;

		private TextBox IP_Add_Change_OLDIP;

		private Label label1;

		private Label label2;

		private TextBox IP_Add_Change_NEWIP;

		private Button IP_Add_Change_Confirm;

		private Button IP_Add_Change_Cancel;

		public IP_Add_Change(Form1 form1, string OLT_IP, OLT_IP_Change_Notify oLT_IP_Change_Notify)
		{
			InitializeComponent();
			IP_Add_Change_OLDIP.Text = OLT_IP;
			old_IP = OLT_IP;
			myOLT_IP_Change_Notify = oLT_IP_Change_Notify;
			_form1 = form1;
			base.Icon = new Icon("FSLogo.ico");
		}

		private void IP_Add_Change_Confirm_Click(object sender, EventArgs e)
		{
			string text = IP_Add_Change_NEWIP.Text;
			try
			{
				IPAddress.Parse(text);
				if (_form1.IP_add_Check(IPAddress.Parse(text), IPAddress.Parse(old_IP)) != 0)
				{
					MessageBox.Show("new IP add is conflict with current oLTs or current IP is not active");
					return;
				}
				myOLT_IP_Change_Notify.OLT_IP_OLD = IPAddress.Parse(old_IP);
				myOLT_IP_Change_Notify.OLT_IP_NEW = IPAddress.Parse(text);
				myOLT_IP_Change_Notify.Change_Notify = true;
				double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
				myOLT_IP_Change_Notify.init_timing = totalSeconds;
			}
			catch (Exception ex)
			{
				myOLT_IP_Change_Notify.OLT_IP_OLD = IPAddress.Parse(old_IP);
				myOLT_IP_Change_Notify.OLT_IP_NEW = IPAddress.None;
				myOLT_IP_Change_Notify.Change_Notify = false;
				myOLT_IP_Change_Notify.init_timing = 1.0;
				MessageBox.Show(ex.Message);
				return;
			}
			Close();
		}

		private void IP_Add_Change_Cancel_Click(object sender, EventArgs e)
		{
			myOLT_IP_Change_Notify.OLT_IP_OLD = IPAddress.Parse(old_IP);
			myOLT_IP_Change_Notify.OLT_IP_NEW = IPAddress.None;
			myOLT_IP_Change_Notify.Change_Notify = false;
			Close();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.IP_Add_Change_OLDIP = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.IP_Add_Change_NEWIP = new System.Windows.Forms.TextBox();
			this.IP_Add_Change_Confirm = new System.Windows.Forms.Button();
			this.IP_Add_Change_Cancel = new System.Windows.Forms.Button();
			base.SuspendLayout();
			this.IP_Add_Change_OLDIP.Location = new System.Drawing.Point(121, 22);
			this.IP_Add_Change_OLDIP.Name = "IP_Add_Change_OLDIP";
			this.IP_Add_Change_OLDIP.ReadOnly = true;
			this.IP_Add_Change_OLDIP.Size = new System.Drawing.Size(236, 27);
			this.IP_Add_Change_OLDIP.TabIndex = 0;
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(22, 25);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(93, 20);
			this.label1.TabIndex = 1;
			this.label1.Text = "Old_IP_Add";
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(22, 72);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(100, 20);
			this.label2.TabIndex = 3;
			this.label2.Text = "New_IP_Add";
			this.IP_Add_Change_NEWIP.Location = new System.Drawing.Point(121, 69);
			this.IP_Add_Change_NEWIP.Name = "IP_Add_Change_NEWIP";
			this.IP_Add_Change_NEWIP.Size = new System.Drawing.Size(236, 27);
			this.IP_Add_Change_NEWIP.TabIndex = 2;
			this.IP_Add_Change_Confirm.Location = new System.Drawing.Point(91, 126);
			this.IP_Add_Change_Confirm.Name = "IP_Add_Change_Confirm";
			this.IP_Add_Change_Confirm.Size = new System.Drawing.Size(94, 29);
			this.IP_Add_Change_Confirm.TabIndex = 4;
			this.IP_Add_Change_Confirm.Text = "Confirm";
			this.IP_Add_Change_Confirm.UseVisualStyleBackColor = true;
			this.IP_Add_Change_Confirm.Click += new System.EventHandler(IP_Add_Change_Confirm_Click);
			this.IP_Add_Change_Cancel.Location = new System.Drawing.Point(202, 126);
			this.IP_Add_Change_Cancel.Name = "IP_Add_Change_Cancel";
			this.IP_Add_Change_Cancel.Size = new System.Drawing.Size(94, 29);
			this.IP_Add_Change_Cancel.TabIndex = 5;
			this.IP_Add_Change_Cancel.Text = "Cancel";
			this.IP_Add_Change_Cancel.UseVisualStyleBackColor = true;
			this.IP_Add_Change_Cancel.Click += new System.EventHandler(IP_Add_Change_Cancel_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(443, 177);
			base.Controls.Add(this.IP_Add_Change_Cancel);
			base.Controls.Add(this.IP_Add_Change_Confirm);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.IP_Add_Change_NEWIP);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.IP_Add_Change_OLDIP);
			base.Name = "IP_Add_Change";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "IP_Add_Change";
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
	public static class SharedResources
	{
		public static readonly object LockObject = new object();

		public static readonly object ArplockObject = new object();

		public static int SharedCounter = 0;
	}
	public struct MAC_Frame_Content
	{
		public byte[] Content;

		public bool read;
	}
	public struct Arp_Frame_Content
	{
		public PhysicalAddress SenderHardwareAddress;

		public IPAddress SenderProtocolAddress;

		public PhysicalAddress TargetHardwareAddress;

		public IPAddress TargetProtocolAddress;

		public ushort Opcode;

		public string specified_code;

		public override string ToString()
		{
			return $"SIP: {SenderProtocolAddress}, SMAC: {SenderHardwareAddress},DIP: {TargetProtocolAddress}, DMAC: {TargetHardwareAddress},OPcode={Opcode},code={specified_code}";
		}
	}
	public class UDP_Analysis_Package
	{
		public PhysicalAddress SenderHardwareAddress;

		public IPAddress SenderProtocolAddress;

		public Command_Code cmd_Code;

		public ushort upd_ID;

		public byte[] content;

		public double aging_time;

		public double time_window;

		public Task_Owner task_owner;

		public bool ack_checked;

		public int UDP_package_length;

		public long special_ID;

		public UDP_Analysis_Package()
		{
			SenderHardwareAddress = PhysicalAddress.None;
			SenderProtocolAddress = IPAddress.None;
			content = new byte[1];
			ack_checked = false;
		}

		private static string ByteArrayToHexString(byte[] ba)
		{
			StringBuilder stringBuilder = new StringBuilder(ba.Length * 2);
			foreach (byte b in ba)
			{
				stringBuilder.AppendFormat("{0:x2}", b);
			}
			return stringBuilder.ToString();
		}

		public override string ToString()
		{
			return $"OLT_IP: {SenderProtocolAddress}, OLT_MAC: {SenderHardwareAddress},Cmdcode: {cmd_Code.ToString("X2")}, ID={upd_ID.ToString("X4")},task_owner ={task_owner}, content = {ByteArrayToHexString(content)}";
		}
	}
	public enum Task_Owner
	{
		None = 0,
		Manual_send = 153,
		Shakehand_Task = 1,
		ip_configuration = 2,
		cpe_white_list_send = 3,
		cpe_white_list_read_quantity = 4,
		ONU_WLIST_RPT = 5,
		cpe_illegal_CPE_reprot = 6,
		cpe_alarm_report = 7,
		olt_alarm_report = 8,
		cpe_service_type_send = 9,
		cpe_white_list_del = 10,
		cpe_opt_para_report = 11,
		cpe_sn_status = 12,
		SERVICE_CONFIG_RPT = 13,
		Password_cmd = 66,
		Password_check_cmd = 67,
		OLT_Update_BIN_cmd = 68,
		OLT_Reset_Maseter_cmd = 69,
		OLT_Reset_Slave_cmd = 70,
		OLT_Softreset_cmd = 71,
		OLT_Image_Send_cmd = 72
	}
	public enum Command_Code
	{
		None = 0,
		shake_hand = 1,
		ip_configuration = 2,
		cpe_white_list_send = 3,
		cpe_white_list_read_quantity = 4,
		ONU_WLIST_RPT = 5,
		cpe_illegal_CPE_reprot = 6,
		cpe_alarm_report = 7,
		olt_alarm_report = 8,
		cpe_service_type_send = 9,
		cpe_white_list_del = 10,
		cpe_opt_para_report = 11,
		cpe_sn_status = 12,
		SERVICE_CONFIG_RPT = 13,
		Password_cmd = 66,
		Password_check_cmd = 67,
		OLT_Update_BIN_cmd = 68
	}
	internal class MAC
	{
		private static readonly object SendFrameLock = new object();

		public CircularBuffer<MAC_Frame_Content> mCircularBuffer = new CircularBuffer<MAC_Frame_Content>(10000);

		public CircularBuffer<Arp_Frame_Content> mArpCircularBuffer = new CircularBuffer<Arp_Frame_Content>(10000);

		private readonly IInjectionDevice _device;

		private readonly PhysicalAddress _sourceMac;

		private PhysicalAddress _destinationMac;

		public readonly IPAddress _sourceIP;

		public IPAddress _destinationIP;

		private readonly ushort _MAC_Type;

		public int MAC_init_Error;

		public MAC(string sourceMac, ushort MAC_Type, IPAddress sourceIP)
		{
			string sourceMac2 = sourceMac;
			base..ctor();
			_MAC_Type = MAC_Type;
			MAC_init_Error = 0;
			if (string.IsNullOrEmpty(sourceMac2))
			{
				MAC_init_Error = -1;
				return;
			}
			sourceMac2 = sourceMac2.Replace("-", "").Replace(":", "");
			CaptureDeviceList instance = CaptureDeviceList.Instance;
			ILiveDevice device = instance.FirstOrDefault((ILiveDevice d) => sourceMac2.Equals(d.MacAddress?.ToString()));
			if (device == null)
			{
				MAC_init_Error = -3;
				return;
			}
			IInjectionDevice injectionDevice = device;
			if (injectionDevice == null)
			{
				MAC_init_Error = -4;
				return;
			}
			_device = injectionDevice;
			NetworkInterface networkInterface = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault((NetworkInterface n) => n.Description == device.Description);
			if (networkInterface == null)
			{
				MAC_init_Error = -4;
				return;
			}
			_sourceMac = networkInterface.GetPhysicalAddress();
			if (_sourceMac == null)
			{
				throw new InvalidOperationException("Cannot get MAC add");
			}
			_sourceIP = sourceIP;
		}

		public static string ByteArrayToHexString(byte[] byteArray)
		{
			StringBuilder stringBuilder = new StringBuilder(byteArray.Length * 2);
			foreach (byte b in byteArray)
			{
				stringBuilder.AppendFormat("{0:X2}", b);
			}
			return stringBuilder.ToString();
		}

		public void StartListening()
		{
			if (_device is ICaptureDevice captureDevice)
			{
				captureDevice.Open(new DeviceConfiguration
				{
					Mode = DeviceModes.Promiscuous,
					ReadTimeout = 100
				});
				string text = "";
				text = ((_MAC_Type != 2048) ? ("ether dst " + _sourceMac.ToString()) : "udp port 64218 or arp");
				captureDevice.Filter = text;
				captureDevice.OnPacketArrival += Device_OnPacketArrival;
				captureDevice.StartCapture();
			}
		}

		public int SendFrame(byte[] payload, PhysicalAddress destMac, IPAddress destIP)
		{
			ArgumentNullException.ThrowIfNull(payload, "payload");
			int result = 0;
			_destinationMac = destMac;
			_destinationIP = destIP;
			try
			{
				lock (SendFrameLock)
				{
					Ether_Frame ether_Frame = new Ether_Frame(_destinationMac.GetAddressBytes(), _sourceMac.GetAddressBytes(), _MAC_Type, payload, _destinationIP, _sourceIP);
					_device.SendPacket(ether_Frame.GetFrame());
				}
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		private ushort CalculatePseudoChecksum(byte[] upd_psy_frame)
		{
			int num = 0;
			for (int i = 0; i < upd_psy_frame.Length; i += 2)
			{
				ushort num2 = ((i + 1 >= upd_psy_frame.Length) ? ((ushort)(upd_psy_frame[i] << 8)) : ((ushort)((upd_psy_frame[i] << 8) + upd_psy_frame[i + 1])));
				num += num2;
				if ((num & 0xFFFF0000u) != 0)
				{
					num = (num & 0xFFFF) + (num >> 16);
				}
			}
			return (ushort)(~num);
		}

		private void Device_OnPacketArrival(object sender, PacketCapture e)
		{
			RawCapture packet = e.GetPacket();
			if (packet?.Data == null)
			{
				return;
			}
			Packet packet2 = Packet.ParsePacket(LinkLayers.Ethernet, e.GetPacket().Data);
			if (packet2 is EthernetPacket { PayloadPacket: ArpPacket payloadPacket })
			{
				Arp_Frame_Content item = default(Arp_Frame_Content);
				item.SenderHardwareAddress = PhysicalAddress.Parse(payloadPacket.SenderHardwareAddress.ToString());
				item.SenderProtocolAddress = payloadPacket.SenderProtocolAddress;
				item.TargetHardwareAddress = PhysicalAddress.Parse(payloadPacket.TargetHardwareAddress.ToString());
				item.TargetProtocolAddress = payloadPacket.TargetProtocolAddress;
				item.Opcode = (ushort)payloadPacket.Operation;
				byte[] array = new byte[3];
				try
				{
					Array.Copy(packet.Data, 42, array, 0, 3);
					item.specified_code = Encoding.ASCII.GetString(array);
				}
				catch
				{
					item.specified_code = "";
				}
				lock (SharedResources.ArplockObject)
				{
					mArpCircularBuffer.Enqueue(item);
					return;
				}
			}
			try
			{
				if (packet.Data.Length < 14)
				{
					return;
				}
				int num = 0;
				ushort num2 = (ushort)(packet.Data[12] * 256 + packet.Data[13]);
				if (num2 == 33024)
				{
					num = 2;
				}
				byte[] array2;
				if (num == 2)
				{
					array2 = new byte[packet.Data.Length - 2];
					Array.Copy(packet.Data, 0, array2, 0, 12);
					Array.Copy(packet.Data, 14, array2, 12, packet.Data.Length - 2 - 12);
				}
				else
				{
					array2 = new byte[packet.Data.Length];
					Array.Copy(packet.Data, 0, array2, 0, packet.Data.Length);
				}
				if (array2[34] != 250 || array2[35] != 218)
				{
					return;
				}
				ushort num3 = 0;
				ushort num4 = (ushort)(array2[40] * 256 + array2[41]);
				num3 = (ushort)(array2[38] * 256 + array2[39]);
				byte[] array3 = new byte[num3];
				Array.Copy(array2, 34, array3, 0, num3);
				byte[] array4 = new byte[num3 + 12];
				Array.Copy(array2, 26, array4, 0, 4);
				Array.Copy(array2, 30, array4, 4, 4);
				array4[8] = 0;
				array4[9] = 17;
				array4[10] = array3[4];
				array4[11] = array3[5];
				Array.Copy(array2, 34, array4, 12, num3);
				array4[18] = 0;
				array4[19] = 0;
				ushort num5 = CalculatePseudoChecksum(array4);
				if (num5 != num4)
				{
					return;
				}
				byte[] array5 = new byte[6];
				Array.Copy(array2, 6, array5, 0, 6);
				byte[] array6 = new byte[6];
				Array.Copy(array2, 0, array6, 0, 6);
				string text = ByteArrayToHexString(array6);
				if (text != _sourceMac.ToString())
				{
					return;
				}
				byte[] array7 = new byte[34 + num3];
				if (array2.Length < 34 + num3)
				{
					return;
				}
				Array.Copy(array2, 6, array7, 0, 28 + num3);
				byte[] array8 = new byte[4];
				Array.Copy(array7, 24, array8, 0, 4);
				IPAddress iPAddress = new IPAddress(array8);
				PhysicalAddress physicalAddress = new PhysicalAddress(array5);
				MAC_Frame_Content item2 = default(MAC_Frame_Content);
				item2.Content = array7;
				item2.read = false;
				lock (SharedResources.LockObject)
				{
					mCircularBuffer.Enqueue(item2);
				}
			}
			catch
			{
			}
		}

		public void Stop()
		{
			if (_device is ICaptureDevice captureDevice)
			{
				try
				{
					captureDevice.StopCapture();
					captureDevice.Close();
				}
				catch
				{
				}
			}
		}

		public int BuildArpRequest(IPAddress destinationIp)
		{
			int num = 0;
			try
			{
				PhysicalAddress sourceMac = _sourceMac;
				IPAddress sourceIP = _sourceIP;
				PhysicalAddress physicalAddress = PhysicalAddress.Parse("FF-FF-FF-FF-FF-FF");
				byte[] array = new byte[60]
				{
					physicalAddress.GetAddressBytes()[0],
					physicalAddress.GetAddressBytes()[1],
					physicalAddress.GetAddressBytes()[2],
					physicalAddress.GetAddressBytes()[3],
					physicalAddress.GetAddressBytes()[4],
					physicalAddress.GetAddressBytes()[5],
					sourceMac.GetAddressBytes()[0],
					sourceMac.GetAddressBytes()[1],
					sourceMac.GetAddressBytes()[2],
					sourceMac.GetAddressBytes()[3],
					sourceMac.GetAddressBytes()[4],
					sourceMac.GetAddressBytes()[5],
					8,
					6,
					0,
					1,
					8,
					0,
					6,
					4,
					0,
					1,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0,
					0
				};
				Buffer.BlockCopy(sourceMac.GetAddressBytes(), 0, array, 22, 6);
				array[28] = sourceIP.GetAddressBytes()[0];
				array[29] = sourceIP.GetAddressBytes()[1];
				array[30] = sourceIP.GetAddressBytes()[2];
				array[31] = sourceIP.GetAddressBytes()[3];
				Buffer.BlockCopy(new byte[6], 0, array, 32, 6);
				array[38] = destinationIp.GetAddressBytes()[0];
				array[39] = destinationIp.GetAddressBytes()[1];
				array[40] = destinationIp.GetAddressBytes()[2];
				array[41] = destinationIp.GetAddressBytes()[3];
				array[42] = Encoding.ASCII.GetBytes("O")[0];
				array[43] = Encoding.ASCII.GetBytes("L")[0];
				array[44] = Encoding.ASCII.GetBytes("T")[0];
				byte[] array2 = new byte[15];
				Buffer.BlockCopy(array2, 0, array, 45, array2.Length);
				_device.SendPacket(array);
				return 0;
			}
			catch
			{
				return -1;
			}
		}

		public void Dispose()
		{
			Stop();
			_device?.Dispose();
		}
	}
	internal class CRC32
	{
		private static readonly uint[] Table;

		private const uint Polynomial = 3988292384u;

		static CRC32()
		{
			Table = new uint[256];
			for (uint num = 0u; num < Table.Length; num++)
			{
				uint num2 = num;
				for (uint num3 = 8u; num3 != 0; num3--)
				{
					num2 = (((num2 & 1) == 0) ? (num2 >> 1) : ((num2 >> 1) ^ 0xEDB88320u));
				}
				Table[num] = num2;
			}
		}

		public static uint ComputeChecksum(byte[] bytes)
		{
			uint num = uint.MaxValue;
			foreach (byte b in bytes)
			{
				byte b2 = (byte)((num & 0xFFu) ^ b);
				num = (num >> 8) ^ Table[b2];
			}
			return ~num;
		}

		public static byte[] GetChecksumBytes(uint checksum)
		{
			return BitConverter.GetBytes(checksum);
		}
	}
	public class CircularBuffer<T>
	{
		private readonly T[] _buffer;

		private int _head;

		private int _tail;

		private int _count;

		private readonly int _capacity;

		private readonly object _lock = new object();

		public int Count => _count;

		public int Capacity => _capacity;

		public bool IsFull => _count == _capacity;

		public bool IsEmpty => _count == 0;

		public CircularBuffer(int capacity)
		{
			if (capacity <= 0)
			{
				throw new ArgumentOutOfRangeException("capacity", "Capacity must be greater than zero.");
			}
			_capacity = capacity;
			_buffer = new T[capacity];
			_head = 0;
			_tail = 0;
			_count = 0;
		}

		public void Enqueue(T item)
		{
			lock (_lock)
			{
				if (IsFull)
				{
					_head = (_head + 1) % _capacity;
					_count--;
				}
				_buffer[_tail] = item;
				_tail = (_tail + 1) % _capacity;
				_count++;
			}
		}

		public void updated(T item)
		{
			lock (_lock)
			{
				int num = 0;
				num = ((!IsEmpty) ? ((_tail != 0) ? (_tail - 1) : (_capacity - 1)) : 0);
				_buffer[num] = item;
			}
		}

		public T GetLast()
		{
			lock (_lock)
			{
				int num = 0;
				num = ((!IsEmpty) ? ((_tail != 0) ? (_tail - 1) : (_capacity - 1)) : 0);
				return _buffer[num];
			}
		}

		public T Dequeue()
		{
			lock (_lock)
			{
				if (IsEmpty)
				{
					throw new InvalidOperationException("The buffer is empty. Cannot dequeue from an empty buffer.");
				}
				T result = _buffer[_head];
				_buffer[_head] = default(T);
				_head = (_head + 1) % _capacity;
				_count--;
				return result;
			}
		}

		public T GetCurrent()
		{
			return _buffer[_head];
		}

		public T Peek()
		{
			lock (_lock)
			{
				if (IsEmpty)
				{
					throw new InvalidOperationException("The buffer is empty. Cannot peek from an empty buffer.");
				}
				return _buffer[_head];
			}
		}

		public void Clear()
		{
			lock (_lock)
			{
				Array.Clear(_buffer, 0, _capacity);
				_head = 0;
				_tail = 0;
				_count = 0;
			}
		}

		public T[] ToArray()
		{
			lock (_lock)
			{
				T[] array = new T[_count];
				for (int i = 0; i < _count; i++)
				{
					array[i] = _buffer[(_head + i) % _capacity];
				}
				return array;
			}
		}
	}
	internal class Ether_Frame
	{
		public static readonly int HeaderSize = 14;

		public static readonly int MinFrameLength = 64;

		public byte[] DestinationAddress { get; }

		public byte[] SourceAddress { get; }

		public ushort Type { get; }

		public byte[] Payload { get; }

		public Ether_Frame(byte[] destinationAddress, byte[] sourceAddress, ushort type, byte[] payload, IPAddress IP_Destination, IPAddress IP_Source)
		{
			if (destinationAddress.Length != 6)
			{
				throw new ArgumentException("目标MAC地址必须是6个字节");
			}
			if (sourceAddress.Length != 6)
			{
				throw new ArgumentException("源MAC地址必须是6个字节");
			}
			DestinationAddress = destinationAddress;
			SourceAddress = sourceAddress;
			Type = type;
			UdpPacketWrapper udpPacketWrapper = new UdpPacketWrapper(payload, 64219, 64219, IP_Destination, IP_Source);
			byte[] packetBytes = udpPacketWrapper.GetPacketBytes();
			RawIPPacket rawIPPacket = new RawIPPacket(IP_Destination, IP_Source);
			byte[] packet = rawIPPacket.GetPacket(packetBytes);
			Payload = packet;
		}

		public byte[] GetFrame()
		{
			byte[] array = new byte[HeaderSize + Payload.Length];
			Array.Copy(DestinationAddress, 0, array, 0, 6);
			Array.Copy(SourceAddress, 0, array, 6, 6);
			array[13] = (byte)(Type & 0xFFu);
			array[12] = (byte)((uint)(Type >> 8) & 0xFFu);
			Array.Copy(Payload, 0, array, HeaderSize, Payload.Length);
			if (array.Length < MinFrameLength)
			{
				int num = MinFrameLength - array.Length;
				byte[] array2 = new byte[num];
				Array.Fill(array2, (byte)0);
				Array.Resize(ref array, array.Length + num);
				Array.Copy(array2, 0, array, array.Length - num, num);
			}
			return array;
		}
	}
	internal class RawIPPacket
	{
		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		private struct IPv4Header
		{
			public byte VersionIHL;

			public byte TypeOfService;

			public ushort TotalLength;

			public ushort Identification;

			public ushort OFFSET;

			public byte TTL;

			public byte Protocol;

			public ushort Checksum;

			public uint SourceAddress;

			public uint DestinationAddress;
		}

		private const int IPHeaderLength = 20;

		private readonly uint _ip_Source;

		private readonly uint _ip_Destination;

		public RawIPPacket(IPAddress IP_Destination, IPAddress IP_Source)
		{
			byte[] addressBytes = IP_Destination.GetAddressBytes();
			uint ip_Destination = (uint)((addressBytes[0] << 24) | (addressBytes[1] << 16) | (addressBytes[2] << 8) | addressBytes[3]);
			_ip_Destination = ip_Destination;
			byte[] addressBytes2 = IP_Source.GetAddressBytes();
			uint ip_Source = (uint)((addressBytes2[0] << 24) | (addressBytes2[1] << 16) | (addressBytes2[2] << 8) | addressBytes2[3]);
			_ip_Source = ip_Source;
		}

		public byte[] GetPacket(byte[] tcpData)
		{
			if (tcpData == null || tcpData.Length < 11)
			{
				throw new ArgumentException("TCP data length is not right.");
			}
			IPv4Header pv4Header = default(IPv4Header);
			pv4Header.VersionIHL = 69;
			pv4Header.TypeOfService = 0;
			pv4Header.TotalLength = (ushort)(20 + tcpData.Length);
			pv4Header.Identification = 23130;
			pv4Header.OFFSET = 0;
			pv4Header.TTL = 128;
			pv4Header.Protocol = 17;
			pv4Header.Checksum = 0;
			pv4Header.SourceAddress = _ip_Source;
			pv4Header.DestinationAddress = _ip_Destination;
			IPv4Header header = pv4Header;
			byte[] ipHeader = IPv4HeaderToBytes(header);
			header.Checksum = CalculateChecksum(ipHeader);
			ipHeader = IPv4HeaderToBytes(header);
			byte[] array = new byte[20 + tcpData.Length];
			Buffer.BlockCopy(ipHeader, 0, array, 0, 20);
			Buffer.BlockCopy(tcpData, 0, array, 20, tcpData.Length);
			return array;
		}

		private static byte[] IPv4HeaderToBytes(IPv4Header header)
		{
			return new byte[20]
			{
				header.VersionIHL,
				header.TypeOfService,
				(byte)(header.TotalLength >> 8),
				(byte)(header.TotalLength & 0xFFu),
				(byte)(header.Identification >> 8),
				(byte)(header.Identification & 0xFFu),
				(byte)((uint)(header.OFFSET >> 8) & 0xFFu),
				(byte)(header.OFFSET & 0xFFu),
				header.TTL,
				header.Protocol,
				(byte)(header.Checksum >> 8),
				(byte)(header.Checksum & 0xFFu),
				(byte)(header.SourceAddress >> 24),
				(byte)(header.SourceAddress >> 16),
				(byte)(header.SourceAddress >> 8),
				(byte)(header.SourceAddress & 0xFFu),
				(byte)(header.DestinationAddress >> 24),
				(byte)(header.DestinationAddress >> 16),
				(byte)(header.DestinationAddress >> 8),
				(byte)(header.DestinationAddress & 0xFFu)
			};
		}

		private static ushort CalculateChecksum(byte[] ipHeader)
		{
			int num = 0;
			for (int i = 0; i < ipHeader.Length; i += 2)
			{
				ushort num2 = ((i + 1 >= ipHeader.Length) ? ((ushort)(ipHeader[i] << 8)) : ((ushort)((ipHeader[i] << 8) + ipHeader[i + 1])));
				num += num2;
				if ((num & 0xFFFF0000u) != 0)
				{
					num = (num & 0xFFFF) + (num >> 16);
				}
			}
			return (ushort)(~num);
		}

		private static byte[] StructToBytes<T>(T structure) where T : struct
		{
			int num = Marshal.SizeOf(structure);
			byte[] array = new byte[num];
			nint num2 = Marshal.AllocHGlobal(num);
			try
			{
				Marshal.StructureToPtr(structure, num2, fDeleteOld: true);
				Marshal.Copy(num2, array, 0, num);
			}
			finally
			{
				Marshal.FreeHGlobal(num2);
			}
			return array;
		}
	}
	public class UdpPacketWrapper
	{
		public ushort SourcePort { get; set; }

		public ushort DestinationPort { get; set; }

		public ushort Length { get; private set; }

		public ushort Checksum { get; private set; }

		public IPAddress dest_ip { get; set; }

		public IPAddress src_ip { get; set; }

		public byte[] Data { get; private set; }

		public UdpPacketWrapper(byte[] userData, ushort sourcePort, ushort destinationPort, IPAddress IP_Destination, IPAddress IP_Source)
		{
			Data = new byte[userData.Length];
			Buffer.BlockCopy(userData, 0, Data, 0, userData.Length);
			dest_ip = IP_Destination;
			src_ip = IP_Source;
			SourcePort = sourcePort;
			DestinationPort = destinationPort;
			Length = (ushort)(8 + Data.Length);
			Checksum = 0;
			byte[] packetBytes_Checksum = GetPacketBytes_Checksum();
			Checksum = CalculateChecksum(packetBytes_Checksum);
		}

		private static ushort CalculateChecksum(byte[] upd_psy_frame)
		{
			int num = 0;
			for (int i = 0; i < upd_psy_frame.Length; i += 2)
			{
				ushort num2 = ((i + 1 >= upd_psy_frame.Length) ? ((ushort)(upd_psy_frame[i] << 8)) : ((ushort)((upd_psy_frame[i] << 8) + upd_psy_frame[i + 1])));
				num += num2;
				if ((num & 0xFFFF0000u) != 0)
				{
					num = (num & 0xFFFF) + (num >> 16);
				}
			}
			return (ushort)(~num);
		}

		public byte[] GetPacketBytes()
		{
			byte[] array = new byte[8]
			{
				(byte)(SourcePort >> 8),
				(byte)(SourcePort & 0xFFu),
				(byte)(DestinationPort >> 8),
				(byte)(DestinationPort & 0xFFu),
				(byte)(Length >> 8),
				(byte)(Length & 0xFFu),
				(byte)(Checksum >> 8),
				(byte)(Checksum & 0xFFu)
			};
			byte[] array2 = new byte[array.Length + Data.Length];
			Buffer.BlockCopy(array, 0, array2, 0, array.Length);
			Buffer.BlockCopy(Data, 0, array2, array.Length, Data.Length);
			return array2;
		}

		public byte[] GetPacketBytes_Checksum()
		{
			byte[] array = new byte[8]
			{
				(byte)(SourcePort >> 8),
				(byte)(SourcePort & 0xFFu),
				(byte)(DestinationPort >> 8),
				(byte)(DestinationPort & 0xFFu),
				(byte)(Length >> 8),
				(byte)(Length & 0xFFu),
				(byte)(Checksum >> 8),
				(byte)(Checksum & 0xFFu)
			};
			byte[] array2 = new byte[12];
			byte[] addressBytes = src_ip.GetAddressBytes();
			array2[0] = addressBytes[0];
			array2[1] = addressBytes[1];
			array2[2] = addressBytes[2];
			array2[3] = addressBytes[3];
			addressBytes = dest_ip.GetAddressBytes();
			array2[4] = addressBytes[0];
			array2[5] = addressBytes[1];
			array2[6] = addressBytes[2];
			array2[7] = addressBytes[3];
			array2[8] = 0;
			array2[9] = 17;
			array2[10] = array[4];
			array2[11] = array[5];
			byte[] array3 = new byte[array.Length + Data.Length + 12];
			Buffer.BlockCopy(array2, 0, array3, 0, 12);
			Buffer.BlockCopy(array, 0, array3, 12, array.Length);
			Buffer.BlockCopy(Data, 0, array3, array.Length + 12, Data.Length);
			return array3;
		}

		public override string ToString()
		{
			return $"UDP Packet: SourcePort={SourcePort}, DestinationPort={DestinationPort}, Length={Length}, Checksum={Checksum}, Data={Convert.ToBase64String(Data.Take(Data.Length - (128 - Data.Length)).ToArray())}...";
		}
	}
	public struct IpMacAddressPair
	{
		public IPAddress IpAddress { get; set; }

		public PhysicalAddress MacAddress { get; set; }

		public string OLT_SN { get; set; }

		public long Aging_time { get; set; }

		public string Items_from { get; set; }

		public IpMacAddressPair(IPAddress ip, PhysicalAddress mac, string sn, long aging_time, string item_from = "OLT_Setting")
		{
			IpAddress = ip;
			MacAddress = mac;
			Aging_time = aging_time;
			Items_from = item_from;
			OLT_SN = sn;
		}

		public override string ToString()
		{
			return $"IP: {IpAddress}, MAC: {BitConverter.ToString(MacAddress.GetAddressBytes()).Replace("-", ":")}, SN:{OLT_SN}";
		}
	}
	public class OLT_HISTORY : Form
	{
		private string dbPath = "example.db";

		private string connectionString = "";

		private DatabaseHelper databaseHelper;

		private IContainer components = null;

		private DataGridView dataGridView_OLT_WL;

		private Label OLT_information_label;

		private ComboBox comboBox_OLT_IP_search;

		private Label label1;

		private ComboBox items_number;

		private TextBox textBox_lut_timebase;

		private Label label2;

		private Button button_Search_OLT_Performance;

		public OLT_HISTORY(string filepath)
		{
			InitializeComponent();
			OLT_information_label.ForeColor = ColorTranslator.FromHtml("#212519");
			label1.ForeColor = ColorTranslator.FromHtml("#212519");
			label2.ForeColor = ColorTranslator.FromHtml("#212519");
			button_Search_OLT_Performance.FlatStyle = FlatStyle.Flat;
			button_Search_OLT_Performance.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#14C9BB");
			button_Search_OLT_Performance.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 20, 201, 187);
			button_Search_OLT_Performance.BackColor = ColorTranslator.FromHtml("#FFFFFF");
			button_Search_OLT_Performance.ForeColor = ColorTranslator.FromHtml("#14C9BB");
			dataGridView_OLT_WL.BackgroundColor = ColorTranslator.FromHtml("#F6F6F6");
			dataGridView_OLT_WL.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#14C9BB");
			dbPath = filepath;
			connectionString = "Data Source=" + dbPath + ";Version=3;";
			databaseHelper = new DatabaseHelper(dbPath, connectionString, 1.0);
			OLT_information_label.Text = "History information: OLTs:";
			items_number.SelectedIndex = 0;
			base.Icon = new Icon("FSLogo.ico");
		}

		private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
		{
		}

		private int time_parser(string dateString, out DateTime dateTime)
		{
			int result = 0;
			dateTime = DateTime.Now;
			if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result2))
			{
				dateTime = result2;
			}
			else
			{
				result = -1;
			}
			return result;
		}

		private void button_Search_OLT_Performance_Click(object sender, EventArgs e)
		{
			string text = textBox_lut_timebase.Text.Trim();
			string text2 = comboBox_OLT_IP_search.Text;
			double num = 0.0;
			int num2 = 0;
			if (text == "")
			{
				num = 0.0;
			}
			else
			{
				if (time_parser(text, out var dateTime) != 0)
				{
					MessageBox.Show("Date input is not right, please check the format YYYY-MM-DD");
					return;
				}
				num = (dateTime - new DateTime(1970, 1, 1)).TotalSeconds;
			}
			if (text2.Trim() != "")
			{
				try
				{
					IPAddress iPAddress = IPAddress.Parse(text2);
				}
				catch (Exception ex)
				{
					MessageBox.Show("IP address format is not right" + ex.Message);
				}
			}
			try
			{
				string[] array = items_number.Text.Trim().Split(',', ' ');
				num2 = ((array.Length <= 2) ? int.Parse(array[0]) : int.Parse(array[1]));
			}
			catch
			{
				num2 = 2000;
			}
			string text3 = "";
			text3 = ((!(text2.Trim() != "")) ? $"select * from  OLT_Performance where time_second > '{num}' ORDER BY time_second DESC LIMIT {num2} " : $"select * from  OLT_Performance where olt_ip ='{text2}' and time_second > '{num}' ORDER BY time_second DESC LIMIT {num2} ");
			try
			{
				int num3 = 0;
				if (databaseHelper.sql_search(text3, out DataTable processTable) == 0)
				{
					dataGridView_OLT_WL.DataSource = processTable;
					dataGridView_OLT_WL.Columns["id"].Visible = false;
					dataGridView_OLT_WL.Columns["time_second"].Visible = false;
					PopulateComboBoxWithUniqueIPs(processTable, comboBox_OLT_IP_search);
					comboBox_OLT_IP_search.SelectedIndex = 0;
				}
			}
			catch
			{
			}
		}

		public void PopulateComboBoxWithUniqueIPs(DataTable processTable, ComboBox comboBox)
		{
			comboBox.Items.Clear();
			if (processTable == null || processTable.Rows.Count == 0)
			{
				comboBox.Items.Add("No data available");
				return;
			}
			if (!processTable.Columns.Contains("olt_ip"))
			{
				comboBox.Items.Add("Column 'olt_ip' not found");
				return;
			}
			try
			{
				List<string> list = (from ip in (from row in processTable.AsEnumerable()
						select row.Field<string>("olt_ip") into ip
						where !string.IsNullOrWhiteSpace(ip)
						select ip).Distinct()
					orderby ip
					select ip).ToList();
				if (list.Any())
				{
					ComboBox.ObjectCollection items = comboBox.Items;
					object[] items2 = list.ToArray();
					items.AddRange(items2);
				}
				else
				{
					comboBox.Items.Add("No valid IP addresses found");
				}
			}
			catch (Exception ex)
			{
				comboBox.Items.Add("Error loading IPs");
				MessageBox.Show("Failed to load IP addresses: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		private void comboBox_OLT_stick_ADD_SelectedIndexChanged(object sender, EventArgs e)
		{
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.dataGridView_OLT_WL = new System.Windows.Forms.DataGridView();
			this.OLT_information_label = new System.Windows.Forms.Label();
			this.comboBox_OLT_IP_search = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.items_number = new System.Windows.Forms.ComboBox();
			this.textBox_lut_timebase = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.button_Search_OLT_Performance = new System.Windows.Forms.Button();
			((System.ComponentModel.ISupportInitialize)this.dataGridView_OLT_WL).BeginInit();
			base.SuspendLayout();
			this.dataGridView_OLT_WL.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView_OLT_WL.Location = new System.Drawing.Point(12, 88);
			this.dataGridView_OLT_WL.Name = "dataGridView_OLT_WL";
			this.dataGridView_OLT_WL.RowHeadersWidth = 51;
			this.dataGridView_OLT_WL.Size = new System.Drawing.Size(933, 447);
			this.dataGridView_OLT_WL.TabIndex = 2;
			this.OLT_information_label.AutoSize = true;
			this.OLT_information_label.ForeColor = System.Drawing.SystemColors.MenuHighlight;
			this.OLT_information_label.Location = new System.Drawing.Point(12, 22);
			this.OLT_information_label.Name = "OLT_information_label";
			this.OLT_information_label.Size = new System.Drawing.Size(158, 20);
			this.OLT_information_label.TabIndex = 4;
			this.OLT_information_label.Text = "Current Active OLTs:";
			this.comboBox_OLT_IP_search.FormattingEnabled = true;
			this.comboBox_OLT_IP_search.Location = new System.Drawing.Point(12, 45);
			this.comboBox_OLT_IP_search.Name = "comboBox_OLT_IP_search";
			this.comboBox_OLT_IP_search.Size = new System.Drawing.Size(265, 28);
			this.comboBox_OLT_IP_search.TabIndex = 10;
			this.comboBox_OLT_IP_search.SelectedIndexChanged += new System.EventHandler(comboBox_OLT_stick_ADD_SelectedIndexChanged);
			this.label1.AutoSize = true;
			this.label1.ForeColor = System.Drawing.SystemColors.MenuHighlight;
			this.label1.Location = new System.Drawing.Point(292, 22);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(188, 20);
			this.label1.TabIndex = 12;
			this.label1.Text = "TimeBase(YYYY-MM-DD)";
			this.items_number.FormattingEnabled = true;
			this.items_number.Items.AddRange(new object[6] { "Last 100", "Last 200", "Last 500", "Last 1000", "Last 2000", "" });
			this.items_number.Location = new System.Drawing.Point(546, 45);
			this.items_number.Name = "items_number";
			this.items_number.Size = new System.Drawing.Size(280, 28);
			this.items_number.TabIndex = 13;
			this.items_number.SelectedIndexChanged += new System.EventHandler(comboBox1_SelectedIndexChanged);
			this.textBox_lut_timebase.Location = new System.Drawing.Point(292, 45);
			this.textBox_lut_timebase.Multiline = true;
			this.textBox_lut_timebase.Name = "textBox_lut_timebase";
			this.textBox_lut_timebase.Size = new System.Drawing.Size(228, 28);
			this.textBox_lut_timebase.TabIndex = 14;
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.SystemColors.MenuHighlight;
			this.label2.Location = new System.Drawing.Point(546, 22);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(109, 20);
			this.label2.TabIndex = 15;
			this.label2.Text = "ItemNumbers";
			this.button_Search_OLT_Performance.Location = new System.Drawing.Point(845, 44);
			this.button_Search_OLT_Performance.Name = "button_Search_OLT_Performance";
			this.button_Search_OLT_Performance.Size = new System.Drawing.Size(100, 29);
			this.button_Search_OLT_Performance.TabIndex = 16;
			this.button_Search_OLT_Performance.Text = "Search";
			this.button_Search_OLT_Performance.UseVisualStyleBackColor = true;
			this.button_Search_OLT_Performance.Click += new System.EventHandler(button_Search_OLT_Performance_Click);
			base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(994, 547);
			base.Controls.Add(this.button_Search_OLT_Performance);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox_lut_timebase);
			base.Controls.Add(this.items_number);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.comboBox_OLT_IP_search);
			base.Controls.Add(this.OLT_information_label);
			base.Controls.Add(this.dataGridView_OLT_WL);
			base.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
			base.MaximizeBox = false;
			base.Name = "OLT_HISTORY";
			base.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
			this.Text = "OLT_History";
			((System.ComponentModel.ISupportInitialize)this.dataGridView_OLT_WL).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
	public class SN_Change
	{
		public static byte[] ConvertStringToByteArray(string input)
		{
			if (input.Length != 12)
			{
				throw new ArgumentException("length must be 12");
			}
			byte[] array = new byte[8];
			for (int i = 0; i < 4; i++)
			{
				array[i] = (byte)input[i];
			}
			for (int j = 4; j < 8; j++)
			{
				int startIndex = (j - 4) * 2 + 4;
				string value = input.Substring(startIndex, 2);
				array[j] = Convert.ToByte(value, 16);
			}
			return array;
		}

		public static string ConvertByteArrayToString(byte[] input)
		{
			if (input.Length != 8)
			{
				throw new ArgumentException("input array must be 8 ");
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < 4; i++)
			{
				stringBuilder.Append((char)input[i]);
			}
			for (int j = 4; j < 8; j++)
			{
				stringBuilder.Append(input[j].ToString("X2"));
			}
			return stringBuilder.ToString();
		}
	}
	public class OLT_IP_Change_Notify
	{
		public IPAddress OLT_IP_OLD;

		public IPAddress OLT_IP_NEW;

		public bool Change_Notify;

		public double init_timing;

		public OLT_IP_Change_Notify()
		{
			OLT_IP_OLD = IPAddress.None;
			OLT_IP_NEW = IPAddress.None;
			Change_Notify = false;
			init_timing = 0.0;
		}
	}
	public class Password_Change_Notify
	{
		public IPAddress OLT_IP;

		public bool Change_Notify;

		public byte change_type;

		public bool flag;

		public Password_Change_Notify()
		{
			OLT_IP = IPAddress.None;
			Change_Notify = false;
			change_type = 0;
			flag = false;
		}
	}
	internal class CPE_WHITE_LIST_Change_Notify
	{
		public ONU_Whitelst_Info oNU_Whitelst_Info;

		public bool Change_Notify;

		public double init_timing;

		public CPE_WHITE_LIST_Change_Notify()
		{
			oNU_Whitelst_Info = new ONU_Whitelst_Info();
			Change_Notify = false;
			init_timing = 0.0;
		}
	}
	public class Del_CPs_Notify
	{
		public IPAddress iPAddress;

		public bool Change_Notify;

		public double init_timing;

		public Del_CPs_Notify()
		{
			iPAddress = IPAddress.None;
			Change_Notify = false;
			init_timing = 0.0;
		}
	}
	public class ONU_Service_Type_Change_Notify
	{
		public ONU_Service_Type_Info oNU_Service_Type_Info;

		public bool Change_Notify;

		public double init_timing;

		public ONU_Service_Type_Change_Notify()
		{
			oNU_Service_Type_Info = new ONU_Service_Type_Info();
			Change_Notify = false;
			init_timing = 0.0;
		}
	}
	public class OLT_SDK_Upgrade_Notify
	{
		public IPAddress iPAddress;

		public bool Change_Notify;

		public double init_timing;

		public byte[] bin_file = null;

		public string FW_Package_type;

		public OLT_SDK_Upgrade_Notify()
		{
			iPAddress = IPAddress.None;
			Change_Notify = false;
			init_timing = 0.0;
			FW_Package_type = "";
		}
	}
	public class CPE_Service_Type_Push_Notify
	{
		public IPAddress iPAddress;

		public bool Change_Notify;

		public double init_timing;

		public CPE_Service_Type_Push_Notify()
		{
			iPAddress = IPAddress.None;
			Change_Notify = false;
			init_timing = 0.0;
		}
	}
	public struct Package_Timing_Management
	{
		public double sending_time;

		public double received_time;

		public double check_time;

		public double age_time;

		public Package_Timing_Management(double _sending_time, double _received_time, double _check_time, double _age_time)
		{
			sending_time = _sending_time;
			check_time = _check_time;
			age_time = _age_time;
			received_time = _received_time;
		}
	}
	internal struct Cpe_stable_struc
	{
		public int cpe_number;

		public uint cpe_checksum;

		public bool cpe_change;

		public double cpe_change_time;

		public double cpe_parse_time;

		public bool cpe_read_back_enable;

		public double cpe_read_ending_time;

		public double cpe_read_ending_time_spec;

		public bool reading_ending_flag_WD;

		public bool GEN_reading_ending_flag_WD;

		public bool send_list_command_done;

		public Cpe_stable_struc()
		{
			cpe_number = -1;
			cpe_checksum = 1u;
			cpe_change = false;
			cpe_change_time = 0.0;
			cpe_parse_time = 10.0;
			cpe_read_back_enable = false;
			cpe_read_ending_time = 0.0;
			cpe_read_ending_time_spec = 15.0;
			reading_ending_flag_WD = false;
			GEN_reading_ending_flag_WD = false;
			send_list_command_done = false;
		}
	}
	internal struct White_List_CPE
	{
		public string CPE_SN;

		public string ONU_Service_Type;

		public string CPE_ACTIVE;

		public White_List_CPE()
		{
			CPE_SN = "";
			ONU_Service_Type = "";
			CPE_ACTIVE = "";
		}
	}
	internal struct CPE_Alarm_Report
	{
		public string CPE_SN;

		public ushort ONU_Alarm;

		public CPE_Alarm_Report()
		{
			CPE_SN = "";
			ONU_Alarm = 0;
		}
	}
	internal enum ONU_Status_Code
	{
		REG_DONE = 1,
		AUTH_DONE = 2,
		OMCI_DONE = 3,
		ETH_CFG = 4,
		ETH_DONE = 5,
		ONLINE = 6,
		LOSFI = 7,
		OFFLINE = 8,
		LONGLU = 9,
		ETH_CFGING = 10,
		ETH_CFGING_DOWN = 11,
		INIT = 0
	}
	public class OLT_Status_Report
	{
		public IPAddress olt_ip;

		public string olt_sn = "";

		public string temperature = string.Empty;

		public string voltage = string.Empty;

		public string tx_power = string.Empty;

		public string tx_bias = string.Empty;

		public string rx_power = string.Empty;

		public byte alarm;

		public byte reserved1;

		public byte reserved2;

		public string reading_time_str = string.Empty;

		public double reading_time_double = 0.0;

		public OLT_Status_Report()
		{
			olt_ip = IPAddress.None;
			reading_time_double = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			reading_time_str = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		}
	}
	public class ONU_STATUS_SN
	{
		public byte onu_id;

		public byte[] ONU_SN;

		public byte onu_state;

		public byte eth_num;

		public byte complete_config;

		public ONU_STATUS_SN()
		{
			onu_id = 0;
			ONU_SN = new byte[8];
			onu_state = 0;
			eth_num = 0;
			complete_config = 0;
		}
	}
	public class ONU_STATUS_SN_list
	{
		public List<ONU_STATUS_SN> oNU_STATUS_SNs_List;

		public string reading_time_str;

		public double reading_time_double;

		public ONU_STATUS_SN_list()
		{
			oNU_STATUS_SNs_List = new List<ONU_STATUS_SN>();
			reading_time_double = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			reading_time_str = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		}

		public void Add(ONU_STATUS_SN temp_sn)
		{
			oNU_STATUS_SNs_List.Add(temp_sn);
		}
	}
	public class ONU_Alarm
	{
		public byte[] ONU_SN;

		public byte onu_alarm;

		public byte onu_state_reserved;

		public ONU_Alarm()
		{
			ONU_SN = new byte[8];
			onu_alarm = 0;
			onu_state_reserved = 0;
		}
	}
	public class ONU_Alarm_list
	{
		public List<ONU_Alarm> oNU_Alarm_List;

		public string reading_time_str;

		public double reading_time_double;

		public ONU_Alarm_list()
		{
			oNU_Alarm_List = new List<ONU_Alarm>();
			reading_time_double = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			reading_time_str = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		}

		public void Add(ONU_Alarm temp_sn)
		{
			oNU_Alarm_List.Add(temp_sn);
		}
	}
	public class OLT_share_data
	{
		public IPAddress iPAddress;

		public PhysicalAddress physicalAddress;

		public string OLT_SN;

		public string Current_status;

		public int olt_stick_Lifecycle;

		public string Comment;

		public string OLT_On_Board_time;

		public int age_time;

		public double OLT_online_time;

		public bool active;

		public int status_int;

		public int OLT_offline_Counter;

		public string sql_station;

		public int olt_Read_accessable_level;

		public int olt_Write_accessable_level;

		public OLT_share_data()
		{
			iPAddress = IPAddress.None;
			physicalAddress = PhysicalAddress.None;
			OLT_SN = "";
			Current_status = "INIT";
			age_time = 0;
			olt_stick_Lifecycle = 0;
			Comment = "";
			OLT_On_Board_time = "";
			OLT_online_time = 0.0;
			active = true;
			status_int = 0;
			OLT_offline_Counter = 50;
			sql_station = "spare";
			olt_Read_accessable_level = -1;
			olt_Write_accessable_level = -1;
		}
	}
	internal struct ONU_Service_Type_Info_Stru
	{
		public List<ONU_Service_Type_Info> oNU_Service_Type_Info_List;

		public ONU_Service_Type_Info_Stru()
		{
			oNU_Service_Type_Info_List = new List<ONU_Service_Type_Info>();
		}
	}
	internal class ONU_ServiceType_GET
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public ONU_Service_Type_Info_Stru oNU_Service_Type_Info_List_stru;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		public bool read_immediately;

		public double last_sending_time;

		public int read_index;

		public ONU_ServiceType_GET(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			oNU_Service_Type_Info_List_stru = new ONU_Service_Type_Info_Stru();
			command = Command_Code.SERVICE_CONFIG_RPT;
			task_timing = new Package_Timing_Management(0.0, 0.0, 3.0, 7200.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			read_immediately = false;
			last_sending_time = 0.0;
			read_index = 0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC)
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[5];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			uDP_Analysis_Package.content[3] = (byte)(read_index >> 8);
			uDP_Analysis_Package.content[4] = (byte)((uint)read_index & 0xFFu);
			uDP_Analysis_Package.task_owner = Task_Owner.SERVICE_CONFIG_RPT;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
				last_sending_time = task_timing.sending_time;
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public int analysis_package(out byte[] content, out int Package_length)
		{
			int result = 0;
			bool flag = false;
			content = null;
			Package_length = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (uDP_Analysis_Packages_receive.IsEmpty)
			{
				if (totalSeconds - task_timing.sending_time > task_timing.check_time)
				{
					return 99;
				}
			}
			else
			{
				UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
				if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result2))
				{
					UDP_Analysis_Package result3;
					while (uDP_Analysis_Packages_send.TryPeek(out result3))
					{
						if (result3.upd_ID == result2.upd_ID)
						{
							uDP_Analysis_Package = result2;
							if (uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _))
							{
								content = result2.content;
								Package_length = result2.UDP_package_length;
							}
							else
							{
								content = new byte[1];
							}
							flag = true;
							result = 1;
						}
						else
						{
							if (uDP_Analysis_Packages_send.Count <= 1)
							{
								break;
							}
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
						}
					}
				}
			}
			if (!flag)
			{
				content = new byte[1];
			}
			return result;
		}

		public int onuservicetpye_read(MAC mAC)
		{
			int result = 0;
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			bool flag = false;
			_share_data.sql_station = "ServiceTypeRead";
			if (totalSeconds - last_sending_time > task_timing.age_time)
			{
				flag = false;
			}
			if (read_immediately || flag)
			{
				read_immediately = false;
				if (send_package(mAC) != 0)
				{
					result = -1;
				}
				else
				{
					int Package_length = 0;
					while (true)
					{
						byte[] content;
						switch (analysis_package(out content, out Package_length))
						{
						case 0:
							continue;
						case 1:
						{
							result = 1;
							int num2 = 5;
							if (Package_length <= 101)
							{
								break;
							}
							int num3 = (Package_length - 3) / 102;
							if (num3 >= num2)
							{
								num3 = num2;
							}
							oNU_Service_Type_Info_List_stru.oNU_Service_Type_Info_List = new List<ONU_Service_Type_Info>();
							for (int i = 0; i < num3; i++)
							{
								ONU_Service_Type_Info oNU_Service_Type_Info = new ONU_Service_Type_Info();
								oNU_Service_Type_Info.olt_ip = _share_data.iPAddress;
								int num4 = 102;
								oNU_Service_Type_Info.ONU_Service_Type_Name = $"reported from OLT {_share_data.iPAddress.ToString()} -{i}";
								oNU_Service_Type_Info.ONU_Service_Type_No = content[3 + i * num4];
								for (int j = 0; j < 5; j++)
								{
									int num5 = 20;
									ONU_Service_flow oNU_Service_flow = new ONU_Service_flow();
									oNU_Service_flow.onuid = content[4 + i * num4 + j * num5 + 1];
									oNU_Service_flow.wan_id = content[4 + i * num4 + j * num5 + 2];
									oNU_Service_flow.tcont_id = (ushort)(content[4 + i * num4 + j * num5 + 3] * 256 + content[4 + i * num4 + j * num5 + 4]);
									oNU_Service_flow.vlan_id = (ushort)(content[4 + i * num4 + j * num5 + 5] * 256 + content[4 + i * num4 + j * num5 + 6]);
									oNU_Service_flow.max = (ushort)(content[4 + i * num4 + j * num5 + 7] * 256 + content[4 + i * num4 + j * num5 + 8]);
									oNU_Service_flow.fix = (ushort)(content[4 + i * num4 + j * num5 + 9] * 256 + content[4 + i * num4 + j * num5 + 10]);
									oNU_Service_flow.ass = (ushort)(content[4 + i * num4 + j * num5 + 11] * 256 + content[4 + i * num4 + j * num5 + 12]);
									oNU_Service_flow.gem_port = (ushort)(content[4 + i * num4 + j * num5 + 13] * 256 + content[4 + i * num4 + j * num5 + 14]);
									oNU_Service_flow.type = content[4 + i * num4 + j * num5 + 15];
									oNU_Service_flow.priority = content[4 + i * num4 + j * num5 + 16];
									oNU_Service_flow.weight = content[4 + i * num4 + j * num5 + 17];
									oNU_Service_flow.valid = content[4 + i * num4 + j * num5 + 18];
									oNU_Service_flow.Reserved = (ushort)(content[4 + i * num4 + j * num5 + 19] * 256 + content[4 + i * num4 + j * num5 + 20]);
									oNU_Service_Type_Info.oNU_Service_Flows.Add(oNU_Service_flow);
								}
								oNU_Service_Type_Info_List_stru.oNU_Service_Type_Info_List.Add(oNU_Service_Type_Info);
							}
							break;
						}
						default:
							result = -2;
							break;
						}
						break;
					}
				}
			}
			return result;
		}
	}
	internal class ONU_Whitelst_GET
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public ConcurrentStack<CPE_WHITE_LIST_Change_Notify> cPE_WHITE_LIST_read_Notify;

		public ONU_Whitelst_Info oNU_Whitelst_Info = null;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		public bool read_immediately;

		public double last_sending_time;

		public int read_index;

		public ONU_Whitelst_GET(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			cPE_WHITE_LIST_read_Notify = new ConcurrentStack<CPE_WHITE_LIST_Change_Notify>();
			command = Command_Code.ONU_WLIST_RPT;
			task_timing = new Package_Timing_Management(0.0, 0.0, 3.0, 7200.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			read_immediately = false;
			last_sending_time = 0.0;
			read_index = 0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC)
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[5];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			uDP_Analysis_Package.content[3] = (byte)(read_index >> 8);
			uDP_Analysis_Package.content[4] = (byte)((uint)read_index & 0xFFu);
			uDP_Analysis_Package.task_owner = Task_Owner.ONU_WLIST_RPT;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
				last_sending_time = task_timing.sending_time;
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public int analysis_package(out byte[] content, out int Package_length)
		{
			int result = 0;
			bool flag = false;
			content = null;
			Package_length = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (uDP_Analysis_Packages_receive.IsEmpty)
			{
				if (totalSeconds - task_timing.sending_time > task_timing.check_time)
				{
					return 99;
				}
			}
			else
			{
				UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
				if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result2))
				{
					UDP_Analysis_Package result3;
					while (uDP_Analysis_Packages_send.TryPeek(out result3))
					{
						if (result3.upd_ID == result2.upd_ID)
						{
							uDP_Analysis_Package = result2;
							if (uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _))
							{
								content = result2.content;
								Package_length = result2.UDP_package_length;
							}
							else
							{
								content = new byte[1];
							}
							flag = true;
							result = 1;
						}
						else
						{
							if (uDP_Analysis_Packages_send.Count <= 1)
							{
								break;
							}
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
						}
					}
				}
			}
			if (!flag)
			{
				content = new byte[1];
			}
			return result;
		}

		public int onuwhitelst_read(MAC mAC)
		{
			int num = 0;
			int num2 = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			bool flag = false;
			_share_data.sql_station = "ONUWHLSTRead";
			if (totalSeconds - last_sending_time > task_timing.age_time)
			{
				flag = false;
			}
			if (read_immediately || flag)
			{
				read_immediately = false;
				bool flag2 = false;
				oNU_Whitelst_Info = new ONU_Whitelst_Info();
				int num3 = 0;
				read_index = 1;
				do
				{
					if (send_package(mAC) != 0)
					{
						num = -1;
					}
					else
					{
						int Package_length = 0;
						while (true)
						{
							byte[] content;
							switch (analysis_package(out content, out Package_length))
							{
							case 0:
								continue;
							case 1:
							{
								num = 1;
								int num4 = 60;
								ONU_Alarm_list oNU_Alarm_list = new ONU_Alarm_list();
								if (Package_length <= 5)
								{
									break;
								}
								int num5 = (Package_length - 5) / 10;
								if (num5 >= num4)
								{
									num5 = num4;
									flag2 = true;
								}
								else
								{
									flag2 = false;
								}
								for (int i = 0; i < num5; i++)
								{
									byte[] array = new byte[8];
									Array.Copy(content, 5 + i * 10, array, 0, 8);
									string snstr = "";
									try
									{
										snstr = HexStringParser.ByteArrayToString(array);
									}
									catch (Exception ex)
									{
										string message = ex.Message;
									}
									byte sertype = content[5 + i * 10 + 8];
									bool act = false;
									if (content[5 + i * 10 + 9] == 1)
									{
										act = true;
									}
									CPE_Info_struct item = new CPE_Info_struct(array, snstr, sertype, act, "");
									oNU_Whitelst_Info.cPE_Struct.Add(item);
								}
								if (num5 == num4)
								{
									read_index = num4 * (num3 + 1) + 1;
									flag2 = true;
								}
								break;
							}
							default:
								num = -2;
								break;
							}
							break;
						}
					}
					num3++;
				}
				while (flag2 && num == 1);
			}
			return num;
		}
	}
	internal struct CPE_Service_Type_send_items
	{
		public long send_ID;

		public CPE_Service_Type_send_items(long _send_ID)
		{
			send_ID = _send_ID;
		}
	}
	internal class OLT_STATUS_GET
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public ConcurrentStack<OLT_Status_Report> oLT_Status_Report_stack;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		public bool read_immediately;

		public double last_sending_time;

		public OLT_STATUS_GET(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			oLT_Status_Report_stack = new ConcurrentStack<OLT_Status_Report>();
			command = Command_Code.olt_alarm_report;
			task_timing = new Package_Timing_Management(0.0, 0.0, 2.0, 10.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			read_immediately = false;
			last_sending_time = 0.0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC)
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[3];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			uDP_Analysis_Package.task_owner = Task_Owner.cpe_sn_status;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
				last_sending_time = task_timing.sending_time;
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public int analysis_package(out byte[] content, out int Package_length)
		{
			int result = 0;
			bool flag = false;
			content = null;
			Package_length = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (uDP_Analysis_Packages_receive.IsEmpty)
			{
				if (totalSeconds - task_timing.sending_time > task_timing.check_time)
				{
					return 99;
				}
			}
			else
			{
				UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
				if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result2))
				{
					UDP_Analysis_Package result3;
					while (uDP_Analysis_Packages_send.TryPeek(out result3))
					{
						if (result3.upd_ID == result2.upd_ID)
						{
							uDP_Analysis_Package = result2;
							if (uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _))
							{
								content = result2.content;
								Package_length = result2.UDP_package_length;
							}
							else
							{
								content = new byte[1];
							}
							flag = true;
							result = 1;
						}
						else
						{
							if (uDP_Analysis_Packages_send.Count <= 1)
							{
								break;
							}
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
						}
					}
				}
			}
			if (!flag)
			{
				content = new byte[1];
			}
			return result;
		}

		public int OLT_status_read(MAC mAC)
		{
			int result = 0;
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			bool flag = false;
			_share_data.sql_station = "OLTStatusRead";
			if (totalSeconds - last_sending_time > task_timing.age_time)
			{
				flag = true;
			}
			if (read_immediately || flag)
			{
				if (send_package(mAC) != 0)
				{
					result = -1;
				}
				else
				{
					int Package_length = 0;
					while (true)
					{
						byte[] content;
						switch (analysis_package(out content, out Package_length))
						{
						case 0:
							continue;
						case 1:
						{
							read_immediately = false;
							OLT_Status_Report oLT_Status_Report = new OLT_Status_Report();
							if (Package_length > 3)
							{
								oLT_Status_Report.olt_ip = _share_data.iPAddress;
								oLT_Status_Report.alarm = content[3];
								ushort num2 = (ushort)(content[4] + content[5] * 256);
								oLT_Status_Report.tx_power = (10.0 * Math.Log10((double)(int)num2 / 10000.0)).ToString("F2");
								ushort num3 = (ushort)(content[6] + content[7] * 256);
								oLT_Status_Report.tx_bias = ((double)(int)num3 / 500.0).ToString("F2");
								short num4 = (short)(ushort)(content[8] + content[9] * 256);
								oLT_Status_Report.temperature = ((double)num4 / 256.0).ToString("F2");
								ushort num5 = (ushort)(content[10] + content[11] * 256);
								oLT_Status_Report.voltage = ((double)(int)num5 / 10000.0).ToString("F2");
								oLT_Status_Report.reserved1 = content[12];
								oLT_Status_Report.reserved2 = content[13];
								oLT_Status_Report.olt_sn = _share_data.OLT_SN;
								oLT_Status_Report_stack.Clear();
								oLT_Status_Report_stack.Push(oLT_Status_Report);
								result = 1;
							}
							break;
						}
						default:
							result = -2;
							break;
						}
						break;
					}
				}
			}
			return result;
		}
	}
	internal class ONU_Alarm_GET
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public ConcurrentStack<ONU_Alarm_list> oNU_Alarm_list_Stack;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		public bool read_immediately;

		public double last_sending_time;

		public ONU_Alarm_GET(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			oNU_Alarm_list_Stack = new ConcurrentStack<ONU_Alarm_list>();
			command = Command_Code.cpe_alarm_report;
			task_timing = new Package_Timing_Management(0.0, 0.0, 2.0, 10.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			read_immediately = false;
			last_sending_time = 0.0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC)
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[3];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			uDP_Analysis_Package.task_owner = Task_Owner.cpe_sn_status;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
				last_sending_time = task_timing.sending_time;
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public int analysis_package(out byte[] content, out int Package_length)
		{
			int result = 0;
			bool flag = false;
			content = null;
			Package_length = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (uDP_Analysis_Packages_receive.IsEmpty)
			{
				if (totalSeconds - task_timing.sending_time > task_timing.check_time)
				{
					return 99;
				}
			}
			else
			{
				UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
				if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result2))
				{
					UDP_Analysis_Package result3;
					while (uDP_Analysis_Packages_send.TryPeek(out result3))
					{
						if (result3.upd_ID == result2.upd_ID)
						{
							uDP_Analysis_Package = result2;
							if (uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _))
							{
								content = result2.content;
								Package_length = result2.UDP_package_length;
							}
							else
							{
								content = new byte[1];
							}
							flag = true;
							result = 1;
						}
						else
						{
							if (uDP_Analysis_Packages_send.Count <= 1)
							{
								break;
							}
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
						}
					}
				}
			}
			if (!flag)
			{
				content = new byte[1];
			}
			return result;
		}

		public int onualarm_read(MAC mAC)
		{
			int result = 0;
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			bool flag = false;
			_share_data.sql_station = "ONUAlarmRead";
			if (totalSeconds - last_sending_time > task_timing.age_time)
			{
				flag = true;
			}
			if (read_immediately || flag)
			{
				if (send_package(mAC) != 0)
				{
					result = -1;
				}
				else
				{
					int Package_length = 0;
					while (true)
					{
						byte[] content;
						switch (analysis_package(out content, out Package_length))
						{
						case 0:
							continue;
						case 1:
						{
							result = 1;
							read_immediately = false;
							ONU_Alarm_list oNU_Alarm_list = new ONU_Alarm_list();
							if (Package_length > 3)
							{
								int num2 = (Package_length - 3) / 10;
								for (int i = 0; i < num2; i++)
								{
									ONU_Alarm oNU_Alarm = new ONU_Alarm();
									Array.Copy(content, 3 + i * 8, oNU_Alarm.ONU_SN, 0, 8);
									oNU_Alarm.onu_alarm = content[3 + i * 8 + 8];
									oNU_Alarm.onu_state_reserved = content[3 + i * 8 + 9];
									oNU_Alarm_list.Add(oNU_Alarm);
								}
								oNU_Alarm_list_Stack.Clear();
								oNU_Alarm_list_Stack.Push(oNU_Alarm_list);
							}
							break;
						}
						default:
							result = -2;
							break;
						}
						break;
					}
				}
			}
			return result;
		}
	}
	internal class ONU_SN_STATUS_GET
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public ConcurrentStack<ONU_STATUS_SN_list> oNU_STATUS_SNs;

		public ONU_STATUS_SN_list Current_ONU_STATUS_SN_list;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		public bool read_immediately;

		public double last_sending_time;

		public ONU_SN_STATUS_GET(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			oNU_STATUS_SNs = new ConcurrentStack<ONU_STATUS_SN_list>();
			Current_ONU_STATUS_SN_list = null;
			command = Command_Code.cpe_sn_status;
			task_timing = new Package_Timing_Management(0.0, 0.0, 2.0, 10.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			read_immediately = false;
			last_sending_time = 0.0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC)
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[3];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			uDP_Analysis_Package.task_owner = Task_Owner.cpe_sn_status;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
				last_sending_time = task_timing.sending_time;
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public int analysis_package(out byte[] content, out int Package_length)
		{
			int result = 0;
			bool flag = false;
			content = null;
			Package_length = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (uDP_Analysis_Packages_receive.IsEmpty)
			{
				if (totalSeconds - task_timing.sending_time > task_timing.check_time)
				{
					return 99;
				}
			}
			else
			{
				UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
				if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result2))
				{
					UDP_Analysis_Package result3;
					while (uDP_Analysis_Packages_send.TryPeek(out result3))
					{
						if (result3.upd_ID == result2.upd_ID)
						{
							uDP_Analysis_Package = result2;
							if (uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _))
							{
								content = result2.content;
								Package_length = result2.UDP_package_length;
							}
							else
							{
								content = new byte[1];
							}
							flag = true;
							result = 1;
						}
						else
						{
							if (uDP_Analysis_Packages_send.Count <= 1)
							{
								break;
							}
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
						}
					}
				}
			}
			if (!flag)
			{
				content = new byte[1];
			}
			return result;
		}

		public int onusn_read(MAC mAC)
		{
			int result = 0;
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			bool flag = false;
			_share_data.sql_station = "ONUSN_read";
			if (totalSeconds - last_sending_time > task_timing.age_time)
			{
				flag = true;
			}
			if (read_immediately || flag)
			{
				read_immediately = false;
				if (send_package(mAC) != 0)
				{
					result = -1;
				}
				else
				{
					int Package_length = 0;
					while (true)
					{
						byte[] content;
						switch (analysis_package(out content, out Package_length))
						{
						case 0:
							continue;
						case 1:
						{
							ONU_STATUS_SN_list oNU_STATUS_SN_list = new ONU_STATUS_SN_list();
							if (Package_length > 3)
							{
								int num2 = (Package_length - 3) / 10;
								for (int i = 0; i < num2; i++)
								{
									ONU_STATUS_SN oNU_STATUS_SN = new ONU_STATUS_SN();
									Array.Copy(content, 3 + i * 10, oNU_STATUS_SN.ONU_SN, 0, 8);
									oNU_STATUS_SN.onu_state = content[3 + i * 10 + 8];
									oNU_STATUS_SN.eth_num = content[3 + i * 10 + 9];
									oNU_STATUS_SN_list.Add(oNU_STATUS_SN);
								}
								if (num2 >= 1)
								{
									oNU_STATUS_SNs.Clear();
									oNU_STATUS_SNs.Push(oNU_STATUS_SN_list);
									Current_ONU_STATUS_SN_list = oNU_STATUS_SN_list;
									result = 1;
								}
							}
							break;
						}
						default:
							result = -2;
							break;
						}
						break;
					}
				}
			}
			return result;
		}
	}
	internal class CPE_ServiceType_Set
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public ConcurrentStack<ONU_Service_Type_Change_Notify> oNU_Service_Type_Change_Notify_stack;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		public CPE_ServiceType_Set(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			oNU_Service_Type_Change_Notify_stack = new ConcurrentStack<ONU_Service_Type_Change_Notify>();
			command = Command_Code.cpe_service_type_send;
			task_timing = new Package_Timing_Management(0.0, 0.0, 2.0, 10.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC, byte[] content)
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[3 + content.Length];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			Array.Copy(content, 0, uDP_Analysis_Package.content, 3, content.Length);
			uDP_Analysis_Package.task_owner = Task_Owner.cpe_service_type_send;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public int analysis_package()
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (uDP_Analysis_Packages_receive.IsEmpty)
			{
				if (totalSeconds - task_timing.sending_time > task_timing.check_time)
				{
					return 99;
				}
			}
			else
			{
				UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
				if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result2))
				{
					UDP_Analysis_Package result3;
					while (uDP_Analysis_Packages_send.TryPeek(out result3))
					{
						if (result3.upd_ID == result2.upd_ID)
						{
							uDP_Analysis_Package = result2;
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
							if (result2.content[3] == 0)
							{
								return 1;
							}
							return 99;
						}
						if (uDP_Analysis_Packages_send.Count > 1)
						{
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
							continue;
						}
						break;
					}
				}
			}
			return result;
		}

		public int servicetype_push(MAC mAC)
		{
			int result = 0;
			int num = 0;
			if (_share_data.olt_stick_Lifecycle != 1)
			{
				return -6;
			}
			_share_data.sql_station = "ServiceTypeSet";
			if (!oNU_Service_Type_Change_Notify_stack.IsEmpty && oNU_Service_Type_Change_Notify_stack.TryPop(out ONU_Service_Type_Change_Notify result2))
			{
				if (result2 == null)
				{
					return -1;
				}
				oNU_Service_Type_Change_Notify_stack.Clear();
				int count = result2.oNU_Service_Type_Info.oNU_Service_Flows.Count;
				int oNU_Service_Type_No = result2.oNU_Service_Type_Info.ONU_Service_Type_No;
				byte[] array = new byte[2 + count * 20];
				array[0] = (byte)oNU_Service_Type_No;
				array[1] = (byte)count;
				for (int i = 0; i < count; i++)
				{
					array[i * 20 + 2] = result2.oNU_Service_Type_Info.oNU_Service_Flows[i].onuid;
					array[i * 20 + 3] = result2.oNU_Service_Type_Info.oNU_Service_Flows[i].wan_id;
					array[i * 20 + 4] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].tcont_id >> 8);
					array[i * 20 + 5] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].tcont_id & 0xFFu);
					array[i * 20 + 6] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].vlan_id >> 8);
					array[i * 20 + 7] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].vlan_id & 0xFFu);
					array[i * 20 + 8] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].max >> 8);
					array[i * 20 + 9] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].max & 0xFFu);
					array[i * 20 + 10] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].fix >> 8);
					array[i * 20 + 11] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].fix & 0xFFu);
					array[i * 20 + 12] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].ass >> 8);
					array[i * 20 + 13] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].ass & 0xFFu);
					array[i * 20 + 14] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].gem_port >> 8);
					array[i * 20 + 15] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].gem_port & 0xFFu);
					array[i * 20 + 16] = result2.oNU_Service_Type_Info.oNU_Service_Flows[i].type;
					array[i * 20 + 17] = result2.oNU_Service_Type_Info.oNU_Service_Flows[i].priority;
					array[i * 20 + 18] = result2.oNU_Service_Type_Info.oNU_Service_Flows[i].weight;
					array[i * 20 + 19] = result2.oNU_Service_Type_Info.oNU_Service_Flows[i].valid;
					array[i * 20 + 20] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].Reserved >> 8);
					array[i * 20 + 21] = (byte)(result2.oNU_Service_Type_Info.oNU_Service_Flows[i].Reserved & 0xFFu);
				}
				if (send_package(mAC, array) != 0)
				{
					result = -1;
				}
				else
				{
					while (true)
					{
						switch (analysis_package())
						{
						case 0:
							continue;
						case 1:
							result = 1;
							break;
						default:
							result = -2;
							break;
						}
						break;
					}
				}
			}
			return result;
		}
	}
	internal class CPE_WhiteLst_Set
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public ConcurrentStack<CPE_WHITE_LIST_Change_Notify> cpe_WHITE_LIST_Change_Notify_stack;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		public byte[] Last_whitelist_SN_Outofrange;

		public CPE_WhiteLst_Set(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			cpe_WHITE_LIST_Change_Notify_stack = new ConcurrentStack<CPE_WHITE_LIST_Change_Notify>();
			command = Command_Code.cpe_white_list_send;
			task_timing = new Package_Timing_Management(0.0, 0.0, 2.0, 10.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			Last_whitelist_SN_Outofrange = new byte[8];
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC, byte[] content)
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[3 + content.Length];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			Array.Copy(content, 0, uDP_Analysis_Package.content, 3, content.Length);
			uDP_Analysis_Package.task_owner = Task_Owner.cpe_white_list_send;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public int analysis_package()
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (uDP_Analysis_Packages_receive.IsEmpty)
			{
				if (totalSeconds - task_timing.sending_time > task_timing.check_time)
				{
					return 99;
				}
			}
			else
			{
				UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
				if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result2))
				{
					UDP_Analysis_Package result3;
					while (uDP_Analysis_Packages_send.TryPeek(out result3))
					{
						if (result3.upd_ID == result2.upd_ID)
						{
							uDP_Analysis_Package = result2;
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
							Last_whitelist_SN_Outofrange = new byte[8];
							if (result2.content[3] == 0)
							{
								return 1;
							}
							if (result2.content[3] == 2)
							{
								Array.Copy(result2.content, 4, Last_whitelist_SN_Outofrange, 0, 8);
								return 2;
							}
							return 99;
						}
						if (uDP_Analysis_Packages_send.Count > 1)
						{
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
							continue;
						}
						break;
					}
				}
			}
			return result;
		}

		public int whitelst_push(MAC mAC)
		{
			int result = 0;
			int num = 0;
			if (_share_data.olt_stick_Lifecycle != 1)
			{
				return -7;
			}
			_share_data.sql_station = "WhlstSet";
			if (!cpe_WHITE_LIST_Change_Notify_stack.IsEmpty)
			{
				if (cpe_WHITE_LIST_Change_Notify_stack.TryPop(out CPE_WHITE_LIST_Change_Notify result2))
				{
					if (result2 == null)
					{
						return -1;
					}
					cpe_WHITE_LIST_Change_Notify_stack.Clear();
					int count = result2.oNU_Whitelst_Info.cPE_Struct.Count;
					if (count == 0)
					{
						return 0;
					}
					int num2 = 100;
					count /= num2;
					int num3 = 0;
					for (num3 = 0; num3 < count; num3++)
					{
						byte[] array = new byte[num2 * 10];
						for (int i = 0; i < num2; i++)
						{
							Array.Copy(result2.oNU_Whitelst_Info.cPE_Struct[num3 * num2 + i].CPE_SN, 0, array, i * 10, 8);
							array[i * 10 + 8] = result2.oNU_Whitelst_Info.cPE_Struct[num3 * num2 + i].service_type;
							byte b = 0;
							if (result2.oNU_Whitelst_Info.cPE_Struct[num3 * num2 + i].active)
							{
								b = 1;
							}
							array[i * 10 + 9] = b;
						}
						if (send_package(mAC, array) != 0)
						{
							result = -1;
							continue;
						}
						while (true)
						{
							switch (analysis_package())
							{
							case 0:
								continue;
							case 1:
								result = 1;
								break;
							case 2:
								result = 2;
								break;
							default:
								result = -2;
								break;
							}
							break;
						}
					}
					if (result2.oNU_Whitelst_Info.cPE_Struct.Count % num2 != 0 && num3 == count)
					{
						int num4 = result2.oNU_Whitelst_Info.cPE_Struct.Count % num2;
						byte[] array2 = new byte[num4 * 10];
						for (int j = 0; j < num4; j++)
						{
							Array.Copy(result2.oNU_Whitelst_Info.cPE_Struct[count * num2 + j].CPE_SN, 0, array2, j * 10, 8);
							array2[j * 10 + 8] = result2.oNU_Whitelst_Info.cPE_Struct[count * num2 + j].service_type;
							byte b2 = 0;
							if (result2.oNU_Whitelst_Info.cPE_Struct[count * num2 + j].active)
							{
								b2 = 1;
							}
							array2[j * 10 + 9] = b2;
						}
						if (send_package(mAC, array2) != 0)
						{
							result = -1;
						}
						else
						{
							while (true)
							{
								switch (analysis_package())
								{
								case 0:
									continue;
								case 1:
									result = 1;
									break;
								case 2:
									result = 2;
									break;
								default:
									result = -2;
									break;
								}
								break;
							}
						}
					}
				}
			}
			else
			{
				result = -3;
			}
			return result;
		}
	}
	internal class PWD_Set
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public ConcurrentStack<Password_Change_Notify> password_Change_Notify_write;

		public ConcurrentStack<Password_Change_Notify> password_Change_Notify_read;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public double last_sending_package_time;

		public ushort Frame_Sent_ID_Code;

		public string Vendor_Write_Key;

		public string Vendor_Read_Key;

		public byte change_W;

		public byte change_R;

		private int PWD_change_status;

		private bool PWD_change_enable;

		public PWD_Set(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			password_Change_Notify_write = new ConcurrentStack<Password_Change_Notify>();
			password_Change_Notify_read = new ConcurrentStack<Password_Change_Notify>();
			command = Command_Code.Password_cmd;
			task_timing = new Package_Timing_Management(0.0, 0.0, 1.0, 3.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			PWD_change_status = 0;
			PWD_change_enable = false;
			Vendor_Write_Key = "";
			Vendor_Read_Key = "";
			change_W = 0;
			change_R = 0;
			last_sending_package_time = 0.0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC, byte change_type, bool flag)
		{
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			string input = "aaaa";
			if (change_type == 82 || change_type == 114)
			{
				if (flag)
				{
					input = _share_data.OLT_SN.ToString() + Vendor_Read_Key;
				}
				change_type = 82;
			}
			else if (change_type == 87 || change_type == 119)
			{
				if (flag)
				{
					input = _share_data.OLT_SN.ToString() + Vendor_Write_Key;
				}
				change_type = 87;
			}
			else
			{
				PWD_change_status = -1;
				num = -1;
			}
			MD5Hash mD5Hash = new MD5Hash();
			byte[] array = MD5Hash.ComputeMD5(input);
			if (array.Length != 16)
			{
				PWD_change_status = -1;
				num = -1;
			}
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[20];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			uDP_Analysis_Package.content[3] = change_type;
			Array.Copy(array, 0, uDP_Analysis_Package.content, 4, 16);
			uDP_Analysis_Package.task_owner = Task_Owner.ip_configuration;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				PWD_change_status = 0;
			}
			catch
			{
				PWD_change_status = -1;
				num = -1;
			}
			if (num == 0)
			{
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
			}
			return num;
		}

		public int analysis_package(byte change_type)
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (!PWD_change_enable)
			{
				if (uDP_Analysis_Packages_receive.IsEmpty)
				{
					if (totalSeconds - task_timing.sending_time > task_timing.check_time)
					{
						uDP_Analysis_Packages_send.Clear();
						PWD_change_status = 10;
						return 99;
					}
				}
				else
				{
					UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
					if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result2))
					{
						UDP_Analysis_Package result3;
						while (uDP_Analysis_Packages_send.TryPeek(out result3))
						{
							if (result3.upd_ID == result2.upd_ID)
							{
								uDP_Analysis_Package = result2;
								uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
								if (uDP_Analysis_Package.content[3] == change_type)
								{
									switch (change_type)
									{
									case 87:
										_share_data.olt_Write_accessable_level = 1;
										break;
									case 82:
										_share_data.olt_Read_accessable_level = 1;
										break;
									case 119:
										_share_data.olt_Write_accessable_level = 0;
										break;
									case 114:
										_share_data.olt_Read_accessable_level = 0;
										break;
									}
									PWD_change_status = 1;
									return 1;
								}
								PWD_change_status = 10;
								return 99;
							}
							if (uDP_Analysis_Packages_send.Count > 1)
							{
								uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
								continue;
							}
							break;
						}
					}
				}
			}
			return result;
		}

		public int PWD_Change(MAC mAC, bool flag, int write_read_flag)
		{
			int result = 0;
			int num = 0;
			byte b = 0;
			_share_data.sql_station = "PWD_Change";
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (_share_data.OLT_SN.ToString().Contains("need_check_SN"))
			{
				return -1;
			}
			if (totalSeconds - last_sending_package_time < task_timing.age_time)
			{
				return 1;
			}
			if (write_read_flag == 1)
			{
				b = change_W;
				num = send_package(mAC, b, flag);
				last_sending_package_time = totalSeconds;
				if (num != 0)
				{
					result = -1;
				}
				else
				{
					while (true)
					{
						switch (analysis_package(b))
						{
						case 0:
							continue;
						case 1:
							result = 0;
							break;
						default:
							result = -2;
							break;
						}
						break;
					}
				}
				return result;
			}
			if (totalSeconds - last_sending_package_time < task_timing.age_time)
			{
				return 1;
			}
			if (write_read_flag == 2)
			{
				b = change_R;
				num = send_package(mAC, b, flag);
				last_sending_package_time = totalSeconds;
				if (num != 0)
				{
					result = -1;
				}
				else
				{
					while (true)
					{
						switch (analysis_package(b))
						{
						case 0:
							continue;
						case 1:
							result = 0;
							break;
						default:
							result = -2;
							break;
						}
						break;
					}
				}
			}
			PWD_change_status = 0;
			PWD_change_enable = false;
			return result;
		}
	}
	internal class PWD_ACK
	{
		private Command_Code command;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		private byte return_value;

		public PWD_ACK(OLT_share_data data)
		{
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			command = Command_Code.Password_check_cmd;
			task_timing = new Package_Timing_Management(0.0, 0.0, 3.0, 10.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			return_value = 0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC)
		{
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[4];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[3] = return_value;
			uDP_Analysis_Package.task_owner = Task_Owner.Password_check_cmd;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
			}
			catch
			{
				num = -1;
			}
			if (num == 0)
			{
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
			}
			return num;
		}

		public int analysis_package(MAC mAC)
		{
			int num = 1;
			if (uDP_Analysis_Packages_receive.IsEmpty)
			{
				return -1;
			}
			while (!uDP_Analysis_Packages_receive.IsEmpty)
			{
				UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
				if (!uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result))
				{
					continue;
				}
				uDP_Analysis_Package = result;
				return_value = uDP_Analysis_Package.content[3];
				if (return_value == 82 || return_value == 87)
				{
					num = 0;
					if (return_value == 87)
					{
						_share_data.olt_Write_accessable_level = 1;
					}
					if (return_value == 82)
					{
						_share_data.olt_Read_accessable_level = 1;
					}
				}
				else if (return_value == 114 || return_value == 119)
				{
					num = 0;
					if (return_value == 119)
					{
						_share_data.olt_Write_accessable_level = 0;
					}
					if (return_value == 114)
					{
						_share_data.olt_Read_accessable_level = 0;
					}
				}
				else
				{
					num = -1;
				}
				if (num == 0)
				{
					send_package(mAC);
				}
			}
			return num;
		}

		public int PWD_Check(MAC mAC)
		{
			int result = 0;
			int num = 0;
			byte b = 0;
			_share_data.sql_station = "IPChange";
			if (analysis_package(mAC) == 0)
			{
			}
			return result;
		}
	}
	internal class IP_Add_Set
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public ConcurrentStack<OLT_IP_Change_Notify> oLT_IP_Change_Notify_list;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		public int IP_change_status;

		public byte[] new_IP;

		private bool Chang_IP_enable;

		public IP_Add_Set(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			oLT_IP_Change_Notify_list = new ConcurrentStack<OLT_IP_Change_Notify>();
			command = Command_Code.ip_configuration;
			task_timing = new Package_Timing_Management(0.0, 0.0, 3.0, 10.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			IP_change_status = 0;
			new_IP = new byte[4];
			Chang_IP_enable = false;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC)
		{
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[7];
			uDP_Analysis_Package.content[0] = (byte)command;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			Array.Copy(new_IP, 0, uDP_Analysis_Package.content, 3, 4);
			uDP_Analysis_Package.task_owner = Task_Owner.ip_configuration;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				IP_change_status = 0;
			}
			catch
			{
				IP_change_status = -1;
				num = -1;
			}
			if (num == 0)
			{
				task_timing.sending_time = uDP_Analysis_Package.aging_time;
			}
			return num;
		}

		public int analysis_package()
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (!Chang_IP_enable)
			{
				if (uDP_Analysis_Packages_receive.IsEmpty)
				{
					if (totalSeconds - task_timing.sending_time > task_timing.check_time)
					{
						IP_change_status = 10;
						return 99;
					}
				}
				else
				{
					UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
					if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result2))
					{
						UDP_Analysis_Package result3;
						while (uDP_Analysis_Packages_send.TryPeek(out result3))
						{
							if (result3.upd_ID == result2.upd_ID)
							{
								uDP_Analysis_Package = result2;
								uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
								byte[] array = new byte[4];
								Array.Copy(uDP_Analysis_Package.content, 3, array, 0, 4);
								if (array.SequenceEqual(new_IP))
								{
									IP_change_status = 1;
									return 1;
								}
								IP_change_status = 10;
								return 99;
							}
							if (uDP_Analysis_Packages_send.Count > 1)
							{
								uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
								continue;
							}
							break;
						}
					}
				}
			}
			return result;
		}

		public int ip_change(MAC mAC)
		{
			int result = 0;
			int num = 0;
			if (_share_data.olt_stick_Lifecycle != 1)
			{
				return -7;
			}
			_share_data.sql_station = "IPChange";
			if (!oLT_IP_Change_Notify_list.IsEmpty && oLT_IP_Change_Notify_list.TryPop(out OLT_IP_Change_Notify result2))
			{
				new_IP = result2.OLT_IP_NEW.GetAddressBytes();
				oLT_IP_Change_Notify_list.Clear();
				if (send_package(mAC) != 0)
				{
					result = -1;
				}
				else
				{
					while (true)
					{
						switch (analysis_package())
						{
						case 0:
							continue;
						case 1:
							result = 1;
							break;
						default:
							result = -2;
							break;
						}
						break;
					}
				}
			}
			IP_change_status = 0;
			Chang_IP_enable = false;
			return result;
		}
	}
	internal class In_band_FW_Upgrade
	{
		private Command_Code command;

		public ConcurrentQueue<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive_bin_sent;

		public ConcurrentStack<OLT_SDK_Upgrade_Notify> oLT_SDK_Upgrade_Notify;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public ushort Frame_Sent_ID_Code;

		public byte[] bin_file = null;

		public In_band_FW_Upgrade(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentQueue<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive_bin_sent = new ConcurrentStack<UDP_Analysis_Package>();
			oLT_SDK_Upgrade_Notify = new ConcurrentStack<OLT_SDK_Upgrade_Notify>();
			command = Command_Code.OLT_Update_BIN_cmd;
			task_timing = new Package_Timing_Management(0.0, 0.0, 0.01, 10.0);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			Frame_Sent_ID_Code = 0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int analysis_package_OLT_Image_Send_cmd()
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (totalSeconds - task_timing.sending_time > task_timing.age_time)
			{
				return 99;
			}
			if (!uDP_Analysis_Packages_receive_bin_sent.IsEmpty && uDP_Analysis_Packages_receive_bin_sent.TryPop(out UDP_Analysis_Package result2))
			{
				UDP_Analysis_Package result3;
				while (uDP_Analysis_Packages_send.TryPeek(out result3))
				{
					if (result3.upd_ID == result2.upd_ID)
					{
						if (result2.content[3] == 0)
						{
							uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
							result = 1;
						}
						else
						{
							result = -1;
						}
						break;
					}
					if (uDP_Analysis_Packages_send.Count > 1)
					{
						uDP_Analysis_Packages_send.TryDequeue(out UDP_Analysis_Package _);
						continue;
					}
					break;
				}
			}
			return result;
		}

		public int send_package_FW(MAC mAC, byte[] content, int offset_add)
		{
			int result = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code &= 16383;
			Frame_Sent_ID_Code++;
			uDP_Analysis_Package.content = new byte[9 + content.Length];
			uDP_Analysis_Package.content[0] = 68;
			uDP_Analysis_Package.content[1] = (byte)(Frame_Sent_ID_Code >> 8);
			uDP_Analysis_Package.content[2] = (byte)(Frame_Sent_ID_Code & 0xFFu);
			uDP_Analysis_Package.content[3] = (byte)(content.Length >> 8);
			uDP_Analysis_Package.content[4] = (byte)((uint)content.Length & 0xFFu);
			uDP_Analysis_Package.content[5] = (byte)((uint)(offset_add >> 24) & 0xFFu);
			uDP_Analysis_Package.content[6] = (byte)((uint)(offset_add >> 16) & 0xFFu);
			uDP_Analysis_Package.content[7] = (byte)((uint)(offset_add >> 8) & 0xFFu);
			uDP_Analysis_Package.content[8] = (byte)((uint)offset_add & 0xFFu);
			Array.Copy(content, 0, uDP_Analysis_Package.content, 9, content.Length);
			uDP_Analysis_Package.task_owner = Task_Owner.OLT_Update_BIN_cmd;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
				uDP_Analysis_Packages_send.Enqueue(uDP_Analysis_Package);
				task_timing.sending_time = totalSeconds;
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public int fw_download(MAC mAC)
		{
			int num = 1280;
			try
			{
				int num2 = bin_file.Length;
				int num3 = 1;
				num3 += (num2 - (num + 12)) / num;
				int num4 = 0;
				int num5 = 0;
				int num6 = 0;
				Frame_Sent_ID_Code = 1;
				for (num4 = 0; num4 < num3; num4++)
				{
					byte[] array = null;
					array = ((num4 != 0) ? new byte[num] : new byte[num + 12]);
					Array.Copy(bin_file, num6, array, 0, array.Length);
					num6 += array.Length;
					int num7 = send_package_FW(mAC, array, num5);
					num5 = ((num4 != 0) ? (num5 + num) : (num5 + num));
					if (num7 != 0)
					{
						return -1;
					}
					while (true)
					{
						num7 = analysis_package_OLT_Image_Send_cmd();
						Task.Delay(10);
						switch (num7)
						{
						case 0:
							continue;
						case 1:
							goto end_IL_00b6;
						}
						return -2;
						continue;
						end_IL_00b6:
						break;
					}
				}
				if ((num2 - (num + 12)) % num != 0 && num4 == num3)
				{
					int num8 = 0;
					int num9 = (num2 - (num + 12)) % num;
					byte[] array2 = new byte[num9];
					Array.Copy(bin_file, num6, array2, 0, num9);
					if (send_package_FW(mAC, array2, num5) != 0)
					{
						return -3;
					}
					while (true)
					{
						switch (analysis_package_OLT_Image_Send_cmd())
						{
						case 0:
							continue;
						case 1:
							goto end_IL_015d;
						}
						return -4;
						continue;
						end_IL_015d:
						break;
					}
				}
			}
			catch
			{
				return -5;
			}
			return 0;
		}

		public int FW_download_onestop_fun(MAC mAC)
		{
			int result = 0;
			int num = 0;
			if (_share_data.olt_stick_Lifecycle != 1)
			{
				return -7;
			}
			_share_data.sql_station = "downloadFW";
			if (oLT_SDK_Upgrade_Notify.TryPop(out OLT_SDK_Upgrade_Notify result2))
			{
				oLT_SDK_Upgrade_Notify.Clear();
				if (result2.Change_Notify)
				{
					bin_file = result2.bin_file;
					try
					{
						num = fw_download(mAC);
						if (num != 0)
						{
							return num;
						}
					}
					catch
					{
						result = -6;
					}
					return result;
				}
			}
			return result;
		}
	}
	internal class Shake_Hands
	{
		private Command_Code command;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_send;

		public ConcurrentStack<UDP_Analysis_Package> uDP_Analysis_Packages_receive;

		public Package_Timing_Management task_timing;

		private readonly OLT_share_data _share_data;

		public PhysicalAddress soure_MAC;

		public IPAddress soure_IP;

		public double last_reading_package_time;

		public ushort Frame_Sent_ID_Code;

		public double shake_hands_off_line_time;

		public double last_sending_time;

		public Shake_Hands(OLT_share_data data)
		{
			uDP_Analysis_Packages_send = new ConcurrentStack<UDP_Analysis_Package>();
			uDP_Analysis_Packages_receive = new ConcurrentStack<UDP_Analysis_Package>();
			command = Command_Code.shake_hand;
			int num = 5;
			task_timing = new Package_Timing_Management(0.0, 0.0, 1.0, num);
			_share_data = data;
			soure_MAC = PhysicalAddress.None;
			soure_IP = IPAddress.None;
			last_reading_package_time = 0.0;
			Frame_Sent_ID_Code = 0;
			shake_hands_off_line_time = num * 4;
			last_sending_time = 0.0;
		}

		public void UpdateData(IPAddress iP, PhysicalAddress physical, string oLT_SN)
		{
			_share_data.iPAddress = iP;
			_share_data.physicalAddress = physical;
			_share_data.OLT_SN = oLT_SN;
		}

		public int send_package(MAC mAC)
		{
			int result = 0;
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			Frame_Sent_ID_Code++;
			Frame_Sent_ID_Code &= 16383;
			uDP_Analysis_Package.content = new byte[3]
			{
				(byte)command,
				(byte)(Frame_Sent_ID_Code >> 8),
				(byte)(Frame_Sent_ID_Code & 0xFFu)
			};
			uDP_Analysis_Package.task_owner = Task_Owner.Shakehand_Task;
			uDP_Analysis_Package.time_window = 10.0;
			uDP_Analysis_Package.aging_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			uDP_Analysis_Package.SenderProtocolAddress = _share_data.iPAddress;
			uDP_Analysis_Package.SenderHardwareAddress = _share_data.physicalAddress;
			uDP_Analysis_Package.cmd_Code = command;
			uDP_Analysis_Package.upd_ID = Frame_Sent_ID_Code;
			uDP_Analysis_Packages_send.Push(uDP_Analysis_Package);
			try
			{
				mAC.SendFrame(uDP_Analysis_Package.content, _share_data.physicalAddress, _share_data.iPAddress);
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public int analysis_package()
		{
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (uDP_Analysis_Packages_receive.IsEmpty)
			{
				if (totalSeconds - last_sending_time > task_timing.check_time)
				{
					return -1;
				}
				return 0;
			}
			UDP_Analysis_Package uDP_Analysis_Package = new UDP_Analysis_Package();
			bool flag = false;
			UDP_Analysis_Package uDP_Analysis_Package2 = new UDP_Analysis_Package();
			if (uDP_Analysis_Packages_receive.TryPop(out UDP_Analysis_Package result))
			{
				if (!uDP_Analysis_Packages_send.TryPop(out UDP_Analysis_Package result2))
				{
					num = 3;
				}
				else if (result2.upd_ID >= result.upd_ID)
				{
					uDP_Analysis_Package = result;
					flag = true;
				}
			}
			if (!flag && num == 3)
			{
				return -2;
			}
			num = 1;
			byte[] array = new byte[16];
			Array.Copy(uDP_Analysis_Package.content, 25, array, 0, 16);
			bool flag2 = true;
			byte[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				if (array2[i] != 0)
				{
					flag2 = false;
					break;
				}
			}
			string text = "";
			if (flag2)
			{
				text = "need_check_SN";
			}
			else
			{
				try
				{
					text = Encoding.ASCII.GetString(array);
				}
				catch
				{
					text = "need_check_SN";
				}
			}
			_share_data.OLT_SN = text;
			return num;
		}

		public int shake_hands_fun(MAC mAC)
		{
			_share_data.sql_station = "shake_hands";
			int result = 0;
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			bool flag = false;
			if (totalSeconds - last_sending_time > task_timing.age_time)
			{
				flag = true;
			}
			if (flag)
			{
				if (send_package(mAC) != 0)
				{
					return -1;
				}
				last_sending_time = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
				while (true)
				{
					switch (analysis_package())
					{
					case 0:
						continue;
					case 1:
						_share_data.olt_stick_Lifecycle = 1;
						_share_data.Current_status = "ON_LINE";
						_share_data.status_int = 1;
						_share_data.OLT_offline_Counter = 0;
						break;
					default:
						_share_data.OLT_offline_Counter++;
						break;
					}
					break;
				}
				if (_share_data.OLT_offline_Counter > 5)
				{
					_share_data.Current_status = "OFF_LINE";
					_share_data.status_int = 0;
					_share_data.olt_stick_Lifecycle = 0;
					_share_data.OLT_offline_Counter = 10;
				}
				else if (_share_data.OLT_offline_Counter > 2)
				{
					_share_data.Current_status = "FLASH";
					_share_data.status_int = 0;
					_share_data.olt_stick_Lifecycle = 0;
				}
			}
			return result;
		}
	}
	internal class OLT_Stick_Management
	{
		public Package_Timing_Management arp_timing;

		public OLT_share_data oLT_share_data = new OLT_share_data();

		public readonly long OLT_Instance_ID;

		public Shake_Hands shake_Hands;

		public In_band_FW_Upgrade in_band_FW_Upgrade;

		public IP_Add_Set ip_Add_Set;

		public CPE_WhiteLst_Set cPE_WhiteLst_Set;

		public CPE_ServiceType_Set cPE_ServiceType_Set;

		public ONU_SN_STATUS_GET oNU_SN_STATUS_GET;

		public ONU_Alarm_GET oNU_Alarm_GET;

		public OLT_STATUS_GET oLT_STATUS_GET;

		public ONU_Whitelst_GET oNU_Whitelst_GET;

		public ONU_ServiceType_GET oNU_ServiceType_GET;

		public PWD_Set pWD_Set;

		public PWD_ACK pWD_ACK;

		public OLT_Stick_Management(OLT_share_data oLT_Data, long instance)
		{
			oLT_share_data = oLT_Data;
			OLT_Instance_ID = instance;
			arp_timing = new Package_Timing_Management(0.0, 0.0, 1.0, 10.0);
			shake_Hands = new Shake_Hands(oLT_share_data);
			in_band_FW_Upgrade = new In_band_FW_Upgrade(oLT_share_data);
			ip_Add_Set = new IP_Add_Set(oLT_share_data);
			cPE_WhiteLst_Set = new CPE_WhiteLst_Set(oLT_share_data);
			cPE_ServiceType_Set = new CPE_ServiceType_Set(oLT_share_data);
			oNU_SN_STATUS_GET = new ONU_SN_STATUS_GET(oLT_share_data);
			oNU_Alarm_GET = new ONU_Alarm_GET(oLT_share_data);
			oLT_STATUS_GET = new OLT_STATUS_GET(oLT_share_data);
			oNU_Whitelst_GET = new ONU_Whitelst_GET(oLT_share_data);
			oNU_ServiceType_GET = new ONU_ServiceType_GET(oLT_share_data);
			pWD_Set = new PWD_Set(oLT_share_data);
			pWD_ACK = new PWD_ACK(oLT_share_data);
		}

		public int send_arp(MAC mAC)
		{
			int num = 0;
			double totalSeconds = (DateTime.Now - new DateTime(1970, 1, 1)).TotalSeconds;
			if (totalSeconds - arp_timing.sending_time > arp_timing.age_time)
			{
				num = mAC.BuildArpRequest(oLT_share_data.iPAddress);
				arp_timing.sending_time = totalSeconds;
			}
			else
			{
				num = 99;
			}
			return num;
		}
	}
	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			ApplicationConfiguration.Initialize();
			Application.Run(new Form1());
		}
	}
	[CompilerGenerated]
	internal static class ApplicationConfiguration
	{
		public static void Initialize()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(defaultValue: false);
			Application.SetHighDpiMode(HighDpiMode.SystemAware);
		}
	}
}
namespace APP_OLT_Stick_V2
{
	public class CPE_Management : Form
	{
		private string dbPath = "example.db";

		private readonly ONU_AppData _appData;

		private readonly BindingSource _bindingSource = new BindingSource();

		private DataGridView _dataGridView;

		private Label _lblTotalONUs;

		private Label _lblonlineONUs;

		private TextBox _txtSelectedSN;

		private TextBox _txtRxPower;

		private TextBox _txtTemperature;

		private TextBox _txtVoltage;

		private Label _lblAlarmStatus;

		private Form1 _form1;

		private BindingSource _selectedUserBindingSource = new BindingSource();

		private IContainer components = null;

		public CPE_Management(Form1 form1, string path, ONU_AppData appData)
		{
			_form1 = form1;
			dbPath = path;
			_appData = appData;
			InitializeComponent();
			InitializeCustomComponents();
			SetupUI();
			SetupDataBindings();
			SubscribeToEvents();
			base.Icon = new Icon("FSLogo.ico");
		}

		private void InitializeCustomComponents()
		{
			SuspendLayout();
			_dataGridView = new DataGridView
			{
				Dock = DockStyle.Top,
				Height = 400,
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				ReadOnly = true,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				Name = "dataGridViewONUs",
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
			};
			base.Controls.Add(_dataGridView);
			_lblTotalONUs = new Label
			{
				Dock = DockStyle.Bottom,
				Height = 25,
				TextAlign = ContentAlignment.MiddleLeft,
				Name = "lblTotalONUs"
			};
			base.Controls.Add(_lblTotalONUs);
			_lblonlineONUs = new Label
			{
				Dock = DockStyle.Bottom,
				Height = 25,
				TextAlign = ContentAlignment.MiddleLeft,
				Name = "lblOnlineONUs"
			};
			base.Controls.Add(_lblonlineONUs);
			Panel panel = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(10),
				Name = "detailPanel"
			};
			base.Controls.Add(panel);
			int num = 10;
			panel.Controls.Add(new Label
			{
				Text = "ONU_SN:",
				Top = num,
				Left = 10,
				Name = "lblSN",
				AutoSize = true
			});
			_txtSelectedSN = new TextBox
			{
				Top = num,
				Left = 120,
				Width = 200,
				ReadOnly = true,
				Name = "txtSelectedSN"
			};
			panel.Controls.Add(_txtSelectedSN);
			num += 30;
			panel.Controls.Add(new Label
			{
				Text = "ONU_RX(dBm):",
				Top = num,
				Left = 10,
				Name = "lblRxPower",
				AutoSize = true
			});
			_txtRxPower = new TextBox
			{
				Top = num,
				Left = 120,
				Width = 100,
				ReadOnly = true,
				Name = "txtRxPower"
			};
			panel.Controls.Add(_txtRxPower);
			num += 30;
			panel.Controls.Add(new Label
			{
				Text = "Vol(V):",
				Top = num,
				Left = 10,
				Name = "lblVoltage",
				AutoSize = true
			});
			_txtVoltage = new TextBox
			{
				Top = num,
				Left = 120,
				Width = 100,
				ReadOnly = true,
				Name = "txtVoltage"
			};
			panel.Controls.Add(_txtTemperature);
			num += 30;
			panel.Controls.Add(new Label
			{
				Text = "Temperature(℃):",
				Top = num,
				Left = 10,
				Name = "lblTemperature",
				AutoSize = true
			});
			_txtTemperature = new TextBox
			{
				Top = num,
				Left = 120,
				Width = 100,
				ReadOnly = true,
				Name = "txtTemperature"
			};
			panel.Controls.Add(_txtTemperature);
			num += 30;
			panel.Controls.Add(new Label
			{
				Text = "Alarm:",
				Top = num,
				Left = 10,
				Name = "lblAlarm",
				AutoSize = true
			});
			_lblAlarmStatus = new Label
			{
				Top = num,
				Left = 120,
				Width = 200,
				ForeColor = Color.Red,
				Name = "lblAlarmStatus"
			};
			panel.Controls.Add(_lblAlarmStatus);
			ResumeLayout(performLayout: true);
		}

		private void SetupUI()
		{
			_dataGridView.AutoGenerateColumns = false;
			_dataGridView.AllowUserToOrderColumns = true;
			_dataGridView.Columns.Clear();
			_dataGridView.BackgroundColor = ColorTranslator.FromHtml("#F6F6F6");
			AddColumn("olt_ip", "OLT IP", 120);
			AddColumn("onu_sn", "ONU_SN", 150);
			AddColumn("onu_status", "Status", 80);
			AddColumn("onu_tx_pwr", "TX Power (dBm)", 100);
			AddColumn("onu_rx_pwr", "RX Power (dBm)", 100);
			AddColumn("onu_bias", "Bias (mA)", 80);
			AddColumn("onu_temperature", "Temperature (℃)", 100);
			AddColumn("onu_voltage", "Voltage (V)", 100);
			AddColumn("onu_rssi", "RSSI", 80);
			AddColumn("alarm_status", "Alarm Status", 120);
			DataGridViewColumn AddColumn(string dataProperty, string header, int width)
			{
				DataGridViewTextBoxColumn dataGridViewTextBoxColumn = new DataGridViewTextBoxColumn
				{
					DataPropertyName = dataProperty,
					HeaderText = header,
					Width = width,
					SortMode = DataGridViewColumnSortMode.Automatic
				};
				_dataGridView.Columns.Add(dataGridViewTextBoxColumn);
				return dataGridViewTextBoxColumn;
			}
		}

		private void SetupDataBindings()
		{
			try
			{
				_bindingSource.DataSource = _appData.Users;
				_dataGridView.DataSource = _bindingSource;
				_dataGridView.SelectionChanged += DataGridView_SelectionChanged;
				_appData.PropertyChanged += AppData_PropertyChanged;
				_selectedUserBindingSource.DataSource = _appData;
				_selectedUserBindingSource.DataMember = "SelectedUser";
				AddSafeBinding(_txtSelectedSN, "Text", _selectedUserBindingSource, "onu_sn", "N/A");
				AddSafeBinding(_txtRxPower, "Text", _selectedUserBindingSource, "onu_rx_pwr", "-");
				AddSafeBinding(_txtTemperature, "Text", _selectedUserBindingSource, "onu_temperature", "-");
				AddSafeBinding(_lblAlarmStatus, "Text", _selectedUserBindingSource, "alarm_status", "Normal");
				_lblTotalONUs.DataBindings.Add("Text", _appData, "TotalUsers", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, "0", "Total ONUs: {0}");
				_lblonlineONUs.DataBindings.Add("Text", _appData, "OnlineUsers", formattingEnabled: true, DataSourceUpdateMode.OnPropertyChanged, "0", "Online ONUs: {0}");
			}
			catch (Exception ex)
			{
				MessageBox.Show("Data binding failed: " + ex.Message + "\n\n" + ex.StackTrace, "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		private void AddSafeBinding(Control control, string propertyName, object dataSource, string dataMember, object nullValue)
		{
			object nullValue2 = nullValue;
			if (control == null || dataSource == null)
			{
				MessageBox.Show($"Binding failed: Control or data source is null\nControl: {control?.Name}\nData source: {dataSource}", "Binding Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			try
			{
				Binding binding = new Binding(propertyName, dataSource, dataMember, formattingEnabled: true);
				binding.Format += delegate(object? sender, ConvertEventArgs e)
				{
					if (e.Value == null)
					{
						e.Value = nullValue2;
					}
				};
				binding.NullValue = nullValue2;
				control.DataBindings.Add(binding);
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to create binding: {ex.Message}\nProperty: {propertyName}\nData member: {dataMember}", "Binding Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
			}
		}

		private void DataGridView_SelectionChanged(object sender, EventArgs e)
		{
			if (_dataGridView.CurrentRow != null && _dataGridView.CurrentRow.DataBoundItem is ONU_Performance_Notity selectedUser)
			{
				_appData.SelectedUser = selectedUser;
			}
			else
			{
				_appData.SelectedUser = null;
			}
		}

		private void AppData_PropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			try
			{
				if (e.PropertyName == "SelectedUser")
				{
					UpdateDataGridViewSelection();
				}
			}
			catch
			{
			}
		}

		private void UpdateDataGridViewSelection()
		{
			if (_dataGridView.InvokeRequired)
			{
				_dataGridView.Invoke(UpdateDataGridViewSelection);
				return;
			}
			try
			{
				if (_appData.SelectedUser != null)
				{
					int num = _appData.Users.IndexOf(_appData.SelectedUser);
					if (num >= 0 && num < _dataGridView.Rows.Count)
					{
						_dataGridView.ClearSelection();
						_dataGridView.Rows[num].Selected = true;
						_dataGridView.CurrentCell = _dataGridView.Rows[num].Cells[0];
					}
				}
				else
				{
					_dataGridView.ClearSelection();
				}
			}
			catch (Exception ex)
			{
				Debug.WriteLine("Selection update failed: " + ex.Message);
			}
		}

		private void SubscribeToEvents()
		{
			_appData.ONUPropertyChanged += OnONUPropertyChanged;
		}

		private void OnONUPropertyChanged(object sender, ONUPropertyChangedEventArgs e)
		{
			object sender2 = sender;
			ONUPropertyChangedEventArgs e2 = e;
			if (base.InvokeRequired)
			{
				Invoke(delegate
				{
					OnONUPropertyChanged(sender2, e2);
				});
				return;
			}
			if (e2.PropertyName == "onu_status")
			{
				UpdateStatusCell(e2.ONU);
			}
			if (e2.PropertyName == "onu_temperature" || e2.PropertyName == "alarm_status")
			{
				UpdateAlarmIndicator(e2.ONU);
			}
			if (e2.ONU == _appData.SelectedUser)
			{
				UpdateDetailPanel(e2.PropertyName);
			}
		}

		private void UpdateStatusCell(ONU_Performance_Notity onu)
		{
			try
			{
				int num = _appData.Users.IndexOf(onu);
				if (num >= 0 && onu.onu_status == "OFF_LINE")
				{
					_dataGridView.InvalidateRow(num);
					_dataGridView.Rows[num].DefaultCellStyle.BackColor = Color.Gray;
				}
			}
			catch
			{
			}
		}

		private void UpdateAlarmIndicator(ONU_Performance_Notity onu = null)
		{
			try
			{
				onu = onu ?? _appData.SelectedUser;
				if (onu != null)
				{
					if (onu.alarm_status == "警告" || (double.TryParse(onu.onu_temperature, out var result) && result > 70.0))
					{
						_lblAlarmStatus.ForeColor = Color.Red;
						_lblAlarmStatus.Text = "ALARM: Warning!";
					}
					else
					{
						_lblAlarmStatus.ForeColor = Color.Green;
						_lblAlarmStatus.Text = "Status: Normal";
					}
				}
			}
			catch
			{
			}
		}

		private void UpdateDetailPanel(string propertyName)
		{
			try
			{
				if (!(propertyName == "onu_rx_pwr"))
				{
					if (propertyName == "onu_temperature")
					{
						_txtTemperature.Text = _appData.SelectedUser?.onu_temperature ?? "";
					}
				}
				else
				{
					_txtRxPower.Text = _appData.SelectedUser?.onu_rx_pwr ?? "";
				}
			}
			catch
			{
			}
		}

		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			try
			{
				_appData.ONUPropertyChanged -= OnONUPropertyChanged;
				_appData.PropertyChanged -= AppData_PropertyChanged;
				if (_dataGridView != null)
				{
					_dataGridView.SelectionChanged -= DataGridView_SelectionChanged;
				}
				_bindingSource.Dispose();
				_selectedUserBindingSource.Dispose();
			}
			catch
			{
			}
			base.OnFormClosed(e);
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			base.SuspendLayout();
			base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(1098, 450);
			base.Name = "CPE_Management";
			this.Text = "ONU_Performance";
			base.ResumeLayout(false);
		}
	}
	public class DatabaseHelper
	{
		private string _dbPath;

		private string _connectionString;

		public double _savetime_gap;

		public DatabaseHelper(string dbPath, string connectionString, double savetime_gap)
		{
			_dbPath = dbPath;
			_connectionString = connectionString;
			_savetime_gap = savetime_gap * 60.0;
		}

		public int connect_SQL()
		{
			int result = 0;
			try
			{
				using SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString);
				sQLiteConnection.Open();
			}
			catch
			{
				result = -1;
			}
			return result;
		}

		public void SaveOnuList(List<ONU_Performance_Notity> onuList)
		{
			using SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString);
			sQLiteConnection.Open();
			using SQLiteTransaction sQLiteTransaction = sQLiteConnection.BeginTransaction();
			try
			{
				string sql = "\r\n                        INSERT OR REPLACE INTO ONU_Performance \r\n                        (OltIp, OnuSn, OnuStatus, OnuTxPwr, OnuRxPwr, OnuBias, OnuTemperature, OnuRssi, AlarmStatus, OnuVoltage, TimeSecond, TimeString)\r\n                        VALUES \r\n                        (@OltIp, @OnuSn, @OnuStatus, @OnuTxPwr, @OnuRxPwr, @OnuBias, @OnuTemperature, @OnuRssi, @AlarmStatus, @OnuVoltage, @TimeSecond, @TimeString)";
				foreach (ONU_Performance_Notity onu in onuList)
				{
					sQLiteConnection.Execute(sql, new
					{
						OltIp = onu.olt_ip.ToString(),
						OnuSn = onu.onu_sn,
						OnuStatus = onu.onu_status,
						OnuTxPwr = onu.onu_tx_pwr,
						OnuRxPwr = onu.onu_rx_pwr,
						OnuBias = onu.onu_bias,
						OnuTemperature = onu.onu_temperature,
						OnuRssi = onu.onu_rssi,
						AlarmStatus = onu.alarm_status,
						OnuVoltage = onu.onu_voltage,
						TimeSecond = onu.time_second,
						TimeString = onu.time_string
					}, sQLiteTransaction);
				}
				sQLiteTransaction.Commit();
			}
			catch
			{
				sQLiteTransaction.Rollback();
			}
		}

		public void SaveOnuList(IEnumerable<ONU_Performance_Notity> onuList)
		{
			using SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString);
			sQLiteConnection.Open();
			using SQLiteTransaction sQLiteTransaction = sQLiteConnection.BeginTransaction();
			try
			{
				string sql = "\r\n                        INSERT OR REPLACE INTO ONU_Performance \r\n                        (OltIp, OnuSn, OnuStatus, OnuTxPwr, OnuRxPwr, OnuBias, OnuTemperature, OnuRssi, AlarmStatus, OnuVoltage, TimeSecond, TimeString)\r\n                        VALUES \r\n                        (@OltIp, @OnuSn, @OnuStatus, @OnuTxPwr, @OnuRxPwr, @OnuBias, @OnuTemperature, @OnuRssi, @AlarmStatus, @OnuVoltage, @TimeSecond, @TimeString)";
				foreach (ONU_Performance_Notity onu in onuList)
				{
					sQLiteConnection.Execute(sql, new
					{
						OltIp = onu.olt_ip.ToString(),
						OnuSn = onu.onu_sn,
						OnuStatus = onu.onu_status,
						OnuTxPwr = onu.onu_tx_pwr,
						OnuRxPwr = onu.onu_rx_pwr,
						OnuBias = onu.onu_bias,
						OnuTemperature = onu.onu_temperature,
						OnuRssi = onu.onu_rssi,
						AlarmStatus = onu.alarm_status,
						OnuVoltage = onu.onu_voltage,
						TimeSecond = onu.time_second,
						TimeString = onu.time_string
					}, sQLiteTransaction);
				}
				sQLiteTransaction.Commit();
			}
			catch
			{
				sQLiteTransaction.Rollback();
			}
		}

		public void SaveOLTList(IEnumerable<OLT_Performance_Notity> oltList)
		{
			using SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString);
			sQLiteConnection.Open();
			using SQLiteTransaction sQLiteTransaction = sQLiteConnection.BeginTransaction();
			try
			{
				string sql = "\r\n                        INSERT OR REPLACE INTO OLT_Performance \r\n                        (olt_ip, olt_sn, olt_mac, olt_status, olt_tx_pwr, olt_bias, olt_temperature, olt_rssi, alarm_status, olt_voltage, time_second, time_string)\r\n                        VALUES \r\n                        (@olt_ip, @olt_sn, @olt_mac, @olt_status, @olt_tx_pwr, @olt_bias, @olt_temperature, @olt_rssi, @alarm_status, @olt_voltage, @time_second, @time_string)";
				foreach (OLT_Performance_Notity olt in oltList)
				{
					sQLiteConnection.Execute(sql, new
					{
						olt_ip = olt.olt_ip.ToString(),
						olt_sn = olt.olt_sn,
						olt_mac = olt.olt_mac.ToString(),
						olt_status = olt.olt_status,
						olt_tx_pwr = olt.olt_tx_pwr,
						olt_bias = olt.olt_bias,
						olt_temperature = olt.olt_temperature,
						olt_rssi = olt.olt_rssi,
						alarm_status = olt.alarm_status,
						olt_voltage = olt.olt_voltage,
						time_second = olt.time_second,
						time_string = olt.time_string
					}, sQLiteTransaction);
				}
				sQLiteTransaction.Commit();
			}
			catch
			{
				sQLiteTransaction.Rollback();
			}
		}

		public int sql_search(string query, out DataTable processTable)
		{
			int num = 0;
			processTable = new DataTable();
			try
			{
				using SQLiteConnection sQLiteConnection = new SQLiteConnection(_connectionString);
				sQLiteConnection.Open();
				if (sQLiteConnection.State != ConnectionState.Open)
				{
					MessageBox.Show("connect databased failed");
					return -1;
				}
				SQLiteCommand sQLiteCommand = new SQLiteCommand(query, sQLiteConnection);
				SQLiteDataAdapter sQLiteDataAdapter = new SQLiteDataAdapter(query, sQLiteConnection);
				sQLiteDataAdapter.Fill(processTable);
				return 0;
			}
			catch
			{
				num = -2;
			}
			return num;
		}
	}
	public class HexStringParser
	{
		public static string ByteArrayToString(byte[] bytes)
		{
			if (bytes == null)
			{
				throw new ArgumentNullException("bytes", "Byte array cannot be null");
			}
			if (bytes.Length != 8)
			{
				throw new ArgumentException("Byte array must be 8 bytes long", "bytes");
			}
			char[] array = new char[4];
			for (int i = 0; i < 4; i++)
			{
				byte b2 = bytes[i];
				if (b2 > 127)
				{
					throw new FormatException($"Byte at index {i} is not a valid ASCII character: {b2}");
				}
				array[i] = (char)b2;
			}
			string text = string.Concat(from b in bytes.Skip(4)
				select b.ToString("X2"));
			return new string(array) + text;
		}

		public static byte[] ParseStringToByteArray(string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				throw new ArgumentException("string cannot be null");
			}
			if (input.Length != 12)
			{
				throw new ArgumentException($"must be 12bytes: {input.Length}");
			}
			byte[] result = new byte[8];
			ParseAsciiPart(input, result);
			ParseHexPart(input, result);
			return result;
		}

		private static void ParseAsciiPart(string input, byte[] result)
		{
			for (int i = 0; i < 4; i++)
			{
				char c = input[i];
				if (c > '\u007f')
				{
					throw new FormatException($" {i}th character '{c}' is not ascii");
				}
				result[i] = (byte)c;
			}
		}

		private static void ParseHexPart(string input, byte[] result)
		{
			string text = input.Substring(4, 8);
			if (!IsValidHexString(text))
			{
				throw new FormatException("rest 8bytes '" + text + "' is not valid");
			}
			for (int i = 0; i < 4; i++)
			{
				string text2 = text.Substring(i * 2, 2);
				try
				{
					result[4 + i] = Convert.ToByte(text2, 16);
				}
				catch (FormatException innerException)
				{
					throw new FormatException("invalid hex data: '" + text2 + "'", innerException);
				}
			}
		}

		private static bool IsValidHexString(string value)
		{
			return value.All((char c) => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));
		}
	}
	public struct CPE_Info_struct
	{
		public byte[] CPE_SN;

		public string SNStr;

		public byte service_type;

		public bool active;

		public string description;

		public CPE_Info_struct(byte[] cpesn, string snstr, byte sertype, bool act, string des)
		{
			CPE_SN = cpesn;
			SNStr = snstr;
			service_type = sertype;
			active = act;
			description = des;
		}
	}
	public class ONU_Whitelst_Info
	{
		public IPAddress olt_ip;

		public List<CPE_Info_struct> cPE_Struct;

		public ONU_Whitelst_Info()
		{
			olt_ip = IPAddress.Parse("192.168.31.1");
			cPE_Struct = new List<CPE_Info_struct>();
		}
	}
	internal class CPE_White_list_OP
	{
		private string filePath;

		private int file_error = 0;

		public ONU_Whitelst_Info oNU_Whitelst_Info;

		private string dbPath = "example.db";

		private string connectionString = "";

		public CPE_White_list_OP(string dbpath)
		{
			dbPath = dbpath;
			oNU_Whitelst_Info = new ONU_Whitelst_Info();
			connectionString = "Data Source=" + dbPath + ";Version=3;";
		}

		public ONU_Whitelst_Info file_parse()
		{
			try
			{
				using StreamReader streamReader = new StreamReader(filePath);
				char[] separator = new char[2] { ' ', '\t' };
				int num = 0;
				int num2 = 0;
				string text;
				while ((text = streamReader.ReadLine()) != null)
				{
					string text2 = text.Trim();
					if (!(text2 != "") || text2[0] == '#')
					{
						continue;
					}
					string[] array = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
					string text3 = "";
					if (array[0].Contains("IP"))
					{
						try
						{
							oNU_Whitelst_Info.olt_ip = IPAddress.Parse(array[1]);
						}
						catch
						{
							num = -3;
						}
						continue;
					}
					if (array.Length >= 3)
					{
						try
						{
							array[1] = array[1].Trim();
							int num3 = int.Parse(array[1]);
							if (num3 < 0 || num3 > 255)
							{
								num = -1;
							}
						}
						catch
						{
							num = -1;
						}
						try
						{
							array[2] = array[2].Trim();
							string obj3 = array[2];
							if (obj3 != null && obj3.Equals("yes", StringComparison.OrdinalIgnoreCase))
							{
								array[2] = "yes";
							}
							else
							{
								string obj4 = array[2];
								if (obj4 != null && obj4.Equals("no", StringComparison.OrdinalIgnoreCase))
								{
									array[2] = "no";
								}
								else
								{
									num2 = -1;
								}
							}
						}
						catch
						{
							num2 = -1;
						}
						switch (num)
						{
						case -1:
							MessageBox.Show("Warning service type is not right: " + text + "line");
							file_error = -1;
							break;
						case -2:
							MessageBox.Show("Warning IP address is not right: " + text + "line");
							file_error = -1;
							break;
						default:
						{
							if (num2 == -1)
							{
								MessageBox.Show("Warning flag is not right,should be yes or  no: " + text + "line");
								file_error = -1;
								break;
							}
							if (num != 0 || num2 != 0)
							{
								continue;
							}
							if (array.Length >= 4)
							{
								int num4 = array.Length;
								for (int i = 3; i < num4; i++)
								{
									text3 = text3 + array[i] + " ";
								}
							}
							byte[] cpesn = HexStringParser.ParseStringToByteArray(array[0]);
							string snstr = array[0];
							byte sertype = byte.Parse(array[1]);
							bool act = false;
							if (array[2] == "yes" || array[2] == "YES")
							{
								act = true;
							}
							CPE_Info_struct item = new CPE_Info_struct(cpesn, snstr, sertype, act, text3);
							oNU_Whitelst_Info.cPE_Struct.Add(item);
							continue;
						}
						}
						break;
					}
					MessageBox.Show("Warning: 数据记录不完整: " + text + "行");
					file_error = -1;
					return oNU_Whitelst_Info;
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error reading file: " + ex.Message);
				file_error = -1;
			}
			return oNU_Whitelst_Info;
		}

		public int File_read()
		{
			int result = 0;
			try
			{
				using OpenFileDialog openFileDialog = new OpenFileDialog();
				openFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
				if (openFileDialog.ShowDialog() != DialogResult.OK)
				{
					return -2;
				}
				filePath = openFileDialog.FileName;
				oNU_Whitelst_Info = file_parse();
				if (file_error != 0)
				{
					MessageBox.Show("File format is not right");
					return -1;
				}
				using SQLiteConnection sQLiteConnection = new SQLiteConnection(connectionString);
				int num = 0;
				sQLiteConnection.Open();
				for (int i = 0; i < oNU_Whitelst_Info.cPE_Struct.Count; i++)
				{
					string value = "no";
					if (oNU_Whitelst_Info.cPE_Struct[i].active)
					{
						value = "yes";
					}
					string commandText = $"insert into CPE_WHITE_LIST(CPE_SN,Service_Type,OLT_IP,ACTIVE,Comment)values ('{oNU_Whitelst_Info.cPE_Struct[i].SNStr}','{oNU_Whitelst_Info.cPE_Struct[i].service_type}','{oNU_Whitelst_Info.olt_ip}', '{value}','{oNU_Whitelst_Info.cPE_Struct[i].description}')";
					using SQLiteCommand sQLiteCommand = new SQLiteCommand(commandText, sQLiteConnection);
					if (sQLiteCommand.ExecuteNonQuery() == 0)
					{
						num++;
						MessageBox.Show($"{oNU_Whitelst_Info.cPE_Struct[i].CPE_SN}added failed！");
					}
				}
			}
			catch
			{
				MessageBox.Show("adding ONU white list: failed");
				result = -1;
			}
			return result;
		}

		private static bool IsValidIpAddress(string ipAddress)
		{
			string pattern = "^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
			return Regex.IsMatch(ipAddress, pattern);
		}
	}
	public class ONU_Service_flow
	{
		public byte onuid;

		public byte wan_id;

		public ushort tcont_id;

		public ushort vlan_id;

		public ushort max;

		public ushort fix;

		public ushort ass;

		public ushort gem_port;

		public byte type;

		public byte priority;

		public byte weight;

		public byte valid;

		public ushort Reserved;
	}
	public class ONU_Service_Type_Info
	{
		public IPAddress olt_ip;

		public byte ONU_Service_Type_No;

		public string ONU_Service_Type_Name;

		public List<ONU_Service_flow> oNU_Service_Flows;

		public ONU_Service_Type_Info()
		{
			olt_ip = IPAddress.None;
			ONU_Service_Type_No = 0;
			ONU_Service_Type_Name = "";
			oNU_Service_Flows = new List<ONU_Service_flow>();
		}
	}
	internal class ONU_Service_Type_Parse
	{
		public ONU_Service_Type_Info oNU_Service_Type_Info;

		private string filePath;

		private string dbPath = "example.db";

		private string connectionString = "";

		private int reading_error = 0;

		private string content;

		public ONU_Service_Type_Parse(string dbpath, IPAddress iPAddress)
		{
			dbPath = dbpath;
			oNU_Service_Type_Info = new ONU_Service_Type_Info();
			connectionString = "Data Source=" + dbPath + ";Version=3;";
			oNU_Service_Type_Info.olt_ip = iPAddress;
			content = "";
		}

		public void file_parse()
		{
			try
			{
				using StreamReader streamReader = new StreamReader(filePath);
				char[] separator = new char[2] { ' ', '\t' };
				string text;
				while ((text = streamReader.ReadLine()) != null)
				{
					string pattern = "^(?=.*<.*?>)(?=.*flow)(?=.*start).+$";
					string text2 = text.Trim();
					if (!(text2 != "") || text2[0] == '#')
					{
						continue;
					}
					if (Regex.IsMatch(text2, pattern, RegexOptions.IgnoreCase))
					{
						ONU_Service_flow oNU_Service_flow = new ONU_Service_flow();
						while ((text = streamReader.ReadLine()) != null && reading_error == 0)
						{
							if (!(text != "") || text[0] == '#')
							{
								continue;
							}
							if (text.Contains("<end>"))
							{
								oNU_Service_Type_Info.oNU_Service_Flows.Add(oNU_Service_flow);
								break;
							}
							string[] array = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
							if (array[0].Contains("wan_id"))
							{
								oNU_Service_flow.wan_id = byte.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("tcont_id"))
							{
								oNU_Service_flow.tcont_id = ushort.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("vlan_id"))
							{
								oNU_Service_flow.vlan_id = ushort.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("max_BD"))
							{
								oNU_Service_flow.max = ushort.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("fix_BD"))
							{
								oNU_Service_flow.fix = ushort.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("ass_BD"))
							{
								oNU_Service_flow.ass = ushort.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("gem_port"))
							{
								oNU_Service_flow.gem_port = ushort.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("type"))
							{
								oNU_Service_flow.type = byte.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("priority"))
							{
								oNU_Service_flow.priority = byte.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("weight"))
							{
								oNU_Service_flow.weight = byte.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("valid"))
							{
								oNU_Service_flow.valid = byte.Parse(array[1]);
								continue;
							}
							if (array[0].Contains("reserved"))
							{
								oNU_Service_flow.Reserved = ushort.Parse(array[1]);
								continue;
							}
							reading_error = -1;
							break;
						}
						if (reading_error != 0)
						{
							break;
						}
					}
					else
					{
						string[] array2 = text.Split(separator, StringSplitOptions.RemoveEmptyEntries);
						if (array2[0].Contains("ONU_service_number"))
						{
							oNU_Service_Type_Info.ONU_Service_Type_No = byte.Parse(array2[1]);
						}
						else if (array2[0].Contains("ONU_service_name"))
						{
							oNU_Service_Type_Info.ONU_Service_Type_Name = array2[1];
						}
					}
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Error reading file: " + ex.Message);
				reading_error = -1;
				return;
			}
			using StreamReader streamReader2 = new StreamReader(filePath);
			content = streamReader2.ReadToEnd();
		}

		public int File_read()
		{
			int result = 0;
			try
			{
				using OpenFileDialog openFileDialog = new OpenFileDialog();
				openFileDialog.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
				if (openFileDialog.ShowDialog() != DialogResult.OK)
				{
					return -2;
				}
				filePath = openFileDialog.FileName;
				file_parse();
				if (reading_error != 0)
				{
					MessageBox.Show("File format is not right");
					return -1;
				}
				using SQLiteConnection sQLiteConnection = new SQLiteConnection(connectionString);
				int num = 0;
				sQLiteConnection.Open();
				string commandText = $"insert into ONU_SERVICE_TYPE(Name,Service_Type_Number,Content,olt_ip)values ('{oNU_Service_Type_Info.ONU_Service_Type_Name}','{oNU_Service_Type_Info.ONU_Service_Type_No}','{content}', '{oNU_Service_Type_Info.olt_ip}')";
				using SQLiteCommand sQLiteCommand = new SQLiteCommand(commandText, sQLiteConnection);
				if (sQLiteCommand.ExecuteNonQuery() == 0)
				{
					num++;
					MessageBox.Show(oNU_Service_Type_Info.ONU_Service_Type_Name + " added failed！");
				}
			}
			catch
			{
				MessageBox.Show("adding ONU service type list: failed");
				result = -1;
			}
			return result;
		}
	}
	public class MD5Hash
	{
		private uint[] state = new uint[4];

		private uint[] count = new uint[2];

		private byte[] buffer = new byte[64];

		public MD5Hash()
		{
			Initialize();
		}

		private void Initialize()
		{
			count[0] = 0u;
			count[1] = 0u;
			state[0] = 1732584193u;
			state[1] = 4023233417u;
			state[2] = 2562383102u;
			state[3] = 271733878u;
		}

		private uint F(uint x, uint y, uint z)
		{
			return (x & y) | (~x & z);
		}

		private uint G(uint x, uint y, uint z)
		{
			return (x & z) | (y & ~z);
		}

		private uint H(uint x, uint y, uint z)
		{
			return x ^ y ^ z;
		}

		private uint I(uint x, uint y, uint z)
		{
			return y ^ (x | ~z);
		}

		private uint RotateLeft(uint x, int n)
		{
			return (x << n) | (x >> 32 - n);
		}

		private void FF(ref uint a, uint b, uint c, uint d, uint x, int s, uint ac)
		{
			a += F(b, c, d) + x + ac;
			a = RotateLeft(a, s);
			a += b;
		}

		private void GG(ref uint a, uint b, uint c, uint d, uint x, int s, uint ac)
		{
			a += G(b, c, d) + x + ac;
			a = RotateLeft(a, s);
			a += b;
		}

		private void HH(ref uint a, uint b, uint c, uint d, uint x, int s, uint ac)
		{
			a += H(b, c, d) + x + ac;
			a = RotateLeft(a, s);
			a += b;
		}

		private void II(ref uint a, uint b, uint c, uint d, uint x, int s, uint ac)
		{
			a += I(b, c, d) + x + ac;
			a = RotateLeft(a, s);
			a += b;
		}

		private void Transform(byte[] block)
		{
			uint a = state[0];
			uint a2 = state[1];
			uint a3 = state[2];
			uint a4 = state[3];
			uint[] array = new uint[16];
			int num = 0;
			for (int i = 0; i < 64; i += 4)
			{
				array[num] = (uint)(block[i] | (block[i + 1] << 8) | (block[i + 2] << 16) | (block[i + 3] << 24));
				num++;
			}
			FF(ref a, a2, a3, a4, array[0], 7, 3614090360u);
			FF(ref a4, a, a2, a3, array[1], 12, 3905402710u);
			FF(ref a3, a4, a, a2, array[2], 17, 606105819u);
			FF(ref a2, a3, a4, a, array[3], 22, 3250441966u);
			FF(ref a, a2, a3, a4, array[4], 7, 4118548399u);
			FF(ref a4, a, a2, a3, array[5], 12, 1200080426u);
			FF(ref a3, a4, a, a2, array[6], 17, 2821735955u);
			FF(ref a2, a3, a4, a, array[7], 22, 4249261313u);
			FF(ref a, a2, a3, a4, array[8], 7, 1770035416u);
			FF(ref a4, a, a2, a3, array[9], 12, 2336552879u);
			FF(ref a3, a4, a, a2, array[10], 17, 4294925233u);
			FF(ref a2, a3, a4, a, array[11], 22, 2304563134u);
			FF(ref a, a2, a3, a4, array[12], 7, 1804603682u);
			FF(ref a4, a, a2, a3, array[13], 12, 4254626195u);
			FF(ref a3, a4, a, a2, array[14], 17, 2792965006u);
			FF(ref a2, a3, a4, a, array[15], 22, 1236535329u);
			GG(ref a, a2, a3, a4, array[1], 5, 4129170786u);
			GG(ref a4, a, a2, a3, array[6], 9, 3225465664u);
			GG(ref a3, a4, a, a2, array[11], 14, 643717713u);
			GG(ref a2, a3, a4, a, array[0], 20, 3921069994u);
			GG(ref a, a2, a3, a4, array[5], 5, 3593408605u);
			GG(ref a4, a, a2, a3, array[10], 9, 38016083u);
			GG(ref a3, a4, a, a2, array[15], 14, 3634488961u);
			GG(ref a2, a3, a4, a, array[4], 20, 3889429448u);
			GG(ref a, a2, a3, a4, array[9], 5, 568446438u);
			GG(ref a4, a, a2, a3, array[14], 9, 3275163606u);
			GG(ref a3, a4, a, a2, array[3], 14, 4107603335u);
			GG(ref a2, a3, a4, a, array[8], 20, 1163531501u);
			GG(ref a, a2, a3, a4, array[13], 5, 2850285829u);
			GG(ref a4, a, a2, a3, array[2], 9, 4243563512u);
			GG(ref a3, a4, a, a2, array[7], 14, 1735328473u);
			GG(ref a2, a3, a4, a, array[12], 20, 2368359562u);
			HH(ref a, a2, a3, a4, array[5], 4, 4294588738u);
			HH(ref a4, a, a2, a3, array[8], 11, 2272392833u);
			HH(ref a3, a4, a, a2, array[11], 16, 1839030562u);
			HH(ref a2, a3, a4, a, array[14], 23, 4259657740u);
			HH(ref a, a2, a3, a4, array[1], 4, 2763975236u);
			HH(ref a4, a, a2, a3, array[4], 11, 1272893353u);
			HH(ref a3, a4, a, a2, array[7], 16, 4139469664u);
			HH(ref a2, a3, a4, a, array[10], 23, 3200236656u);
			HH(ref a, a2, a3, a4, array[13], 4, 681279174u);
			HH(ref a4, a, a2, a3, array[0], 11, 3936430074u);
			HH(ref a3, a4, a, a2, array[3], 16, 3572445317u);
			HH(ref a2, a3, a4, a, array[6], 23, 76029189u);
			HH(ref a, a2, a3, a4, array[9], 4, 3654602809u);
			HH(ref a4, a, a2, a3, array[12], 11, 3873151461u);
			HH(ref a3, a4, a, a2, array[15], 16, 530742520u);
			HH(ref a2, a3, a4, a, array[2], 23, 3299628645u);
			II(ref a, a2, a3, a4, array[0], 6, 4096336452u);
			II(ref a4, a, a2, a3, array[7], 10, 1126891415u);
			II(ref a3, a4, a, a2, array[14], 15, 2878612391u);
			II(ref a2, a3, a4, a, array[5], 21, 4237533241u);
			II(ref a, a2, a3, a4, array[12], 6, 1700485571u);
			II(ref a4, a, a2, a3, array[3], 10, 2399980690u);
			II(ref a3, a4, a, a2, array[10], 15, 4293915773u);
			II(ref a2, a3, a4, a, array[1], 21, 2240044497u);
			II(ref a, a2, a3, a4, array[8], 6, 1873313359u);
			II(ref a4, a, a2, a3, array[15], 10, 4264355552u);
			II(ref a3, a4, a, a2, array[6], 15, 2734768916u);
			II(ref a2, a3, a4, a, array[13], 21, 1309151649u);
			II(ref a, a2, a3, a4, array[4], 6, 4149444226u);
			II(ref a4, a, a2, a3, array[11], 10, 3174756917u);
			II(ref a3, a4, a, a2, array[2], 15, 718787259u);
			II(ref a2, a3, a4, a, array[9], 21, 3951481745u);
			state[0] += a;
			state[1] += a2;
			state[2] += a3;
			state[3] += a4;
		}

		public void Update(byte[] input, int length)
		{
			uint num = (count[0] >> 3) & 0x3Fu;
			count[0] += (uint)(length << 3);
			if (count[0] < length << 3)
			{
				count[1]++;
			}
			count[1] += (uint)(length >> 29);
			int num2 = (int)(64 - num);
			int i = 0;
			if (length >= num2)
			{
				Array.Copy(input, 0L, buffer, num, num2);
				Transform(buffer);
				for (i = num2; i + 63 < length; i += 64)
				{
					byte[] array = new byte[64];
					Array.Copy(input, i, array, 0, 64);
					Transform(array);
				}
				num = 0u;
			}
			if (i < length)
			{
				Array.Copy(input, i, buffer, num, length - i);
			}
		}

		public byte[] Final()
		{
			byte[] array = new byte[64];
			array[0] = 128;
			byte[] array2 = new byte[8];
			Encode(count, array2, 0, 8);
			uint num = (count[0] >> 3) & 0x3Fu;
			uint length = ((num < 56) ? (56 - num) : (120 - num));
			Update(array, (int)length);
			Update(array2, 8);
			byte[] array3 = new byte[16];
			Encode(state, array3, 0, 16);
			Initialize();
			return array3;
		}

		private void Encode(uint[] input, byte[] output, int outputOffset, int length)
		{
			int num = 0;
			for (int i = outputOffset; i < outputOffset + length; i += 4)
			{
				output[i] = (byte)(input[num] & 0xFFu);
				output[i + 1] = (byte)((input[num] >> 8) & 0xFFu);
				output[i + 2] = (byte)((input[num] >> 16) & 0xFFu);
				output[i + 3] = (byte)((input[num] >> 24) & 0xFFu);
				num++;
			}
		}

		public static byte[] ComputeMD5(string input)
		{
			MD5Hash mD5Hash = new MD5Hash();
			byte[] bytes = Encoding.UTF8.GetBytes(input);
			mD5Hash.Update(bytes, bytes.Length);
			return mD5Hash.Final();
		}
	}
	public class Logger
	{
		private readonly string _logDirectory;

		private readonly string _baseFileName;

		private readonly long _maxFileSizeBytes;

		private string _currentFilePath;

		public Logger(string fileName = "logtext.txt", long maxFileSizeMB = 50L)
		{
			_logDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			_baseFileName = fileName;
			_maxFileSizeBytes = maxFileSizeMB * 1024 * 1024;
			_currentFilePath = Path.Combine(_logDirectory, fileName);
		}

		public void Log(string message)
		{
			try
			{
				CheckAndRotateFile();
				string value = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
				using StreamWriter streamWriter = new StreamWriter(_currentFilePath, append: true, Encoding.UTF8);
				streamWriter.WriteLine(value);
			}
			catch
			{
			}
		}

		private void CheckAndRotateFile()
		{
			try
			{
				if (File.Exists(_currentFilePath))
				{
					FileInfo fileInfo = new FileInfo(_currentFilePath);
					if (fileInfo.Length >= _maxFileSizeBytes)
					{
						RotateLogFile();
					}
				}
			}
			catch
			{
			}
		}

		private void RotateLogFile()
		{
			try
			{
				string text = DateTime.Now.ToString("yyyyMMdd_HHmmss");
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_baseFileName);
				string extension = Path.GetExtension(_baseFileName);
				string path = fileNameWithoutExtension + "_" + text + extension;
				string destFileName = Path.Combine(_logDirectory, path);
				if (File.Exists(_currentFilePath))
				{
					File.Move(_currentFilePath, destFileName);
				}
				_currentFilePath = Path.Combine(_logDirectory, _baseFileName);
			}
			catch
			{
			}
		}

		public void Log(string format, params object[] args)
		{
			Log(string.Format(format, args));
		}

		public string GetCurrentLogInfo()
		{
			if (File.Exists(_currentFilePath))
			{
				FileInfo fileInfo = new FileInfo(_currentFilePath);
				return $"当前文件: {_currentFilePath}, 大小: {fileInfo.Length / 1024 / 1024}MB";
			}
			return "当前文件: " + _currentFilePath + ", 文件不存在";
		}
	}
	public class ONU_OP
	{
		public static string ONU_Alarm_GEN(int alarm)
		{
			string text = "";
			if ((alarm & 1) == 1)
			{
				text += "ONUi LOS Alarm;";
			}
			if ((alarm & 2) == 2)
			{
				text += "ONUi LOF Alarm;";
			}
			if ((alarm & 4) == 4)
			{
				text += "ONUi WD Alarm;";
			}
			if ((alarm & 8) == 8)
			{
				text += "GEM CDL Alarm;";
			}
			if ((alarm & 0x10) == 16)
			{
				text += "ONUi RDI Alarm;";
			}
			if ((alarm & 0x20) == 32)
			{
				text += "ONUi PLOAM Loss Alarm;";
			}
			if ((alarm & 0x40) == 64)
			{
				text += "ONUi SWSD Alarm;";
			}
			if ((alarm & 0x80) == 128)
			{
				text += "ONUi TX Power-off Alarm";
			}
			if (text == "")
			{
				text = "No_Alarm";
			}
			return text;
		}

		public static string ONU_status_GEN(byte status)
		{
			string text = "";
			return status switch
			{
				1 => "REG_DONE", 
				2 => "AUTH_DONE", 
				3 => "OMCI_DONE", 
				4 => "ETH_CFG", 
				5 => "ETH_DONE", 
				6 => "ON_LINE", 
				7 => "LOSFI", 
				8 => "OFF_LINE", 
				9 => "LONGLU", 
				10 => "ETH_CFGING", 
				11 => "ETH_CFGING_DOWN", 
				12 => "ONU_FRQ", 
				13 => "DYING_GASP", 
				14 => "POWER_OFF", 
				_ => "", 
			};
		}
	}
	public class ONU_AppData : INotifyPropertyChanged
	{
		private BindingList<ONU_Performance_Notity> _users = null;

		private ONU_Performance_Notity _selectedUser = null;

		private readonly object _lock = new object();

		private int _onlineUsers;

		public BindingList<ONU_Performance_Notity> Users
		{
			get
			{
				return _users;
			}
			set
			{
				if (_users != value)
				{
					if (_users != null)
					{
						_users.ListChanged -= Users_ListChanged;
						UnsubscribeFromAllItems(_users);
					}
					_users = value;
					if (_users != null)
					{
						_users.ListChanged += Users_ListChanged;
						SubscribeToAllItems(_users);
						_users.RaiseListChangedEvents = true;
					}
					OnPropertyChanged("OnlineUsers");
					OnPropertyChanged("TotalUsers");
				}
			}
		}

		public ONU_Performance_Notity SelectedUser
		{
			get
			{
				return _selectedUser;
			}
			set
			{
				if (_selectedUser != value)
				{
					_selectedUser = value;
					OnPropertyChanged("SelectedUser");
					OnPropertyChanged("IsUserSelected");
				}
			}
		}

		public bool IsUserSelected => SelectedUser != null;

		public int TotalUsers => Users?.Count ?? 0;

		public int OnlineUsers
		{
			get
			{
				return _onlineUsers;
			}
			private set
			{
				if (_onlineUsers != value)
				{
					_onlineUsers = value;
					OnPropertyChanged("OnlineUsers");
				}
			}
		}

		public event EventHandler<ONUPropertyChangedEventArgs> ONUPropertyChanged = null;

		public event PropertyChangedEventHandler? PropertyChanged;

		public ONU_AppData()
		{
			Users = new BindingList<ONU_Performance_Notity>();
			Users.RaiseListChangedEvents = true;
			Users.ListChanged += Users_ListChanged;
		}

		public List<List<ONU_Performance_Notity>> GetGroupsByOLT()
		{
			lock (_lock)
			{
				IEnumerable<ONU_Performance_Notity> source = _users.Where((ONU_Performance_Notity u) => u.onu_status != "OFF_LINE" && u.onu_status != "DYING_GASP" && u.read_FLAG == 0);
				List<List<ONU_Performance_Notity>> result = (from u in source
					group u by u.olt_ip into g
					select g.ToList()).ToList();
				List<IPAddress> list = (from u in _users
					group u by u.olt_ip into g
					where !g.Any((ONU_Performance_Notity u) => u.read_FLAG == 0)
					select g.Key).ToList();
				if (list.Any())
				{
					foreach (IPAddress oltIp in list)
					{
						List<ONU_Performance_Notity> list2 = _users.Where((ONU_Performance_Notity u) => u.olt_ip.Equals(oltIp)).ToList();
						foreach (ONU_Performance_Notity item in list2)
						{
							item.read_FLAG = 0;
						}
					}
					source = _users.Where((ONU_Performance_Notity u) => u.onu_status != "OFF_LINE" && u.onu_status != "DYING_GASP" && u.read_FLAG == 0);
					result = (from u in source
						group u by u.olt_ip into g
						select g.ToList()).ToList();
				}
				return result;
			}
		}

		public IEnumerable<ONU_Performance_Notity> TraverseByGroupIndex()
		{
			lock (_lock)
			{
				IEnumerable<ONU_Performance_Notity> filteredUsers = _users.Where((ONU_Performance_Notity u) => u.onu_status != "OFF_LINE" && u.onu_status != "DYING_GASP" && u.read_FLAG == 0);
				List<List<ONU_Performance_Notity>> groups = (from u in filteredUsers
					group u by u.olt_ip into g
					select g.ToList()).ToList();
				int maxCount = (groups.Any() ? groups.Max((List<ONU_Performance_Notity> g) => g.Count) : 0);
				for (int i = 0; i < maxCount; i++)
				{
					foreach (List<ONU_Performance_Notity> group in groups)
					{
						if (i < group.Count)
						{
							yield return group[i];
						}
					}
				}
			}
		}

		public IEnumerable<ONU_Performance_Notity> Save2databaseNormallist()
		{
			lock (_lock)
			{
				return _users.Where((ONU_Performance_Notity u) => u != null && u.onu_status != "OFF_LINE" && u.onu_status != "DYINGGASP").ToList();
			}
		}

		public IEnumerable<ONU_Performance_Notity> Save2databaseImmediatelylist()
		{
			lock (_lock)
			{
				List<ONU_Performance_Notity> list = _users.Where((ONU_Performance_Notity u) => u != null && u.needsaverightnow == "YES").ToList();
				foreach (ONU_Performance_Notity item in list)
				{
					item.needsaverightnow = "NO";
				}
				return list;
			}
		}

		public void RemoveOfflineAndDyinggaspOnus()
		{
			lock (_lock)
			{
				List<ONU_Performance_Notity> list = _users.Where((ONU_Performance_Notity u) => u != null && (u.onu_status == "OFF_LINE" || u.onu_status == "DYINGGASP")).ToList();
				foreach (ONU_Performance_Notity item in list)
				{
					_users.Remove(item);
				}
			}
		}

		public void AddONU(ONU_Performance_Notity onu)
		{
			lock (_lock)
			{
				_users.Add(onu);
			}
		}

		public void RemoveONUAt(int index)
		{
			lock (_lock)
			{
				_users.RemoveAt(index);
			}
		}

		private void Get_onlineusers()
		{
			lock (_lock)
			{
				int num = 0;
				if (Users != null)
				{
					foreach (ONU_Performance_Notity user in Users)
					{
						if (user.onu_status == "ON_LINE")
						{
							num++;
						}
					}
				}
				OnlineUsers = num;
			}
		}

		private void Users_ListChanged(object? sender, ListChangedEventArgs e)
		{
			switch (e.ListChangedType)
			{
			case ListChangedType.ItemAdded:
			{
				ONU_Performance_Notity oNU_Performance_Notity = Users[e.NewIndex];
				oNU_Performance_Notity.PropertyChanged += User_PropertyChanged;
				if (oNU_Performance_Notity.onu_status == "ON_LINE")
				{
					Get_onlineusers();
				}
				break;
			}
			case ListChangedType.ItemDeleted:
				Get_onlineusers();
				break;
			case ListChangedType.ItemChanged:
				if (e.PropertyDescriptor?.Name == "onu_status")
				{
					Get_onlineusers();
				}
				break;
			case ListChangedType.Reset:
				SubscribeToAllItems(Users);
				Get_onlineusers();
				break;
			}
			OnPropertyChanged("TotalUsers");
		}

		private void User_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender != null && e.PropertyName != null && sender is ONU_Performance_Notity onu)
			{
				ONUPropertyChangedEventArgs e2 = new ONUPropertyChangedEventArgs(onu, e.PropertyName);
				OnONUPropertyChanged(e2);
			}
		}

		private void SubscribeToAllItems(BindingList<ONU_Performance_Notity> items)
		{
			foreach (ONU_Performance_Notity item in items)
			{
				item.PropertyChanged += User_PropertyChanged;
			}
		}

		private void UnsubscribeFromAllItems(BindingList<ONU_Performance_Notity> items)
		{
			foreach (ONU_Performance_Notity item in items)
			{
				item.PropertyChanged -= User_PropertyChanged;
			}
		}

		public void RemoveONU(ONU_Performance_Notity onu)
		{
			if (Users.Contains(onu))
			{
				onu.PropertyChanged -= User_PropertyChanged;
				Users.Remove(onu);
			}
		}

		public void ClearAllONU()
		{
			foreach (ONU_Performance_Notity item in Users.ToList())
			{
				item.PropertyChanged -= User_PropertyChanged;
			}
			Users.Clear();
		}

		protected virtual void OnONUPropertyChanged(ONUPropertyChangedEventArgs e)
		{
			this.ONUPropertyChanged?.Invoke(this, e);
		}

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
	public class ONUPropertyChangedEventArgs : EventArgs
	{
		public ONU_Performance_Notity ONU { get; }

		public string PropertyName { get; }

		public ONUPropertyChangedEventArgs(ONU_Performance_Notity onu, string propertyName)
		{
			ONU = onu;
			PropertyName = propertyName;
		}
	}
	public class ONU_Performance_Notity : INotifyPropertyChanged
	{
		private IPAddress _olt_ip = IPAddress.None;

		private string _onu_sn = string.Empty;

		private string _onu_status = string.Empty;

		private string _onu_tx_pwr = string.Empty;

		private string _onu_rx_pwr = string.Empty;

		private string _onu_bias = string.Empty;

		private string _onu_temperature = string.Empty;

		private string _onu_rssi = string.Empty;

		private string _alarm_status = string.Empty;

		private string _onu_voltage = string.Empty;

		private double _time_second = 0.0;

		private string _time_string = string.Empty;

		private string _needsaverightnow = string.Empty;

		private int _read_FLAG = 0;

		public IPAddress olt_ip
		{
			get
			{
				return _olt_ip;
			}
			set
			{
				if (_olt_ip != value)
				{
					_olt_ip = value;
					OnPropertyChanged("olt_ip");
				}
			}
		}

		public double time_second
		{
			get
			{
				return _time_second;
			}
			set
			{
				if (_time_second != value)
				{
					_time_second = value;
				}
			}
		}

		public string needsaverightnow
		{
			get
			{
				return _needsaverightnow;
			}
			set
			{
				if (_needsaverightnow != value)
				{
					_needsaverightnow = value;
				}
				OnPropertyChanged("needsaverightnow");
			}
		}

		public string time_string
		{
			get
			{
				return _time_string;
			}
			set
			{
				if (_time_string != value)
				{
					_time_string = value;
				}
			}
		}

		public string onu_sn
		{
			get
			{
				return _onu_sn;
			}
			set
			{
				if (_onu_sn != value)
				{
					_onu_sn = value;
					OnPropertyChanged("onu_sn");
				}
			}
		}

		public string onu_status
		{
			get
			{
				return _onu_status;
			}
			set
			{
				if (_onu_status != value)
				{
					_onu_status = value;
					OnPropertyChanged("onu_status");
				}
			}
		}

		public string onu_tx_pwr
		{
			get
			{
				return _onu_tx_pwr;
			}
			set
			{
				if (_onu_tx_pwr != value)
				{
					_onu_tx_pwr = value;
					OnPropertyChanged("onu_tx_pwr");
				}
			}
		}

		public string onu_rx_pwr
		{
			get
			{
				return _onu_rx_pwr;
			}
			set
			{
				if (_onu_rx_pwr != value)
				{
					_onu_rx_pwr = value;
					OnPropertyChanged("onu_rx_pwr");
				}
			}
		}

		public string onu_bias
		{
			get
			{
				return _onu_bias;
			}
			set
			{
				if (_onu_bias != value)
				{
					_onu_bias = value;
					OnPropertyChanged("onu_bias");
				}
			}
		}

		public string onu_voltage
		{
			get
			{
				return _onu_voltage;
			}
			set
			{
				if (_onu_voltage != value)
				{
					_onu_voltage = value;
					OnPropertyChanged("onu_voltage");
				}
			}
		}

		public string onu_temperature
		{
			get
			{
				return _onu_temperature;
			}
			set
			{
				if (_onu_temperature != value)
				{
					_onu_temperature = value;
					OnPropertyChanged("onu_temperature");
				}
			}
		}

		public string onu_rssi
		{
			get
			{
				return _onu_rssi;
			}
			set
			{
				if (_onu_rssi != value)
				{
					_onu_rssi = value;
					OnPropertyChanged("onu_rssi");
				}
			}
		}

		public string alarm_status
		{
			get
			{
				return _alarm_status;
			}
			set
			{
				if (_alarm_status != value)
				{
					_alarm_status = value;
					OnPropertyChanged("alarm_status");
				}
			}
		}

		public int read_FLAG
		{
			get
			{
				return _read_FLAG;
			}
			set
			{
				if (_read_FLAG != value)
				{
					_read_FLAG = value;
				}
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
	public class OLT_AppData : INotifyPropertyChanged
	{
		private BindingList<OLT_Performance_Notity> _users = null;

		private OLT_Performance_Notity _selectedUser = null;

		private readonly object _lock = new object();

		public BindingList<OLT_Performance_Notity> Users
		{
			get
			{
				return _users;
			}
			set
			{
				if (_users != value)
				{
					if (_users != null)
					{
						_users.ListChanged -= Users_ListChanged;
						UnsubscribeFromAllItems(_users);
					}
					_users = value;
					if (_users != null)
					{
						_users.ListChanged += Users_ListChanged;
						SubscribeToAllItems(_users);
						_users.RaiseListChangedEvents = true;
					}
					OnPropertyChanged("Users");
					OnPropertyChanged("TotalUsers");
				}
			}
		}

		public OLT_Performance_Notity SelectedUser
		{
			get
			{
				return _selectedUser;
			}
			set
			{
				if (_selectedUser != value)
				{
					_selectedUser = value;
					OnPropertyChanged("SelectedUser");
					OnPropertyChanged("IsUserSelected");
				}
			}
		}

		public bool IsUserSelected => SelectedUser != null;

		public int TotalUsers => Users?.Count ?? 0;

		public event EventHandler<OLTPropertyChangedEventArgs> OLTPropertyChanged = null;

		public event PropertyChangedEventHandler? PropertyChanged;

		public OLT_AppData()
		{
			Users = new BindingList<OLT_Performance_Notity>();
			Users.RaiseListChangedEvents = true;
			Users.ListChanged += Users_ListChanged;
		}

		public IEnumerable<OLT_Performance_Notity> Save2databaseNormallist()
		{
			lock (_lock)
			{
				return _users.Where((OLT_Performance_Notity u) => u != null && u.olt_status != "OFF_LINE").ToList();
			}
		}

		public IEnumerable<OLT_Performance_Notity> Save2databaseImmediatelylist()
		{
			lock (_lock)
			{
				List<OLT_Performance_Notity> list = _users.Where((OLT_Performance_Notity u) => u != null && u.needsaverightnow == "YES").ToList();
				foreach (OLT_Performance_Notity item in list)
				{
					item.needsaverightnow = "NO";
				}
				return list;
			}
		}

		public void RemovedOLTbyIP(IPAddress ip)
		{
			IPAddress ip2 = ip;
			lock (_lock)
			{
				List<OLT_Performance_Notity> list = _users.Where((OLT_Performance_Notity u) => u?.olt_ip.Equals(ip2) ?? false).ToList();
				foreach (OLT_Performance_Notity item in list)
				{
					_users.Remove(item);
				}
			}
		}

		public void RemoveOfflineAndDyinggaspOnus()
		{
			lock (_lock)
			{
				List<OLT_Performance_Notity> list = _users.Where((OLT_Performance_Notity u) => u != null && u.olt_status == "OFF_LINE").ToList();
				foreach (OLT_Performance_Notity item in list)
				{
					_users.Remove(item);
				}
			}
		}

		public void AddOLT(OLT_Performance_Notity olt)
		{
			lock (_lock)
			{
				try
				{
					_users.Add(olt);
				}
				catch
				{
				}
			}
		}

		public void RemoveOLTAt(int index)
		{
			lock (_lock)
			{
				try
				{
					_users.RemoveAt(index);
				}
				catch
				{
				}
			}
		}

		private void Users_ListChanged(object? sender, ListChangedEventArgs e)
		{
			switch (e.ListChangedType)
			{
			case ListChangedType.ItemAdded:
			{
				OLT_Performance_Notity oLT_Performance_Notity = Users[e.NewIndex];
				oLT_Performance_Notity.PropertyChanged += User_PropertyChanged;
				break;
			}
			case ListChangedType.Reset:
				SubscribeToAllItems(Users);
				break;
			}
			OnPropertyChanged("TotalUsers");
		}

		private void User_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender != null && e.PropertyName != null && sender is OLT_Performance_Notity olt)
			{
				OLTPropertyChangedEventArgs e2 = new OLTPropertyChangedEventArgs(olt, e.PropertyName);
				OnOLTPropertyChanged(e2);
			}
		}

		private void SubscribeToAllItems(BindingList<OLT_Performance_Notity> items)
		{
			foreach (OLT_Performance_Notity item in items)
			{
				item.PropertyChanged += User_PropertyChanged;
			}
		}

		private void UnsubscribeFromAllItems(BindingList<OLT_Performance_Notity> items)
		{
			foreach (OLT_Performance_Notity item in items)
			{
				item.PropertyChanged -= User_PropertyChanged;
			}
		}

		public void RemoveOLT(OLT_Performance_Notity olt)
		{
			if (Users.Contains(olt))
			{
				olt.PropertyChanged -= User_PropertyChanged;
				Users.Remove(olt);
			}
		}

		public void ClearAllOLT()
		{
			foreach (OLT_Performance_Notity item in Users.ToList())
			{
				item.PropertyChanged -= User_PropertyChanged;
			}
			Users.Clear();
		}

		protected virtual void OnOLTPropertyChanged(OLTPropertyChangedEventArgs e)
		{
			this.OLTPropertyChanged?.Invoke(this, e);
		}

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
	public class OLTPropertyChangedEventArgs : EventArgs
	{
		public OLT_Performance_Notity OLT { get; }

		public string PropertyName { get; }

		public OLTPropertyChangedEventArgs(OLT_Performance_Notity olt, string propertyName)
		{
			OLT = olt;
			PropertyName = propertyName;
		}
	}
	public class OLT_Performance_Notity : INotifyPropertyChanged
	{
		private IPAddress _olt_ip = IPAddress.None;

		private string _olt_sn = string.Empty;

		private string _olt_mac = string.Empty;

		private string _olt_status = string.Empty;

		private string _olt_tx_pwr = string.Empty;

		private string _olt_bias = string.Empty;

		private string _olt_temperature = string.Empty;

		private string _olt_rssi = string.Empty;

		private string _alarm_status = string.Empty;

		private string _olt_voltage = string.Empty;

		private double _time_second = 0.0;

		private string _time_string = string.Empty;

		private string _needsaverightnow = string.Empty;

		public IPAddress olt_ip
		{
			get
			{
				return _olt_ip;
			}
			set
			{
				if (_olt_ip != value)
				{
					_olt_ip = value;
					OnPropertyChanged("olt_ip");
				}
			}
		}

		public string olt_mac
		{
			get
			{
				return _olt_mac;
			}
			set
			{
				if (_olt_mac != value)
				{
					_olt_mac = value;
					OnPropertyChanged("olt_mac");
				}
			}
		}

		public double time_second
		{
			get
			{
				return _time_second;
			}
			set
			{
				if (_time_second != value)
				{
					_time_second = value;
				}
			}
		}

		public string needsaverightnow
		{
			get
			{
				return _needsaverightnow;
			}
			set
			{
				if (_needsaverightnow != value)
				{
					_needsaverightnow = value;
				}
			}
		}

		public string time_string
		{
			get
			{
				return _time_string;
			}
			set
			{
				if (_time_string != value)
				{
					_time_string = value;
				}
			}
		}

		public string olt_sn
		{
			get
			{
				return _olt_sn;
			}
			set
			{
				if (_olt_sn != value)
				{
					_olt_sn = value;
					OnPropertyChanged("olt_sn");
				}
			}
		}

		public string olt_status
		{
			get
			{
				return _olt_status;
			}
			set
			{
				if (_olt_status != value)
				{
					_olt_status = value;
					OnPropertyChanged("olt_status");
				}
			}
		}

		public string olt_tx_pwr
		{
			get
			{
				return _olt_tx_pwr;
			}
			set
			{
				if (_olt_tx_pwr != value)
				{
					_olt_tx_pwr = value;
					OnPropertyChanged("olt_tx_pwr");
				}
			}
		}

		public string olt_bias
		{
			get
			{
				return _olt_bias;
			}
			set
			{
				if (_olt_bias != value)
				{
					_olt_bias = value;
					OnPropertyChanged("olt_bias");
				}
			}
		}

		public string olt_voltage
		{
			get
			{
				return _olt_voltage;
			}
			set
			{
				if (_olt_voltage != value)
				{
					_olt_voltage = value;
					OnPropertyChanged("olt_voltage");
				}
			}
		}

		public string olt_temperature
		{
			get
			{
				return _olt_temperature;
			}
			set
			{
				if (_olt_temperature != value)
				{
					_olt_temperature = value;
					OnPropertyChanged("olt_temperature");
				}
			}
		}

		public string olt_rssi
		{
			get
			{
				return _olt_rssi;
			}
			set
			{
				if (_olt_rssi != value)
				{
					_olt_rssi = value;
					OnPropertyChanged("olt_rssi");
				}
			}
		}

		public string alarm_status
		{
			get
			{
				return _alarm_status;
			}
			set
			{
				if (_alarm_status != value)
				{
					_alarm_status = value;
					OnPropertyChanged("alarm_status");
				}
			}
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
	public class ONU_HISTORY : Form
	{
		private string dbPath = "example.db";

		private string connectionString = "";

		private DatabaseHelper databaseHelper;

		private IContainer components = null;

		private Button button_Search_ONU_Performance;

		private Label label2;

		private TextBox textBox_lut_timebase;

		private Label label1;

		private ComboBox comboBox_OLT_IP_search;

		private Label OLT_information_label;

		private DataGridView dataGridView_OLT_WL;

		private ComboBox comboBox_CPE_SN;

		private Label label3;

		private Button button_Search_ONUSN_Performance;

		private CustomComboBox items_number;

		public ONU_HISTORY(string filepath)
		{
			InitializeComponent();
			OLT_information_label.ForeColor = ColorTranslator.FromHtml("#212519");
			label1.ForeColor = ColorTranslator.FromHtml("#212519");
			label2.ForeColor = ColorTranslator.FromHtml("#212519");
			label3.ForeColor = ColorTranslator.FromHtml("#212519");
			button_Search_ONU_Performance.FlatStyle = FlatStyle.Flat;
			button_Search_ONU_Performance.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#14C9BB");
			button_Search_ONU_Performance.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 20, 201, 187);
			button_Search_ONU_Performance.BackColor = ColorTranslator.FromHtml("#FFFFFF");
			button_Search_ONU_Performance.ForeColor = ColorTranslator.FromHtml("#14C9BB");
			button_Search_ONUSN_Performance.FlatStyle = FlatStyle.Flat;
			button_Search_ONUSN_Performance.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#14C9BB");
			button_Search_ONUSN_Performance.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 20, 201, 187);
			button_Search_ONUSN_Performance.BackColor = ColorTranslator.FromHtml("#FFFFFF");
			button_Search_ONUSN_Performance.ForeColor = ColorTranslator.FromHtml("#14C9BB");
			dataGridView_OLT_WL.BackgroundColor = ColorTranslator.FromHtml("#F6F6F6");
			dataGridView_OLT_WL.DefaultCellStyle.SelectionBackColor = ColorTranslator.FromHtml("#14C9BB");
			dbPath = filepath;
			connectionString = "Data Source=" + dbPath + ";Version=3;";
			databaseHelper = new DatabaseHelper(dbPath, connectionString, 1.0);
			OLT_information_label.Text = "History information: OLTs:";
			items_number.SelectedIndex = 0;
			base.Icon = new Icon("FSLogo.ico");
		}

		private int time_parser(string dateString, out DateTime dateTime)
		{
			int result = 0;
			dateTime = DateTime.Now;
			if (DateTime.TryParseExact(dateString, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result2))
			{
				dateTime = result2;
			}
			else
			{
				result = -1;
			}
			return result;
		}

		private void button_Search_ONU_Performance_Click(object sender, EventArgs e)
		{
			string text = textBox_lut_timebase.Text.Trim();
			string text2 = comboBox_OLT_IP_search.Text;
			double num = 0.0;
			int num2 = 0;
			if (text == "")
			{
				num = 0.0;
			}
			else
			{
				if (time_parser(text, out var dateTime) != 0)
				{
					MessageBox.Show("Date input is not right, please check the format YYYY-MM-DD");
					return;
				}
				num = (dateTime - new DateTime(1970, 1, 1)).TotalSeconds;
			}
			if (text2.Trim() != "")
			{
				try
				{
					IPAddress iPAddress = IPAddress.Parse(text2);
				}
				catch (Exception ex)
				{
					MessageBox.Show("IP address format is not right" + ex.Message);
				}
			}
			try
			{
				string[] array = items_number.Text.Trim().Split(',', ' ');
				num2 = ((array.Length <= 2) ? int.Parse(array[0]) : int.Parse(array[1]));
			}
			catch
			{
				num2 = 2000;
			}
			string text3 = "";
			text3 = ((!(text2.Trim() != "")) ? $"select * from  ONU_Performance where TimeSecond > '{num}' ORDER BY TimeSecond DESC LIMIT {num2} " : $"select * from  ONU_Performance where OltIp ='{text2}' and TimeSecond > '{num}' ORDER BY TimeSecond DESC LIMIT {num2} ");
			try
			{
				int num3 = 0;
				if (databaseHelper.sql_search(text3, out DataTable processTable) == 0)
				{
					dataGridView_OLT_WL.DataSource = processTable;
					dataGridView_OLT_WL.Columns["id"].Visible = false;
					dataGridView_OLT_WL.Columns["TimeSecond"].Visible = false;
					PopulateComboBoxWithUniqueIPs(processTable, comboBox_OLT_IP_search);
					comboBox_OLT_IP_search.SelectedIndex = 0;
					PopulateComboBoxWithUniqueCPEs(processTable, comboBox_CPE_SN);
					comboBox_CPE_SN.SelectedIndex = 0;
				}
			}
			catch
			{
			}
		}

		private static void PopulateComboBoxWithUniqueIPs(DataTable processTable, ComboBox comboBox)
		{
			comboBox.Items.Clear();
			if (processTable == null || processTable.Rows.Count == 0)
			{
				comboBox.Items.Add("No data available");
				return;
			}
			if (!processTable.Columns.Contains("OltIp"))
			{
				comboBox.Items.Add("Column 'OltIp' not found");
				return;
			}
			try
			{
				List<string> list = (from ip in (from row in processTable.AsEnumerable()
						select row.Field<string>("OltIp") into ip
						where !string.IsNullOrWhiteSpace(ip)
						select ip).Distinct()
					orderby ip
					select ip).ToList();
				if (list.Count != 0)
				{
					ComboBox.ObjectCollection items = comboBox.Items;
					object[] items2 = list.ToArray();
					items.AddRange(items2);
				}
				else
				{
					comboBox.Items.Add("No valid IP addresses found");
				}
			}
			catch (Exception ex)
			{
				comboBox.Items.Add("Error loading IPs");
				MessageBox.Show("Failed to load IP addresses: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		private static void PopulateComboBoxWithUniqueCPEs(DataTable processTable, ComboBox comboBox)
		{
			comboBox.Items.Clear();
			if (processTable == null || processTable.Rows.Count == 0)
			{
				comboBox.Items.Add("No data available");
				return;
			}
			if (!processTable.Columns.Contains("OnuSn"))
			{
				comboBox.Items.Add("Column 'OnuSn' not found");
				return;
			}
			try
			{
				List<string> list = (from ip in (from row in processTable.AsEnumerable()
						select row.Field<string>("OnuSn") into ip
						where !string.IsNullOrWhiteSpace(ip)
						select ip).Distinct()
					orderby ip
					select ip).ToList();
				if (list.Count != 0)
				{
					ComboBox.ObjectCollection items = comboBox.Items;
					object[] items2 = list.ToArray();
					items.AddRange(items2);
				}
				else
				{
					comboBox.Items.Add("No valid OnuSn found");
				}
			}
			catch (Exception ex)
			{
				comboBox.Items.Add("Error loading OnuSn");
				MessageBox.Show("Failed to load OnuSn: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Hand);
			}
		}

		private void button_Search_ONUSN_Performance_Click(object sender, EventArgs e)
		{
			string text = textBox_lut_timebase.Text.Trim();
			string text2 = comboBox_CPE_SN.Text;
			double num = 0.0;
			int num2 = 0;
			if (text == "")
			{
				num = 0.0;
			}
			else
			{
				if (time_parser(text, out var dateTime) != 0)
				{
					MessageBox.Show("Date input is not right, please check the format YYYY-MM-DD");
					return;
				}
				num = (dateTime - new DateTime(1970, 1, 1)).TotalSeconds;
			}
			if (text2.Trim() != "")
			{
				text2 = text2.Trim();
			}
			try
			{
				string[] array = items_number.Text.Trim().Split(',', ' ');
				num2 = ((array.Length <= 2) ? int.Parse(array[0]) : int.Parse(array[1]));
			}
			catch
			{
				num2 = 2000;
			}
			string text3 = "";
			text3 = ((!(text2.Trim() != "")) ? $"select * from  ONU_Performance where TimeSecond > '{num}' ORDER BY TimeSecond DESC LIMIT {num2} " : $"select * from  ONU_Performance where OnuSn ='{text2}' and TimeSecond > '{num}' ORDER BY TimeSecond DESC LIMIT {num2} ");
			try
			{
				int num3 = 0;
				if (databaseHelper.sql_search(text3, out DataTable processTable) == 0)
				{
					dataGridView_OLT_WL.DataSource = processTable;
					dataGridView_OLT_WL.Columns["id"].Visible = false;
					dataGridView_OLT_WL.Columns["TimeSecond"].Visible = false;
				}
			}
			catch
			{
			}
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && components != null)
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		private void InitializeComponent()
		{
			this.button_Search_ONU_Performance = new System.Windows.Forms.Button();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox_lut_timebase = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.comboBox_OLT_IP_search = new System.Windows.Forms.ComboBox();
			this.OLT_information_label = new System.Windows.Forms.Label();
			this.dataGridView_OLT_WL = new System.Windows.Forms.DataGridView();
			this.comboBox_CPE_SN = new System.Windows.Forms.ComboBox();
			this.label3 = new System.Windows.Forms.Label();
			this.button_Search_ONUSN_Performance = new System.Windows.Forms.Button();
			this.items_number = new APP_OLT_Stick_V2.CustomComboBox();
			((System.ComponentModel.ISupportInitialize)this.dataGridView_OLT_WL).BeginInit();
			base.SuspendLayout();
			this.button_Search_ONU_Performance.Location = new System.Drawing.Point(688, 26);
			this.button_Search_ONU_Performance.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button_Search_ONU_Performance.Name = "button_Search_ONU_Performance";
			this.button_Search_ONU_Performance.Size = new System.Drawing.Size(117, 29);
			this.button_Search_ONU_Performance.TabIndex = 24;
			this.button_Search_ONU_Performance.Text = "Search(OLT)";
			this.button_Search_ONU_Performance.UseVisualStyleBackColor = true;
			this.button_Search_ONU_Performance.Click += new System.EventHandler(button_Search_ONU_Performance_Click);
			this.label2.AutoSize = true;
			this.label2.ForeColor = System.Drawing.SystemColors.MenuHighlight;
			this.label2.Location = new System.Drawing.Point(405, 4);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(109, 20);
			this.label2.TabIndex = 23;
			this.label2.Text = "ItemNumbers";
			this.textBox_lut_timebase.Location = new System.Drawing.Point(211, 28);
			this.textBox_lut_timebase.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.textBox_lut_timebase.Multiline = true;
			this.textBox_lut_timebase.Name = "textBox_lut_timebase";
			this.textBox_lut_timebase.Size = new System.Drawing.Size(188, 28);
			this.textBox_lut_timebase.TabIndex = 22;
			this.label1.AutoSize = true;
			this.label1.ForeColor = System.Drawing.SystemColors.MenuHighlight;
			this.label1.Location = new System.Drawing.Point(211, 5);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(188, 20);
			this.label1.TabIndex = 20;
			this.label1.Text = "TimeBase(YYYY-MM-DD)";
			this.comboBox_OLT_IP_search.FormattingEnabled = true;
			this.comboBox_OLT_IP_search.Location = new System.Drawing.Point(10, 28);
			this.comboBox_OLT_IP_search.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.comboBox_OLT_IP_search.Name = "comboBox_OLT_IP_search";
			this.comboBox_OLT_IP_search.Size = new System.Drawing.Size(188, 28);
			this.comboBox_OLT_IP_search.TabIndex = 19;
			this.OLT_information_label.AutoSize = true;
			this.OLT_information_label.ForeColor = System.Drawing.SystemColors.MenuHighlight;
			this.OLT_information_label.Location = new System.Drawing.Point(10, 5);
			this.OLT_information_label.Name = "OLT_information_label";
			this.OLT_information_label.Size = new System.Drawing.Size(158, 20);
			this.OLT_information_label.TabIndex = 18;
			this.OLT_information_label.Text = "Current Active OLTs:";
			this.dataGridView_OLT_WL.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
			this.dataGridView_OLT_WL.Location = new System.Drawing.Point(10, 62);
			this.dataGridView_OLT_WL.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.dataGridView_OLT_WL.Name = "dataGridView_OLT_WL";
			this.dataGridView_OLT_WL.RowHeadersWidth = 51;
			this.dataGridView_OLT_WL.Size = new System.Drawing.Size(933, 529);
			this.dataGridView_OLT_WL.TabIndex = 17;
			this.comboBox_CPE_SN.FormattingEnabled = true;
			this.comboBox_CPE_SN.Location = new System.Drawing.Point(536, 27);
			this.comboBox_CPE_SN.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.comboBox_CPE_SN.Name = "comboBox_CPE_SN";
			this.comboBox_CPE_SN.Size = new System.Drawing.Size(145, 28);
			this.comboBox_CPE_SN.TabIndex = 25;
			this.label3.AutoSize = true;
			this.label3.ForeColor = System.Drawing.SystemColors.MenuHighlight;
			this.label3.Location = new System.Drawing.Point(519, 5);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(73, 20);
			this.label3.TabIndex = 26;
			this.label3.Text = "ONU SN:";
			this.button_Search_ONUSN_Performance.Location = new System.Drawing.Point(810, 26);
			this.button_Search_ONUSN_Performance.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			this.button_Search_ONUSN_Performance.Name = "button_Search_ONUSN_Performance";
			this.button_Search_ONUSN_Performance.Size = new System.Drawing.Size(111, 29);
			this.button_Search_ONUSN_Performance.TabIndex = 27;
			this.button_Search_ONUSN_Performance.Text = "Search(ONU)";
			this.button_Search_ONUSN_Performance.UseVisualStyleBackColor = true;
			this.button_Search_ONUSN_Performance.Click += new System.EventHandler(button_Search_ONUSN_Performance_Click);
			this.items_number.BorderColor = System.Drawing.Color.FromArgb(20, 201, 187);
			this.items_number.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
			this.items_number.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
			this.items_number.FillColor = System.Drawing.Color.FromArgb(20, 201, 187);
			this.items_number.Font = new System.Drawing.Font("微软雅黑", 9f);
			this.items_number.FormattingEnabled = true;
			this.items_number.ItemHeight = 20;
			this.items_number.Items.AddRange(new object[5] { "Last 100", "Last 200", "Last 500", "Last 1000", "Last 2000" });
			this.items_number.Location = new System.Drawing.Point(406, 26);
			this.items_number.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
			this.items_number.Name = "items_number";
			this.items_number.Size = new System.Drawing.Size(124, 26);
			this.items_number.TabIndex = 28;
			this.items_number.TextColor = System.Drawing.Color.Black;
			base.AutoScaleDimensions = new System.Drawing.SizeF(9f, 20f);
			base.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			base.ClientSize = new System.Drawing.Size(948, 604);
			base.Controls.Add(this.items_number);
			base.Controls.Add(this.button_Search_ONUSN_Performance);
			base.Controls.Add(this.label3);
			base.Controls.Add(this.comboBox_CPE_SN);
			base.Controls.Add(this.button_Search_ONU_Performance);
			base.Controls.Add(this.label2);
			base.Controls.Add(this.textBox_lut_timebase);
			base.Controls.Add(this.label1);
			base.Controls.Add(this.comboBox_OLT_IP_search);
			base.Controls.Add(this.OLT_information_label);
			base.Controls.Add(this.dataGridView_OLT_WL);
			base.Margin = new System.Windows.Forms.Padding(3, 4, 3, 4);
			base.Name = "ONU_HISTORY";
			this.Text = "ONU_History";
			((System.ComponentModel.ISupportInitialize)this.dataGridView_OLT_WL).EndInit();
			base.ResumeLayout(false);
			base.PerformLayout();
		}
	}
	[GeneratedCode("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
	[DebuggerNonUserCode]
	[CompilerGenerated]
	public class Resource1
	{
		private static ResourceManager resourceMan;

		private static CultureInfo resourceCulture;

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static ResourceManager ResourceManager
		{
			get
			{
				if (resourceMan == null)
				{
					ResourceManager resourceManager = new ResourceManager("APP_OLT_Stick_V2.Resource1", typeof(Resource1).Assembly);
					resourceMan = resourceManager;
				}
				return resourceMan;
			}
		}

		[EditorBrowsable(EditorBrowsableState.Advanced)]
		public static CultureInfo Culture
		{
			get
			{
				return resourceCulture;
			}
			set
			{
				resourceCulture = value;
			}
		}

		internal Resource1()
		{
		}
	}
	public class CustomComboBox : ComboBox
	{
		private Color fillColor = ColorTranslator.FromHtml("#14C9BB");

		private Color borderColor = ColorTranslator.FromHtml("#14C9BB");

		private Color textColor = Color.Black;

		public Color FillColor
		{
			get
			{
				return fillColor;
			}
			set
			{
				fillColor = value;
				Invalidate();
			}
		}

		public Color BorderColor
		{
			get
			{
				return borderColor;
			}
			set
			{
				borderColor = value;
				Invalidate();
			}
		}

		public Color TextColor
		{
			get
			{
				return textColor;
			}
			set
			{
				textColor = value;
				Invalidate();
			}
		}

		public CustomComboBox()
		{
			SetStyle(ControlStyles.UserPaint, value: true);
			SetStyle(ControlStyles.AllPaintingInWmPaint, value: true);
			SetStyle(ControlStyles.DoubleBuffer, value: true);
			SetStyle(ControlStyles.ResizeRedraw, value: true);
			base.DrawMode = DrawMode.OwnerDrawFixed;
			base.DropDownStyle = ComboBoxStyle.DropDownList;
			base.ItemHeight = 20;
			Font = new Font("Microsoft YaHei", 9f);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (SolidBrush brush = new SolidBrush(Color.FromArgb(26, fillColor)))
			{
				graphics.FillRectangle(brush, base.ClientRectangle);
			}
			using (Pen pen = new Pen(borderColor, 1f))
			{
				Rectangle rect = new Rectangle(0, 0, base.Width - 1, base.Height - 1);
				graphics.DrawRectangle(pen, rect);
			}
			if (SelectedIndex >= 0)
			{
				string itemText = GetItemText(base.SelectedItem);
				Rectangle rectangle = new Rectangle(8, 0, base.Width - 25, base.Height);
				using SolidBrush brush2 = new SolidBrush(textColor);
				StringFormat format = new StringFormat
				{
					LineAlignment = StringAlignment.Center,
					Alignment = StringAlignment.Near
				};
				graphics.DrawString(itemText, Font, brush2, rectangle, format);
			}
			DrawDropDownArrow(graphics);
		}

		private void DrawDropDownArrow(Graphics g)
		{
			int num = 4;
			int num2 = base.Width - 15;
			int num3 = base.Height / 2;
			Point[] points = new Point[3]
			{
				new Point(num2 - num, num3 - num / 2),
				new Point(num2 + num, num3 - num / 2),
				new Point(num2, num3 + num / 2)
			};
			using SolidBrush brush = new SolidBrush(Color.Gray);
			g.FillPolygon(brush, points);
		}

		protected override void OnDrawItem(DrawItemEventArgs e)
		{
			if (e.Index < 0)
			{
				return;
			}
			Graphics graphics = e.Graphics;
			graphics.SmoothingMode = SmoothingMode.AntiAlias;
			if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
			{
				using SolidBrush brush = new SolidBrush(Color.FromArgb(51, fillColor));
				graphics.FillRectangle(brush, e.Bounds);
			}
			else
			{
				using SolidBrush brush2 = new SolidBrush(Color.White);
				graphics.FillRectangle(brush2, e.Bounds);
			}
			string itemText = GetItemText(base.Items[e.Index]);
			Rectangle rectangle = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
			using (SolidBrush brush3 = new SolidBrush(textColor))
			{
				StringFormat format = new StringFormat
				{
					LineAlignment = StringAlignment.Center,
					Alignment = StringAlignment.Near
				};
				graphics.DrawString(itemText, e.Font ?? Font, brush3, rectangle, format);
			}
			if ((e.State & DrawItemState.Focus) != DrawItemState.Focus)
			{
				return;
			}
			using Pen pen = new Pen(borderColor, 1f);
			pen.DashStyle = DashStyle.Dot;
			Rectangle rect = new Rectangle(e.Bounds.X + 1, e.Bounds.Y + 1, e.Bounds.Width - 2, e.Bounds.Height - 2);
			graphics.DrawRectangle(pen, rect);
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			base.OnMouseEnter(e);
			Invalidate();
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			Invalidate();
		}
	}
	public class CustomMenuColorTable : ProfessionalColorTable
	{
		public override Color MenuItemSelectedGradientBegin => Color.FromArgb(25, 20, 201, 187);

		public override Color MenuItemSelectedGradientEnd => Color.FromArgb(25, 20, 201, 187);

		public override Color MenuItemBorder => Color.Transparent;
	}
	public class CustomMenuRenderer : ToolStripProfessionalRenderer
	{
		public CustomMenuRenderer()
			: base(new CustomMenuColorTable())
		{
		}

		protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
		{
			if (e.Item.Selected)
			{
				e.TextColor = ColorTranslator.FromHtml("#14C9BB");
			}
			else
			{
				e.TextColor = e.Item.ForeColor;
			}
			base.OnRenderItemText(e);
		}
	}
}
