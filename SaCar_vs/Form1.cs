using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sunny.UI;

namespace SaCar_vs
{
    public partial class Form1 : UIForm
    {
        private List<byte> buffer = new List<byte>(); //设置缓存处理CRC32串口的校验
        public static bool intimewindowIsOpen = false; //判断波形窗口是否创建
        List<byte> CheckedData = new List<byte>();//申请一个大容量的数组
        //private List<byte> SerialPortReceiveData = new List<byte>(); //用于存储串口的数据
        int start = 0;//充当指针的作用
        double[] numbers = new double[10];//测试用
        int chartP = 0;//充当绘制用的指针
        private const int RectWidth = 5;//矩形宽
        private const int RectMargin = 0;//间距
        double[] thresholds = new double[10];//十通道阈值
        /*double threshold = 0;//阈值*/
        private Thread dataThread;//创建一个新线程
        private ManualResetEvent _stopThreadEvent = new ManualResetEvent(false);//用于标定线程状态
        private int framesReceived = 0;
        private calibration calibrationWindow;//自定义校验窗口
        private double precision = 10.0;//校准精度




        #region 判断串口是否插入
        public bool search_port_is_exist(String item, String[] port_list)
        {
            for (int i = 0; i < port_list.Length; i++)
            {
                if (port_list[i].Equals(item))
                {
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region 扫描串口列表并添加到选择框
        private void Update_Serial_List()
        {
            try
            {
                /* 搜索串口 */
                String[] cur_port_list = System.IO.Ports.SerialPort.GetPortNames();

                /* 刷新串口列表comboBox */
                int count = uiComboBox1.Items.Count;
                if (count == 0)
                {
                    //combox中无内容，将当前串口列表全部加入
                    uiComboBox1.Items.AddRange(cur_port_list);
                    return;
                }
                else
                {
                    //combox中有内容

                    //判断有无新插入的串口
                    for (int i = 0; i < cur_port_list.Length; i++)
                    {
                        if (!uiComboBox1.Items.Contains(cur_port_list[i]))
                        {
                            //找到新插入串口，添加到combox中
                            uiComboBox1.Items.Add(cur_port_list[i]);
                        }
                    }

                    //判断有无拔掉的串口
                    for (int i = 0; i < count; i++)
                    {
                        if (!search_port_is_exist(uiComboBox1.Items[i].ToString(), cur_port_list))
                        {
                            //找到已被拔掉的串口，从combox中移除
                            uiComboBox1.Items.RemoveAt(i);
                        }
                    }
                }

                /* 如果当前选中项为空，则默认选择第一项 */
                if (uiComboBox1.Items.Count > 0)
                {
                    if (uiComboBox1.Text.Equals(""))
                    {
                        //软件刚启动时，列表项的文本值为空
                        uiComboBox1.Text = uiComboBox1.Items[0].ToString();

                    }
                }
                else
                {
                    //无可用列表，清空文本值
                    uiComboBox1.Text = "";
                }


            }
            catch (Exception)
            {
                //当下拉框被打开时，修改下拉框会发生异常
                return;
            }
        }
        #endregion

        #region 串口接收数据
        private void serialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            /* 串口接收事件处理 */



            int num = serialPort1.BytesToRead;      //获取接收缓冲区中的字节数
            if (num == 0)
            {
                return;
            }
            byte[] received_buf = new byte[num];    //声明一个大小为num的字节数据用于存放读出的byte型数据

            serialPort1.Read(received_buf, 0, num);   //读取接收缓冲区中num个字节到byte数组中

            #region 数据校验
            buffer.AddRange(received_buf); //缓存数据

            // resize arr
            int count = buffer.Count;

            int bytesIgnored = 0;

            while (start + 44 <= count)
            {
                // head, tail
                if (buffer[start] != 0xAA || buffer[start + 1] != 0x29 || buffer[start + 43] != 0x80)
                {
                    start++;
                    bytesIgnored++;
                    continue;
                }

                // CRC8: from  start + 2  to start + 41, check by start + 42
                if (CRC8(buffer, start + 2, 40) != buffer[start + 42])
                {
                    start++;
                    bytesIgnored++;
                    continue;
                }

                // append data
                {
                    // copy 40 bytes from start
                    for (int i = start + 2; i < start + 42; i++)
                    {
                        CheckedData.Add(buffer[i]);
                    }

                }

                start += 44;
                framesReceived++;
            }

            if (bytesIgnored > 0)
            {
                DateTime dt = DateTime.Now;
                Console.WriteLine("{0}:{1}:{2}.{3} {4} frames received, {5} bytes ignored",
                    dt.Hour, dt.Minute, dt.Second, dt.Millisecond, framesReceived, bytesIgnored);
                framesReceived = 0;
            }

            if ( start < count )
            {
                List<byte> buf = new List<byte>();
                buf.AddRange(buffer.GetRange(start, count - start));
                buffer = buf;
            }
            else
            {
                buffer.Clear();
            }
            start = 0;

            #endregion

        }
        #endregion

        #region CRC8校验函数
        public static byte CRC8(List<byte> buffer, int start, int length)
        {
            byte crc = 0;// Initial value

            for (int j = start; j < start + length; j++)
            {
                crc ^= buffer[j];
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x80) != 0)
                    {
                        crc <<= 1;
                        crc ^= 0x07;
                    }
                    else
                    {
                        crc <<= 1;
                    }
                }
            }
            return crc;
        }


