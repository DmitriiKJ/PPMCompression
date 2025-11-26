using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ProtoBuf;

namespace PPM_Compression.Collections
{
    internal class CompressedPPM
    {
        public List<byte> bytes;
        private byte currentByte;
        private int bitPosition;
        private int readPosition;

        public int FollowBits { get; set; } = 0;
        public int BytesLen { get; set; }
        public string FileName { get; set; } = string.Empty;

        public CompressedPPM()
        {
            bytes = new List<byte>();
            bitPosition = 7;
            readPosition = 7;
            currentByte = 0;
        }

        public void AddBits(byte bit)
        {
            if (bit == 1 || bit == 0)
            {
                currentByte |= (byte)(bit << bitPosition);
                bitPosition--;
                if (bitPosition < 0)
                {
                    bytes.Add(currentByte);
                    currentByte = 0;
                    bitPosition = 7;
                }
            }
        }

        public uint GetCode()
        {
            uint res = 0;
            for (int i = 0; i < 4; i++)
            {
                res |= (uint)bytes[0] << (8 * (3 - i));
                bytes.RemoveAt(0);
            }

            return res;
        }

        public void FinalizeArr()
        {
            if (bitPosition != 7)
            {
                bytes.Add(currentByte);
                currentByte = 0;
                bitPosition = 7;
            }
        }

        public byte PopBit()
        {
            if (bytes.Count == 0)
                return 0;

            byte res = (byte)((bytes[0] >> readPosition) & 1);

            readPosition--;

            if (readPosition < 0)
            {
                readPosition = 7;
                bytes.RemoveAt(0);
            }

            return res;
        }

        public static byte[] ToBinary(CompressedPPM data)
        {
            using var ms = new MemoryStream();
            using var bw = new BinaryWriter(ms);

            bw.Write(data.BytesLen);

            bw.Write(data.bytes.Count);

            bw.Write(data.bytes.ToArray());

            bw.Write(data.FileName);

            return ms.ToArray();
        }

        public static CompressedPPM FromBinary(byte[] raw)
        {
            using var ms = new MemoryStream(raw);
            using var br = new BinaryReader(ms);

            var result = new CompressedPPM
            {
                BytesLen = br.ReadInt32()
            };

            int len = br.ReadInt32();
            result.bytes = br.ReadBytes(len).ToList();

            result.FileName = br.ReadString();

            return result;
        }
    }
}
