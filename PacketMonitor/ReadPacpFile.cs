﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

namespace ReadPcapFile
{
    public delegate void PackerHandler(Byte[] PacketData, bool isEnd);

    [StructLayout(LayoutKind.Sequential, Pack = 16)]
    public  struct PcapPacketHeader
    {
        public Int32 Timestamp;
        public Int32 Timestamp2;
        public Int32 Caplen;
        public Int32 Len;
    }

    unsafe class ReadPacpFile
    {
        public ReadPacpFile(ProgressBar e)
        {
            progressBar = e;
            files = new List<byte[]>();
            mThread = new Thread(WorkOnThread);
        }

        public bool OpenFile(List<string> FilePaths)
        {
            try
            {
                int totalBytes = 0;

                foreach(string filePath in FilePaths)
                {
                    fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite);  // open file
                    br = new BinaryReader(fs);                                           // file reader

                    byte[] fileBytes = br.ReadBytes((int)fs.Length);
                    totalBytes += fileBytes.Length;
                    files.Add(fileBytes);
                }

                progressBar.Maximum = totalBytes;
                progressBar.Value = 0;
                progressBar.Step = 1;
            }
            catch
            {
                files.Clear();
                return false;
            }          
            return true;       
        }

        public void Start()
        {
            mThread.Start();
        }

        private void WorkOnThread()
        {
            foreach (var file in files)
            {
                ALLdata = file;
                PacketData = ALLdata;

                if (ALLdata[0] == 0x0a && ALLdata[1] == 0x0d && ALLdata[2] == 0x0d && ALLdata[3] == 0x0a)  // pcapng
                {
                    ByteIndex = ALLdata[4];
                    ByteIndex = ByteIndex + ALLdata[ByteIndex + 4];
                    fixed (byte* buf = PacketData)
                    {
                        PcapngPacketHeader = buf + ByteIndex;
                        _Byte = PcapngPacketHeader + PcapngPacketHeaderLen;
                    }
                    int packetLen = 0;
                    while (ByteIndex < ALLdata.Length)
                    {
                        packetLen = PcapngPacketHeader[21] * 256 + PcapngPacketHeader[20];
                        PacketData = new byte[packetLen];
                        for (int i = 0; i < packetLen; i++)
                        {
                            PacketData[i] = _Byte[i];
                        }

                        if (PackerHandler != null)
                        {
                            PackerHandler(PacketData, false);
                        }

                        ByteIndex += PcapngPacketHeader[5] * 256 + PcapngPacketHeader[4];
                        PcapngPacketHeader = PcapngPacketHeader + PcapngPacketHeader[5] * 256 + PcapngPacketHeader[4];
                        _Byte = PcapngPacketHeader + PcapngPacketHeaderLen;
                    }
                    //PackerHandler(PacketData, true);
                }
                else // pcap
                {
                    ByteIndex = PcapFileHeaderLen;
                    fixed (byte* buf = PacketData)
                    {
                        _PcapPacketHeader = (PcapPacketHeader*)(buf + PcapFileHeaderLen);
                        _Byte = (byte*)((byte*)_PcapPacketHeader + sizeof(PcapPacketHeader));
                    }
                    while (ByteIndex < ALLdata.Length)
                    {
                        PacketData = new byte[_PcapPacketHeader->Len];
                        for (int i = 0; i < _PcapPacketHeader->Len; i++)
                        {
                            PacketData[i] = _Byte[i];
                        }

                        if (PackerHandler != null)
                        {
                            PackerHandler(PacketData, false);
                        }

                        ByteIndex += _PcapPacketHeader->Len + sizeof(PcapPacketHeader);
                        _PcapPacketHeader = (PcapPacketHeader*)(_Byte + _PcapPacketHeader->Len);
                        _Byte = (byte*)((byte*)_PcapPacketHeader + sizeof(PcapPacketHeader));
                    }                   
                    //PackerHandler(PacketData, true);
                }
                
            }

            files.Clear();
            PackerHandler(PacketData, true);  // 表明結束
        }

        public PackerHandler PackerHandler = null;

        private ProgressBar progressBar;
        private List<byte[]> files;
        private FileStream fs;
        private BinaryReader br;
        PcapPacketHeader* _PcapPacketHeader;
        byte* PcapngPacketHeader;
        private byte* _Byte;
        private byte[] ALLdata;
        private byte[] PacketData;
        private const int PcapFileHeaderLen = 24;
        private const int PcapngPacketHeaderLen = 28;
        private int ByteIndex;
        private Thread mThread;
    }
}
