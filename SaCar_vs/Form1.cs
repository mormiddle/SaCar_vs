﻿using System;
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
        private const int RectWidth = 30;//矩形宽
        private const int RectMargin = 0;//间距
        double threshold = 0;//阈值
        private Thread dataThread;//创建一个新线程
        private ManualResetEvent _stopThreadEvent = new ManualResetEvent(false);//用于标定线程状态



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
                        /*uiComboBox1.Text = uiComboBox1.Items[0].ToString();*/
                        uiComboBox1.Text = "COM7";
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

            while (start + 44 <= count)
            {
                // head, tail
                if (buffer[start] != 0xAA || buffer[start + 1] != 0x29 || buffer[start + 43] != 0x80)
                {
                    start += 2;
                    continue;
                }

                // CRC8: from  start + 2  to start + 41, check by start + 42
                if (CRC8(buffer, start + 2, 40) != buffer[start + 42])
                {
                    start += 2;
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
            }

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



            this.DoubleBuffered = true;

        }


        private void uiButton1_Click(object sender, EventArgs e)
        {
            
            try
            {
                threshold = Convert.ToDouble(uiTextBox1.Text);
                uiTextBox1.Enabled = false;
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

                        chartP = 0;


                        /* 串口已经处于关闭状态，则设置好串口属性后打开 */
                        //停止串口扫描
                        timer1.Stop();
                        /*timer2.Start();*/
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
            catch (Exception)
            {

                MessageBox.Show("请输入合适阈值(数字)！");
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
                    uiTextBox1.Enabled = true;

                    serialPort1.Close();    //关闭串口
                    uiComboBox1.Enabled = true;
                    uiButton2.Enabled = false;
                    uiButton1.Enabled = true;
                    CheckedData.Clear();
                    start = 0;
                    //开启端口扫描
                    timer1.Interval = 1000;
                    timer1.Start();
                    /*timer2.Stop();*/
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

        /*       private void timer2_Tick(object sender, EventArgs e)
               {
                   while (CheckedData.Count() > chartP + 40)
                   {
                       for (int i = 0; i < 10; i++)
                       {
                           numbers[i] = ToData(CheckedData[chartP], CheckedData[chartP + 1]);
                           chartP += 4;

                       }
                       UpPanel(mypanel1, numbers[0]);
                       UpPanel(mypanel2, numbers[1]);
                       UpPanel(mypanel3, numbers[2]);
                       UpPanel(mypanel4, numbers[3]);
                       UpPanel(mypanel5, numbers[4]);
                       UpPanel(mypanel6, numbers[5]);
                       UpPanel(mypanel7, numbers[6]);
                       UpPanel(mypanel8, numbers[7]);
                       UpPanel(mypanel9, numbers[8]);
                       UpPanel(mypanel10, numbers[9]);


                       uiLabel1.Text = numbers[0].ToString("F1");
                       uiLabel2.Text = numbers[1].ToString("F1");
                       uiLabel3.Text = numbers[2].ToString("F1");
                       uiLabel4.Text = numbers[3].ToString("F1");
                       uiLabel5.Text = numbers[4].ToString("F1");
                       uiLabel6.Text = numbers[5].ToString("F1");
                       uiLabel7.Text = numbers[6].ToString("F1");
                       uiLabel8.Text = numbers[7].ToString("F1");
                       uiLabel9.Text = numbers[8].ToString("F1");
                       uiLabel10.Text = numbers[9].ToString("F1");

                   }

               }*/

       /* private async void timer2_Tick(object sender, EventArgs e)*/
       /* {
            await Task.Run(() =>
            {
                while (CheckedData.Count() > chartP + 40)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        numbers[i] = ToData(CheckedData[chartP], CheckedData[chartP + 1]);
                        chartP += 4;
                    }

                    UpPanel(mypanel1, numbers[0]);
                    UpPanel(mypanel2, numbers[1]);
                    UpPanel(mypanel3, numbers[2]);
                    UpPanel(mypanel4, numbers[3]);
                    UpPanel(mypanel5, numbers[4]);
                    UpPanel(mypanel6, numbers[5]);
                    UpPanel(mypanel7, numbers[6]);
                    UpPanel(mypanel8, numbers[7]);
                    UpPanel(mypanel9, numbers[8]);
                    UpPanel(mypanel10, numbers[9]);

                    Invoke(new Action(() =>
                    {
                        uiLabel1.Text = numbers[0].ToString("F1");
                        uiLabel2.Text = numbers[1].ToString("F1");
                        uiLabel3.Text = numbers[2].ToString("F1");
                        uiLabel4.Text = numbers[3].ToString("F1");
                        uiLabel5.Text = numbers[4].ToString("F1");
                        uiLabel6.Text = numbers[5].ToString("F1");
                        uiLabel7.Text = numbers[6].ToString("F1");
                        uiLabel8.Text = numbers[7].ToString("F1");
                        uiLabel9.Text = numbers[8].ToString("F1");
                        uiLabel10.Text = numbers[9].ToString("F1");
                    }));
                }
            });
        }*/

        private void DataThreadMethod()
        {
            while (!_stopThreadEvent.WaitOne(0))
            {
                while (CheckedData.Count() > chartP + 40)
                {
                    for (int i = 0; i < 10; i++)
                    {
                        numbers[i] = ToData(CheckedData[chartP], CheckedData[chartP + 1]);
                        chartP += 4;
                    }
                    UpPanel(mypanel1, numbers[0]);
                    UpPanel(mypanel2, numbers[1]);
                    UpPanel(mypanel3, numbers[2]);
                    UpPanel(mypanel4, numbers[3]);
                    UpPanel(mypanel5, numbers[4]);
                    UpPanel(mypanel6, numbers[5]);
                    UpPanel(mypanel7, numbers[6]);
                    UpPanel(mypanel8, numbers[7]);
                    UpPanel(mypanel9, numbers[8]);
                    UpPanel(mypanel10, numbers[9]);

                    try
                    {
                        Invoke(new Action(() =>
                        {
                            uiLabel1.Text = numbers[0].ToString("F1");
                            uiLabel2.Text = numbers[1].ToString("F1");
                            uiLabel3.Text = numbers[2].ToString("F1");
                            uiLabel4.Text = numbers[3].ToString("F1");
                            uiLabel5.Text = numbers[4].ToString("F1");
                            uiLabel6.Text = numbers[5].ToString("F1");
                            uiLabel7.Text = numbers[6].ToString("F1");
                            uiLabel8.Text = numbers[7].ToString("F1");
                            uiLabel9.Text = numbers[8].ToString("F1");
                            uiLabel10.Text = numbers[9].ToString("F1");
                        }));
                    }
                    catch (Exception)
                    {

                        throw;
                    }
                  
                }
                
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
            for (int j = 0; j < rectangles.Length; j++)
            {
                int x = j * (RectWidth + RectMargin);
                int y = 0;
                rectangles[j] = new Rectangle(x, y, RectWidth, panel.Height);
                states[j] = false; // Default state is blue
            }

            panel.Paint += (sender, e) =>
            {
                Graphics graphics = e.Graphics;

                for (int j = 0; j < rectangles.Length; j++)
                {
                    Rectangle rect = rectangles[j];

                    // Fill the rectangle with the appropriate color
                    Color color = states[j] ? Color.FromArgb(255, 69, 0): Color.FromArgb(80, 160, 255);
                    SolidBrush brush = new SolidBrush(color);
                    graphics.FillRectangle(brush, rect);

                    // Draw a border around the rectangle
                    ControlPaint.DrawBorder(graphics, rect, Color.Transparent, ButtonBorderStyle.None);
                }
            };

            panel.Tag = new Tuple<Rectangle[], bool[]>(rectangles, states);
        }

        private void UpPanel(Mypanel panel, double number)
        {
            Tuple<Rectangle[], bool[]> data = (Tuple<Rectangle[], bool[]>)panel.Tag;
            Rectangle[] rectangles = data.Item1;
            bool[] states = data.Item2;


            // Move the states of the other rectangles one position to the left
            for (int j = 1; j < rectangles.Length; j++)
            {
                states[j - 1] = states[j];
            }

            // Set the rightmost rectangle's state based on the random number
            states[rectangles.Length - 1] = number > threshold;

            panel.Invalidate();
        }

        private void StartThread()
        {
  
            _stopThreadEvent.Reset();
            dataThread = new Thread(DataThreadMethod);
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
    }
}
