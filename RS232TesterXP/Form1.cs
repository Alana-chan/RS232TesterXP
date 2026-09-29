using System;
using System.Drawing;
using System.IO.Ports;
using System.Text;
using System.Windows.Forms;

namespace RS232TesterXP
{
    public partial class Form1 : Form
    {
        private SerialPort port = new SerialPort();

        private ComboBox cmbPort = new ComboBox();
        private ComboBox cmbBaud = new ComboBox();

        private Button btnRefresh = new Button();
        private Button btnConnect = new Button();

        private Button btnDTR = new Button();
        private Button btnRTS = new Button();
        private Button btnBreak = new Button();

        private Label ledCTS = new Label();
        private Label ledDSR = new Label();
        private Label ledDCD = new Label();
        private Label ledRI = new Label();

        private TextBox txtRX = new TextBox();
        private TextBox txtTX = new TextBox();

        private Button btnSend = new Button();
        private Button btnClear = new Button();

        private RadioButton rbTxAscii = new RadioButton();
        private RadioButton rbTxHex = new RadioButton();
        private RadioButton rbRxAscii = new RadioButton();
        private RadioButton rbRxHex = new RadioButton();

        private CheckBox chkCR = new CheckBox();
        private CheckBox chkLF = new CheckBox();

        private Label lblStatus = new Label();
        private Label lblCounters = new Label();

        private Timer inputTimer = new Timer();
        private Timer riTimer = new Timer();

        private bool dtrState = false;
        private bool rtsState = false;
        private bool breakState = false;

        private long txCount = 0;
        private long rxCount = 0;

        public Form1()
        {
            InitializeComponent();

            BuildGui();
            RefreshPorts();

            port.DataReceived +=
                new SerialDataReceivedEventHandler(Port_DataReceived);

            port.PinChanged +=
                new SerialPinChangedEventHandler(Port_PinChanged);

            inputTimer.Interval = 50;
            inputTimer.Tick += new EventHandler(InputTimer_Tick);
            inputTimer.Start();

            // RI jest zdarzeniem, więc lampka świeci tylko chwilę.
            riTimer.Interval = 300;
            riTimer.Tick += new EventHandler(RiTimer_Tick);

            this.FormClosing +=
                new FormClosingEventHandler(Form1_FormClosing);
        }

        // ============================================================
        // GUI
        // ============================================================

