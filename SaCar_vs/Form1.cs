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
        }

        private void uiButton1_Click(object sender, EventArgs e)
        {

        }

        private void uiButton2_Click(object sender, EventArgs e)
        {

        }
    }
}
