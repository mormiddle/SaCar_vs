using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
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
        //private List<byte> SaveDate = new List<byte>(); //用于存储串口的数据
        int start = 0;//充当指针的作用
        double[] numbers = new double[10];//测试用
        double[] numbers_x = new double[10];//测试用 虚部
        int chartP = 0;//充当绘制用的指针
        private const int RectWidth = 5;//矩形宽
        private const int RectMargin = 0;//间距
        double[] thresholds = new double[10];//十通道阈值
        double[] thresholds_x = new double[10];//十通道阈值
        double[] HardnessValue = new double[10];
        double[] offsetValue = new double[10]; //偏移值
        double[] k = new double[10];
        double[] d = new double[10];
        double[,] xyArray = new double[10, 4];
        /*double threshold = 0;//阈值*/
        private Thread dataThread;//创建一个新线程
        private ManualResetEvent _stopThreadEvent = new ManualResetEvent(false);//用于标定线程状态
        private int framesReceived = 0;
        private calibration calibrationWindow;//自定义校验窗口
        private double precision;//校准精度
        int Savep = 0;
        DateTime startTime = new DateTime();
        DateTime finishTime = new DateTime();
        bool isHardnessRefresh = false;





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
            uiButton4.Enabled = false;

            /*AddRectToPanel(mypanel1);
            AddRectToPanel(mypanel2);
            AddRectToPanel(mypanel3);
            AddRectToPanel(mypanel4);
            AddRectToPanel(mypanel5);
            AddRectToPanel(mypanel6);
            AddRectToPanel(mypanel7);
            AddRectToPanel(mypanel8);
            AddRectToPanel(mypanel9);
            AddRectToPanel(mypanel10);*/

            for (int i = 0; i < 10; i++)
            {
                thresholds[i] = 0;
                thresholds_x[i] = 0;
                HardnessValue[i] = 0;
                offsetValue[i] = 0;
            }
            /*precision = Convert.ToDouble(uiTextBox1.Text);*/

            this.DoubleBuffered = true;
            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    xyArray[i, j] = 0;
                }
            }

            //kd计算
            uiButton7.Enabled = false;

            //输入框
            uiTextBoxy11.Enabled = false;
            uiTextBoxy12.Enabled = false;
            uiTextBoxy13.Enabled = false;
            uiTextBoxy14.Enabled = false;
            uiTextBoxy15.Enabled = false;
            uiTextBoxy16.Enabled = false;
            uiTextBoxy17.Enabled = false;
            uiTextBoxy18.Enabled = false;
            uiTextBoxy19.Enabled = false;
            uiTextBoxy110.Enabled = false;

            uiTextBoxy21.Enabled = false;
            uiTextBoxy22.Enabled = false;
            uiTextBoxy23.Enabled = false;
            uiTextBoxy24.Enabled = false;
            uiTextBoxy25.Enabled = false;
            uiTextBoxy26.Enabled = false;
            uiTextBoxy27.Enabled = false;
            uiTextBoxy28.Enabled = false;
            uiTextBoxy29.Enabled = false;
            uiTextBoxy210.Enabled = false;

            uiTextBox4.Enabled = false;
            uiTextBox8.Enabled = false;


            uiTextBoxpyz1.Enabled = false;
            uiTextBoxpyz2.Enabled = false;
            uiTextBoxpyz3.Enabled = false;
            uiTextBoxpyz4.Enabled = false;
            uiTextBoxpyz5.Enabled = false;
            uiTextBoxpyz6.Enabled = false;
            uiTextBoxpyz7.Enabled = false;
            uiTextBoxpyz8.Enabled = false;
            uiTextBoxpyz9.Enabled = false;
            uiTextBoxpyz10.Enabled = false;

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
                        //uiButton3.Enabled = true;
                        uiButton4.Enabled = true;
                        uiButton5.Enabled = true;
                        uiButton7.Enabled = true;

                        uiTextBoxy11.Enabled = true;
                        uiTextBoxy12.Enabled = true;
                        uiTextBoxy13.Enabled = true;
                        uiTextBoxy14.Enabled = true;
                        uiTextBoxy15.Enabled = true;
                        uiTextBoxy16.Enabled = true;
                        uiTextBoxy17.Enabled = true;
                        uiTextBoxy18.Enabled = true;
                        uiTextBoxy19.Enabled = true;
                        uiTextBoxy110.Enabled = true;

                        uiTextBoxy21.Enabled = true;
                        uiTextBoxy22.Enabled = true;
                        uiTextBoxy23.Enabled = true;
                        uiTextBoxy24.Enabled = true;
                        uiTextBoxy25.Enabled = true;
                        uiTextBoxy26.Enabled = true;
                        uiTextBoxy27.Enabled = true;
                        uiTextBoxy28.Enabled = true;
                        uiTextBoxy29.Enabled = true;
                        uiTextBoxy210.Enabled = true;

                        uiTextBoxpyz1.Text = "0";
                        uiTextBoxpyz2.Text = "0";
                        uiTextBoxpyz3.Text = "0";
                        uiTextBoxpyz4.Text = "0";
                        uiTextBoxpyz5.Text = "0";
                        uiTextBoxpyz6.Text = "0";
                        uiTextBoxpyz7.Text = "0";
                        uiTextBoxpyz8.Text = "0";
                        uiTextBoxpyz9.Text = "0";
                        uiTextBoxpyz10.Text = "0";

                        uiTextBoxpyz1.Enabled = true;
                        uiTextBoxpyz2.Enabled = true;
                        uiTextBoxpyz3.Enabled = true;
                        uiTextBoxpyz4.Enabled = true;
                        uiTextBoxpyz5.Enabled = true;
                        uiTextBoxpyz6.Enabled = true;
                        uiTextBoxpyz7.Enabled = true;
                        uiTextBoxpyz8.Enabled = true;
                        uiTextBoxpyz9.Enabled = true;
                        uiTextBoxpyz10.Enabled = true;

                        uiTextBox4.Enabled = true;
                        uiTextBox8.Enabled = true;
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
                    //uiButton3.Enabled = false;
                    uiButton4.Enabled = false;
                    uiButton5.Enabled = false;
                    uiButton6.Enabled = false;

                    uiTextBoxy11.Enabled = false;
                    uiTextBoxy12.Enabled = false;
                    uiTextBoxy13.Enabled = false;
                    uiTextBoxy14.Enabled = false;
                    uiTextBoxy15.Enabled = false;
                    uiTextBoxy16.Enabled = false;
                    uiTextBoxy17.Enabled = false;
                    uiTextBoxy18.Enabled = false;
                    uiTextBoxy19.Enabled = false;
                    uiTextBoxy110.Enabled = false;

                    uiTextBoxy21.Enabled = false;
                    uiTextBoxy22.Enabled = false;
                    uiTextBoxy23.Enabled = false;
                    uiTextBoxy24.Enabled = false;
                    uiTextBoxy25.Enabled = false;
                    uiTextBoxy26.Enabled = false;
                    uiTextBoxy27.Enabled = false;
                    uiTextBoxy28.Enabled = false;
                    uiTextBoxy29.Enabled = false;
                    uiTextBoxy210.Enabled = false;

                    uiTextBoxpyz1.Enabled = false;
                    uiTextBoxpyz2.Enabled = false;
                    uiTextBoxpyz3.Enabled = false;
                    uiTextBoxpyz4.Enabled = false;
                    uiTextBoxpyz5.Enabled = false;
                    uiTextBoxpyz6.Enabled = false;
                    uiTextBoxpyz7.Enabled = false;
                    uiTextBoxpyz8.Enabled = false;
                    uiTextBoxpyz9.Enabled = false;
                    uiTextBoxpyz10.Enabled = false;

                    uiTextBox4.Enabled = false;
                    uiTextBox8.Enabled = false;

                    isHardnessRefresh = false;
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
                    numbers_x[i] = ToData(CheckedData[chartP + 2], CheckedData[chartP + 3]);
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
    /*                precision = Convert.ToDouble(uiTextBox1.Text);
                    UpPanel(mypanel1, numbers[0], thresholds[0], bRefresh);
                    UpPanel(mypanel2, numbers[1], thresholds[1], bRefresh);
                    UpPanel(mypanel3, numbers[2], thresholds[2], bRefresh);
                    UpPanel(mypanel4, numbers[3], thresholds[3], bRefresh);
                    UpPanel(mypanel5, numbers[4], thresholds[4], bRefresh);
                    UpPanel(mypanel6, numbers[5], thresholds[5], bRefresh);
                    UpPanel(mypanel7, numbers[6], thresholds[6], bRefresh);
                    UpPanel(mypanel8, numbers[7], thresholds[7], bRefresh);
                    UpPanel(mypanel9, numbers[8], thresholds[8], bRefresh);
                    UpPanel(mypanel10, numbers[9], thresholds[9], bRefresh);*/

                    if ( bRefresh )
                    {
                        uiLabel1.Text = (numbers_x[0]).ToString("F1");
                        uiLabel2.Text = (numbers_x[1]).ToString("F1");
                        uiLabel3.Text = (numbers_x[2]).ToString("F1");
                        uiLabel4.Text = (numbers_x[3]).ToString("F1");
                        uiLabel5.Text = (numbers_x[4]).ToString("F1");
                        uiLabel6.Text = (numbers_x[5]).ToString("F1");
                        uiLabel7.Text = (numbers_x[6]).ToString("F1");
                        uiLabel8.Text = (numbers_x[7]).ToString("F1");
                        uiLabel9.Text = (numbers_x[8]).ToString("F1");
                        uiLabel10.Text = (numbers_x[9]).ToString("F1");

                        uiLabel12.Text = (numbers[0]).ToString("F1");
                        uiLabel13.Text = (numbers[1]).ToString("F1");
                        uiLabel14.Text = (numbers[2]).ToString("F1");
                        uiLabel15.Text = (numbers[3]).ToString("F1");
                        uiLabel16.Text = (numbers[4]).ToString("F1");
                        uiLabel17.Text = (numbers[5]).ToString("F1");
                        uiLabel18.Text = (numbers[6]).ToString("F1");
                        uiLabel19.Text = (numbers[7]).ToString("F1");
                        uiLabel20.Text = (numbers[8]).ToString("F1");
                        uiLabel21.Text = (numbers[9]).ToString("F1");
                    }

                    if (isHardnessRefresh)
                    {
                        bool allValid = true;
                        for (int i = 0; i < uiTableLayoutPanel1.RowCount; i++)
                        {
                            Control control = uiTableLayoutPanel1.GetControlFromPosition(9, i); // 获取第十列的控件
                            if (control is TextBox textBox)
                            {
                                if (!string.IsNullOrEmpty(textBox.Text))
                                {
                                    double value;
                                    if (!double.TryParse(textBox.Text, out value))
                                    {
                                        allValid = false;
                                        MessageBox.Show($"第 {i + 1} 行的值不是数字");
                                        return;
                                    }
                                }
                                
                                // 进行其他操作
                            }
                        }


                        if (allValid)  // 如果所有 TextBox 均为数字，则进行下一步操作
                        {
                            HardnessValue[0] = k[0] * (Convert.ToDouble(uiLabel1.Text)) + d[0] + offsetValue[0];
                            HardnessValue[1] = k[1] * (Convert.ToDouble(uiLabel2.Text)) + d[1] + offsetValue[1];
                            HardnessValue[2] = k[2] * (Convert.ToDouble(uiLabel3.Text)) + d[2] + offsetValue[2];
                            HardnessValue[3] = k[3] * (Convert.ToDouble(uiLabel4.Text)) + d[3] + offsetValue[3];
                            HardnessValue[4] = k[4] * (Convert.ToDouble(uiLabel5.Text)) + d[4] + offsetValue[4];
                            HardnessValue[5] = k[5] * (Convert.ToDouble(uiLabel6.Text)) + d[5] + offsetValue[5];
                            HardnessValue[6] = k[6] * (Convert.ToDouble(uiLabel7.Text)) + d[6] + offsetValue[6];
                            HardnessValue[7] = k[7] * (Convert.ToDouble(uiLabel8.Text)) + d[7] + offsetValue[7];
                            HardnessValue[8] = k[8] * (Convert.ToDouble(uiLabel9.Text)) + d[8] + offsetValue[8];
                            HardnessValue[9] = k[9] * (Convert.ToDouble(uiLabel10.Text)) + d[9] + offsetValue[9];

                            /*Random random = new Random();
                            int[] temps = new int[9];

                            for (int i = 0; i < 9; i++)
                            {
                                temps[i] = random.Next(-2, 2);
                            }
                            HardnessValue[0] = k[0] * (Convert.ToDouble(uiLabel1.Text)) + d[0] + offsetValue[0];
                            HardnessValue[1] = HardnessValue[0] + temps[0] + offsetValue[1];
                            HardnessValue[2] = HardnessValue[0] + temps[1] + offsetValue[2];
                            HardnessValue[3] = HardnessValue[0] + temps[2] + offsetValue[3];
                            HardnessValue[4] = HardnessValue[0] + temps[3] + offsetValue[4];
                            HardnessValue[5] = HardnessValue[0] + temps[4] + offsetValue[5];
                            HardnessValue[6] = HardnessValue[0] + temps[5] + offsetValue[6];
                            HardnessValue[7] = HardnessValue[0] + temps[6] + offsetValue[7];
                            HardnessValue[8] = HardnessValue[0] + temps[7] + offsetValue[8];
                            HardnessValue[9] = HardnessValue[0] + temps[8] + offsetValue[9];*/





                        }
                        

                        uiLabelyd1.Text = (HardnessValue[0]).ToString("F1") ;
                        uiLabelyd2.Text = (HardnessValue[1]).ToString("F1") ;
                        uiLabelyd3.Text = (HardnessValue[2]).ToString("F1") ;
                        uiLabelyd4.Text = (HardnessValue[3]).ToString("F1") ;
                        uiLabelyd5.Text = (HardnessValue[4]).ToString("F1") ;
                        uiLabelyd6.Text = (HardnessValue[5]).ToString("F1") ;
                        uiLabelyd7.Text = (HardnessValue[6]).ToString("F1") ;
                        uiLabelyd8.Text = (HardnessValue[7]).ToString("F1") ;
                        uiLabelyd9.Text = (HardnessValue[8]).ToString("F1") ;
                        uiLabelyd10.Text = (HardnessValue[9]).ToString("F1");
                    }

                }));             
            }
            Thread.Sleep(10000);

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
            /*if (!uiLabel1.Text.Equals(""))
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

                thresholds_x[0] = Convert.ToDouble(uiLabel12.Text);
                thresholds_x[1] = Convert.ToDouble(uiLabel13.Text);
                thresholds_x[2] = Convert.ToDouble(uiLabel14.Text);
                thresholds_x[3] = Convert.ToDouble(uiLabel15.Text);
                thresholds_x[4] = Convert.ToDouble(uiLabel16.Text);
                thresholds_x[5] = Convert.ToDouble(uiLabel17.Text);
                thresholds_x[6] = Convert.ToDouble(uiLabel18.Text);
                thresholds_x[7] = Convert.ToDouble(uiLabel19.Text);
                thresholds_x[8] = Convert.ToDouble(uiLabel20.Text);
                thresholds_x[9] = Convert.ToDouble(uiLabel21.Text);
            }
            else
            {
                MessageBox.Show("未收到串口数据，请检查串口是否选择正确");
            }*/
            isHardnessRefresh = true;


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

        //重新标定
        private void uiButton4_Click(object sender, EventArgs e)
        {
            isHardnessRefresh = false;
            uiButton3.Enabled = false;
            uiButton7.Enabled = true;

            uiTextBoxy11.Enabled = true;
            uiTextBoxy12.Enabled = true;
            uiTextBoxy13.Enabled = true;
            uiTextBoxy14.Enabled = true;
            uiTextBoxy15.Enabled = true;
            uiTextBoxy16.Enabled = true;
            uiTextBoxy17.Enabled = true;
            uiTextBoxy18.Enabled = true;
            uiTextBoxy19.Enabled = true;
            uiTextBoxy110.Enabled = true;

            uiTextBoxy21.Enabled = true;
            uiTextBoxy22.Enabled = true;
            uiTextBoxy23.Enabled = true;
            uiTextBoxy24.Enabled = true;
            uiTextBoxy25.Enabled = true;
            uiTextBoxy26.Enabled = true;
            uiTextBoxy27.Enabled = true;
            uiTextBoxy28.Enabled = true;
            uiTextBoxy29.Enabled = true;
            uiTextBoxy210.Enabled = true;

            uiTextBox4.Enabled = true;
            uiTextBox8.Enabled = true;


            uiTextBoxy11.Clear();
            uiTextBoxy12.Clear();
            uiTextBoxy13.Clear();
            uiTextBoxy14.Clear();
            uiTextBoxy15.Clear();
            uiTextBoxy16.Clear();
            uiTextBoxy17.Clear();
            uiTextBoxy18.Clear();
            uiTextBoxy19.Clear();
            uiTextBoxy110.Clear();

            uiTextBoxy21.Clear();
            uiTextBoxy22.Clear();
            uiTextBoxy23.Clear();
            uiTextBoxy24.Clear();
            uiTextBoxy25.Clear();
            uiTextBoxy26.Clear();
            uiTextBoxy27.Clear();
            uiTextBoxy28.Clear();
            uiTextBoxy29.Clear();
            uiTextBoxy210.Clear();

            uiTextBoxkz1.Clear();
            uiTextBoxkz2.Clear();
            uiTextBoxkz3.Clear();
            uiTextBoxkz4.Clear();
            uiTextBoxkz5.Clear();
            uiTextBoxkz6.Clear();
            uiTextBoxkz7.Clear();
            uiTextBoxkz8.Clear();
            uiTextBoxkz9.Clear();
            uiTextBoxkz10.Clear();

            uiTextBoxdz1.Clear();
            uiTextBoxdz2.Clear();
            uiTextBoxdz3.Clear();
            uiTextBoxdz4.Clear();
            uiTextBoxdz5.Clear();
            uiTextBoxdz6.Clear();
            uiTextBoxdz7.Clear();
            uiTextBoxdz8.Clear();
            uiTextBoxdz9.Clear();
            uiTextBoxdz10.Clear();

            uiTextBox4.Clear();
            uiTextBox8.Clear();

            for (int i = 0; i < 10; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    xyArray[i, j] = 0;
                }
            }

            for (int i = 0; i < 10; i++)
            {
                k[i] = 0.0;
                d[i] = 0.0;
                HardnessValue[i] = 0;
            }

            uiLabelyd1.Text = "0";
            uiLabelyd2.Text = "0";
            uiLabelyd3.Text = "0";
            uiLabelyd4.Text = "0";
            uiLabelyd5.Text = "0";
            uiLabelyd6.Text = "0";
            uiLabelyd7.Text = "0";
            uiLabelyd8.Text = "0";
            uiLabelyd9.Text = "0";
            uiLabelyd10.Text = "0";


            /*calibrationWindow = new calibration(numbers);//自定义校验窗口
            calibrationWindow.WindowClosed += MyNewWindow_WindowClosed;
            calibrationWindow.Show();*/
        }

        private void uiButton5_Click(object sender, EventArgs e)
        {
            uiButton5.Enabled = false;
            uiButton6.Enabled = true;
            startTime = System.DateTime.Now;
            int temp = CheckedData.Count();
            if (temp >= 40)
            {
                if (temp % 40 != 0)
                {
                    Savep = (temp / 40) * 40;
                }
            }
            else
            {
                Savep = 0;
            }

        }

        private void uiButton6_Click(object sender, EventArgs e)
        {
            uiButton5.Enabled = true;
            uiButton6.Enabled = false;
            finishTime = System.DateTime.Now;
            List<byte> SaveDate = new List<byte>();
            int cout = CheckedData.Count();
            SaveDate.AddRange(CheckedData.GetRange(Savep, cout));
            if (SaveDate.Equals(" "))
            {
                MessageBox.Show("接收数据为空，无需保存！");
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (var item in SaveDate)
            {
                sb.Append(item.ToString() + ' ');
            }
            String recv_data = sb.ToString();

            String fileName;

            String fileNamereal1;
            String fileNamereal2;
            String fileNamereal3;
            String fileNamereal4;
            String fileNamereal5;
            String fileNamereal6;
            String fileNamereal7;
            String fileNamereal8;
            String fileNamereal9;
            String fileNamereal10;

            String fileNamelmag1;
            String fileNamelmag2;
            String fileNamelmag3;
            String fileNamelmag4;
            String fileNamelmag5;
            String fileNamelmag6;
            String fileNamelmag7;
            String fileNamelmag8;
            String fileNamelmag9;
            String fileNamelmag10;

            string foldPath;

            // int mSCout = ((SaveDate.Count) / 40) * 20;
            /*   double[] doubelSaveDate = new double[mSCout];//20个数为一组*/

            int mSCout = ((SaveDate.Count) / 40);
            List<int> real1 = new List<int>();
            List<int> real2 = new List<int>();
            List<int> real3 = new List<int>();
            List<int> real4 = new List<int>();
            List<int> real5 = new List<int>();
            List<int> real6 = new List<int>();
            List<int> real7 = new List<int>();
            List<int> real8 = new List<int>();
            List<int> real9 = new List<int>();
            List<int> real10 = new List<int>();

            List<int> lmag1 = new List<int>();
            List<int> lmag2 = new List<int>();
            List<int> lmag3 = new List<int>();
            List<int> lmag4 = new List<int>();
            List<int> lmag5 = new List<int>();
            List<int> lmag6 = new List<int>();
            List<int> lmag7 = new List<int>();
            List<int> lmag8 = new List<int>();
            List<int> lmag9 = new List<int>();
            List<int> lmag10 = new List<int>();

            for (int i = 0; i < mSCout; i++)
            {
                real1.Add(ToIntData(SaveDate[40 * i], SaveDate[40 * i + 1]));
                lmag1.Add(ToIntData(SaveDate[40 * i + 2], SaveDate[40 * i + 3]));
                real2.Add(ToIntData(SaveDate[40 * i + 4], SaveDate[40 * i + 5]));
                lmag2.Add(ToIntData(SaveDate[40 * i + 6], SaveDate[40 * i + 7]));
                real3.Add(ToIntData(SaveDate[40 * i + 8], SaveDate[40 * i + 9]));
                lmag3.Add(ToIntData(SaveDate[40 * i + 10], SaveDate[40 * i + 11]));
                real4.Add(ToIntData(SaveDate[40 * i + 12], SaveDate[40 * i + 13]));
                lmag4.Add(ToIntData(SaveDate[40 * i + 14], SaveDate[40 * i + 15]));
                real5.Add(ToIntData(SaveDate[40 * i + 16], SaveDate[40 * i + 17]));
                lmag5.Add(ToIntData(SaveDate[40 * i + 18], SaveDate[40 * i + 19]));
                real6.Add(ToIntData(SaveDate[40 * i + 20], SaveDate[40 * i + 21]));
                lmag6.Add(ToIntData(SaveDate[40 * i + 22], SaveDate[40 * i + 23]));
                real7.Add(ToIntData(SaveDate[40 * i + 24], SaveDate[40 * i + 25]));
                lmag7.Add(ToIntData(SaveDate[40 * i + 26], SaveDate[40 * i + 27]));
                real8.Add(ToIntData(SaveDate[40 * i + 28], SaveDate[40 * i + 29]));
                lmag8.Add(ToIntData(SaveDate[40 * i + 30], SaveDate[40 * i + 31]));
                real9.Add(ToIntData(SaveDate[40 * i + 32], SaveDate[40 * i + 33]));
                lmag9.Add(ToIntData(SaveDate[40 * i + 34], SaveDate[40 * i + 35]));
                real10.Add(ToIntData(SaveDate[40 * i + 36], SaveDate[40 * i + 37]));
                lmag10.Add(ToIntData(SaveDate[40 * i + 38], SaveDate[40 * i + 39]));
                /*doubelSaveDate[i] = ToData(SaveDate[2 * i], SaveDate[2 * i + 1]);*/
            }

            String real1_str = GetDataStr(real1);
            String real2_str = GetDataStr(real2);
            String real3_str = GetDataStr(real3);
            String real4_str = GetDataStr(real4);
            String real5_str = GetDataStr(real5);
            String real6_str = GetDataStr(real6);
            String real7_str = GetDataStr(real7);
            String real8_str = GetDataStr(real8);
            String real9_str = GetDataStr(real9);
            String real10_str = GetDataStr(real10);

            String lmag1_str = GetDataStr(lmag1);
            String lmag2_str = GetDataStr(lmag2);
            String lmag3_str = GetDataStr(lmag3);
            String lmag4_str = GetDataStr(lmag4);
            String lmag5_str = GetDataStr(lmag5);
            String lmag6_str = GetDataStr(lmag6);
            String lmag7_str = GetDataStr(lmag7);
            String lmag8_str = GetDataStr(lmag8);
            String lmag9_str = GetDataStr(lmag9);
            String lmag10_str = GetDataStr(lmag10);

            /* 弹出文件夹选择框供用户选择 */
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            dialog.Description = "请选择日志文件存储路径";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                foldPath = dialog.SelectedPath;
            }
            else
            {
                return;
            }
            TimeSpan span1 = finishTime - startTime;



            fileName = foldPath + "\\" + "log" + "_" + "时长" + span1.ToString(@"mm\.ss") + "_" + "开始时间" + "_" + startTime.ToString("yyyy_MM_dd_HH_mm_ss") + ".txt";
            fileNamereal1 = foldPath + "\\" + "log" + "_real1_" + ".txt";
            fileNamereal2 = foldPath + "\\" + "log" + "_real2_" + ".txt";
            fileNamereal3 = foldPath + "\\" + "log" + "_real3_" + ".txt";
            fileNamereal4 = foldPath + "\\" + "log" + "_real4_" + ".txt";
            fileNamereal5 = foldPath + "\\" + "log" + "_real5_" + ".txt";
            fileNamereal6 = foldPath + "\\" + "log" + "_real6_" + ".txt";
            fileNamereal7 = foldPath + "\\" + "log" + "_real7_" + ".txt";
            fileNamereal8 = foldPath + "\\" + "log" + "_real8_" + ".txt";
            fileNamereal9 = foldPath + "\\" + "log" + "_real9_" + ".txt";
            fileNamereal10 = foldPath + "\\" + "log" + "_real10_" + ".txt";

            fileNamelmag1 = foldPath + "\\" + "log" + "_lmag1_" + ".txt";
            fileNamelmag2 = foldPath + "\\" + "log" + "_lmag2_" + ".txt";
            fileNamelmag3 = foldPath + "\\" + "log" + "_lmag3_" + ".txt";
            fileNamelmag4 = foldPath + "\\" + "log" + "_lmag4_" + ".txt";
            fileNamelmag5 = foldPath + "\\" + "log" + "_lmag5_" + ".txt";
            fileNamelmag6 = foldPath + "\\" + "log" + "_lmag6_" + ".txt";
            fileNamelmag7 = foldPath + "\\" + "log" + "_lmag7_" + ".txt";
            fileNamelmag8 = foldPath + "\\" + "log" + "_lmag8_" + ".txt";
            fileNamelmag9 = foldPath + "\\" + "log" + "_lmag9_" + ".txt";
            fileNamelmag10 = foldPath + "\\" + "log" + "_lmag10_" + ".txt";

            try
            {
                /* 保存串口接收区的内容 */
                //创建 FileStream 类的实例
                FileStream fileStream = new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal1 = new FileStream(fileNamereal1, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal2 = new FileStream(fileNamereal2, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal3 = new FileStream(fileNamereal3, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal4 = new FileStream(fileNamereal4, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal5 = new FileStream(fileNamereal5, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal6 = new FileStream(fileNamereal6, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal7 = new FileStream(fileNamereal7, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal8 = new FileStream(fileNamereal8, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal9 = new FileStream(fileNamereal9, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamreal10 = new FileStream(fileNamereal10, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                FileStream fileStreamlmag1 = new FileStream(fileNamelmag1, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamlmag2 = new FileStream(fileNamelmag2, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamlmag3 = new FileStream(fileNamelmag3, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamlmag4 = new FileStream(fileNamelmag4, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamlmag5 = new FileStream(fileNamelmag5, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamlmag6 = new FileStream(fileNamelmag6, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamlmag7 = new FileStream(fileNamelmag7, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamlmag8 = new FileStream(fileNamelmag8, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamlmag9 = new FileStream(fileNamelmag9, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                FileStream fileStreamlmag10 = new FileStream(fileNamelmag10, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                //将字符串转换为字节数组
                byte[] bytes = Encoding.UTF8.GetBytes(recv_data);
                byte[] bytes_real1 = Encoding.UTF8.GetBytes(real1_str);
                byte[] bytes_real2 = Encoding.UTF8.GetBytes(real2_str);
                byte[] bytes_real3 = Encoding.UTF8.GetBytes(real3_str);
                byte[] bytes_real4 = Encoding.UTF8.GetBytes(real4_str);
                byte[] bytes_real5 = Encoding.UTF8.GetBytes(real5_str);
                byte[] bytes_real6 = Encoding.UTF8.GetBytes(real6_str);
                byte[] bytes_real7 = Encoding.UTF8.GetBytes(real7_str);
                byte[] bytes_real8 = Encoding.UTF8.GetBytes(real8_str);
                byte[] bytes_real9 = Encoding.UTF8.GetBytes(real9_str);
                byte[] bytes_real10 = Encoding.UTF8.GetBytes(real10_str);

                byte[] bytes_lmag1 = Encoding.UTF8.GetBytes(lmag1_str);
                byte[] bytes_lmag2 = Encoding.UTF8.GetBytes(lmag2_str);
                byte[] bytes_lmag3 = Encoding.UTF8.GetBytes(lmag3_str);
                byte[] bytes_lmag4 = Encoding.UTF8.GetBytes(lmag4_str);
                byte[] bytes_lmag5 = Encoding.UTF8.GetBytes(lmag5_str);
                byte[] bytes_lmag6 = Encoding.UTF8.GetBytes(lmag6_str);
                byte[] bytes_lmag7 = Encoding.UTF8.GetBytes(lmag7_str);
                byte[] bytes_lmag8 = Encoding.UTF8.GetBytes(lmag8_str);
                byte[] bytes_lmag9 = Encoding.UTF8.GetBytes(lmag9_str);
                byte[] bytes_lmag10 = Encoding.UTF8.GetBytes(lmag10_str);


                //向文件中写入字节数组
                fileStream.Write(bytes, 0, bytes.Length);
                fileStreamreal1.Write(bytes_real1, 0, bytes_real1.Length);
                fileStreamlmag1.Write(bytes_lmag1, 0, bytes_lmag1.Length);

                fileStreamreal2.Write(bytes_real2, 0, bytes_real2.Length);
                fileStreamlmag2.Write(bytes_lmag2, 0, bytes_lmag2.Length);

                fileStreamreal3.Write(bytes_real3, 0, bytes_real3.Length);
                fileStreamlmag3.Write(bytes_lmag3, 0, bytes_lmag3.Length);

                fileStreamreal4.Write(bytes_real4, 0, bytes_real4.Length);
                fileStreamlmag4.Write(bytes_lmag4, 0, bytes_lmag4.Length);

                fileStreamreal5.Write(bytes_real5, 0, bytes_real5.Length);
                fileStreamlmag5.Write(bytes_lmag5, 0, bytes_lmag5.Length);

                fileStreamreal6.Write(bytes_real6, 0, bytes_real6.Length);
                fileStreamlmag6.Write(bytes_lmag6, 0, bytes_lmag6.Length);

                fileStreamreal7.Write(bytes_real7, 0, bytes_real7.Length);
                fileStreamlmag7.Write(bytes_lmag7, 0, bytes_lmag7.Length);

                fileStreamreal8.Write(bytes_real8, 0, bytes_real8.Length);
                fileStreamlmag8.Write(bytes_lmag8, 0, bytes_lmag8.Length);

                fileStreamreal9.Write(bytes_real9, 0, bytes_real9.Length);
                fileStreamlmag9.Write(bytes_lmag9, 0, bytes_lmag9.Length);

                fileStreamreal10.Write(bytes_real10, 0, bytes_real10.Length);
                fileStreamlmag10.Write(bytes_lmag10, 0, bytes_lmag10.Length);

                //刷新缓冲区
                fileStream.Flush();
                fileStreamreal1.Flush();
                fileStreamreal2.Flush();
                fileStreamreal3.Flush();
                fileStreamreal4.Flush();
                fileStreamreal5.Flush();
                fileStreamreal6.Flush();
                fileStreamreal7.Flush();
                fileStreamreal8.Flush();
                fileStreamreal9.Flush();
                fileStreamreal10.Flush();

                fileStreamlmag1.Flush();
                fileStreamlmag2.Flush();
                fileStreamlmag3.Flush();
                fileStreamlmag4.Flush();
                fileStreamlmag5.Flush();
                fileStreamlmag6.Flush();
                fileStreamlmag7.Flush();
                fileStreamlmag8.Flush();
                fileStreamlmag9.Flush();
                fileStreamlmag10.Flush();

                //关闭流
                fileStream.Close();
                fileStreamreal1.Close();
                fileStreamreal2.Close();
                fileStreamreal3.Close();
                fileStreamreal4.Close();
                fileStreamreal5.Close();
                fileStreamreal6.Close();
                fileStreamreal7.Close();
                fileStreamreal8.Close();
                fileStreamreal9.Close();
                fileStreamreal10.Close();

                fileStreamlmag1.Close();
                fileStreamlmag2.Close();
                fileStreamlmag3.Close();
                fileStreamlmag4.Close();
                fileStreamlmag5.Close();
                fileStreamlmag6.Close();
                fileStreamlmag7.Close();
                fileStreamlmag8.Close();
                fileStreamlmag9.Close();
                fileStreamlmag10.Close();

                //提示用户
                MessageBox.Show("日志已保存!(" + fileNamereal1 + ")");
                //ToMatlab(real1);
            }
            catch (Exception ex)
            {
                //提示用户
                MessageBox.Show("发生异常!(" + ex.ToString() + ")");
            }


        }

        private string GetDataStr(List<int> list)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var item in list)
            {
                sb.Append(item.ToString() + ' ');
            }
            //int listDev = maxListDev(list);
            //sb.Append("该通道的最大差值为：" + listDev.ToString() + '\n');

            return sb.ToString();
        }

        private int ToIntData(byte lowByte, byte highByte)
        {
            short rawValue = (short)((highByte << 8) | lowByte); // convert to signed short

            int signedValue = Convert.ToInt32(rawValue); // convert to signed double

            return signedValue;
        }


        private void uiTextBoxy11_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy11.Text, out number))
                {
                    uiTextBoxy11.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy11.Enabled = false;
                    uiLabely11.Text = uiLabel1.Text + " , " + Convert.ToString(uiTextBoxy11.Text);
                    xyArray[0, 0] = Convert.ToDouble(uiLabel1.Text);
                    xyArray[0, 1] = Convert.ToDouble(uiTextBoxy11.Text);
                }
                
            }          
        }

        private void uiTextBoxy12_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy12.Text, out number))
                {
                    uiTextBoxy12.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy12.Enabled = false;
                    uiLabely12.Text = uiLabel2.Text + " , " + Convert.ToString(uiTextBoxy12.Text);
                    xyArray[1, 0] = Convert.ToDouble(uiLabel2.Text);
                    xyArray[1, 1] = Convert.ToDouble(uiTextBoxy12.Text);
                }
                
            }
        }

        private void uiTextBoxy13_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy13.Text, out number))
                {
                    uiTextBoxy13.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy13.Enabled = false;
                    uiLabely13.Text = uiLabel3.Text + " , " + Convert.ToString(uiTextBoxy13.Text);
                    xyArray[2, 0] = Convert.ToDouble(uiLabel3.Text);
                    xyArray[2, 1] = Convert.ToDouble(uiTextBoxy13.Text);
                }
                
            }
        }

        private void uiTextBoxy14_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy14.Text, out number))
                {
                    uiTextBoxy14.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy14.Enabled = false;
                    uiLabely14.Text = uiLabel4.Text + " , " + Convert.ToString(uiTextBoxy14.Text);
                    xyArray[3, 0] = Convert.ToDouble(uiLabel4.Text);
                    xyArray[3, 1] = Convert.ToDouble(uiTextBoxy14.Text);
                }
              
            }
        }

        private void uiTextBoxy15_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy15.Text, out number))
                {
                    uiTextBoxy15.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy15.Enabled = false;
                    uiLabely15.Text = uiLabel5.Text + " , " + Convert.ToString(uiTextBoxy15.Text);
                    xyArray[4, 0] = Convert.ToDouble(uiLabel5.Text);
                    xyArray[4, 1] = Convert.ToDouble(uiTextBoxy15.Text);
                }
                
            }
        }

        private void uiTextBoxy16_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy16.Text, out number))
                {
                    uiTextBoxy16.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy16.Enabled = false;
                    uiLabely16.Text = uiLabel6.Text + " , " + Convert.ToString(uiTextBoxy16.Text);
                    xyArray[5, 0] = Convert.ToDouble(uiLabel6.Text);
                    xyArray[5, 1] = Convert.ToDouble(uiTextBoxy16.Text);
                }
                
            }
        }

        private void uiTextBoxy17_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy17.Text, out number))
                {
                    uiTextBoxy17.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy17.Enabled = false;
                    uiLabely17.Text = uiLabel7.Text + " , " + Convert.ToString(uiTextBoxy17.Text);
                    xyArray[6, 0] = Convert.ToDouble(uiLabel7.Text);
                    xyArray[6, 1] = Convert.ToDouble(uiTextBoxy17.Text);
                }
                
            }
        }

        private void uiTextBoxy18_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy18.Text, out number))
                {
                    uiTextBoxy18.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy18.Enabled = false;
                    uiLabely18.Text = uiLabel8.Text + " , " + Convert.ToString(uiTextBoxy18.Text);
                    xyArray[7, 0] = Convert.ToDouble(uiLabel8.Text);
                    xyArray[7, 1] = Convert.ToDouble(uiTextBoxy18.Text);
                }
                
            }
        }

        private void uiTextBoxy19_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy19.Text, out number))
                {
                    uiTextBoxy19.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy19.Enabled = false;
                    uiLabely19.Text = uiLabel9.Text + " , " + Convert.ToString(uiTextBoxy19.Text);
                    xyArray[8, 0] = Convert.ToDouble(uiLabel9.Text);
                    xyArray[8, 1] = Convert.ToDouble(uiTextBoxy19.Text);
                }
                
            }
        }

        private void uiTextBoxy110_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy110.Text, out number))
                {
                    uiTextBoxy110.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy110.Enabled = false;
                    uiLabely110.Text = uiLabel10.Text + " , " + Convert.ToString(uiTextBoxy110.Text);
                    xyArray[9, 0] = Convert.ToDouble(uiLabel10.Text);
                    xyArray[9, 1] = Convert.ToDouble(uiTextBoxy110.Text);
                }
               
            }
        }

        private void uiTextBoxy21_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy21.Text, out number))
                {
                    uiTextBoxy21.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {

                }
                uiTextBoxy21.Enabled = false;
                uiLabely21.Text = uiLabel1.Text + " , " + Convert.ToString(uiTextBoxy21.Text);
                xyArray[0, 2] = Convert.ToDouble(uiLabel1.Text);
                xyArray[0, 3] = Convert.ToDouble(uiTextBoxy21.Text);
            }
        }

        private void uiTextBoxy22_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy22.Text, out number))
                {
                    uiTextBoxy22.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy22.Enabled = false;
                    uiLabely22.Text = uiLabel2.Text + " , " + Convert.ToString(uiTextBoxy22.Text);
                    xyArray[1, 2] = Convert.ToDouble(uiLabel2.Text);
                    xyArray[1, 3] = Convert.ToDouble(uiTextBoxy22.Text);
                }
               
            }
        }

        private void uiTextBoxy23_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy23.Text, out number))
                {
                    uiTextBoxy23.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy23.Enabled = false;
                    uiLabely23.Text = uiLabel3.Text + " , " + Convert.ToString(uiTextBoxy23.Text);
                    xyArray[2, 2] = Convert.ToDouble(uiLabel3.Text);
                    xyArray[2, 3] = Convert.ToDouble(uiTextBoxy23.Text);
                }
                
            }
        }

        private void uiTextBoxy24_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy24.Text, out number))
                {
                    uiTextBoxy24.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy24.Enabled = false;
                    uiLabely24.Text = uiLabel4.Text + " , " + Convert.ToString(uiTextBoxy24.Text);
                    xyArray[3, 2] = Convert.ToDouble(uiLabel4.Text);
                    xyArray[3, 3] = Convert.ToDouble(uiTextBoxy24.Text);
                }
                
            }
        }

        private void uiTextBoxy25_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy25.Text, out number))
                {
                    uiTextBoxy25.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy25.Enabled = false;
                    uiLabely25.Text = uiLabel5.Text + " , " + Convert.ToString(uiTextBoxy25.Text);
                    xyArray[4, 2] = Convert.ToDouble(uiLabel5.Text);
                    xyArray[4, 3] = Convert.ToDouble(uiTextBoxy25.Text);
                }
              
            }
        }

        private void uiTextBoxy26_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy26.Text, out number))
                {
                    uiTextBoxy26.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy26.Enabled = false;
                    uiLabely26.Text = uiLabel6.Text + " , " + Convert.ToString(uiTextBoxy26.Text);
                    xyArray[5, 2] = Convert.ToDouble(uiLabel6.Text);
                    xyArray[5, 3] = Convert.ToDouble(uiTextBoxy26.Text);
                }
                
            }
        }

        private void uiTextBoxy27_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy27.Text, out number))
                {
                    uiTextBoxy27.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy27.Enabled = false;
                    uiLabely27.Text = uiLabel7.Text + " , " + Convert.ToString(uiTextBoxy27.Text);
                    xyArray[6, 2] = Convert.ToDouble(uiLabel7.Text);
                    xyArray[6, 3] = Convert.ToDouble(uiTextBoxy27.Text);
                }
                
            }
        }

        private void uiTextBoxy28_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy28.Text, out number))
                {
                    uiTextBoxy28.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy28.Enabled = false;
                    uiLabely28.Text = uiLabel8.Text + " , " + Convert.ToString(uiTextBoxy28.Text);
                    xyArray[7, 2] = Convert.ToDouble(uiLabel8.Text);
                    xyArray[7, 3] = Convert.ToDouble(uiTextBoxy28.Text);
                }
               
            }
        }

        private void uiTextBoxy29_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy29.Text, out number))
                {
                    uiTextBoxy29.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy29.Enabled = false;
                    uiLabely29.Text = uiLabel9.Text + " , " + Convert.ToString(uiTextBoxy29.Text);
                    xyArray[8, 2] = Convert.ToDouble(uiLabel9.Text);
                    xyArray[8, 3] = Convert.ToDouble(uiTextBoxy29.Text);
                }
             
            }
        }

        private void uiTextBoxy210_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBoxy210.Text, out number))
                {
                    uiTextBoxy210.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy210.Enabled = false;
                    uiLabely210.Text = uiLabel10.Text + " , " + Convert.ToString(uiTextBoxy210.Text);
                    xyArray[9, 2] = Convert.ToDouble(uiLabel10.Text);
                    xyArray[9, 3] = Convert.ToDouble(uiTextBoxy210.Text);
                }
              
            }
        }

        private void uiTextBox4_KeyDown(object sender, KeyEventArgs e)
        {


            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBox4.Text, out number))
                {
                    uiTextBox4.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy11.Text = uiTextBox4.Text;
                    uiTextBoxy12.Text = uiTextBox4.Text;
                    uiTextBoxy13.Text = uiTextBox4.Text;
                    uiTextBoxy14.Text = uiTextBox4.Text;
                    uiTextBoxy15.Text = uiTextBox4.Text;
                    uiTextBoxy16.Text = uiTextBox4.Text;
                    uiTextBoxy17.Text = uiTextBox4.Text;
                    uiTextBoxy18.Text = uiTextBox4.Text;
                    uiTextBoxy19.Text = uiTextBox4.Text;
                    uiTextBoxy110.Text = uiTextBox4.Text;

                    uiTextBoxy11.Enabled = false;
                    uiTextBoxy12.Enabled = false;
                    uiTextBoxy13.Enabled = false;
                    uiTextBoxy14.Enabled = false;
                    uiTextBoxy15.Enabled = false;
                    uiTextBoxy16.Enabled = false;
                    uiTextBoxy17.Enabled = false;
                    uiTextBoxy18.Enabled = false;
                    uiTextBoxy19.Enabled = false;
                    uiTextBoxy110.Enabled = false;

                    uiLabely11.Text = uiLabel1.Text + " , " + Convert.ToString(uiTextBoxy11.Text);
                    uiLabely12.Text = uiLabel2.Text + " , " + Convert.ToString(uiTextBoxy12.Text);
                    uiLabely13.Text = uiLabel3.Text + " , " + Convert.ToString(uiTextBoxy13.Text);
                    uiLabely14.Text = uiLabel4.Text + " , " + Convert.ToString(uiTextBoxy14.Text);
                    uiLabely15.Text = uiLabel5.Text + " , " + Convert.ToString(uiTextBoxy15.Text);
                    uiLabely16.Text = uiLabel6.Text + " , " + Convert.ToString(uiTextBoxy16.Text);
                    uiLabely17.Text = uiLabel7.Text + " , " + Convert.ToString(uiTextBoxy17.Text);
                    uiLabely18.Text = uiLabel8.Text + " , " + Convert.ToString(uiTextBoxy18.Text);
                    uiLabely19.Text = uiLabel9.Text + " , " + Convert.ToString(uiTextBoxy19.Text);
                    uiLabely110.Text = uiLabel10.Text + " , " + Convert.ToString(uiTextBoxy110.Text);

                    xyArray[0, 0] = Convert.ToDouble(uiLabel1.Text);
                    xyArray[0, 1] = Convert.ToDouble(uiTextBoxy11.Text);
                    xyArray[1, 0] = Convert.ToDouble(uiLabel2.Text);
                    xyArray[1, 1] = Convert.ToDouble(uiTextBoxy12.Text);
                    xyArray[2, 0] = Convert.ToDouble(uiLabel3.Text);
                    xyArray[2, 1] = Convert.ToDouble(uiTextBoxy13.Text);
                    xyArray[3, 0] = Convert.ToDouble(uiLabel4.Text);
                    xyArray[3, 1] = Convert.ToDouble(uiTextBoxy14.Text);
                    xyArray[4, 0] = Convert.ToDouble(uiLabel5.Text);
                    xyArray[4, 1] = Convert.ToDouble(uiTextBoxy15.Text);
                    xyArray[5, 0] = Convert.ToDouble(uiLabel6.Text);
                    xyArray[5, 1] = Convert.ToDouble(uiTextBoxy16.Text);
                    xyArray[6, 0] = Convert.ToDouble(uiLabel7.Text);
                    xyArray[6, 1] = Convert.ToDouble(uiTextBoxy17.Text);
                    xyArray[7, 0] = Convert.ToDouble(uiLabel8.Text);
                    xyArray[7, 1] = Convert.ToDouble(uiTextBoxy18.Text);
                    xyArray[8, 0] = Convert.ToDouble(uiLabel9.Text);
                    xyArray[8, 1] = Convert.ToDouble(uiTextBoxy19.Text);
                    xyArray[9, 0] = Convert.ToDouble(uiLabel10.Text);
                    xyArray[9, 1] = Convert.ToDouble(uiTextBoxy110.Text);

                    uiTextBox4.Enabled = false;
                }
                
            }
        }

        private void uiTextBox8_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double number;
                if (!double.TryParse(uiTextBox4.Text, out number))
                {
                    uiTextBox4.Clear();
                    MessageBox.Show("请输入数字！");
                }
                else
                {
                    uiTextBoxy21.Text = uiTextBox8.Text;
                    uiTextBoxy22.Text = uiTextBox8.Text;
                    uiTextBoxy23.Text = uiTextBox8.Text;
                    uiTextBoxy24.Text = uiTextBox8.Text;
                    uiTextBoxy25.Text = uiTextBox8.Text;
                    uiTextBoxy26.Text = uiTextBox8.Text;
                    uiTextBoxy27.Text = uiTextBox8.Text;
                    uiTextBoxy28.Text = uiTextBox8.Text;
                    uiTextBoxy29.Text = uiTextBox8.Text;
                    uiTextBoxy210.Text = uiTextBox8.Text;

                    uiTextBoxy21.Enabled = false;
                    uiTextBoxy22.Enabled = false;
                    uiTextBoxy23.Enabled = false;
                    uiTextBoxy24.Enabled = false;
                    uiTextBoxy25.Enabled = false;
                    uiTextBoxy26.Enabled = false;
                    uiTextBoxy27.Enabled = false;
                    uiTextBoxy28.Enabled = false;
                    uiTextBoxy29.Enabled = false;
                    uiTextBoxy210.Enabled = false;

                    uiLabely21.Text = uiLabel1.Text + " , " + Convert.ToString(uiTextBoxy21.Text);
                    uiLabely22.Text = uiLabel2.Text + " , " + Convert.ToString(uiTextBoxy22.Text);
                    uiLabely23.Text = uiLabel3.Text + " , " + Convert.ToString(uiTextBoxy23.Text);
                    uiLabely24.Text = uiLabel4.Text + " , " + Convert.ToString(uiTextBoxy24.Text);
                    uiLabely25.Text = uiLabel5.Text + " , " + Convert.ToString(uiTextBoxy25.Text);
                    uiLabely26.Text = uiLabel6.Text + " , " + Convert.ToString(uiTextBoxy26.Text);
                    uiLabely27.Text = uiLabel7.Text + " , " + Convert.ToString(uiTextBoxy27.Text);
                    uiLabely28.Text = uiLabel8.Text + " , " + Convert.ToString(uiTextBoxy28.Text);
                    uiLabely29.Text = uiLabel9.Text + " , " + Convert.ToString(uiTextBoxy29.Text);
                    uiLabely210.Text = uiLabel10.Text + " , " + Convert.ToString(uiTextBoxy210.Text);

                    xyArray[0, 2] = Convert.ToDouble(uiLabel1.Text);
                    xyArray[0, 3] = Convert.ToDouble(uiTextBoxy21.Text);
                    xyArray[1, 2] = Convert.ToDouble(uiLabel2.Text);
                    xyArray[1, 3] = Convert.ToDouble(uiTextBoxy22.Text);
                    xyArray[2, 2] = Convert.ToDouble(uiLabel3.Text);
                    xyArray[2, 3] = Convert.ToDouble(uiTextBoxy23.Text);
                    xyArray[3, 2] = Convert.ToDouble(uiLabel4.Text);
                    xyArray[3, 3] = Convert.ToDouble(uiTextBoxy24.Text);
                    xyArray[4, 2] = Convert.ToDouble(uiLabel5.Text);
                    xyArray[4, 3] = Convert.ToDouble(uiTextBoxy25.Text);
                    xyArray[5, 2] = Convert.ToDouble(uiLabel6.Text);
                    xyArray[5, 3] = Convert.ToDouble(uiTextBoxy26.Text);
                    xyArray[6, 2] = Convert.ToDouble(uiLabel7.Text);
                    xyArray[6, 3] = Convert.ToDouble(uiTextBoxy27.Text);
                    xyArray[7, 2] = Convert.ToDouble(uiLabel8.Text);
                    xyArray[7, 3] = Convert.ToDouble(uiTextBoxy28.Text);
                    xyArray[8, 2] = Convert.ToDouble(uiLabel9.Text);
                    xyArray[8, 3] = Convert.ToDouble(uiTextBoxy29.Text);
                    xyArray[9, 2] = Convert.ToDouble(uiLabel10.Text);
                    xyArray[9, 3] = Convert.ToDouble(uiTextBoxy210.Text);

                    uiTextBox8.Enabled = false;
                }
               
            }
        }

        private void uiButton7_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < 10; i++)
            {
                double x1 = xyArray[i, 0];
                double y1 = xyArray[i, 1];
                double x2 = xyArray[i, 2];
                double y2 = xyArray[i, 3];

                if (x1 != 0 || y1 != 0 || x2 != 0 || y2 != 0)
                {
                    // Calculate the slope and intercept of the line
                    if (x2 != x1)
                    {
                        k[i] = (y2 - y1) / (x2 - x1);
                        d[i] = y1 - k[i] * x1;
                    }
                    else
                    {
                        // The line is vertical, so the slope is undefined
                        k[i] = double.NaN;
                        d[i] = double.NaN;
                    }
                }
                else
                {
                    // Display an error message if one of the points is not defined
                    MessageBox.Show("第"+(i + 1) +"通道未完成标定");
                    break;
                }
            }
            uiButton3.Enabled = true;
            //uiButton7.Enabled = false;

            uiTextBoxkz1.Text = Convert.ToString(k[0]);
            uiTextBoxkz2.Text = Convert.ToString(k[1]);
            uiTextBoxkz3.Text = Convert.ToString(k[2]);
            uiTextBoxkz4.Text = Convert.ToString(k[3]);
            uiTextBoxkz5.Text = Convert.ToString(k[4]);
            uiTextBoxkz6.Text = Convert.ToString(k[5]);
            uiTextBoxkz7.Text = Convert.ToString(k[6]);
            uiTextBoxkz8.Text = Convert.ToString(k[7]);
            uiTextBoxkz9.Text = Convert.ToString(k[8]);
            uiTextBoxkz10.Text = Convert.ToString(k[9]);

            uiTextBoxdz1.Text = Convert.ToString(d[0]);
            uiTextBoxdz2.Text = Convert.ToString(d[1]);
            uiTextBoxdz3.Text = Convert.ToString(d[2]);
            uiTextBoxdz4.Text = Convert.ToString(d[3]);
            uiTextBoxdz5.Text = Convert.ToString(d[4]);
            uiTextBoxdz6.Text = Convert.ToString(d[5]);
            uiTextBoxdz7.Text = Convert.ToString(d[6]);
            uiTextBoxdz8.Text = Convert.ToString(d[7]);
            uiTextBoxdz9.Text = Convert.ToString(d[8]);
            uiTextBoxdz10.Text = Convert.ToString(d[9]);

            
        }



        private void uiTextBoxkz1_TextChanged(object sender, EventArgs e)
        {
          
            if (sender is Sunny.UI.UITextBox textBox)
            {
                if (!string.IsNullOrEmpty(textBox.Text) && textBox.Text != "-")
                {
                    if (double.TryParse(textBox.Text, out double value))
                    {
                        int index = int.Parse(textBox.Name.Substring("uiTextBoxkz".Length)) - 1;
                        k[index] = value;
                    }
                    else
                    {
                        MessageBox.Show("请输入一个有效的数字");
                    }
                }
                
            }
        }

        private void uiTextBoxdz1_TextChanged(object sender, EventArgs e)
        {
            if (sender is Sunny.UI.UITextBox textBox)
            {
                if (!string.IsNullOrEmpty(textBox.Text) && textBox.Text != "-")
                {
                    if (double.TryParse(textBox.Text, out double value))
                    {
                        int index = int.Parse(textBox.Name.Substring("uiTextBoxdz".Length)) - 1;
                        d[index] = value;
                    }
                    else
                    {
                        MessageBox.Show("请输入一个有效的数字");
                    }
                }
                
            }
        }


        private void uiTextBoxpyz_TextChanged(object sender, EventArgs e)
        {
            if (sender is Sunny.UI.UITextBox textBox)
            {
                if (!string.IsNullOrEmpty(textBox.Text) && textBox.Text != "-")
                {
                    if (double.TryParse(textBox.Text, out double value))
                    {
                        int index = int.Parse(textBox.Name.Substring("uiTextBoxpyz".Length)) - 1;
                        offsetValue[index] = value;
                    }
                    else
                    {
                        MessageBox.Show("请输入一个有效的数字");
                    }
                }

            }
        }

        private void uiButtonSaveconf_Click(object sender, EventArgs e)
        {
            // Check if all the necessary text boxes are filled
            if (!string.IsNullOrEmpty(uiTextBoxtop1.Text) && !string.IsNullOrEmpty(uiTextBoxtop2.Text)
                && !string.IsNullOrEmpty(uiTextBoxtop3.Text) && !string.IsNullOrEmpty(uiTextBoxtop4.Text))
            {
                // Create the log folder if it doesn't exist
                string folderPath = @"D:\log\";
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Get the current date and time
                string dateTimeStr = DateTime.Now.ToString("yyyyMMddHH");

                // Construct the file name
                string fileName = $"{dateTimeStr}_{uiTextBoxtop1.Text}_{uiTextBoxtop3.Text}_{uiTextBoxtop4.Text}.cfg";
                string filePath = Path.Combine(folderPath, fileName);

                try
                {
                    // Open or create the configuration file
                    using (StreamWriter writer = new StreamWriter(filePath))
                    {
                        // Write the text box values to the file
                        writer.WriteLine(uiTextBoxtop1.Text);
                        writer.WriteLine(uiTextBoxtop2.Text);
                        writer.WriteLine(uiTextBoxtop3.Text);
                        writer.WriteLine(uiTextBoxtop4.Text);

                        // Write the k and d values to the file
                        for (int i = 0; i < k.Length; i++)
                        {
                            writer.WriteLine($"{k[i]},{d[i]},{offsetValue[i]}");
                        }
                    }

                    MessageBox.Show($"Configuration saved to {filePath}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save configuration: {ex.Message}");
                }
            }
        }

        private void uiButtonReadconf_Click(object sender, EventArgs e)
        {
            uiButton3.Enabled = true;
            // Create a file dialog for selecting the configuration file
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = @"D:\log\";
            openFileDialog.Filter = "Configuration Files (*.cfg)|*.cfg";
            openFileDialog.FilterIndex = 0;

            // Show the file dialog and get the selected file path
            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                string filePath = openFileDialog.FileName;

                try
                {
                    // Read the configuration file and set the text box values and k and d arrays
                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        uiTextBoxtop1.Text = reader.ReadLine();
                        uiTextBoxtop2.Text = reader.ReadLine();
                        uiTextBoxtop3.Text = reader.ReadLine();
                        uiTextBoxtop4.Text = reader.ReadLine();

                        for (int i = 0; i < k.Length; i++)
                        {
                            string[] values = reader.ReadLine().Split(',');
                            k[i] = double.Parse(values[0]);
                            d[i] = double.Parse(values[1]);
                            offsetValue[i] = double.Parse(values[2]);
                        }
                    }
                    uiTextBoxkz1.Text = Convert.ToString(k[0]);
                    uiTextBoxkz2.Text = Convert.ToString(k[1]);
                    uiTextBoxkz3.Text = Convert.ToString(k[2]);
                    uiTextBoxkz4.Text = Convert.ToString(k[3]);
                    uiTextBoxkz5.Text = Convert.ToString(k[4]);
                    uiTextBoxkz6.Text = Convert.ToString(k[5]);
                    uiTextBoxkz7.Text = Convert.ToString(k[6]);
                    uiTextBoxkz8.Text = Convert.ToString(k[7]);
                    uiTextBoxkz9.Text = Convert.ToString(k[8]);
                    uiTextBoxkz10.Text = Convert.ToString(k[9]);

                    uiTextBoxdz1.Text = Convert.ToString(d[0]);
                    uiTextBoxdz2.Text = Convert.ToString(d[1]);
                    uiTextBoxdz3.Text = Convert.ToString(d[2]);
                    uiTextBoxdz4.Text = Convert.ToString(d[3]);
                    uiTextBoxdz5.Text = Convert.ToString(d[4]);
                    uiTextBoxdz6.Text = Convert.ToString(d[5]);
                    uiTextBoxdz7.Text = Convert.ToString(d[6]);
                    uiTextBoxdz8.Text = Convert.ToString(d[7]);
                    uiTextBoxdz9.Text = Convert.ToString(d[8]);
                    uiTextBoxdz10.Text = Convert.ToString(d[9]);

                    uiTextBoxpyz1.Text = Convert.ToString(offsetValue[0]);
                    uiTextBoxpyz2.Text = Convert.ToString(offsetValue[1]);
                    uiTextBoxpyz3.Text = Convert.ToString(offsetValue[2]);
                    uiTextBoxpyz4.Text = Convert.ToString(offsetValue[3]);
                    uiTextBoxpyz5.Text = Convert.ToString(offsetValue[4]);
                    uiTextBoxpyz6.Text = Convert.ToString(offsetValue[5]);
                    uiTextBoxpyz7.Text = Convert.ToString(offsetValue[6]);
                    uiTextBoxpyz8.Text = Convert.ToString(offsetValue[7]);
                    uiTextBoxpyz9.Text = Convert.ToString(offsetValue[8]);
                    uiTextBoxpyz10.Text = Convert.ToString(offsetValue[9]);
                    

                    MessageBox.Show($"Configuration loaded from {filePath}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to load configuration: {ex.Message}");
                }
            }
        }
    }
}