        private void BuildGui()
        {
            this.Text = "RS-232 Tester";
            this.ClientSize = new Size(720, 620);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // --------------------------------------------------------
            // PORT
            // --------------------------------------------------------

            GroupBox grpPort = new GroupBox();
            grpPort.Text = "PORT";
            grpPort.SetBounds(15, 10, 690, 70);
            this.Controls.Add(grpPort);

            Label lblPort = MakeLabel("COM:", 15, 29);
            grpPort.Controls.Add(lblPort);

            cmbPort.SetBounds(55, 25, 90, 24);
            cmbPort.DropDownStyle = ComboBoxStyle.DropDownList;
            grpPort.Controls.Add(cmbPort);

            btnRefresh.Text = "Refresh";
            btnRefresh.SetBounds(150, 24, 70, 26);
            btnRefresh.Click += new EventHandler(BtnRefresh_Click);
            grpPort.Controls.Add(btnRefresh);

            Label lblBaud = MakeLabel("Baud:", 240, 29);
            grpPort.Controls.Add(lblBaud);

            cmbBaud.SetBounds(285, 25, 100, 24);

            // DropDown zamiast DropDownList:
            // można wpisać własny baud rate.
            cmbBaud.DropDownStyle = ComboBoxStyle.DropDown;

            cmbBaud.Items.Add("110");
            cmbBaud.Items.Add("300");
            cmbBaud.Items.Add("600");
            cmbBaud.Items.Add("1200");
            cmbBaud.Items.Add("2400");
            cmbBaud.Items.Add("4800");
            cmbBaud.Items.Add("9600");
            cmbBaud.Items.Add("19200");
            cmbBaud.Items.Add("38400");
            cmbBaud.Items.Add("57600");
            cmbBaud.Items.Add("115200");

            cmbBaud.Text = "9600";

            grpPort.Controls.Add(cmbBaud);

            lblStatus.Text = "CLOSED";
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            lblStatus.BorderStyle = BorderStyle.FixedSingle;
            lblStatus.SetBounds(405, 25, 100, 25);
            grpPort.Controls.Add(lblStatus);

            btnConnect.Text = "CONNECT";
            btnConnect.SetBounds(525, 23, 145, 29);
            btnConnect.Click += new EventHandler(BtnConnect_Click);
            grpPort.Controls.Add(btnConnect);

            // --------------------------------------------------------
            // SIGNALS
            // --------------------------------------------------------

            GroupBox grpOutputs = new GroupBox();
            grpOutputs.Text = "OUTPUTS";
            grpOutputs.SetBounds(15, 90, 330, 165);
            this.Controls.Add(grpOutputs);

            AddOutputLabel(grpOutputs, "DTR", "pin 4", 20, 35);
            btnDTR.SetBounds(190, 29, 110, 29);
            btnDTR.Click += new EventHandler(BtnDTR_Click);
            grpOutputs.Controls.Add(btnDTR);

            AddOutputLabel(grpOutputs, "RTS", "pin 7", 20, 75);
            btnRTS.SetBounds(190, 69, 110, 29);
            btnRTS.Click += new EventHandler(BtnRTS_Click);
            grpOutputs.Controls.Add(btnRTS);

            AddOutputLabel(grpOutputs, "TXD BREAK", "pin 3", 20, 115);
            btnBreak.SetBounds(190, 109, 110, 29);
            btnBreak.Click += new EventHandler(BtnBreak_Click);
            grpOutputs.Controls.Add(btnBreak);

            UpdateOutputButtons();

            GroupBox grpInputs = new GroupBox();
            grpInputs.Text = "INPUTS / EVENTS";
            grpInputs.SetBounds(360, 90, 345, 165);
            this.Controls.Add(grpInputs);

            CreateInput(
                grpInputs,
                ledCTS,
                "CTS",
                "pin 8",
                20,
                35);

            CreateInput(
                grpInputs,
                ledDSR,
                "DSR",
                "pin 6",
                20,
                70);

            CreateInput(
                grpInputs,
                ledDCD,
                "DCD",
                "pin 1",
                20,
                105);

            // RI celowo jest po prawej i opisany EVENT.
            ledRI.Text = "●";
            ledRI.Font = new Font("Arial", 18, FontStyle.Bold);
            ledRI.ForeColor = Color.Gray;
            ledRI.SetBounds(185, 34, 30, 30);
            grpInputs.Controls.Add(ledRI);

            Label lblRI = MakeLabel("RI EVENT", 220, 40);
            lblRI.Font = new Font(
                lblRI.Font,
                FontStyle.Bold);

            grpInputs.Controls.Add(lblRI);

            Label lblRIPin = MakeLabel("pin 9", 220, 60);
            lblRIPin.ForeColor = Color.DimGray;
            grpInputs.Controls.Add(lblRIPin);

            Label lblRIInfo =
                MakeLabel("300 ms pulse", 185, 92);

            lblRIInfo.ForeColor = Color.DimGray;
            grpInputs.Controls.Add(lblRIInfo);

            // --------------------------------------------------------
            // TERMINAL
            // --------------------------------------------------------

            GroupBox grpTerminal = new GroupBox();
            grpTerminal.Text = "TERMINAL";
            grpTerminal.SetBounds(15, 265, 690, 300);
            this.Controls.Add(grpTerminal);

            Label lblRX = MakeLabel("RX:", 15, 28);
            lblRX.Font = new Font(
                lblRX.Font,
                FontStyle.Bold);

            grpTerminal.Controls.Add(lblRX);

            rbRxAscii.Text = "ASCII";
            rbRxAscii.SetBounds(55, 24, 60, 24);
            rbRxAscii.Checked = true;
            grpTerminal.Controls.Add(rbRxAscii);

            rbRxHex.Text = "HEX";
            rbRxHex.SetBounds(120, 24, 55, 24);
            grpTerminal.Controls.Add(rbRxHex);

            btnClear.Text = "CLEAR RX";
            btnClear.SetBounds(575, 22, 90, 27);
            btnClear.Click += new EventHandler(BtnClear_Click);
            grpTerminal.Controls.Add(btnClear);

            txtRX.SetBounds(15, 55, 650, 135);
            txtRX.Multiline = true;
            txtRX.ScrollBars = ScrollBars.Vertical;
            txtRX.ReadOnly = true;
            txtRX.BackColor = Color.White;
            txtRX.Font =
                new Font(FontFamily.GenericMonospace, 9);

            grpTerminal.Controls.Add(txtRX);

            Label lblTX = MakeLabel("TX:", 15, 211);
            lblTX.Font = new Font(
                lblTX.Font,
                FontStyle.Bold);

            grpTerminal.Controls.Add(lblTX);

            txtTX.SetBounds(45, 207, 475, 24);
            txtTX.Font =
                new Font(FontFamily.GenericMonospace, 9);

            txtTX.KeyDown +=
                new KeyEventHandler(TxtTX_KeyDown);

            grpTerminal.Controls.Add(txtTX);

            btnSend.Text = "SEND";
            btnSend.SetBounds(535, 205, 130, 28);
            btnSend.Click += new EventHandler(BtnSend_Click);
            grpTerminal.Controls.Add(btnSend);

            rbTxAscii.Text = "ASCII";
            rbTxAscii.SetBounds(45, 243, 60, 24);
            rbTxAscii.Checked = true;
            grpTerminal.Controls.Add(rbTxAscii);

            rbTxHex.Text = "HEX";
            rbTxHex.SetBounds(110, 243, 55, 24);
            grpTerminal.Controls.Add(rbTxHex);

            chkCR.Text = "CR";
            chkCR.SetBounds(190, 243, 50, 24);
            grpTerminal.Controls.Add(chkCR);

            chkLF.Text = "LF";
            chkLF.SetBounds(245, 243, 50, 24);
            grpTerminal.Controls.Add(chkLF);

            Label lblEnter =
                MakeLabel("Enter = SEND", 330, 247);

            lblEnter.ForeColor = Color.DimGray;
            grpTerminal.Controls.Add(lblEnter);

            // --------------------------------------------------------
            // BOTTOM STATUS
            // --------------------------------------------------------

            lblCounters.Text = "TX: 0 B     RX: 0 B";
            lblCounters.SetBounds(20, 580, 300, 20);
            this.Controls.Add(lblCounters);

            Label lblFormat =
                MakeLabel("8 data bits / no parity / 1 stop bit", 430, 580);

            lblFormat.ForeColor = Color.DimGray;
            this.Controls.Add(lblFormat);
        }

