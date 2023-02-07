using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sunny.UI;

namespace SaCar_vs
{
    public partial class Form1 : UIForm
    {
        private StringBuilder sb = new StringBuilder();    //为了避免在接收处理函数中反复调用，依然声明为一个全局变量
        private DateTime current_time = new DateTime();    //为了避免在接收处理函数中反复调用，依然声明为一个全局变量
        private bool is_need_time = true;
        private List<byte> buffer = new List<byte>(); //设置缓存处理CRC32串口的校验
        public static bool intimewindowIsOpen = false; //判断波形窗口是否创建
        List<byte> CheckedData = new List<byte>();//申请一个大容量的数组
        //private List<byte> SerialPortReceiveData = new List<byte>(); //用于存储串口的数据
        string timeStart;//采集开始时间
        int start = 0;//充当指针的作用
        


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
        }


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

            #region 初始化表格
            UIBarOption option = new UIBarOption();
            option.Title = new UITitle();
            option.Title.Text = "十通道硬点值";

            //设置Legend
            option.Legend = new UILegend();
            option.Legend.Orient = UIOrient.Horizontal;
            option.Legend.Top = UITopAlignment.Top;
            option.Legend.Left = UILeftAlignment.Left;
            option.Legend.AddData("硬点值");

            var series = new UIBarSeries();
            series.Name = "Bar1";
            series.AddData(0);
            series.AddData(0);
            series.AddData(0);
            series.AddData(0);
            series.AddData(0);
            series.AddData(0);
            series.AddData(0);
            series.AddData(0);
            series.AddData(0);
            series.AddData(0);

            //数据显示小数位数
            series.DecimalPlaces = 1;
            option.Series.Add(series);


            option.XAxis.Data.Add("通道1");
            option.XAxis.Data.Add("通道2");
            option.XAxis.Data.Add("通道3");
            option.XAxis.Data.Add("通道4");
            option.XAxis.Data.Add("通道5");
            option.XAxis.Data.Add("通道6");
            option.XAxis.Data.Add("通道7");
            option.XAxis.Data.Add("通道8");
            option.XAxis.Data.Add("通道9");
            option.XAxis.Data.Add("通道10");

            option.ToolTip.Visible = true;
            option.YAxis.Scale = true;

            option.XAxis.Name = "通道";
            option.XAxis.AxisLabel.Angle = 0;//(0° ~ 90°)

            option.YAxis.Name = "数值";

            //坐标轴显示小数位数
            option.YAxis.AxisLabel.DecimalPlaces = 1;

            option.YAxisScaleLines.Add(new UIScaleLine() { Color = Color.Red, Name = "上限", Value = 12 });
            option.YAxisScaleLines.Add(new UIScaleLine() { Color = Color.Gold, Name = "下限", Value = -20 });

            option.ToolTip.AxisPointer.Type = UIAxisPointerType.Shadow;

            option.ShowValue = true;

            uiBarChart1.SetOption(option);
            #endregion



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
                    /* 串口已经处于关闭状态，则设置好串口属性后打开 */
                    //停止串口扫描
                    timer1.Stop();

                    uiComboBox1.Enabled = false;
                    serialPort1.PortName = uiComboBox1.Text;
                    serialPort1.BaudRate = Convert.ToInt32("115200");
                    serialPort1.DataBits = Convert.ToInt16("8");
                    serialPort1.Parity = System.IO.Ports.Parity.None;
                    serialPort1.StopBits = System.IO.Ports.StopBits.One;
                    //打开串口，设置状态
                    serialPort1.Open();
                    uiButton2.Enabled = true ;
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

        private void uiButton2_Click(object sender, EventArgs e)
        {
            try
            {


                //将可能产生异常的代码放置在try块中
                //根据当前串口属性来判断是否打开
                if (serialPort1.IsOpen)
                {
                    //串口已经处于打开状态

                    serialPort1.Close();    //关闭串口
                    uiComboBox1.Enabled = true;
                    uiButton2.Enabled = false;
                    uiButton1.Enabled = true;
                    CheckedData.Clear();
                    start = 0;
                    //开启端口扫描
                    timer1.Interval = 1000;
                    timer1.Start();
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
    }
}
