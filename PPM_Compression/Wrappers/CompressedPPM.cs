using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace PPM_Compression.Collections
{
    [ProtoContract]
    internal class CompressedPPM
    {
        [ProtoMember(1)]
        public List<byte> bytes;
        [ProtoMember(2)]
        private byte currentByte;
        [ProtoMember(3)]
        private int bitPosition;
        [ProtoMember(4)]
        private int readPosition;
        [ProtoMember(5)]
        public int BytesLen { get; set; }

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
            Serializer.Serialize(ms, data);
            return ms.ToArray();
        }

        public static CompressedPPM FromBinary(byte[] raw)
        {
            using var ms = new MemoryStream(raw);
            return Serializer.Deserialize<CompressedPPM>(ms);
        }
    }
}