        private Label MakeLabel(string text, int x, int y)
        {
            Label l = new Label();

            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);

            return l;
        }

        private void AddOutputLabel(
            Control parent,
            string name,
            string pin,
            int x,
            int y)
        {
            Label l1 = MakeLabel(name, x, y);
            l1.Font = new Font(l1.Font, FontStyle.Bold);
            parent.Controls.Add(l1);

            Label l2 = MakeLabel(pin, x + 90, y);
            l2.ForeColor = Color.DimGray;
            parent.Controls.Add(l2);
        }

        private void CreateInput(
            Control parent,
            Label led,
            string name,
            string pin,
            int x,
            int y)
        {
            led.Text = "●";
            led.Font = new Font(
                "Arial",
                18,
                FontStyle.Bold);

            led.ForeColor = Color.Gray;
            led.SetBounds(x, y - 8, 30, 30);

            parent.Controls.Add(led);

            Label l1 = MakeLabel(name, x + 38, y);
            l1.Font = new Font(l1.Font, FontStyle.Bold);
            parent.Controls.Add(l1);

            Label l2 = MakeLabel(pin, x + 95, y);
            l2.ForeColor = Color.DimGray;
            parent.Controls.Add(l2);
        }

        // ============================================================
        // PORTS
        // ============================================================

        private void BtnRefresh_Click(object sender, EventArgs e)
        {
            RefreshPorts();
        }

        private void RefreshPorts()
        {
            string previous = cmbPort.Text;

            cmbPort.Items.Clear();

            string[] names = SerialPort.GetPortNames();

            Array.Sort(names);

            int i;

            for (i = 0; i < names.Length; i++)
                cmbPort.Items.Add(names[i]);

            if (cmbPort.Items.Contains(previous))
            {
                cmbPort.SelectedItem = previous;
            }
            else if (cmbPort.Items.Count > 0)
            {
                cmbPort.SelectedIndex = 0;
            }
        }

        // ============================================================
        // CONNECT / DISCONNECT
        // ============================================================

