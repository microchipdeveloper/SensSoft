using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CapSens
{
    public partial class Form1 : Form
    {
        public byte[] devStatus = new byte[20];
        public byte[] cmd = new byte[64];
        public byte[] indataPort1 = new byte[255];
        byte rxdLen;
        private bool portNewData = false;
        long ElapsedTime;
        //----------------------------------------------------------------------------------------
        public Form1()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            cbPort.Items.AddRange(items: SerialPort.GetPortNames());
            if (cbPort.Items.Count > 0) cbPort.SelectedIndex = 0;

        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            try
            {
                if (serialPort1.IsOpen)
                {
                    portCloseAtrbt();
                }
                else
                {   cbPort.Text = "COM3";
                    serialPort1.PortName = cbPort.Text;
                    serialPort1.Open();
                    btnOpen.Text = "Закрыть";
                    btnOpen.BackColor = Color.Green;
                    timer1.Start();
                }
            }
            catch (Exception err)
            {
                portCloseAtrbt();
                MessageBox.Show(err.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        // Конвертеры ---------------------------------------------------------------------------------------
        private string AsciiToString(byte[] indata, int ofset, byte len)   // Конвертер ASCII в строку
        {
            String retStr = "";
            char x;
            while (indata[ofset] != 0 && len > 0)
            {
                x = (char)indata[ofset];
                if (x > 0xBF)
                {
                    x += (char)0x0350;
                }
                retStr += x;
                ofset++;
                len--;
            }
            return retStr;
        }
        private byte ChrToASCII(char symb)                                      // Конвертер Char -> ASCII
        {
            byte res;
            if (symb >= 'А')
            {
                symb -= 'А';
                symb += (char)0xC0;
            }
            res = (byte)symb;
            return res;
        }
        private string HexToStr(byte data)                                      // Конвертер HEX в 2 символьную строку + пробел
        {
            var a = data;
            char ch = '0';
            a >>= 4;
            if (a > 9)
            {
                a += 7;
                while (a-- > 0) ch++;
            }
            else ch += (char)a;
            string str = ch.ToString();
            data &= 0x0F;
            ch = '0';
            if (data > 9)
            {
                data += 7;
                while (data-- > 0) ch++;
            }
            else ch += (char)data;
            str += ch.ToString();
            return str + ' ';
        }
        //-----------------------------------------------------------------------------------------
        private byte calcCRC8(byte[] buf, byte offset, byte cnt)
        {
            byte crc = 0, i = offset;
            while (i < cnt)
            {
                crc += buf[i++];
            }
            return crc;
        }
        public byte portal_1(byte[] sendBuf, byte len, int wait)
        {
            sendBuf[len] = calcCRC8(sendBuf, 0, len);
            rxdLen = 0;
            try
            {
                serialPort1.Write(sendBuf, 0, ++len);
            }
            catch (Exception err)
            {
                portCloseAtrbt();
                MessageBox.Show(err.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Thread.Sleep(wait);
            if (rxdLen > 3 && sendBuf[1] + 0x80 == indataPort1[len + 1])
            {
                rxdLen--;
                if (calcCRC8(indataPort1, len, rxdLen) == indataPort1[rxdLen])
                {
                    //serialPort1.Write(indataPort1, len, rxdLen);
                    return indataPort1[len + 1];            // Возвращает код функции
                }
            }
            return 0;
        }
        public byte getStat(byte adr)
        {
            cmd[0] = adr;
            cmd[1] = 0x77;
            var tmout = 100;
            if (serialPort1.BaudRate < 4800) tmout = 250;
            if (portal_1(cmd, 2, tmout) == 0xF7)
            {
                return ParseStatus(3);
            }
            return 0;
        }
        private byte ParseStatus(byte ofset)
        {
            if (rxdLen > 250) rxdLen = 0;         // иногда вылетает исключение
            if (calcCRC8(indataPort1, ofset, rxdLen) == indataPort1[rxdLen])
            {
                if (indataPort1[ofset + 1] == 0xF7 || indataPort1[ofset + 1] == 0xFF)//
                {
                    for (var i = 0; i < rxdLen; i++)
                    {
                        devStatus[i] = indataPort1[i + 2 + ofset];
                    }
                    return indataPort1[ofset + 2];              // Вернуть количество байт статусного ответа
                }
            }
            /*      else if (indataPort1[ofset + 1] == 0xF6)
                  {
                      if (calcCRC8(indataPort1, ofset, 3) == indataPort1[3])
                      {
                          devStatus[1] = indataPort1[ofset + 2];
                          KpiStatInfo();
                      }
                  }*/
            return 0;
        }
        private void portCloseAtrbt()
        {
            serialPort1.Close();
            timer1.Stop();
            btnOpen.Text = "Открыть";
            btnOpen.BackColor = Color.LightGray;
            tbStatus.BackColor = Color.White;
        }
        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int len;
            try
            {
                portNewData = true;
                len = serialPort1.BytesToRead;
                serialPort1.Read(indataPort1, rxdLen, len);
                rxdLen += (byte)len;
                this.Invoke(new EventHandler(RXProcess));
            }
            catch (Exception err)
            {
                portCloseAtrbt();
                MessageBox.Show(err.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }

        private void RXProcess(object sender, EventArgs e)
        {
            ElapsedTime = Stopwatch.GetTimestamp();
                //       ElapsedTime *= ((long)1.0 / Stopwatch.Frequency);   //  - Приведение тиков к реальному времени в секундах
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (!portNewData)
            {
                while ((Stopwatch.GetTimestamp() - ElapsedTime) < 25000)
                {
                    // пустой цикл ожидания
                }
                if (getStat((byte)devAdr.Value) != 0)
                {
                    tbStatus.BackColor = Color.LimeGreen;
                }
                else tbStatus.BackColor = Color.White;
            }
            else
            {
                portNewData = false;
                rxdLen--;
                ParseStatus(3);
            }
            tbStatus.Text = "";
            var i = 1;
            var x = devStatus[0];
            while (x-- > 0 && x < devStatus.Length)
            {
                tbStatus.Text += HexToStr(devStatus[i++]);
            }

        }
    }
}