        #endregion

        public Form1()
        {
            InitializeComponent();

            


            DoubleBuffered = true; // enable double buffering
        }

        #region 防止闪屏
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;
                return cp;
            }
        }
        #endregion


        private void timer1_Tick(object sender, EventArgs e)
        {
            Update_Serial_List();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            /* 添加串口选择列表 */
            Update_Serial_List();

            /* 在串口未打开的情况下每隔1s刷新一次串口列表框 */
            timer1.Interval = 1000;
            timer1.Start();


            uiButton2.Enabled = false;
            uiButton1.Enabled = true;
            uiButton3.Enabled = false;

            AddRectToPanel(mypanel1);
            AddRectToPanel(mypanel2);
            AddRectToPanel(mypanel3);
            AddRectToPanel(mypanel4);
            AddRectToPanel(mypanel5);
            AddRectToPanel(mypanel6);
            AddRectToPanel(mypanel7);
            AddRectToPanel(mypanel8);
            AddRectToPanel(mypanel9);
            AddRectToPanel(mypanel10);

            for (int i = 0; i < 10; i++)
            {
                thresholds[i] = 0;
            }

           


            this.DoubleBuffered = true;

        }


        private void uiButton1_Click(object sender, EventArgs e)
        {
            
 
            try
            {
                //将可能产生异常的代码放置在try块中
                //根据当前串口属性来判断是否打开
                if (serialPort1.IsOpen)
                {
                    //串口已经处于打开状态
                    uiButton2.Enabled = true;
                    uiButton1.Enabled = false;

                }
                else
                {

                    if (!uiComboBox1.Text.Equals(""))
                    {
                        /* 串口已经处于关闭状态，则设置好串口属性后打开 */
                        //停止串口扫描
                        timer1.Stop();
                        StartThread();

                        uiComboBox1.Enabled = false;
                        serialPort1.PortName = uiComboBox1.Text;
                        serialPort1.BaudRate = Convert.ToInt32("115200");
                        serialPort1.DataBits = Convert.ToInt16("8");
                        serialPort1.Parity = System.IO.Ports.Parity.None;
                        serialPort1.StopBits = System.IO.Ports.StopBits.One;
                        //打开串口，设置状态
                        serialPort1.Open();
                        uiButton2.Enabled = true;
                        uiButton1.Enabled = false;
                        uiButton3.Enabled = true;
                    }
                    else
                    {
                        MessageBox.Show("请等待串口连接");
                    }

                    

                }
            }
            catch (Exception ex)
            {
                //捕获可能发生的异常并进行处理

                //捕获到异常，创建一个新的对象，之前的不可以再用  
                serialPort1 = new System.IO.Ports.SerialPort(components);
                serialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(serialPort1_DataReceived);

                //响铃并显示异常给用户
                System.Media.SystemSounds.Beep.Play();
                MessageBox.Show(ex.Message);
                uiComboBox1.Enabled = true;
            }


            
        }

        private void uiButton2_Click(object sender, EventArgs e)
        {
            
            try
            {
                //将可能产生异常的代码放置在try块中
                //根据当前串口属性来判断是否打开
                if (serialPort1.IsOpen)
                {
                    //串口已经处于打开状态
                    //串口已经处于打开状态
                   /* uiTextBox1.Enabled = true;*/

                    serialPort1.Close();    //关闭串口
                    uiComboBox1.Enabled = true;
                    uiButton2.Enabled = false;
                    uiButton1.Enabled = true;
                    uiButton3.Enabled = false;
                    CheckedData.Clear();
                    start = 0;
                    //开启端口扫描
                    timer1.Interval = 1000;
                    timer1.Start();
                    StopThread();
                }
                else
                {
                    /* 串口已经处于关闭状态，则设置好串口属性后打开 */
                    //停止串口扫描
                    uiButton2.Enabled = false;
                    uiButton1.Enabled = true;

                }
            }
            catch (Exception ex)
            {
                //捕获可能发生的异常并进行处理

                //捕获到异常，创建一个新的对象，之前的不可以再用  
                serialPort1 = new System.IO.Ports.SerialPort(components);
                serialPort1.DataReceived += new System.IO.Ports.SerialDataReceivedEventHandler(serialPort1_DataReceived);

                //响铃并显示异常给用户
                System.Media.SystemSounds.Beep.Play();
                MessageBox.Show(ex.Message);
            }
        }


        private void DataThreadMethod()
        {
            long msRefresh = 0;
            while (!_stopThreadEvent.WaitOne(0))
            {
                if (CheckedData.Count() < chartP + 40)
                {
                    continue;
                }
                
                for (int i = 0; i < 10; i++)
                {
                    numbers[i] = ToData(CheckedData[chartP], CheckedData[chartP + 1]);
                    chartP += 4;
                }

                bool bRefresh = false;
                long ms = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                if ( Math.Abs(ms - msRefresh) > 100)
                {
                    msRefresh = ms;
                    bRefresh = true;
                }

                BeginInvoke(new Action(() =>
                {

                    UpPanel(mypanel1, numbers[0], thresholds[0], bRefresh);
                    UpPanel(mypanel2, numbers[1], thresholds[1], bRefresh);
                    UpPanel(mypanel3, numbers[2], thresholds[2], bRefresh);
                    UpPanel(mypanel4, numbers[3], thresholds[3], bRefresh);
                    UpPanel(mypanel5, numbers[4], thresholds[4], bRefresh);
                    UpPanel(mypanel6, numbers[5], thresholds[5], bRefresh);
                    UpPanel(mypanel7, numbers[6], thresholds[6], bRefresh);
                    UpPanel(mypanel8, numbers[7], thresholds[7], bRefresh);
                    UpPanel(mypanel9, numbers[8], thresholds[8], bRefresh);
                    UpPanel(mypanel10, numbers[9], thresholds[9], bRefresh);

                    if ( bRefresh )
                    {
                        uiLabel1.Text = (numbers[0] - thresholds[0]).ToString("F1");
                        uiLabel2.Text = (numbers[1] - thresholds[1]).ToString("F1");
                        uiLabel3.Text = (numbers[2] - thresholds[2]).ToString("F1");
                        uiLabel4.Text = (numbers[3] - thresholds[3]).ToString("F1");
                        uiLabel5.Text = (numbers[4] - thresholds[4]).ToString("F1");
                        uiLabel6.Text = (numbers[5] - thresholds[5]).ToString("F1");
                        uiLabel7.Text = (numbers[6] - thresholds[6]).ToString("F1");
                        uiLabel8.Text = (numbers[7] - thresholds[7]).ToString("F1");
                        uiLabel9.Text = (numbers[8] - thresholds[8]).ToString("F1");
                        uiLabel10.Text = (numbers[9] - thresholds[9]).ToString("F1");
                    }

                }));             
            }
            Thread.Sleep(100);

        }


        private double ToData(byte lowByte, byte highByte)
        {
            short rawValue = (short)((highByte << 8) | lowByte); // convert to signed short

            double signedValue = Convert.ToDouble(rawValue); // convert to signed double

            return signedValue;
        }

        private void AddRectToPanel(Mypanel panel)
        {
            Rectangle[] rectangles = new Rectangle[panel.Width / (RectWidth + RectMargin)];
            bool[] states = new bool[rectangles.Length];
            int[] redValues = new int[rectangles.Length];
            for (int j = 0; j < rectangles.Length; j++)
            {
                int x = j * (RectWidth + RectMargin);
                int y = 0;
                rectangles[j] = new Rectangle(x, y, RectWidth, panel.Height);
                states[j] = false; // Default state is blue
                redValues[j] = 0; // Default red value is 0
            }

            panel.Paint += (sender, e) =>
            {
                Graphics graphics = e.Graphics;

                for (int j = 0; j < rectangles.Length; j++)
                {
                    Rectangle rect = rectangles[j];

                    // Fill the rectangle with the appropriate color
                    Color color = states[j] ? Color.FromArgb(redValues[j], 0, 0): Color.FromArgb(80, 160, 255);
                    SolidBrush brush = new SolidBrush(color);
                    graphics.FillRectangle(brush, rect);

                    // Draw a border around the rectangle
                    ControlPaint.DrawBorder(graphics, rect, Color.Transparent, ButtonBorderStyle.None);
                }
            };

            panel.Tag = new Tuple<Rectangle[], bool[], int[]>(rectangles, states, redValues);
           /* panel.Tag = new Tuple<Rectangle[], bool[]>(rectangles, states);*/
        }

        private void UpPanel(Mypanel panel, double number, double threshold, bool bRefresh)
        {
            Tuple<Rectangle[], bool[], int[]> data = (Tuple<Rectangle[], bool[], int[]>)panel.Tag;
            /*Tuple<Rectangle[], bool[]> data = (Tuple<Rectangle[], bool[]>)panel.Tag;
            Rectangle[] rectangles = data.Item1;
            bool[] states = data.Item2;*/
            Rectangle[] rectangles = data.Item1;
            bool[] states = data.Item2;
            int[] redValues = data.Item3;


            // Move the states of the other rectangles one position to the left
            for (int j = 1; j < rectangles.Length; j++)
            {
                states[j - 1] = states[j];
                redValues[j - 1] = redValues[j];
            }

            /*// Set the rightmost rectangle's state based on the random number
            states[rectangles.Length - 1] = number > threshold + precision;*/

            // Calculate the distance from the threshold
            double distance = Math.Abs(number - threshold);

            // Set the rightmost rectangle's state based on the distance from the threshold
            if (distance <= precision)
            {
                // The number is within the precision range of the threshold, so color the rectangle red
                states[rectangles.Length - 1] = false;
                redValues[rectangles.Length - 1] = 0;
            }
            else
            {
                // The number is outside the precision range of the threshold, so adjust the color of the red based on the distance
                int redValue = Math.Max((int)(255 * (1 - (distance - precision) / (1 - threshold + precision))), 0);
                states[rectangles.Length - 1] = true;
                redValues[rectangles.Length - 1] = redValue;
            }


            if ( bRefresh )
            {
                panel.Invalidate();
            }
        }

        private void StartThread()
        {
  
            _stopThreadEvent.Reset();
            dataThread = new Thread(DataThreadMethod);
            dataThread.IsBackground = true;
            dataThread.Start();


        }

        private void StopThread()
        {
   
            _stopThreadEvent.Set();
            if (dataThread != null)
            {
                if (!dataThread.Join(500))
                {
                    dataThread.Abort();
                }
                dataThread = null;
            }
        }

        private void uiButton3_Click(object sender, EventArgs e)
        {
            if (!uiLabel1.Text.Equals(""))
            {
                thresholds[0] = Convert.ToDouble(uiLabel1.Text);
                thresholds[1] = Convert.ToDouble(uiLabel2.Text);
                thresholds[2] = Convert.ToDouble(uiLabel3.Text);
                thresholds[3] = Convert.ToDouble(uiLabel4.Text);
                thresholds[4] = Convert.ToDouble(uiLabel5.Text);
                thresholds[5] = Convert.ToDouble(uiLabel6.Text);
                thresholds[6] = Convert.ToDouble(uiLabel7.Text);
                thresholds[7] = Convert.ToDouble(uiLabel8.Text);
                thresholds[8] = Convert.ToDouble(uiLabel9.Text);
                thresholds[9] = Convert.ToDouble(uiLabel10.Text);
            }
            else
            {
                MessageBox.Show("未收到串口数据，请检查串口是否选择正确");
            }
            
        }

        private void uiButton5_Click(object sender, EventArgs e)
        {
            calibrationWindow = new calibration(numbers);//自定义校验窗口
            calibrationWindow.WindowClosed += MyNewWindow_WindowClosed;
            calibrationWindow.Show();
      

        }

        private void MyNewWindow_WindowClosed(object sender, double[] e)
        {
            for (int i = 0; i < 10; i++)
            {
                if (e[i] != 0.0)
                {
                    thresholds[i] = e[i];
                }
            }


            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine(thresholds[i] + "\n");
            }
        }


    }
}