        private void BtnConnect_Click(object sender, EventArgs e)
        {
            if (port.IsOpen)
            {
                ClosePort();
                return;
            }

            if (cmbPort.Text.Length == 0)
            {
                MessageBox.Show(
                    "No COM port selected.",
                    "RS-232 Tester",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            int baud;

            if (!Int32.TryParse(cmbBaud.Text, out baud) ||
                baud <= 0)
            {
                MessageBox.Show(
                    "Invalid baud rate.",
                    "RS-232 Tester",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            try
            {
                port.PortName = cmbPort.Text;
                port.BaudRate = baud;
                port.DataBits = 8;
                port.Parity = Parity.None;
                port.StopBits = StopBits.One;
                port.Handshake = Handshake.None;

                port.DtrEnable = dtrState;
                port.RtsEnable = rtsState;

                port.Open();

                port.BreakState = breakState;

                cmbPort.Enabled = false;
                cmbBaud.Enabled = false;
                btnRefresh.Enabled = false;

                btnConnect.Text = "DISCONNECT";

                lblStatus.Text = "CONNECTED";
                lblStatus.ForeColor = Color.DarkGreen;

                txtTX.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Cannot open port",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void ClosePort()
        {
            try
            {
                if (port.IsOpen)
                {
                    port.BreakState = false;
                    port.DtrEnable = false;
                    port.RtsEnable = false;

                    port.Close();
                }
            }
            catch
            {
            }

            cmbPort.Enabled = true;
            cmbBaud.Enabled = true;
            btnRefresh.Enabled = true;

            btnConnect.Text = "CONNECT";

            lblStatus.Text = "CLOSED";
            lblStatus.ForeColor = SystemColors.ControlText;

            SetInputLed(ledCTS, false);
            SetInputLed(ledDSR, false);
            SetInputLed(ledDCD, false);

            ledRI.ForeColor = Color.Gray;
            riTimer.Stop();
        }

        // ============================================================
        // OUTPUTS
        // ============================================================

        private void BtnDTR_Click(object sender, EventArgs e)
        {
            dtrState = !dtrState;

            try
            {
                if (port.IsOpen)
                    port.DtrEnable = dtrState;
            }
            catch
            {
            }

            UpdateOutputButtons();
        }

        private void BtnRTS_Click(object sender, EventArgs e)
        {
            rtsState = !rtsState;

            try
            {
                if (port.IsOpen)
                    port.RtsEnable = rtsState;
            }
            catch
            {
            }

            UpdateOutputButtons();
        }

        private void BtnBreak_Click(object sender, EventArgs e)
        {
            breakState = !breakState;

            try
            {
                if (port.IsOpen)
                    port.BreakState = breakState;
            }
            catch
            {
            }

            UpdateOutputButtons();
        }

        private void UpdateOutputButtons()
        {
            SetOutputButton(btnDTR, dtrState);
            SetOutputButton(btnRTS, rtsState);
            SetOutputButton(btnBreak, breakState);
        }

        private void SetOutputButton(Button button, bool state)
        {
            if (state)
            {
                button.Text = "ON";
                button.BackColor = Color.LightGreen;
            }
            else
            {
                button.Text = "OFF";
                button.BackColor = SystemColors.Control;
            }
        }

        // ============================================================
        // INPUTS
        // ============================================================

        private void InputTimer_Tick(object sender, EventArgs e)
        {
            if (!port.IsOpen)
                return;

            try
            {
                SetInputLed(
                    ledCTS,
                    port.CtsHolding);

                SetInputLed(
                    ledDSR,
                    port.DsrHolding);

                SetInputLed(
                    ledDCD,
                    port.CDHolding);
            }
            catch
            {
            }
        }

        private void SetInputLed(Label led, bool state)
        {
            if (state)
                led.ForeColor = Color.LimeGreen;
            else
                led.ForeColor = Color.Gray;
        }

        // ============================================================
        // RI EVENT
        // ============================================================

        private void Port_PinChanged(
            object sender,
            SerialPinChangedEventArgs e)
        {
            if (e.EventType != SerialPinChange.Ring)
                return;

            try
            {
                this.BeginInvoke(
                    new MethodInvoker(ShowRiEvent));
            }
            catch
            {
            }
        }

        private void ShowRiEvent()
        {
            // RI nie jest przedstawiany jako stan.
            // Każde zdarzenie Ring daje bursztynowy błysk.

            ledRI.ForeColor = Color.Orange;

            riTimer.Stop();
            riTimer.Start();
        }

        private void RiTimer_Tick(object sender, EventArgs e)
        {
            riTimer.Stop();
            ledRI.ForeColor = Color.Gray;
        }

        // ============================================================
        // TX
        // ============================================================

        private void BtnSend_Click(object sender, EventArgs e)
        {
            SendData();
        }

        private void TxtTX_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendData();
            }
        }

        private void SendData()
        {
            if (!port.IsOpen)
            {
                MessageBox.Show(
                    "Port is closed.",
                    "RS-232 Tester",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            if (breakState)
            {
                MessageBox.Show(
                    "TXD BREAK is active.",
                    "RS-232 Tester",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            try
            {
                byte[] data;

                if (rbTxHex.Checked)
                {
                    data = ParseHex(txtTX.Text);
                }
                else
                {
                    string text = txtTX.Text;

                    if (chkCR.Checked)
                        text += "\r";

                    if (chkLF.Checked)
                        text += "\n";

                    data = Encoding.Default.GetBytes(text);
                }

                if (data.Length == 0)
                    return;

                port.Write(
                    data,
                    0,
                    data.Length);

                txCount += data.Length;

                UpdateCounters();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "TX error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private byte[] ParseHex(string text)
        {
            string cleaned = text;

            cleaned = cleaned.Replace("-", " ");
            cleaned = cleaned.Replace(",", " ");
            cleaned = cleaned.Replace(";", " ");

            string[] parts = cleaned.Split(
                new char[]
                {
                    ' ',
                    '\t',
                    '\r',
                    '\n'
                },
                StringSplitOptions.RemoveEmptyEntries);

            byte[] data = new byte[parts.Length];

            int i;

            for (i = 0; i < parts.Length; i++)
            {
                string p = parts[i];

                if (p.StartsWith("0x") ||
                    p.StartsWith("0X"))
                {
                    p = p.Substring(2);
                }

                data[i] =
                    Convert.ToByte(p, 16);
            }

            return data;
        }

        // ============================================================
        // RX
        // ============================================================

        private void Port_DataReceived(
            object sender,
            SerialDataReceivedEventArgs e)
        {
            try
            {
                int count = port.BytesToRead;

                if (count <= 0)
                    return;

                byte[] buffer = new byte[count];

                int read =
                    port.Read(
                        buffer,
                        0,
                        buffer.Length);

                if (read <= 0)
                    return;

                byte[] actual =
                    new byte[read];

                Array.Copy(
                    buffer,
                    actual,
                    read);

                rxCount += read;

                this.BeginInvoke(
                    new ReceivedDataDelegate(
                        DisplayReceivedData),
                    new object[] { actual });
            }
            catch
            {
            }
        }

        private delegate void ReceivedDataDelegate(
            byte[] data);

        private void DisplayReceivedData(byte[] data)
        {
            if (rbRxHex.Checked)
            {
                StringBuilder sb =
                    new StringBuilder();

                int i;

                for (i = 0; i < data.Length; i++)
                {
                    sb.Append(
                        data[i].ToString("X2"));

                    sb.Append(" ");
                }

                txtRX.AppendText(
                    sb.ToString());
            }
            else
            {
                txtRX.AppendText(
                    Encoding.Default.GetString(data));
            }

            txtRX.SelectionStart =
                txtRX.TextLength;

            txtRX.ScrollToCaret();

            UpdateCounters();
        }

        // ============================================================
        // COUNTERS / CLEAR
        // ============================================================

        private void BtnClear_Click(object sender, EventArgs e)
        {
            txtRX.Clear();

            rxCount = 0;

            UpdateCounters();
        }

        private void UpdateCounters()
        {
            lblCounters.Text =
                "TX: " +
                txCount.ToString() +
                " B     RX: " +
                rxCount.ToString() +
                " B";
        }

        // ============================================================
        // EXIT
        // ============================================================

        private void Form1_FormClosing(
            object sender,
            FormClosingEventArgs e)
        {
            inputTimer.Stop();
            riTimer.Stop();

            try
            {
                if (port.IsOpen)
                {
                    port.BreakState = false;
                    port.DtrEnable = false;
                    port.RtsEnable = false;

                    port.Close();
                }
            }
            catch
            {
            }

            port.Dispose();
        }
    }
}